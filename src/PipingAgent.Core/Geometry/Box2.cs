using System;
using System.Collections.Generic;

namespace PipingAgent.Core.Geometry
{
    /// <summary>Hộp bao trên mặt bằng, cạnh song song trục.</summary>
    public readonly struct Box2 : IEquatable<Box2>
    {
        public Box2(Point2 min, Point2 max)
        {
            if (min.X > max.X || min.Y > max.Y)
            {
                throw new ArgumentException("Góc nhỏ phải không lớn hơn góc lớn. Dùng FromPoints nếu chưa biết thứ tự.");
            }

            Min = min;
            Max = max;
        }

        public Point2 Min { get; }
        public Point2 Max { get; }

        public double Width => Max.X - Min.X;
        public double Height => Max.Y - Min.Y;
        public Point2 Center => new Point2((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2);

        public static Box2 FromPoints(Point2 a, Point2 b) => new Box2(
            new Point2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)),
            new Point2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));

        public static Box2 FromPoints(IEnumerable<Point2> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            var any = false;
            foreach (var p in points)
            {
                any = true;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            if (!any) throw new ArgumentException("Cần ít nhất một điểm.", nameof(points));
            return new Box2(new Point2(minX, minY), new Point2(maxX, maxY));
        }

        public static Box2 Around(Point2 center, double radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            return new Box2(new Point2(center.X - radius, center.Y - radius), new Point2(center.X + radius, center.Y + radius));
        }

        public Box2 Expand(double margin) => new Box2(
            new Point2(Min.X - margin, Min.Y - margin), new Point2(Max.X + margin, Max.Y + margin));

        public Box2 Union(Box2 other) => new Box2(
            new Point2(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y)),
            new Point2(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y)));

        public bool Contains(Point2 p) => p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y;

        public bool Contains(Point2 p, Tolerance tolerance) => Expand(tolerance.Length).Contains(p);

        public bool Intersects(Box2 other) =>
            Min.X <= other.Max.X && Max.X >= other.Min.X && Min.Y <= other.Max.Y && Max.Y >= other.Min.Y;

        public double DistanceTo(Point2 p)
        {
            var dx = Math.Max(Math.Max(Min.X - p.X, 0), p.X - Max.X);
            var dy = Math.Max(Math.Max(Min.Y - p.Y, 0), p.Y - Max.Y);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Equals(Box2 other) => Min.Equals(other.Min) && Max.Equals(other.Max);
        public override bool Equals(object? obj) => obj is Box2 b && Equals(b);
        public override int GetHashCode() => unchecked(Min.GetHashCode() * 397 ^ Max.GetHashCode());
        public override string ToString() => $"[{Min} .. {Max}]";
    }
}
