using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;

namespace PipingAgent.Core.Rules
{
    /// <summary>Ngữ cảnh một lần chạy luật: mô hình, hồ sơ, và hàm tạo vấn đề đã gán mức theo hồ sơ.</summary>
    public sealed class RuleContext
    {
        private readonly List<Issue> _issues = new List<Issue>();
        private int _no;

        public RuleContext(PipingModel model, ConventionProfile profile)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _no = model.Issues.Count;
        }

        public PipingModel Model { get; }

        public ConventionProfile Profile { get; }

        public IReadOnlyList<Issue> Issues => _issues;

        /// <summary>Tạo vấn đề theo mức của hồ sơ. Luật bị tắt thì trả về null và không thêm.</summary>
        public Issue? Report(string ruleId, string message, IEnumerable<string>? elements = null, IEnumerable<string>? sources = null, Point3? position = null)
        {
            var severity = RuleCatalog.Resolve(Profile, ruleId);
            if (severity == null) return null;
            _no++;
            var issue = new Issue
            {
                Id = "issue-" + _no.ToString("000", CultureInfo.InvariantCulture),
                RuleId = ruleId,
                Severity = severity.Value,
                Message = message,
                Position = position,
            };
            if (elements != null) issue.Elements.AddRange(elements);
            if (sources != null) issue.Sources.AddRange(sources.Distinct());
            _issues.Add(issue);
            return issue;
        }
    }

    /// <summary>Một luật kiểm tra, mục 9.2: có mã, đọc mô hình, tạo vấn đề qua ngữ cảnh. Không sửa mô hình.</summary>
    public interface IRule
    {
        string Id { get; }

        void Check(RuleContext context);
    }

    public sealed class RuleRunResult
    {
        public int RulesRun { get; set; }

        public int RulesOff { get; set; }

        public List<Issue> Issues { get; } = new List<Issue>();

        public List<string> Failures { get; } = new List<string>();

        public override string ToString() => $"{RulesRun} luật chạy, {RulesOff} luật tắt, {Issues.Count} vấn đề" + (Failures.Count > 0 ? $", {Failures.Count} luật lỗi" : string.Empty);
    }

    /// <summary>
    /// Bộ chạy luật, S4-07 và FR-CHK-01 đến 03: chạy từng luật, bỏ luật bị tắt trong hồ sơ, mức độ lấy từ hồ sơ,
    /// luật lỗi không làm dừng các luật khác. Vấn đề được thêm vào mô hình.
    /// </summary>
    public static class RuleRunner
    {
        public static IReadOnlyList<IRule> DefaultRules => new IRule[]
        {
            new UntaggedRunRule(),
            new OpenEndRule(),
            new ElevationRangeRule(),
            new UnverifiedCatalogRule(),
            new UnitsRule(),
            new ComponentOverlapRule(),
        };

        public static RuleRunResult Run(PipingModel model, ConventionProfile profile, IEnumerable<IRule>? rules = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var result = new RuleRunResult();
            var context = new RuleContext(model, profile);
            foreach (var rule in rules ?? DefaultRules)
            {
                if (RuleCatalog.Resolve(profile, rule.Id) == null)
                {
                    result.RulesOff++;
                    continue;
                }

                try
                {
                    rule.Check(context);
                    result.RulesRun++;
                }
                catch (System.Exception ex)
                {
                    result.Failures.Add($"{rule.Id}: {ex.Message}");
                }
            }

            result.Issues.AddRange(context.Issues);
            model.Issues.AddRange(context.Issues);
            return result;
        }

        /// <summary>R-TAG-01: đoạn chạy không có nhãn và không thừa hưởng được.</summary>
        public sealed class UntaggedRunRule : IRule
        {
            public string Id => "R-TAG-01";

            public void Check(RuleContext c)
            {
                foreach (var run in c.Model.Runs.Where(r => r.TagRef == null && string.IsNullOrEmpty(r.LineId)))
                {
                    var pos = run.PlanPoints.Count > 0 ? Box2.FromPoints(run.PlanPoints).Center.At(0) : (Point3?)null;
                    c.Report(Id, $"Đoạn chạy {run.Id} chưa có nhãn và không thừa hưởng được từ đoạn nối tiếp.", new[] { run.Id }, run.Prov?.Sources, pos);
                }
            }
        }

        /// <summary>R-CON-01: đầu ống hở, không phải đầu chờ thiết bị và không phải điểm nối tiếp.</summary>
        public sealed class OpenEndRule : IRule
        {
            public string Id => "R-CON-01";

            public void Check(RuleContext c)
            {
                foreach (var node in c.Model.Nodes.Where(n => n.Kind == NodeKind.OpenEnd))
                {
                    c.Report(Id, $"Đầu ống hở tại nút {node.Id} {node.Position}.", new[] { node.Id }, node.Prov?.Sources, node.Position);
                }
            }
        }

        /// <summary>R-ELV-03: cao độ ngoài khoảng hợp lý khai báo trong hồ sơ.</summary>
        public sealed class ElevationRangeRule : IRule
        {
            public string Id => "R-ELV-03";

            public void Check(RuleContext c)
            {
                var range = c.Profile.Tolerances.ElevationRange;
                if (range == null || range.Count != 2) return;
                foreach (var run in c.Model.Runs.Where(r => r.Elevation.Centerline.HasValue))
                {
                    var z = run.Elevation.Centerline!.Value;
                    if (z < range[0] || z > range[1])
                    {
                        c.Report(Id, string.Format(CultureInfo.InvariantCulture, "Đoạn chạy {0} có cao độ tâm {1:0} ngoài khoảng {2:0} đến {3:0}.", run.Id, z, range[0], range[1]),
                            new[] { run.Id }, run.Prov?.Sources, run.PlanPoints.Count > 0 ? run.PlanPoints[0].At(z) : (Point3?)null);
                    }
                }
            }
        }

        /// <summary>R-GEN-02: không xác định được đơn vị bản vẽ.</summary>
        public sealed class UnitsRule : IRule
        {
            public string Id => "R-GEN-02";

            public void Check(RuleContext c)
            {
                if (c.Model.Source.UnitsNeedConfirmation == true)
                {
                    c.Report(Id, "Bản vẽ không khai báo đơn vị. Xác nhận đơn vị hoặc đặt hệ số trước khi dựng.");
                }
            }
        }

        /// <summary>R-GEO-04: cấu kiện nối tiếp chồng lên nhau hoặc lên phụ kiện ở đầu đoạn ống.</summary>
        public sealed class ComponentOverlapRule : IRule
        {
            public string Id => "R-GEO-04";

            public void Check(RuleContext c)
            {
                var segments = c.Model.Segments.ToDictionary(s => s.Id, StringComparer.Ordinal);
                var nodes = c.Model.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
                foreach (var group in c.Model.Components.Where(x => !string.IsNullOrEmpty(x.SegmentId) && segments.ContainsKey(x.SegmentId)).GroupBy(x => x.SegmentId))
                {
                    var seg = segments[group.Key];
                    if (!nodes.TryGetValue(seg.NodeA, out var a) || !nodes.TryGetValue(seg.NodeB, out var b)) continue;
                    var axis = (b.Position - a.Position).Normalized();
                    var spans = group.Select(x => (x, T: (x.Position - a.Position).Dot(axis), Half: Math.Max(x.Length, 1) / 2)).OrderBy(s => s.T).ToList();
                    for (var i = 0; i < spans.Count; i++)
                    {
                        var s = spans[i];
                        if (s.T - s.Half < seg.TrimA - 0.01 || s.T + s.Half > seg.Length - seg.TrimB + 0.01)
                        {
                            c.Report(Id, $"Cấu kiện {s.x.Id} chồng lên phụ kiện ở đầu đoạn ống {seg.Id}.", new[] { s.x.Id, seg.Id }, s.x.Prov?.Sources, s.x.Position);
                        }

                        if (i > 0 && spans[i - 1].T + spans[i - 1].Half > s.T - s.Half + 0.01)
                        {
                            c.Report(Id, $"Cấu kiện {spans[i - 1].x.Id} và {s.x.Id} chồng lên nhau trên đoạn ống {seg.Id}.", new[] { spans[i - 1].x.Id, s.x.Id }, s.x.Prov?.Sources, s.x.Position);
                        }
                    }
                }
            }
        }

        /// <summary>R-CAT-03: mô hình dùng danh mục chưa được ký xác nhận. Một vấn đề mức thông tin cho cả mô hình.</summary>
        public sealed class UnverifiedCatalogRule : IRule
        {
            public string Id => "R-CAT-03";

            public void Check(RuleContext c)
            {
                if (c.Model.Segments.Count > 0 && c.Model.Generator?.CatalogVerified == false)
                {
                    c.Report(Id, "Kích thước ống và phụ kiện lấy từ danh mục chưa được chuyên gia ký xác nhận (S2-16).");
                }
            }
        }
    }
}
