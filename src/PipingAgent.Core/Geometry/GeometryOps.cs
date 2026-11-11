using System;

namespace PipingAgent.Core.Geometry
{
    /// <summary>Quan hệ giữa hai đoạn thẳng trên mặt bằng.</summary>
    public enum SegmentRelation
    {
        /// <summary>Không chạm nhau.</summary>
        Disjoint,

        /// <summary>Cắt nhau tại một điểm nằm trong lòng cả hai đoạn.</summary>
        Crossing,

        /// <summary>Chạm nhau tại một điểm, điểm đó là đầu mút của ít nhất một đoạn. Gồm cả hình chữ T và chữ L.</summary>
        Touching,

        /// <summary>Song song, không cùng đường thẳng.</summary>
        Parallel,

        /// <summary>Cùng đường thẳng và có phần chung dài hơn dung sai.</summary>
        Overlapping,

        /// <summary>Cùng đường thẳng, chỉ chạm nhau ở một đầu mút.</summary>
        CollinearTouching,

        /// <summary>Cùng đường thẳng nhưng rời nhau.</summary>
        CollinearDisjoint,
    }

    /// <summary>Kết quả xét giao hai đoạn.</summary>
    public readonly struct SegmentIntersection
    {
        public SegmentIntersection(SegmentRelation relation, Point2? point, double tFirst, double tSecond)
        {
            Relation = relation;
            Point = point;
            TFirst = tFirst;
            TSecond = tSecond;
        }

        public SegmentRelation Relation { get; }

        /// <summary>Điểm chung, có khi quan hệ là Crossing, Touching hoặc CollinearTouching.</summary>
        public Point2? Point { get; }

        /// <summary>Tham số trên đoạn thứ nhất tại điểm chung, NaN nếu không có điểm.</summary>
        public double TFirst { get; }

        /// <summary>Tham số trên đoạn thứ hai tại điểm chung, NaN nếu không có điểm.</summary>
        public double TSecond { get; }

        public bool HasPoint => Point.HasValue;

        internal static SegmentIntersection None(SegmentRelation relation) =>
            new SegmentIntersection(relation, null, double.NaN, double.NaN);
    }

    /// <summary>Các phép hình học cơ bản mà bộ nhận diện dùng. Thuần hàm, không giữ trạng thái.</summary>
    public static class GeometryOps
    {
        /// <summary>Khoảng cách từ điểm tới đường thẳng vô hạn qua hai điểm. Hai điểm trùng nhau thì trả khoảng cách tới điểm.</summary>
        public static double DistancePointToLine(Point2 p, Point2 lineA, Point2 lineB)
        {
            var d = lineB - lineA;
            var l = d.Length;
            if (l == 0) return p.DistanceTo(lineA);
            return Math.Abs(d.Cross(p - lineA)) / l;
        }

        /// <summary>Khoảng cách từ điểm tới đoạn thẳng.</summary>
        public static double DistancePointToSegment(Point2 p, Point2 a, Point2 b) => new Segment2(a, b).DistanceTo(p);

        /// <summary>Khoảng cách ngắn nhất giữa hai đoạn thẳng. Bằng 0 nếu cắt nhau.</summary>
        public static double DistanceSegmentToSegment(Segment2 s, Segment2 t)
        {
            var x = Intersect(s, t, Tolerance.Default);
            if (x.Relation == SegmentRelation.Crossing || x.Relation == SegmentRelation.Touching
                || x.Relation == SegmentRelation.Overlapping || x.Relation == SegmentRelation.CollinearTouching)
            {
                return 0;
            }

            return Math.Min(
                Math.Min(s.DistanceTo(t.A), s.DistanceTo(t.B)),
                Math.Min(t.DistanceTo(s.A), t.DistanceTo(s.B)));
        }

        /// <summary>Khoảng cách ngắn nhất từ hộp tới đoạn thẳng. Bằng 0 nếu đoạn chạm hoặc nằm trong hộp.</summary>
        public static double DistanceBoxToSegment(Box2 box, Segment2 s)
        {
            if (box.Contains(s.A) || box.Contains(s.B)) return 0;
            var c0 = box.Min;
            var c1 = new Point2(box.Max.X, box.Min.Y);
            var c2 = box.Max;
            var c3 = new Point2(box.Min.X, box.Max.Y);
            var d = DistanceSegmentToSegment(s, new Segment2(c0, c1));
            d = Math.Min(d, DistanceSegmentToSegment(s, new Segment2(c1, c2)));
            d = Math.Min(d, DistanceSegmentToSegment(s, new Segment2(c2, c3)));
            d = Math.Min(d, DistanceSegmentToSegment(s, new Segment2(c3, c0)));
            return d;
        }

        /// <summary>Ba điểm thẳng hàng trong dung sai. Hai điểm trùng nhau luôn thẳng hàng với điểm thứ ba.</summary>
        public static bool AreCollinear(Point2 a, Point2 b, Point2 c, Tolerance tolerance)
        {
            // Lấy cặp xa nhau nhất làm đường thẳng để tránh sai số khi hai điểm sát nhau.
            var ab = a.DistanceTo(b);
            var bc = b.DistanceTo(c);
            var ac = a.DistanceTo(c);
            if (ab >= bc && ab >= ac) return DistancePointToLine(c, a, b) <= tolerance.Length;
            if (bc >= ab && bc >= ac) return DistancePointToLine(a, b, c) <= tolerance.Length;
            return DistancePointToLine(b, a, c) <= tolerance.Length;
        }

        /// <summary>
        /// Xét quan hệ và điểm chung của hai đoạn trong dung sai.
        /// Mọi ca biên được phân loại rõ: song song, trùng đường, chạm đầu mút, chữ T.
        /// </summary>
        public static SegmentIntersection Intersect(Segment2 first, Segment2 second, Tolerance tolerance)
        {
            var eps = tolerance.Length;
            var d1 = first.Direction;
            var d2 = second.Direction;
            var l1 = d1.Length;
            var l2 = d2.Length;

            if (l1 <= eps || l2 <= eps)
            {
                return IntersectDegenerate(first, second, tolerance);
            }

            var denominator = d1.Cross(d2);
            var sinAngle = Math.Abs(denominator) / (l1 * l2);

            if (sinAngle <= Math.Sin(tolerance.AngleRad))
            {
                return IntersectParallel(first, second, tolerance);
            }

            var w = second.A - first.A;
            var t = w.Cross(d2) / denominator;
            var u = w.Cross(d1) / denominator;

            // Cho phép vượt khỏi [0, 1] một lượng tương đương dung sai độ dài.
            var tEps = eps / l1;
            var uEps = eps / l2;
            if (t < -tEps || t > 1 + tEps || u < -uEps || u > 1 + uEps)
            {
                return SegmentIntersection.None(SegmentRelation.Disjoint);
            }

            var tc = Clamp01(t);
            var uc = Clamp01(u);
            var point = first.PointAt(tc);
            var atEndpoint = tc <= tEps || tc >= 1 - tEps || uc <= uEps || uc >= 1 - uEps;

            return new SegmentIntersection(atEndpoint ? SegmentRelation.Touching : SegmentRelation.Crossing, point, tc, uc);
        }

        private static SegmentIntersection IntersectDegenerate(Segment2 first, Segment2 second, Tolerance tolerance)
        {
            // Một trong hai đoạn thực chất là điểm.
            var firstIsPoint = first.Length <= tolerance.Length;
            var point = firstIsPoint ? first.A : second.A;
            var other = firstIsPoint ? second : first;
            if (other.Contains(point, tolerance))
            {
                var tOther = Clamp01(other.ProjectParameter(point));
                return firstIsPoint
                    ? new SegmentIntersection(SegmentRelation.Touching, point, 0, tOther)
                    : new SegmentIntersection(SegmentRelation.Touching, point, tOther, 0);
            }

            return SegmentIntersection.None(SegmentRelation.Disjoint);
        }

        private static SegmentIntersection IntersectParallel(Segment2 first, Segment2 second, Tolerance tolerance)
        {
            var eps = tolerance.Length;
            if (DistancePointToLine(second.A, first.A, first.B) > eps)
            {
                return SegmentIntersection.None(SegmentRelation.Parallel);
            }

            // Cùng đường thẳng. Chiếu hai đầu của đoạn hai lên tham số của đoạn một.
            var l1 = first.Length;
            var tA = first.ProjectParameter(second.A);
            var tB = first.ProjectParameter(second.B);
            var tMin = Math.Min(tA, tB);
            var tMax = Math.Max(tA, tB);
            var tEps = eps / l1;

            var overlapStart = Math.Max(0, tMin);
            var overlapEnd = Math.Min(1, tMax);
            var overlapLength = (overlapEnd - overlapStart) * l1;

            if (overlapLength > eps)
            {
                return SegmentIntersection.None(SegmentRelation.Overlapping);
            }

            if (tMax < -tEps || tMin > 1 + tEps)
            {
                return SegmentIntersection.None(SegmentRelation.CollinearDisjoint);
            }

            // Chạm nhau ở một đầu mút.
            var t = Clamp01(tMax < 0.5 ? tMax : tMin);
            var point = first.PointAt(t);
            var u = Clamp01(second.ProjectParameter(point));
            return new SegmentIntersection(SegmentRelation.CollinearTouching, point, t, u);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
