using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PipingAgent.Core.Equipment;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Rules;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Recognition
{
    /// <summary>Hình bao của một thiết bị trên mặt bằng: tâm, cạnh dài, cạnh ngắn, góc xoay của cạnh dài.</summary>
    public sealed class Footprint
    {
        public Point2 Center { get; set; }

        public double Length { get; set; }

        public double Width { get; set; }

        public double RotationDeg { get; set; }

        /// <summary><c>circle</c>, <c>closed_rectangle</c>, <c>closed_polyline</c>, <c>block</c>.</summary>
        public string Shape { get; set; } = string.Empty;

        public double? Radius { get; set; }

        public List<Point2> Outline { get; } = new List<Point2>();

        /// <summary>Khoảng cách từ điểm tới biên hình bao, 0 nếu nằm trên biên.</summary>
        public double DistanceToBoundary(Point2 p)
        {
            if (Radius.HasValue) return Math.Abs(p.DistanceTo(Center) - Radius.Value);
            var best = double.MaxValue;
            for (var i = 0; i < Outline.Count; i++)
            {
                var s = new Segment2(Outline[i], Outline[(i + 1) % Outline.Count]);
                best = Math.Min(best, s.DistanceTo(p));
            }

            return best;
        }

        public bool Contains(Point2 p)
        {
            if (Radius.HasValue) return p.DistanceTo(Center) <= Radius.Value;
            var inside = false;
            for (int i = 0, j = Outline.Count - 1; i < Outline.Count; j = i++)
            {
                var a = Outline[i];
                var b = Outline[j];
                if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }

            return inside;
        }

        public Box2 Bounds => Radius.HasValue ? Box2.Around(Center, Radius.Value) : Box2.FromPoints(Outline);
    }

    public sealed class EquipmentStats
    {
        public int Equipment { get; set; }

        public int GenericBoxes { get; set; }

        public int Untagged { get; set; }

        public override string ToString() => $"{Equipment} thiết bị, {GenericBoxes} hộp bao dự phòng, {Untagged} không số hiệu";
    }

    /// <summary>
    /// Nhận diện thiết bị theo <c>equipmentRules</c>, S4-04: khớp theo hình, tên block, tiền tố số hiệu; lấy tham số từ hình bao
    /// và chữ gần đó theo định nghĩa trong thư viện. Loại chưa có định nghĩa thành <c>generic_box</c> kèm R-EQP-04.
    /// </summary>
    public sealed class EquipmentRecognizer
    {
        private readonly ConventionProfile _profile;
        private readonly EquipmentLibrary _library;

        public EquipmentRecognizer(ConventionProfile profile, EquipmentLibrary library)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        public Dictionary<string, Footprint> Footprints { get; } = new Dictionary<string, Footprint>(StringComparer.Ordinal);

        public EquipmentStats Recognize(PipingModel model, IEnumerable<SourceEntity> shapes, IEnumerable<SourceEntity> tags)
        {
            var stats = new EquipmentStats();
            var tagList = tags.Where(t => !string.IsNullOrWhiteSpace(t.Text) && t.Center.HasValue).ToList();
            var usedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var issueNo = model.Issues.Count;
            var n = model.Equipment.Count;
            foreach (var e in shapes.OrderBy(s => s.Handle, StringComparer.Ordinal))
            {
                var fp = FootprintOf(e);
                if (fp == null) continue;
                var rule = _profile.EquipmentRules.FirstOrDefault(r => Matches(r, e, fp, tagList));
                if (rule == null) continue;

                var nearTexts = tagList.Where(t => !usedTags.Contains(t.Handle) && NearFootprint(fp, t)).OrderBy(t => t.Center!.Value.Plan.DistanceTo(fp.Center)).ToList();
                var definition = _library.Find(rule.Type);
                var type = rule.Type;
                n++;
                var eq = new Ir.Equipment
                {
                    Id = "eq-" + n.ToString("000", CultureInfo.InvariantCulture),
                    Type = type,
                    Geometry = new Placement { Origin = fp.Center.At(0), RotationDeg = fp.RotationDeg },
                    Prov = new Provenance { Sources = new List<string> { e.Handle }, Rule = RecognitionRules.EquipmentFromShape },
                };
                if (definition == null)
                {
                    eq.Type = EquipmentLibrary.GenericBox;
                    definition = _library.Find(EquipmentLibrary.GenericBox);
                    stats.GenericBoxes++;
                    AddIssue(model, ref issueNo, "R-EQP-04", $"Loại thiết bị '{type}' chưa có định nghĩa trong thư viện, dựng tạm bằng hộp bao.", eq.Id, e, fp);
                }

                // Số hiệu: chữ gần nhất không phải tham số, ưu tiên chữ có dạng số hiệu.
                var tagText = nearTexts.FirstOrDefault(t => LooksLikeTag(t.Text!));
                if (tagText != null)
                {
                    eq.Tag = CadText.Normalize(tagText.Text);
                    eq.Prov.Sources.Add(tagText.Handle);
                    usedTags.Add(tagText.Handle);
                }
                else
                {
                    stats.Untagged++;
                    AddIssue(model, ref issueNo, "R-EQP-01", $"Thiết bị {eq.Id} tại {fp.Center} không có số hiệu.", eq.Id, e, fp);
                }

                if (definition != null) FillParams(model, ref issueNo, eq, definition, fp, nearTexts, e, usedTags);
                eq.Geometry.Params["L"] = eq.Geometry.Params.TryGetValue("L", out var l) ? l : fp.Length;
                eq.Geometry.Params["W"] = eq.Geometry.Params.TryGetValue("W", out var w) ? w : fp.Width;
                Footprints[eq.Id] = fp;
                model.Equipment.Add(eq);
                stats.Equipment++;
            }

            return stats;
        }

        private void FillParams(PipingModel model, ref int issueNo, Ir.Equipment eq, EquipmentDefinition def, Footprint fp, List<SourceEntity> nearTexts, SourceEntity shape, HashSet<string> usedTags)
        {
            foreach (var p in def.Params)
            {
                double? value = null;
                switch (p.From)
                {
                    case "footprint.length": value = fp.Length; break;
                    case "footprint.width": value = fp.Width; break;
                    case "footprint.diameter": value = fp.Radius.HasValue ? fp.Radius.Value * 2 : Math.Max(fp.Length, fp.Width); break;
                    case "tag":
                        var rx = new Regex(p.Pattern!, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                        foreach (var t in nearTexts)
                        {
                            var m = rx.Match(CadText.Normalize(t.Text));
                            if (m.Success && double.TryParse(m.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                            {
                                value = v;
                                eq.Prov?.Sources.Add(t.Handle);
                                usedTags.Add(t.Handle);
                                break;
                            }
                        }

                        break;
                    case "attribute":
                        if (shape.Attributes != null && shape.Attributes.TryGetValue(p.Pattern!, out var text)
                            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var av))
                        {
                            value = av;
                        }

                        break;
                }

                if (!value.HasValue) value = p.Default;
                if (!value.HasValue)
                {
                    AddIssue(model, ref issueNo, "R-EQP-02", $"Thiết bị {eq.Tag ?? eq.Id} thiếu tham số '{p.Name}' cần để dựng.", eq.Id, shape, fp);
                    continue;
                }

                eq.Geometry.Params[p.Name] = value.Value;
            }
        }

        private bool Matches(EquipmentRule r, SourceEntity e, Footprint fp, List<SourceEntity> tags)
        {
            var m = r.Match;
            if (m.Shape != null && !string.Equals(m.Shape, fp.Shape, StringComparison.OrdinalIgnoreCase)) return false;
            if (m.Layer != null && !Wildcard.Match(m.Layer, e.Layer)) return false;
            if (m.Block != null && (e.Kind != SourceKind.Insert || !Wildcard.Match(m.Block, e.Name))) return false;
            if (m.TagPrefix != null)
            {
                var has = tags.Any(t => NearFootprint(fp, t) && CadText.Normalize(t.Text).StartsWith(m.TagPrefix, StringComparison.OrdinalIgnoreCase));
                if (!has) return false;
            }

            return true;
        }

        private bool NearFootprint(Footprint fp, SourceEntity t)
        {
            var p = t.Center!.Value.Plan;
            if (fp.Contains(p)) return true;
            var reach = Math.Max(t.TextHeight ?? 0, 1) * 2;
            return fp.DistanceToBoundary(p) <= reach || (t.Bounds.HasValue && fp.Bounds.Expand(reach).Intersects(t.Bounds.Value));
        }

        private static bool LooksLikeTag(string text)
        {
            var s = CadText.Normalize(text);
            return Regex.IsMatch(s, "^[A-Z]{1,4}-?\\d{2,}[A-Z]?(?:/[A-Z0-9]+)?$");
        }

        public static Footprint? FootprintOf(SourceEntity e)
        {
            if (e.Kind == SourceKind.Circle && e.Center.HasValue && e.Radius.HasValue)
            {
                var r = e.Radius.Value;
                return new Footprint { Center = e.Center.Value.Plan, Length = 2 * r, Width = 2 * r, Radius = r, Shape = "circle" };
            }

            if (e.Kind == SourceKind.Insert && e.Center.HasValue)
            {
                var b = e.Bounds ?? Box2.Around(e.Center.Value.Plan, 1);
                var fp = new Footprint { Center = b.Center, Length = Math.Max(b.Width, b.Height), Width = Math.Min(b.Width, b.Height), RotationDeg = b.Width >= b.Height ? 0 : 90, Shape = "block" };
                fp.Outline.AddRange(new[] { b.Min, new Point2(b.Max.X, b.Min.Y), b.Max, new Point2(b.Min.X, b.Max.Y) });
                return fp;
            }

            if (e.Kind == SourceKind.Polyline && e.Closed == true && e.Points != null && e.Points.Count >= 3)
            {
                var pts = e.Points.Select(p => p.Plan).ToList();
                if (pts.Count > 3 && pts[0].IsSame(pts[pts.Count - 1], Tolerance.Default)) pts.RemoveAt(pts.Count - 1);
                var fp = new Footprint();
                fp.Outline.AddRange(pts);
                var tol = new Tolerance(1.0, 1.0);
                if (pts.Count == 4 && IsRectangle(pts, tol))
                {
                    var d1 = pts[1] - pts[0];
                    var d2 = pts[2] - pts[1];
                    var longEdge = d1.Length >= d2.Length ? d1 : d2;
                    fp.Length = Math.Max(d1.Length, d2.Length);
                    fp.Width = Math.Min(d1.Length, d2.Length);
                    fp.RotationDeg = Math.Round(longEdge.AngleRad * 180 / Math.PI, 3) % 180;
                    fp.Shape = "closed_rectangle";
                    fp.Center = new Point2(pts.Average(p => p.X), pts.Average(p => p.Y));
                }
                else
                {
                    var b = Box2.FromPoints(pts);
                    fp.Length = Math.Max(b.Width, b.Height);
                    fp.Width = Math.Min(b.Width, b.Height);
                    fp.RotationDeg = b.Width >= b.Height ? 0 : 90;
                    fp.Shape = "closed_polyline";
                    fp.Center = b.Center;
                }

                return fp;
            }

            return null;
        }

        private static bool IsRectangle(List<Point2> p, Tolerance tol)
        {
            for (var i = 0; i < 4; i++)
            {
                var a = p[(i + 1) % 4] - p[i];
                var b = p[(i + 2) % 4] - p[(i + 1) % 4];
                if (!a.IsPerpendicularTo(b, tol)) return false;
            }

            return true;
        }

        private void AddIssue(PipingModel model, ref int issueNo, string ruleId, string message, string element, SourceEntity source, Footprint fp)
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
                Position = fp.Center.At(0),
            };
            issue.Elements.Add(element);
            issue.Sources.Add(source.Handle);
            model.Issues.Add(issue);
        }
    }
}
