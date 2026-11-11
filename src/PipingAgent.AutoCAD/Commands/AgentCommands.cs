using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using PipingAgent.AutoCAD.Session;
using PipingAgent.Core.Recognition;
using PipingAgent.Hosting;
using PipingAgent.Tools;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>
    /// PA_ASK: hỏi agent một câu về bản vẽ đang mở, S7-06. Agent chỉ có công cụ chỉ đọc; công cụ chạy trên luồng chính của AutoCAD
    /// trong lúc lệnh chờ. Hết giờ thì dừng, AutoCAD không treo.
    /// </summary>
    public sealed class AskCommand : CommandBase
    {
        protected override string Name => "PA_ASK";

        [CommandMethod("PA_ASK")]
        public void Ask() => Execute();

        protected override void Run(CommandContext context)
        {
            var session = PluginApp.Sessions?.GetOrCreate(context.Document);
            if (session == null) return;
            var config = AgentConfig.Load(context.Document.Name);
            if (!config.Enabled)
            {
                context.Write("Piping Agent: agent đang tắt. Chạy PA_AGENTCONFIG để bật và chọn mô hình. " + config.Describe());
                return;
            }

            if (session.LastEntities == null)
            {
                context.Write("Piping Agent: chưa quét bản vẽ, đang quét trước...");
                var reader = new AcadDrawingReader(context.Database, context.Transaction);
                var entities = ScanCommand.Report(context, reader, "cả không gian mô hình", null);
                ScanCommand.Recognize(context, reader, entities);
                if (session.LastEntities == null) return;
            }

            var question = FixCommand.Ask(context, "\nCâu hỏi cho agent: ");
            if (question == null) return;

            var toolContext = new ToolContext
            {
                Entities = session.LastEntities,
                Summary = session.LastResult?.Model.Source == null ? null : BuildSummary(session),
                Model = session.LastResult?.Model,
                Catalog = PluginApp.Sessions!.Catalog,
                Profile = session.Profile,
            };
            var registry = ReadOnlyTools.Register(new ToolRegistry());
            var sessionContext = $"Bản vẽ {session.DrawingName}, {session.LastEntities.Count} đối tượng, hồ sơ quy ước {session.Profile?.Id ?? "chưa chọn"}. " +
                (session.LastResult != null ? RecognitionReport.ToText(session.LastResult, maxLayers: 5, maxIssues: 0) : string.Empty);

            context.Write($"Piping Agent: khởi chạy Agent Host, mô hình {config.Model} tại {config.Endpoint}...");
            var timeout = TimeSpan.FromSeconds(Math.Max(60, (config.TimeoutSec ?? 180) * 2));
            try
            {
                using (var client = AgentClient.Start(config, context.Document.Name, TimeSpan.FromSeconds(20)))
                {
                    var steps = 0;
                    var result = client.RunSession(question, "qa", sessionContext, registry, toolContext, timeout,
                        tool => context.Write($"  bước {++steps}: {tool}"),
                        (name, args, ms) => context.Log?.Info("agent_tool", new Dictionary<string, object?> { ["tool"] = name, ["args"] = args.GetRawText(), ["ms"] = ms }));
                    var answer = result.TryGetProperty("answer", out var a) ? a.GetString() : null;
                    var stop = result.TryGetProperty("stopReason", out var s) ? s.GetString() : null;
                    context.Write("\nAgent trả lời:\n" + answer + "\n");
                    context.Write(string.Format(CultureInfo.InvariantCulture, "[{0}, {1} bước, {2} lệnh gọi công cụ, {3}+{4} token, {5} s, {6}]",
                        stop, Get(result, "steps"), Get(result, "toolCalls"), Get(result, "inputTokens"), Get(result, "outputTokens"), Get(result, "elapsedSec"), config.Model));
                    context.Log?.Info("agent_session", new Dictionary<string, object?> { ["question"] = question, ["stop"] = stop, ["steps"] = Get(result, "steps"), ["toolCalls"] = Get(result, "toolCalls"), ["model"] = config.Model });
                }
            }
            catch (TimeoutException ex)
            {
                context.Write("Piping Agent: hết giờ chờ agent, đã dừng Agent Host. " + ex.Message);
            }
        }

        private static string Get(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.ToString() : "?";

        private static PipingAgent.Core.Source.DrawingSummary BuildSummary(DocumentSession session) =>
            PipingAgent.Core.Source.SourceEntityExtensions.Summarize(session.LastEntities!);
    }

    /// <summary>PA_AGENTCONFIG: cài đặt nhà cung cấp, điểm cuối, mô hình, khoá, S7-07. Khoá lưu bằng DPAPI, không ở dạng rõ trong tệp.</summary>
    public sealed class AgentConfigCommand : CommandBase
    {
        protected override string Name => "PA_AGENTCONFIG";

        [CommandMethod("PA_AGENTCONFIG")]
        public void Configure() => Execute();

        protected override void Run(CommandContext context)
        {
            var current = AgentConfig.Load(context.Document.Name);
            context.Write("Piping Agent: cấu hình hiện tại: " + current.Describe());
            var layer = AgentConfig.ReadLayer(AgentConfig.UserPath) ?? new AgentConfig();

            var providerPrompt = new PromptKeywordOptions("\nNhà cung cấp [Openai/Tat] ") { AllowNone = true };
            providerPrompt.Keywords.Add("Openai");
            providerPrompt.Keywords.Add("Tat");
            providerPrompt.Keywords.Default = current.Enabled ? "Openai" : "Tat";
            var provider = context.Editor.GetKeywords(providerPrompt);
            if (provider.Status != PromptStatus.OK && provider.Status != PromptStatus.None) return;
            layer.Provider = (provider.Status == PromptStatus.OK ? provider.StringResult : providerPrompt.Keywords.Default) == "Openai" ? "openai-compatible" : "off";

            if (layer.Provider == "openai-compatible")
            {
                // Load xuất phát từ Defaults nên hai giá trị này luôn có.
                var endpoint = AskDefault(context, "Điểm cuối kiểu OpenAI", current.Endpoint!);
                if (endpoint == null) return;
                layer.Endpoint = endpoint;
                var model = AskDefault(context, "Tên mô hình", current.Model!);
                if (model == null) return;
                layer.Model = model;
                var key = context.Editor.GetString(new PromptStringOptions("\nKhoá truy cập, để trống nếu giữ khoá cũ hoặc không cần: ") { AllowSpaces = false });
                if (key.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(key.StringResult))
                {
                    layer.ApiKeyProtected = AgentConfig.Protect(key.StringResult.Trim());
                    layer.ApiKeyEnv = null;
                }
                else if (string.IsNullOrEmpty(layer.ApiKeyProtected) && string.IsNullOrEmpty(layer.ApiKeyEnv))
                {
                    layer.ApiKeyProtected = AgentConfig.Protect("none");
                }
            }

            AgentConfig.Save(AgentConfig.UserPath, layer);
            context.Write($"Piping Agent: đã ghi {AgentConfig.UserPath}. Khoá được mã hoá theo tài khoản Windows, không xuất hiện trong tệp ở dạng rõ.");
            context.Log?.Info("agent_config", new Dictionary<string, object?> { ["provider"] = layer.Provider, ["endpoint"] = layer.Endpoint, ["model"] = layer.Model });
        }

        private static string? AskDefault(CommandContext context, string label, string current)
        {
            var r = context.Editor.GetString(new PromptStringOptions($"\n{label} <{current}>: ") { AllowSpaces = true });
            if (r.Status != PromptStatus.OK && r.Status != PromptStatus.None) return null;
            return string.IsNullOrWhiteSpace(r.StringResult) ? current : r.StringResult.Trim();
        }
    }
}
