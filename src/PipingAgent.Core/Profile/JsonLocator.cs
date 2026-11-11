using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PipingAgent.Core.Profile
{
    /// <summary>
    /// Tìm số dòng của một đường dẫn JSON dạng <c>$.roles[0].match.layer</c> trong văn bản gốc,
    /// để báo lỗi hồ sơ chỉ rõ dòng và trường (S2-01). Chỉ dùng khi báo lỗi nên không cần nhanh.
    /// </summary>
    public sealed class JsonLocator
    {
        private readonly Dictionary<string, (int Line, int Column)> _positions = new Dictionary<string, (int, int)>(StringComparer.Ordinal);

        public JsonLocator(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var bytes = Encoding.UTF8.GetBytes(json);
            var lineStarts = LineStarts(bytes);
            try
            {
                Index(bytes, lineStarts);
            }
            catch (JsonException)
            {
                // Văn bản sai cú pháp: giữ những gì đã đánh chỉ mục được, lỗi cú pháp đã có dòng riêng.
            }
        }

        /// <summary>Dòng và cột, bắt đầu từ 1, của giá trị tại đường dẫn. Null nếu không có.</summary>
        public (int Line, int Column)? Find(string path)
        {
            if (path == null) return null;
            while (path.Length > 0)
            {
                if (_positions.TryGetValue(path, out var p)) return p;
                var cut = Math.Max(path.LastIndexOf('.'), path.LastIndexOf('['));
                if (cut <= 0) break;
                path = path.Substring(0, cut);
            }

            return _positions.TryGetValue("$", out var root) ? root : ((int, int)?)null;
        }

        private static List<int> LineStarts(byte[] bytes)
        {
            var starts = new List<int> { 0 };
            for (var i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == (byte)'\n') starts.Add(i + 1);
            }

            return starts;
        }

        private static (int Line, int Column) Position(List<int> lineStarts, long offset)
        {
            var lo = 0;
            var hi = lineStarts.Count - 1;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (lineStarts[mid] <= offset) lo = mid;
                else hi = mid - 1;
            }

            return (lo + 1, (int)(offset - lineStarts[lo]) + 1);
        }

        private void Index(byte[] bytes, List<int> lineStarts)
        {
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            var path = new List<string> { "$" };
            var arrayIndex = new Stack<int>();
            var inArray = new Stack<bool>();
            string? pendingName = null;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        pendingName = reader.GetString();
                        break;
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        Enter(path, arrayIndex, inArray, ref pendingName, reader.TokenStartIndex, lineStarts);
                        inArray.Push(reader.TokenType == JsonTokenType.StartArray);
                        arrayIndex.Push(0);
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        inArray.Pop();
                        arrayIndex.Pop();
                        if (path.Count > 1) path.RemoveAt(path.Count - 1);
                        break;
                    default:
                        Enter(path, arrayIndex, inArray, ref pendingName, reader.TokenStartIndex, lineStarts);
                        if (path.Count > 1) path.RemoveAt(path.Count - 1);
                        break;
                }
            }
        }

        private void Enter(List<string> path, Stack<int> arrayIndex, Stack<bool> inArray, ref string? pendingName, long offset, List<int> lineStarts)
        {
            if (inArray.Count == 0)
            {
                _positions["$"] = Position(lineStarts, offset);
                return;
            }

            string segment;
            if (inArray.Peek())
            {
                var i = arrayIndex.Pop();
                arrayIndex.Push(i + 1);
                segment = "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
            }
            else
            {
                segment = "." + (pendingName ?? string.Empty);
                pendingName = null;
            }

            path.Add(segment);
            _positions[string.Concat(path)] = Position(lineStarts, offset);
        }
    }
}
