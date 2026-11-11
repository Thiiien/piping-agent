using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Rules;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>PA_CONVERT: tên theo quy trình UC-01, làm đúng việc của PA_SCAN: đọc, nhận diện, chạy luật, hiện bảng. S4-15.</summary>
    public sealed class ConvertCommand : CommandBase
    {
        protected override string Name => "PA_CONVERT";

        [CommandMethod("PA_CONVERT", CommandFlags.UsePickSet)]
        public void Convert() => Execute();

        protected override void Run(CommandContext context)
        {
            var selected = ScanCommand.SelectedHandles(context.Editor);
            var reader = new AcadDrawingReader(context.Database, context.Transaction, onlyHandles: selected);
            var entities = ScanCommand.Report(context, reader, selected == null ? "cả không gian mô hình" : $"{selected.Count} đối tượng đã chọn", null);
            ScanCommand.Recognize(context, reader, entities);
        }
    }

    /// <summary>
    /// PA_CHECK: chạy lại bộ luật trên IR của lần quét gần nhất và in tóm tắt vấn đề theo mức, S4-15.
    /// Vấn đề do các luật mặc định tạo ra được thay mới, vấn đề do bộ nhận diện tạo ra giữ nguyên.
    /// </summary>
    public sealed class CheckCommand : CommandBase
    {
        protected override string Name => "PA_CHECK";

        [CommandMethod("PA_CHECK")]
        public void Check() => Execute();

        protected override void Run(CommandContext context)
        {
            var session = PluginApp.Sessions?.GetOrCreate(context.Document);
            var result = session?.LastResult;
            if (session == null || result == null || session.Profile == null)
            {
                context.Write("Piping Agent: chưa có kết quả nhận diện. Chạy PA_CONVERT trước.");
                return;
            }

            var ruleIds = new HashSet<string>(RuleRunner.DefaultRules.Select(r => r.Id));
            result.Model.Issues.RemoveAll(i => ruleIds.Contains(i.RuleId) && i.Status == IssueStatus.Open);
            result.Rules = RuleRunner.Run(result.Model, session.Profile);

            var open = result.Model.Issues.Where(i => i.Status == IssueStatus.Open).ToList();
            var blockers = open.Count(i => i.Severity == Severity.Blocker);
            context.Write($"Piping Agent kiểm tra: {result.Rules}. Đang mở {open.Count} vấn đề: {blockers} chặn, {open.Count(i => i.Severity == Severity.Warning)} cảnh báo, {open.Count(i => i.Severity == Severity.Info)} thông tin.");
            foreach (var g in open.GroupBy(i => i.RuleId).OrderBy(g => g.Min(i => i.Severity)).ThenBy(g => g.Key))
            {
                var def = RuleCatalog.Find(g.Key);
                context.Write($"  {RecognitionReport.SeverityText(g.First().Severity),-9} {g.Key} x{g.Count()}  {def?.Description}");
            }

            context.Write(blockers > 0 ? $"Còn {blockers} vấn đề chặn, PA_BUILD bị khoá cho đến khi sửa hoặc miễn trừ." : "Không còn vấn đề chặn, có thể PA_BUILD.");
            if (PanelHost.IsOpen) PanelHost.ShowSession(session);
            context.Log?.Info("check", new Dictionary<string, object?> { ["open"] = open.Count, ["blockers"] = blockers, ["rulesRun"] = result.Rules.RulesRun });
        }
    }
}

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>PA_BUILDIR: nạp một tệp IR JSON, ví dụ từ `PipingAgent.Cli iso`, vào phiên của bản vẽ rồi gọi PA_BUILD. Dùng cho isometric I1.</summary>
    public sealed class BuildIrCommand : CommandBase
    {
        protected override string Name => "PA_BUILDIR";

        [CommandMethod("PA_BUILDIR")]
        public void BuildIr() => Execute();

        protected override void Run(CommandContext context)
        {
            var session = PluginApp.Sessions?.GetOrCreate(context.Document);
            if (session == null) return;
            var path = FixCommand.Ask(context, "\nĐường dẫn tệp IR JSON: ");
            if (path == null) return;
            var full = System.IO.Path.GetFullPath(path.Trim('"'));
            var model = PipingAgent.Core.Ir.Json.IrJson.Deserialize(System.IO.File.ReadAllText(full));
            session.StoreIso(model);
            context.Write($"Piping Agent: đã nạp IR {System.IO.Path.GetFileName(full)}: {model.Segments.Count} đoạn ống, {model.Nodes.Count} nút, {model.Issues.Count} vấn đề. Gõ PA_BUILD để dựng.");
        }
    }
}
