using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Ir.Json;

namespace PipingAgent.Core.Decisions
{
    /// <summary>
    /// Tệp phiên đi kèm cạnh bản vẽ, S5-06 và FR-CFG-01: IR gần nhất, quyết định, hồ sơ đã dùng, dấu vân tay bản vẽ.
    /// Ứng dụng chủ ghi sau mỗi thay đổi. Lõi chỉ tuần tự hoá, không đọc tệp.
    /// </summary>
    public sealed class SessionFile
    {
        public const string Extension = ".pa.json";

        [JsonRequired]
        public string SchemaVersion { get; set; } = "1.0";

        public string? Drawing { get; set; }

        /// <summary>Dấu vân tay bản vẽ lúc lưu. Khác lúc mở nghĩa là bản vẽ đã đổi, quyết định vẫn áp theo handle.</summary>
        public string? Fingerprint { get; set; }

        public string? ProfileId { get; set; }

        public int? ProfileVersion { get; set; }

        public string? ProfilePath { get; set; }

        public string SavedAt { get; set; } = string.Empty;

        public List<Decision> Decisions { get; set; } = new List<Decision>();

        /// <summary>IR gần nhất, để xem lại mà không cần chạy lại. Có thể bỏ qua khi lưu để tệp nhỏ.</summary>
        public PipingModel? Model { get; set; }

        private static readonly JsonSerializerOptions Options = IrJson.CreateOptions(indented: true);

        public static string Serialize(SessionFile session) => JsonSerializer.Serialize(session, Options);

        public static SessionFile Deserialize(string json)
        {
            var s = JsonSerializer.Deserialize<SessionFile>(json, Options) ?? throw new IrFormatException("Tệp phiên rỗng.");
            if (s.SchemaVersion != "1.0") throw new IrFormatException($"Phiên bản tệp phiên '{s.SchemaVersion}' không đọc được.");
            return s;
        }

        /// <summary>Đường dẫn tệp phiên của một bản vẽ: cùng thư mục, thêm đuôi <c>.pa.json</c>.</summary>
        public static string PathFor(string drawingPath) => drawingPath + Extension;

        public static string Now() => DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture);

        /// <summary>Tạo một quyết định của người dùng với định danh và thời điểm.</summary>
        public static Decision NewDecision(DecisionOp op, Dictionary<string, string> target, JsonElement? value, string? rationale = null, DecidedBy by = DecidedBy.User)
        {
            return new Decision
            {
                Id = "dec-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Op = op,
                Target = target,
                Value = value,
                By = by,
                Status = by == DecidedBy.User ? DecisionStatus.Approved : DecisionStatus.Pending,
                Rationale = rationale,
                At = Now(),
            };
        }
    }
}
