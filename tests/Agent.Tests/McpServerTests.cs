using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PipingAgent.Mcp;
using Xunit;

namespace Agent.Tests
{
    public class McpServerTests
    {
        [Fact]
        public async Task HandleRequest_Initialize_ReturnsProtocolAndServerInfo()
        {
            var server = new McpServer();
            var response = await server.HandleRequestAsync("initialize", JsonDocument.Parse("{}").RootElement);

            Assert.NotNull(response);
            var root = response.Value;
            Assert.Equal("2024-11-05", root.GetProperty("protocolVersion").GetString());
            Assert.True(root.GetProperty("capabilities").TryGetProperty("tools", out _));
            Assert.Equal("piping-agent-mcp", root.GetProperty("serverInfo").GetProperty("name").GetString());
        }

        [Fact]
        public async Task HandleRequest_ToolsList_ReturnsAllReadOnlyTools()
        {
            var server = new McpServer();
            var response = await server.HandleRequestAsync("tools/list", null);

            Assert.NotNull(response);
            var tools = response.Value.GetProperty("tools");
            Assert.True(tools.GetArrayLength() >= 12);

            var foundCatalog = false;
            var foundSummary = false;
            foreach (var t in tools.EnumerateArray())
            {
                var name = t.GetProperty("name").GetString();
                if (name == "catalog_lookup") foundCatalog = true;
                if (name == "drawing_get_summary") foundSummary = true;
            }
            Assert.True(foundCatalog);
            Assert.True(foundSummary);
        }

        [Fact]
        public async Task HandleRequest_ToolsCall_CatalogLookup_SucceedsOffline()
        {
            var server = new McpServer();
            var callParams = JsonDocument.Parse("{\"name\":\"catalog_lookup\",\"arguments\":{\"item\":\"pipe\",\"dn\":100}}").RootElement;
            var response = await server.HandleRequestAsync("tools/call", callParams);

            Assert.NotNull(response);
            var root = response.Value;
            Assert.False(root.GetProperty("isError").GetBoolean());
            var content = root.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.NotNull(content);
            Assert.Contains("od", content);
        }

        [Fact]
        public async Task HandleRequest_ToolsCall_OfflineWithoutDrawing_ReturnsGuidance()
        {
            // Không truyền tệp DXF và không có Live CAD -> báo lỗi có hướng dẫn rõ ràng
            var server = new McpServer(drawingPath: null, pipeName: "non_existent_pipe_12345");
            var callParams = JsonDocument.Parse("{\"name\":\"drawing_get_summary\",\"arguments\":{}}").RootElement;
            var response = await server.HandleRequestAsync("tools/call", callParams);

            Assert.NotNull(response);
            var root = response.Value;
            Assert.True(root.GetProperty("isError").GetBoolean());
            var text = root.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.NotNull(text);
            Assert.Contains("Live CAD Bridge", text);
        }

        [Fact]
        public async Task HandleRequest_ToolsCall_WithDxfFile_Succeeds()
        {
            var dxfPath = Path.Combine(AppContext.BaseDirectory, "Data", "piping_plan_2d.dxf");
            Assert.True(File.Exists(dxfPath), $"Không tìm thấy tệp {dxfPath}");

            var server = new McpServer(drawingPath: dxfPath, pipeName: "non_existent_pipe_12345");
            var callParams = JsonDocument.Parse("{\"name\":\"drawing_get_summary\",\"arguments\":{}}").RootElement;
            var response = await server.HandleRequestAsync("tools/call", callParams);

            Assert.NotNull(response);
            var root = response.Value;
            Assert.False(root.GetProperty("isError").GetBoolean());
            var text = root.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.NotNull(text);
            Assert.Contains("entityCount", text);
        }

        [Fact]
        public async Task HandleRequest_Ping_ReturnsEmptyObject()
        {
            var server = new McpServer();
            var response = await server.HandleRequestAsync("ping", null);
            Assert.NotNull(response);
        }
    }
}
