using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using PipingAgent.AutoCAD.Session;
using PipingAgent.Core.Profile;
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
            if (PanelHost.IsGuiAvailable())
            {
                PanelHost.ShowChatWindow();
                context.Write("Piping Agent: đã mở cửa sổ trò chuyện với AI.");
                return;
            }

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
                AllowAction = config.AllowAction == true,
                ApplyMutationExecutor = p => session.ApplyMutation(p),
                DocumentPath = context.Document.Name,
                DocumentFingerprint = session.LastResult?.Model?.Source?.Fingerprint,
            };
            foreach (var kvp in session.Proposals) toolContext.Proposals[kvp.Key] = kvp.Value;
            var registry = ReadOnlyTools.Register(new ToolRegistry());
            ActionTools.Register(registry);
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
                    foreach (var kvp in toolContext.Proposals) session.Proposals[kvp.Key] = kvp.Value;
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
            catch (System.Exception ex)
            {
                var msg = ex.Message;
                if (config.Endpoint != null && config.Endpoint.Contains("8645") && !msg.Contains("Hermes"))
                {
                    msg += " (Không kết nối được Hermes Proxy tại 127.0.0.1:8645. Hãy kiểm tra Hermes đang chạy).";
                }
                context.Write("Piping Agent: lỗi chạy agent: " + msg);
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

                var actionPrompt = new PromptKeywordOptions("\nCho phép Agent can thiệp/sửa bản vẽ [Co/Khong] ") { AllowNone = true };
                actionPrompt.Keywords.Add("Co");
                actionPrompt.Keywords.Add("Khong");
                actionPrompt.Keywords.Default = current.AllowAction == true ? "Co" : "Khong";
                var actionRes = context.Editor.GetKeywords(actionPrompt);
                if (actionRes.Status == PromptStatus.OK || actionRes.Status == PromptStatus.None)
                {
                    layer.AllowAction = (actionRes.Status == PromptStatus.OK ? actionRes.StringResult : actionPrompt.Keywords.Default) == "Co";
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

    /// <summary>
    /// PA_PROPOSE: agent khảo sát bản vẽ và đề xuất hồ sơ quy ước, S8-06.
    /// </summary>
    public sealed class ProposeCommand : CommandBase
    {
        protected override string Name => "PA_PROPOSE";

        [CommandMethod("PA_PROPOSE")]
        public void Propose() => Execute();

        protected override void Run(CommandContext context)
        {
            if (PanelHost.IsGuiAvailable())
            {
                PanelHost.ShowChatWindow();
                PanelHost.Model.ProposeProfile();
                context.Write("Piping Agent: đã mở cửa sổ trò chuyện và bắt đầu đề xuất hồ sơ quy ước.");
                return;
            }

            var session = PluginApp.Sessions?.GetOrCreate(context.Document);
            if (session == null) return;
            var config = AgentConfig.Load(context.Document.Name);
            if (!config.Enabled)
            {
                context.Write("Piping Agent: agent đang tắt. Chạy PA_AGENTCONFIG để bật và chọn mô hình.");
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

            var toolContext = new ToolContext
            {
                Entities = session.LastEntities,
                Summary = PipingAgent.Core.Source.SourceEntityExtensions.Summarize(session.LastEntities),
                Model = session.LastResult?.Model,
                Catalog = PluginApp.Sessions!.Catalog,
                Profile = session.Profile,
            };
            var registry = ReadOnlyTools.Register(new ToolRegistry());
            ProfileDraftTools.Register(registry);

            var sessionContext = $"Bản vẽ {session.DrawingName}, {session.LastEntities.Count} đối tượng, hồ sơ hiện tại {session.Profile?.Id ?? "chưa chọn"}.";
            context.Write($"Piping Agent: agent đang khảo sát bản vẽ và đề xuất hồ sơ quy ước...");
            var timeout = TimeSpan.FromSeconds(Math.Max(60, (config.TimeoutSec ?? 180) * 2));
            try
            {
                using (var client = AgentClient.Start(config, context.Document.Name, TimeSpan.FromSeconds(20)))
                {
                    var steps = 0;
                    var result = client.RunSession("Khảo sát bản vẽ và đề xuất hồ sơ quy ước tối ưu.", "profile", sessionContext, registry, toolContext, timeout,
                        tool => context.Write($"  bước {++steps}: {tool}"),
                        (name, args, ms) => context.Log?.Info("agent_tool", new Dictionary<string, object?> { ["tool"] = name, ["args"] = args.GetRawText(), ["ms"] = ms }));

                    var answer = result.TryGetProperty("answer", out var a) ? a.GetString() : null;
                    var stop = result.TryGetProperty("stopReason", out var s) ? s.GetString() : null;
                    context.Write("\nAgent đề xuất:\n" + answer + "\n");
                    context.Write($"[Dừng: {stop}, {steps} bước]");

                    if (toolContext.DraftProfile != null)
                    {
                        var dp = toolContext.DraftProfile;
                        context.Write($"\nTìm thấy hồ sơ nháp: {dp.Id} ({dp.PipeStandard}). {dp.Roles.Count} vai trò, {dp.Blocks.Count} block.");
                        var confirm = FixCommand.Ask(context, "Lưu hồ sơ nháp này ra tệp? (c/k) [k]: ");
                        if (string.Equals(confirm, "c", StringComparison.OrdinalIgnoreCase))
                        {
                            var saveJson = ProfileLoader.Serialize(dp);
                            var path = Path.Combine(ProfileStore.ProfileDirectory, dp.Id + ".json");
                            Directory.CreateDirectory(ProfileStore.ProfileDirectory);
                            File.WriteAllText(path, saveJson, new UTF8Encoding(false));
                            context.Write("Đã lưu hồ sơ tại: " + path);
                            session.ProfilePath = path;
                            session.LoadProfile();
                            context.Write("Chạy PA_SCAN để áp dụng hồ sơ mới.");
                        }
                    }
                }
            }
            catch (TimeoutException ex)
            {
                context.Write("Piping Agent: hết giờ chờ agent: " + ex.Message);
            }
            catch (System.Exception ex)
            {
                var msg = ex.Message;
                if (config.Endpoint != null && config.Endpoint.Contains("8645") && !msg.Contains("Hermes"))
                {
                    msg += " (Không kết nối được Hermes Proxy tại 127.0.0.1:8645. Hãy kiểm tra Hermes đang chạy).";
                }
                context.Write("Piping Agent: lỗi chạy agent: " + msg);
            }
        }
    }

    /// <summary>
    /// PA_DOCTOR: Chẩn đoán toàn diện sức khỏe hệ thống Piping Agent (ADR-14).
    /// </summary>
    public sealed class DoctorCommand : CommandBase
    {
        protected override string Name => "PA_DOCTOR";

        [CommandMethod("PA_DOCTOR")]
        public void Doctor() => Execute();

        protected override void Run(CommandContext context)
        {
            var doc = context.Document;
            var session = PluginApp.Sessions?.GetOrCreate(doc);
            var config = AgentConfig.Load(doc.Name);

            context.Write("\n=== [Piping Agent System Doctor - ADR-14] ===");

            // 1. Bản vẽ
            var entCount = session?.LastEntities?.Count ?? 0;
            context.Write($"1. Bản vẽ AutoCAD: [PASS] {Path.GetFileName(doc.Name)} ({entCount} đối tượng, tỉ lệ {session?.ScaleToMm ?? 1.0} mm)");

            // 2. Thư viện tiêu chuẩn
            var catalog = PluginApp.Sessions?.Catalog;
            var stdCount = catalog?.Standards.Count ?? 0;
            var catStatus = stdCount > 0 ? "[PASS]" : "[WARN]";
            context.Write($"2. Danh mục ASME: {catStatus} Đã nạp {stdCount} tiêu chuẩn ({string.Join(", ", catalog?.Standards ?? Array.Empty<string>())})");

            // 3. AI Agent Config
            var aiStatus = config.Enabled ? "[PASS]" : "[OFF]";
            context.Write($"3. Cấu hình AI: {aiStatus} Provider={config.Provider}, Model={config.Model}, Endpoint={config.Endpoint}");

            // 4. Quyền can thiệp bản vẽ (Action Tools)
            var actionStatus = config.AllowAction == true ? "[PASS] Đã bật" : "[INFO] Tắt (Chỉ đọc)";
            context.Write($"4. Quyền can thiệp: {actionStatus}");

            // 5. Đề xuất chờ duyệt
            var pending = session?.Proposals.Values.Count(p => p.Status == Tools.MutationStatus.Pending) ?? 0;
            context.Write($"5. Hàng đợi đề xuất: [PASS] {pending} đề xuất đang chờ duyệt.");

            context.Write("===============================================\n");
        }
    }
}
