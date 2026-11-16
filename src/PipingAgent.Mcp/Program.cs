using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PipingAgent.Hosting;
using PipingAgent.Mcp;
using PipingAgent.Tools;

// Thiết lập mã hoá UTF-8 không có BOM cho stdio để tránh làm hỏng luồng JSON của client MCP
Console.OutputEncoding = new UTF8Encoding(false);
Console.InputEncoding = Encoding.UTF8;

string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

bool HasFlag(string name) => Array.IndexOf(args, name) >= 0;

var drawingPath = Arg("--drawing");
var pipeName = Arg("--pipe") ?? LiveCadClient.DefaultPipeName;
var allowDraft = HasFlag("--allow-draft");
var allowAction = HasFlag("--allow-action");

Console.Error.WriteLine($"[PipingAgent.Mcp] Khởi động máy chủ MCP (kênh live pipe: {pipeName}, allowDraft: {allowDraft}, allowAction: {allowAction})");

var server = new McpServer(drawingPath, pipeName, allowDraft, allowAction);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var reader = Console.In;
var writer = Console.Out;

while (!cts.IsCancellationRequested)
{
    string? line;
    try
    {
        line = await reader.ReadLineAsync().ConfigureAwait(false);
    }
    catch
    {
        break;
    }

    if (line == null)
    {
        // EOF trên stdin khi client MCP đóng kết nối
        break;
    }

    if (string.IsNullOrWhiteSpace(line)) continue;
    line = line.TrimStart('\uFEFF');

    try
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
        var hasId = root.TryGetProperty("id", out var idProp);
        var id = hasId ? idProp.Clone() : (JsonElement?)null;
        var parameters = root.TryGetProperty("params", out var p) ? p.Clone() : (JsonElement?)null;

        // Xử lý thông báo (Notification): không có id, không cần gửi phản hồi
        if (!hasId)
        {
            if (method != null)
            {
                try
                {
                    await server.HandleRequestAsync(method, parameters).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[PipingAgent.Mcp] Lỗi xử lý notification '{method}': {ex.Message}");
                }
            }
            continue;
        }

        // Xử lý yêu cầu (Request): có id, bắt buộc gửi phản hồi
        try
        {
            var result = await server.HandleRequestAsync(method ?? string.Empty, parameters).ConfigureAwait(false);
            var responseObj = new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = result
            };
            var responseLine = JsonSerializer.Serialize(responseObj, ToolJson.Options);
            await writer.WriteLineAsync(responseLine).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            var errObj = new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new { code = ex.Code, message = ex.Message }
            };
            var errLine = JsonSerializer.Serialize(errObj, ToolJson.Options);
            await writer.WriteLineAsync(errLine).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var errObj = new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new { code = -32603, message = $"Lỗi nội bộ: {ex.Message}" }
            };
            var errLine = JsonSerializer.Serialize(errObj, ToolJson.Options);
            await writer.WriteLineAsync(errLine).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }
    }
    catch (JsonException ex)
    {
        Console.Error.WriteLine($"[PipingAgent.Mcp] JSON không hợp lệ: {ex.Message}");
        var errObj = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = null,
            ["error"] = new { code = -32700, message = "Lỗi phân tích cú pháp JSON: " + ex.Message }
        };
        var errLine = JsonSerializer.Serialize(errObj, ToolJson.Options);
        await writer.WriteLineAsync(errLine).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }
}

Console.Error.WriteLine("[PipingAgent.Mcp] Máy chủ dừng bình thường.");
return 0;
