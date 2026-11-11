using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Rules;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Recognition
{
    /// <summary>Kết quả tra tên block trong bảng <c>blocks</c> của hồ sơ.</summary>
    public sealed class BlockMatch
    {
        public BlockMatch(BlockRule rule)
        {
            Rule = rule;
        }

        public BlockRule Rule { get; }

        public string? Component => Rule.Component;

        public string? Structure => Rule.Structure;

        public string? Equipment => Rule.Equipment;

        public string? Symbol => Rule.Symbol;
    }

    /// <summary>Tra tên block theo mẫu trong hồ sơ, không phân biệt hoa thường, quy tắc đầu tiên khớp thắng.</summary>
    public sealed class BlockMatcher
    {
        private readonly (Wildcard Pattern, BlockRule Rule)[] _rules;

        public BlockMatcher(ConventionProfile profile)
        {
            _rules = (profile ?? throw new ArgumentNullException(nameof(profile))).Blocks
                .Where(b => !string.IsNullOrWhiteSpace(b.Name))
                .Select(b => (new Wildcard(b.Name), b)).ToArray();
        }

        public BlockMatch? Match(string? blockName)
        {
            if (string.IsNullOrEmpty(blockName)) return null;
            foreach (var (pattern, rule) in _rules)
            {
                if (pattern.IsMatch(blockName)) return new BlockMatch(rule);
            }

            return null;
        }
    }

    public sealed class ComponentStats
    {
        public int Components { get; set; }

        public int Unmapped { get; set; }

        public int OffPipe { get; set; }

        public override string ToString() => $"{Components} cấu kiện, {Unmapped} block chưa ánh xạ, {OffPipe} không nằm trên ống";
    }

    /// <summary>
    /// Nhận diện van và cấu kiện nối tiếp từ block, S4-01 và S4-18: tìm đoạn ống chứa nó, vị trí, hướng trục, chiều dài danh mục,
    /// đọc thuộc tính block theo ánh xạ của hồ sơ. Block không nằm trên ống nào thì R-GEO-06, block chưa ánh xạ thì R-CAT-02.
    /// </summary>
    public sealed class ComponentRecognizer
    {
        private readonly ConventionProfile _profile;
        private readonly PipeCatalog? _catalog;
        private readonly BlockMatcher _blocks;

        public ComponentRecognizer(ConventionProfile profile, PipeCatalog? catalog)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _catalog = catalog;
            _blocks = new BlockMatcher(profile);
        }

        public ComponentStats Recognize(PipingModel model, IEnumerable<SourceEntity> inserts)
        {
            var stats = new ComponentStats();
            var nodes = model.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            var segments = model.Segments.Where(s => nodes.ContainsKey(s.NodeA) && nodes.ContainsKey(s.NodeB)).ToList();
            var tolerance = Math.Max(_profile.Tolerances.ComponentTolerance, _profile.Tolerances.Snap);
            var issueNo = model.Issues.Count;
            var n = model.Components.Count;
            foreach (var insert in inserts.Where(i => i.Kind == SourceKind.Insert && i.Center.HasValue).OrderBy(i => i.Handle, StringComparer.Ordinal))
            {
                var match = _blocks.Match(insert.Name);
                if (match != null && match.Component == null)
                {
                    // Block ánh xạ sang kết cấu, thiết bị hoặc ký hiệu phụ trợ: không phải cấu kiện nối tiếp.
                    continue;
                }

                var type = match?.Component ?? "unclassified";
                if (match == null)
                {
                    stats.Unmapped++;
                    AddIssue(model, ref issueNo, "R-CAT-02", $"Block '{insert.Name}' trên layer cấu kiện chưa được ánh xạ trong hồ sơ.", null, insert);
                }

                var plan = insert.Center!.Value.Plan;
                Segment? best = null;
                var bestD = double.MaxValue;
                foreach (var s in segments)
                {
                    if (s.Kind == SegmentKind.Riser) continue;
                    var seg = new Segment2(nodes[s.NodeA].Position.Plan, nodes[s.NodeB].Position.Plan);
                    var d = seg.DistanceTo(plan);
                    if (d <= tolerance && d < bestD)
                    {
                        bestD = d;
                        best = s;
                    }
                }

                n++;
                var component = new Component
                {
                    Id = "cmp-" + n.ToString("000", CultureInfo.InvariantCulture),
                    Type = type,
                    Position = insert.Center.Value,
                    Prov = new Provenance { Sources = new List<string> { insert.Handle }, Rule = RecognitionRules.ComponentFromBlock, Confidence = match == null ? 0.5 : 1.0 },
                };
                ReadAttributes(component, insert, match);
                if (best != null)
                {
                    var a = nodes[best.NodeA].Position;
                    var b = nodes[best.NodeB].Position;
                    var axis = (b - a).Normalized();
                    var seg = new Segment2(a.Plan, b.Plan);
                    var t = seg.ProjectParameter(plan);
                    component.SegmentId = best.Id;
                    component.Position = a + axis * (t * a.DistanceTo(b));
                    component.Axis = axis;
                    component.Dn = best.Dn;
                    component.Length = CatalogLength(type, best.Dn);
                }
                else
                {
                    stats.OffPipe++;
                    AddIssue(model, ref issueNo, "R-GEO-06", $"Cấu kiện '{insert.Name}' tại {plan} không nằm trên đoạn ống nào trong {tolerance:0.#} mm.", component.Id, insert);
                }

                model.Components.Add(component);
                stats.Components++;
            }

            return stats;
        }

        private double CatalogLength(string type, int dn)
        {
            if (_catalog == null || dn <= 0) return 0;
            var standard = _profile.ValveStandard ?? "ASME-B16.10";
            var r = _catalog.FittingDim(standard, type.ToUpperInvariant(), dn, "L");
            return r.Found ? r.Value : 0;
        }

        /// <summary>Thuộc tính block sang thuộc tính cấu kiện theo ánh xạ. Thuộc tính không có trong ánh xạ giữ nguyên tên, viết thường.</summary>
        private static void ReadAttributes(Component component, SourceEntity insert, BlockMatch? match)
        {
            if (insert.Attributes == null || insert.Attributes.Count == 0) return;
            var map = match?.Rule.Attributes ?? new Dictionary<string, string>();
            component.Attributes ??= new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
            foreach (var kv in insert.Attributes)
            {
                if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                var target = map.FirstOrDefault(m => string.Equals(m.Key, kv.Key, StringComparison.OrdinalIgnoreCase)).Value;
                var value = new AttributeValue { Value = kv.Value.Trim(), By = "block_attribute", SourceHandle = insert.Handle, Confidence = 1.0 };
                if (target == "tag")
                {
                    component.Tag = value.Value;
                    component.TagRef = new TagRef { Text = value.Value, By = "block_attribute", SourceHandle = insert.Handle, Confidence = 1.0 };
                }
                else if (target != null && target.StartsWith("attributes.", StringComparison.Ordinal))
                {
                    component.Attributes[target.Substring("attributes.".Length)] = value;
                }
                else
                {
                    component.Attributes[kv.Key.ToLowerInvariant()] = value;
                }
            }

            if (component.Attributes.Count == 0) component.Attributes = null;
        }

        private void AddIssue(PipingModel model, ref int issueNo, string ruleId, string message, string? element, SourceEntity source)
        {
            var severity = RuleCatalog.Resolve(_profile, ruleId);
            if (severity == null) return;
            issueNo++;
            var issue = new Issue
            {
                Id = "issue-" + issueNo.ToString("000", CultureInfo.InvariantCulture),
                RuleId = ruleId,
                Severity = severity.Value,
                Message = message,
                Position = source.Center,
            };
            if (element != null) issue.Elements.Add(element);
            issue.Sources.Add(source.Handle);
            model.Issues.Add(issue);
        }
    }
}
