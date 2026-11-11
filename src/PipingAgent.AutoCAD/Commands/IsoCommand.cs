using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Rules;
using PipingAgent.Core.Source;
using PipingAgent.Iso;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>
    /// PA_ISO: đọc một tờ isometric ISOGEN DWG, DXF hoặc PDF ngay trong AutoCAD, I1 và I3. Bước 1 kiểm kê và ghi
    /// <c>&lt;tên&gt;.kiem-ke.md</c> cạnh tờ; bước 2 chỉ nạp IR vào phiên để PA_BUILD dựng khi kiểm kê không còn khoảng trống,
    /// trừ khi người dùng chọn Dungvan. Enter không nhập đường dẫn thì lấy chính bản vẽ đang mở, để mở tờ ISO rồi gõ PA_ISO, PA_BUILD.
    /// </summary>
    public sealed class IsoCommand : CommandBase
    {
        protected override string Name => "PA_ISO";

        [CommandMethod("PA_ISO")]
        public void Iso() => Execute();

        protected override void Run(CommandContext context)
        {
            var session = PluginApp.Sessions?.GetOrCreate(context.Document);
            if (session == null) return;

            var current = context.Document.Name;
            var hint = File.Exists(current) ? $" <Enter = {Path.GetFileName(current)}>" : string.Empty;
            var answer = context.Editor.GetString(new PromptStringOptions($"\nĐường dẫn tờ isometric DWG, DXF hoặc PDF{hint}: ") { AllowSpaces = true });
            if (answer.Status != PromptStatus.OK && answer.Status != PromptStatus.None) return;
            var path = string.IsNullOrWhiteSpace(answer.StringResult) ? current : Path.GetFullPath(answer.StringResult.Trim().Trim('"'));
            if (!File.Exists(path))
            {
                context.Write($"Piping Agent: không thấy tệp {path}. Lưu bản vẽ đang mở hoặc nhập đường dẫn đầy đủ.");
                return;
            }

            // Hồ sơ của phiên là hồ sơ mặt bằng 2D; tờ iso dùng hồ sơ iso-default như CLI để hai đường cho cùng kết quả.
            var run = IsoPipeline.Run(path, PluginApp.Sessions?.Catalog);
            var r = run.Analysis;
            context.Write($"Piping Agent: {Path.GetFileName(path)} {(run.IsDwg ? "DWG/DXF" : "PDF")}{(run.Isogen ? " kiểu ISOGEN" : "")}, tuyến {r.LineTag}, DN{r.Dn}, nét ống {r.Segments.Count(s => !s.Bridged)}, góc co {r.Corners}, neo {r.Anchors.Count}.");
            context.Write($"Piping Agent: đồ thị 3D {run.GraphStats}; cấu kiện {run.Model.Components.Count}, bảng vật tư {r.Bom.Count} dòng.");

            var reportPath = IsoPipeline.ReportPathFor(path);
            File.WriteAllText(reportPath, run.Extraction.ToMarkdown());
            var ex = run.Extraction;
            context.Write($"Piping Agent: kiểm kê {(ex.Complete ? "đủ để dựng" : ex.Gaps + " khoảng trống")}, {ex.Warnings} điểm cần xem. Đã ghi {reportPath}.");
            foreach (var c in ex.Checks.Where(c => c.Status != "ok").Take(8))
            {
                context.Write($"  [{(c.Status == "gap" ? "THIẾU" : "xem")}] {c.Group}: {c.Subject}, bản vẽ {c.Expected}, đọc được {c.Found}{(c.Note != null ? ", " + c.Note : "")}");
            }

            foreach (var i in run.Model.Issues.Where(i => i.Severity == Severity.Blocker && i.Status == IssueStatus.Open).Take(5))
            {
                context.Write($"  [chặn] {i.RuleId} {i.Message}");
            }

            if (!ex.Complete)
            {
                // Quy tắc hai bước: còn khoảng trống thì chưa dựng. Người dùng có thể ép qua, và việc ép được ghi nhật ký.
                var prompt = new PromptKeywordOptions("\nKiểm kê còn khoảng trống. Vẫn nạp để dựng? [Dungvan/Thoi] ") { AllowNone = true };
                prompt.Keywords.Add("Dungvan");
                prompt.Keywords.Add("Thoi");
                prompt.Keywords.Default = "Thoi";
                var k = context.Editor.GetKeywords(prompt);
                if (k.Status != PromptStatus.OK || k.StringResult != "Dungvan")
                {
                    context.Write("Piping Agent: chưa nạp IR. Xem bản kiểm kê, bổ sung tờ hoặc sửa, rồi chạy lại PA_ISO.");
                    return;
                }
            }

            var irPath = IsoPipeline.IrPathFor(path);
            File.WriteAllText(irPath, PipingAgent.Core.Ir.Json.IrJson.Serialize(run.Model));
            session.StoreIso(run.Model);
            context.Write($"Piping Agent: đã nạp IR {Path.GetFileName(irPath)}: {run.Model.Segments.Count} đoạn ống, {run.Model.Nodes.Count} nút, {run.Model.Components.Count} cấu kiện. Gõ PA_BUILD rồi chọn Hientai hoặc Moi để dựng 3D.");
            context.Log?.Info("iso", new Dictionary<string, object?>
            {
                ["file"] = Path.GetFileName(path),
                ["isogen"] = run.Isogen,
                ["gaps"] = ex.Gaps,
                ["warnings"] = ex.Warnings,
                ["forced"] = !ex.Complete,
                ["segments"] = run.Model.Segments.Count,
                ["components"] = run.Model.Components.Count,
            });
        }
    }
}
