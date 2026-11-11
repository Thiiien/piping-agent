using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Rules;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Recognition
{
    /// <summary>Cách một nhãn được gắn vào đoạn chạy, theo thứ tự ưu tiên của mục 9.4.</summary>
    public static class AttachMethods
    {
        public const string Leader = "leader";
        public const string Adjacent = "adjacent";
        public const string Nearest = "nearest";

        /// <summary>Chữ cao độ ghép với nhãn tuyến đứng cạnh, theo đoạn chạy của nhãn đó.</summary>
        public const string Paired = "paired";
    }

    public sealed class RunCandidate
    {
        public RunCandidate(Run run, double distance)
        {
            Run = run;
            Distance = distance;
        }

        public Run Run { get; }

        /// <summary>Khoảng cách từ nhãn tới đoạn chạy, mm. Với đường dẫn là khoảng cách đầu mũi tên tới đoạn chạy.</summary>
        public double Distance { get; }
    }

    /// <summary>Kết quả gắn một nhãn.</summary>
    public sealed class TagAttachment
    {
        public TagAttachment(TagReadResult tag)
        {
            Tag = tag;
        }

        public TagReadResult Tag { get; }

        /// <summary>Đoạn chạy đã gắn, null nếu không gắn.</summary>
        public Run? Run { get; set; }

        public string? Method { get; set; }

        public double Confidence { get; set; }

        /// <summary>Các đoạn chạy ứng viên ở mức đã chọn, xếp theo khoảng cách tăng dần.</summary>
        public List<RunCandidate> Candidates { get; } = new List<RunCandidate>();

        /// <summary>Handle đường dẫn đã dùng.</summary>
        public List<string> LeaderHandles { get; } = new List<string>();

        public bool Attached => Run != null;
    }

    public sealed class TagAttachResult
    {
        public List<TagAttachment> Attachments { get; } = new List<TagAttachment>();

        public List<Issue> Issues { get; } = new List<Issue>();
    }

    /// <summary>
    /// Gắn nhãn với đoạn chạy, S2-08 và S2-09. Ba mức ưu tiên: đường dẫn chạm đoạn chạy, nhãn nằm song song sát đoạn chạy,
    /// đoạn chạy gần nhất trong bán kính. Điểm tin cậy theo mục 9.4. Hai ứng viên có điểm gần nhau thì tạo vấn đề R-TAG-02
    /// thay vì tự chọn.
    /// </summary>
    public sealed class TagAttacher
    {
        public const double LeaderConfidence = 1.00;
        public const double AdjacentConfidence = 0.90;
        public const double NearestConfidence = 0.80;

        /// <summary>Từ ngưỡng này trở lên thì tự quyết.</summary>
        public const double AutoThreshold = 0.75;

        /// <summary>Dưới ngưỡng này thì không gắn, chỉ tạo vấn đề.</summary>
        public const double RejectThreshold = 0.40;

        private readonly ConventionProfile _profile;
        private readonly ProfileTolerances _tol;
        private Dictionary<string, int> _component = new Dictionary<string, int>(StringComparer.Ordinal);

        public TagAttacher(ConventionProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _tol = profile.Tolerances;
        }

        public TagAttachResult Attach(IReadOnlyList<Run> runs, IEnumerable<TagReadResult> tags, IEnumerable<SourceEntity> leaders)
        {
            if (runs == null) throw new ArgumentNullException(nameof(runs));
            if (tags == null) throw new ArgumentNullException(nameof(tags));
            var result = new TagAttachResult();
            var leaderList = (leaders ?? Enumerable.Empty<SourceEntity>())
                .Where(l => l.Points != null && l.Points.Count >= 2)
                .ToList();

            var extent = runs.Count == 0
                ? new Box2(Point2.Origin, Point2.Origin)
                : runs.Select(r => Box2.FromPoints(r.PlanPoints)).Aggregate((a, b) => a.Union(b));
            var grid = new SpatialGrid<Run>(Math.Max(SpatialGrid<Run>.SuggestCellSize(extent, runs.Count), _tol.TagSearchRadius));
            foreach (var r in runs)
            {
                if (r.PlanPoints.Count > 0) grid.Insert(r, Box2.FromPoints(r.PlanPoints));
            }

            _component = ConnectedComponents(runs);
            var issueNo = 0;
            var ordered = tags.OrderBy(t => t.Entity.Handle, StringComparer.Ordinal).ToList();
            foreach (var tag in ordered)
            {
                var att = new TagAttachment(tag);
                result.Attachments.Add(att);
                if (!tag.Matched)
                {
                    AddIssue(result, ref issueNo, "R-TAG-03", tag.Entity,
                        $"Chữ '{Shorten(tag.Entity.Text)}' trên layer nhãn không khớp mẫu nào trong hồ sơ.");
                    continue;
                }

                var box = TextBox(tag.Entity);
                if (TryLeader(att, box, leaderList, grid) || TryAdjacent(att, box, tag.Entity, grid) || TryNearest(att, box, grid))
                {
                    Decide(att, result, ref issueNo);
                }
                else
                {
                    AddIssue(result, ref issueNo, "R-TAG-04", tag.Entity,
                        $"Nhãn '{tag.Tag!.Text}' không gần đoạn chạy nào trong bán kính {_tol.TagSearchRadius:0} mm.");
                }
            }

            PairElevationTexts(result);
            return result;
        }

        /// <summary>
        /// Chữ cao độ ghi rời đứng sát một nhãn tuyến, thành nhãn hai dòng, thì theo đoạn chạy của nhãn tuyến đó
        /// với cùng điểm tin cậy. Bản vẽ thật có hàng trăm cặp như vậy, và gắn độc lập theo khoảng cách thường mơ hồ.
        /// </summary>
        private void PairElevationTexts(TagAttachResult result)
        {
            var lineTags = result.Attachments.Where(a => a.Attached && a.Tag.Matched && !a.Tag.Tag!.IsElevationOnly && a.Tag.Entity.Bounds.HasValue).ToList();
            if (lineTags.Count == 0) return;
            foreach (var att in result.Attachments.Where(a => a.Tag.Matched && a.Tag.Tag!.IsElevationOnly && a.Tag.Entity.Bounds.HasValue))
            {
                if (att.Attached && att.Confidence >= AutoThreshold && att.Method == AttachMethods.Leader) continue;
                var box = att.Tag.Entity.Bounds!.Value;
                var height = att.Tag.Entity.TextHeight ?? 0;
                if (height <= 0) continue;
                var rotation = att.Tag.Entity.RotationDeg ?? 0;
                TagAttachment? best = null;
                var bestGap = double.MaxValue;
                foreach (var line in lineTags)
                {
                    var lr = line.Tag.Entity.RotationDeg ?? 0;
                    if (Math.Abs(((rotation - lr) % 180 + 180) % 180) > 1.0) continue;
                    var gap = BoxGap(box, line.Tag.Entity.Bounds!.Value);
                    if (gap <= height * 2.5 && gap < bestGap)
                    {
                        bestGap = gap;
                        best = line;
                    }
                }

                if (best == null) continue;
                att.Run = best.Run;
                att.Method = AttachMethods.Paired;
                att.Confidence = best.Confidence;
                att.Candidates.Clear();
                att.Candidates.Add(new RunCandidate(best.Run!, bestGap));
                result.Issues.RemoveAll(i => (i.RuleId == "R-TAG-02" || i.RuleId == "R-TAG-04") && i.Sources.Contains(att.Tag.Entity.Handle));
            }
        }

        private static double BoxGap(Box2 a, Box2 b)
        {
            var dx = Math.Max(0, Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
            var dy = Math.Max(0, Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Cụm đoạn chạy nối nhau qua đầu mút chung trong dung sai bắt điểm. Hai mảnh của cùng một đường ống không phải hai ứng viên khác nhau.</summary>
        private Dictionary<string, int> ConnectedComponents(IReadOnlyList<Run> runs)
        {
            var parent = new Dictionary<string, string>(StringComparer.Ordinal);
            string Find(string x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }

                return x;
            }

            void Union(string a, string b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra != rb) parent[ra] = rb;
            }

            var snap = Math.Max(_tol.Snap, 0.01);
            var byPoint = new Dictionary<(long, long), string>();
            foreach (var r in runs)
            {
                parent[r.Id] = r.Id;
                if (r.PlanPoints.Count == 0) continue;
                foreach (var p in new[] { r.PlanPoints[0], r.PlanPoints[r.PlanPoints.Count - 1] })
                {
                    var key = ((long)Math.Round(p.X / snap), (long)Math.Round(p.Y / snap));
                    if (byPoint.TryGetValue(key, out var other)) Union(r.Id, other);
                    else byPoint[key] = r.Id;
                }
            }

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in runs)
            {
                var root = Find(r.Id);
                if (!index.TryGetValue(root, out var i))
                {
                    i = index.Count;
                    index[root] = i;
                }

                result[r.Id] = i;
            }

            return result;
        }

        private bool TryLeader(TagAttachment att, Box2 box, List<SourceEntity> leaders, SpatialGrid<Run> grid)
        {
            var reach = Math.Max(att.Tag.Entity.TextHeight ?? 0, _tol.LeaderTouch);
            var candidates = new Dictionary<string, RunCandidate>();
            foreach (var leader in leaders)
            {
                var pts = leader.Points!.Select(p => p.Plan).ToList();
                var first = pts[0];
                var last = pts[pts.Count - 1];
                var dFirst = box.DistanceTo(first);
                var dLast = box.DistanceTo(last);
                Point2 tip;
                if (dFirst <= reach && dFirst <= dLast) tip = last;
                else if (dLast <= reach) tip = first;
                else continue;

                var touched = false;
                foreach (var run in grid.QueryNear(tip, _tol.LeaderTouch))
                {
                    var d = DistanceToRun(tip, run);
                    if (d > _tol.LeaderTouch) continue;
                    touched = true;
                    if (!candidates.TryGetValue(run.Id, out var existing) || d < existing.Distance)
                    {
                        candidates[run.Id] = new RunCandidate(run, d);
                    }
                }

                if (touched) att.LeaderHandles.Add(leader.Handle);
            }

            if (candidates.Count == 0) return false;
            att.Method = AttachMethods.Leader;
            att.Candidates.AddRange(candidates.Values.OrderBy(c => c.Distance).ThenBy(c => c.Run.Id, StringComparer.Ordinal));
            Score(att, LeaderConfidence);
            return true;
        }

        private bool TryAdjacent(TagAttachment att, Box2 box, SourceEntity text, SpatialGrid<Run> grid)
        {
            var height = text.TextHeight ?? 0;
            if (height <= 0) return false;
            var limit = height * _tol.TagAdjacentFactor;
            var angle = (text.RotationDeg ?? 0) * Math.PI / 180.0;
            var dir = new Vector2(Math.Cos(angle), Math.Sin(angle));
            var tol = new Tolerance(_tol.Snap, Math.Max(1.0, _tol.CollinearDeg));
            var candidates = new List<RunCandidate>();
            foreach (var run in grid.Query(box.Expand(limit)))
            {
                var best = double.MaxValue;
                for (var i = 1; i < run.PlanPoints.Count; i++)
                {
                    var seg = new Segment2(run.PlanPoints[i - 1], run.PlanPoints[i]);
                    if (seg.IsDegenerate(tol) || !seg.Direction.IsParallelTo(dir, tol)) continue;
                    // Hình chiếu của hộp chữ lên đoạn phải chồng lên đoạn, nếu không chữ chỉ nằm trên đường kéo dài của nó.
                    var tMin = double.MaxValue;
                    var tMax = double.MinValue;
                    foreach (var corner in new[] { box.Min, box.Max, new Point2(box.Min.X, box.Max.Y), new Point2(box.Max.X, box.Min.Y) })
                    {
                        var t = seg.ProjectParameter(corner);
                        tMin = Math.Min(tMin, t);
                        tMax = Math.Max(tMax, t);
                    }

                    if (tMax < 0 || tMin > 1) continue;
                    var d = GeometryOps.DistanceBoxToSegment(box, seg);
                    if (d <= limit && d < best) best = d;
                }

                if (best < double.MaxValue) candidates.Add(new RunCandidate(run, best));
            }

            if (candidates.Count == 0) return false;
            att.Method = AttachMethods.Adjacent;
            att.Candidates.AddRange(candidates.OrderBy(c => c.Distance).ThenBy(c => c.Run.Id, StringComparer.Ordinal));
            Score(att, AdjacentConfidence);
            return true;
        }

        private bool TryNearest(TagAttachment att, Box2 box, SpatialGrid<Run> grid)
        {
            var radius = _tol.TagSearchRadius;
            var candidates = new List<RunCandidate>();
            foreach (var run in grid.Query(box.Expand(radius)))
            {
                var d = DistanceToRun(box, run);
                if (d <= radius) candidates.Add(new RunCandidate(run, d));
            }

            if (candidates.Count == 0) return false;
            att.Method = AttachMethods.Nearest;
            att.Candidates.AddRange(candidates.OrderBy(c => c.Distance).ThenBy(c => c.Run.Id, StringComparer.Ordinal));
            Score(att, NearestConfidence);
            return true;
        }

        /// <summary>
        /// Điểm cơ sở nhân với (1 trừ tỷ số khoảng cách thứ nhất chia thứ hai) khi có ứng viên thứ hai, mục 9.4.
        /// Ứng viên thứ hai chỉ tính khi thuộc cụm đoạn chạy khác với ứng viên thứ nhất; hai mảnh của cùng đường ống không gây mơ hồ.
        /// </summary>
        private void Score(TagAttachment att, double baseConfidence)
        {
            var c = att.Candidates;
            if (c.Count == 1)
            {
                att.Confidence = baseConfidence;
                return;
            }

            var first = c[0];
            var firstComponent = _component.TryGetValue(first.Run.Id, out var fc) ? fc : -1;
            var second = c.Skip(1).FirstOrDefault(x => !_component.TryGetValue(x.Run.Id, out var xc) || xc != firstComponent);
            if (second == null)
            {
                att.Confidence = baseConfidence;
                return;
            }

            var d1 = first.Distance;
            var d2 = second.Distance;
            var ratio = d2 <= 0 ? 1.0 : Math.Min(1.0, d1 / d2);
            att.Confidence = Math.Round(baseConfidence * (1 - ratio), 3);
        }

        private void Decide(TagAttachment att, TagAttachResult result, ref int issueNo)
        {
            var tag = att.Tag.Tag!;
            if (att.Confidence >= RejectThreshold)
            {
                att.Run = att.Candidates[0].Run;
            }

            if (att.Confidence >= AutoThreshold) return;

            var others = string.Join(", ", att.Candidates.Take(3).Select(c => $"{c.Run.Id} ({c.Distance:0} mm)"));
            var message = att.Attached
                ? $"Nhãn '{tag.Text}' gắn vào {att.Run!.Id} với điểm tin cậy {att.Confidence:0.00}. Ứng viên: {others}."
                : $"Nhãn '{tag.Text}' có nhiều đoạn chạy ứng viên gần nhau, chưa gắn. Ứng viên: {others}.";
            var issue = AddIssue(result, ref issueNo, "R-TAG-02", att.Tag.Entity, message);
            if (issue == null) return;
            if (att.Attached) issue.Elements.Add(att.Run!.Id);
            foreach (var c in att.Candidates.Take(3))
            {
                issue.Suggestions.Add(new Suggestion
                {
                    Op = DecisionOp.AssignTag,
                    Target = new Dictionary<string, string> { ["run"] = c.Run.Id, ["source"] = att.Tag.Entity.Handle },
                    Value = JsonSerializer.SerializeToElement(tag.Text),
                    Label = $"Gắn vào {c.Run.Id}",
                });
            }
        }

        private Issue? AddIssue(TagAttachResult result, ref int issueNo, string ruleId, SourceEntity source, string message)
        {
            var severity = RuleCatalog.Resolve(_profile, ruleId);
            if (severity == null) return null;
            issueNo++;
            var issue = new Issue
            {
                Id = "issue-tag-" + issueNo.ToString("000", CultureInfo.InvariantCulture),
                RuleId = ruleId,
                Severity = severity.Value,
                Message = message,
                Position = source.Center ?? (source.Bounds.HasValue ? source.Bounds.Value.Center.At(0) : (Point3?)null),
            };
            issue.Sources.Add(source.Handle);
            result.Issues.Add(issue);
            return issue;
        }

        private static Box2 TextBox(SourceEntity text)
        {
            if (text.Bounds.HasValue) return text.Bounds.Value;
            var c = text.Center?.Plan ?? Point2.Origin;
            return new Box2(c, c);
        }

        private static double DistanceToRun(Point2 p, Run run)
        {
            var best = double.MaxValue;
            for (var i = 1; i < run.PlanPoints.Count; i++)
            {
                best = Math.Min(best, new Segment2(run.PlanPoints[i - 1], run.PlanPoints[i]).DistanceTo(p));
            }

            if (run.PlanPoints.Count == 1) best = run.PlanPoints[0].DistanceTo(p);
            return best;
        }

        private static double DistanceToRun(Box2 box, Run run)
        {
            var best = double.MaxValue;
            for (var i = 1; i < run.PlanPoints.Count; i++)
            {
                best = Math.Min(best, GeometryOps.DistanceBoxToSegment(box, new Segment2(run.PlanPoints[i - 1], run.PlanPoints[i])));
            }

            if (run.PlanPoints.Count == 1) best = box.DistanceTo(run.PlanPoints[0]);
            return best;
        }

        private static string Shorten(string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s!.Length <= 40 ? s : s.Substring(0, 37) + "...";
        }
    }
}
