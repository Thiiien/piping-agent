using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Rules;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Graph
{
    public sealed class GraphStats
    {
        public int RunsUsed { get; set; }

        public int RunsSkipped { get; set; }

        public int Segments { get; set; }

        public int Risers { get; set; }

        public int Nodes { get; set; }

        public int Elbows { get; set; }

        public int Tees { get; set; }

        public int OpenEnds { get; set; }

        public int TeeSplits { get; set; }

        public double TotalLength { get; set; }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0} đoạn chạy dùng, {1} bỏ, {2} đoạn ống gồm {3} ống đứng, {4} nút: {5} co, {6} tê, {7} đầu hở, tổng {8:0.0} m",
                RunsUsed, RunsSkipped, Segments, Risers, Nodes, Elbows, Tees, OpenEnds, TotalLength / 1000.0);
    }

    /// <summary>
    /// Dựng đồ thị kết nối 3D, S3-03 đến S3-08: nút tại đầu mút, điểm gãy, điểm giao; cạnh là đoạn ống thẳng.
    /// Hai đoạn khác cao độ cắt nhau trên mặt bằng không nối. Ống đứng suy ra khi hai đoạn chạy khác cao độ có chung điểm
    /// trên mặt bằng. Nút phân loại co, tê, đầu hở. Kích thước phụ kiện từ danh mục cắt vào đoạn ống. Mọi phần tử có nguồn gốc.
    /// Lỗi của một đoạn chạy thành một vấn đề, các đoạn khác vẫn xử lý.
    /// </summary>
    public sealed class GraphBuilder
    {
        private readonly ConventionProfile _profile;
        private readonly PipeCatalog? _catalog;
        private readonly Tolerance _tol;
        private int _issueNo;

        public GraphBuilder(ConventionProfile profile, PipeCatalog? catalog)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _catalog = catalog;
            _tol = new Tolerance(profile.Tolerances.Snap, Math.Max(profile.Tolerances.CollinearDeg, 0.5));
        }

        public GraphStats Build(PipingModel model, IEnumerable<SourceEntity>? riserSymbols = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var stats = new GraphStats();
            _issueNo = model.Issues.Count;
            model.Segments.Clear();
            model.Nodes.Clear();

            // 1. Cao độ tâm từng đoạn chạy, S3-01. Thiếu thì chặn và bỏ đoạn chạy đó khỏi đồ thị.
            var usable = new List<Run>();
            foreach (var run in model.Runs)
            {
                try
                {
                    if (run.PlanPoints.Count < (run.EndElevation.HasValue ? 1 : 2))
                    {
                        stats.RunsSkipped++;
                        continue;
                    }

                    var r = ElevationResolver.Apply(run, _catalog, _profile);
                    if (!r.Ok)
                    {
                        AddIssue(model, "R-ELV-05", $"Đoạn chạy {run.Id}: {r.Problem}.", new[] { run.Id }, run.Prov?.Sources, Center(run).At(0));
                        stats.RunsSkipped++;
                        continue;
                    }

                    usable.Add(run);
                }
                catch (System.Exception ex)
                {
                    AddIssue(model, "R-GEO-05", $"Đoạn chạy {run.Id} không xử lý được: {ex.Message}", new[] { run.Id }, run.Prov?.Sources, null);
                    stats.RunsSkipped++;
                }
            }

            stats.RunsUsed = usable.Count;

            // 2. Đa giác 3D của từng đoạn chạy, tách tại chỗ đoạn chạy khác đâm vào (tê).
            // Đoạn chạy thẳng đứng: một điểm mặt bằng, hai cao độ.
            var polylines = usable.ToDictionary(r => r.Id, r => r.EndElevation.HasValue
                ? new List<Point3> { r.PlanPoints[0].At(r.Elevation.Centerline!.Value), r.PlanPoints[0].At(r.Elevation.Centerline!.Value + (r.EndElevation.Value - r.Elevation.Raw)) }
                : r.PlanPoints.Select(p => p.At(r.Elevation.Centerline!.Value)).ToList(), StringComparer.Ordinal);
            stats.TeeSplits = SplitAtTees(usable, polylines, model);

            // 3. Nút và cạnh.
            var nodes = new Dictionary<(long, long, long), Node>();
            var nodeList = new List<Node>();
            var segments = new List<Segment>();
            foreach (var run in usable)
            {
                try
                {
                    var pts = polylines[run.Id];
                    for (var i = 1; i < pts.Count; i++)
                    {
                        var a = GetNode(nodes, nodeList, pts[i - 1], run);
                        var b = GetNode(nodes, nodeList, pts[i], run);
                        if (a == b) continue;
                        var seg = MakeSegment(segments.Count + 1, run, a, b, run.EndElevation.HasValue ? SegmentKind.Riser : SegmentKind.Horizontal, run.Prov?.Sources, false);
                        segments.Add(seg);
                        a.Segments.Add(seg.Id);
                        b.Segments.Add(seg.Id);
                    }
                }
                catch (System.Exception ex)
                {
                    AddIssue(model, "R-GEO-05", $"Đoạn chạy {run.Id} không dựng được đoạn ống: {ex.Message}", new[] { run.Id }, run.Prov?.Sources, null);
                }
            }

            // 4. Ống đứng, S3-04: hai nút trùng mặt bằng, khác cao độ.
            stats.Risers = AddRisers(model, usable, nodes, nodeList, segments, riserSymbols);

            // 5. Phân loại nút và kích thước phụ kiện, S3-05 và S3-06.
            var segById = segments.ToDictionary(s => s.Id, StringComparer.Ordinal);
            var nodeById = nodeList.ToDictionary(n => n.Id, StringComparer.Ordinal);
            foreach (var node in nodeList) ClassifyNode(model, node, segById, nodeById, stats);
            foreach (var seg in segments) CheckTrims(model, seg);

            model.Segments.AddRange(segments);
            model.Nodes.AddRange(nodeList);
            stats.Segments = segments.Count;
            stats.Nodes = nodeList.Count;
            stats.TotalLength = segments.Sum(s => s.Length);
            return stats;
        }

        /// <summary>Đầu mút của đoạn chạy này nằm trong lòng một đoạn của đoạn chạy khác cùng cao độ thì chèn đỉnh vào đó.</summary>
        private int SplitAtTees(List<Run> runs, Dictionary<string, List<Point3>> polylines, PipingModel model)
        {
            var splits = 0;
            var ends = runs.SelectMany(r => new[] { polylines[r.Id][0], polylines[r.Id][polylines[r.Id].Count - 1] }).ToList();
            foreach (var run in runs)
            {
                if (run.EndElevation.HasValue) continue;
                var pts = polylines[run.Id];
                var i = 0;
                while (i < pts.Count - 1)
                {
                    var a = pts[i];
                    var b = pts[i + 1];
                    var seg = new Segment2(a.Plan, b.Plan);
                    Point3? hit = null;
                    var bestT = double.MaxValue;
                    foreach (var e in ends)
                    {
                        if (Math.Abs(e.Z - a.Z) > _tol.Length) continue;
                        if (!seg.ContainsInterior(e.Plan, _tol)) continue;
                        var t = seg.ProjectParameter(e.Plan);
                        if (t < bestT)
                        {
                            bestT = t;
                            hit = seg.PointAt(t).At(a.Z);
                        }
                    }

                    if (hit.HasValue)
                    {
                        pts.Insert(i + 1, hit.Value);
                        splits++;
                    }

                    i++;
                }
            }

            // Hai đoạn cùng cao độ cắt nhau mà không có đầu mút tại đó: R-CON-03.
            for (var x = 0; x < runs.Count; x++)
            {
                for (var y = x + 1; y < runs.Count; y++)
                {
                    foreach (var (sa, za) in Segments3(polylines[runs[x].Id]))
                    {
                        foreach (var (sb, zb) in Segments3(polylines[runs[y].Id]))
                        {
                            if (Math.Abs(za - zb) > _tol.Length) continue;
                            var r = GeometryOps.Intersect(sa, sb, _tol);
                            if (r.Relation == SegmentRelation.Crossing)
                            {
                                AddIssue(model, "R-CON-03", $"Đoạn chạy {runs[x].Id} và {runs[y].Id} cùng cao độ cắt nhau trên mặt bằng mà không có nút.",
                                    new[] { runs[x].Id, runs[y].Id }, null, r.Point!.Value.At(za));
                            }
                        }
                    }
                }
            }

            return splits;
        }

        private static IEnumerable<(Segment2, double)> Segments3(List<Point3> pts)
        {
            for (var i = 1; i < pts.Count; i++) yield return (new Segment2(pts[i - 1].Plan, pts[i].Plan), pts[i].Z);
        }

        private Node GetNode(Dictionary<(long, long, long), Node> nodes, List<Node> list, Point3 p, Run run)
        {
            var key = Key(p);
            if (!nodes.TryGetValue(key, out var node))
            {
                node = new Node
                {
                    Id = "node-" + (list.Count + 1).ToString("000", CultureInfo.InvariantCulture),
                    Position = p,
                    Kind = NodeKind.OpenEnd,
                    Prov = new Provenance { Rule = RecognitionRules.NodeFromGraph, Sources = new List<string>() },
                };
                nodes[key] = node;
                list.Add(node);
            }

            foreach (var s in run.Prov?.Sources ?? Enumerable.Empty<string>())
            {
                if (!node.Prov!.Sources.Contains(s)) node.Prov.Sources.Add(s);
            }

            return node;
        }

        private (long, long, long) Key(Point3 p)
        {
            var s = _tol.Length;
            return ((long)Math.Round(p.X / s), (long)Math.Round(p.Y / s), (long)Math.Round(p.Z / s));
        }

        private Segment MakeSegment(int n, Run run, Node a, Node b, SegmentKind kind, IEnumerable<string>? sources, bool inferred)
        {
            var od = ElevationResolver.OuterDiameter(run.Dn, _catalog, _profile) ?? 0;
            return new Segment
            {
                Id = "seg-" + n.ToString("000", CultureInfo.InvariantCulture),
                LineId = run.LineId,
                RunId = inferred ? null : run.Id,
                NodeA = a.Id,
                NodeB = b.Id,
                Kind = kind,
                Dn = run.Dn,
                Od = od,
                Length = a.Position.DistanceTo(b.Position),
                Prov = new Provenance
                {
                    Sources = (sources ?? Enumerable.Empty<string>()).Distinct().ToList(),
                    Rule = inferred ? RecognitionRules.RiserInferred : RecognitionRules.SegmentFromRun,
                    Confidence = run.Prov?.Confidence ?? 1.0,
                    Inferred = inferred,
                },
            };
        }

        private int AddRisers(PipingModel model, List<Run> runs, Dictionary<(long, long, long), Node> nodes, List<Node> nodeList, List<Segment> segments, IEnumerable<SourceEntity>? riserSymbols)
        {
            var byPlan = new Dictionary<(long, long), List<Node>>();
            foreach (var n in nodeList)
            {
                var k = Key(n.Position);
                var pk = (k.Item1, k.Item2);
                if (!byPlan.TryGetValue(pk, out var list))
                {
                    list = new List<Node>();
                    byPlan[pk] = list;
                }

                list.Add(n);
            }

            var symbols = (riserSymbols ?? Enumerable.Empty<SourceEntity>()).Where(s => s.Center.HasValue).ToList();
            var usedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var runOfNode = new Dictionary<string, Run>(StringComparer.Ordinal);
            foreach (var run in runs)
            {
                foreach (var p in run.PlanPoints)
                {
                    var k = Key(p.At(run.Elevation.Centerline!.Value));
                    if (nodes.TryGetValue(k, out var n) && !runOfNode.ContainsKey(n.Id)) runOfNode[n.Id] = run;
                }
            }

            var risers = 0;
            foreach (var group in byPlan.Values.Where(g => g.Count >= 2))
            {
                var ordered = group.OrderBy(n => n.Position.Z).ToList();
                for (var i = 1; i < ordered.Count; i++)
                {
                    var low = ordered[i - 1];
                    var high = ordered[i];
                    if (high.Position.Z - low.Position.Z <= _tol.Length) continue;
                    // Ống đứng đã khai tường minh trong đoạn chạy thẳng đứng thì không suy thêm.
                    if (segments.Any(s => s.Kind == SegmentKind.Riser && ((s.NodeA == low.Id && s.NodeB == high.Id) || (s.NodeA == high.Id && s.NodeB == low.Id)))) continue;
                    // Chỉ nối khi ít nhất một trong hai là đầu mút của đoạn chạy, tức ống thật sự rẽ lên hoặc xuống.
                    // Hai đỉnh giữa chừng tình cờ chồng nhau trên mặt bằng ở hai cao độ thì không phải ống đứng.
                    if (low.Segments.Count != 1 && high.Segments.Count != 1) continue;
                    var run = runOfNode.TryGetValue(high.Id, out var rh) ? rh : runOfNode[low.Id];
                    var seg = MakeSegment(segments.Count + 1, run, low, high, SegmentKind.Riser, low.Prov?.Sources.Concat(high.Prov?.Sources ?? Enumerable.Empty<string>()), true);
                    segments.Add(seg);
                    low.Segments.Add(seg.Id);
                    high.Segments.Add(seg.Id);
                    risers++;

                    var plan = low.Position.Plan;
                    var symbol = symbols.FirstOrDefault(s => !usedSymbols.Contains(s.Handle) && s.Center!.Value.Plan.DistanceTo(plan) <= Math.Max(s.Radius ?? 0, _profile.Tolerances.Snap) + _profile.Tolerances.Snap);
                    if (symbol != null)
                    {
                        usedSymbols.Add(symbol.Handle);
                        seg.Prov!.Sources.Add(symbol.Handle);
                        seg.Prov.Confidence = 1.0;
                    }
                    else
                    {
                        seg.Prov!.Confidence = 0.9;
                        AddIssue(model, "R-ELV-01", $"Ống đứng {seg.Id} suy ra từ đổi cao độ tại {plan} nhưng không có ký hiệu ống đứng.", new[] { seg.Id }, null, low.Position);
                    }
                }
            }

            foreach (var s in symbols.Where(s => !usedSymbols.Contains(s.Handle)))
            {
                AddIssue(model, "R-ELV-02", $"Ký hiệu ống đứng tại {s.Center!.Value.Plan} không có đổi cao độ tương ứng.", Array.Empty<string>(), new[] { s.Handle }, s.Center);
            }

            return risers;
        }

        private void ClassifyNode(PipingModel model, Node node, Dictionary<string, Segment> segById, Dictionary<string, Node> nodeById, GraphStats stats)
        {
            var dirs = node.Segments.Select(id =>
            {
                var s = segById[id];
                var other = nodeById[s.NodeA == node.Id ? s.NodeB : s.NodeA];
                return (Segment: s, Dir: (other.Position - node.Position).Normalized());
            }).ToList();
            node.Params = node.Params ?? new Dictionary<string, double>();

            var reducingTee = false;
            switch (dirs.Count)
            {
                case 0:
                case 1:
                    node.Kind = NodeKind.OpenEnd;
                    stats.OpenEnds++;
                    break;
                case 2:
                    {
                        var angle = AngleDeg(dirs[0].Dir, dirs[1].Dir);
                        var bend = 180 - angle;
                        if (bend <= Math.Max(_profile.Tolerances.CollinearDeg, 0.5))
                        {
                            node.Kind = NodeKind.Continuation;
                            break;
                        }

                        if (bend >= 179.5)
                        {
                            // Hai đoạn ống trùng hướng từ cùng một nút: đoạn gập ngược hoặc vẽ chồng, không phải co.
                            node.Kind = NodeKind.OpenEnd;
                            AddIssue(model, "R-GEO-05", $"Nút {node.Id}: hai đoạn ống {dirs[0].Segment.Id} và {dirs[1].Segment.Id} chồng lên nhau.", new[] { node.Id }, node.Prov?.Sources, node.Position);
                            break;
                        }

                        node.Kind = NodeKind.Elbow;
                        stats.Elbows++;
                        node.Params["angle"] = Math.Round(bend, 3);
                        var allowed = _profile.Tolerances.AllowedBendAngles.Any(a => Math.Abs(a - bend) <= 0.5);
                        if (!allowed)
                        {
                            AddIssue(model, "R-GEO-01", string.Format(CultureInfo.InvariantCulture, "Nút {0}: góc đổi hướng {1:0.#} độ không thuộc tập cho phép.", node.Id, bend), new[] { node.Id }, node.Prov?.Sources, node.Position);
                        }

                        ApplyElbow(model, node, dirs[0].Segment, dirs[1].Segment, bend);
                        break;
                    }
                case 3:
                    {
                        node.Kind = NodeKind.Tee;
                        stats.Tees++;
                        // Nhánh là đoạn không thẳng hàng với hai đoạn còn lại.
                        var branchIndex = 0;
                        var bestStraight = -1.0;
                        for (var i = 0; i < 3; i++)
                        {
                            var a = dirs[(i + 1) % 3].Dir;
                            var b = dirs[(i + 2) % 3].Dir;
                            var straightness = -a.Dot(b);
                            if (straightness > bestStraight)
                            {
                                bestStraight = straightness;
                                branchIndex = i;
                            }
                        }

                        var branch = dirs[branchIndex].Segment;
                        var runs = dirs.Where((_, i) => i != branchIndex).Select(d => d.Segment).ToList();
                        node.Params["branchDn"] = branch.Dn;
                        // Tê thu: hai đoạn chạy cùng DN, nhánh nhỏ hơn. Không phải lỗi DN khác nhau.
                        reducingTee = runs[0].Dn == runs[1].Dn && branch.Dn > 0 && branch.Dn < runs[0].Dn;
                        ApplyTee(model, node, runs, branch);
                        break;
                    }
                default:
                    node.Kind = NodeKind.Tee;
                    stats.Tees++;
                    AddIssue(model, "R-CON-04", $"Nút {node.Id} có {dirs.Count} đoạn ống nối vào, chưa hỗ trợ nút nhiều hơn ba nhánh.", new[] { node.Id }, node.Prov?.Sources, node.Position);
                    break;
            }

            if (dirs.Count >= 2)
            {
                var dns = dirs.Select(d => d.Segment.Dn).Distinct().ToList();
                if (dns.Count > 1 && node.Kind != NodeKind.Reducer && !reducingTee && !(node.Params?.ContainsKey("olet") ?? false))
                {
                    AddIssue(model, "R-CON-04", $"Nút {node.Id}: các đoạn ống có DN khác nhau ({string.Join(", ", dns)}) mà nút không phải côn thu hoặc tê thu.", new[] { node.Id }, node.Prov?.Sources, node.Position);
                }
            }
        }

        private void ApplyElbow(PipingModel model, Node node, Segment s1, Segment s2, double bend)
        {
            var dn = Math.Max(s1.Dn, s2.Dn);
            if (dn <= 0 || _catalog == null || _profile.FittingStandard == null) return;
            var is45 = Math.Abs(bend - 45) <= 0.5;
            var type = is45 ? FittingTypes.Elbow45LR : FittingTypes.Elbow90LR;
            var dim = _catalog.FittingDim(_profile.FittingStandard, type, dn, is45 ? "B" : "A");
            if (!dim.Found)
            {
                AddIssue(model, "R-CAT-01", $"Nút {node.Id}: {dim.Reason}", new[] { node.Id }, node.Prov?.Sources, node.Position);
                return;
            }

            node.CatalogRef = PipeCatalog.Ref(_profile.FittingStandard, type, dn);
            node.Params!["radius"] = is45 ? Math.Round(dim.Value / Math.Tan(22.5 * Math.PI / 180), 3) : dim.Value;
            node.Params["centerToEnd"] = dim.Value;
            Trim(s1, node, dim.Value);
            Trim(s2, node, dim.Value);
        }

        private void ApplyTee(PipingModel model, Node node, List<Segment> runs, Segment branch)
        {
            var dn = runs.Max(s => s.Dn);
            // Nhánh nối bằng olet, half coupling hay hàn trực tiếp: có cấu kiện loại olet ngay tại nút thì không đặt tê, ống chính không bị cắt.
            var oletReach = Math.Max(60, Math.Max(branch.Od, 0) + 40);
            var olet = model.Components.FirstOrDefault(c => c.Type == "olet" && c.Position.DistanceTo(node.Position) <= oletReach);
            if (olet != null)
            {
                node.Params!["olet"] = 1;
                node.Prov ??= new Provenance();
                node.Prov.Sources.AddRange(olet.Prov?.Sources ?? new List<string>());
                return;
            }

            if (dn <= 0 || _catalog == null || _profile.FittingStandard == null) return;
            var tee = _catalog.TeeDims(_profile.FittingStandard, dn);
            if (!tee.Found)
            {
                AddIssue(model, "R-CAT-01", $"Nút {node.Id}: {tee.Reason}", new[] { node.Id }, node.Prov?.Sources, node.Position);
                return;
            }

            node.CatalogRef = PipeCatalog.Ref(_profile.FittingStandard, FittingTypes.Tee, dn, branch.Dn != dn ? branch.Dn : (int?)null);
            node.Params!["centerToRun"] = tee.Value.C;
            node.Params["centerToBranch"] = tee.Value.M;
            foreach (var s in runs) Trim(s, node, tee.Value.C);
            Trim(branch, node, tee.Value.M);
        }

        private static void Trim(Segment s, Node node, double amount)
        {
            if (s.NodeA == node.Id) s.TrimA = amount; else s.TrimB = amount;
        }

        private void CheckTrims(PipingModel model, Segment seg)
        {
            if (seg.Length - seg.TrimA - seg.TrimB < -_tol.Length)
            {
                AddIssue(model, "R-GEO-02", string.Format(CultureInfo.InvariantCulture,
                    "Đoạn ống {0} dài {1:0} mm, ngắn hơn phần phụ kiện hai đầu chiếm chỗ {2:0} mm.", seg.Id, seg.Length, seg.TrimA + seg.TrimB),
                    new[] { seg.Id }, seg.Prov?.Sources, null);
            }
        }

        private static double AngleDeg(Vector3 a, Vector3 b)
        {
            var d = Math.Max(-1, Math.Min(1, a.Dot(b)));
            return Math.Acos(d) * 180 / Math.PI;
        }

        private static Point2 Center(Run run) => Box2.FromPoints(run.PlanPoints).Center;

        private void AddIssue(PipingModel model, string ruleId, string message, IEnumerable<string> elements, IEnumerable<string>? sources, Point3? position)
        {
            var severity = RuleCatalog.Resolve(_profile, ruleId);
            if (severity == null) return;
            _issueNo++;
            var issue = new Issue
            {
                Id = "issue-" + _issueNo.ToString("000", CultureInfo.InvariantCulture),
                RuleId = ruleId,
                Severity = severity.Value,
                Message = message,
                Position = position,
            };
            issue.Elements.AddRange(elements);
            if (sources != null) issue.Sources.AddRange(sources.Distinct());
            model.Issues.Add(issue);
        }
    }
}
