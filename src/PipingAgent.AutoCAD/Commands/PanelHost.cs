using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PipingAgent.AutoCAD.Session;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Source;
using PipingAgent.Hosting;
using PipingAgent.Tools;
using PipingAgent.UI;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>
    /// Nơi duy nhất trong plug-in biết đến PaletteSet và các kiểu của AcMgd.dll, để lớp lệnh vẫn nạp được trong accoreconsole.
    /// Kiểu PaletteSet chỉ được nhắc tên trong <see cref="Show"/>, hàm không cho phép inline: accoreconsole không có AcMgd.dll
    /// và chỉ cần JIT một hàm có chạm tới kiểu đó là tiến trình chết, không bắt được. Trường giữ bảng khai báo là <c>object</c>.
    /// Bảng điều khiển nhận dữ liệu qua <see cref="PanelViewModel"/>, gọi ngược về AutoCAD qua <see cref="AcadHostActions"/>.
    /// </summary>
    internal static class PanelHost
    {
        private static object? _palette;
        private static MainPanel? _panel;
        private static ChatWindow? _chatWindow;
        private static PanelViewModel? _model;
        private static AcadProgress? _running;

        public static PanelViewModel Model => _model ??= CreateModel();

        /// <summary>Bảng hoặc cửa sổ trò chuyện đã được tạo.</summary>
        public static bool IsOpen => _panel != null || _chatWindow != null;

        public static bool IsGuiAvailable()
        {
            if (string.Equals(System.Diagnostics.Process.GetCurrentProcess().ProcessName, "accoreconsole", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        private static PanelViewModel CreateModel()
        {
            var model = new PanelViewModel(new AcadHostActions());
            model.ProfileSelected += OnProfileSelected;
            if (PluginApp.Sessions != null) PluginApp.Sessions.ActiveSessionChanged += (_, s) => ShowSession(s);
            LoadProfiles(model);
            return model;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void Show()
        {
            Autodesk.AutoCAD.Windows.PaletteSet palette;
            if (_palette == null)
            {
                var model = Model;
                _panel = new MainPanel(model);
                _panel.CancelRequested += (_, __) => _running?.Cancel();
                palette = new Autodesk.AutoCAD.Windows.PaletteSet("Piping Agent", new Guid("7E1D3C2A-4B6F-4D8E-9A21-5C0F3B7D9E10"))
                {
                    Style = Autodesk.AutoCAD.Windows.PaletteSetStyles.ShowAutoHideButton
                          | Autodesk.AutoCAD.Windows.PaletteSetStyles.ShowCloseButton
                          | Autodesk.AutoCAD.Windows.PaletteSetStyles.ShowPropertiesMenu,
                    MinimumSize = new System.Drawing.Size(320, 240),
                };
                palette.AddVisual("Piping Agent", _panel);
                _palette = palette;
            }
            else
            {
                palette = (Autodesk.AutoCAD.Windows.PaletteSet)_palette;
            }

            palette.Visible = true;
            ShowSession(PluginApp.Sessions?.Active);
        }

        public static void ShowChatWindow()
        {
            if (!IsGuiAvailable()) return;
            OpenChatWindowInternal();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void OpenChatWindowInternal()
        {
            if (_chatWindow == null || !_chatWindow.IsLoaded)
            {
                _chatWindow = new ChatWindow(Model);
                _chatWindow.Closed += (_, __) => _chatWindow = null;
                try
                {
                    AcApp.ShowModelessWindow(_chatWindow);
                }
                catch
                {
                    _chatWindow.Show();
                }
            }
            else
            {
                if (_chatWindow.WindowState == System.Windows.WindowState.Minimized)
                {
                    _chatWindow.WindowState = System.Windows.WindowState.Normal;
                }
                _chatWindow.Activate();
            }

            _chatWindow.Focus();
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var config = AgentConfig.Load(doc?.Name);
            var modelName = config.Model ?? "Solar mini4";
            if (modelName.Contains("/")) modelName = modelName.Substring(modelName.LastIndexOf('/') + 1);
            if (modelName.Contains(":")) modelName = modelName.Substring(0, modelName.IndexOf(':'));
            Model.ConnectionStatusText = $"🟢 MCP: 12 công cụ | {modelName} (Hermes)";
            ShowSession(PluginApp.Sessions?.Active);
        }

        /// <summary>Gắn thước tiến độ đang chạy để nút Hủy của bảng hủy được.</summary>
        public static void SetRunning(AcadProgress? progress)
        {
            _running = progress;
            var model = Model;
            model.Busy = progress != null;
            if (progress == null)
            {
                model.Progress = 0;
                model.ProgressText = string.Empty;
            }
        }

        /// <summary>Đổ dữ liệu phiên của bản vẽ đang hoạt động vào bảng. Gọi sau mỗi lần quét và khi chuyển bản vẽ.</summary>
        public static void ShowSession(DocumentSession? session)
        {
            var model = Model;
            if (session == null)
            {
                model.DrawingName = string.Empty;
                model.Status = "Không có bản vẽ nào đang mở.";
                return;
            }

            model.DrawingName = session.DrawingName;
            SelectProfile(model, session.ProfilePath);
            model.ShowProfile(session.Profile, session.ProfileErrors, session.ProfilePath);
            if (session.LastResult != null)
            {
                string? preview = null;
                try
                {
                    var plan = BuildCommand.Plan(session.LastResult.Model, new PipingAgent.Core.Build.BuildOptions(), session);
                    var blockers = session.LastResult.Model.Issues.Count(i => i.Severity == PipingAgent.Core.Ir.Severity.Blocker && i.Status == PipingAgent.Core.Ir.IssueStatus.Open);
                    preview = plan.Summary() + (blockers > 0 ? $". Còn {blockers} vấn đề chặn, PA_BUILD bị khoá" : ". Sẵn sàng PA_BUILD");
                }
                catch (System.Exception ex)
                {
                    preview = "không lập được kế hoạch dựng: " + ex.Message;
                }

                model.ShowResult(session.LastResult, session.DrawingName, session.BoundsByHandle, preview);
                var byLayer = session.LastEntities!.GroupBy(e => e.Layer, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new LayerRoleRow { Name = g.Key, Count = g.Count(), Types = string.Join(", ", g.GroupBy(e => e.NativeType ?? e.Kind.ToString()).OrderByDescending(k => k.Count()).Take(3).Select(k => $"{k.Key} {k.Count()}")) });
                var byBlock = session.LastEntities!.Where(e => e.Kind == PipingAgent.Core.Source.SourceKind.Insert && e.Name != null).GroupBy(e => e.Name!, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new LayerRoleRow { Name = g.Key, IsBlock = true, Count = g.Count(), Types = "INSERT" });
                model.LoadEditorRows(byLayer.OrderByDescending(r => r.Count).Concat(byBlock.OrderByDescending(r => r.Count)));
            }
            else
            {
                model.OverviewSummary = string.Empty;
                model.Roles.Clear();
                model.UnclassifiedLayers.Clear();
                model.Issues.Clear();
                model.Runs.Clear();
                model.Status = $"{session.DrawingName}: chưa quét. Gõ PA_SCAN hoặc bấm Quét.";
            }

            RefreshLog(model);
        }

        public static void RefreshLog(PanelViewModel model)
        {
            var path = PluginApp.LogPath;
            if (path == null || !File.Exists(path)) return;
            try
            {
                string[] lines;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    lines = reader.ReadToEnd().Split('\n');
                }

                model.SetLog(lines.Where(l => l.Length > 0).Reverse().Take(200).Select(Shorten));
            }
            catch (System.Exception)
            {
                // Không đọc được nhật ký thì thẻ để trống.
            }
        }

        private static string Shorten(string line) => line.Length <= 300 ? line : line.Substring(0, 297) + "...";

        private static void LoadProfiles(PanelViewModel model)
        {
            model.Profiles.Clear();
            foreach (var path in ProfileStore.AvailableProfiles())
            {
                model.Profiles.Add(new ProfileChoice { Path = path, Label = Path.GetFileNameWithoutExtension(path) });
            }
        }

        private static void SelectProfile(PanelViewModel model, string? path)
        {
            if (path == null) return;
            var existing = model.Profiles.FirstOrDefault(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new ProfileChoice { Path = path, Label = Path.GetFileNameWithoutExtension(path) };
                model.Profiles.Add(existing);
            }

            if (!ReferenceEquals(model.SelectedProfile, existing))
            {
                model.ProfileSelected -= OnProfileSelected;
                model.SelectedProfile = existing;
                model.ProfileSelected += OnProfileSelected;
            }
        }

        private static void OnProfileSelected(object? sender, EventArgs e)
        {
            var model = _panel?.Model;
            var session = PluginApp.Sessions?.Active;
            var choice = model?.SelectedProfile;
            if (model == null || session == null || choice == null) return;
            session.ProfilePath = choice.Path;
            var loaded = session.LoadProfile();
            model.ShowProfile(session.Profile, session.ProfileErrors, choice.Path);
            if (loaded.Ok) ProfileStore.RememberProfile(choice.Path);
            model.Status = loaded.Ok ? $"Đã chọn hồ sơ {loaded.Profile!.Id}. Bấm Quét để chạy lại." : "Hồ sơ có lỗi, xem thẻ Hồ sơ.";
        }

        public static void DispatchToUi(Action action)
        {
            if (action == null) return;
            var dispatcher = _chatWindow?.Dispatcher ?? _panel?.Dispatcher ?? System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(action);
            }
            else
            {
                HostThread.Post(action);
            }
        }
    }

    /// <summary>Cài <see cref="IHostActions"/> bằng API AutoCAD. Mọi việc đi qua phiên của bản vẽ đang hoạt động.</summary>
    internal sealed class AcadHostActions : IHostActions
    {
        public void Highlight(IReadOnlyList<string> handles)
        {
            var session = PluginApp.Sessions?.Active;
            if (session == null) return;
            HostThread.Post(() => session.Highlight.Highlight(handles));
        }

        public void ClearHighlight()
        {
            var session = PluginApp.Sessions?.Active;
            if (session == null) return;
            HostThread.Post(() => session.Highlight.Clear());
        }

        public void ZoomTo(Box2 region)
        {
            var session = PluginApp.Sessions?.Active;
            if (session == null) return;
            HostThread.Post(() => session.Highlight.ZoomTo(region, session.ScaleToMm));
        }

        public void Rescan()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute("_.PA_SCAN ", true, false, false);
        }

        public void WaiveIssue(string issueId, string reason)
        {
            var session = PluginApp.Sessions?.Active;
            var issue = session?.LastResult?.Model.Issues.FirstOrDefault(i => i.Id == issueId);
            if (session == null || issue == null) return;
            WaiveCommand.Waive(session, issue, reason, null);
            RunCommand("PA_RERUN");
        }

        public void RunCommand(string command)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute("_." + command + " ", true, false, false);
        }

        public string TestTagPattern(string regex)
        {
            var session = PluginApp.Sessions?.Active;
            if (session?.LastResult == null) return "Chưa quét bản vẽ.";
            try
            {
                var pattern = new PipingAgent.Core.Profile.TagPattern { Name = "thu", Regex = regex };
                var parser = new PipingAgent.Core.Recognition.TagParser(new[] { pattern });
                var texts = session.LastEntities!.Where(e => (e.Kind == PipingAgent.Core.Source.SourceKind.Text || e.Kind == PipingAgent.Core.Source.SourceKind.MText) && !string.IsNullOrWhiteSpace(e.Text)).ToList();
                var matched = texts.Where(t => parser.Parse(t.Text) != null).ToList();
                var sample = string.Join(" | ", texts.Where(t => parser.Parse(t.Text) == null).Take(5).Select(t => t.Text));
                return $"Khớp {matched.Count} trên {texts.Count} chữ. Không khớp, ví dụ: {sample}";
            }
            catch (System.Exception ex)
            {
                return "Mẫu sai: " + ex.Message;
            }
        }

        public string? SaveProfileFile(string json)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Ghi hồ sơ quy ước", Filter = "Hồ sơ quy ước (*.json)|*.json", DefaultExt = ".json" };
            if (Directory.Exists(ProfileStore.ProfileDirectory)) dialog.InitialDirectory = ProfileStore.ProfileDirectory;
            if (dialog.ShowDialog() != true) return null;
            File.WriteAllText(dialog.FileName, json, new System.Text.UTF8Encoding(false));
            return dialog.FileName;
        }

        public string? PickProfileFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn hồ sơ quy ước",
                Filter = "Hồ sơ quy ước (*.json)|*.json|Mọi tệp|*.*",
                CheckFileExists = true,
            };
            if (Directory.Exists(ProfileStore.ProfileDirectory)) dialog.InitialDirectory = ProfileStore.ProfileDirectory;
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public void RunAgent(string mode, string? question)
        {
            var session = PluginApp.Sessions?.Active;
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var model = PanelHost.Model;
            if (session == null || doc == null || model == null) return;

            var config = AgentConfig.Load(doc.Name);
            if (!config.Enabled)
            {
                model.AddChatMessage("Hệ thống", "Agent đang tắt. Hãy chạy lệnh PA_AGENTCONFIG để bật.");
                model.IsAgentRunning = false;
                model.AgentCurrentStep = "Agent đang tắt";
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    // Tự động quét bản vẽ ngầm qua MCP nếu chưa quét (Auto-scan on Ask)
                    if (session.LastEntities == null)
                    {
                        PanelHost.DispatchToUi(() =>
                        {
                            model.AgentCurrentStep = "Đang tự động đọc bản vẽ...";
                            model.AddChatMessage("Hệ thống", "Bản vẽ chưa được quét. Hệ thống đang tự động đọc đối tượng ngầm qua công cụ MCP...");
                        });

                        try
                        {
                            using (doc.LockDocument())
                            using (var tr = doc.TransactionManager.StartTransaction())
                            {
                                var reader = new AcadDrawingReader(doc.Database, tr);
                                var entities = reader.ReadEntities(PipingAgent.Core.Source.EntityFilter.All);
                                var summary = reader.GetSummary();
                                if (session.Profile == null && session.ProfilePath != null) session.LoadProfile();

                                var result = session.Profile != null
                                    ? RecognitionPipeline.Run(entities, session.Profile, PluginApp.Sessions?.Catalog)
                                    : new RecognitionResult();

                                session.StoreScan(entities, result, reader.ScaleToMm, summary.Fingerprint);
                                tr.Commit();
                            }

                            PanelHost.DispatchToUi(() =>
                            {
                                model.AddChatMessage("Hệ thống", $"Đã nạp {session.LastEntities?.Count ?? 0} đối tượng từ bản vẽ {Path.GetFileName(doc.Name)}.");
                            });
                        }
                        catch (Exception ex)
                        {
                            PluginApp.Log?.Error("chat_autoscan_failed", ex, null);
                            PanelHost.DispatchToUi(() =>
                            {
                                model.AddChatMessage("Hệ thống", $"Không thể tự động đọc bản vẽ: {ex.Message}. Vui lòng thử lại hoặc chạy lệnh PA_SCAN.");
                                model.IsAgentRunning = false;
                                model.AgentCurrentStep = "Lỗi đọc bản vẽ";
                            });
                            return;
                        }
                    }

                    var allowAction = config.AllowAction == true;
                    var toolContext = new ToolContext
                    {
                        Entities = session.LastEntities ?? new List<PipingAgent.Core.Source.SourceEntity>(),
                        Summary = session.LastEntities != null ? session.LastEntities.Summarize() : null,
                        Model = session.LastResult?.Model,
                        Catalog = PluginApp.Sessions!.Catalog,
                        Profile = session.Profile,
                        AllowAction = allowAction,
                        ApplyMutationExecutor = p => session.ApplyMutation(p),
                        DocumentPath = doc.Name,
                        DocumentFingerprint = session.LastResult?.Model?.Source?.Fingerprint,
                    };
                    foreach (var kvp in session.Proposals) toolContext.Proposals[kvp.Key] = kvp.Value;
                    var registry = ReadOnlyTools.Register(new ToolRegistry());
                    ProfileDraftTools.Register(registry);
                    ActionTools.Register(registry);

                    var sessionContext = $"Bản vẽ {session.DrawingName}, {session.LastEntities?.Count ?? 0} đối tượng, hồ sơ quy ước {session.Profile?.Id ?? "chưa chọn"}.";
                    var pendingList = session.Proposals.Values.Where(pr => pr.Status == MutationStatus.Pending).ToList();
                    if (pendingList.Count > 0)
                    {
                        sessionContext += $" Đề xuất can thiệp đang chờ duyệt: {string.Join(", ", pendingList.Select(pr => $"{pr.ProposalId} ({pr.Summary})"))}.";
                    }
                    var timeout = TimeSpan.FromSeconds(Math.Max(60, (config.TimeoutSec ?? 180) * 2));

                    using (var client = AgentClient.Start(config, doc.Name, TimeSpan.FromSeconds(20)))
                    {
                        var steps = 0;
                        var result = client.RunSession(question ?? string.Empty, mode, sessionContext, registry, toolContext, timeout,
                            tool =>
                            {
                                PanelHost.DispatchToUi(() =>
                                {
                                    if (PanelHost.Model != null)
                                    {
                                        PanelHost.Model.AgentCurrentStep = $"Bước {++steps}: {FormatMcpStep(tool)}";
                                        PanelHost.Model.AddChatMessage("Công cụ", FormatMcpTool(tool));
                                    }
                                });
                            },
                            (name, args, ms) => { });

                        var answer = result.TryGetProperty("answer", out var a) ? a.GetString() : string.Empty;
                        var stop = result.TryGetProperty("stopReason", out var s) ? s.GetString() : string.Empty;

                        PanelHost.DispatchToUi(() =>
                        {
                            var m = PanelHost.Model;
                            if (m != null)
                            {
                                m.IsAgentRunning = false;
                                if (stop == "provider_error")
                                {
                                    m.AgentCurrentStep = "Lỗi kết nối mô hình";
                                    if (answer != null && answer.Contains("429"))
                                    {
                                        answer = "⚠️ Máy chủ AI đang quá tải lưu lượng tạm thời (HTTP 429: Upstream at capacity).\n" +
                                                 "Hệ thống đã tự động thử lại 3 lần nhưng máy chủ thượng nguồn chưa kịp phản hồi.\n\n" +
                                                 "👉 Bạn vui lòng chờ vài giây rồi gửi lại, hoặc chạy lệnh PA_AGENTCONFIG để đổi sang mô hình khác (ví dụ: stepfun/step-3.7-flash:free).";
                                    }
                                    else if (config.Endpoint != null && config.Endpoint.Contains("8645") && answer != null && !answer.Contains("Hermes"))
                                    {
                                        answer += "\n[Gợi ý]: Hãy kiểm tra Hermes Desktop đang mở và dịch vụ Hermes Proxy tại 127.0.0.1:8645 đang chạy.";
                                    }
                                }
                                else
                                {
                                    m.AgentCurrentStep = $"Hoàn tất ({stop})";
                                }

                                if (!string.IsNullOrEmpty(answer))
                                {
                                    m.AddChatMessage("Agent", answer!);
                                }

                                if (toolContext.DraftProfile != null)
                                {
                                    var dp = toolContext.DraftProfile;
                                    var rec = RecognitionPipeline.Run(toolContext.Entities, dp, toolContext.Catalog);
                                    var totalEntities = toolContext.Entities.Count;
                                    var classified = totalEntities - rec.Classification.Report.Unclassified;
                                    var classRatio = totalEntities > 0 ? (double)classified / totalEntities : 0;
                                    var totalTags = rec.TagReads.Count;
                                    var matchedTags = rec.TagReads.Count(t => t.Matched);
                                    var tagRatio = totalTags > 0 ? (double)matchedTags / totalTags : 0;
                                    var unlabeled = rec.Model.Runs.Count(r => string.IsNullOrEmpty(r.LineId));

                                    m.SetDraftProfile(dp.Id, dp.PipeStandard ?? "ASME-B36.10", dp.Roles.Count, dp.Blocks.Count, dp.TagPatterns.Count,
                                        classRatio, tagRatio, unlabeled, rec.Model.Issues.Count,
                                        ProfileLoader.Serialize(dp));
                                    m.AddChatMessage("Hệ thống", $"Agent đã đề xuất hồ sơ '{dp.Id}'. Xem lại và bấm 'Duyệt & Lưu hồ sơ...' để áp dụng.");
                                }

                                foreach (var kvp in toolContext.Proposals)
                                {
                                    session.Proposals[kvp.Key] = kvp.Value;
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    PanelHost.DispatchToUi(() =>
                    {
                        var m = PanelHost.Model;
                        if (m != null)
                        {
                            m.IsAgentRunning = false;
                            m.AgentCurrentStep = "Lỗi kết nối";
                            var msg = ex.Message;
                            if (config.Endpoint != null && config.Endpoint.Contains("8645") && !msg.Contains("Hermes"))
                            {
                                msg += " (Không kết nối được Hermes Proxy tại 127.0.0.1:8645). Hãy kiểm tra Hermes đang chạy.";
                            }
                            m.AddChatMessage("Lỗi", msg);
                        }
                    });
                }
            });
        }

        public string? AcceptDraftProfile(string json)
        {
            var path = SaveProfileFile(json);
            if (string.IsNullOrEmpty(path)) return null;

            var session = PluginApp.Sessions?.Active;
            if (session != null)
            {
                session.ProfilePath = path;
                var loaded = session.LoadProfile();
                PanelHost.Model?.ShowProfile(session.Profile, session.ProfileErrors, path);
                if (loaded.Ok)
                {
                    ProfileStore.RememberProfile(path!);
                    Rescan();
                }
            }
            return path;
        }

        private static readonly Dictionary<string, (string Step, string Tool)> ToolLabels = new Dictionary<string, (string Step, string Tool)>(StringComparer.OrdinalIgnoreCase)
        {
            ["drawing_get_summary"] = ("📊 Đang đọc tổng quan thông số bản vẽ...", "Đọc tóm tắt bản vẽ"),
            ["drawing_list_layers"] = ("📋 Đang rà soát danh sách layer...", "Tra cứu danh sách layer"),
            ["drawing_list_blocks"] = ("🧱 Đang kiểm tra các block phụ kiện...", "Tra cứu block phụ kiện"),
            ["drawing_sample_entities"] = ("🔍 Đang trích xuất dữ liệu đối tượng...", "Lấy mẫu đối tượng"),
            ["drawing_sample_texts"] = ("🏷️ Đang phân tích chữ & nhãn đường ống...", "Lấy mẫu văn bản & nhãn"),
            ["drawing_query_near"] = ("📐 Đang kiểm tra đối tượng lân cận...", "Truy vấn đối tượng lân cận"),
            ["drawing_get_general_notes"] = ("📝 Đang đọc ghi chú kỹ thuật...", "Đọc ghi chú bản vẽ"),
            ["catalog_lookup"] = ("📖 Đang tra cứu tiêu chuẩn ASME...", "Tra cứu danh mục ASME"),
            ["ir_get_overview"] = ("🌐 Đang phân tích mô hình mạng ống IR...", "Thống kê mô hình IR"),
            ["ir_get_element"] = ("🔎 Đang xem chi tiết phần tử tuyến ống...", "Xem chi tiết phần tử IR"),
            ["ir_list_issues"] = ("⚠️ Đang rà soát các vấn đề quy tắc...", "Kiểm tra danh sách vấn đề"),
            ["ir_get_issue_context"] = ("🧭 Đang phân tích ngữ cảnh vấn đề...", "Phân tích ngữ cảnh vấn đề"),
            ["profile_draft_write"] = ("✨ Đang lập hồ sơ quy ước đề xuất...", "Đề xuất hồ sơ quy ước nháp"),
            ["profile_draft_test"] = ("🧪 Đang thử nghiệm chỉ số hồ sơ...", "Chạy thử hồ sơ quy ước nháp"),
            ["cad_propose_mutation"] = ("📝 Đang chuẩn bị đề xuất can thiệp bản vẽ...", "Đề xuất sửa đổi bản vẽ CAD"),
            ["cad_apply_mutation"] = ("⚡ Đang thực thi thay đổi vào bản vẽ CAD...", "Áp dụng can thiệp vào CAD"),
        };

        private static string FormatMcpTool(string tool) =>
            ToolLabels.TryGetValue(tool, out var l) ? $"🔧 [MCP] {tool}: {l.Tool}" : $"🔧 [MCP] {tool}";

        private static string FormatMcpStep(string tool) =>
            ToolLabels.TryGetValue(tool, out var l) ? l.Step : $"⚙️ Đang thực thi {tool}...";
    }
}
