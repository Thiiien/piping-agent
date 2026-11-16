using System.Collections.Generic;
using System.Text.Json;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Source;
using PipingAgent.Tools;
using Xunit;

namespace Agent.Tests;

public class ProfileDraftToolsTests
{
    private static ToolRegistry CreateRegistry()
    {
        var reg = new ToolRegistry();
        ReadOnlyTools.Register(reg);
        ProfileDraftTools.Register(reg);
        return reg;
    }

    private static ToolContext CreateContext()
    {
        var entities = new List<SourceEntity>
        {
            new SourceEntity { Handle = "h1", Layer = "PIPE_CENTER", Kind = SourceKind.Line, Points = new List<Point3> { new(0, 0, 0), new(1000, 0, 0) } },
            new SourceEntity { Handle = "h2", Layer = "PIPE_TEXT", Kind = SourceKind.Text, Text = "100-P-01-DN100-EL1000", Center = new(500, 50, 0) },
        };

        return new ToolContext
        {
            Entities = entities,
            Summary = entities.Summarize(),
        };
    }

    [Fact]
    public void WriteDraftProfile_ValidJson_SetsContextDraftProfile()
    {
        var reg = CreateRegistry();
        var context = CreateContext();

        var profileJson = "{\"schemaVersion\":\"1.0\",\"id\":\"draft-test\",\"pipeStandard\":\"ASME-B36.10\",\"roles\":[{\"role\":\"pipe_centerline\",\"match\":{\"layers\":[\"PIPE_CENTER\"]}},{\"role\":\"pipe_tag\",\"match\":{\"layers\":[\"PIPE_TEXT\"]}}]}";
        var input = JsonDocument.Parse(JsonSerializer.Serialize(new { profile_json = profileJson })).RootElement;

        var result = reg.Invoke("profile_draft_write", input, context);

        Assert.True(result.Ok);
        Assert.NotNull(context.DraftProfile);
        Assert.Equal("draft-test", context.DraftProfile!.Id);
        Assert.Equal("ASME-B36.10", context.DraftProfile.PipeStandard);
    }

    [Fact]
    public void WriteDraftProfile_InvalidJson_ReturnsFailure()
    {
        var reg = CreateRegistry();
        var context = CreateContext();

        var input = JsonDocument.Parse(JsonSerializer.Serialize(new { profile_json = "invalid json {{" })).RootElement;
        var result = reg.Invoke("profile_draft_write", input, context);

        Assert.False(result.Ok);
        Assert.Null(context.DraftProfile);
        Assert.Contains("không hợp lệ", result.Error ?? string.Empty);
    }

    [Fact]
    public void TestDraftProfile_NoDraft_ReturnsFailure()
    {
        var reg = CreateRegistry();
        var context = CreateContext();

        var input = JsonDocument.Parse("{\"sample_limit\":null}").RootElement;
        var result = reg.Invoke("profile_draft_test", input, context);

        Assert.False(result.Ok);
        Assert.Contains("Chưa có hồ sơ nháp", result.Error ?? string.Empty);
    }

    [Fact]
    public void TestDraftProfile_WithDraft_ReturnsRecognitionMetrics()
    {
        var reg = CreateRegistry();
        var context = CreateContext();

        // 1. Ghi hồ sơ nháp
        var profileJson = "{\"schemaVersion\":\"1.0\",\"id\":\"draft-test\",\"pipeStandard\":\"ASME-B36.10\",\"roles\":[{\"role\":\"pipe_centerline\",\"match\":{\"layers\":[\"PIPE_CENTER\"]}},{\"role\":\"pipe_tag\",\"match\":{\"layers\":[\"PIPE_TEXT\"]}}]}";
        var writeInput = JsonDocument.Parse(JsonSerializer.Serialize(new { profile_json = profileJson })).RootElement;
        reg.Invoke("profile_draft_write", writeInput, context);

        // 2. Chạy thử
        var testInput = JsonDocument.Parse("{\"sample_limit\":5}").RootElement;
        var result = reg.Invoke("profile_draft_test", testInput, context);

        Assert.True(result.Ok);
        var content = result.Content;
        Assert.Equal("draft-test", content.GetProperty("profile_id").GetString());
        Assert.Equal(2, content.GetProperty("total_entities").GetInt32());
        Assert.True(content.GetProperty("classification_ratio").GetDouble() > 0);
    }
}
