using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PipingAgent.Core.Catalog
{
    /// <summary>Quy đổi cỡ ống danh nghĩa giữa inch (NPS) và DN theo ASME B36.10M. Bảng cố định, không phụ thuộc danh mục.</summary>
    public static class NominalSize
    {
        private static readonly (string Nps, int Dn)[] Table =
        {
            ("1/8", 6), ("1/4", 8), ("3/8", 10), ("1/2", 15), ("3/4", 20), ("1", 25), ("1-1/4", 32), ("1-1/2", 40),
            ("2", 50), ("2-1/2", 65), ("3", 80), ("3-1/2", 90), ("4", 100), ("5", 125), ("6", 150), ("8", 200),
            ("10", 250), ("12", 300), ("14", 350), ("16", 400), ("18", 450), ("20", 500), ("22", 550), ("24", 600),
            ("26", 650), ("28", 700), ("30", 750), ("32", 800), ("34", 850), ("36", 900), ("38", 950), ("40", 1000),
            ("42", 1050), ("44", 1100), ("46", 1150), ("48", 1200),
        };

        private static readonly Dictionary<int, string> DnToNps = BuildDnToNps();

        private static readonly Regex NpsRegex = new Regex(
            @"^\s*(?<whole>\d+)?\s*(?:[-\s]\s*)?(?:(?<num>\d+)\s*/\s*(?<den>\d+))?\s*(?:""|''|in\.?|inch)?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Đọc cỡ inch như <c>6</c>, <c>6"</c>, <c>3/4"</c>, <c>1-1/2"</c>, <c>1 1/2</c>, <c>1.5</c> ra DN. Null nếu không hiểu hoặc không có trong bảng.
        /// </summary>
        public static int? NpsToDn(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var inches = ParseInches(text!);
            if (!inches.HasValue) return null;
            foreach (var (nps, dn) in Table)
            {
                if (Math.Abs(ParseInches(nps)!.Value - inches.Value) < 1e-6) return dn;
            }

            return null;
        }

        /// <summary>Giá trị inch dạng số thập phân của một chuỗi cỡ ống, ví dụ <c>1-1/2"</c> ra 1,5.</summary>
        public static double? ParseInches(string text)
        {
            if (text == null) return null;
            var trimmed = text.Trim();
            if (double.TryParse(trimmed.TrimEnd('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out var dec) && trimmed.IndexOf('/') < 0)
            {
                return dec;
            }

            var m = NpsRegex.Match(trimmed);
            if (!m.Success) return null;
            double value = 0;
            if (m.Groups["whole"].Success) value += int.Parse(m.Groups["whole"].Value, CultureInfo.InvariantCulture);
            if (m.Groups["num"].Success)
            {
                var den = int.Parse(m.Groups["den"].Value, CultureInfo.InvariantCulture);
                if (den == 0) return null;
                value += (double)int.Parse(m.Groups["num"].Value, CultureInfo.InvariantCulture) / den;
            }
            else if (!m.Groups["whole"].Success)
            {
                return null;
            }

            return value;
        }

        public static bool IsStandardDn(int dn) => DnToNps.ContainsKey(dn);

        private static Dictionary<int, string> BuildDnToNps()
        {
            var d = new Dictionary<int, string>();
            foreach (var (nps, dn) in Table) d[dn] = nps;
            return d;
        }
    }
}
