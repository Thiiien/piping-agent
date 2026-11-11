using System;
using System.Globalization;

namespace PipingAgent.Core.Geometry
{
    /// <summary>Điểm trong không gian, mm, trục Z hướng lên.</summary>
    public readonly struct Point3 : IEquatable<Point3>
    {
        public static readonly Point3 Origin = new Point3(0, 0, 0);

        public Point3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Point2 Plan => new Point2(X, Y);

        public double DistanceTo(Point3 other) => (this - other).Length;

        public bool IsSame(Point3 other, Tolerance tolerance) => DistanceTo(other) <= tolerance.Length;

        public Point3 WithZ(double z) => new Point3(X, Y, z);

        public static Vector3 operator -(Point3 a, Point3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Point3 operator +(Point3 p, Vector3 v) => new Point3(p.X + v.X, p.Y + v.Y, p.Z + v.Z);
        public static Point3 operator -(Point3 p, Vector3 v) => new Point3(p.X - v.X, p.Y - v.Y, p.Z - v.Z);
        public static bool operator ==(Point3 a, Point3 b) => a.Equals(b);
        public static bool operator !=(Point3 a, Point3 b) => !a.Equals(b);

        public bool Equals(Point3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object? obj) => obj is Point3 p && Equals(p);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode());
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", X, Y, Z);
    }

    /// <summary>Véc-tơ trong không gian.</summary>
    public readonly struct Vector3 : IEquatable<Vector3>
    {
        public static readonly Vector3 Zero = new Vector3(0, 0, 0);
        public static readonly Vector3 UnitX = new Vector3(1, 0, 0);
        public static readonly Vector3 UnitY = new Vector3(0, 1, 0);
        public static readonly Vector3 UnitZ = new Vector3(0, 0, 1);

        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public double LengthSquared => X * X + Y * Y + Z * Z;

        public Vector2 Plan => new Vector2(X, Y);

        public double Dot(Vector3 other) => X * other.X + Y * other.Y + Z * other.Z;

        public Vector3 Cross(Vector3 other) => new Vector3(
            Y * other.Z - Z * other.Y,
            Z * other.X - X * other.Z,
            X * other.Y - Y * other.X);

        public bool IsZero(Tolerance tolerance) => Length <= tolerance.Length;

        public Vector3 Normalized()
        {
            var length = Length;
            if (length == 0) throw new InvalidOperationException("Không chuẩn hoá được véc-tơ không.");
            return new Vector3(X / length, Y / length, Z / length);
        }

        /// <summary>Góc giữa hai véc-tơ, radian, trong [0, π].</summary>
        public double AngleTo(Vector3 other)
        {
            var l = Length * other.Length;
            if (l == 0) throw new InvalidOperationException("Không tính được góc với véc-tơ không.");
            var c = Dot(other) / l;
            if (c > 1) c = 1;
            if (c < -1) c = -1;
            return Math.Acos(c);
        }

        public bool IsParallelTo(Vector3 other, Tolerance tolerance)
        {
            var l = Length * other.Length;
            if (l == 0) return false;
            return Cross(other).Length / l <= Math.Sin(tolerance.AngleRad);
        }

        public bool IsPerpendicularTo(Vector3 other, Tolerance tolerance)
        {
            var l = Length * other.Length;
            if (l == 0) return false;
            return Math.Abs(Dot(other)) / l <= Math.Sin(tolerance.AngleRad);
        }

        public bool IsVertical(Tolerance tolerance) => IsParallelTo(UnitZ, tolerance);

        public bool IsHorizontal(Tolerance tolerance) => IsPerpendicularTo(UnitZ, tolerance);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator -(Vector3 v) => new Vector3(-v.X, -v.Y, -v.Z);
        public static Vector3 operator *(Vector3 v, double k) => new Vector3(v.X * k, v.Y * k, v.Z * k);
        public static Vector3 operator *(double k, Vector3 v) => new Vector3(v.X * k, v.Y * k, v.Z * k);
        public static Vector3 operator /(Vector3 v, double k) => new Vector3(v.X / k, v.Y / k, v.Z / k);
        public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);
        public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);

        public bool Equals(Vector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object? obj) => obj is Vector3 v && Equals(v);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode());
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "<{0:0.###}, {1:0.###}, {2:0.###}>", X, Y, Z);
    }
}
