using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Recognition
{
    /// <summary>Nhãn tuyến ống đã tách trường.</summary>
    public sealed class ParsedTag
    {
        public ParsedTag(string text, string patternName)
        {
            Text = text;
            PatternName = patternName;
        }

        /// <summary>Chữ đã chuẩn hoá, đầu vào của việc khớp mẫu.</summary>
        public string Text { get; }

        public string PatternName { get; }

        /// <summary>Số hiệu tuyến như trên bản vẽ.</summary>
        public string Line { get; set; } = string.Empty;

        /// <summary>Đường kính danh nghĩa, mm. Null khi nhãn không ghi.</summary>
        public int? Dn { get; set; }

        /// <summary>Cỡ ghi trên nhãn trước khi quy đổi, ví dụ <c>6"</c>, để hiển thị.</summary>
        public string? DnText { get; set; }

        /// <summary>Cao độ, mm, theo mốc <see cref="Datum"/>. Null khi nhãn không ghi.</summary>
        public double? Elevation { get; set; }

        public ElevationDatum Datum { get; set; } = ElevationDatum.Unknown;

        public string? Service { get; set; }

        public string? Spec { get; set; }

        public string? InsulationType { get; set; }

        public double? InsulationThickness { get; set; }

        /// <summary>Đúng với chữ cao độ ghi rời, không có số hiệu tuyến. Chỉ bổ sung cao độ cho đoạn chạy.</summary>
        public bool IsElevationOnly => Line.Length == 0 && Elevation.HasValue;

        /// <summary>Các nhóm đặt tên khác của mẫu, giữ nguyên chuỗi.</summary>
        public Dictionary<string, string> Extra { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Lỗi mềm khi tách, ví dụ cỡ inch không có trong bảng. Nhãn vẫn được coi là khớp.</summary>
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>Kết quả đọc một đối tượng chữ trên layer nhãn.</summary>
    public sealed class TagReadResult
    {
        public TagReadResult(SourceEntity entity, ParsedTag? tag)
        {
            Entity = entity;
            Tag = tag;
        }

        public SourceEntity Entity { get; }

        /// <summary>Null khi chữ không khớp mẫu nào. Được ghi nhận, không bị bỏ qua, luật R-TAG-03.</summary>
        public ParsedTag? Tag { get; }

        public bool Matched => Tag != null;
    }

    /// <summary>
    /// Đọc nhãn theo mẫu trong hồ sơ, S2-05. Chữ nhiều dòng đã bỏ mã định dạng ở bộ đọc; ở đây chuẩn hoá khoảng trắng lần nữa
    /// rồi khớp lần lượt từng mẫu, mẫu đầu tiên khớp thắng.
    /// </summary>
    public sealed class TagParser
    {
        private static readonly string[] StandardGroups =
        {
            "line", "dn", "el", "datum", "service", "unit", "seq", "spec", "insul", "insulThk", "tag",
        };

        private readonly (TagPattern Pattern, Regex Regex)[] _patterns;

        public TagParser(ConventionProfile profile)
            : this((profile ?? throw new ArgumentNullException(nameof(profile))).TagPatterns.Concat(profile.ElevationPatterns))
        {
        }

        /// <param name="patterns">Mẫu nhãn tuyến rồi tới mẫu cao độ rời, xét theo thứ tự.</param>
        public TagParser(IEnumerable<TagPattern> patterns)
        {
            if (patterns == null) throw new ArgumentNullException(nameof(patterns));
            _patterns = patterns
                .Select(p => (p, new Regex(p.Regex, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))))
                .ToArray();
        }

        public int PatternCount => _patterns.Length;

        public ParsedTag? Parse(string? text)
        {
            var normalized = CadText.Normalize(CadText.Strip(text));
            if (normalized.Length == 0) return null;
            foreach (var (pattern, regex) in _patterns)
            {
                var m = regex.Match(normalized);
                if (!m.Success) continue;
                return Build(normalized, pattern, regex, m);
            }

            return null;
        }

        public TagReadResult Read(SourceEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return new TagReadResult(entity, Parse(entity.Text ?? entity.RawText));
        }

        public List<TagReadResult> ReadAll(IEnumerable<SourceEntity> entities) => entities.Select(Read).ToList();

        private static ParsedTag Build(string text, TagPattern pattern, Regex regex, Match m)
        {
            var tag = new ParsedTag(text, pattern.Name);
            foreach (var name in regex.GetGroupNames())
            {
                if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) continue;
                var g = m.Groups[name];
                if (!g.Success) continue;
                var value = g.Value.Trim();
                switch (name)
                {
                    case "line": tag.Line = value; break;
                    case "dn": ReadDn(tag, value, pattern); break;
                    case "el": ReadElevation(tag, value, pattern); break;
                    case "datum": tag.Datum = ParseDatum(value, tag); break;
                    case "service": tag.Service = value; break;
                    case "spec": tag.Spec = value; break;
                    case "insul": tag.InsulationType = value; break;
                    case "insulThk":
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var thk)) tag.InsulationThickness = thk;
                        else tag.Warnings.Add($"Chiều dày cách nhiệt '{value}' không phải số.");
                        break;
                    default:
                        tag.Extra[name] = value;
                        break;
                }
            }

            if (tag.Datum == ElevationDatum.Unknown && pattern.ElevationDatum.HasValue && !m.Groups["datum"].Success)
            {
                tag.Datum = pattern.ElevationDatum.Value;
            }

            if (tag.Line.Length == 0 && m.Groups["tag"].Success)
            {
                tag.Line = m.Groups["tag"].Value.Trim();
            }

            return tag;
        }

        private static void ReadDn(ParsedTag tag, string value, TagPattern pattern)
        {
            tag.DnText = value;
            var isInch = pattern.DnUnit == "inch" || value.IndexOf('"') >= 0 || value.IndexOf('/') >= 0;
            if (isInch)
            {
                var dn = NominalSize.NpsToDn(value);
                if (dn.HasValue) tag.Dn = dn;
                else tag.Warnings.Add($"Cỡ inch '{value}' không có trong bảng quy đổi.");
                return;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mm))
            {
                tag.Dn = mm;
                if (!NominalSize.IsStandardDn(mm)) tag.Warnings.Add($"DN{mm} không phải cỡ chuẩn.");
            }
            else
            {
                tag.Warnings.Add($"DN '{value}' không phải số.");
            }
        }

        private static void ReadElevation(ParsedTag tag, string value, TagPattern pattern)
        {
            var cleaned = value.Replace(" ", string.Empty).Replace(",", string.Empty);
            if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var el))
            {
                tag.Warnings.Add($"Cao độ '{value}' không phải số.");
                return;
            }

            tag.Elevation = pattern.ElevationUnit == "m" ? el * 1000.0 : el;
        }

        private static ElevationDatum ParseDatum(string value, ParsedTag tag)
        {
            switch (value.Trim().ToUpperInvariant())
            {
                case "CL": case "C/L": case "CTR": case "C": return ElevationDatum.CL;
                case "BOP": case "B.O.P": case "B.O.P.": case "INV": return ElevationDatum.BOP;
                case "TOP": case "T.O.P": case "T.O.P.": return ElevationDatum.TOP;
                case "TOS": case "T.O.S": case "T.O.S.": return ElevationDatum.TOS;
                default:
                    tag.Warnings.Add($"Mốc cao độ '{value}' không nhận ra.");
                    return ElevationDatum.Unknown;
            }
        }

        public static bool IsStandardGroup(string name) => Array.IndexOf(StandardGroups, name) >= 0;
    }
}
