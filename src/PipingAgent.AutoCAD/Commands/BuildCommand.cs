using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using PipingAgent.AutoCAD.Builder;
using PipingAgent.Core.Build;
using PipingAgent.Core.Rules;
using PipingAgent.Core.Ir;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>
    /// PA_BUILD: dựng ống, co, tê dạng solid từ IR của lần quét gần nhất, S3-10 đến S3-17.
    /// Từ khoá: Xemtruoc chỉ in số lượng, Moi dựng vào bản vẽ mới có tham chiếu bản 2D làm nền, Hientai dựng vào bản đang mở.
    /// Cả lần dựng nằm trong một giao dịch của một lệnh nên một lần Undo đưa bản vẽ về trước; lỗi giữa chừng huỷ giao dịch.
    /// </summary>
    public sealed class BuildCommand : CommandBase
    {
        protected override string Name => "PA_BUILD";

        protected override bool ReadOnly => false;

        [CommandMethod("PA_BUILD")]
        public void Build() => Execute();

        protected override void Run(CommandContext context)
        {
            var session = PluginApp.Sessions?.GetOrCreate(context.Document);
            var model = session?.LastResult?.Model;
            if (session == null || model == null)
            {
                context.Write("Piping Agent: chưa có kết quả nhận diện. Chạy PA_SCAN trước.");
                return;
            }

            if (model.Segments.Count == 0)
            {
                context.Write("Piping Agent: IR chưa có đoạn ống nào để dựng. Xem thẻ Vấn đề, thường là thiếu cao độ hoặc DN.");
                return;
            }

            var blockers = model.Issues.Count(i => i.Severity == Severity.Blocker && i.Status == IssueStatus.Open);
            var prompt = new PromptKeywordOptions("\nDựng mô hình 3D [Xemtruoc/Moi/Hientai/Loithu] ") { AllowNone = true };
            prompt.Keywords.Add("Xemtruoc");
            prompt.Keywords.Add("Moi");
            prompt.Keywords.Add("Hientai");
            prompt.Keywords.Add("Loithu");
            prompt.Keywords.Default = "Xemtruoc";
            var choice = context.Editor.GetKeywords(prompt);
            if (choice.Status != PromptStatus.OK && choice.Status != PromptStatus.None) return;
            var mode = choice.Status == PromptStatus.None ? "Xemtruoc" : choice.StringResult;

            var options = new BuildOptions { Target = mode == "Moi" ? BuildTarget.NewDrawing : BuildTarget.CurrentDrawing };
            if (mode == "Loithu") options.FailAfterItems = 3;
            var plan = BuildPlan.FromModel(model, options);
            plan.AddExtras(model, options, PluginApp.Sessions?.Equipment, session.Profile?.Tolerances.DefaultSectionSize ?? 300);
            context.Write($"Piping Agent: kế hoạch dựng {plan.Summary()}. {blockers} vấn đề mức chặn đang mở.");
            foreach (var s in plan.Skipped.Take(5)) context.Write("  Bỏ qua " + s);
            if (mode == "Xemtruoc") return;
            if (blockers > 0 && mode != "Loithu")
            {
                context.Write($"Piping Agent: còn {blockers} vấn đề mức chặn, chưa dựng được (FR-CHK-02). Xem thẻ Vấn đề hoặc chạy PA_CHECK, sửa hoặc miễn trừ rồi dựng lại.");
                foreach (var b in model.Issues.Where(i => i.Severity == Severity.Blocker && i.Status == IssueStatus.Open).Take(5)) context.Write($"  {b.RuleId} {b.Message}");
                return;
            }

            var fingerprintBefore = session.LastFingerprint;
            BuildStats stats;
            if (mode == "Moi")
            {
                stats = BuildIntoNewDrawing(context, session, plan, options, out var path);
                context.Write($"Piping Agent: đã dựng {stats} vào {path}. Bản 2D gốc không bị thay đổi.");
                foreach (var f in stats.Failures.Take(5)) context.Write("  Lỗi " + f);
                OpenIfGui(path);
            }
            else
            {
                var builder = new AcadBuilder(context.Database, context.Transaction, session.ScaleToMm);
                stats = builder.Build(plan, options);
                PresentAs3D(context.Database, context.Transaction, context.Editor);
                if (PanelHost.IsGuiAvailable()) QueueShaded(context.Document);
                context.Write($"Piping Agent: đã dựng {stats} vào bản vẽ hiện tại trên layer riêng. Một lần Undo sẽ bỏ toàn bộ.");
                foreach (var f in stats.Failures.Take(5)) context.Write("  Lỗi " + f);
            }

            context.Log?.Info("build", new Dictionary<string, object?>
            {
                ["mode"] = mode,
                ["pipes"] = stats.Pipes,
                ["elbows"] = stats.Elbows,
                ["tees"] = stats.Tees,
                ["layers"] = stats.Layers,
                ["failures"] = stats.Failures.Count,
                ["skipped"] = plan.Skipped.Count,
                ["fingerprintBefore"] = fingerprintBefore,
            });
        }

        private static BuildStats BuildIntoNewDrawing(CommandContext context, Session.DocumentSession session, BuildPlan plan, BuildOptions options, out string path)
        {
            var sourcePath = context.Document.Name;
            var dir = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(dir) || !File.Exists(sourcePath))
            {
                throw new InvalidOperationException("Bản vẽ 2D chưa được lưu, không tham chiếu làm nền được. Lưu bản vẽ rồi chạy lại, hoặc chọn Hientai.");
            }

            path = Path.Combine(dir!, Path.GetFileNameWithoutExtension(sourcePath) + "-3D.dwg");
            using (var db = new Database(true, true))
            {
                db.Insunits = context.Database.Insunits;
                // Solid và region chỉ tạo được trong cơ sở dữ liệu đang làm việc, nên tạm chuyển sang bản vẽ mới.
                var previous = HostApplicationServices.WorkingDatabase;
                HostApplicationServices.WorkingDatabase = db;
                try
                {
                    return BuildInto(context, session, plan, options, db, sourcePath, path);
                }
                finally
                {
                    HostApplicationServices.WorkingDatabase = previous;
                }
            }
        }

        private static BuildStats BuildInto(CommandContext context, Session.DocumentSession session, BuildPlan plan, BuildOptions options, Database db, string sourcePath, string path)
        {
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // Tham chiếu ngoài chỉ nhận tệp DWG. Không gắn được thì vẫn dựng, chỉ không có nền.
                    // Tờ isometric là giấy, không trùng hệ toạ độ với mô hình nên không làm nền.
                    if (session.FromIsometric)
                    {
                        context.Write("Piping Agent: nguồn là tờ isometric nên bản 3D không gắn nền 2D.");
                    }
                    else if (string.Equals(Path.GetExtension(sourcePath), ".dwg", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var xrefId = db.AttachXref(sourcePath, "NEN_2D");
                            if (!xrefId.IsNull)
                            {
                                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                                var br = new BlockReference(Autodesk.AutoCAD.Geometry.Point3d.Origin, xrefId);
                                ms.AppendEntity(br);
                                tr.AddNewlyCreatedDBObject(br, true);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            context.Write($"Piping Agent: không tham chiếu được bản 2D làm nền ({ex.Message}), bản 3D không có nền.");
                        }
                    }
                    else
                    {
                        context.Write("Piping Agent: bản 2D không phải DWG nên không tham chiếu làm nền được.");
                    }

                    var builder = new AcadBuilder(db, tr, session.ScaleToMm);
                    var stats = builder.Build(plan, options);
                    PresentAs3D(db, tr, null);
                    tr.Commit();
                    db.SaveAs(path, DwgVersion.Current);
                    return stats;
                }
            }
        }

        /// <summary>
        /// Cho bản 3D hiện ra là khối tô bóng chứ không phải bốn nét sinh của mỗi ống: mặt cong mịn (ISOLINES, FACETRES, DISPSILH),
        /// góc nhìn SW Isometric, kiểu hiển thị Shaded, thu phóng vừa mô hình. Ghi vào bảng viewport của bản vẽ để mở lại vẫn vậy;
        /// có <paramref name="editor"/> thì đổi luôn khung nhìn đang mở.
        /// </summary>
        private static void PresentAs3D(Database db, Transaction tr, Editor? editor)
        {
            db.Isolines = 24;
            db.Facetres = 10;
            try { Autodesk.AutoCAD.ApplicationServices.Core.Application.SetSystemVariable("DISPSILH", 1); } catch (System.Exception) { /* biến không có trong accoreconsole cũ thì bỏ qua */ }

            var vsId = ObjectId.Null;
            var vsDict = (DBDictionary)tr.GetObject(db.VisualStyleDictionaryId, OpenMode.ForRead);
            foreach (var name in new[] { "Shaded", "Realistic", "Conceptual" })
            {
                if (vsDict.Contains(name)) { vsId = vsDict.GetAt(name); break; }
            }

            db.UpdateExt(true);
            var min = db.Extmin;
            var max = db.Extmax;
            var span = max - min;
            var ok = span.Length > 1e-6 && span.Length < 1e9;
            var center = ok ? min + span * 0.5 : Autodesk.AutoCAD.Geometry.Point3d.Origin;
            var size = ok ? span.Length * 1.1 : 10000;
            var dir = new Autodesk.AutoCAD.Geometry.Vector3d(-1, -1, 1);

            var vt = (ViewportTable)tr.GetObject(db.ViewportTableId, OpenMode.ForWrite);
            var ids = new List<ObjectId>();
            foreach (ObjectId id in vt) ids.Add(id);
            if (ids.Count == 0)
            {
                // Bản vẽ mới tạo bằng Database(true, true) chưa có viewport nào; AutoCAD sẽ tạo *Active mặc định khi mở nên phải tạo trước.
                var created = new ViewportTableRecord { Name = "*Active" };
                ids.Add(vt.Add(created));
                tr.AddNewlyCreatedDBObject(created, true);
            }

            foreach (var id in ids)
            {
                var vtr = (ViewportTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                vtr.ViewDirection = dir;
                vtr.Target = center;
                vtr.CenterPoint = Autodesk.AutoCAD.Geometry.Point2d.Origin;
                vtr.Height = size;
                vtr.Width = size * 1.5;
                vtr.GridEnabled = false;
                if (!vsId.IsNull) vtr.VisualStyleId = vsId;
            }

            if (editor == null) return;
            using (var view = editor.GetCurrentView())
            {
                view.ViewDirection = dir;
                view.Target = center;
                view.CenterPoint = Autodesk.AutoCAD.Geometry.Point2d.Origin;
                view.Height = size;
                view.Width = size * 1.5;
                if (!vsId.IsNull) view.VisualStyleId = vsId;
                editor.SetCurrentView(view);
            }
        }

        private static void OpenIfGui(string path)
        {
            if (!PanelHost.IsGuiAvailable()) return;
            try
            {
                GuiOpen(path);
            }
            catch (System.Exception)
            {
                // Không mở được thì người dùng mở tay, tệp đã ghi xong.
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void QueueShaded(Autodesk.AutoCAD.ApplicationServices.Document doc) =>
            doc.SendStringToExecute("_.VSCURRENT _Shaded ", true, false, false);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void GuiOpen(string path)
        {
            var opened = Autodesk.AutoCAD.ApplicationServices.DocumentCollectionExtension.Open(Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager, path, false);
            // Bảng viewport ghi trong tệp không được AutoCAD giữ khi lưu từ cơ sở dữ liệu phụ, nên xếp hàng lệnh xem cho bản vẽ vừa mở:
            // góc nhìn SW Isometric, tô bóng, thu phóng vừa mô hình. Lệnh chạy khi bản vẽ đó thành bản đang hoạt động.
            opened?.SendStringToExecute("_.-VIEW _SWISO _.VSCURRENT _Shaded _.ZOOM _E ", true, false, false);
        }
    }
}
