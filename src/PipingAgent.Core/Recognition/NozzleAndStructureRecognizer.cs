using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Rules;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Recognition
{
    public sealed class NozzleStats
    {
        public int Nozzles { get; set; }

        public int OpenEnds { get; set; }

        public int NearMisses { get; set; }

        public override string ToString() => $"{Nozzles} đầu chờ, {OpenEnds} đầu hở, {NearMisses} gần thiết bị mà không chạm";
    }

    /// <summary>
    /// Nhận diện đầu chờ, S4-05 và FR-REC-12: đầu ống kết thúc trên biên thiết bị thành nút đầu chờ, ghi thiết bị được nối.
    /// Đầu ống gần thiết bị nhưng không chạm biên thì R-CON-05.
    /// </summary>
    public sealed class NozzleRecognizer
    {
        private readonly ConventionProfile _profile;

        public NozzleRecognizer(ConventionProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public NozzleStats Recognize(PipingModel model, IReadOnlyDictionary<string, Footprint> footprints)
        {
            var stats = new NozzleStats();
            var issueNo = model.Issues.Count;
            var segments = model.Segments.ToDictionary(s => s.Id, StringComparer.Ordinal);
            var nodes = model.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            var tolerance = Math.Max(_profile.Tolerances.NozzleTolerance, _profile.Tolerances.Snap);
            foreach (var node in model.Nodes.Where(n => n.Kind == NodeKind.OpenEnd && n.Segments.Count == 1))
            {
                var plan = node.Position.Plan;
                Ir.Equipment? hit = null;
                Footprint? hitFp = null;
                var best = double.MaxValue;
                foreach (var eq in model.Equipment)
                {
                    if (!footprints.TryGetValue(eq.Id, out var fp)) continue;
                    var d = fp.DistanceToBoundary(plan);
                    if (d < best)
                    {
                        best = d;
                        hit = eq;
                        hitFp = fp;
                    }
                }

                if (hit != null && best <= tolerance)
                {
                    var seg = segments[node.Segments[0]];
                    var other = nodes[seg.NodeA == node.Id ? seg.NodeB : seg.NodeA];
                    var outward = (other.Position - node.Position).Normalized();
                    var nozzle = new Nozzle
                    {
                        Id = hit.Id + "-n" + (hit.Nozzles.Count + 1).ToString(CultureInfo.InvariantCulture),
                        Position = node.Position,
                        Direction = outward,
                        Dn = seg.Dn,
                    };
                    hit.Nozzles.Add(nozzle);
                    node.Kind = NodeKind.Nozzle;
                    node.EquipmentId = hit.Id;
                    node.NozzleId = nozzle.Id;
                    if (node.Prov != null)
                    {
                        node.Prov.Rule = RecognitionRules.NozzleFromBoundary;
                        node.Prov.Sources.AddRange(hit.Prov?.Sources ?? Enumerable.Empty<string>());
                    }

                    stats.Nozzles++;
                }
                else
                {
                    stats.OpenEnds++;
                    if (hit != null && best <= _profile.Tolerances.NearEquipment)
                    {
                        stats.NearMisses++;
                        var severity = RuleCatalog.Resolve(_profile, "R-CON-05");
                        if (severity != null)
                        {
                            issueNo++;
                            var issue = new Issue
                            {
                                Id = "issue-" + issueNo.ToString("000", CultureInfo.InvariantCulture),
                                RuleId = "R-CON-05",
                                Severity = severity.Value,
                                Message = string.Format(CultureInfo.InvariantCulture, "Đầu ống {0} cách biên thiết bị {1} {2:0.#} mm nhưng không chạm.", node.Id, hit.Tag ?? hit.Id, best),
                                Position = node.Position,
                            };
                            issue.Elements.Add(node.Id);
                            issue.Elements.Add(hit.Id);
                            issue.Sources.AddRange(node.Prov?.Sources ?? Enumerable.Empty<string>());
                            model.Issues.Add(issue);
                        }
                    }
                }
            }

            return stats;
        }
    }

    public sealed class StructureStats
    {
        public int Columns { get; set; }

        public int Beams { get; set; }

        public int WithoutTos { get; set; }

        public override string ToString() => $"{Columns} cột, {Beams} dầm, {WithoutTos} không có cao độ mặt trên";
    }

    /// <summary>
    /// Nhận diện giá đỡ, S4-06 và FR-REC-13: block ánh xạ <c>structure: column</c> thành cột, nét thẳng trên layer kết cấu thành dầm,
    /// cao độ mặt trên từ chữ khớp <c>structureRules.tosPattern</c> gần nhất.
    /// </summary>
    public sealed class StructureRecognizer
    {
        private readonly ConventionProfile _profile;
        private readonly BlockMatcher _blocks;

        public StructureRecognizer(ConventionProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _blocks = new BlockMatcher(profile);
        }

        public StructureStats Recognize(PipingModel model, IEnumerable<SourceEntity> structures, IEnumerable<SourceEntity> tags)
        {
            var stats = new StructureStats();
            var tosTexts = new List<(Point2 At, double Tos, string Handle)>();
            if (!string.IsNullOrEmpty(_profile.StructureRules?.TosPattern))
            {
                var rx = new Regex(_profile.StructureRules!.TosPattern!, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                foreach (var t in tags.Where(t => t.Text != null && t.Center.HasValue))
                {
                    var m = rx.Match(CadText.Normalize(t.Text));
                    if (m.Success && double.TryParse(m.Groups["tos"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    {
                        tosTexts.Add((t.Center!.Value.Plan, v, t.Handle));
                    }
                }
            }

            var n = model.Structures.Count;
            foreach (var e in structures.OrderBy(s => s.Handle, StringComparer.Ordinal))
            {
                Structure? st = null;
                if (e.Kind == SourceKind.Insert && e.Center.HasValue)
                {
                    var match = _blocks.Match(e.Name);
                    if (match?.Structure == null) continue;
                    var tos = NearestTos(tosTexts, e.Center.Value.Plan);
                    var top = tos?.Tos ?? _profile.Tolerances.DefaultEquipmentHeight;
                    st = new Structure
                    {
                        Type = match.Structure,
                        Points = new List<Point3> { e.Center.Value.Plan.At(0), e.Center.Value.Plan.At(top) },
                        Tos = tos?.Tos,
                        Section = SectionOf(e),
                    };
                    if (match.Structure == "column") stats.Columns++;
                }
                else if (e.Kind == SourceKind.Line && e.Points != null && e.Points.Count == 2)
                {
                    var a = e.Points[0].Plan;
                    var b = e.Points[1].Plan;
                    var tos = NearestTos(tosTexts, new Point2((a.X + b.X) / 2, (a.Y + b.Y) / 2));
                    var top = tos?.Tos ?? _profile.Tolerances.DefaultEquipmentHeight;
                    st = new Structure { Type = "beam", Points = new List<Point3> { a.At(top), b.At(top) }, Tos = tos?.Tos };
                    stats.Beams++;
                }

                if (st == null) continue;
                n++;
                st.Id = "str-" + n.ToString("000", CultureInfo.InvariantCulture);
                st.Prov = new Provenance { Sources = new List<string> { e.Handle }, Rule = RecognitionRules.StructureFromEntity, Confidence = st.Tos.HasValue ? 1.0 : 0.6 };
                var tosHandle = NearestTos(tosTexts, st.Points[0].Plan)?.Handle;
                if (st.Tos.HasValue && tosHandle != null) st.Prov.Sources.Add(tosHandle);
                if (!st.Tos.HasValue) stats.WithoutTos++;
                model.Structures.Add(st);
            }

            return stats;
        }

        private static (Point2 At, double Tos, string Handle)? NearestTos(List<(Point2 At, double Tos, string Handle)> texts, Point2 p)
        {
            if (texts.Count == 0) return null;
            return texts.OrderBy(t => t.At.DistanceTo(p)).First();
        }

        private static string? SectionOf(SourceEntity e)
        {
            if (e.Attributes != null)
            {
                foreach (var key in new[] { "SECTION", "SIZE", "PROFILE" })
                {
                    if (e.Attributes.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }

            return null;
        }
    }
}
