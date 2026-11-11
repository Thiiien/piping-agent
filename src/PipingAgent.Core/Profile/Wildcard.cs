using System;
using System.Text;
using System.Text.RegularExpressions;

namespace PipingAgent.Core.Profile
{
    /// <summary>Khớp tên theo ký tự đại diện kiểu AutoCAD: <c>*</c> nhiều ký tự, <c>?</c> một ký tự, <c>#</c> một chữ số. Không phân biệt hoa thường.</summary>
    public sealed class Wildcard
    {
        private readonly Regex? _regex;
        private readonly string _literal;

        public Wildcard(string pattern)
        {
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            if (pattern.IndexOfAny(new[] { '*', '?', '#' }) < 0)
            {
                _literal = pattern;
                _regex = null;
                return;
            }

            _literal = string.Empty;
            var sb = new StringBuilder("^");
            foreach (var c in pattern)
            {
                switch (c)
                {
                    case '*': sb.Append(".*"); break;
                    case '?': sb.Append('.'); break;
                    case '#': sb.Append("[0-9]"); break;
                    default: sb.Append(Regex.Escape(c.ToString())); break;
                }
            }

            sb.Append('$');
            _regex = new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        public string Pattern { get; }

        public bool IsMatch(string? value)
        {
            if (value == null) return false;
            return _regex != null ? _regex.IsMatch(value) : string.Equals(_literal, value, StringComparison.OrdinalIgnoreCase);
        }

        public static bool Match(string pattern, string? value) => new Wildcard(pattern).IsMatch(value);
    }
}
