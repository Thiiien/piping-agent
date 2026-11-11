using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PipingAgent.Core.Source
{
    /// <summary>
    /// Dấu vân tay nội dung bản vẽ, SHA-256 trên dạng chuẩn hoá của các đối tượng nguồn.
    /// Không phụ thuộc thứ tự đọc, không phụ thuộc bộ đọc, làm tròn toạ độ 0,001 mm.
    /// Dùng để chứng minh bản vẽ không bị sửa sau khi chạy, NFR-REL-03 và FR-ING-06, và để biết IR cũ còn khớp bản vẽ không.
    /// </summary>
    public static class DrawingFingerprint
    {
        public const string Prefix = "sha256:";

        public static string Compute(IEnumerable<SourceEntity> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var lines = entities.Select(Canonical).ToList();
            lines.Sort(StringComparer.Ordinal);

            using var sha = SHA256.Create();
            var sb = new StringBuilder();
            foreach (var line in lines) sb.Append(line).Append('\n');
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Prefix + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>Một dòng chuẩn hoá cho một đối tượng. Công khai để kiểm thử và để so hai bộ đọc.</summary>
        public static string Canonical(SourceEntity e)
        {
            var sb = new StringBuilder();
            sb.Append(e.Handle).Append('|').Append(e.Kind).Append('|').Append(e.Layer.ToUpperInvariant());
            sb.Append('|').Append(e.Color?.ToString(CultureInfo.InvariantCulture) ?? "-");
            sb.Append('|').Append(e.Linetype?.ToUpperInvariant() ?? "-");
            sb.Append('|').Append(string.Join("/", e.BlockPath));
            sb.Append('|').Append(e.Name ?? "-");
            sb.Append('|').Append(e.Text ?? "-");
            if (e.Points != null)
            {
                sb.Append('|');
                foreach (var p in e.Points) Append(sb, p.X, p.Y, p.Z);
            }

            if (e.Center.HasValue) { sb.Append("|c"); Append(sb, e.Center.Value.X, e.Center.Value.Y, e.Center.Value.Z); }
            if (e.Radius.HasValue) sb.Append("|r").Append(R(e.Radius.Value));
            if (e.StartAngleDeg.HasValue) sb.Append("|a").Append(R(e.StartAngleDeg.Value)).Append(',').Append(R(e.EndAngleDeg ?? 0));
            if (e.TextHeight.HasValue) sb.Append("|h").Append(R(e.TextHeight.Value));
            if (e.RotationDeg.HasValue) sb.Append("|o").Append(R(e.RotationDeg.Value));
            if (e.Scale.HasValue) { sb.Append("|s"); Append(sb, e.Scale.Value.X, e.Scale.Value.Y, e.Scale.Value.Z); }
            if (e.Attributes != null && e.Attributes.Count > 0)
            {
                sb.Append("|t");
                foreach (var pair in e.Attributes.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    sb.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
                }
            }

            return sb.ToString();
        }

        private static void Append(StringBuilder sb, double x, double y, double z) =>
            sb.Append(R(x)).Append(',').Append(R(y)).Append(',').Append(R(z)).Append(';');

        private static string R(double v)
        {
            var rounded = Math.Round(v, 3, MidpointRounding.AwayFromZero);
            if (rounded == 0) rounded = 0; // bỏ dấu âm của -0
            return rounded.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
