using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using PipingAgent.Core.Geometry;

namespace PipingAgent.Core.Ir.Json
{
    /// <summary>
    /// Tuần tự hoá Piping IR ra JSON và đọc lại. FR-EXP-03, NFR-CMP-06.
    /// Quy ước: khoá camelCase, liệt kê dạng snake_case, điểm và véc-tơ là mảng số, bỏ các trường null.
    /// Lõi không mở tệp, chỉ làm việc với chuỗi và Stream do ứng dụng chủ đưa vào.
    /// </summary>
    public static class IrJson
    {
        /// <summary>Các phiên bản lược đồ mà bộ đọc này hiểu. Phiên bản cũ vẫn đọc được theo NFR-CMP-06.</summary>
        public static readonly string[] ReadableSchemaVersions = { "1.0" };

        private static readonly JsonSerializerOptions WriteOptions = CreateOptions(indented: true);
        private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);

        public static JsonSerializerOptions CreateOptions(bool indented)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = indented,
                NumberHandling = JsonNumberHandling.Strict,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            options.Converters.Add(new SnakeCaseEnumConverter());
            options.Converters.Add(new Point2Converter());
            options.Converters.Add(new Point3Converter());
            options.Converters.Add(new Vector2Converter());
            options.Converters.Add(new Vector3Converter());
            options.Converters.Add(new Box2Converter());
            return options;
        }

        public static string Serialize(PipingModel model, bool indented = true)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            return JsonSerializer.Serialize(model, indented ? WriteOptions : CompactOptions);
        }

        /// <summary>Đọc IR. Ném <see cref="IrFormatException"/> khi JSON sai cú pháp hoặc phiên bản lược đồ không đọc được.</summary>
        public static PipingModel Deserialize(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            PipingModel? model;
            try
            {
                model = JsonSerializer.Deserialize<PipingModel>(json, WriteOptions);
            }
            catch (JsonException exception)
            {
                throw new IrFormatException("JSON của IR không hợp lệ: " + exception.Message, exception);
            }

            return CheckVersion(model);
        }

        private static PipingModel CheckVersion(PipingModel? model)
        {
            if (model == null) throw new IrFormatException("JSON của IR rỗng.");
            if (string.IsNullOrEmpty(model.SchemaVersion))
            {
                throw new IrFormatException("Thiếu trường schemaVersion.");
            }

            if (Array.IndexOf(ReadableSchemaVersions, model.SchemaVersion) < 0)
            {
                throw new IrFormatException(
                    $"Phiên bản lược đồ {model.SchemaVersion} không đọc được. Bản này đọc: {string.Join(", ", ReadableSchemaVersions)}.");
            }

            return model;
        }
    }

    /// <summary>
    /// Liệt kê ghi dạng snake_case, ví dụ <c>open_end</c>. Bộ chuyển trong Options đứng trước thuộc tính trên kiểu,
    /// nên phải nhường các liệt kê có bộ chuyển riêng như <see cref="ElevationDatum"/>, vốn ghi nguyên tên <c>BOP</c>.
    /// </summary>
    public sealed class SnakeCaseEnumConverter : JsonConverterFactory
    {
        private readonly JsonStringEnumConverter _inner = new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower);

        public override bool CanConvert(Type typeToConvert)
        {
            if (!_inner.CanConvert(typeToConvert)) return false;
            var target = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
            return !target.IsDefined(typeof(JsonConverterAttribute), inherit: false);
        }

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            _inner.CreateConverter(typeToConvert, options);
    }

    public sealed class IrFormatException : Exception
    {
        public IrFormatException(string message) : base(message)
        {
        }

        public IrFormatException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
