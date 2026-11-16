using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PipingAgent.Mcp;
using PipingAgent.Tools;
using Xunit;

namespace Agent.Tests
{
    public class ActionToolsTests
    {
        [Fact]
        public void ProposeMutation_WhenAllowActionFalse_FailsWithPermissionGuardError()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext { AllowAction = false };

            var input = JsonDocument.Parse(@"{
                ""summary"": ""Sửa nhãn van"",
                ""rationale"": ""Nhãn V-101 bị sai chính tả"",
                ""operations"": [
                    { ""kind"": ""modify_text"", ""handle"": ""3A"", ""new_text"": ""V-101"" }
                ]
            }").RootElement;

            var result = registry.Invoke("cad_propose_mutation", input, context);

            Assert.False(result.Ok);
            Assert.Contains("Permission Guard", result.Error);
        }

        [Fact]
        public void ProposeMutation_WithEmptyOperations_FailsValidation()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext { AllowAction = true };

            var input = JsonDocument.Parse(@"{
                ""summary"": ""Thử nghiệm rỗng"",
                ""rationale"": ""Không có thao tác"",
                ""operations"": []
            }").RootElement;

            var result = registry.Invoke("cad_propose_mutation", input, context);

            Assert.False(result.Ok);
            Assert.Contains("không được rỗng", result.Error);
        }

        [Fact]
        public void ProposeMutation_WithInvalidKind_FailsValidation()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext { AllowAction = true };

            var input = JsonDocument.Parse(@"{
                ""summary"": ""Xóa nhầm"",
                ""rationale"": ""Thử lệnh xóa"",
                ""operations"": [
                    { ""kind"": ""delete_entity"", ""handle"": ""1B"" }
                ]
            }").RootElement;

            var result = registry.Invoke("cad_propose_mutation", input, context);

            Assert.False(result.Ok);
            Assert.Contains("lược đồ", result.Error);
        }

        [Fact]
        public void ProposeMutation_WithValidOperations_StoresProposalAndReturnsPending()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext { AllowAction = true };

            var input = JsonDocument.Parse(@"{
                ""summary"": ""Bổ sung nhãn và đổi layer"",
                ""rationale"": ""Kỹ sư yêu cầu chuẩn hóa"",
                ""operations"": [
                    { ""kind"": ""add_text"", ""text"": ""DN100-P-1001"", ""x"": 1500.0, ""y"": 2500.0, ""height"": 250.0 },
                    { ""kind"": ""change_layer"", ""handle"": ""2F"", ""target_layer"": ""PA_PIPE"" },
                    { ""kind"": ""add_revision_cloud"", ""min_x"": 1000.0, ""min_y"": 2000.0, ""max_x"": 3000.0, ""max_y"": 4000.0, ""comment"": ""Cần xác nhận"" },
                    { ""kind"": ""waive_issue"", ""issue_id"": ""ISSUE-01"", ""reason"": ""Đã được kỹ sư trưởng phê duyệt"" }
                ]
            }").RootElement;

            var result = registry.Invoke("cad_propose_mutation", input, context);

            Assert.True(result.Ok);
            Assert.Single(context.Proposals);

            var doc = JsonDocument.Parse(result.ToModelText());
            var proposalId = doc.RootElement.GetProperty("proposal_id").GetString();
            Assert.NotNull(proposalId);
            Assert.StartsWith("prop-", proposalId);
            Assert.Equal("pending", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal(4, doc.RootElement.GetProperty("operations_count").GetInt32());

            var proposal = context.Proposals[proposalId!];
            Assert.Equal(MutationStatus.Pending, proposal.Status);
            Assert.Equal(4, proposal.Operations.Count);
        }

        [Fact]
        public void ApplyMutation_WithValidProposal_ExecutesAndMarksApplied()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext { AllowAction = true };

            var proposal = new MutationProposal
            {
                ProposalId = "prop-test001",
                Summary = "Đổi layer",
                Status = MutationStatus.Pending,
                Operations = { new MutationOp { Kind = MutationOpKind.ChangeLayer, Handle = "5A", TargetLayer = "PA_VALVE" } }
            };
            context.Proposals[proposal.ProposalId] = proposal;

            var executed = false;
            context.ApplyMutationExecutor = p =>
            {
                executed = true;
                return (true, "Thành công");
            };

            var input = JsonDocument.Parse("{\"proposal_id\":\"prop-test001\"}").RootElement;
            var result = registry.Invoke("cad_apply_mutation", input, context);

            Assert.True(result.Ok);
            Assert.True(executed);
            Assert.Equal(MutationStatus.Applied, proposal.Status);

            var doc = JsonDocument.Parse(result.ToModelText());
            Assert.Equal("applied", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, doc.RootElement.GetProperty("applied_count").GetInt32());
            Assert.Equal("Thành công", doc.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public async Task McpServer_WithAllowActionFlag_ExposesActionTools()
        {
            var server = new McpServer(drawingPath: null, pipeName: "dummy", allowDraft: false, allowAction: true);
            var response = await server.HandleRequestAsync("tools/list", null);

            Assert.NotNull(response);
            var tools = response.Value.GetProperty("tools");

            var foundPropose = false;
            var foundApply = false;
            foreach (var t in tools.EnumerateArray())
            {
                var name = t.GetProperty("name").GetString();
                if (name == "cad_propose_mutation") foundPropose = true;
                if (name == "cad_apply_mutation") foundApply = true;
            }

            Assert.True(foundPropose);
            Assert.True(foundApply);
        }

        [Fact]
        public void ProposeMutation_WithAddPipe_AppliesSmartDefaults()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext { AllowAction = true };

            var input = JsonDocument.Parse(@"{
                ""summary"": ""Vẽ đoạn ống mẫu"",
                ""rationale"": ""Yêu cầu người dùng vẽ ống mẫu"",
                ""operations"": [
                    { ""kind"": ""add_pipe"" }
                ]
            }").RootElement;

            var result = registry.Invoke("cad_propose_mutation", input, context);

            Assert.True(result.Ok);
            Assert.Single(context.Proposals);

            var proposal = context.Proposals.Values.First();
            var op = Assert.Single(proposal.Operations);
            Assert.Equal(MutationOpKind.AddPipe, op.Kind);
            Assert.Equal(2000.0, op.LengthMm);
            Assert.Equal(100, op.Dn);
            Assert.Equal("X", op.Direction);
            Assert.Equal("2D", op.Mode);
            Assert.Equal("PA_PIPE", op.TargetLayer);
            Assert.Equal("100A-SAMPLE-PIPE", op.Tag);
        }

        [Fact]
        public void ProposeMutation_WithAddPipeCustomParams_PreservesCustomValues()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext { AllowAction = true };

            var input = JsonDocument.Parse(@"{
                ""summary"": ""Vẽ ống 5m DN200 3D"",
                ""rationale"": ""Theo thông số kỹ sư chỉ định"",
                ""operations"": [
                    {
                        ""kind"": ""add_pipe"",
                        ""length_mm"": 5000.0,
                        ""dn"": 200,
                        ""direction"": ""Y"",
                        ""mode"": ""3D"",
                        ""target_layer"": ""PIPE_PROCESS"",
                        ""tag"": ""200-P-5001""
                    }
                ]
            }").RootElement;

            var result = registry.Invoke("cad_propose_mutation", input, context);

            Assert.True(result.Ok);
            var proposal = context.Proposals.Values.First();
            var op = Assert.Single(proposal.Operations);
            Assert.Equal(5000.0, op.LengthMm);
            Assert.Equal(200, op.Dn);
            Assert.Equal("Y", op.Direction);
            Assert.Equal("3D", op.Mode);
            Assert.Equal("PIPE_PROCESS", op.TargetLayer);
            Assert.Equal("200-P-5001", op.Tag);
        }

        [Fact]
        public void ApplyMutation_WhenDocumentTargetMismatches_FailsWithMismatchError()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext
            {
                AllowAction = true,
                DocumentPath = "C:\\Projects\\Plant\\Unit20.dwg"
            };

            var proposal = new MutationProposal
            {
                ProposalId = "prop-mismatch01",
                Summary = "Đổi layer",
                Status = MutationStatus.Pending,
                DrawingPath = "C:\\Projects\\Plant\\Unit10.dwg",
                Operations = { new MutationOp { Kind = MutationOpKind.ChangeLayer, Handle = "5A", TargetLayer = "PA_VALVE" } }
            };
            context.Proposals[proposal.ProposalId] = proposal;

            var input = JsonDocument.Parse("{\"proposal_id\":\"prop-mismatch01\"}").RootElement;
            var result = registry.Invoke("cad_apply_mutation", input, context);

            Assert.False(result.Ok);
            Assert.Contains("Document Target Mismatch", result.Error);
        }

        [Fact]
        public void ApplyMutation_WhenDocumentTargetMatches_PopulatesEvidence()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext
            {
                AllowAction = true,
                DocumentPath = "C:\\Projects\\Plant\\Unit10.dwg"
            };

            var proposal = new MutationProposal
            {
                ProposalId = "prop-match01",
                Summary = "Vẽ ống mẫu",
                Status = MutationStatus.Pending,
                DrawingPath = "C:\\Projects\\Plant\\Unit10.dwg",
                Operations = { new MutationOp { Kind = MutationOpKind.AddPipe, LengthMm = 2000.0, Dn = 100 } }
            };
            context.Proposals[proposal.ProposalId] = proposal;

            context.ApplyMutationExecutor = p =>
            {
                p.Evidence = new MutationEvidence
                {
                    ProposalId = p.ProposalId,
                    CreatedHandles = { "10A", "10B" },
                    BoundingBoxMin = new double[] { 0, 0, 0 },
                    BoundingBoxMax = new double[] { 2000, 150, 0 },
                    MeasuredMetrics = { ["pipe_length_mm"] = 2000.0 },
                    AuditPassed = true
                };
                return (true, "Đã áp dụng thành công.");
            };

            var input = JsonDocument.Parse("{\"proposal_id\":\"prop-match01\"}").RootElement;
            var result = registry.Invoke("cad_apply_mutation", input, context);

            Assert.True(result.Ok);
            Assert.Equal(MutationStatus.Applied, proposal.Status);
            Assert.NotNull(proposal.Evidence);
            Assert.Equal(2, proposal.Evidence.CreatedHandles.Count);

            var doc = JsonDocument.Parse(result.ToModelText());
            Assert.True(doc.RootElement.TryGetProperty("evidence", out var ev));
            Assert.Equal(2, ev.GetProperty("created_handles").GetArrayLength());
        }

        [Fact]
        public void RunDoctor_ReturnsHealthReport()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext
            {
                AllowAction = true,
                DocumentPath = "C:\\drawings\\Piping_01.dwg"
            };

            var input = JsonDocument.Parse("{}").RootElement;
            var result = registry.Invoke("cad_doctor", input, context);

            Assert.True(result.Ok);
            var doc = JsonDocument.Parse(result.ToModelText());
            Assert.True(doc.RootElement.TryGetProperty("overall_health", out _));
            Assert.True(doc.RootElement.TryGetProperty("checks", out var checks));
            Assert.True(checks.GetArrayLength() >= 4);
        }

        [Fact]
        public void ProposeMutation_WithAutoApplyTrue_ExecutesAndReturnsAppliedStatus()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext
            {
                AllowAction = true,
                DocumentPath = "C:\\drawings\\Drawing1.dwg"
            };

            var executed = false;
            context.ApplyMutationExecutor = p =>
            {
                executed = true;
                p.Evidence = new MutationEvidence
                {
                    ProposalId = p.ProposalId,
                    CreatedHandles = { "4B", "4C" },
                    AuditPassed = true
                };
                return (true, "Đã áp dụng thành công 1 thao tác.");
            };

            var input = JsonDocument.Parse(@"{
                ""summary"": ""Vẽ ống mẫu tự động"",
                ""rationale"": ""Người dùng yêu cầu vẽ ngay"",
                ""auto_apply"": true,
                ""operations"": [
                    { ""kind"": ""add_pipe"", ""dn"": 100, ""length_mm"": 2000.0 }
                ]
            }").RootElement;

            var result = registry.Invoke("cad_propose_mutation", input, context);

            Assert.True(result.Ok);
            Assert.True(executed);

            var doc = JsonDocument.Parse(result.ToModelText());
            Assert.Equal("applied", doc.RootElement.GetProperty("status").GetString());
            Assert.True(doc.RootElement.GetProperty("undo_available").GetBoolean());
            Assert.True(doc.RootElement.TryGetProperty("evidence", out var ev));
            Assert.Equal(2, ev.GetProperty("created_handles").GetArrayLength());
        }

        [Fact]
        public void ProposeMutation_WithAutoApplyTrue_WhenExecutorFails_ReturnsFail()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext
            {
                AllowAction = true,
                DocumentPath = "C:\\drawings\\Drawing1.dwg"
            };

            context.ApplyMutationExecutor = p => (false, "Lỗi kiểm toán L3");

            var input = JsonDocument.Parse(@"{
                ""summary"": ""Vẽ ống mẫu lỗi"",
                ""rationale"": ""Test lỗi executor"",
                ""auto_apply"": true,
                ""operations"": [
                    { ""kind"": ""add_pipe"", ""dn"": 100, ""length_mm"": 2000.0 }
                ]
            }").RootElement;

            var result = registry.Invoke("cad_propose_mutation", input, context);

            Assert.False(result.Ok);
            Assert.Contains("Lỗi kiểm toán L3", result.Error);
        }

        [Fact]
        public void ApplyMutation_WithFuzzyProposalId_ResolvesAndAppliesSuccessfully()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext
            {
                AllowAction = true,
                DocumentPath = "C:\\drawings\\Drawing1.dwg"
            };

            var proposal = new MutationProposal
            {
                ProposalId = "prop-d468e093",
                Summary = "Đoạn ống mẫu",
                Status = MutationStatus.Pending,
                DrawingPath = "C:\\drawings\\Drawing1.dwg"
            };
            context.Proposals[proposal.ProposalId] = proposal;

            var executed = false;
            context.ApplyMutationExecutor = p =>
            {
                executed = true;
                return (true, "Thành công.");
            };

            // Người dùng gõ nhầm 'pro-' thay vì 'prop-'
            var input = JsonDocument.Parse("{\"proposal_id\":\"pro-d468e093\"}").RootElement;
            var result = registry.Invoke("cad_apply_mutation", input, context);

            Assert.True(result.Ok);
            Assert.True(executed);
            Assert.Equal(MutationStatus.Applied, proposal.Status);
        }

        [Fact]
        public void ApplyMutation_WithoutProposalId_WhenSinglePending_ResolvesAndAppliesSuccessfully()
        {
            var registry = ActionTools.Register(new ToolRegistry());
            var context = new ToolContext
            {
                AllowAction = true,
                DocumentPath = "C:\\drawings\\Drawing1.dwg"
            };

            var proposal = new MutationProposal
            {
                ProposalId = "prop-single01",
                Summary = "Đoạn ống mẫu duy nhất",
                Status = MutationStatus.Pending,
                DrawingPath = "C:\\drawings\\Drawing1.dwg"
            };
            context.Proposals[proposal.ProposalId] = proposal;

            var executed = false;
            context.ApplyMutationExecutor = p =>
            {
                executed = true;
                return (true, "Thành công.");
            };

            // Gọi không kèm proposal_id
            var input = JsonDocument.Parse("{}").RootElement;
            var result = registry.Invoke("cad_apply_mutation", input, context);

            Assert.True(result.Ok);
            Assert.True(executed);
            Assert.Equal(MutationStatus.Applied, proposal.Status);
        }
    }
}
