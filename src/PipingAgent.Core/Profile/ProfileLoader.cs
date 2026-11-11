using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PipingAgent.Core.Ir.Json;

namespace PipingAgent.Core.Profile
{
    /// <summary>Một lỗi trong hồ sơ quy ước, có dòng và trường để người dùng sửa.</summary>
    public sealed class ProfileError
    {
        public ProfileError(string path, string message, int? line = null, int? column = null)
        {
            Path = path;
            Message = message;
            Line = line;
            Column = column;
        }

        /// <summary>Đường dẫn JSON, ví dụ <c>$.roles[2].role</c>.</summary>
        public string Path { get; }

        public string Message { get; }

        public int? Line { get; }

        public int? Column { get; }

        public override string ToString() =>
            Line.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "dòng {0}, {1}: {2}", Line, Path, Message)
                : $"{Path}: {Message}";
    }

    public sealed class ProfileLoadResult
    {
        public ConventionProfile? Profile { get; set; }

        public List<ProfileError> Errors { get; } = new List<ProfileError>();

        public bool Ok => Profile != null && Errors.Count == 0;
    }

    /// <summary>
    /// Nạp và kiểm tra hồ sơ quy ước, S2-01. Lỗi cú pháp và lỗi nội dung đều trả về cùng một danh sách,
    /// mỗi lỗi có dòng và trường. Không ném ngoại lệ với hồ sơ sai.
    /// </summary>
    public static class ProfileLoader
    {
        public static readonly string[] ReadableSchemaVersions = { "1.0" };

        private static readonly JsonSerializerOptions Options = IrJson.CreateOptions(indented: true);

        public static string Serialize(ConventionProfile profile) => JsonSerializer.Serialize(profile, Options);

        /// <summary>Đọc một hồ sơ từ văn bản JSON, không giải quyết <c>extends</c>.</summary>
        public static ProfileLoadResult Parse(string json)
        {
            var result = new ProfileLoadResult();
            if (string.IsNullOrWhiteSpace(json))
            {
                result.Errors.Add(new ProfileError("$", "Hồ sơ rỗng."));
                return result;
            }

            ConventionProfile? profile;
            try
            {
                profile = JsonSerializer.Deserialize<ConventionProfile>(json, Options);
            }
            catch (JsonException ex)
            {
                var line = ex.LineNumber.HasValue ? (int?)(ex.LineNumber.Value + 1) : null;
                var col = ex.BytePositionInLine.HasValue ? (int?)(ex.BytePositionInLine.Value + 1) : null;
                result.Errors.Add(new ProfileError(ex.Path ?? "$", Simplify(ex.Message), line, col));
                return result;
            }

            if (profile == null)
            {
                result.Errors.Add(new ProfileError("$", "Hồ sơ phải là một đối tượng JSON."));
                return result;
            }

            var locator = new JsonLocator(json);
            Validate(profile, result.Errors, locator);
            if (result.Errors.Count == 0) result.Profile = profile;
            return result;
        }

        /// <summary>
        /// Đọc hồ sơ từ văn bản JSON và giải quyết chuỗi <c>extends</c>. Lõi không đọc tệp: ứng dụng chủ truyền hàm
        /// trả về JSON của hồ sơ cha theo định danh, hoặc null nếu không có. Xem <c>PipingAgent.Hosting.ProfileFiles</c>.
        /// </summary>
        public static ProfileLoadResult Load(string json, Func<string, string?> readParentJson)
        {
            if (readParentJson == null) throw new ArgumentNullException(nameof(readParentJson));
            var result = new ProfileLoadResult();
            var chain = new List<ConventionProfile>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = json;
            string? currentId = null;
            while (true)
            {
                if (current == null)
                {
                    result.Errors.Add(new ProfileError("$.extends", $"Không tìm thấy hồ sơ cha '{currentId}'."));
                    return result;
                }

                var one = Parse(current);
                if (!one.Ok)
                {
                    foreach (var e in one.Errors)
                    {
                        result.Errors.Add(chain.Count == 0 ? e : new ProfileError(e.Path, $"[{currentId}] {e.Message}", e.Line, e.Column));
                    }

                    return result;
                }

                var p = one.Profile!;
                if (!seen.Add(p.Id))
                {
                    result.Errors.Add(new ProfileError("$.extends", $"Chuỗi extends lặp vòng tại '{p.Id}'."));
                    return result;
                }

                chain.Add(p);
                if (string.IsNullOrEmpty(p.Extends)) break;
                currentId = p.Extends;
                current = readParentJson(p.Extends!);
            }

            var merged = chain[chain.Count - 1];
            for (var i = chain.Count - 2; i >= 0; i--)
            {
                merged = Merge(chain[i], merged);
            }

            result.Profile = merged;
            return result;
        }

        /// <summary>Gộp hồ sơ con lên hồ sơ cha. Giá trị đơn của con thắng, danh sách của con đứng trước danh sách của cha.</summary>
        public static ConventionProfile Merge(ConventionProfile child, ConventionProfile parent)
        {
            var m = new ConventionProfile
            {
                SchemaVersion = child.SchemaVersion,
                Id = child.Id,
                Version = child.Version,
                Extends = child.Extends,
                Units = child.Units,
                PipeStandard = child.PipeStandard ?? parent.PipeStandard,
                FittingStandard = child.FittingStandard ?? parent.FittingStandard,
                Tolerances = child.HasTolerances ? child.Tolerances : parent.Tolerances,
                StructureRules = child.StructureRules ?? parent.StructureRules,
            };
            m.Roles.AddRange(child.Roles);
            m.Roles.AddRange(parent.Roles);
            m.TagPatterns.AddRange(child.TagPatterns);
            m.TagPatterns.AddRange(parent.TagPatterns);
            m.ElevationPatterns.AddRange(child.ElevationPatterns);
            m.ElevationPatterns.AddRange(parent.ElevationPatterns);
            m.Blocks.AddRange(child.Blocks);
            m.Blocks.AddRange(parent.Blocks);
            m.ComponentTagPatterns.AddRange(child.ComponentTagPatterns);
            m.ComponentTagPatterns.AddRange(parent.ComponentTagPatterns);
            m.EquipmentRules.AddRange(child.EquipmentRules);
            m.EquipmentRules.AddRange(parent.EquipmentRules);
            foreach (var kv in parent.Rules) m.Rules[kv.Key] = kv.Value;
            foreach (var kv in child.Rules) m.Rules[kv.Key] = kv.Value;
            return m;
        }

        private static readonly string[] Severities = { "blocker", "warning", "info", "off" };

        private static void Validate(ConventionProfile p, List<ProfileError> errors, JsonLocator locator)
        {
            void Add(string path, string message)
            {
                var pos = locator.Find(path);
                errors.Add(new ProfileError(path, message, pos?.Line, pos?.Column));
            }

            if (Array.IndexOf(ReadableSchemaVersions, p.SchemaVersion) < 0)
            {
                Add("$.schemaVersion", $"Phiên bản lược đồ '{p.SchemaVersion}' không đọc được. Hỗ trợ: {string.Join(", ", ReadableSchemaVersions)}.");
            }

            if (string.IsNullOrWhiteSpace(p.Id)) Add("$.id", "Thiếu định danh hồ sơ.");
            else if (!Regex.IsMatch(p.Id, "^[A-Za-z0-9][A-Za-z0-9._-]*$")) Add("$.id", "Định danh chỉ gồm chữ, số, dấu chấm, gạch ngang, gạch dưới.");
            if (p.Version < 1) Add("$.version", "Phiên bản phải từ 1 trở lên.");
            if (p.Units != "mm") Add("$.units", $"Đơn vị '{p.Units}' chưa hỗ trợ, hiện chỉ có mm.");
            if (p.Extends != null && string.Equals(p.Extends, p.Id, StringComparison.OrdinalIgnoreCase)) Add("$.extends", "Hồ sơ không thể kế thừa chính nó.");

            var t = p.HasTolerances ? p.Tolerances : new ProfileTolerances();
            if (!(t.Snap > 0)) Add("$.tolerances.snap", "Dung sai bắt điểm phải là số dương.");
            if (!(t.CollinearDeg >= 0 && t.CollinearDeg < 45)) Add("$.tolerances.collinearDeg", "Góc thẳng hàng phải từ 0 đến dưới 45 độ.");
            if (!(t.TagSearchRadius > 0)) Add("$.tolerances.tagSearchRadius", "Bán kính tìm nhãn phải là số dương.");
            if (!(t.LeaderTouch >= 0)) Add("$.tolerances.leaderTouch", "Dung sai chạm của đường dẫn không được âm.");
            if (!(t.TagAdjacentFactor > 0)) Add("$.tolerances.tagAdjacentFactor", "Hệ số nằm sát phải là số dương.");
            if (t.MinSegmentLength.HasValue && t.MinSegmentLength.Value < 0) Add("$.tolerances.minSegmentLength", "Chiều dài nét tối thiểu không được âm.");
            if (!(t.UnclassifiedWarning >= 0 && t.UnclassifiedWarning <= 1)) Add("$.tolerances.unclassifiedWarning", "Ngưỡng chưa phân loại phải từ 0 đến 1.");
            if (t.FittingReach < 0) Add("$.tolerances.fittingReach", "Bán kính nối qua block không được âm.");
            if (t.GapTolerance < 0) Add("$.tolerances.gapTolerance", "Ngưỡng khe hở không được âm.");
            if (t.DefaultSupportHeight.HasValue && t.DefaultSupportHeight.Value < 0) Add("$.tolerances.defaultSupportHeight", "Chiều cao đỡ không được âm.");
            if (t.AllowedBendAngles == null || t.AllowedBendAngles.Count == 0) Add("$.tolerances.allowedBendAngles", "Cần ít nhất một góc đổi hướng cho phép.");
            else if (t.AllowedBendAngles.Any(a => !(a > 0 && a < 180))) Add("$.tolerances.allowedBendAngles", "Góc đổi hướng phải trong khoảng 0 đến 180 độ.");

            for (var i = 0; i < p.Roles.Count; i++)
            {
                var r = p.Roles[i];
                var path = $"$.roles[{i}]";
                if (r == null)
                {
                    Add(path, "Quy tắc rỗng.");
                    continue;
                }

                if (!EntityRoles.IsKnown(r.Role))
                {
                    Add(path + ".role", $"Vai trò '{r.Role}' không có trong danh sách: {string.Join(", ", EntityRoles.All)}.");
                }

                var m = r.Match;
                if (m == null || (m.Layer == null && m.Layers == null && m.Types == null && m.Block == null && m.Color == null && m.Linetype == null && m.TextPattern == null))
                {
                    Add(path + ".match", "Điều kiện khớp phải có ít nhất một trường.");
                    continue;
                }

                if (m.Layers != null && m.Layers.Count == 0) Add(path + ".match.layers", "Danh sách layer rỗng.");
                if (m.Types != null)
                {
                    for (var k = 0; k < m.Types.Count; k++)
                    {
                        if (string.IsNullOrWhiteSpace(m.Types[k])) Add($"{path}.match.types[{k}]", "Tên loại rỗng.");
                    }
                }

                if (m.Color.HasValue && (m.Color < 0 || m.Color > 256)) Add(path + ".match.color", "Màu ACI phải từ 0 đến 256.");
                CheckRegex(m.TextPattern, path + ".match.textPattern", Add);
            }

            CheckPatterns(p.TagPatterns, "$.tagPatterns", Add, "line", "Mẫu nhãn tuyến phải có nhóm đặt tên 'line'.");
            CheckPatterns(p.ElevationPatterns, "$.elevationPatterns", Add, "el", "Mẫu cao độ phải có nhóm đặt tên 'el'.");
            CheckPatterns(p.ComponentTagPatterns, "$.componentTagPatterns", Add, "tag", "Mẫu chú thích cấu kiện phải có nhóm đặt tên 'tag'.");

            for (var i = 0; i < p.Blocks.Count; i++)
            {
                var b = p.Blocks[i];
                var path = $"$.blocks[{i}]";
                if (b == null)
                {
                    Add(path, "Mục rỗng.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(b.Name)) Add(path + ".name", "Thiếu tên block.");
                var kinds = (b.Component != null ? 1 : 0) + (b.Structure != null ? 1 : 0) + (b.Equipment != null ? 1 : 0) + (b.Symbol != null ? 1 : 0);
                if (kinds == 0) Add(path, "Block phải ánh xạ sang một trong component, structure, equipment, symbol.");
                if (kinds > 1) Add(path, "Block chỉ ánh xạ sang một loại.");
            }

            for (var i = 0; i < p.EquipmentRules.Count; i++)
            {
                var e = p.EquipmentRules[i];
                var path = $"$.equipmentRules[{i}]";
                if (e == null)
                {
                    Add(path, "Mục rỗng.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(e.Type)) Add(path + ".type", "Thiếu loại thiết bị.");
                if (e.Match == null || (e.Match.Shape == null && e.Match.Layer == null && e.Match.Block == null))
                {
                    Add(path + ".match", "Điều kiện khớp thiết bị cần shape, layer hoặc block.");
                }
                else if (e.Match.Shape != null && Array.IndexOf(new[] { "circle", "closed_rectangle", "closed_polyline", "block" }, e.Match.Shape) < 0)
                {
                    Add(path + ".match.shape", $"Hình '{e.Match.Shape}' không hỗ trợ. Dùng circle, closed_rectangle, closed_polyline, block.");
                }
            }

            if (p.StructureRules != null) CheckRegex(p.StructureRules.TosPattern, "$.structureRules.tosPattern", Add);

            foreach (var kv in p.Rules)
            {
                if (!Regex.IsMatch(kv.Key, "^R-[A-Z]{3}-\\d{2}$")) Add("$.rules." + kv.Key, "Mã luật phải có dạng R-XXX-nn.");
                if (Array.IndexOf(Severities, kv.Value) < 0) Add("$.rules." + kv.Key, $"Mức '{kv.Value}' không hợp lệ. Dùng blocker, warning, info, off.");
            }
        }

        private static void CheckPatterns(List<TagPattern> patterns, string basePath, Action<string, string> add, string requiredGroup, string requiredMessage)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < patterns.Count; i++)
            {
                var tp = patterns[i];
                var path = $"{basePath}[{i}]";
                if (tp == null)
                {
                    add(path, "Mẫu rỗng.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(tp.Name)) add(path + ".name", "Thiếu tên mẫu.");
                else if (!names.Add(tp.Name)) add(path + ".name", $"Tên mẫu '{tp.Name}' bị trùng.");
                var rx = CheckRegex(tp.Regex, path + ".regex", add);
                if (rx != null)
                {
                    if (Array.IndexOf(rx.GetGroupNames(), requiredGroup) < 0) add(path + ".regex", requiredMessage);
                }

                if (tp.DnUnit != null && tp.DnUnit != "mm" && tp.DnUnit != "inch") add(path + ".dnUnit", "Đơn vị DN phải là mm hoặc inch.");
                if (tp.ElevationUnit != null && tp.ElevationUnit != "mm" && tp.ElevationUnit != "m") add(path + ".elevationUnit", "Đơn vị cao độ phải là mm hoặc m.");
                if (tp.SearchRadius.HasValue && !(tp.SearchRadius.Value > 0)) add(path + ".searchRadius", "Bán kính tìm phải là số dương.");
            }
        }

        private static Regex? CheckRegex(string? pattern, string path, Action<string, string> add)
        {
            if (pattern == null) return null;
            if (pattern.Length == 0)
            {
                add(path, "Biểu thức chính quy rỗng.");
                return null;
            }

            try
            {
                return new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException ex)
            {
                add(path, "Biểu thức chính quy sai: " + ex.Message);
                return null;
            }
        }

        private static string Simplify(string message)
        {
            // Bỏ phần "Path: $.x | LineNumber: n | BytePositionInLine: m." của System.Text.Json, đã có cột riêng.
            var cut = message.IndexOf(" Path:", StringComparison.Ordinal);
            if (cut > 0) message = message.Substring(0, cut);
            if (message.StartsWith("JSON deserialization for type", StringComparison.Ordinal))
            {
                var idx = message.IndexOf("missing required properties", StringComparison.Ordinal);
                if (idx > 0) return "Thiếu trường bắt buộc: " + message.Substring(idx + "missing required properties".Length).Trim(' ', ':', '.', ',');
            }

            return message.Trim();
        }
    }
}
