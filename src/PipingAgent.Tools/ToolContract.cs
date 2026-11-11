using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PipingAgent.Tools
{
    /// <summary>Lớp quyền của công cụ, mục 8.2. Agent Host chỉ nạp nhóm phù hợp với chế độ đang chạy.</summary>
    public enum ToolTier
    {
        ReadOnly,
        Propose,
        Interact,
    }

    /// <summary>Kết quả một lệnh gọi công cụ. Lỗi cũng là kết quả, có cờ và hướng dẫn để agent tự sửa, mục 8.3.</summary>
    public sealed class ToolResult
    {
        public bool Ok { get; set; } = true;

        /// <summary>Nội dung JSON trả về cho mô hình.</summary>
        public JsonElement Content { get; set; }

        public string? Error { get; set; }

        /// <summary>Cách gọi đúng khi lỗi.</summary>
        public string? Hint { get; set; }

        /// <summary>Đúng khi kết quả bị cắt bớt cho vừa giới hạn kích thước.</summary>
        public bool Truncated { get; set; }

        public static ToolResult Success(object content) => new ToolResult { Ok = true, Content = JsonSerializer.SerializeToElement(content, ToolJson.Options) };

        public static ToolResult Fail(string error, string? hint = null) => new ToolResult { Ok = false, Error = error, Hint = hint, Content = JsonSerializer.SerializeToElement(new { error, hint }, ToolJson.Options) };

        /// <summary>Dạng gửi cho mô hình: một đối tượng JSON gọn, có cờ lỗi.</summary>
        public string ToModelText()
        {
            if (Ok) return Content.GetRawText();
            return JsonSerializer.Serialize(new { error = Error, hint = Hint }, ToolJson.Options);
        }
    }

    public static class ToolJson
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        /// <summary>Giới hạn kích thước kết quả gửi cho mô hình, ký tự. Kết quả lớn hơn phải phân trang.</summary>
        public const int MaxResultChars = 24000;
    }

    /// <summary>Định nghĩa một công cụ: tên, mô tả, lược đồ vào ở chế độ chặt, lớp quyền, hàm xử lý.</summary>
    public sealed class ToolDefinition
    {
        public ToolDefinition(string name, string description, ToolTier tier, string inputSchemaJson, Func<JsonElement, ToolContext, ToolResult> handler)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Tier = tier;
            InputSchema = JsonDocument.Parse(inputSchemaJson).RootElement.Clone();
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            if (!System.Text.RegularExpressions.Regex.IsMatch(Name, "^[a-z][a-z0-9_]{2,63}$")) throw new ArgumentException($"Tên công cụ '{Name}' phải là snake_case.");
            var errors = ToolSchema.ValidateSchemaShape(InputSchema);
            if (errors.Count > 0) throw new ArgumentException($"Lược đồ của '{Name}' không ở chế độ chặt: {string.Join("; ", errors)}");
        }

        public string Name { get; }

        public string Description { get; }

        public ToolTier Tier { get; }

        public JsonElement InputSchema { get; }

        public Func<JsonElement, ToolContext, ToolResult> Handler { get; }

        /// <summary>Kiểm tra tham số rồi chạy. Lỗi lược đồ hoặc ngoại lệ đều thành kết quả lỗi, không ném ra ngoài.</summary>
        public ToolResult Invoke(JsonElement input, ToolContext context)
        {
            var errors = ToolSchema.Validate(InputSchema, input, "$");
            if (errors.Count > 0)
            {
                return ToolResult.Fail("Tham số không đúng lược đồ: " + string.Join("; ", errors), "Gọi lại " + Name + " với đúng các trường: " + ToolSchema.Describe(InputSchema));
            }

            try
            {
                var result = Handler(input, context);
                return Clamp(result);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Công cụ {Name} gặp lỗi: {ex.Message}", "Kiểm tra lại định danh và giá trị đã truyền.");
            }
        }

        private static ToolResult Clamp(ToolResult r)
        {
            if (!r.Ok) return r;
            var text = r.Content.GetRawText();
            if (text.Length <= ToolJson.MaxResultChars) return r;
            r.Truncated = true;
            r.Content = JsonSerializer.SerializeToElement(new
            {
                truncated = true,
                note = "Kết quả quá dài, chỉ giữ phần đầu. Hãy gọi lại với bộ lọc hẹp hơn hoặc phân trang.",
                head = text.Substring(0, ToolJson.MaxResultChars),
            }, ToolJson.Options);
            return r;
        }

        /// <summary>Mô tả công cụ theo dạng hàm của API kiểu OpenAI, dùng chung cho mọi nhà cung cấp hỗ trợ gọi hàm.</summary>
        public object ToFunctionSpec() => new
        {
            type = "function",
            function = new { name = Name, description = Description, parameters = InputSchema, strict = true },
        };
    }

    /// <summary>Sổ đăng ký công cụ. Thứ tự đăng ký cố định để định nghĩa gửi cho mô hình ổn định, mục 8.5.</summary>
    public sealed class ToolRegistry
    {
        private readonly List<ToolDefinition> _tools = new List<ToolDefinition>();

        public IReadOnlyList<ToolDefinition> Tools => _tools;

        public ToolRegistry Add(ToolDefinition tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            if (_tools.Any(t => t.Name == tool.Name)) throw new ArgumentException($"Công cụ '{tool.Name}' đã đăng ký.");
            _tools.Add(tool);
            return this;
        }

        public ToolDefinition? Find(string name) => _tools.FirstOrDefault(t => t.Name == name);

        public IEnumerable<ToolDefinition> ForTiers(params ToolTier[] tiers) => _tools.Where(t => tiers.Contains(t.Tier));

        public ToolResult Invoke(string name, JsonElement input, ToolContext context)
        {
            var tool = Find(name);
            if (tool == null)
            {
                return ToolResult.Fail($"Không có công cụ '{name}'.", "Các công cụ có: " + string.Join(", ", _tools.Select(t => t.Name)));
            }

            return tool.Invoke(input, context);
        }
    }

    /// <summary>
    /// Bộ kiểm tra lược đồ JSON tối giản cho tham số công cụ: object, string, number, integer, boolean, array, enum, required,
    /// additionalProperties false, minimum, maximum, minItems, maxItems, nullable qua mảng type. Đủ cho chế độ chặt của mục 8.3,
    /// viết riêng để lớp công cụ không tham chiếu thư viện ngoài.
    /// </summary>
    public static class ToolSchema
    {
        public static List<string> ValidateSchemaShape(JsonElement schema)
        {
            var errors = new List<string>();
            if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out var t) || t.GetString() != "object")
            {
                errors.Add("gốc phải là object");
                return errors;
            }

            if (!schema.TryGetProperty("additionalProperties", out var ap) || ap.ValueKind != JsonValueKind.False) errors.Add("thiếu additionalProperties: false");
            var props = schema.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object ? p.EnumerateObject().Select(x => x.Name).ToList() : new List<string>();
            var required = schema.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.Array ? r.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList() : new List<string>();
            foreach (var name in props.Where(n => !required.Contains(n))) errors.Add($"trường '{name}' phải có trong required, trường tuỳ chọn khai báo type gồm null");
            return errors;
        }

        public static List<string> Validate(JsonElement schema, JsonElement value, string path)
        {
            var errors = new List<string>();
            Check(schema, value, path, errors);
            return errors;
        }

        private static void Check(JsonElement schema, JsonElement value, string path, List<string> errors)
        {
            var types = Types(schema);
            if (types.Count > 0 && !types.Any(t => Matches(t, value)))
            {
                errors.Add($"{path} phải là {string.Join(" hoặc ", types)}, nhận {value.ValueKind.ToString().ToLowerInvariant()}");
                return;
            }

            if (value.ValueKind == JsonValueKind.Null) return;
            if (schema.TryGetProperty("enum", out var en) && en.ValueKind == JsonValueKind.Array)
            {
                var allowed = en.EnumerateArray().Select(e => e.ToString()).ToList();
                if (!allowed.Contains(value.ToString())) errors.Add($"{path} phải là một trong {string.Join(", ", allowed)}");
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                var d = value.GetDouble();
                if (schema.TryGetProperty("minimum", out var min) && d < min.GetDouble()) errors.Add($"{path} phải >= {min.GetDouble().ToString(CultureInfo.InvariantCulture)}");
                if (schema.TryGetProperty("maximum", out var max) && d > max.GetDouble()) errors.Add($"{path} phải <= {max.GetDouble().ToString(CultureInfo.InvariantCulture)}");
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                var n = value.GetArrayLength();
                if (schema.TryGetProperty("minItems", out var mi) && n < mi.GetInt32()) errors.Add($"{path} cần ít nhất {mi.GetInt32()} phần tử");
                if (schema.TryGetProperty("maxItems", out var ma) && n > ma.GetInt32()) errors.Add($"{path} tối đa {ma.GetInt32()} phần tử");
                if (schema.TryGetProperty("items", out var items))
                {
                    var i = 0;
                    foreach (var item in value.EnumerateArray()) Check(items, item, $"{path}[{i++}]", errors);
                }
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                var props = schema.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object ? p : default;
                if (schema.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in req.EnumerateArray())
                    {
                        if (!value.TryGetProperty(r.GetString() ?? string.Empty, out _)) errors.Add($"{path} thiếu trường bắt buộc '{r.GetString()}'");
                    }
                }

                foreach (var prop in value.EnumerateObject())
                {
                    if (props.ValueKind == JsonValueKind.Object && props.TryGetProperty(prop.Name, out var sub)) Check(sub, prop.Value, $"{path}.{prop.Name}", errors);
                    else errors.Add($"{path} có trường lạ '{prop.Name}'");
                }
            }
        }

        private static List<string> Types(JsonElement schema)
        {
            if (!schema.TryGetProperty("type", out var t)) return new List<string>();
            if (t.ValueKind == JsonValueKind.String) return new List<string> { t.GetString()! };
            if (t.ValueKind == JsonValueKind.Array) return t.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList();
            return new List<string>();
        }

        private static bool Matches(string type, JsonElement v)
        {
            switch (type)
            {
                case "string": return v.ValueKind == JsonValueKind.String;
                case "number": return v.ValueKind == JsonValueKind.Number;
                case "integer": return v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out _);
                case "boolean": return v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False;
                case "array": return v.ValueKind == JsonValueKind.Array;
                case "object": return v.ValueKind == JsonValueKind.Object;
                case "null": return v.ValueKind == JsonValueKind.Null;
                default: return false;
            }
        }

        public static string Describe(JsonElement schema)
        {
            if (!schema.TryGetProperty("properties", out var p) || p.ValueKind != JsonValueKind.Object) return "không có tham số";
            return string.Join(", ", p.EnumerateObject().Select(x => x.Name + ":" + string.Join("|", Types(x.Value))));
        }
    }
}
