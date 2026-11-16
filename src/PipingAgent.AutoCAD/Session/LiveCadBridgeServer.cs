using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Source;
using PipingAgent.Hosting;
using PipingAgent.Tools;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace PipingAgent.AutoCAD.Session
{
    /// <summary>
    /// Máy chủ Live CAD Bridge, S21-01 / ADR-09 / ADR-11:
    /// Lắng nghe trên Named Pipe "piping-agent-live-cad" để cho phép MCP Server (PipingAgent.Mcp)
    /// hoặc các công cụ AI bên ngoài truy vấn trực tiếp bản vẽ đang mở trong AutoCAD theo thời gian thực.
    /// </summary>
    public sealed class LiveCadBridgeServer : IDisposable
    {
        public const string PipeName = "piping-agent-live-cad";

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task? _serverLoopTask;
        private bool _disposed;

        public bool IsRunning => _serverLoopTask != null && !_serverLoopTask.IsCompleted && !_cts.IsCancellationRequested;

        public void Start()
        {
            if (_serverLoopTask != null) return;
            _serverLoopTask = Task.Run(RunServerLoopAsync);
        }

        private async Task RunServerLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var pipe = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);

                    // Xử lý từng kết nối trên một tác vụ riêng để không chặn các yêu cầu tiếp theo
                    _ = Task.Run(async () =>
                    {
                        using (pipe)
                        using (var peer = new JsonRpcPeer(pipe))
                        {
                            peer.AsyncRequestHandler = HandleRequestAsync;
                            try
                            {
                                await peer.RunAsync(_cts.Token).ConfigureAwait(false);
                            }
                            catch (Exception)
                            {
                                // Kết nối ngắt bình thường khi client đóng
                            }
                        }
                    }, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    PluginApp.Log?.Error("live_cad_bridge_loop_error", ex, null);
                    try
                    {
                        await Task.Delay(1000, _cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private Task<JsonElement?> HandleRequestAsync(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "ping":
                    return Task.FromResult<JsonElement?>(GetPingInfo());

                case "drawing.info":
                    return Task.FromResult<JsonElement?>(GetDrawingInfo());

                case "tool.call":
                    return Task.FromResult<JsonElement?>(ExecuteTool(parameters));

                default:
                    throw new RpcException(-32601, "Không có phương thức " + method);
            }
        }

        private JsonElement GetPingInfo()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return JsonRpcPeer.ToElement(new
                {
                    connected = true,
                    hasDocument = false,
                    pipe = PipeName
                });
            }

            var session = PluginApp.Sessions?.GetOrCreate(doc);
            return JsonRpcPeer.ToElement(new
            {
                connected = true,
                hasDocument = true,
                pipe = PipeName,
                drawing = doc.Name,
                drawingName = Path.GetFileName(doc.Name),
                entityCount = session?.LastEntities?.Count ?? 0,
                isScanned = session?.LastEntities != null,
                hasModel = session?.LastResult?.Model != null
            });
        }

        private JsonElement GetDrawingInfo()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                throw new RpcException(-32002, "Không có bản vẽ nào đang mở trong AutoCAD.");
            }

            var session = PluginApp.Sessions?.GetOrCreate(doc);
            return JsonRpcPeer.ToElement(new
            {
                drawing = doc.Name,
                drawingName = Path.GetFileName(doc.Name),
                entityCount = session?.LastEntities?.Count ?? 0,
                isScanned = session?.LastEntities != null,
                profile = session?.Profile?.Id,
                issuesCount = session?.LastResult?.Model?.Issues?.Count ?? 0,
                runsCount = session?.LastResult?.Model?.Runs?.Count ?? 0
            });
        }

        private JsonElement ExecuteTool(JsonElement? parameters)
        {
            var name = parameters?.GetProperty("name").GetString() ?? string.Empty;
            var args = parameters.HasValue && parameters.Value.TryGetProperty("args", out var a) ? a : JsonDocument.Parse("{}").RootElement;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                throw new RpcException(-32002, "Không có bản vẽ nào đang mở trong AutoCAD.");
            }

            var sessions = PluginApp.Sessions;
            if (sessions == null)
            {
                throw new RpcException(-32003, "Hệ thống phiên chưa sẵn sàng.");
            }

            var session = sessions.GetOrCreate(doc);

            // Tự động đọc đối tượng nếu bản vẽ chưa được quét
            if (session.LastEntities == null)
            {
                try
                {
                    using (doc.LockDocument())
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        var reader = new AcadDrawingReader(doc.Database, tr);
                        var entities = reader.ReadEntities(EntityFilter.All);
                        var summary = reader.GetSummary();
                        if (session.Profile == null && session.ProfilePath != null) session.LoadProfile();

                        var result = session.Profile != null
                            ? RecognitionPipeline.Run(entities, session.Profile, sessions.Catalog)
                            : new RecognitionResult();

                        session.StoreScan(entities, result, reader.ScaleToMm, summary.Fingerprint);
                        tr.Commit();
                    }
                }
                catch (Exception ex)
                {
                    PluginApp.Log?.Error("live_bridge_autoscan_error", ex, null);
                }
            }

            var config = Hosting.AgentConfig.Load(doc.Name);
            var allowAction = config.AllowAction == true;

            var toolContext = new ToolContext
            {
                Entities = session.LastEntities ?? new List<SourceEntity>(),
                Summary = session.LastEntities != null ? session.LastEntities.Summarize() : null,
                Model = session.LastResult?.Model,
                Catalog = sessions.Catalog,
                Profile = session.Profile,
                AllowAction = allowAction,
                ApplyMutationExecutor = p => session.ApplyMutation(p),
                DocumentPath = doc.Name,
                DocumentFingerprint = session.LastResult?.Model?.Source?.Fingerprint,
            };

            foreach (var kvp in session.Proposals)
            {
                toolContext.Proposals[kvp.Key] = kvp.Value;
            }

            var registry = ReadOnlyTools.Register(new ToolRegistry());
            ProfileDraftTools.Register(registry);
            ActionTools.Register(registry);

            var toolResult = registry.Invoke(name, args, toolContext);

            foreach (var kvp in toolContext.Proposals)
            {
                session.Proposals[kvp.Key] = kvp.Value;
            }

            return JsonRpcPeer.ToElement(new { result = JsonDocument.Parse(toolResult.ToModelText()).RootElement });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }
}
