using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PipingAgent.AutoCAD.Session;
using PipingAgent.Core.Geometry;
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
        private static AcadProgress? _running;

        public static PanelViewModel? Model => _panel?.Model;

        /// <summary>Bảng đã được tạo. Không hỏi PaletteSet có đang hiện hay không, vì cập nhật bảng ẩn cũng rẻ.</summary>
        public static bool IsOpen => _panel != null;

        public static bool IsGuiAvailable() =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => string.Equals(a.GetName().Name, "AcMgd", StringComparison.OrdinalIgnoreCase));

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void Show()
        {
            Autodesk.AutoCAD.Windows.PaletteSet palette;
            if (_palette == null)
            {
                var model = new PanelViewModel(new AcadHostActions());
                _panel = new MainPanel(model);
                _panel.CancelRequested += (_, __) => _running?.Cancel();
                model.ProfileSelected += OnProfileSelected;
                palette = new Autodesk.AutoCAD.Windows.PaletteSet("Piping Agent", new Guid("7E1D3C2A-4B6F-4D8E-9A21-5C0F3B7D9E10"))
                {
                    Style = Autodesk.AutoCAD.Windows.PaletteSetStyles.ShowAutoHideButton
                          | Autodesk.AutoCAD.Windows.PaletteSetStyles.ShowCloseButton
                          | Autodesk.AutoCAD.Windows.PaletteSetStyles.ShowPropertiesMenu,
                    MinimumSize = new System.Drawing.Size(320, 240),
                };
                palette.AddVisual("Piping Agent", _panel);
                _palette = palette;
                if (PluginApp.Sessions != null) PluginApp.Sessions.ActiveSessionChanged += (_, s) => ShowSession(s);
                LoadProfiles(model);
            }
            else
            {
                palette = (Autodesk.AutoCAD.Windows.PaletteSet)_palette;
            }

            palette.Visible = true;
            ShowSession(PluginApp.Sessions?.Active);
        }

        /// <summary>Gắn thước tiến độ đang chạy để nút Hủy của bảng hủy được.</summary>
        public static void SetRunning(AcadProgress? progress)
        {
            _running = progress;
            if (_panel == null) return;
            _panel.Model.Busy = progress != null;
            if (progress == null)
            {
                _panel.Model.Progress = 0;
                _panel.Model.ProgressText = string.Empty;
            }
        }

        /// <summary>Đổ dữ liệu phiên của bản vẽ đang hoạt động vào bảng. Gọi sau mỗi lần quét và khi chuyển bản vẽ.</summary>
        public static void ShowSession(DocumentSession? session)
        {
            if (_panel == null) return;
            var model = _panel.Model;
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
    }
}
