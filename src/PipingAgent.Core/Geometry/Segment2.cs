using System;

namespace PipingAgent.Core.Geometry
{
    /// <summary>Đoạn thẳng trên mặt bằng giữa hai điểm A và B.</summary>
    public readonly struct Segment2 : IEquatable<Segment2>
    {
        public Segment2(Point2 a, Point2 b)
        {
            A = a;
            B = b;
        }

        public Point2 A { get; }
        public Point2 B { get; }

        public Vector2 Direction => B - A;
        public double Length => Direction.Length;
        public Point2 MidPoint => new Point2((A.X + B.X) / 2, (A.Y + B.Y) / 2);
        public Box2 Bounds => Box2.FromPoints(A, B);

        public bool IsDegenerate(Tolerance tolerance) => Length <= tolerance.Length;

        /// <summary>Điểm trên đoạn tại tham số t trong [0, 1].</summary>
        public Point2 PointAt(double t) => A + Direction * t;

        /// <summary>
        /// Tham số của hình chiếu điểm lên đường thẳng chứa đoạn. 0 tại A, 1 tại B, ngoài [0, 1] khi hình chiếu nằm ngoài đoạn.
        /// Với đoạn suy biến trả về 0.
        /// </summary>
        public double ProjectParameter(Point2 p)
        {
            var d = Direction;
            var l2 = d.LengthSquared;
            if (l2 == 0) return 0;
            return (p - A).Dot(d) / l2;
        }

        /// <summary>Điểm gần nhất trên đoạn tới điểm p.</summary>
        public Point2 ClosestPoint(Point2 p)
        {
            var t = ProjectParameter(p);
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return PointAt(t);
        }

        /// <summary>Khoảng cách từ điểm tới đoạn, tính tới đầu mút nếu hình chiếu nằm ngoài đoạn.</summary>
        public double DistanceTo(Point2 p) => ClosestPoint(p).DistanceTo(p);

        /// <summary>Điểm nằm trên đoạn, kể cả hai đầu mút, trong dung sai.</summary>
        public bool Contains(Point2 p, Tolerance tolerance) => DistanceTo(p) <= tolerance.Length;

        /// <summary>Điểm nằm trong lòng đoạn, không tính hai đầu mút.</summary>
        public bool ContainsInterior(Point2 p, Tolerance tolerance) =>
            Contains(p, tolerance) && !A.IsSame(p, tolerance) && !B.IsSame(p, tolerance);

        public bool TouchesEndpoint(Point2 p, Tolerance tolerance) => A.IsSame(p, tolerance) || B.IsSame(p, tolerance);

        public Segment2 Reversed() => new Segment2(B, A);

        public bool IsParallelTo(Segment2 other, Tolerance tolerance) => Direction.IsParallelTo(other.Direction, tolerance);

        /// <summary>
        /// Hai đoạn nằm trên cùng một đường thẳng trong dung sai độ dài: mọi đầu mút của đoạn này cách đường thẳng
        /// chứa đoạn kia không quá dung sai, và ngược lại. Không dùng dung sai góc, vì đoạn ngắn lệch vài phần trăm mm
        /// vẫn phải coi là thẳng hàng.
        /// </summary>
        public bool IsCollinearWith(Segment2 other, Tolerance tolerance)
        {
            if (IsDegenerate(tolerance) || other.IsDegenerate(tolerance)) return false;
            var eps = tolerance.Length;
            return GeometryOps.DistancePointToLine(other.A, A, B) <= eps
                && GeometryOps.DistancePointToLine(other.B, A, B) <= eps
                && GeometryOps.DistancePointToLine(A, other.A, other.B) <= eps
                && GeometryOps.DistancePointToLine(B, other.A, other.B) <= eps;
        }

        public bool Equals(Segment2 other) => A.Equals(other.A) && B.Equals(other.B);
        public override bool Equals(object? obj) => obj is Segment2 s && Equals(s);
        public override int GetHashCode() => unchecked(A.GetHashCode() * 397 ^ B.GetHashCode());
        public override string ToString() => $"{A} -> {B}";
    }
}
