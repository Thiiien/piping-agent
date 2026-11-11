using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using PipingAgent.AutoCAD.Session;
using PipingAgent.Core;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Source;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>Lệnh thử của việc S0-10, giữ lại để kiểm tra nhanh việc nạp.</summary>
    public sealed class HelloCommand : CommandBase
    {
        protected override string Name => "PA_HELLO";

        [CommandMethod("PA_HELLO")]
        public void Hello() => Execute();

        protected override void Run(CommandContext context)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var blockTable = (BlockTable)context.Transaction.GetObject(context.Database.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)context.Transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var total = 0;
            foreach (ObjectId id in modelSpace)
            {
                var kind = id.ObjectClass.DxfName ?? id.ObjectClass.Name;
                counts[kind] = counts.TryGetValue(kind, out var current) ? current + 1 : 1;
                total++;
            }

            var report = new StringBuilder();
            report.Append("Piping Agent ").Append(PluginApp.Version).Append(", lược đồ IR ").Append(CoreInfo.IrSchemaVersion)
                .Append(", nền ").Append(RuntimeInformation.FrameworkDescription);
            report.Append("\nKhông gian mô hình có ").Append(total).Append(" đối tượng:");
            foreach (var pair in counts) report.Append("\n  ").Append(pair.Key).Append(": ").Append(pair.Value);
            context.Write(report.ToString());
        }
    }

    /// <summary>
    /// PA_SCAN: đọc bản vẽ thành đối tượng trung lập, in bản tóm tắt (S1-13), rồi chạy chuỗi nhận diện của lõi
    /// và hiện kết quả ở bảng điều khiển (S2-12). Tập chọn trước thì chỉ đọc tập đó (S1-14). Có tiến độ và hủy (S2-14).
    /// PA_CONVERT là tên theo quy trình UC-01 của cùng lệnh, S4-15.
    /// </summary>
    public sealed class ScanCommand : CommandBase
    {
        protected override string Name => "PA_SCAN";

        [CommandMethod("PA_SCAN", CommandFlags.UsePickSet)]
        public void Scan() => Execute();

        [CommandMethod("PA_CONVERT", CommandFlags.UsePickSet)]
        public void Convert() => Execute();

        protected override void Run(CommandContext context)
        {
            var selected = SelectedHandles(context.Editor);
            var reader = new AcadDrawingReader(context.Database, context.Transaction, onlyHandles: selected);
            var entities = Report(context, reader, selected == null ? "cả không gian mô hình" : $"{selected.Count} đối tượng đã chọn", null);
            Recognize(context, reader, entities);
        }

        /// <summary>Chạy lõi trên các đối tượng vừa đọc, lưu vào phiên của bản vẽ, in báo cáo và cập nhật bảng. S2-12, S2-14.</summary>
        internal static void Recognize(CommandContext context, AcadDrawingReader reader, IReadOnlyList<SourceEntity> entities)
        {
            var sessions = PluginApp.Sessions;
            if (sessions == null) return;
            var session = sessions.GetOrCreate(context.Document);
            if (session.Profile == null && session.ProfilePath != null) session.LoadProfile();
            if (session.Profile == null)
            {
                context.Write("Piping Agent: chưa có hồ sơ quy ước nào nạp được, bỏ qua bước nhận diện. Chọn hồ sơ ở thẻ Hồ sơ của PA_PANEL.");
                foreach (var e in session.ProfileErrors.Take(5)) context.Write("  " + e);
                return;
            }

            var summary = reader.GetSummary();
            var source = new SourceInfo
            {
                File = session.DrawingName,
                Fingerprint = summary.Fingerprint,
                ReadAt = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
                EntityCount = entities.Count,
                UnitsNeedConfirmation = reader.Units.NeedsConfirmation,
            };
            if (session.SessionLoadError != null) context.Write($"Piping Agent: tệp phiên hỏng, bắt đầu phiên trống ({session.SessionLoadError}).");

            var result = RunPipeline("Piping Agent đang nhận diện", entities, session, source);
            session.StoreScan(entities, result, reader.ScaleToMm, summary.Fingerprint);
            try
            {
                if (session.SaveSessionFile()) context.Write($"Piping Agent: đã lưu phiên vào {session.SessionPath}.");
            }
            catch (System.Exception ex)
            {
                context.Write($"Piping Agent: không ghi được tệp phiên ({ex.Message}).");
            }

            context.Write(result.Cancelled ? "Piping Agent: đã hủy nhận diện theo yêu cầu.\n" : RecognitionReport.ToText(result, maxLayers: 8, maxIssues: 8));
            if (PanelHost.IsOpen) PanelHost.ShowSession(session);

            var r = result.Classification.Report;
            context.Log?.Info("recognize", new Dictionary<string, object?>
            {
                ["drawing"] = session.DrawingName,
                ["profile"] = session.Profile.Id,
                ["entities"] = r.Total,
                ["unclassified"] = r.Unclassified,
                ["runs"] = result.Model.Runs.Count,
                ["tagged"] = result.Model.Runs.Count(x => x.TagRef != null),
                ["issues"] = result.Model.Issues.Count,
                ["blockers"] = result.Model.Issues.Count(i => i.Severity == Severity.Blocker),
                ["cancelled"] = result.Cancelled,
                ["elapsedMs"] = (long)result.Elapsed.TotalMilliseconds,
            });
        }

        /// <summary>Chạy chuỗi nhận diện với thước tiến độ và nút Hủy của bảng; dùng cho quét lần đầu và chạy lại. Gọi khi phiên đã có hồ sơ.</summary>
        internal static RecognitionResult RunPipeline(string title, IReadOnlyList<SourceEntity> entities, DocumentSession session, SourceInfo source)
        {
            var sessions = PluginApp.Sessions!;
            var model = PanelHost.Model;
            using (var progress = new AcadProgress(title, info => model?.ReportProgress(info)))
            {
                PanelHost.SetRunning(progress);
                try
                {
                    return RecognitionPipeline.Run(entities, session.Profile!, sessions.Catalog, source, sessions.Equipment, progress, session.Decisions, progress.Token);
                }
                finally
                {
                    PanelHost.SetRunning(null);
                }
            }
        }

        internal static List<string>? SelectedHandles(Editor editor)
        {
            var implied = editor.SelectImplied();
            if (implied.Status != PromptStatus.OK || implied.Value.Count == 0) return null;
            editor.SetImpliedSelection(Array.Empty<ObjectId>());
            return implied.Value.GetObjectIds().Select(id => id.Handle.ToString()).ToList();
        }

        internal static IReadOnlyList<SourceEntity> Report(CommandContext context, AcadDrawingReader reader, string scope, Box2? region)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var entities = reader.ReadEntities(new EntityFilter { Region = region });
            var summary = reader.GetSummary();
            watch.Stop();

            var sb = new StringBuilder();
            sb.Append($"Piping Agent quét {scope} trong {watch.ElapsedMilliseconds} ms.");
            sb.Append($"\nĐơn vị: {reader.Units.Note} Hệ số ra mm: {reader.ScaleToMm.ToString(CultureInfo.InvariantCulture)}.");
            if (reader.Units.NeedsConfirmation) sb.Append("\n  CẦN XÁC NHẬN đơn vị trước khi nhận diện.");
            sb.Append($"\nĐối tượng đọc được: {entities.Count}");
            if (region.HasValue) sb.Append($" trong vùng {region.Value}");
            sb.Append($". Bỏ qua: {reader.Skipped.Count}.");
            sb.Append("\nTheo loại:");
            foreach (var pair in entities.GroupBy(e => e.Kind).OrderByDescending(g => g.Count()))
            {
                sb.Append($"\n  {pair.Key,-12} {pair.Count(),6}");
            }

            sb.Append($"\nLayer có đối tượng: {summary.Layers.Count(l => l.EntityCount > 0)} trên {summary.Layers.Count}. Nhiều nhất:");
            foreach (var layer in summary.Layers.Where(l => l.EntityCount > 0).OrderByDescending(l => l.EntityCount).Take(10))
            {
                sb.Append($"\n  {layer.Name,-24} {layer.EntityCount,6}  màu {layer.Color?.ToString() ?? "-",3}  {layer.Linetype}");
            }

            sb.Append($"\nBlock được chèn: {summary.Blocks.Count(b => b.InsertCount > 0)} loại, {summary.Blocks.Sum(b => b.InsertCount)} lần. Nhiều nhất:");
            foreach (var block in summary.Blocks.Where(b => b.InsertCount > 0).Take(10))
            {
                sb.Append($"\n  {block.Name,-28} x{block.InsertCount,5}  {block.EntityCount,4} đối tượng{(block.HasAttributes ? ", có thuộc tính" : string.Empty)}");
            }

            if (summary.Extents.HasValue) sb.Append($"\nPhạm vi: {summary.Extents.Value}");
            sb.Append($"\nXref: {(summary.HasXrefs ? "có" : "không")}. Proxy: {(summary.HasProxyEntities ? "có" : "không")}.");
            sb.Append($"\nDấu vân tay: {summary.Fingerprint}");
            foreach (var skipped in reader.Skipped.Take(5)) sb.Append("\n  Bỏ qua: ").Append(skipped);
            context.Write(sb.ToString() + "\n");

            context.Log?.Info("scan", new Dictionary<string, object?>
            {
                ["scope"] = scope,
                ["entities"] = entities.Count,
                ["skipped"] = reader.Skipped.Count,
                ["layers"] = summary.Layers.Count,
                ["blocks"] = summary.Blocks.Count,
                ["unitsNeedConfirmation"] = reader.Units.NeedsConfirmation,
                ["fingerprint"] = summary.Fingerprint,
                ["elapsedMs"] = watch.ElapsedMilliseconds,
            });
            return entities;
        }
    }

    /// <summary>PA_SCANWINDOW: chỉ đọc đối tượng trong cửa sổ hai điểm. Việc S1-14.</summary>
    public sealed class ScanWindowCommand : CommandBase
    {
        protected override string Name => "PA_SCANWINDOW";

        [CommandMethod("PA_SCANWINDOW")]
        public void ScanWindow() => Execute();

        protected override void Run(CommandContext context)
        {
            var first = context.Editor.GetPoint("\nGóc thứ nhất của vùng quét: ");
            if (first.Status != PromptStatus.OK) return;
            var second = context.Editor.GetCorner("\nGóc đối diện: ", first.Value);
            if (second.Status != PromptStatus.OK) return;

            var reader = new AcadDrawingReader(context.Database, context.Transaction);
            var scale = reader.ScaleToMm;
            var region = Box2.FromPoints(
                new Point2(first.Value.X * scale, first.Value.Y * scale),
                new Point2(second.Value.X * scale, second.Value.Y * scale));
            var entities = ScanCommand.Report(context, reader, "vùng cửa sổ", region);
            ScanCommand.Recognize(context, reader, entities);
        }
    }

    /// <summary>
    /// PA_PANEL: mở bảng điều khiển. Các kiểu của AcMgd.dll chỉ xuất hiện trong <see cref="PanelHost"/>,
    /// để lớp này vẫn nạp được trong accoreconsole, nơi lệnh sẽ báo không có giao diện thay vì làm hỏng plug-in.
    /// </summary>
    public sealed class PanelCommand : CommandBase
    {
        protected override string Name => "PA_PANEL";

        [CommandMethod("PA_PANEL")]
        public void Panel() => Execute();

        protected override void Run(CommandContext context)
        {
            // accoreconsole không nạp AcMgd.dll. Gọi PaletteSet ở đó làm sập cả tiến trình, không bắt được bằng try/catch,
            // nên phải kiểm tra trước. Đã xác nhận bằng thực nghiệm ngày 10/11/2026.
            if (!PanelHost.IsGuiAvailable())
            {
                context.Write("Piping Agent: môi trường này không có giao diện đồ hoạ, không mở được bảng điều khiển.");
                return;
            }

            PanelHost.Show();
            context.Write("Piping Agent: đã mở bảng điều khiển.");
        }
    }
}
