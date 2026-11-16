using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PipingAgent.Tools
{
    /// <summary>
    /// Các công cụ hành động (ToolTier.Action) cho phép Agent can thiệp vào bản vẽ CAD theo cơ chế 2-Pha Staging & Human-in-the-Loop (ADR-12).
    /// </summary>
    public static class ActionTools
    {
        public static ToolRegistry Register(ToolRegistry registry)
        {
            registry.Add(new ToolDefinition(
                "cad_propose_mutation",
                "Đề xuất một tập hợp các thay đổi (thêm ghi chú, sửa nhãn text, đổi layer, vẽ mây sửa đổi, miễn trừ cảnh báo, vẽ đoạn ống mẫu) trên bản vẽ AutoCAD. Đề xuất được lưu tạm trong bộ nhớ và chờ kỹ sư phê duyệt trước khi commit.",
                ToolTier.Action,
                "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"summary\",\"rationale\",\"auto_apply\",\"operations\"],\"properties\":{\"summary\":{\"type\":\"string\",\"description\":\"Tóm tắt ngắn gọn mục đích can thiệp.\"},\"rationale\":{\"type\":\"string\",\"description\":\"Căn cứ kỹ thuật và lý do thực hiện thay đổi.\"},\"auto_apply\":{\"type\":[\"boolean\",\"null\"],\"description\":\"Tự động thực thi trực tiếp vào bản vẽ CAD ngay sau khi tạo đề xuất (mặc định false). Dùng khi người dùng yêu cầu vẽ hoặc chỉnh sửa trực tiếp, hoàn tác an toàn qua lệnh Undo (Ctrl+Z) của AutoCAD.\"},\"operations\":{\"type\":\"array\",\"description\":\"Danh sách các thao tác can thiệp cụ thể.\",\"items\":{\"type\":\"object\",\"required\":[\"kind\"],\"properties\":{\"kind\":{\"type\":\"string\",\"enum\":[\"add_text\",\"modify_text\",\"change_layer\",\"add_revision_cloud\",\"waive_issue\",\"add_pipe\"]},\"handle\":{\"type\":[\"string\",\"null\"]},\"text\":{\"type\":[\"string\",\"null\"]},\"new_text\":{\"type\":[\"string\",\"null\"]},\"target_layer\":{\"type\":[\"string\",\"null\"]},\"x\":{\"type\":[\"number\",\"null\"]},\"y\":{\"type\":[\"number\",\"null\"]},\"height\":{\"type\":[\"number\",\"null\"]},\"min_x\":{\"type\":[\"number\",\"null\"]},\"min_y\":{\"type\":[\"number\",\"null\"]},\"max_x\":{\"type\":[\"number\",\"null\"]},\"max_y\":{\"type\":[\"number\",\"null\"]},\"comment\":{\"type\":[\"string\",\"null\"]},\"issue_id\":{\"type\":[\"string\",\"null\"]},\"reason\":{\"type\":[\"string\",\"null\"]},\"length_mm\":{\"type\":[\"number\",\"null\"]},\"dn\":{\"type\":[\"integer\",\"null\"]},\"z\":{\"type\":[\"number\",\"null\"]},\"end_x\":{\"type\":[\"number\",\"null\"]},\"end_y\":{\"type\":[\"number\",\"null\"]},\"end_z\":{\"type\":[\"number\",\"null\"]},\"direction\":{\"type\":[\"string\",\"null\"]},\"mode\":{\"type\":[\"string\",\"null\"]},\"tag\":{\"type\":[\"string\",\"null\"]}}}}}}",
                ProposeMutation));

            registry.Add(new ToolDefinition(
                "cad_apply_mutation",
                "Thực thi một đề xuất can thiệp đã được kỹ sư phê duyệt. Ghi toàn bộ thay đổi vào bản vẽ trong một khối giao dịch duy nhất có thể hoàn tác bằng UNDO.",
                ToolTier.Action,
                "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"proposal_id\"],\"properties\":{\"proposal_id\":{\"type\":[\"string\",\"null\"],\"description\":\"Mã định danh đề xuất đã được tạo từ cad_propose_mutation (để null để tự động chọn đề xuất đang chờ duy nhất).\"}}}",
                ApplyMutation));

            registry.Add(new ToolDefinition(
                "cad_doctor",
                "Chẩn đoán toàn diện sức khỏe hệ thống Piping Agent (AutoCAD, kết nối AI/Proxy, thư viện ASME Catalog, cấu hình quyền). Không can thiệp bản vẽ.",
                ToolTier.ReadOnly,
                "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[],\"properties\":{}}",
                RunDoctor));

            return registry;
        }

        private static ToolResult ProposeMutation(JsonElement input, ToolContext context)
        {
            if (!context.AllowAction)
            {
                return ToolResult.Fail(
                    "Quyền can thiệp bản vẽ đang bị khóa (Permission Guard). Hãy bật tùy chọn 'Cho phép can thiệp bản vẽ' trong Cấu hình AI hoặc truyền cờ --allow-action khi khởi chạy máy chủ MCP.",
                    "Bật AllowAction trong cấu hình trước khi thực hiện thao tác sửa đổi bản vẽ.");
            }

            var summary = input.TryGetProperty("summary", out var s) ? s.GetString() : string.Empty;
            var rationale = input.TryGetProperty("rationale", out var r) ? r.GetString() : string.Empty;

            if (string.IsNullOrWhiteSpace(summary))
            {
                return ToolResult.Fail("Tham số 'summary' không được để trống.", "Cung cấp tóm tắt ngắn gọn mục đích can thiệp.");
            }

            if (!input.TryGetProperty("operations", out var opsElement) || opsElement.ValueKind != JsonValueKind.Array)
            {
                return ToolResult.Fail("Tham số 'operations' phải là một danh sách thao tác.", "Truyền danh sách ít nhất 1 thao tác can thiệp hợp lệ.");
            }

            var opList = new List<MutationOp>();
            foreach (var opItem in opsElement.EnumerateArray())
            {
                if (!opItem.TryGetProperty("kind", out var kindProp) || string.IsNullOrWhiteSpace(kindProp.GetString()))
                {
                    return ToolResult.Fail("Mỗi thao tác phải có trường 'kind' hợp lệ.", "Các giá trị hợp lệ: add_text, modify_text, change_layer, add_revision_cloud, waive_issue, add_pipe.");
                }

                var kind = kindProp.GetString()!;
                var op = new MutationOp
                {
                    Kind = kind,
                    Handle = opItem.TryGetProperty("handle", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null,
                    Text = opItem.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null,
                    NewText = opItem.TryGetProperty("new_text", out var nt) && nt.ValueKind == JsonValueKind.String ? nt.GetString() : null,
                    TargetLayer = opItem.TryGetProperty("target_layer", out var tl) && tl.ValueKind == JsonValueKind.String ? tl.GetString() : null,
                    X = opItem.TryGetProperty("x", out var x) && x.ValueKind == JsonValueKind.Number ? x.GetDouble() : (double?)null,
                    Y = opItem.TryGetProperty("y", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetDouble() : (double?)null,
                    Height = opItem.TryGetProperty("height", out var ht) && ht.ValueKind == JsonValueKind.Number ? ht.GetDouble() : (double?)null,
                    MinX = opItem.TryGetProperty("min_x", out var mx) && mx.ValueKind == JsonValueKind.Number ? mx.GetDouble() : (double?)null,
                    MinY = opItem.TryGetProperty("min_y", out var my) && my.ValueKind == JsonValueKind.Number ? my.GetDouble() : (double?)null,
                    MaxX = opItem.TryGetProperty("max_x", out var xx) && xx.ValueKind == JsonValueKind.Number ? xx.GetDouble() : (double?)null,
                    MaxY = opItem.TryGetProperty("max_y", out var xy) && xy.ValueKind == JsonValueKind.Number ? xy.GetDouble() : (double?)null,
                    Comment = opItem.TryGetProperty("comment", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null,
                    IssueId = opItem.TryGetProperty("issue_id", out var iss) && iss.ValueKind == JsonValueKind.String ? iss.GetString() : null,
                    Reason = opItem.TryGetProperty("reason", out var re) && re.ValueKind == JsonValueKind.String ? re.GetString() : null,
                    LengthMm = opItem.TryGetProperty("length_mm", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetDouble() : (double?)null,
                    Dn = opItem.TryGetProperty("dn", out var dn) && dn.ValueKind == JsonValueKind.Number ? dn.GetInt32() : (int?)null,
                    Z = opItem.TryGetProperty("z", out var z) && z.ValueKind == JsonValueKind.Number ? z.GetDouble() : (double?)null,
                    EndX = opItem.TryGetProperty("end_x", out var ex) && ex.ValueKind == JsonValueKind.Number ? ex.GetDouble() : (double?)null,
                    EndY = opItem.TryGetProperty("end_y", out var ey) && ey.ValueKind == JsonValueKind.Number ? ey.GetDouble() : (double?)null,
                    EndZ = opItem.TryGetProperty("end_z", out var ez) && ez.ValueKind == JsonValueKind.Number ? ez.GetDouble() : (double?)null,
                    Direction = opItem.TryGetProperty("direction", out var dir) && dir.ValueKind == JsonValueKind.String ? dir.GetString() : null,
                    Mode = opItem.TryGetProperty("mode", out var md) && md.ValueKind == JsonValueKind.String ? md.GetString() : null,
                    Tag = opItem.TryGetProperty("tag", out var tg) && tg.ValueKind == JsonValueKind.String ? tg.GetString() : null,
                };

                // Kiểm tra hợp lệ từng loại thao tác
                switch (kind)
                {
                    case MutationOpKind.AddText:
                        if (string.IsNullOrWhiteSpace(op.Text) || !op.X.HasValue || !op.Y.HasValue)
                        {
                            return ToolResult.Fail("Thao tác 'add_text' yêu cầu trường 'text', 'x', và 'y'.", "Cung cấp nội dung văn bản và tọa độ x, y chèn chữ.");
                        }
                        break;

                    case MutationOpKind.ModifyText:
                        if (string.IsNullOrWhiteSpace(op.Handle) || string.IsNullOrWhiteSpace(op.NewText))
                        {
                            return ToolResult.Fail("Thao tác 'modify_text' yêu cầu trường 'handle' và 'new_text'.", "Cung cấp handle của đối tượng Text/MText và chuỗi nội dung mới.");
                        }
                        break;

                    case MutationOpKind.ChangeLayer:
                        if (string.IsNullOrWhiteSpace(op.Handle) || string.IsNullOrWhiteSpace(op.TargetLayer))
                        {
                            return ToolResult.Fail("Thao tác 'change_layer' yêu cầu trường 'handle' và 'target_layer'.", "Cung cấp handle đối tượng và tên layer đích.");
                        }
                        break;

                    case MutationOpKind.AddRevisionCloud:
                        if (!op.MinX.HasValue || !op.MinY.HasValue || !op.MaxX.HasValue || !op.MaxY.HasValue)
                        {
                            return ToolResult.Fail("Thao tác 'add_revision_cloud' yêu cầu các trường 'min_x', 'min_y', 'max_x', 'max_y'.", "Cung cấp 4 tọa độ hộp bao cho vùng cần đánh dấu.");
                        }
                        break;

                    case MutationOpKind.WaiveIssue:
                        if (string.IsNullOrWhiteSpace(op.IssueId) || string.IsNullOrWhiteSpace(op.Reason))
                        {
                            return ToolResult.Fail("Thao tác 'waive_issue' yêu cầu trường 'issue_id' và 'reason'.", "Cung cấp mã định danh vấn đề và lý do miễn trừ.");
                        }
                        break;

                    case MutationOpKind.AddPipe:
                        // Áp dụng Smart Defaults nếu không chỉ định
                        if (!op.LengthMm.HasValue || op.LengthMm.Value <= 0)
                        {
                            op.LengthMm = 2000.0;
                        }
                        if (!op.Dn.HasValue || op.Dn.Value <= 0)
                        {
                            op.Dn = 100;
                        }
                        if (string.IsNullOrWhiteSpace(op.Direction))
                        {
                            op.Direction = "X";
                        }
                        if (string.IsNullOrWhiteSpace(op.Mode))
                        {
                            op.Mode = "2D";
                        }
                        if (string.IsNullOrWhiteSpace(op.TargetLayer))
                        {
                            op.TargetLayer = "PA_PIPE";
                        }
                        if (string.IsNullOrWhiteSpace(op.Tag))
                        {
                            op.Tag = $"{op.Dn}A-SAMPLE-PIPE";
                        }
                        break;

                    default:
                        return ToolResult.Fail($"Loại thao tác '{kind}' không được hỗ trợ trong Giai đoạn 1.", "Chỉ hỗ trợ: add_text, modify_text, change_layer, add_revision_cloud, waive_issue, add_pipe.");
                }

                opList.Add(op);
            }

            if (opList.Count == 0)
            {
                return ToolResult.Fail("Danh sách 'operations' không được rỗng.", "Cung cấp ít nhất 1 thao tác cần thực hiện.");
            }

            if (opList.Count > 50)
            {
                return ToolResult.Fail("Số lượng thao tác trong một đề xuất vượt quá giới hạn an toàn (tối đa 50 thao tác).", "Chia nhỏ đề xuất thành nhiều phần.");
            }

            var autoApply = input.TryGetProperty("auto_apply", out var aa) && aa.ValueKind == JsonValueKind.True;

            var proposalId = "prop-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var proposal = new MutationProposal
            {
                ProposalId = proposalId,
                Summary = summary ?? string.Empty,
                Rationale = rationale ?? string.Empty,
                Operations = opList,
                Status = MutationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                DrawingPath = context.DocumentPath,
                DrawingFingerprint = context.DocumentFingerprint,
            };

            context.Proposals[proposalId] = proposal;

            if (autoApply)
            {
                // Document Target Binding Check (ADR-14)
                if (!string.IsNullOrEmpty(proposal.DrawingPath) && !string.IsNullOrEmpty(context.DocumentPath))
                {
                    var curName = System.IO.Path.GetFileName(context.DocumentPath);
                    var targetName = System.IO.Path.GetFileName(proposal.DrawingPath);
                    if (!string.Equals(curName, targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        proposal.Status = MutationStatus.Failed;
                        proposal.ErrorMessage = $"Khóa liên kết tài liệu không khớp (Document Target Mismatch, ADR-14): Bản vẽ đang kích hoạt ('{curName}') không khớp với bản vẽ mục tiêu ('{targetName}').";
                        return ToolResult.Fail(proposal.ErrorMessage, "Vui lòng chuyển về đúng tab bản vẽ trước khi thực thi.");
                    }
                }

                if (context.ApplyMutationExecutor != null)
                {
                    var (ok, msg) = context.ApplyMutationExecutor(proposal);
                    if (ok)
                    {
                        proposal.Status = MutationStatus.Applied;
                        return ToolResult.Success(new
                        {
                            proposal_id = proposal.ProposalId,
                            status = "applied",
                            summary = proposal.Summary,
                            operations_count = proposal.Operations.Count,
                            message = msg,
                            evidence = FormatEvidence(proposal.Evidence),
                            undo_available = true,
                            undo_hint = "Thay đổi đã được commit vào bản vẽ CAD. Bạn có thể nhấn Ctrl+Z (Undo) trong AutoCAD nếu muốn hoàn tác."
                        });
                    }
                    else
                    {
                        proposal.Status = MutationStatus.Failed;
                        proposal.ErrorMessage = msg;
                        return ToolResult.Fail($"Áp dụng thất bại: {msg}", "Xem chi tiết lỗi để khắc phục.");
                    }
                }
                else
                {
                    proposal.Status = MutationStatus.Applied;
                    return ToolResult.Success(new
                    {
                        proposal_id = proposal.ProposalId,
                        status = "applied",
                        summary = proposal.Summary,
                        operations_count = proposal.Operations.Count,
                        message = "Đã thực thi thành công (chế độ không có AutoCAD session).",
                        evidence = FormatEvidence(proposal.Evidence),
                        undo_available = true,
                    });
                }
            }

            return ToolResult.Success(new
            {
                proposal_id = proposal.ProposalId,
                status = "pending",
                summary = proposal.Summary,
                operations_count = proposal.Operations.Count,
                operations = proposal.Operations.Select(o => new
                {
                    o.Kind,
                    o.Handle,
                    o.Text,
                    o.NewText,
                    o.TargetLayer,
                    o.Comment,
                    o.IssueId
                }),
                message = "Đề xuất đã được lưu vào bộ nhớ tạm (Staging). Đang chờ kỹ sư phê duyệt trước khi commit vào bản vẽ CAD."
            });
        }

        private static ToolResult ApplyMutation(JsonElement input, ToolContext context)
        {
            if (!context.AllowAction)
            {
                return ToolResult.Fail(
                    "Quyền can thiệp bản vẽ đang bị khóa (Permission Guard). Hãy bật tùy chọn 'Cho phép can thiệp bản vẽ' trong Cấu hình AI hoặc truyền cờ --allow-action khi khởi chạy máy chủ MCP.",
                    "Bật AllowAction trong cấu hình trước khi thực hiện thao tác sửa đổi bản vẽ.");
            }

            var proposalId = input.TryGetProperty("proposal_id", out var p) ? p.GetString() : null;
            MutationProposal? proposal = null;

            if (!string.IsNullOrWhiteSpace(proposalId))
            {
                if (context.Proposals.TryGetValue(proposalId!, out var exact))
                {
                    proposal = exact;
                }
                else
                {
                    // Resilient matching: case-insensitive, prefix/suffix stripped (e.g. pro-xxx vs prop-xxx)
                    var cleanSearch = proposalId!.Trim().Replace("prop-", "").Replace("pro-", "");
                    proposal = context.Proposals.Values.FirstOrDefault(pr =>
                        string.Equals(pr.ProposalId, proposalId, StringComparison.OrdinalIgnoreCase) ||
                        pr.ProposalId.Replace("prop-", "").Equals(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(cleanSearch) && pr.ProposalId.IndexOf(cleanSearch, StringComparison.OrdinalIgnoreCase) >= 0));
                }
            }

            if (proposal == null)
            {
                var pending = context.Proposals.Values.Where(pr => pr.Status == MutationStatus.Pending).ToList();
                if (pending.Count == 1)
                {
                    proposal = pending[0];
                    proposalId = proposal.ProposalId;
                }
                else if (string.IsNullOrWhiteSpace(proposalId))
                {
                    return ToolResult.Fail("Thiếu tham số 'proposal_id'.", "Truyền mã định danh đề xuất đã được tạo từ cad_propose_mutation.");
                }
                else
                {
                    return ToolResult.Fail($"Không tìm thấy đề xuất có mã '{proposalId}'.", "Kiểm tra lại proposal_id hoặc gọi cad_propose_mutation để tạo đề xuất mới.");
                }
            }

            // Document Target Binding Check (ADR-14)
            if (!string.IsNullOrEmpty(proposal.DrawingPath) && !string.IsNullOrEmpty(context.DocumentPath))
            {
                var curName = System.IO.Path.GetFileName(context.DocumentPath);
                var targetName = System.IO.Path.GetFileName(proposal.DrawingPath);
                if (!string.Equals(curName, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return ToolResult.Fail(
                        $"Khóa liên kết tài liệu không khớp (Document Target Mismatch, ADR-14): Bản vẽ đang kích hoạt ('{curName}') không khớp với bản vẽ nơi đề xuất được tạo ('{targetName}').",
                        "Vui lòng chuyển về đúng tab bản vẽ trước khi áp dụng đề xuất.");
                }
            }

            if (proposal.Status == MutationStatus.Applied)
            {
                return ToolResult.Fail($"Đề xuất '{proposalId}' đã được áp dụng trước đó.", "Không áp dụng lại đề xuất đã hoàn tất.");
            }

            if (proposal.Status == MutationStatus.Rejected)
            {
                return ToolResult.Fail($"Đề xuất '{proposalId}' đã bị từ chối.", "Tạo đề xuất mới nếu cần can thiệp lại.");
            }

            string? execMsg = null;
            if (context.ApplyMutationExecutor != null)
            {
                var (ok, msg) = context.ApplyMutationExecutor(proposal);
                if (!ok)
                {
                    proposal.Status = MutationStatus.Failed;
                    proposal.ErrorMessage = msg;
                    return ToolResult.Fail($"Áp dụng đề xuất thất bại: {msg}", "Kiểm tra quyền truy cập bản vẽ hoặc thử lại.");
                }
                execMsg = msg;
            }

            proposal.Status = MutationStatus.Applied;
            return ToolResult.Success(new
            {
                proposal_id = proposal.ProposalId,
                status = "applied",
                applied_count = proposal.Operations.Count,
                message = execMsg ?? "Đã áp dụng toàn bộ thay đổi vào bản vẽ AutoCAD thành công trong một giao dịch đơn lẻ.",
                evidence = FormatEvidence(proposal.Evidence)
            });
        }

        private static object? FormatEvidence(MutationEvidence? ev) =>
            ev != null ? new
            {
                applied_at = ev.AppliedAt.ToString("o"),
                created_handles = ev.CreatedHandles,
                modified_handles = ev.ModifiedHandles,
                bounding_box_min = ev.BoundingBoxMin,
                bounding_box_max = ev.BoundingBoxMax,
                measured_metrics = ev.MeasuredMetrics,
                audit_passed = ev.AuditPassed,
                verification_summary = ev.VerificationSummary
            } : null;

        private static ToolResult RunDoctor(JsonElement input, ToolContext context)
        {
            var checks = new List<object>();

            // 1. Kiểm tra tài liệu CAD
            var hasDoc = !string.IsNullOrEmpty(context.DocumentPath) || context.Summary != null || context.Entities.Count > 0;
            var docName = !string.IsNullOrEmpty(context.DocumentPath) ? System.IO.Path.GetFileName(context.DocumentPath) : (hasDoc ? "Bản vẽ hiện hành" : "Chưa nạp bản vẽ");
            checks.Add(new
            {
                category = "cad_document",
                name = "Bản vẽ AutoCAD",
                status = hasDoc ? "PASS" : "WARN",
                details = $"Tài liệu: {docName}, Đối tượng: {context.Entities.Count}, Layer: {context.Summary?.Layers.Count ?? 0}"
            });

            // 2. Kiểm tra danh mục tiêu chuẩn ASME
            var catalogLoaded = context.Catalog != null && context.Catalog.Standards.Count > 0;
            checks.Add(new
            {
                category = "piping_catalog",
                name = "Thư viện Tiêu chuẩn ASME",
                status = catalogLoaded ? "PASS" : "WARN",
                details = catalogLoaded ? $"Đã nạp {context.Catalog!.Standards.Count} tiêu chuẩn ({string.Join(", ", context.Catalog.Standards)})" : "Chưa nạp hoặc danh mục rỗng"
            });

            // 3. Quyền can thiệp bản vẽ (Action Tools)
            checks.Add(new
            {
                category = "permissions",
                name = "Quyền Can thiệp Bản vẽ",
                status = context.AllowAction ? "PASS" : "INFO",
                details = context.AllowAction ? "Kích hoạt (AllowAction: true)" : "Chỉ đọc (AllowAction: false). Dùng PA_AGENTCONFIG để bật nếu cần vẽ."
            });

            // 4. Hàng đợi đề xuất can thiệp
            var pendingCount = context.Proposals.Values.Count(p => p.Status == MutationStatus.Pending);
            checks.Add(new
            {
                category = "staging",
                name = "Hàng đợi Đề xuất Can thiệp",
                status = "PASS",
                details = $"{pendingCount} đề xuất đang chờ duyệt, tổng cộng {context.Proposals.Count} đề xuất trong phiên."
            });

            var overallHealth = hasDoc && catalogLoaded ? "Healthy (Hệ thống sẵn sàng 100%)" : "Degraded (Có cảnh báo, vui lòng kiểm tra chi tiết)";

            return ToolResult.Success(new
            {
                overall_health = overallHealth,
                checks_count = checks.Count,
                checks
            });
        }
    }
}
