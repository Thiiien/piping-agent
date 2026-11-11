using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using PipingAgent.Core.Ir.Json;

namespace PipingAgent.Core.Equipment
{
    /// <summary>Giá trị trong định nghĩa: số hoặc biểu thức trên tham số. JSON nhận cả số lẫn chuỗi.</summary>
    [JsonConverter(typeof(ExprConverter))]
    public readonly struct Expr
    {
        public Expr(string text)
        {
            Text = text ?? string.Empty;
        }

        public string Text { get; }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Text);

        public double Evaluate(IReadOnlyDictionary<string, double> p) => Expression.Evaluate(Text, p);

        public override string ToString() => Text;

        public static implicit operator Expr(string s) => new Expr(s);

        public static implicit operator Expr(double d) => new Expr(d.ToString(CultureInfo.InvariantCulture));
    }

    public sealed class ExprConverter : JsonConverter<Expr>
    {
        public override Expr Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number: return new Expr(reader.GetDouble().ToString(CultureInfo.InvariantCulture));
                case JsonTokenType.String: return new Expr(reader.GetString() ?? string.Empty);
                case JsonTokenType.Null: return new Expr(string.Empty);
                default: throw new JsonException("Giá trị phải là số hoặc chuỗi biểu thức.");
            }
        }

        public override void Write(Utf8JsonWriter writer, Expr value, JsonSerializerOptions options)
        {
            if (double.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) writer.WriteNumberValue(d);
            else writer.WriteStringValue(value.Text);
        }
    }

    /// <summary>Tệp định nghĩa một loại thiết bị, mục 7.8.3.</summary>
    public sealed class EquipmentDefinition
    {
        [JsonRequired]
        public string Id { get; set; } = string.Empty;

        public string? Name { get; set; }

        [JsonRequired]
        public string SchemaVersion { get; set; } = "1.0";

        public List<EquipmentParam> Params { get; set; } = new List<EquipmentParam>();

        public List<BodyShape> Body { get; set; } = new List<BodyShape>();

        public List<NozzleDefinition> Nozzles { get; set; } = new List<NozzleDefinition>();

        public Dictionary<string, JsonElement>? Hosts { get; set; }

        /// <summary>Lớp thư viện đã cung cấp định nghĩa này: <c>default</c>, <c>company</c>, <c>project</c>.</summary>
        [JsonIgnore]
        public string Layer { get; set; } = "default";
    }

    public sealed class EquipmentParam
    {
        [JsonRequired]
        public string Name { get; set; } = string.Empty;

        public string Unit { get; set; } = "mm";

        /// <summary><c>footprint.length</c>, <c>footprint.width</c>, <c>footprint.diameter</c>, <c>tag</c>, <c>attribute</c>, <c>default</c>.</summary>
        public string From { get; set; } = "default";

        /// <summary>Với <c>tag</c>: biểu thức chính quy có nhóm <c>v</c> áp lên chữ gần thiết bị. Với <c>attribute</c>: thẻ thuộc tính block.</summary>
        public string? Pattern { get; set; }

        public double? Default { get; set; }
    }

    public sealed class BodyShape
    {
        /// <summary><c>cylinder</c>, <c>box</c>, <c>cone</c>, <c>dished_head</c>, <c>sphere</c>.</summary>
        [JsonRequired]
        public string Shape { get; set; } = string.Empty;

        /// <summary><c>x</c>, <c>y</c>, <c>z</c>. Mặc định z.</summary>
        public string Axis { get; set; } = "z";

        public Expr Diameter { get; set; }

        /// <summary>Đường kính đầu nhỏ của nón. Mặc định 0.</summary>
        public Expr TopDiameter { get; set; }

        public Expr Length { get; set; }

        /// <summary>Kích thước hộp theo X, Y, Z cục bộ.</summary>
        public List<Expr>? Size { get; set; }

        public List<Expr>? Center { get; set; }
    }

    public sealed class NozzleDefinition
    {
        [JsonRequired]
        public string Name { get; set; } = string.Empty;

        public List<Expr> At { get; set; } = new List<Expr>();

        /// <summary><c>+x</c>, <c>-x</c>, <c>+y</c>, <c>-y</c>, <c>+z</c>, <c>-z</c>.</summary>
        public string Direction { get; set; } = "+z";

        public int? Dn { get; set; }
    }

    public sealed class EquipmentLibraryError
    {
        public EquipmentLibraryError(string source, string path, string message)
        {
            Source = source;
            Path = path;
            Message = message;
        }

        public string Source { get; }

        public string Path { get; }

        public string Message { get; }

        public override string ToString() => $"{Source} {Path}: {Message}";
    }

    /// <summary>
    /// Thư viện thiết bị, S4-02. Ba lớp mặc định, công ty, dự án: lớp sau ghi đè lớp trước theo <c>id</c>.
    /// Lõi không đọc tệp, ứng dụng chủ truyền văn bản JSON từng định nghĩa.
    /// </summary>
    public sealed class EquipmentLibrary
    {
        public const string GenericBox = "generic_box";
        public static readonly string[] Shapes = { "cylinder", "box", "cone", "dished_head", "sphere" };
        public static readonly string[] ParamSources = { "footprint.length", "footprint.width", "footprint.diameter", "tag", "attribute", "default" };

        private static readonly JsonSerializerOptions Options = IrJson.CreateOptions(indented: true);
        private readonly Dictionary<string, EquipmentDefinition> _definitions = new Dictionary<string, EquipmentDefinition>(StringComparer.OrdinalIgnoreCase);

        public List<EquipmentLibraryError> Errors { get; } = new List<EquipmentLibraryError>();

        public IReadOnlyCollection<EquipmentDefinition> Definitions => _definitions.Values.OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase).ToList();

        public EquipmentDefinition? Find(string id) => id != null && _definitions.TryGetValue(id, out var d) ? d : null;

        public bool Has(string id) => Find(id) != null;

        /// <summary>Thêm một định nghĩa từ JSON. Sai lược đồ thì ghi lỗi và không thêm. Trả về định nghĩa đã thêm hoặc null.</summary>
        public EquipmentDefinition? AddJson(string json, string layer, string source)
        {
            EquipmentDefinition? def;
            try
            {
                def = JsonSerializer.Deserialize<EquipmentDefinition>(json, Options);
            }
            catch (JsonException ex)
            {
                Errors.Add(new EquipmentLibraryError(source, ex.Path ?? "$", ex.Message.Split('.')[0] + "."));
                return null;
            }

            if (def == null)
            {
                Errors.Add(new EquipmentLibraryError(source, "$", "Định nghĩa rỗng."));
                return null;
            }

            var errors = Validate(def);
            foreach (var (path, message) in errors) Errors.Add(new EquipmentLibraryError(source, path, message));
            if (errors.Count > 0) return null;
            def.Layer = layer;
            _definitions[def.Id] = def;
            return def;
        }

        public static List<(string Path, string Message)> Validate(EquipmentDefinition def)
        {
            var errors = new List<(string, string)>();
            if (def.SchemaVersion != "1.0") errors.Add(("$.schemaVersion", $"Phiên bản '{def.SchemaVersion}' không đọc được."));
            if (string.IsNullOrWhiteSpace(def.Id)) errors.Add(("$.id", "Thiếu định danh."));
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < def.Params.Count; i++)
            {
                var p = def.Params[i];
                if (string.IsNullOrWhiteSpace(p.Name)) errors.Add(($"$.params[{i}].name", "Thiếu tên tham số."));
                else if (!names.Add(p.Name)) errors.Add(($"$.params[{i}].name", $"Tham số '{p.Name}' bị trùng."));
                if (Array.IndexOf(ParamSources, p.From) < 0) errors.Add(($"$.params[{i}].from", $"Nguồn '{p.From}' không hỗ trợ."));
                if ((p.From == "tag" || p.From == "attribute") && string.IsNullOrEmpty(p.Pattern)) errors.Add(($"$.params[{i}].pattern", "Nguồn tag và attribute cần pattern."));
                if (p.From == "default" && !p.Default.HasValue) errors.Add(($"$.params[{i}].default", "Nguồn default cần giá trị default."));
                if (p.From == "tag" && p.Pattern != null)
                {
                    try
                    {
                        var rx = new System.Text.RegularExpressions.Regex(p.Pattern);
                        if (Array.IndexOf(rx.GetGroupNames(), "v") < 0) errors.Add(($"$.params[{i}].pattern", "Mẫu phải có nhóm đặt tên 'v'."));
                    }
                    catch (ArgumentException ex)
                    {
                        errors.Add(($"$.params[{i}].pattern", "Biểu thức chính quy sai: " + ex.Message));
                    }
                }
            }

            void CheckExpr(Expr e, string path, bool required)
            {
                if (e.IsEmpty)
                {
                    if (required) errors.Add((path, "Thiếu giá trị."));
                    return;
                }

                try
                {
                    foreach (var n in Expression.Validate(e.Text))
                    {
                        if (!names.Contains(n)) errors.Add((path, $"Tham số '{n}' chưa khai báo trong params."));
                    }
                }
                catch (ExpressionException ex)
                {
                    errors.Add((path, ex.Message));
                }
            }

            for (var i = 0; i < def.Body.Count; i++)
            {
                var b = def.Body[i];
                var path = $"$.body[{i}]";
                if (Array.IndexOf(Shapes, b.Shape) < 0) errors.Add((path + ".shape", $"Khối '{b.Shape}' không hỗ trợ. Dùng {string.Join(", ", Shapes)}."));
                if (b.Axis != "x" && b.Axis != "y" && b.Axis != "z") errors.Add((path + ".axis", "Trục phải là x, y hoặc z."));
                switch (b.Shape)
                {
                    case "cylinder":
                    case "cone":
                        CheckExpr(b.Diameter, path + ".diameter", true);
                        CheckExpr(b.Length, path + ".length", true);
                        CheckExpr(b.TopDiameter, path + ".topDiameter", false);
                        break;
                    case "sphere":
                    case "dished_head":
                        CheckExpr(b.Diameter, path + ".diameter", true);
                        CheckExpr(b.Length, path + ".length", false);
                        break;
                    case "box":
                        if (b.Size == null || b.Size.Count != 3) errors.Add((path + ".size", "Hộp cần size ba thành phần."));
                        else for (var k = 0; k < 3; k++) CheckExpr(b.Size[k], $"{path}.size[{k}]", true);
                        break;
                }

                if (b.Center != null)
                {
                    if (b.Center.Count != 3) errors.Add((path + ".center", "Tâm cần ba thành phần."));
                    else for (var k = 0; k < 3; k++) CheckExpr(b.Center[k], $"{path}.center[{k}]", true);
                }
            }

            for (var i = 0; i < def.Nozzles.Count; i++)
            {
                var n = def.Nozzles[i];
                var path = $"$.nozzles[{i}]";
                if (n.At.Count != 3) errors.Add((path + ".at", "Vị trí cần ba thành phần."));
                else for (var k = 0; k < 3; k++) CheckExpr(n.At[k], $"{path}.at[{k}]", true);
                if (Array.IndexOf(new[] { "+x", "-x", "+y", "-y", "+z", "-z" }, n.Direction) < 0) errors.Add((path + ".direction", "Hướng phải là ±x, ±y, ±z."));
            }

            return errors;
        }
    }
}
