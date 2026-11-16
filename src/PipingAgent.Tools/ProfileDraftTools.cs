using System;
using System.Linq;
using System.Text.Json;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;

namespace PipingAgent.Tools
{
    /// <summary>
    /// Hai công cụ của chế độ lập hồ sơ, S8-05: ghi hồ sơ nháp vào bộ nhớ phiên và chạy thử hồ sơ nháp
    /// trả về chỉ số nhận diện (tỷ lệ phân loại, tỷ lệ nhãn khớp, số đoạn chạy không nhãn, mẫu không khớp).
    /// </summary>
    public static class ProfileDraftTools
    {
        public static ToolRegistry Register(ToolRegistry registry)
        {
            registry.Add(new ToolDefinition("profile_draft_write",
                "Ghi hồ sơ quy ước nháp vào bộ nhớ phiên làm việc để chuẩn bị chạy thử. Tham số là chuỗi JSON của hồ sơ quy ước (id, pipeStandard, layers, blocks, tagFormat, v.v.). Hồ sơ nháp chưa có hiệu lực cho đến khi người dùng duyệt.",
                ToolTier.Proposal,
                "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"profile_json\"],\"properties\":{\"profile_json\":{\"type\":\"string\",\"description\":\"Chuỗi JSON định nghĩa ConventionProfile nháp.\"}}}",
                WriteDraftProfile));

            registry.Add(new ToolDefinition("profile_draft_test",
                "Chạy thử hồ sơ nháp trên các đối tượng đã đọc của bản vẽ đang mở, trả về chỉ số nhận diện: tỷ lệ phân loại thực thể, tỷ lệ nhãn khớp mẫu, số đoạn chạy không nhãn, và các mẫu không khớp.",
                ToolTier.Proposal,
                "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"sample_limit\"],\"properties\":{\"sample_limit\":{\"type\":[\"integer\",\"null\"],\"minimum\":1,\"maximum\":50,\"description\":\"Số lượng ca mẫu không khớp trả về. Null để lấy mặc định 5.\"}}}",
                TestDraftProfile));

            return registry;
        }

        private static ToolResult WriteDraftProfile(JsonElement input, ToolContext context)
        {
            var json = input.GetProperty("profile_json").GetString();
            if (string.IsNullOrWhiteSpace(json))
            {
                return ToolResult.Fail("Chuỗi JSON hồ sơ nháp không được rỗng.", "Truyền chuỗi JSON hợp lệ của ConventionProfile.");
            }

            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(json!);
                if (node is System.Text.Json.Nodes.JsonObject obj)
                {
                    if (!obj.ContainsKey("schemaVersion"))
                    {
                        obj["schemaVersion"] = PipingAgent.Core.CoreInfo.ProfileSchemaVersion;
                    }
                    json = obj.ToJsonString();
                }
            }
            catch
            {
                // Để ProfileLoader.Parse báo lỗi cú pháp cụ thể
            }

            var loadResult = ProfileLoader.Parse(json!);
            if (loadResult.Profile == null || loadResult.Errors.Count > 0)
            {
                var errStr = string.Join("; ", loadResult.Errors.Select(e => e.Path + ": " + e.Message));
                return ToolResult.Fail("Hồ sơ JSON không hợp lệ: " + errStr, "Sửa lại các trường trong JSON theo đúng định dạng ConventionProfile.");
            }

            context.DraftProfile = loadResult.Profile;
            return ToolResult.Success(new
            {
                saved = true,
                profile_id = loadResult.Profile.Id,
                pipe_standard = loadResult.Profile.PipeStandard,
                roles_count = loadResult.Profile.Roles.Count,
                tag_patterns_count = loadResult.Profile.TagPatterns.Count,
                blocks_count = loadResult.Profile.Blocks.Count,
                message = "Đã lưu hồ sơ nháp vào bộ nhớ phiên. Có thể gọi profile_draft_test để kiểm tra chất lượng.",
            });
        }

        private static ToolResult TestDraftProfile(JsonElement input, ToolContext context)
        {
            var profile = context.DraftProfile;
            if (profile == null)
            {
                return ToolResult.Fail("Chưa có hồ sơ nháp nào trong phiên. Hãy gọi profile_draft_write trước.", "Gọi profile_draft_write với chuỗi JSON hồ sơ đề xuất.");
            }

            var limit = input.TryGetProperty("sample_limit", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 5;

            if (context.TestProfileRunner != null)
            {
                var custom = context.TestProfileRunner(profile);
                return ToolResult.Success(custom);
            }

            if (context.Entities == null || context.Entities.Count == 0)
            {
                return ToolResult.Fail("Bản vẽ hiện tại không có đối tượng nào để chạy thử.", "Đảm bảo bản vẽ có đối tượng hoặc kiểm tra drawing_get_summary.");
            }

            var result = RecognitionPipeline.Run(context.Entities, profile, context.Catalog);
            var totalEntities = context.Entities.Count;
            var unclassified = result.Classification.Report.Unclassified;
            var classified = totalEntities - unclassified;
            var classifyRatio = totalEntities > 0 ? Math.Round((double)classified / totalEntities, 4) : 0;

            var totalTags = result.TagReads.Count;
            var matchedTags = result.TagReads.Count(t => t.Matched);
            var tagMatchRatio = totalTags > 0 ? Math.Round((double)matchedTags / totalTags, 4) : 0;

            var totalRuns = result.Model.Runs.Count;
            var unlabeledRuns = result.Model.Runs.Count(r => string.IsNullOrEmpty(r.LineId));

            var unmatchedSamples = result.TagReads
                .Where(t => !t.Matched)
                .Take(limit)
                .Select(t => new { text = t.Entity.Text, layer = t.Entity.Layer })
                .ToList();

            var unlabeledSamples = result.Model.Runs
                .Where(r => string.IsNullOrEmpty(r.LineId))
                .Take(limit)
                .Select(r => new { run_id = r.Id, dn = r.Dn, elevation = r.Elevation?.Raw })
                .ToList();

            return ToolResult.Success(new
            {
                profile_id = profile.Id,
                total_entities = totalEntities,
                classified_entities = classified,
                classification_ratio = classifyRatio,
                total_tags = totalTags,
                matched_tags = matchedTags,
                tag_match_ratio = tagMatchRatio,
                total_runs = totalRuns,
                unlabeled_runs = unlabeledRuns,
                issue_count = result.Model.Issues.Count,
                unmatched_tag_samples = unmatchedSamples,
                unlabeled_run_samples = unlabeledSamples,
            });
        }
    }
}
