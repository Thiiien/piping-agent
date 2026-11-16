using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PipingAgent.AgentHost;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Source;
using PipingAgent.Tools;
using Xunit;

namespace Agent.Tests;

public class AgentLoopTests
{
    private static (ToolRegistry Registry, ToolContext Context) SetupTools()
    {
        var reg = new ToolRegistry();
        ReadOnlyTools.Register(reg);
        ProfileDraftTools.Register(reg);

        var entities = new List<SourceEntity>
        {
            new SourceEntity { Handle = "h1", Layer = "PIPE", Kind = SourceKind.Line, Points = new List<Point3> { new(0, 0, 0), new(1000, 0, 0) } },
            new SourceEntity { Handle = "h2", Layer = "DIMLINES", Kind = SourceKind.Line, Points = new List<Point3> { new(0, 50, 0), new(1000, 50, 0) } },
            new SourceEntity { Handle = "h3", Layer = "ISOTEXT", Kind = SourceKind.Text, Text = "1000", Center = new(500, 60, 0) },
        };

        var context = new ToolContext
        {
            Entities = entities,
            Summary = entities.Summarize(),
        };

        return (reg, context);
    }

    [Fact]
    public async Task RunAsync_DirectAnswer_StopsWithAnswerReason()
    {
        var (reg, context) = SetupTools();
        var mock = new MockLlmProvider();
        mock.EnqueueText("Bản vẽ có 3 layer: PIPE, DIMLINES, ISOTEXT.");

        var logs = new List<object>();
        var loop = new AgentLoop(mock, reg, (name, args) => Task.FromResult(reg.Invoke(name, args, context).ToModelText()), l => logs.Add(l), (_, _) => { });

        var req = new SessionRequest { Question = "Bản vẽ có những layer nào?" };
        var res = await loop.RunAsync(req, 10, CancellationToken.None);

        Assert.Equal("answer", res.StopReason);
        Assert.Contains("Bản vẽ có 3 layer", res.Answer);
        Assert.Equal(1, res.Steps);
        Assert.Equal(0, res.ToolCalls);
    }

    [Fact]
    public async Task RunAsync_ToolCallExecution_ExecutesToolAndAnswers()
    {
        var (reg, context) = SetupTools();
        var mock = new MockLlmProvider();

        // Turn 1: Mô hình gọi drawing_list_layers
        mock.EnqueueToolCall("drawing_list_layers", new { limit = 10 });
        // Turn 2: Mô hình trả lời sau khi nhận kết quả
        mock.EnqueueText("Các layer gồm PIPE, DIMLINES và ISOTEXT.");

        var notifications = new List<string>();
        var loop = new AgentLoop(
            mock,
            reg,
            (name, args) => Task.FromResult(reg.Invoke(name, args, context).ToModelText()),
            _ => { },
            (ev, payload) => notifications.Add(ev));

        var req = new SessionRequest { Question = "Liệt kê các layer" };
        var res = await loop.RunAsync(req, 10, CancellationToken.None);

        Assert.Equal("answer", res.StopReason);
        Assert.Equal(2, res.Steps);
        Assert.Equal(1, res.ToolCalls);
        Assert.Contains("PIPE", res.Answer);
        Assert.Contains("event.step", notifications);
    }

    [Fact]
    public async Task RunAsync_ProfileMode_ExecutesDraftWriteAndTest()
    {
        var (reg, context) = SetupTools();
        var mock = new MockLlmProvider();

        // Turn 1: Khảo sát layers
        mock.EnqueueToolCall("drawing_list_layers", new { limit = 5 });

        // Turn 2: Viết hồ sơ nháp
        var profileJson = "{\"schemaVersion\":\"1.0\",\"id\":\"iso-draft\",\"pipeStandard\":\"ASME-B36.10\",\"roles\":[{\"role\":\"pipe_centerline\",\"match\":{\"layers\":[\"PIPE\"]}}]}";
        mock.EnqueueToolCall("profile_draft_write", new { profile_json = profileJson });

        // Turn 3: Chạy thử hồ sơ nháp
        mock.EnqueueToolCall("profile_draft_test", new { sample_limit = 5 });

        // Turn 4: Kết luận
        mock.EnqueueText("Đã khảo sát và đề xuất hồ sơ iso-draft thành công với tỷ lệ phân loại đạt yêu cầu.");

        var loop = new AgentLoop(
            mock,
            reg,
            (name, args) => Task.FromResult(reg.Invoke(name, args, context).ToModelText()),
            _ => { },
            (_, _) => { });

        var req = new SessionRequest { Mode = "profile", Question = "Đề xuất hồ sơ cho bản vẽ này" };
        var res = await loop.RunAsync(req, 10, CancellationToken.None);

        Assert.Equal("answer", res.StopReason);
        Assert.Equal(4, res.Steps);
        Assert.Equal(3, res.ToolCalls);
        Assert.NotNull(context.DraftProfile);
        Assert.Equal("iso-draft", context.DraftProfile!.Id);
    }

    [Fact]
    public async Task RunAsync_MaxStepsExceeded_StopsGracefully()
    {
        var (reg, context) = SetupTools();
        var mock = new MockLlmProvider();

        // Các lệnh gọi công cụ với đối số khác nhau để không kích hoạt lặp
        for (var i = 0; i < 5; i++)
        {
            mock.EnqueueToolCall("drawing_list_layers", new { limit = i + 1 }, callId: "c" + i);
        }

        var loop = new AgentLoop(
            mock,
            reg,
            (name, args) => Task.FromResult(reg.Invoke(name, args, context).ToModelText()),
            _ => { },
            (_, _) => { });

        var req = new SessionRequest { Question = "Nhiều bước" };
        var res = await loop.RunAsync(req, 3, CancellationToken.None);

        Assert.Equal("max_steps", res.StopReason);
        Assert.Equal(3, res.Steps);
    }

    [Fact]
    public async Task RunAsync_RepeatedToolCall_StopsWithRepeatedReason()
    {
        var (reg, context) = SetupTools();
        var mock = new MockLlmProvider();

        // 3 lần gọi cùng một công cụ với cùng đối số
        for (var i = 0; i < 3; i++)
        {
            mock.EnqueueToolCall("drawing_list_layers", new { limit = 2 }, callId: "c" + i);
        }

        var loop = new AgentLoop(
            mock,
            reg,
            (name, args) => Task.FromResult(reg.Invoke(name, args, context).ToModelText()),
            _ => { },
            (_, _) => { });

        var req = new SessionRequest { Question = "Lặp" };
        var res = await loop.RunAsync(req, 5, CancellationToken.None);

        Assert.Equal("repeated_tool_call", res.StopReason);
        Assert.Contains("lặp", res.Answer);
    }

    [Fact]
    public async Task RunAsync_RefusalOrFilter_StopsWithRefused()
    {
        var (reg, context) = SetupTools();
        var mock = new MockLlmProvider();
        mock.EnqueueTurn(new LlmTurn { FinishReason = "content_filter" });

        var loop = new AgentLoop(
            mock,
            reg,
            (name, args) => Task.FromResult(reg.Invoke(name, args, context).ToModelText()),
            _ => { },
            (_, _) => { });

        var req = new SessionRequest { Question = "Nội dung nhạy cảm" };
        var res = await loop.RunAsync(req, 5, CancellationToken.None);

        Assert.Equal("refused", res.StopReason);
        Assert.Contains("từ chối", res.Answer);
    }
}
