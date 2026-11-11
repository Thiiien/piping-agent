using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using PipingAgent.AutoCAD.Session;
using PipingAgent.Core.Decisions;
using PipingAgent.Core.Ir;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>
    /// PA_FIX: thao tác sửa kết quả nhận diện, S5-08 và S5-09. Mỗi thao tác tạo một quyết định lưu vào tệp phiên,
    /// rồi chạy lại nhận diện và bộ luật trên các đối tượng đã đọc (S5-14). Nhãn gắn theo handle nguồn nên vẫn áp khi chạy lại.
    /// </summary>
    public sealed class FixCommand : CommandBase
    {
        protected override string Name => "PA_FIX";

        [CommandMethod("PA_FIX")]
        public void Fix() => Execute();

        protected override void Run(CommandContext context)
        {
            var session = PluginApp.Sessions?.GetOrCreate(context.Document);
            if (session?.LastResult == null || session.LastEntities == null)
            {
                context.Write("Piping Agent: chưa có kết quả nhận diện. Chạy PA_CONVERT trước.");
                return;
            }

            var prompt = new PromptKeywordOptions("\nSửa gì [Nhan/Dn/Caodo/Nut/Caukien/Noi/Tach/Boqua] ");
            foreach (var k in new[] { "Nhan", "Dn", "Caodo", "Nut", "Caukien", "Noi", "Tach", "Boqua" }) prompt.Keywords.Add(k);
            var choice = context.Editor.GetKeywords(prompt);
            if (choice.Status != PromptStatus.OK) return;

            var handle = PickHandle(context, "\nChọn đối tượng nguồn trên bản vẽ: ");
            if (handle == null) return;
            var target = new Dictionary<string, string> { ["source"] = handle };
            Decision? decision = null;
            switch (choice.StringResult)
            {
                case "Nhan":
                    {
                        var text = Ask(context, "\nNhãn đầy đủ theo mẫu của hồ sơ, ví dụ P-1011 DN150 EL+4100: ");
                        if (text == null) return;
                        decision = SessionFile.NewDecision(DecisionOp.AssignTag, target, JsonSerializer.SerializeToElement(text));
                        break;
                    }

                case "Dn":
                    {
                        var dn = context.Editor.GetInteger("\nDN, mm: ");
                        if (dn.Status != PromptStatus.OK) return;
                        decision = SessionFile.NewDecision(DecisionOp.SetDn, target, JsonSerializer.SerializeToElement(dn.Value));
                        break;
                    }

                case "Caodo":
                    {
                        var el = context.Editor.GetDouble("\nCao độ, mm: ");
                        if (el.Status != PromptStatus.OK) return;
                        var datumPrompt = new PromptKeywordOptions("\nMốc [CL/BOP/TOP/TOS] ") { AllowNone = true };
                        foreach (var k in new[] { "CL", "BOP", "TOP", "TOS" }) datumPrompt.Keywords.Add(k);
                        datumPrompt.Keywords.Default = "CL";
                        var datum = context.Editor.GetKeywords(datumPrompt);
                        var datumText = datum.Status == PromptStatus.OK ? datum.StringResult : "CL";
                        decision = SessionFile.NewDecision(DecisionOp.SetElevation, target, JsonSerializer.SerializeToElement(new { raw = el.Value, datum = datumText }));
                        break;
                    }

                case "Nut":
                    {
                        var kindPrompt = new PromptKeywordOptions("\nLoại nút [Elbow/Tee/Reducer/Nozzle/OpenEnd/Continuation] ");
                        foreach (var k in new[] { "Elbow", "Tee", "Reducer", "Nozzle", "OpenEnd", "Continuation" }) kindPrompt.Keywords.Add(k);
                        var kind = context.Editor.GetKeywords(kindPrompt);
                        if (kind.Status != PromptStatus.OK) return;
                        decision = SessionFile.NewDecision(DecisionOp.SetNodeKind, target, JsonSerializer.SerializeToElement(kind.StringResult));
                        break;
                    }

                case "Caukien":
                    {
                        var type = Ask(context, "\nLoại cấu kiện, ví dụ valve_gate, valve_globe, flange_pair: ");
                        if (type == null) return;
                        decision = SessionFile.NewDecision(DecisionOp.SetComponentType, target, JsonSerializer.SerializeToElement(type));
                        break;
                    }

                case "Noi":
                    {
                        var second = PickHandle(context, "\nChọn đối tượng nguồn của đầu ống thứ hai: ");
                        if (second == null) return;
                        target["source2"] = second;
                        decision = SessionFile.NewDecision(DecisionOp.Connect, target, null);
                        break;
                    }

                case "Tach":
                    {
                        var pt = context.Editor.GetPoint("\nĐiểm tách, chọn gần một đỉnh của đoạn chạy: ");
                        if (pt.Status != PromptStatus.OK) return;
                        var scale = session.ScaleToMm;
                        decision = SessionFile.NewDecision(DecisionOp.SplitLine, target, JsonSerializer.SerializeToElement(new[] { pt.Value.X * scale, pt.Value.Y * scale }));
                        break;
                    }

                case "Boqua":
                    decision = SessionFile.NewDecision(DecisionOp.IgnoreSource, target, null);
                    break;
            }

            if (decision == null) return;
            session.AddDecision(decision);
            context.Write($"Piping Agent: đã ghi quyết định {decision.Id} ({decision.Op}) cho handle {handle}. Chạy lại nhận diện...");
            context.Log?.Info("decision", new Dictionary<string, object?> { ["id"] = decision.Id, ["op"] = decision.Op.ToString(), ["source"] = handle });
            Rerun.Run(context, session);
        }

        internal static string? PickHandle(CommandContext context, string message)
        {
            var r = context.Editor.GetEntity(message);
            if (r.Status != PromptStatus.OK) return null;
            return r.ObjectId.Handle.ToString();
        }

        internal static string? Ask(CommandContext context, string message)
        {
            var r = context.Editor.GetString(new PromptStringOptions(message) { AllowSpaces = true });
            return r.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(r.StringResult) ? r.StringResult.Trim() : null;
        }
    }

    /// <summary>PA_WAIVE: bỏ qua một vấn đề, bắt buộc nhập lý do, S5-10 và FR-CHK-06. Ghi vào tệp phiên và nhật ký.</summary>
    public sealed class WaiveCommand : CommandBase
    {
        protected override string Name => "PA_WAIVE";

        [CommandMethod("PA_WAIVE")]
        public void Waive() => Execute();

        protected override void Run(CommandContext context)
        {
            var session = PluginApp.Sessions?.GetOrCreate(context.Document);
            var model = session?.LastResult?.Model;
            if (session == null || model == null)
            {
                context.Write("Piping Agent: chưa có kết quả nhận diện. Chạy PA_CONVERT trước.");
                return;
            }

            var open = model.Issues.Where(i => i.Status == IssueStatus.Open).ToList();
            if (open.Count == 0)
            {
                context.Write("Piping Agent: không có vấn đề đang mở.");
                return;
            }

            var id = FixCommand.Ask(context, $"\nMã vấn đề cần bỏ qua, ví dụ {open[0].Id}: ");
            var issue = open.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
            if (issue == null)
            {
                context.Write("Piping Agent: không có vấn đề đang mở với mã đó.");
                return;
            }

            var reason = FixCommand.Ask(context, "\nLý do bỏ qua, bắt buộc: ");
            if (reason == null)
            {
                context.Write("Piping Agent: không có lý do thì không bỏ qua được.");
                return;
            }

            Waive(session, issue, reason, context);
            Rerun.Run(context, session);
        }

        internal static void Waive(DocumentSession session, Issue issue, string reason, CommandContext? context)
        {
            var target = new Dictionary<string, string> { ["rule"] = issue.RuleId };
            if (issue.Sources.Count > 0) target["source"] = issue.Sources[0];
            if (issue.Elements.Count > 0) target["element"] = issue.Elements[0];
            var decision = SessionFile.NewDecision(DecisionOp.WaiveIssue, target, null, reason);
            session.AddDecision(decision);
            context?.Write($"Piping Agent: đã bỏ qua {issue.Id} ({issue.RuleId}) với lý do: {reason}");
            PluginApp.Log?.Info("waive", new Dictionary<string, object?> { ["issue"] = issue.Id, ["rule"] = issue.RuleId, ["reason"] = reason, ["decision"] = decision.Id });
        }
    }

    /// <summary>Chạy lại nhận diện trên các đối tượng đã đọc với quyết định mới, rồi cập nhật bảng, S5-14.</summary>
    internal static class Rerun
    {
        public static void Run(CommandContext context, DocumentSession session)
        {
            if (PluginApp.Sessions == null || session.Profile == null || session.LastEntities == null || session.LastResult == null) return;
            var result = ScanCommand.RunPipeline("Piping Agent đang chạy lại", session.LastEntities, session, session.LastResult.Model.Source);
            session.StoreScan(session.LastEntities, result, session.ScaleToMm, session.LastFingerprint);
            session.SaveSessionFile();
            var open = result.Model.Issues.Count(i => i.Status == IssueStatus.Open);
            var blockers = result.Model.Issues.Count(i => i.Status == IssueStatus.Open && i.Severity == Severity.Blocker);
            context.Write($"Piping Agent: chạy lại xong trong {result.Elapsed.TotalSeconds:0.00} s. {result.Decisions}. Đang mở {open} vấn đề, {blockers} chặn.");
            foreach (var n in result.Decisions.Notes.Take(3)) context.Write("  " + n);
            if (PanelHost.IsOpen) PanelHost.ShowSession(session);
        }
    }
}

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>PA_RERUN: chạy lại nhận diện và bộ luật với các quyết định hiện có, S5-14. Bảng điều khiển gọi sau mỗi thao tác.</summary>
    public sealed class RerunCommand : CommandBase
    {
        protected override string Name => "PA_RERUN";

        [CommandMethod("PA_RERUN")]
        public void Run() => Execute();

        protected override void Run(CommandContext context)
        {
            var session = PluginApp.Sessions?.GetOrCreate(context.Document);
            if (session?.LastResult == null)
            {
                context.Write("Piping Agent: chưa có kết quả nhận diện. Chạy PA_CONVERT trước.");
                return;
            }

            Rerun.Run(context, session);
        }
    }
}
