using System;
using System.Collections.Generic;
using System.Linq;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Recognition
{
    /// <summary>Một chuỗi đường tâm trên mặt bằng, đầu vào và đầu ra của bước làm sạch.</summary>
    public sealed class Chain
    {
        public Chain(IEnumerable<Point2> points, IEnumerable<string> sources, bool isPolyline)
        {
            Points = points.ToList();
            Sources = sources.ToList();
            IsPolyline = isPolyline;
        }

        public List<Point2> Points { get; }

        /// <summary>Handle của các đối tượng nguồn đã góp vào chuỗi, kể cả đối tượng trùng bị loại.</summary>
        public List<string> Sources { get; }

        /// <summary>Đúng nếu bắt nguồn từ một polyline. Polyline là ranh giới đoạn chạy, không nối với chuỗi khác.</summary>
        public bool IsPolyline { get; }

        public bool Closed { get; set; }

        public int SegmentCount => Math.Max(0, Points.Count - 1);

        public double Length
        {
            get
            {
                double l = 0;
                for (var i = 1; i < Points.Count; i++) l += Points[i - 1].DistanceTo(Points[i]);
                return l;
            }
        }

        public IEnumerable<Segment2> Segments()
        {
            for (var i = 1; i < Points.Count; i++) yield return new Segment2(Points[i - 1], Points[i]);
        }

        public static Chain FromEntity(SourceEntity e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var pts = (e.Points ?? new List<Point3>()).Select(p => p.Plan).ToList();
            var chain = new Chain(pts, new[] { e.Handle }, e.Kind == SourceKind.Polyline) { Closed = e.Closed == true };
            if (chain.Closed && pts.Count > 2 && !pts[0].Equals(pts[pts.Count - 1])) chain.Points.Add(pts[0]);
            return chain;
        }
    }

    public sealed class CleanupStats
    {
        public int InputChains { get; set; }

        public int DroppedDegenerate { get; set; }

        public int SnappedVertices { get; set; }

        public int RemovedDuplicates { get; set; }

        public int MergedCollinear { get; set; }

        public int JoinedChains { get; set; }

        public int OutputChains { get; set; }

        public override string ToString() =>
            $"vào {InputChains}, ra {OutputChains}, loại nét suy biến {DroppedDegenerate}, bắt điểm {SnappedVertices}, bỏ trùng {RemovedDuplicates}, gộp thẳng hàng {MergedCollinear}, nối chuỗi {JoinedChains}";
    }

    public sealed class CleanupResult
    {
        public List<Chain> Chains { get; } = new List<Chain>();

        public CleanupStats Stats { get; } = new CleanupStats();
    }

    /// <summary>
    /// Làm sạch đường tâm ống, S2-06: loại nét suy biến, bắt đầu mút gần nhau trong dung sai, bỏ đoạn trùng,
    /// gộp đoạn nối tiếp thẳng hàng, nối các nét rời thành chuỗi. Thuật toán thuần, không giữ trạng thái.
    /// </summary>
    public sealed class CenterlineCleaner
    {
        public CenterlineCleaner(double snap, double collinearDeg, double? minSegmentLength = null)
        {
            if (!(snap > 0)) throw new ArgumentOutOfRangeException(nameof(snap));
            Snap = snap;
            CollinearDeg = collinearDeg;
            MinSegmentLength = minSegmentLength ?? snap;
        }

        public double Snap { get; }

        public double CollinearDeg { get; }

        public double MinSegmentLength { get; }

        public CleanupResult Clean(IEnumerable<Chain> input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var result = new CleanupResult();
            var stats = result.Stats;
            var chains = input.Select(c => new Chain(c.Points, c.Sources, c.IsPolyline) { Closed = c.Closed }).ToList();
            stats.InputChains = chains.Count;

            SnapVertices(chains, stats);
            chains = DropDegenerate(chains, stats);
            chains = RemoveDuplicates(chains, stats);
            foreach (var c in chains) MergeCollinear(c, stats);
            chains = DropDegenerate(chains, stats);
            chains = JoinLooseSegments(chains, stats);
            foreach (var c in chains) MergeCollinear(c, stats);

            result.Chains.AddRange(chains);
            stats.OutputChains = chains.Count;
            return result;
        }

        /// <summary>Gom mọi đỉnh cách nhau không quá dung sai về một điểm đại diện, điểm gặp đầu tiên.</summary>
        private void SnapVertices(List<Chain> chains, CleanupStats stats)
        {
            var all = chains.SelectMany(c => c.Points).ToList();
            if (all.Count == 0) return;
            var grid = new SpatialGrid<Point2>(Math.Max(Snap * 4, 1));
            var representatives = new List<Point2>();
            foreach (var chain in chains)
            {
                for (var i = 0; i < chain.Points.Count; i++)
                {
                    var p = chain.Points[i];
                    var near = grid.QueryNear(p, Snap);
                    Point2? rep = null;
                    var best = double.MaxValue;
                    foreach (var q in near)
                    {
                        var d = q.DistanceTo(p);
                        if (d <= Snap && d < best)
                        {
                            best = d;
                            rep = q;
                        }
                    }

                    if (rep.HasValue)
                    {
                        if (!rep.Value.Equals(p))
                        {
                            chain.Points[i] = rep.Value;
                            stats.SnappedVertices++;
                        }
                    }
                    else
                    {
                        grid.Insert(p, p);
                        representatives.Add(p);
                    }
                }
            }
        }

        private List<Chain> DropDegenerate(List<Chain> chains, CleanupStats stats)
        {
            var output = new List<Chain>();
            foreach (var chain in chains)
            {
                var pts = new List<Point2>();
                foreach (var p in chain.Points)
                {
                    if (pts.Count > 0 && pts[pts.Count - 1].DistanceTo(p) <= MinSegmentLength)
                    {
                        stats.DroppedDegenerate++;
                        continue;
                    }

                    pts.Add(p);
                }

                if (pts.Count < 2)
                {
                    if (chain.Points.Count >= 2) stats.DroppedDegenerate++;
                    continue;
                }

                var c = new Chain(pts, chain.Sources, chain.IsPolyline) { Closed = chain.Closed && pts.Count > 2 };
                output.Add(c);
            }

            return output;
        }

        /// <summary>Bỏ chuỗi mà mọi đoạn của nó đều trùng với đoạn của chuỗi khác đã giữ. Handle của chuỗi bị bỏ gộp vào chuỗi giữ.</summary>
        private List<Chain> RemoveDuplicates(List<Chain> chains, CleanupStats stats)
        {
            var seen = new Dictionary<(Point2, Point2), Chain>();
            var output = new List<Chain>();
            foreach (var chain in chains.OrderByDescending(c => c.SegmentCount).ThenByDescending(c => c.IsPolyline))
            {
                var owners = new List<Chain>();
                var allDuplicate = true;
                foreach (var s in chain.Segments())
                {
                    if (seen.TryGetValue(Key(s), out var owner)) owners.Add(owner);
                    else allDuplicate = false;
                }

                if (allDuplicate && chain.SegmentCount > 0)
                {
                    stats.RemovedDuplicates++;
                    foreach (var owner in owners.Distinct())
                    {
                        foreach (var h in chain.Sources)
                        {
                            if (!owner.Sources.Contains(h)) owner.Sources.Add(h);
                        }
                    }

                    continue;
                }

                foreach (var s in chain.Segments())
                {
                    var key = Key(s);
                    if (!seen.ContainsKey(key)) seen[key] = chain;
                }

                output.Add(chain);
            }

            // Giữ thứ tự đầu vào để kết quả ổn định.
            var order = chains.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
            return output.OrderBy(c => order[c]).ToList();
        }

        private static (Point2, Point2) Key(Segment2 s)
        {
            var a = s.A;
            var b = s.B;
            if (a.X > b.X || (a.X == b.X && a.Y > b.Y)) return (b, a);
            return (a, b);
        }

        /// <summary>Bỏ đỉnh giữa hai đoạn nối tiếp cùng hướng trong dung sai góc.</summary>
        private void MergeCollinear(Chain chain, CleanupStats stats)
        {
            if (chain.Points.Count < 3) return;
            var limit = CollinearDeg * Math.PI / 180.0;
            var pts = chain.Points;
            var i = 1;
            while (i < pts.Count - 1)
            {
                var d1 = (pts[i] - pts[i - 1]);
                var d2 = (pts[i + 1] - pts[i]);
                if (d1.LengthSquared > 0 && d2.LengthSquared > 0)
                {
                    var cos = d1.Dot(d2) / (d1.Length * d2.Length);
                    cos = Math.Max(-1, Math.Min(1, cos));
                    if (Math.Acos(cos) <= limit)
                    {
                        pts.RemoveAt(i);
                        stats.MergedCollinear++;
                        continue;
                    }
                }

                i++;
            }
        }

        /// <summary>
        /// Nối các nét rời (không phải polyline) tại đỉnh có đúng hai đầu nét gặp nhau và không có đỉnh polyline nào ở đó.
        /// Đỉnh có ba đầu trở lên là chỗ rẽ nhánh, giữ làm ranh giới.
        /// </summary>
        private List<Chain> JoinLooseSegments(List<Chain> chains, CleanupStats stats)
        {
            var polylineVertices = new HashSet<Point2>(chains.Where(c => c.IsPolyline).SelectMany(c => c.Points));
            var loose = chains.Where(c => !c.IsPolyline && !c.Closed).ToList();
            if (loose.Count == 0) return chains;

            var degree = new Dictionary<Point2, int>();
            foreach (var c in loose)
            {
                Bump(degree, c.Points[0]);
                Bump(degree, c.Points[c.Points.Count - 1]);
            }

            bool Joinable(Point2 p) => degree.TryGetValue(p, out var d) && d == 2 && !polylineVertices.Contains(p);

            var endIndex = new Dictionary<Point2, List<Chain>>();
            foreach (var c in loose)
            {
                AddEnd(endIndex, c.Points[0], c);
                AddEnd(endIndex, c.Points[c.Points.Count - 1], c);
            }

            var consumed = new HashSet<Chain>();
            var joined = new List<Chain>();
            foreach (var start in loose)
            {
                if (consumed.Contains(start)) continue;
                consumed.Add(start);
                var pts = new List<Point2>(start.Points);
                var sources = new List<string>(start.Sources);
                var count = 0;

                // Kéo dài về phía cuối rồi phía đầu.
                for (var side = 0; side < 2; side++)
                {
                    while (true)
                    {
                        var end = pts[pts.Count - 1];
                        if (!Joinable(end)) break;
                        var next = endIndex[end].FirstOrDefault(c => !consumed.Contains(c));
                        if (next == null) break;
                        consumed.Add(next);
                        var np = next.Points[0].Equals(end) ? next.Points : Enumerable.Reverse(next.Points).ToList();
                        pts.AddRange(np.Skip(1));
                        sources.AddRange(next.Sources);
                        count++;
                        if (pts[0].Equals(pts[pts.Count - 1])) break;
                    }

                    pts.Reverse();
                }

                stats.JoinedChains += count;
                var chain = new Chain(pts, sources, false) { Closed = pts.Count > 3 && pts[0].Equals(pts[pts.Count - 1]) };
                joined.Add(chain);
            }

            var output = chains.Where(c => c.IsPolyline || c.Closed).ToList();
            output.AddRange(joined);
            return output;
        }

        private static void Bump(Dictionary<Point2, int> d, Point2 p) => d[p] = d.TryGetValue(p, out var n) ? n + 1 : 1;

        private static void AddEnd(Dictionary<Point2, List<Chain>> d, Point2 p, Chain c)
        {
            if (!d.TryGetValue(p, out var list))
            {
                list = new List<Chain>();
                d[p] = list;
            }

            list.Add(c);
        }
    }
}
