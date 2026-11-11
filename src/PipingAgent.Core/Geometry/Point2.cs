using System;
using System.Globalization;

namespace PipingAgent.Core.Geometry
{
    /// <summary>Điểm trên mặt bằng, mm.</summary>
    public readonly struct Point2 : IEquatable<Point2>
    {
        public static readonly Point2 Origin = new Point2(0, 0);

        public Point2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public Point3 At(double z) => new Point3(X, Y, z);

        public double DistanceTo(Point2 other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool IsSame(Point2 other, Tolerance tolerance) => DistanceTo(other) <= tolerance.Length;

        public static Vector2 operator -(Point2 a, Point2 b) => new Vector2(a.X - b.X, a.Y - b.Y);
        public static Point2 operator +(Point2 p, Vector2 v) => new Point2(p.X + v.X, p.Y + v.Y);
        public static Point2 operator -(Point2 p, Vector2 v) => new Point2(p.X - v.X, p.Y - v.Y);
        public static bool operator ==(Point2 a, Point2 b) => a.Equals(b);
        public static bool operator !=(Point2 a, Point2 b) => !a.Equals(b);

        /// <summary>Bằng nhau tuyệt đối. Dùng <see cref="IsSame"/> cho so sánh hình học.</summary>
        public bool Equals(Point2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Point2 p && Equals(p);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###})", X, Y);
    }

    /// <summary>Véc-tơ trên mặt bằng.</summary>
    public readonly struct Vector2 : IEquatable<Vector2>
    {
        public static readonly Vector2 Zero = new Vector2(0, 0);
        public static readonly Vector2 UnitX = new Vector2(1, 0);
        public static readonly Vector2 UnitY = new Vector2(0, 1);

        public Vector2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public double Length => Math.Sqrt(X * X + Y * Y);
        public double LengthSquared => X * X + Y * Y;

        public double Dot(Vector2 other) => X * other.X + Y * other.Y;

        /// <summary>Tích có hướng 2D, dương khi quay ngược chiều kim đồng hồ từ véc-tơ này sang véc-tơ kia.</summary>
        public double Cross(Vector2 other) => X * other.Y - Y * other.X;

        /// <summary>Góc so với trục X, radian, trong [0, 2π).</summary>
        public double AngleRad => Tolerance.NormalizeRad(Math.Atan2(Y, X));

        public Vector2 Normalized()
        {
            var length = Length;
            if (length == 0) throw new InvalidOperationException("Không chuẩn hoá được véc-tơ không.");
            return new Vector2(X / length, Y / length);
        }

        public Vector2 Perpendicular() => new Vector2(-Y, X);

        public bool IsParallelTo(Vector2 other, Tolerance tolerance)
        {
            var l = Length * other.Length;
            if (l == 0) return false;
            return Math.Abs(Cross(other)) / l <= Math.Sin(tolerance.AngleRad);
        }

        public bool IsPerpendicularTo(Vector2 other, Tolerance tolerance)
        {
            var l = Length * other.Length;
            if (l == 0) return false;
            return Math.Abs(Dot(other)) / l <= Math.Sin(tolerance.AngleRad);
        }

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y);
        public static Vector2 operator -(Vector2 v) => new Vector2(-v.X, -v.Y);
        public static Vector2 operator *(Vector2 v, double k) => new Vector2(v.X * k, v.Y * k);
        public static Vector2 operator *(double k, Vector2 v) => new Vector2(v.X * k, v.Y * k);
        public static Vector2 operator /(Vector2 v, double k) => new Vector2(v.X / k, v.Y / k);
        public static bool operator ==(Vector2 a, Vector2 b) => a.Equals(b);
        public static bool operator !=(Vector2 a, Vector2 b) => !a.Equals(b);

        public bool Equals(Vector2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Vector2 v && Equals(v);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "<{0:0.###}, {1:0.###}>", X, Y);
    }
}
