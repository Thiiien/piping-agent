using System.IO;
using Autodesk.AutoCAD.Runtime;
using PipingAgent.AutoCAD.Session;

namespace PipingAgent.AutoCAD.Commands
{
    /// <summary>
    /// Lệnh PA_MCP: Kiểm tra trạng thái máy chủ Live CAD Bridge, hiển thị thông tin bản vẽ và kênh Named Pipe.
    /// </summary>
    public sealed class McpBridgeCommand : CommandBase
    {
        protected override string Name => "PA_MCP";

        [CommandMethod("PA_MCP")]
        public void McpStatus() => Execute();

        protected override void Run(CommandContext context)
        {
            var bridge = PluginApp.LiveBridge;
            var isRunning = bridge != null && bridge.IsRunning;
            var doc = context.Document;
            var session = PluginApp.Sessions?.GetOrCreate(doc);

            context.Write("\n[Piping Agent Live CAD Bridge]");
            context.Write($"  Trạng thái: {(isRunning ? "ĐANG LẮNG NGHE" : "ĐÃ TẮT")}");
            context.Write($"  Kênh Named Pipe: \\\\.\\pipe\\{LiveCadBridgeServer.PipeName}");
            context.Write($"  Bản vẽ hiện tại: {Path.GetFileName(doc.Name)}");
            context.Write($"  Số đối tượng trong bộ nhớ: {session?.LastEntities?.Count ?? 0}");
            context.Write($"  Mô hình IR: {(session?.LastResult?.Model != null ? "Sẵn sàng" : "Chưa quét (sẽ tự động đọc khi nhận truy vấn MCP)")}");
            context.Write($"  Hồ sơ quy ước: {session?.Profile?.Id ?? "Mặc định"}");
            context.Write("\n-> Máy chủ MCP (PipingAgent.Mcp) và Hermes Agent đã có thể kết nối trực tiếp để hỏi đáp về bản vẽ này.\n");
        }
    }
}
