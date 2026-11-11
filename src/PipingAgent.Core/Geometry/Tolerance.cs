using System;

namespace PipingAgent.Core.Geometry
{
    /// <summary>
    /// Dung sai hình học. Mọi phép so sánh trong lõi đi qua đây, không so sánh số thực trực tiếp.
    /// Đơn vị milimét theo quy tắc 5 mục 6.12.3. Mặc định 0,01 mm, chặt hơn 10 lần so với NFR-ACC-01.
    /// </summary>
    public readonly struct Tolerance : IEquatable<Tolerance>
    {
        public const double DefaultLength = 0.01;
        public const double DefaultAngleDeg = 0.01;

        public static readonly Tolerance Default = new Tolerance(DefaultLength, DefaultAngleDeg);

        public Tolerance(double length, double angleDeg)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length), "Dung sai độ dài phải dương.");
            if (angleDeg <= 0) throw new ArgumentOutOfRangeException(nameof(angleDeg), "Dung sai góc phải dương.");
            Length = length;
            AngleDeg = angleDeg;
        }

        /// <summary>Dung sai độ dài, mm.</summary>
        public double Length { get; }

        /// <summary>Dung sai góc, độ.</summary>
        public double AngleDeg { get; }

        public double AngleRad => AngleDeg * Math.PI / 180.0;

        public bool Equals(Tolerance other) => Length.Equals(other.Length) && AngleDeg.Equals(other.AngleDeg);
        public override bool Equals(object? obj) => obj is Tolerance t && Equals(t);
        public override int GetHashCode() => unchecked(Length.GetHashCode() * 397 ^ AngleDeg.GetHashCode());
        public override string ToString() => $"±{Length} mm, ±{AngleDeg}°";

        /// <summary>Đưa góc về khoảng [0, 2π).</summary>
        public static double NormalizeRad(double angle)
        {
            var twoPi = 2 * Math.PI;
            angle %= twoPi;
            if (angle < 0) angle += twoPi;
            return angle;
        }
    }
}
