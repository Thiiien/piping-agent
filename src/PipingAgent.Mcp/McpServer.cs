using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Source;
using PipingAgent.Dxf;
using PipingAgent.Hosting;
using PipingAgent.Tools;

namespace PipingAgent.Mcp
{
    /// <summary>
    /// Xử lý giao thức Model Context Protocol (MCP) qua kênh JSON-RPC stdio.
    /// Hỗ trợ cả 2 chế độ: Live CAD Bridge (khi AutoCAD đang mở) và Offline file (qua tệp DXF/DWG).
    /// </summary>
    public sealed class McpServer
    {
        private readonly ToolRegistry _registry;
        private readonly ToolContext _localContext;
        private readonly string _pipeName;
        private readonly bool _allowDraft;
        private readonly bool _allowAction;

        public McpServer(string? drawingPath = null, string pipeName = LiveCadClient.DefaultPipeName, bool allowDraft = false, bool allowAction = false)
        {
            _pipeName = pipeName;
            _allowDraft = allowDraft;
            _allowAction = allowAction;
            _registry = ReadOnlyTools.Register(new ToolRegistry());
            if (_allowDraft)
            {
                ProfileDraftTools.Register(_registry);
            }
            if (_allowAction)
            {
                ActionTools.Register(_registry);
            }

            _localContext = new ToolContext { AllowAction = _allowAction };
            var catalogDir = Path.Combine(AppContext.BaseDirectory, "catalog");
            if (Directory.Exists(catalogDir))
            {
                _localContext.Catalog = CatalogFiles.LoadDirectory(catalogDir);
            }

            if (!string.IsNullOrEmpty(drawingPath) && File.Exists(drawingPath))
            {
                try
                {
                    var reader = DxfDrawingReader.Open(drawingPath);
                    _localContext.Entities = reader.ReadEntities(EntityFilter.All);
                    _localContext.Summary = reader.GetSummary();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[PipingAgent.Mcp] Cảnh báo: không đọc được tệp {drawingPath}: {ex.Message}");
                }
            }
        }

        public async Task<JsonElement?> HandleRequestAsync(string method, JsonElement? parameters)
        {
            switch (method)
            {
                case "initialize":
                    return HandleInitialize(parameters);

                case "notifications/initialized":
                    return null;

                case "tools/list":
                    return HandleToolsList();

                case "tools/call":
                    return await HandleToolsCallAsync(parameters).ConfigureAwait(false);

                case "ping":
                    return JsonRpcPeer.ToElement(new { });

                case "prompts/list":
                    return JsonRpcPeer.ToElement(new { prompts = Array.Empty<object>() });

                case "resources/list":
                    return JsonRpcPeer.ToElement(new { resources = Array.Empty<object>() });

                default:
                    throw new RpcException(-32601, "Không có phương thức MCP: " + method);
            }
        }

        private static JsonElement HandleInitialize(JsonElement? parameters)
        {
            return JsonRpcPeer.ToElement(new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { }
                },
                serverInfo = new
                {
                    name = "piping-agent-mcp",
                    version = "0.1.0"
                }
            });
        }

        private JsonElement HandleToolsList()
        {
            var tools = _registry.Tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                inputSchema = t.InputSchema
            }).ToList();

            return JsonRpcPeer.ToElement(new { tools });
        }

        private async Task<JsonElement> HandleToolsCallAsync(JsonElement? parameters)
        {
            if (parameters == null || !parameters.Value.TryGetProperty("name", out var nameProp))
            {
                throw new RpcException(-32602, "Thiếu tham số 'name' của công cụ.");
            }

            var toolName = nameProp.GetString() ?? string.Empty;
            var args = parameters.Value.TryGetProperty("arguments", out var a) ? a : JsonDocument.Parse("{}").RootElement;

            // 1. Ưu tiên thử gọi qua Live CAD Bridge nếu AutoCAD đang mở
            if (await LiveCadClient.IsAvailableAsync(_pipeName).ConfigureAwait(false))
            {
                var (success, result, error) = await LiveCadClient.CallToolAsync(_pipeName, toolName, args).ConfigureAwait(false);
                if (success)
                {
                    return MakeMcpResult(result, isError: false);
                }
                return MakeMcpResult(JsonSerializer.Serialize(new { error }), isError: true);
            }

            // 2. Chế độ ngoại tuyến (Offline Mode)
            var tool = _registry.Tools.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
            if (tool == null)
            {
                return MakeMcpResult(JsonSerializer.Serialize(new { error = $"Không tìm thấy công cụ '{toolName}' trong máy chủ MCP." }), isError: true);
            }

            if (_localContext.Entities.Count == 0 && toolName != "catalog_lookup")
            {
                var msg = "Không kết nối được phiên AutoCAD đang mở (Live CAD Bridge chưa bật), và chưa có tệp bản vẽ DXF nào được nạp qua --drawing. Hãy mở AutoCAD có bản vẽ hoặc khởi chạy máy chủ MCP kèm --drawing <đường dẫn DXF>.";
                return MakeMcpResult(JsonSerializer.Serialize(new { error = msg }), isError: true);
            }

            var res = tool.Invoke(args, _localContext);
            return MakeMcpResult(res.ToModelText(), isError: !res.Ok);
        }

        private static JsonElement MakeMcpResult(string text, bool isError)
        {
            return JsonRpcPeer.ToElement(new
            {
                content = new[]
                {
                    new { type = "text", text }
                },
                isError
            });
        }
    }
}
