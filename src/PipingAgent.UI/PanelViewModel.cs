using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;

namespace PipingAgent.UI
{
    /// <summary>
    /// Việc mà bảng điều khiển nhờ ứng dụng chủ làm. Bảng không gọi API CAD trực tiếp, S2-11,
    /// nên cùng một bảng dùng lại được cho Smart 3D ở mức tích hợp A, mục 6.12.1.
    /// </summary>
    public interface IHostActions
    {
        /// <summary>Tô sáng các đối tượng theo handle bằng đồ hoạ tạm thời, không đổi bản vẽ.</summary>
        void Highlight(IReadOnlyList<string> handles);

        void ClearHighlight();

        /// <summary>Phóng khung nhìn tới vùng này, mm.</summary>
        void ZoomTo(Box2 region);

        /// <summary>Quét lại bản vẽ đang mở với hồ sơ đang chọn.</summary>
        void Rescan();

        /// <summary>Mở hộp thoại chọn tệp hồ sơ, trả về đường dẫn hoặc null nếu huỷ.</summary>
        string? PickProfileFile();

        /// <summary>Bỏ qua một vấn đề với lý do, S5-10. Ứng dụng chủ ghi quyết định, chạy lại và cập nhật bảng.</summary>
        void WaiveIssue(string issueId, string reason);

        /// <summary>Chạy một lệnh của ứng dụng chủ, ví dụ PA_FIX, S5-08.</summary>
        void RunCommand(string command);

        /// <summary>Thử một mẫu nhãn trên chữ của bản vẽ đang mở, S5-13. Trả về dòng tóm tắt.</summary>
        string TestTagPattern(string regex);

        /// <summary>Ghi hồ sơ do trình soạn tạo ra tệp, hỏi đường dẫn. Trả về đường dẫn hoặc null.</summary>
        string? SaveProfileFile(string json);
    }

    public sealed class LineRow
    {
        public string Tag { get; set; } = string.Empty;

        public string Dn { get; set; } = string.Empty;

        public string Length { get; set; } = string.Empty;

        public int Runs { get; set; }

        public int Issues { get; set; }

        public List<string> Handles { get; set; } = new List<string>();

        public Box2? Region { get; set; }
    }

    /// <summary>Một layer hoặc block của bản vẽ và vai trò người dùng gán trong trình soạn hồ sơ, S5-12.</summary>
    public sealed class LayerRoleRow : ObservableObject
    {
        private string _role = EntityRoles.Unclassified;

        public string Name { get; set; } = string.Empty;

        public bool IsBlock { get; set; }

        public int Count { get; set; }

        public string Types { get; set; } = string.Empty;

        public string Role { get => _role; set => Set(ref _role, value); }
    }

    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }

        protected void Raise([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class RoleRow
    {
        public string Role { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int Count { get; set; }

        public string Share { get; set; } = string.Empty;
    }

    public sealed class LayerRowItem
    {
        public string Layer { get; set; } = string.Empty;

        public int Total { get; set; }

        public int Unclassified { get; set; }

        public string Share { get; set; } = string.Empty;

        public string Types { get; set; } = string.Empty;
    }

    public sealed class IssueRow
    {
        public string Id { get; set; } = string.Empty;

        public string Severity { get; set; } = string.Empty;

        public string Rule { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Line { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Suggestions { get; set; } = string.Empty;

        public List<string> Handles { get; set; } = new List<string>();

        public Box2? Region { get; set; }
    }

    public sealed class RunRow
    {
        public string Id { get; set; } = string.Empty;

        public string Line { get; set; } = string.Empty;

        public string Dn { get; set; } = string.Empty;

        public string Elevation { get; set; } = string.Empty;

        public string Tag { get; set; } = string.Empty;

        public string Method { get; set; } = string.Empty;

        public string Confidence { get; set; } = string.Empty;

        public string Length { get; set; } = string.Empty;

        public List<string> Handles { get; set; } = new List<string>();

        public Box2? Region { get; set; }
    }

    public sealed class ProfileChoice
    {
        public string Path { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public override string ToString() => Label;
    }

    /// <summary>Dữ liệu của bảng điều khiển. Không phụ thuộc WPF lẫn CAD, kiểm thử được bằng xUnit.</summary>
    public sealed class PanelViewModel : ObservableObject
    {
        private string _status = "Chưa có bản vẽ nào được quét. Gõ PA_SCAN hoặc bấm Quét.";
        private string _drawingName = string.Empty;
        private string _overviewSummary = string.Empty;
        private string _profileSummary = "Chưa chọn hồ sơ.";
        private string _profileErrors = string.Empty;
        private ProfileChoice? _selectedProfile;
        private RunRow? _selectedRun;
        private IssueRow? _selectedIssue;
        private bool _busy;
        private double _progress;
        private string _progressText = string.Empty;
        private string _buildPreview = string.Empty;
        private string _issueSeverityFilter = "Tất cả";
        private string _issueRuleFilter = string.Empty;
        private string _issueLineFilter = string.Empty;
        private string _issueStatusFilter = "Tất cả";
        private readonly List<IssueRow> _allIssues = new List<IssueRow>();
        private LineRow? _selectedLine;
        private string _waiveReason = string.Empty;
        private string _tagPatternText = string.Empty;
        private string _tagPatternResult = string.Empty;
        private string _editorProfileId = "ho-so-moi";

        public PanelViewModel(IHostActions? host)
        {
            Host = host;
        }

        public IHostActions? Host { get; set; }

        public string Status { get => _status; set => Set(ref _status, value); }

        public string DrawingName { get => _drawingName; set => Set(ref _drawingName, value); }

        public string OverviewSummary { get => _overviewSummary; set => Set(ref _overviewSummary, value); }

        public ObservableCollection<RoleRow> Roles { get; } = new ObservableCollection<RoleRow>();

        public ObservableCollection<LayerRowItem> UnclassifiedLayers { get; } = new ObservableCollection<LayerRowItem>();

        /// <summary>Danh sách vấn đề sau khi lọc, S4-13. Lọc theo mức, mã luật, tuyến, trạng thái, kết hợp được.</summary>
        public ObservableCollection<IssueRow> Issues { get; } = new ObservableCollection<IssueRow>();

        public string[] SeverityChoices { get; } = { "Tất cả", "CHẶN", "CẢNH BÁO", "THÔNG TIN" };

        public string[] StatusChoices { get; } = { "Tất cả", "Mở", "Đã sửa", "Miễn trừ" };

        public string IssueSeverityFilter { get => _issueSeverityFilter; set { if (Set(ref _issueSeverityFilter, value)) ApplyIssueFilter(); } }

        public string IssueRuleFilter { get => _issueRuleFilter; set { if (Set(ref _issueRuleFilter, value)) ApplyIssueFilter(); } }

        public string IssueLineFilter { get => _issueLineFilter; set { if (Set(ref _issueLineFilter, value)) ApplyIssueFilter(); } }

        public string IssueStatusFilter { get => _issueStatusFilter; set { if (Set(ref _issueStatusFilter, value)) ApplyIssueFilter(); } }

        public int IssueTotal => _allIssues.Count;

        /// <summary>Xem trước số đối tượng sẽ dựng, S4-14, do ứng dụng chủ tính từ kế hoạch dựng.</summary>
        public string BuildPreview { get => _buildPreview; set => Set(ref _buildPreview, value); }

        public void ApplyIssueFilter()
        {
            Issues.Clear();
            foreach (var i in _allIssues)
            {
                if (IssueSeverityFilter != "Tất cả" && i.Severity != IssueSeverityFilter) continue;
                if (IssueRuleFilter.Length > 0 && i.Rule.IndexOf(IssueRuleFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (IssueLineFilter.Length > 0 && i.Line.IndexOf(IssueLineFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (IssueStatusFilter != "Tất cả" && i.Status != IssueStatusFilter) continue;
                Issues.Add(i);
            }

            Raise(nameof(IssueTotal));
        }

        public ObservableCollection<RunRow> Runs { get; } = new ObservableCollection<RunRow>();

        /// <summary>Thẻ Tuyến ống, S5-11: mỗi tuyến kèm DN, chiều dài, số vấn đề.</summary>
        public ObservableCollection<LineRow> Lines { get; } = new ObservableCollection<LineRow>();

        public LineRow? SelectedLine
        {
            get => _selectedLine;
            set
            {
                if (Set(ref _selectedLine, value) && value != null) Focus(value.Handles, value.Region);
            }
        }

        /// <summary>Lý do bỏ qua vấn đề đang chọn. Bắt buộc, FR-CHK-06.</summary>
        public string WaiveReason { get => _waiveReason; set => Set(ref _waiveReason, value); }

        public string WaiveSelected()
        {
            if (SelectedIssue == null) return "Chưa chọn vấn đề.";
            if (string.IsNullOrWhiteSpace(WaiveReason)) return "Phải nhập lý do mới bỏ qua được.";
            if (Host == null) return "Không có ứng dụng chủ.";
            Host.WaiveIssue(SelectedIssue.Id, WaiveReason.Trim());
            WaiveReason = string.Empty;
            return "Đã ghi quyết định bỏ qua.";
        }

        // Trình soạn hồ sơ, S5-12 và S5-13.
        public ObservableCollection<LayerRoleRow> EditorRows { get; } = new ObservableCollection<LayerRoleRow>();

        public string[] RoleChoices { get; } = EntityRoles.All.Concat(new[] { EntityRoles.Unclassified }).ToArray();

        public string EditorProfileId { get => _editorProfileId; set => Set(ref _editorProfileId, value); }

        public string TagPatternText { get => _tagPatternText; set => Set(ref _tagPatternText, value); }

        public string TagPatternResult { get => _tagPatternResult; set => Set(ref _tagPatternResult, value); }

        public void TestTagPattern()
        {
            TagPatternResult = Host == null ? "Không có ứng dụng chủ." : Host.TestTagPattern(TagPatternText);
        }

        /// <summary>Đổ layer và block của lần quét gần nhất vào trình soạn, giữ vai trò đã gán nếu tên trùng.</summary>
        public void LoadEditorRows(IEnumerable<LayerRoleRow> rows)
        {
            var previous = EditorRows.ToDictionary(r => (r.Name, r.IsBlock), r => r.Role);
            EditorRows.Clear();
            foreach (var r in rows)
            {
                if (previous.TryGetValue((r.Name, r.IsBlock), out var role)) r.Role = role;
                EditorRows.Add(r);
            }
        }

        /// <summary>Hồ sơ từ các vai trò đã gán và mẫu nhãn đã thử, dạng JSON để ghi ra tệp.</summary>
        public string BuildProfileJson()
        {
            var p = new ConventionProfile { Id = string.IsNullOrWhiteSpace(EditorProfileId) ? "ho-so-moi" : EditorProfileId.Trim(), Extends = "base-default", PipeStandard = "ASME-B36.10", FittingStandard = "ASME-B16.9" };
            foreach (var r in EditorRows.Where(r => r.Role != EntityRoles.Unclassified))
            {
                var match = r.IsBlock ? new EntityMatch { Block = r.Name } : new EntityMatch { Layer = r.Name };
                p.Roles.Add(new RoleRule { Role = r.Role, Match = match });
            }

            if (!string.IsNullOrWhiteSpace(TagPatternText))
            {
                p.TagPatterns.Add(new TagPattern { Name = "mau-1", Regex = TagPatternText.Trim(), ElevationDatum = ElevationDatum.CL });
            }

            return ProfileLoader.Serialize(p);
        }

        public string SaveEditorProfile()
        {
            if (Host == null) return "Không có ứng dụng chủ.";
            var errors = ProfileLoader.Parse(BuildProfileJson()).Errors;
            if (errors.Count > 0) return "Hồ sơ chưa hợp lệ: " + errors[0];
            var path = Host.SaveProfileFile(BuildProfileJson());
            return path == null ? "Đã huỷ." : "Đã ghi " + path;
        }

        public ObservableCollection<ProfileChoice> Profiles { get; } = new ObservableCollection<ProfileChoice>();

        public ObservableCollection<string> LogLines { get; } = new ObservableCollection<string>();

        public string ProfileSummary { get => _profileSummary; set => Set(ref _profileSummary, value); }

        public string ProfileErrors { get => _profileErrors; set => Set(ref _profileErrors, value); }

        public ProfileChoice? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (Set(ref _selectedProfile, value)) ProfileSelected?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Người dùng vừa chọn hồ sơ khác. Ứng dụng chủ nạp hồ sơ và gọi <see cref="ShowProfile"/>.</summary>
        public event EventHandler? ProfileSelected;

        public bool Busy { get => _busy; set => Set(ref _busy, value); }

        public double Progress { get => _progress; set => Set(ref _progress, value); }

        public string ProgressText { get => _progressText; set => Set(ref _progressText, value); }

        public RunRow? SelectedRun
        {
            get => _selectedRun;
            set
            {
                if (Set(ref _selectedRun, value) && value != null) Focus(value.Handles, value.Region);
            }
        }

        public IssueRow? SelectedIssue
        {
            get => _selectedIssue;
            set
            {
                if (Set(ref _selectedIssue, value) && value != null) Focus(value.Handles, value.Region);
            }
        }

        public void ReportProgress(ProgressInfo info)
        {
            Progress = info.Fraction;
            ProgressText = info.Detail != null ? $"{info.Step}: {info.Detail}" : info.Step;
        }

        public void Focus(IReadOnlyList<string> handles, Box2? region)
        {
            if (Host == null) return;
            Host.ClearHighlight();
            if (region.HasValue) Host.ZoomTo(region.Value.Expand(Math.Max(region.Value.Width, region.Value.Height) * 0.25 + 500));
            if (handles.Count > 0) Host.Highlight(handles);
        }

        public void ShowProfile(ConventionProfile? profile, IEnumerable<ProfileError>? errors, string? path)
        {
            if (profile != null)
            {
                ProfileSummary = $"{profile.Id} v{profile.Version}: {profile.Roles.Count} quy tắc vai trò, {profile.TagPatterns.Count} mẫu nhãn, " +
                    $"{profile.ElevationPatterns.Count} mẫu cao độ, {profile.Blocks.Count} ánh xạ block. Tệp: {path}";
            }
            else
            {
                ProfileSummary = path != null ? $"Không nạp được hồ sơ {path}." : "Chưa chọn hồ sơ.";
            }

            ProfileErrors = errors == null ? string.Empty : string.Join(Environment.NewLine, errors.Select(e => e.ToString()));
        }

        /// <summary>Đổ kết quả nhận diện vào các thẻ. Gọi trên luồng giao diện.</summary>
        public void ShowResult(RecognitionResult result, string drawingName, IReadOnlyDictionary<string, Box2>? boundsByHandle = null, string? buildPreview = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            DrawingName = drawingName;
            BuildPreview = buildPreview ?? string.Empty;
            var r = result.Classification.Report;
            var m = result.Model;
            OverviewSummary = string.Format(CultureInfo.CurrentCulture,
                "{0} đối tượng, {1} chưa phân loại ({2:P1}). {3} tuyến, {4} đoạn chạy, {5} có nhãn. {6} vấn đề. {7:0.00} s.",
                r.Total, r.Unclassified, r.UnclassifiedRatio, m.Lines.Count, m.Runs.Count, m.Runs.Count(x => x.TagRef != null), m.Issues.Count, result.Elapsed.TotalSeconds);

            Roles.Clear();
            foreach (var kv in r.ByRole.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal))
            {
                Roles.Add(new RoleRow
                {
                    Role = kv.Key,
                    Label = RecognitionReport.RoleText(kv.Key),
                    Count = kv.Value,
                    Share = r.Total == 0 ? "0 %" : ((double)kv.Value / r.Total).ToString("P1", CultureInfo.CurrentCulture),
                });
            }

            UnclassifiedLayers.Clear();
            foreach (var l in r.Layers.Where(x => x.Unclassified > 0))
            {
                UnclassifiedLayers.Add(new LayerRowItem
                {
                    Layer = l.Layer,
                    Total = l.Total,
                    Unclassified = l.Unclassified,
                    Share = l.UnclassifiedRatio.ToString("P0", CultureInfo.CurrentCulture),
                    Types = string.Join(", ", l.UnclassifiedByType.OrderByDescending(t => t.Value).Take(4).Select(t => $"{t.Key} {t.Value}")),
                });
            }

            var lines = m.Lines.ToDictionary(l => l.Id, l => l.Tag, StringComparer.Ordinal);
            var runLine = m.Runs.ToDictionary(r => r.Id, r => lines.TryGetValue(r.LineId, out var t) ? t : string.Empty, StringComparer.Ordinal);
            _allIssues.Clear();
            foreach (var i in m.Issues.OrderBy(i => i.Severity).ThenBy(i => i.Id, StringComparer.Ordinal))
            {
                _allIssues.Add(new IssueRow
                {
                    Id = i.Id,
                    Severity = RecognitionReport.SeverityText(i.Severity),
                    Rule = i.RuleId,
                    Message = i.Message,
                    Line = string.Join(", ", i.Elements.Select(e => runLine.TryGetValue(e, out var t) ? t : string.Empty).Where(t => t.Length > 0).Distinct()),
                    Status = i.Status == IssueStatus.Open ? "Mở" : i.Status == IssueStatus.Fixed ? "Đã sửa" : "Miễn trừ",
                    Suggestions = string.Join("; ", i.Suggestions.Select(s => s.Label ?? s.Op.ToString())),
                    Handles = i.Sources.ToList(),
                    Region = RegionOf(i, m, boundsByHandle),
                });
            }

            ApplyIssueFilter();

            Runs.Clear();
            foreach (var run in m.Runs)
            {
                var length = 0.0;
                for (var k = 1; k < run.PlanPoints.Count; k++) length += run.PlanPoints[k - 1].DistanceTo(run.PlanPoints[k]);
                Runs.Add(new RunRow
                {
                    Id = run.Id,
                    Line = lines.TryGetValue(run.LineId, out var tag) ? tag : string.Empty,
                    Dn = run.Dn > 0 ? "DN" + run.Dn.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    Elevation = run.Elevation.Datum == ElevationDatum.Unknown
                        ? string.Empty
                        : string.Format(CultureInfo.InvariantCulture, "{0:0} {1}", run.Elevation.Raw, run.Elevation.Datum),
                    Tag = run.TagRef?.Text ?? string.Empty,
                    Method = run.TagRef?.By ?? string.Empty,
                    Confidence = run.TagRef?.Confidence?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
                    Length = string.Format(CultureInfo.InvariantCulture, "{0:0.0} m", length / 1000.0),
                    Handles = run.Prov?.Sources.ToList() ?? new List<string>(),
                    Region = run.PlanPoints.Count > 0 ? Box2.FromPoints(run.PlanPoints) : (Box2?)null,
                });
            }

            Lines.Clear();
            foreach (var line in m.Lines.OrderBy(l => l.Tag, StringComparer.OrdinalIgnoreCase))
            {
                var runs = m.Runs.Where(r => r.LineId == line.Id).ToList();
                var runIds = new HashSet<string>(runs.Select(r => r.Id), StringComparer.Ordinal);
                var length = m.Segments.Where(s => s.LineId == line.Id).Sum(s => s.Length);
                if (length <= 0) length = runs.Sum(r => r.PlanPoints.Zip(r.PlanPoints.Skip(1), (a, b) => a.DistanceTo(b)).Sum());
                var pts = runs.SelectMany(r => r.PlanPoints).ToList();
                Lines.Add(new LineRow
                {
                    Tag = line.Tag,
                    Dn = string.Join(", ", runs.Where(r => r.Dn > 0).Select(r => r.Dn).Distinct().OrderBy(d => d).Select(d => "DN" + d)),
                    Length = string.Format(CultureInfo.InvariantCulture, "{0:0.0} m", length / 1000.0),
                    Runs = runs.Count,
                    Issues = m.Issues.Count(i => i.Status == IssueStatus.Open && i.Elements.Any(runIds.Contains)),
                    Handles = runs.SelectMany(r => r.Prov?.Sources ?? new List<string>()).Distinct().ToList(),
                    Region = pts.Count > 0 ? Box2.FromPoints(pts) : (Box2?)null,
                });
            }

            Status = result.Cancelled ? "Đã hủy giữa chừng, kết quả không đầy đủ." : $"Đã quét {drawingName}.";
        }

        public void AppendLog(string line)
        {
            LogLines.Add(line);
            while (LogLines.Count > 500) LogLines.RemoveAt(0);
        }

        public void SetLog(IEnumerable<string> lines)
        {
            LogLines.Clear();
            foreach (var l in lines) LogLines.Add(l);
        }

        private static Box2? RegionOf(Issue issue, PipingModel model, IReadOnlyDictionary<string, Box2>? boundsByHandle)
        {
            Box2? box = null;
            if (boundsByHandle != null)
            {
                foreach (var h in issue.Sources)
                {
                    if (boundsByHandle.TryGetValue(h, out var b)) box = box.HasValue ? box.Value.Union(b) : b;
                }
            }

            foreach (var id in issue.Elements)
            {
                var run = model.Runs.FirstOrDefault(x => x.Id == id);
                if (run != null && run.PlanPoints.Count > 0)
                {
                    var b = Box2.FromPoints(run.PlanPoints);
                    box = box.HasValue ? box.Value.Union(b) : b;
                }
            }

            if (!box.HasValue && issue.Position.HasValue) box = Box2.Around(issue.Position.Value.Plan, 1000);
            return box;
        }
    }
}
