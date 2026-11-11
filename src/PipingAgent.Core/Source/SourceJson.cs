using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PipingAgent.Core.Ir.Json;

namespace PipingAgent.Core.Source
{
    /// <summary>Gói đối tượng nguồn đã đọc từ một bản vẽ, để lưu, so sánh hai bộ đọc, hoặc đưa cho agent.</summary>
    public sealed class SourceDump
    {
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>Tên bộ đọc, ví dụ <c>dxf</c> hoặc <c>autocad</c>.</summary>
        public string Reader { get; set; } = string.Empty;

        public string? ReadAt { get; set; }

        public DrawingSummary Summary { get; set; } = new DrawingSummary();

        public List<SourceEntity> Entities { get; set; } = new List<SourceEntity>();
    }

    /// <summary>Tuần tự hoá đối tượng nguồn với cùng quy ước JSON như Piping IR.</summary>
    public static class SourceJson
    {
        private static readonly JsonSerializerOptions Options = IrJson.CreateOptions(indented: true);
        private static readonly JsonSerializerOptions Compact = IrJson.CreateOptions(indented: false);

        public static string Serialize(SourceDump dump, bool indented = true) =>
            JsonSerializer.Serialize(dump ?? throw new ArgumentNullException(nameof(dump)), indented ? Options : Compact);

        public static void Serialize(SourceDump dump, Stream output)
        {
            if (dump == null) throw new ArgumentNullException(nameof(dump));
            if (output == null) throw new ArgumentNullException(nameof(output));
            using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
            JsonSerializer.Serialize(writer, dump, Options);
        }

        public static SourceDump Deserialize(string json) =>
            JsonSerializer.Deserialize<SourceDump>(json ?? throw new ArgumentNullException(nameof(json)), Options)
            ?? throw new IrFormatException("JSON đối tượng nguồn rỗng.");

        public static SourceDump Deserialize(Stream input) =>
            JsonSerializer.Deserialize<SourceDump>(input ?? throw new ArgumentNullException(nameof(input)), Options)
            ?? throw new IrFormatException("JSON đối tượng nguồn rỗng.");
    }
}
