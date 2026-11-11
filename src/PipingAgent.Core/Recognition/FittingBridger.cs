using System;
using System.Collections.Generic;
using System.Linq;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Recognition
{
    public sealed class BridgeStats
    {
        public int FittingsConsidered { get; set; }

        public int Bridged { get; set; }

        public int EndpointsMoved { get; set; }

        public override string ToString() => $"xét {FittingsConsidered} block, nối {Bridged} chỗ, dời {EndpointsMoved} đầu mút";
    }

    /// <summary>
    /// Nối đoạn chạy bị ngắt tại block phụ kiện, phát hiện ở mốc M1 trên bản vẽ thật: nét ống dừng tại mép ký hiệu co, tê,
    /// mặt bích nên hai đoạn chạy không chạm nhau. Với mỗi block cấu kiện, tìm các đầu mút đoạn chạy trong bán kính
    /// <c>fittingReach</c>, tính điểm nút chung và kéo các đầu mút tới đó. Sau bước này các đoạn chạy có chung đỉnh
    /// và đồ thị kết nối dựng được. Đầu mút đã dời được ghi nguồn gốc là handle của block.
    /// </summary>
    public sealed class FittingBridger
    {
        private readonly double _reach;
        private readonly double _snap;

        public FittingBridger(double reach, double snap)
        {
            _reach = reach;
            _snap = snap;
        }

        public BridgeStats Bridge(IReadOnlyList<Run> runs, IEnumerable<SourceEntity> fittings)
        {
            var stats = new BridgeStats();
            if (_reach <= 0 || runs == null || runs.Count == 0) return stats;

            var ends = new List<RunEnd>();
            foreach (var run in runs)
            {
                if (run.PlanPoints.Count < 2) continue;
                ends.Add(new RunEnd(run, true));
                ends.Add(new RunEnd(run, false));
            }

            var extent = Box2.FromPoints(ends.Select(e => e.Point));
            var grid = new SpatialGrid<RunEnd>(Math.Max(_reach * 2, SpatialGrid<RunEnd>.SuggestCellSize(extent, ends.Count)));
            foreach (var e in ends) grid.Insert(e, e.Point);

            var used = new HashSet<RunEnd>();
            foreach (var fitting in fittings.Where(f => f.Kind == SourceKind.Insert && f.Center.HasValue).OrderBy(f => f.Handle, StringComparer.Ordinal))
            {
                stats.FittingsConsidered++;
                var center = fitting.Center!.Value.Plan;
                var reach = _reach;
                if (fitting.Bounds.HasValue)
                {
                    var b = fitting.Bounds.Value;
                    reach = Math.Max(reach, Math.Max(b.Width, b.Height) / 2 + _snap);
                }

                var near = grid.QueryNear(center, reach)
                    .Where(e => !used.Contains(e) && e.Point.DistanceTo(center) <= reach)
                    .GroupBy(e => e.Run)
                    .Select(g => g.OrderBy(e => e.Point.DistanceTo(center)).First())
                    .OrderBy(e => e.Point.DistanceTo(center))
                    .Take(3)
                    .ToList();
                if (near.Count < 2) continue;

                var junction = Junction(near, center);
                if (!junction.HasValue) continue;
                var moved = 0;
                foreach (var e in near)
                {
                    if (e.Point.DistanceTo(junction.Value) <= _snap) continue;
                    // Không kéo lùi qua đỉnh kế tiếp: đầu mút chỉ được dời về phía trước hoặc sang ngang trong tầm với.
                    if (e.Point.DistanceTo(junction.Value) > reach * 2) continue;
                    e.Move(junction.Value);
                    moved++;
                }

                foreach (var e in near)
                {
                    used.Add(e);
                    e.Run.Prov?.Sources.Add(fitting.Handle);
                }

                stats.Bridged++;
                stats.EndpointsMoved += moved;
            }

            return stats;
        }

        /// <summary>
        /// Điểm nút chung: giao của hai đường thẳng chứa đoạn cuối của hai đầu mút gần nhất nếu chúng không song song,
        /// ngược lại là hình chiếu của tâm block lên đường đó.
        /// </summary>
        private Point2? Junction(List<RunEnd> near, Point2 center)
        {
            var a = near[0];
            var b = near[1];
            var da = a.Direction;
            var db = b.Direction;
            var tol = new Tolerance(_snap, 1.0);
            if (da.LengthSquared == 0 || db.LengthSquared == 0) return center;
            if (da.IsParallelTo(db, tol))
            {
                var line = new Segment2(a.Point, a.Point + da);
                var t = line.ProjectParameter(center);
                return line.PointAt(t);
            }

            var cross = da.Cross(db);
            if (Math.Abs(cross) < 1e-9) return center;
            var t1 = (b.Point - a.Point).Cross(db) / cross;
            var p = a.Point + da * t1;
            return p.DistanceTo(center) <= Math.Max(_reach, 1) * 3 ? p : (Point2?)null;
        }

        private sealed class RunEnd
        {
            public RunEnd(Run run, bool isStart)
            {
                Run = run;
                IsStart = isStart;
            }

            public Run Run { get; }

            public bool IsStart { get; }

            public Point2 Point => IsStart ? Run.PlanPoints[0] : Run.PlanPoints[Run.PlanPoints.Count - 1];

            /// <summary>Hướng từ trong ra đầu mút.</summary>
            public Vector2 Direction
            {
                get
                {
                    var pts = Run.PlanPoints;
                    var v = IsStart ? pts[0] - pts[1] : pts[pts.Count - 1] - pts[pts.Count - 2];
                    return v.LengthSquared > 0 ? v.Normalized() : Vector2.Zero;
                }
            }

            public void Move(Point2 to)
            {
                if (IsStart) Run.PlanPoints[0] = to; else Run.PlanPoints[Run.PlanPoints.Count - 1] = to;
            }
        }
    }
}
