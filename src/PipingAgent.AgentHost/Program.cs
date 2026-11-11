using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using PipingAgent.AgentHost;
using PipingAgent.Hosting;
using PipingAgent.Tools;

// Agent Host, S7-04: tiến trình nền chạy vòng lặp agent. Plug-in khởi chạy nó với tên kênh và mã tiến trình cha,
// kết nối qua named pipe chỉ người dùng hiện tại mở được, gửi session.start và trả lời tool.call. Khi kênh đóng hoặc
// tiến trình cha chết thì tự thoát. Khoá truy cập mô hình không bao giờ được in hay ghi nhật ký.
//
// Dùng: PipingAgent.AgentHost --pipe <tên> [--parent-pid <pid>] [--drawing <đường dẫn>] [--idle-sec <giây>]
//       PipingAgent.AgentHost --selftest "<câu hỏi>" [--drawing <dxf>]    chạy một lượt không cần plug-in

Console.OutputEncoding = Encoding.UTF8;
string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PipingAgent", "logs", "agent-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".jsonl");
Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
var logLock = new object();
void Log(object record)
{
    var line = JsonSerializer.Serialize(new { ts = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture), pid = Environment.ProcessId, record }, ToolJson.Options);
    lock (logLock) File.AppendAllText(logPath, line + "\n");
}

var config = AgentConfig.Load(Arg("--drawing"));
if (!config.Enabled)
{
    Console.Error.WriteLine("Agent đang tắt trong cấu hình (provider=off). " + config.Describe());
    return 3;
}

ILlmProvider provider = new OpenAiCompatibleProvider(config.Endpoint!, config.Model!, config.ResolveApiKey(), config.MaxOutputTokens ?? 4000, config.Temperature ?? 0, config.TimeoutSec ?? 180);
var registry = ReadOnlyTools.Register(new ToolRegistry());
Log(new { ev = "host_start", config = config.Describe(), tools = registry.Tools.Count });

var selfTest = Arg("--selftest");
if (selfTest != null)
{
    // Chế độ tự thử: công cụ chạy ngay trong tiến trình trên một tệp DXF, không cần AutoCAD. Dùng cho kiểm thử tích hợp.
    var context = new ToolContext();
    var drawing = Arg("--drawing");
    if (drawing != null)
    {
        var reader = PipingAgent.Dxf.DxfDrawingReader.Open(drawing);
        context.Entities = reader.ReadEntities(PipingAgent.Core.Source.EntityFilter.All);
        context.Summary = reader.GetSummary();
        context.Catalog = CatalogFiles.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "catalog"));
    }

    var loop = new AgentLoop(provider, registry, (name, a) => Task.FromResult(registry.Invoke(name, a, context).ToModelText()), Log, (m, p) => Console.Error.WriteLine($"  [{m}] {JsonSerializer.Serialize(p, ToolJson.Options)}"));
    var r = await loop.RunAsync(new SessionRequest { Question = selfTest }, config.MaxSteps ?? 20, CancellationToken.None);
    Console.WriteLine(r.Answer);
    Console.WriteLine($"[{r.StopReason}, {r.Steps} bước, {r.ToolCalls} lệnh gọi công cụ, {r.InputTokens}+{r.OutputTokens} token, cache {r.CachedTokens}, {r.ElapsedSec} s, {r.Model}]");
    return r.StopReason == "answer" ? 0 : 1;
}

var pipeName = Arg("--pipe");
if (pipeName == null)
{
    Console.Error.WriteLine("Thiếu --pipe. " + config.Describe());
    return 2;
}

var parentPid = int.TryParse(Arg("--parent-pid"), out var pp) ? pp : (int?)null;
var idleSec = int.TryParse(Arg("--idle-sec"), out var idle) ? idle : 600;

// Chỉ người dùng hiện tại có quyền đọc ghi kênh; không có quyền cho Everyone hay người dùng khác.
var security = new PipeSecurity();
var me = WindowsIdentity.GetCurrent().User!;
security.AddAccessRule(new PipeAccessRule(me, PipeAccessRights.FullControl, AccessControlType.Allow));
using var server = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 1 << 16, 1 << 16, security);

using var cts = new CancellationTokenSource();
if (parentPid.HasValue)
{
    _ = Task.Run(async () =>
    {
        while (!cts.IsCancellationRequested)
        {
            try { Process.GetProcessById(parentPid.Value); } catch (ArgumentException) { Log(new { ev = "parent_gone" }); cts.Cancel(); break; }
            await Task.Delay(2000);
        }
    });
}

var waitConnect = server.WaitForConnectionAsync(cts.Token);
if (await Task.WhenAny(waitConnect, Task.Delay(TimeSpan.FromSeconds(30), cts.Token)) != waitConnect)
{
    Log(new { ev = "no_client" });
    return 4;
}

await waitConnect;
Log(new { ev = "client_connected", pipe = pipeName });
using var peer = new JsonRpcPeer(server);
var sessionLock = new SemaphoreSlim(1, 1);
var lastActivity = DateTime.UtcNow;

peer.AsyncRequestHandler = async (method, parameters) =>
{
    lastActivity = DateTime.UtcNow;
    switch (method)
    {
        case "host.ping":
            return JsonRpcPeer.ToElement(new { ok = true, model = provider.Model, tools = registry.Tools.Select(t => t.Name) });
        case "tools.list":
            return JsonRpcPeer.ToElement(registry.Tools.Select(t => new { t.Name, t.Description, tier = t.Tier.ToString(), schema = t.InputSchema }));
        case "session.start":
            {
                var request = JsonRpcPeer.FromElement<SessionRequest>(parameters) ?? new SessionRequest();
                if (!await sessionLock.WaitAsync(0)) throw new RpcException(-32001, "Đang có phiên khác chạy.");
                try
                {
                    Log(new { ev = "session_start", request.Mode, request.Question });
                    var loop = new AgentLoop(provider, registry, async (name, a) =>
                    {
                        var reply = await peer.RequestAsync("tool.call", new { name, args = a }).ConfigureAwait(false);
                        if (reply.Error != null) return ToolResult.Fail(reply.Error.Message).ToModelText();
                        return reply.Result.HasValue && reply.Result.Value.TryGetProperty("result", out var r) ? r.GetRawText() : reply.Result?.GetRawText() ?? "{}";
                    }, Log, peer.Notify);
                    var result = await loop.RunAsync(request, request.MaxSteps ?? config.MaxSteps ?? 20, cts.Token).ConfigureAwait(false);
                    Log(new { ev = "session_end", result.StopReason, result.Steps, result.ToolCalls, result.InputTokens, result.OutputTokens, result.ElapsedSec });
                    return JsonRpcPeer.ToElement(result);
                }
                finally
                {
                    sessionLock.Release();
                }
            }

        case "host.shutdown":
            cts.CancelAfter(200);
            return JsonRpcPeer.ToElement(new { ok = true });
        default:
            throw new RpcException(-32601, "Không có phương thức " + method);
    }
};

var run = peer.RunAsync(cts.Token);
while (!run.IsCompleted)
{
    await Task.WhenAny(run, Task.Delay(1000));
    if (cts.IsCancellationRequested) break;
    if ((DateTime.UtcNow - lastActivity).TotalSeconds > idleSec)
    {
        Log(new { ev = "idle_exit" });
        break;
    }
}

Log(new { ev = "host_exit" });
return 0;
