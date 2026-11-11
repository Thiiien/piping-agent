using System;
using System.Globalization;
using System.Text;

namespace PipingAgent.Core.Source
{
    /// <summary>
    /// Bỏ mã định dạng của chữ AutoCAD để còn lại nội dung. Dùng cho TEXT lẫn MTEXT.
    /// Bản vẽ thật dùng nhiều <c>%%U</c>, <c>%%C</c>, <c>%%%</c>, xem KHAO_SAT_BAN_VE_THAT.md mục 3.6.
    /// </summary>
    public static class CadText
    {
        public static string Strip(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var s = raw!;
            var sb = new StringBuilder(s.Length);
            var i = 0;
            while (i < s.Length)
            {
                var c = s[i];

                // Mã %% của TEXT.
                if (c == '%' && i + 2 < s.Length && s[i + 1] == '%')
                {
                    var code = char.ToUpperInvariant(s[i + 2]);
                    switch (code)
                    {
                        case 'U': // gạch chân, bật tắt
                        case 'O': // gạch trên, bật tắt
                            i += 3; continue;
                        case 'D': sb.Append('°'); i += 3; continue;
                        case 'C': sb.Append('Ø'); i += 3; continue;
                        case 'P': sb.Append('±'); i += 3; continue;
                        case '%': sb.Append('%'); i += 3; continue;
                    }

                    // %%nnn: mã ký tự ba chữ số.
                    if (i + 4 < s.Length && char.IsDigit(s[i + 2]) && char.IsDigit(s[i + 3]) && char.IsDigit(s[i + 4]))
                    {
                        var number = int.Parse(s.Substring(i + 2, 3), CultureInfo.InvariantCulture);
                        sb.Append((char)number);
                        i += 5;
                        continue;
                    }

                    sb.Append(c);
                    i++;
                    continue;
                }

                // Mã \ của MTEXT.
                if (c == '\\' && i + 1 < s.Length)
                {
                    var code = s[i + 1];
                    switch (code)
                    {
                        case 'P': sb.Append('\n'); i += 2; continue;      // xuống dòng
                        case '~': sb.Append(' '); i += 2; continue;       // khoảng trắng không ngắt
                        case '\\': sb.Append('\\'); i += 2; continue;
                        case '{': sb.Append('{'); i += 2; continue;
                        case '}': sb.Append('}'); i += 2; continue;
                        case 'L': case 'l': case 'O': case 'o': case 'K': case 'k':
                            i += 2; continue;                              // gạch chân, gạch trên, gạch ngang
                        case 'U':
                            if (i + 6 < s.Length && s[i + 2] == '+')
                            {
                                // \U+XXXX
                                var hex = s.Substring(i + 3, 4);
                                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cp))
                                {
                                    sb.Append((char)cp);
                                    i += 7;
                                    continue;
                                }
                            }

                            break;
                        case 'A': case 'C': case 'c': case 'F': case 'f': case 'H': case 'Q': case 'T': case 'W': case 'p':
                        case 'S':
                            {
                                // Mã có tham số kết thúc bằng dấu chấm phẩy. Với \S (phân số xếp chồng) giữ nội dung.
                                var end = s.IndexOf(';', i + 2);
                                if (end < 0) { i += 2; continue; }
                                if (code == 'S')
                                {
                                    var body = s.Substring(i + 2, end - i - 2).Replace('^', '/').Replace('#', '/');
                                    sb.Append(body);
                                }

                                i = end + 1;
                                continue;
                            }
                    }

                    sb.Append(c);
                    i++;
                    continue;
                }

                if (c == '{' || c == '}')
                {
                    i++;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString().Trim();
        }

        /// <summary>Thu gọn khoảng trắng và đưa về một dòng, dùng khi so khớp mẫu nhãn.</summary>
        public static string Normalize(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text!.Length);
            var pendingSpace = false;
            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = sb.Length > 0;
                    continue;
                }

                if (pendingSpace) sb.Append(' ');
                pendingSpace = false;
                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
