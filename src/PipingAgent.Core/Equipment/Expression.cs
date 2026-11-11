using System;
using System.Collections.Generic;
using System.Globalization;

namespace PipingAgent.Core.Equipment
{
    public sealed class ExpressionException : Exception
    {
        public ExpressionException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Bộ tính biểu thức cho thư viện thiết bị, S4-03: bốn phép tính, dấu ngoặc, dấu âm, số và tên tham số.
    /// Phân tích đệ quy, không gọi mã ngoài, nên tệp định nghĩa do khách hàng viết không thể thực thi lệnh trên máy.
    /// </summary>
    public static class Expression
    {
        public static double Evaluate(string text, IReadOnlyDictionary<string, double> parameters)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            var p = new Parser(text, parameters);
            var value = p.ParseSum();
            p.SkipSpaces();
            if (!p.AtEnd) throw new ExpressionException($"Ký tự thừa tại vị trí {p.Position + 1} trong '{text}'.");
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ExpressionException($"Biểu thức '{text}' cho kết quả không hữu hạn.");
            return value;
        }

        /// <summary>Kiểm tra cú pháp và tên tham số mà không cần giá trị. Trả về danh sách tên tham số đã dùng.</summary>
        public static List<string> Validate(string text)
        {
            var names = new List<string>();
            var p = new Parser(text ?? string.Empty, new Dictionary<string, double>(), names);
            p.ParseSum();
            p.SkipSpaces();
            if (!p.AtEnd) throw new ExpressionException($"Ký tự thừa tại vị trí {p.Position + 1} trong '{text}'.");
            return names;
        }

        private sealed class Parser
        {
            private readonly string _s;
            private readonly IReadOnlyDictionary<string, double> _params;
            private readonly List<string>? _collect;

            public Parser(string s, IReadOnlyDictionary<string, double> parameters, List<string>? collect = null)
            {
                _s = s;
                _params = parameters;
                _collect = collect;
            }

            public int Position { get; private set; }

            public bool AtEnd => Position >= _s.Length;

            public void SkipSpaces()
            {
                while (!AtEnd && char.IsWhiteSpace(_s[Position])) Position++;
            }

            public double ParseSum()
            {
                var value = ParseProduct();
                while (true)
                {
                    SkipSpaces();
                    if (AtEnd) return value;
                    var c = _s[Position];
                    if (c == '+') { Position++; value += ParseProduct(); }
                    else if (c == '-') { Position++; value -= ParseProduct(); }
                    else return value;
                }
            }

            private double ParseProduct()
            {
                var value = ParseUnary();
                while (true)
                {
                    SkipSpaces();
                    if (AtEnd) return value;
                    var c = _s[Position];
                    if (c == '*') { Position++; value *= ParseUnary(); }
                    else if (c == '/')
                    {
                        Position++;
                        var d = ParseUnary();
                        if (d == 0 && _collect == null) throw new ExpressionException($"Chia cho không trong '{_s}'.");
                        value = d == 0 ? 0 : value / d;
                    }
                    else return value;
                }
            }

            private double ParseUnary()
            {
                SkipSpaces();
                if (!AtEnd && _s[Position] == '-')
                {
                    Position++;
                    return -ParseUnary();
                }

                if (!AtEnd && _s[Position] == '+')
                {
                    Position++;
                    return ParseUnary();
                }

                return ParseAtom();
            }

            private double ParseAtom()
            {
                SkipSpaces();
                if (AtEnd) throw new ExpressionException($"Biểu thức '{_s}' kết thúc đột ngột.");
                var c = _s[Position];
                if (c == '(')
                {
                    Position++;
                    var v = ParseSum();
                    SkipSpaces();
                    if (AtEnd || _s[Position] != ')') throw new ExpressionException($"Thiếu dấu đóng ngoặc trong '{_s}'.");
                    Position++;
                    return v;
                }

                if (char.IsDigit(c) || c == '.')
                {
                    var start = Position;
                    while (!AtEnd && (char.IsDigit(_s[Position]) || _s[Position] == '.')) Position++;
                    var token = _s.Substring(start, Position - start);
                    if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                    {
                        throw new ExpressionException($"Số '{token}' không hợp lệ trong '{_s}'.");
                    }

                    return n;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    var start = Position;
                    while (!AtEnd && (char.IsLetterOrDigit(_s[Position]) || _s[Position] == '_' || _s[Position] == '.')) Position++;
                    var name = _s.Substring(start, Position - start);
                    if (_collect != null)
                    {
                        if (!_collect.Contains(name)) _collect.Add(name);
                        return 1;
                    }

                    if (!_params.TryGetValue(name, out var value)) throw new ExpressionException($"Tham số '{name}' không tồn tại trong '{_s}'.");
                    return value;
                }

                throw new ExpressionException($"Ký tự '{c}' không hợp lệ tại vị trí {Position + 1} trong '{_s}'.");
            }
        }
    }
}
