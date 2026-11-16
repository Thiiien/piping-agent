using System;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PipingAgent.Hosting;

namespace PipingAgent.Mcp
{
    /// <summary>
    /// Bộ kết nối Live CAD Bridge, S21-01 / ADR-09:
    /// Cho phép MCP Server kết nối tới phiên AutoCAD đang mở qua Named Pipe "piping-agent-live-cad".
    /// </summary>
    public static class LiveCadClient
    {
        public const string DefaultPipeName = "piping-agent-live-cad";

        /// <summary>Kiểm tra nhanh xem tiến trình AutoCAD và Live CAD Bridge có đang chạy không.</summary>
        public static async Task<bool> IsAvailableAsync(string pipeName = DefaultPipeName, int timeoutMs = 300)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(timeoutMs, cts.Token).ConfigureAwait(false);
                return client.IsConnected;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Lấy thông tin trạng thái bản vẽ đang mở trong AutoCAD.</summary>
        public static async Task<JsonElement?> PingAsync(string pipeName = DefaultPipeName, int timeoutMs = 1500)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    client.Connect(timeoutMs);

                    using var peer = new JsonRpcPeer(client);
                    var reply = peer.RequestAndPump("ping", null, TimeSpan.FromMilliseconds(timeoutMs));
                    return reply.Result;
                }
                catch
                {
                    return null;
                }
            }).ConfigureAwait(false);
        }

        /// <summary>Gửi lệnh gọi công cụ tới AutoCAD để thực thi trên bản vẽ đang mở.</summary>
        public static async Task<(bool Success, string Result, string? Error)> CallToolAsync(string pipeName, string toolName, JsonElement args, int timeoutMs = 30000)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    client.Connect(Math.Min(1000, timeoutMs));

                    using var peer = new JsonRpcPeer(client);
                    var reply = peer.RequestAndPump("tool.call", new { name = toolName, args }, TimeSpan.FromMilliseconds(timeoutMs));

                    if (reply.Error != null)
                    {
                        return (Success: false, Result: string.Empty, Error: (string?)reply.Error.Message);
                    }

                    if (reply.Result.HasValue && reply.Result.Value.TryGetProperty("result", out var r))
                    {
                        return (Success: true, Result: r.GetRawText(), Error: (string?)null);
                    }

                    return (Success: true, Result: reply.Result?.GetRawText() ?? "{}", Error: (string?)null);
                }
                catch (RpcException ex)
                {
                    return (Success: false, Result: string.Empty, Error: (string?)ex.Message);
                }
                catch (TimeoutException)
                {
                    return (Success: false, Result: string.Empty, Error: (string?)"Kết nối tới AutoCAD bị quá thời gian chờ.");
                }
                catch (Exception ex)
                {
                    return (Success: false, Result: string.Empty, Error: (string?)$"Lỗi kết nối Live CAD: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }
    }
}
