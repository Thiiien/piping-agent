using System.Collections.Generic;
using System.Reflection.Metadata;
using PipingAgent.Core.Geometry;
using PipingAgent.UI;
using Xunit;

namespace Agent.Tests;

public class PanelViewModelTests
{
    private sealed class MockHostActions : IHostActions
    {
        public string? LastRunAgentMode { get; private set; }
        public string? LastRunAgentQuestion { get; private set; }
        public string? LastAcceptedJson { get; private set; }

        public void Highlight(IReadOnlyList<string> handles) { }
        public void ClearHighlight() { }
        public void ZoomTo(Box2 region) { }
        public void Rescan() { }
        public string? PickProfileFile() => null;
        public void WaiveIssue(string issueId, string reason) { }
        public void RunCommand(string command) { }
        public string TestTagPattern(string regex) => string.Empty;
        public string? SaveProfileFile(string json) => "test.json";

        public void RunAgent(string mode, string? question)
        {
            LastRunAgentMode = mode;
            LastRunAgentQuestion = question;
        }

        public string? AcceptDraftProfile(string json)
        {
            LastAcceptedJson = json;
            return "saved-profile.json";
        }
    }

    [Fact]
    public void SendChatMessage_ValidText_AddsMessageAndInvokesHost()
    {
        var host = new MockHostActions();
        var vm = new PanelViewModel(host);

        vm.ChatInput = "Bản vẽ có bao nhiêu tuyến ống?";
        vm.SendChatMessage();

        Assert.Single(vm.ChatMessages);
        Assert.Equal("Người dùng", vm.ChatMessages[0].Sender);
        Assert.Equal("Bản vẽ có bao nhiêu tuyến ống?", vm.ChatMessages[0].Text);
        Assert.True(vm.IsAgentRunning);
        Assert.Equal("qa", host.LastRunAgentMode);
        Assert.Equal("Bản vẽ có bao nhiêu tuyến ống?", host.LastRunAgentQuestion);
    }

    [Fact]
    public void SendChatMessage_WhenEmpty_DoesNothing()
    {
        var host = new MockHostActions();
        var vm = new PanelViewModel(host);

        vm.ChatInput = "   ";
        vm.SendChatMessage();

        Assert.Empty(vm.ChatMessages);
        Assert.False(vm.IsAgentRunning);
        Assert.Null(host.LastRunAgentMode);
    }

    [Fact]
    public void ProposeProfile_InvokesHostWithProfileMode()
    {
        var host = new MockHostActions();
        var vm = new PanelViewModel(host);

        vm.ProposeProfile();

        Assert.Single(vm.ChatMessages);
        Assert.Equal("Người dùng", vm.ChatMessages[0].Sender);
        Assert.True(vm.IsAgentRunning);
        Assert.Equal("profile", host.LastRunAgentMode);
    }

    [Fact]
    public void SetDraftProfile_SetsPropertiesAndHasDraftProfileTrue()
    {
        var host = new MockHostActions();
        var vm = new PanelViewModel(host);

        Assert.False(vm.HasDraftProfile);

        vm.SetDraftProfile("profile-test", "ASME-B36.10", 3, 2, 1, 0.95, 0.90, 0, 1, "{\"id\":\"profile-test\"}");

        Assert.True(vm.HasDraftProfile);
        Assert.NotNull(vm.DraftProfile);
        Assert.Equal("profile-test", vm.DraftProfile!.ProfileId);
        Assert.Equal("ASME-B36.10", vm.DraftProfile.PipeStandard);
        Assert.Equal(3, vm.DraftProfile.RolesCount);
        Assert.Equal(0.95, vm.DraftProfile.ClassificationRatio);
        Assert.Contains("95.0%", vm.DraftProfile.Description);
    }

    [Fact]
    public void AcceptDraftProfile_InvokesHostAndClearsDraft()
    {
        var host = new MockHostActions();
        var vm = new PanelViewModel(host);

        vm.SetDraftProfile("p1", "ASME-B36.10", 2, 1, 1, 0.9, 0.8, 1, 0, "{\"id\":\"p1\"}");
        Assert.True(vm.HasDraftProfile);

        vm.AcceptDraftProfile();

        Assert.False(vm.HasDraftProfile);
        Assert.Null(vm.DraftProfile);
        Assert.Equal("{\"id\":\"p1\"}", host.LastAcceptedJson);
        Assert.Contains(vm.ChatMessages, m => m.Sender == "Hệ thống" && m.Text.Contains("saved-profile.json"));
    }

    [Fact]
    public void RejectDraftProfile_ClearsDraftWithoutHostCall()
    {
        var host = new MockHostActions();
        var vm = new PanelViewModel(host);

        vm.SetDraftProfile("p1", "ASME-B36.10", 2, 1, 1, 0.9, 0.8, 1, 0, "{\"id\":\"p1\"}");
        Assert.True(vm.HasDraftProfile);

        vm.RejectDraftProfile();

        Assert.False(vm.HasDraftProfile);
        Assert.Null(vm.DraftProfile);
        Assert.Null(host.LastAcceptedJson);
        Assert.Contains(vm.ChatMessages, m => m.Sender == "Hệ thống" && m.Text.Contains("từ chối"));
    }

    [Fact]
    public void ChatMessageItem_RoleInferenceAndProperties_WorkAsExpected()
    {
        var vm = new PanelViewModel(null);

        vm.AddChatMessage("Người dùng", "Xin chào");
        vm.AddChatMessage("Agent", "Chào bạn! Tôi có thể giúp gì?");
        vm.AddChatMessage("Công cụ", "[read_drawing_summary]");
        vm.AddChatMessage("Lỗi", "Không kết nối được");
        vm.AddChatMessage("Hệ thống", "Đã lưu cấu hình");

        Assert.Equal(5, vm.ChatMessages.Count);

        var userMsg = vm.ChatMessages[0];
        Assert.True(userMsg.IsUser);
        Assert.Contains("Bạn", userMsg.Badge);
        Assert.Contains(userMsg.Timestamp, userMsg.DisplayHeader);

        var agentMsg = vm.ChatMessages[1];
        Assert.True(agentMsg.IsAgent);
        Assert.Contains("Piping AI", agentMsg.Badge);

        var toolMsg = vm.ChatMessages[2];
        Assert.True(toolMsg.IsTool);
        Assert.Contains("Công cụ", toolMsg.Badge);

        var errorMsg = vm.ChatMessages[3];
        Assert.True(errorMsg.IsError);
        Assert.Contains("Lỗi", errorMsg.Badge);

        var sysMsg = vm.ChatMessages[4];
        Assert.True(sysMsg.IsSystem);
        Assert.Contains("Hệ thống", sysMsg.Badge);
    }

    [Fact]
    public void SendSuggestedQuery_ExecutesAndAddsMessage()
    {
        var host = new MockHostActions();
        var vm = new PanelViewModel(host);

        vm.SendSuggestedQuery("Thống kê layer bản vẽ");

        Assert.Single(vm.ChatMessages);
        Assert.Equal("Thống kê layer bản vẽ", vm.ChatMessages[0].Text);
        Assert.True(vm.ChatMessages[0].IsUser);
        Assert.Equal("qa", host.LastRunAgentMode);
        Assert.Equal("Thống kê layer bản vẽ", host.LastRunAgentQuestion);
    }

    [Fact]
    public void ClearChat_ResetsMessagesAndStepAndDraft()
    {
        var vm = new PanelViewModel(null);
        vm.AddChatMessage("Người dùng", "Câu hỏi 1");
        vm.AgentCurrentStep = "Đang chạy...";
        vm.SetDraftProfile("p1", "ASME-B36.10", 1, 1, 1, 0.5, 0.5, 0, 0, "{}");

        Assert.NotEmpty(vm.ChatMessages);
        Assert.True(vm.HasDraftProfile);

        vm.ClearChat();

        Assert.Empty(vm.ChatMessages);
        Assert.Empty(vm.AgentCurrentStep);
        Assert.False(vm.HasDraftProfile);
    }

    [Fact]
    public void DraftProfileSummaryItem_FormattingProperties_ReturnExpectedStrings()
    {
        var item = new DraftProfileSummaryItem
        {
            ProfileId = "p-demo",
            PipeStandard = "ASME-B36.10",
            ClassificationRatio = 0.945,
            TagMatchRatio = 0.823,
        };

        Assert.Equal("94.5%", item.ClassificationText);
        Assert.Equal("82.3%", item.TagMatchText);
    }
}
