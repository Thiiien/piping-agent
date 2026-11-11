using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using PipingAgent.AutoCAD.Builder;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>
    /// PA_PORT: chọn một khối hoặc vòng cổng đã dựng, in cổng nối và khối đối diện đọc từ xdata, S4-12. Thay cho bảng
    /// thuộc tính nối của S3D: người xem biết khối này nối với khối nào mà không cần đo.
    /// </summary>
    public sealed class PortCommand : CommandBase
    {
        protected override string Name => "PA_PORT";

        [CommandMethod("PA_PORT")]
        public void Port() => Execute();

        private static string Get(System.Collections.Generic.Dictionary<string, string> d, string key, string fallback) => d.TryGetValue(key, out var v) ? v : fallback;

        protected override void Run(CommandContext context)
        {
            var pick = context.Editor.GetEntity(new PromptEntityOptions("\nChọn khối hoặc vòng cổng của Piping Agent: "));
            if (pick.Status != PromptStatus.OK) return;
            var obj = context.Transaction.GetObject(pick.ObjectId, OpenMode.ForRead);
            var data = AcadBuilder.ReadXData(obj);
            if (data == null)
            {
                context.Write("Piping Agent: đối tượng này không do Piping Agent dựng.");
                return;
            }

            if (data.TryGetValue("port", out var port))
            {
                context.Write($"Piping Agent: cổng {port}, DN{Get(data, "dn", "?")}, nối với {Get(data, "mate", "?")}.");
                return;
            }

            context.Write($"Piping Agent: {Get(data, "kind", "?")} {Get(data, "ir", "?")} tuyến {Get(data, "line", "?")} DN{Get(data, "dn", "?")}.");
            var ports = Get(data, "ports", string.Empty);
            if (ports.Length == 0)
            {
                context.Write("  Không có cổng nối.");
                return;
            }

            foreach (var p in ports.Split('|'))
            {
                var at = p.IndexOf('@');
                var gt = p.IndexOf('>');
                var name = at > 0 ? p.Substring(0, at) : p;
                var pos = gt > at ? p.Substring(at + 1, gt - at - 1) : p.Substring(at + 1);
                var mate = gt > 0 ? p.Substring(gt + 1) : null;
                context.Write(mate != null ? $"  cổng {name} tại ({pos}) nối với {mate}" : $"  cổng {name} tại ({pos}) HỞ");
            }
        }
    }
}
