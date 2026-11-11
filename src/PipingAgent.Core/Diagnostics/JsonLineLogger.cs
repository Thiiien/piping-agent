using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace PipingAgent.Core.Diagnostics
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// Nhật ký có cấu trúc: mỗi dòng một bản ghi JSON, có mã phiên. NFR-MNT-06, mục 11.5.
    /// Lõi không mở tệp. Ứng dụng chủ đưa <see cref="TextWriter"/> vào, thường là tệp <c>.jsonl</c> trong thư mục dữ liệu ứng dụng.
    /// Không bao giờ ghi nội dung bản vẽ, chỉ ghi số đếm, mã, thời gian. Mục 11.2.
    /// </summary>
    public sealed class JsonLineLogger : IDisposable
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly TextWriter _writer;
        private readonly bool _ownsWriter;
        private readonly object _gate = new object();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public JsonLineLogger(TextWriter writer, string host, string hostVersion, bool ownsWriter = true)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _ownsWriter = ownsWriter;
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            MinimumLevel = LogLevel.Info;
            Write(LogLevel.Info, "session_start", new Dictionary<string, object?>
            {
                ["host"] = host,
                ["hostVersion"] = hostVersion,
                ["coreVersion"] = typeof(JsonLineLogger).Assembly.GetName().Version?.ToString(),
                ["irSchema"] = CoreInfo.IrSchemaVersion,
            });
        }

        /// <summary>Mã phiên, mỗi lần nạp plug-in một mã. Dùng để lọc nhật ký khi hỗ trợ.</summary>
        public string SessionId { get; }

        public LogLevel MinimumLevel { get; set; }

        public void Info(string eventName, IDictionary<string, object?>? data = null) => Write(LogLevel.Info, eventName, data);

        public void Warning(string eventName, IDictionary<string, object?>? data = null) => Write(LogLevel.Warning, eventName, data);

        public void Error(string eventName, Exception exception, IDictionary<string, object?>? data = null)
        {
            var payload = data ?? new Dictionary<string, object?>();
            payload["exceptionType"] = exception.GetType().FullName;
            payload["exceptionMessage"] = exception.Message;
            payload["stackTrace"] = exception.StackTrace;
            Write(LogLevel.Error, eventName, payload);
        }

        public void Write(LogLevel level, string eventName, IDictionary<string, object?>? data)
        {
            if (level < MinimumLevel) return;
            var record = new Dictionary<string, object?>
            {
                ["ts"] = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
                ["session"] = SessionId,
                ["level"] = level.ToString().ToLowerInvariant(),
                ["event"] = eventName,
            };
            if (data != null)
            {
                foreach (var pair in data) record[pair.Key] = pair.Value;
            }

            var line = JsonSerializer.Serialize(record, Options);
            lock (_gate)
            {
                _writer.WriteLine(line);
                _writer.Flush();
            }
        }

        public void Dispose()
        {
            Write(LogLevel.Info, "session_end", new Dictionary<string, object?> { ["uptimeMs"] = Math.Round(_clock.Elapsed.TotalMilliseconds, 1) });
            if (_ownsWriter) _writer.Dispose();
        }
    }
}
