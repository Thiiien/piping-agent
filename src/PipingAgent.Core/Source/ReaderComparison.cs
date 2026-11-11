using System;
using System.Collections.Generic;
using System.Linq;

namespace PipingAgent.Core.Source
{
    /// <summary>Một khác biệt giữa hai bộ đọc trên cùng một đối tượng.</summary>
    public sealed class EntityDifference
    {
        public EntityDifference(string handle, SourceKind kind, string field, string left, string right)
        {
            Handle = handle;
            Kind = kind;
            Field = field;
            Left = left;
            Right = right;
        }

        public string Handle { get; }
        public SourceKind Kind { get; }
        public string Field { get; }
        public string Left { get; }
        public string Right { get; }
    }

    public sealed class ComparisonResult
    {
        public List<string> OnlyInLeft { get; } = new List<string>();
        public List<string> OnlyInRight { get; } = new List<string>();
        public List<EntityDifference> Different { get; } = new List<EntityDifference>();
        public bool Identical => OnlyInLeft.Count == 0 && OnlyInRight.Count == 0 && Different.Count == 0;
    }

    /// <summary>
    /// So hai bộ đối tượng nguồn đọc từ cùng một bản vẽ bằng hai bộ đọc, theo handle. Việc S1-16, mục 13.6.
    /// So trên dạng chuẩn hoá của dấu vân tay, nên hai bộ đọc trùng nhau khi và chỉ khi dấu vân tay trùng.
    /// </summary>
    public static class ReaderComparison
    {
        public static ComparisonResult Compare(SourceDump left, SourceDump right)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));

            var result = new ComparisonResult();
            var leftMap = left.Entities.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
            var rightMap = right.Entities.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);

            foreach (var key in leftMap.Keys.Where(k => !rightMap.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal)) result.OnlyInLeft.Add(key);
            foreach (var key in rightMap.Keys.Where(k => !leftMap.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal)) result.OnlyInRight.Add(key);

            foreach (var key in leftMap.Keys.Where(rightMap.ContainsKey).OrderBy(k => k, StringComparer.Ordinal))
            {
                var a = leftMap[key];
                var b = rightMap[key];
                var canonicalA = DrawingFingerprint.Canonical(a);
                var canonicalB = DrawingFingerprint.Canonical(b);
                if (canonicalA == canonicalB) continue;
                result.Different.Add(new EntityDifference(key, a.Kind, FirstDifferentField(a, b), canonicalA, canonicalB));
            }

            return result;
        }

        private static string Key(SourceEntity e) => e.BlockPath.Count == 0 ? e.Handle : string.Join("/", e.BlockPath) + "/" + e.Handle;

        private static string FirstDifferentField(SourceEntity a, SourceEntity b)
        {
            if (a.Kind != b.Kind) return "kind";
            if (!string.Equals(a.Layer, b.Layer, StringComparison.OrdinalIgnoreCase)) return "layer";
            if (a.Color != b.Color) return "color";
            if (!string.Equals(a.Linetype, b.Linetype, StringComparison.OrdinalIgnoreCase)) return "linetype";
            if (a.Name != b.Name) return "name";
            if (a.Text != b.Text) return "text";
            if ((a.Points?.Count ?? 0) != (b.Points?.Count ?? 0)) return "points.count";
            if (a.Points != null && b.Points != null && a.Points.Zip(b.Points, (p, q) => p.DistanceTo(q)).Any(d => d > 0.0015)) return "points";
            if (a.Center.HasValue != b.Center.HasValue || (a.Center.HasValue && a.Center.Value.DistanceTo(b.Center!.Value) > 0.0015)) return "center";
            if (!Same(a.Radius, b.Radius)) return "radius";
            if (!Same(a.StartAngleDeg, b.StartAngleDeg) || !Same(a.EndAngleDeg, b.EndAngleDeg)) return "angles";
            if (!Same(a.TextHeight, b.TextHeight)) return "textHeight";
            if (!Same(a.RotationDeg, b.RotationDeg)) return "rotation";
            if (a.Scale.HasValue != b.Scale.HasValue || (a.Scale.HasValue && (a.Scale.Value - b.Scale!.Value).Length > 0.0015)) return "scale";
            if ((a.Attributes?.Count ?? 0) != (b.Attributes?.Count ?? 0)) return "attributes.count";
            if (a.Attributes != null && b.Attributes != null && a.Attributes.Any(p => !b.Attributes.TryGetValue(p.Key, out var v) || v != p.Value)) return "attributes";
            return "other";
        }

        private static bool Same(double? a, double? b)
        {
            if (a.HasValue != b.HasValue) return false;
            return !a.HasValue || Math.Abs(a.Value - b!.Value) <= 0.0015;
        }
    }
}
