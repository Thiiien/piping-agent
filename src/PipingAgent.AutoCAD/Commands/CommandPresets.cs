using System;
using System.Collections.Generic;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>
    /// Câu trả lời đặt sẵn cho câu hỏi đầu tiên của một lệnh, do nút ribbon đặt ngay trước khi gửi lệnh. Lệnh lấy một lần
    /// rồi xoá, nên gõ tay vẫn được hỏi như cũ. Không gửi từ khoá qua dòng lệnh vì khi lệnh dừng sớm, từ khoá thừa sẽ thành
    /// "Unknown command".
    /// </summary>
    public static class CommandPresets
    {
        private static readonly Dictionary<string, string> Pending = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void Set(string command, string? value)
        {
            if (string.IsNullOrEmpty(value)) Pending.Remove(command);
            else Pending[command] = value!;
        }

        public static string? Take(string command)
        {
            if (!Pending.TryGetValue(command, out var v)) return null;
            Pending.Remove(command);
            return v;
        }
    }
}
