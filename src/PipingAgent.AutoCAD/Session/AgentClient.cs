using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using PipingAgent.Hosting;
using PipingAgent.Tools;

namespace PipingAgent.AutoCAD.Session
{
    /// <summary>
    /// Đầu plug-in của giao thức nội bộ, S7-06: khởi chạy Agent Host thành tiến trình riêng, kết nối named pipe, gửi phiên,
    /// nhận <c>tool.call</c> và chạy công cụ ngay trên luồng đang chờ, tức luồng chính của AutoCAD, có giới hạn thời gian.
    /// Agent Host chết thì phiên lỗi, AutoCAD không treo vì mọi chờ đợi đều có hạn.
    /// </summary>
    public sealed class AgentClient : IDisposable
    {
        private readonly Process _process;
        private readonly NamedPipeClientStream _pipe;
        private readonly JsonRpcPeer _peer;

        private AgentClient(Process process, NamedPipeClientStream pipe, JsonRpcPeer peer)
        {
            _process = process;
            _pipe = pipe;
            _peer = peer;
        }

        public int ProcessId => _process.Id;

        public bool IsAlive => !_process.HasExited && _pipe.IsConnected;

        /// <summary>Đường dẫn tệp thực thi Agent Host: cấu hình, hoặc thư mục <c>agenthost</c> cạnh plug-in.</summary>
        public static string ResolveExe(AgentConfig config)
        {
            if (!string.IsNullOrEmpty(config.HostExePath) && File.Exists(config.HostExePath)) return config.HostExePath!;
            return Path.Combine(ProfileStore.PluginDirectory, "agenthost", "PipingAgent.AgentHost.exe");
        }

        public static AgentClient Start(AgentConfig config, string? drawingPath, TimeSpan connectTimeout)
        {
            var exe = ResolveExe(config);
            if (!File.Exists(exe)) throw new FileNotFoundException("Không thấy Agent Host. Dựng lại plug-in hoặc đặt hostExePath trong agent.json.", exe);
            var pipeName = "PipingAgent." + Guid.NewGuid().ToString("N");
            var args = $"--pipe {pipeName} --parent-pid {Process.GetCurrentProcess().Id}";
            if (!string.IsNullOrEmpty(drawingPath)) args += $" --drawing \"{drawingPath}\"";
            var process = Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
            }) ?? throw new InvalidOperationException("Không khởi chạy được Agent Host.");

            var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            var deadline = DateTime.UtcNow + connectTimeout;
            while (true)
            {
                try
                {
                    pipe.Connect(500);
                    break;
                }
                catch (TimeoutException)
                {
                    if (process.HasExited)
                    {
                        var err = process.StandardError.ReadToEnd();
                        throw new InvalidOperationException($"Agent Host thoát với mã {process.ExitCode}. {err}".Trim());
                    }

                    if (DateTime.UtcNow > deadline) { TryKill(process); throw new TimeoutException("Agent Host không mở kênh trong thời hạn."); }
                }
            }

            var peer = new JsonRpcPeer(pipe);
            return new AgentClient(process, pipe, peer);
        }

        /// <summary>Chạy một phiên hỏi đáp. Công cụ được chạy bằng <paramref name="registry"/> trên <paramref name="context"/> ngay trên luồng này.</summary>
        public JsonElement RunSession(string question, string mode, string? sessionContext, ToolRegistry registry, ToolContext context, TimeSpan timeout, Action<string>? onStep, Action<string, JsonElement, long>? onToolCall)
        {
            _peer.RequestHandler = (method, parameters) =>
            {
                if (method != "tool.call") throw new RpcException(-32601, "Không có phương thức " + method);
                var name = parameters?.GetProperty("name").GetString() ?? string.Empty;
                var args = parameters.HasValue && parameters.Value.TryGetProperty("args", out var a) ? a : JsonDocument.Parse("{}").RootElement;
                var sw = Stopwatch.StartNew();
                var result = registry.Invoke(name, args, context);
                onToolCall?.Invoke(name, args, sw.ElapsedMilliseconds);
                return JsonRpcPeer.ToElement(new { result = JsonDocument.Parse(result.ToModelText()).RootElement });
            };
            _peer.NotificationHandler = (method, parameters) =>
            {
                if (method == "event.step" && onStep != null)
                {
                    var tool = parameters.HasValue && parameters.Value.TryGetProperty("tool", out var t) ? t.GetString() : "?";
                    onStep(tool ?? "?");
                }
            };
            var reply = _peer.RequestAndPump("session.start", new { mode, question, context = sessionContext }, timeout);
            return reply.Result ?? JsonDocument.Parse("{}").RootElement;
        }

        public JsonElement Ping(TimeSpan timeout) => _peer.RequestAndPump("host.ping", null, timeout).Result ?? JsonDocument.Parse("{}").RootElement;

        public void Dispose()
        {
            try { _peer.Notify("host.shutdown", null); } catch (Exception) { }
            try { _peer.Dispose(); _pipe.Dispose(); } catch (Exception) { }
            if (!_process.WaitForExit(1500)) TryKill(_process);
            _process.Dispose();
        }

        private static void TryKill(Process p)
        {
            try { if (!p.HasExited) p.Kill(); } catch (Exception) { }
        }
    }
}
