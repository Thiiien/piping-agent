using System;
using System.Collections.Generic;
using System.Linq;

namespace PipingAgent.Core.Ir
{
    /// <summary>Một lỗi hoặc cảnh báo của bộ kiểm tra IR.</summary>
    public sealed class ValidationFinding
    {
        public ValidationFinding(bool isError, string path, string message)
        {
            IsError = isError;
            Path = path;
            Message = message;
        }

        /// <summary>Lỗi thì IR không dùng được. Cảnh báo thì dùng được nhưng thiếu thông tin.</summary>
        public bool IsError { get; }

        /// <summary>Đường dẫn tới trường sai, ví dụ <c>segments[2].nodeA</c>.</summary>
        public string Path { get; }

        public string Message { get; }

        public override string ToString() => $"{(IsError ? "LỖI" : "CẢNH BÁO")} {Path}: {Message}";
    }

    /// <summary>
    /// Kiểm tra tính nhất quán của IR mà lược đồ JSON không diễn tả được: định danh duy nhất,
    /// tham chiếu tồn tại, điểm tin cậy trong khoảng, quyết định của agent có lý do.
    /// Chỉ đọc, không sửa mô hình.
    /// </summary>
    public static class ModelValidator
    {
        public static IReadOnlyList<ValidationFinding> Validate(PipingModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var findings = new List<ValidationFinding>();

            if (model.SchemaVersion != CoreInfo.IrSchemaVersion)
            {
                findings.Add(new ValidationFinding(false, "schemaVersion",
                    $"Phiên bản {model.SchemaVersion} khác phiên bản hiện hành {CoreInfo.IrSchemaVersion}."));
            }

            if (model.Units != CoreInfo.Units)
            {
                findings.Add(new ValidationFinding(true, "units", $"Đơn vị phải là {CoreInfo.Units}, gặp '{model.Units}'."));
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            void CheckId(string id, string path)
            {
                if (string.IsNullOrWhiteSpace(id)) findings.Add(new ValidationFinding(true, path, "Thiếu định danh."));
                else if (!ids.Add(id)) findings.Add(new ValidationFinding(true, path, $"Định danh '{id}' bị trùng."));
            }

            for (var i = 0; i < model.Lines.Count; i++) CheckId(model.Lines[i].Id, $"lines[{i}].id");
            for (var i = 0; i < model.Runs.Count; i++) CheckId(model.Runs[i].Id, $"runs[{i}].id");
            for (var i = 0; i < model.Segments.Count; i++) CheckId(model.Segments[i].Id, $"segments[{i}].id");
            for (var i = 0; i < model.Nodes.Count; i++) CheckId(model.Nodes[i].Id, $"nodes[{i}].id");
            for (var i = 0; i < model.Components.Count; i++) CheckId(model.Components[i].Id, $"components[{i}].id");
            for (var i = 0; i < model.Equipment.Count; i++) CheckId(model.Equipment[i].Id, $"equipment[{i}].id");
            for (var i = 0; i < model.Structures.Count; i++) CheckId(model.Structures[i].Id, $"structures[{i}].id");
            for (var i = 0; i < model.Issues.Count; i++) CheckId(model.Issues[i].Id, $"issues[{i}].id");
            for (var i = 0; i < model.Decisions.Count; i++) CheckId(model.Decisions[i].Id, $"decisions[{i}].id");

            var lineIds = new HashSet<string>(model.Lines.Select(l => l.Id), StringComparer.Ordinal);
            var runIds = new HashSet<string>(model.Runs.Select(r => r.Id), StringComparer.Ordinal);
            var segmentIds = new HashSet<string>(model.Segments.Select(s => s.Id), StringComparer.Ordinal);
            var nodeIds = new HashSet<string>(model.Nodes.Select(n => n.Id), StringComparer.Ordinal);
            var equipmentIds = new HashSet<string>(model.Equipment.Select(e => e.Id), StringComparer.Ordinal);

            void CheckRef(string? id, HashSet<string> set, string path, string kind, bool required)
            {
                if (string.IsNullOrEmpty(id))
                {
                    if (required) findings.Add(new ValidationFinding(true, path, $"Thiếu tham chiếu tới {kind}."));
                    return;
                }

                if (!set.Contains(id!)) findings.Add(new ValidationFinding(true, path, $"Không có {kind} nào có định danh '{id}'."));
            }

            void CheckProv(Provenance? prov, string path)
            {
                if (prov == null)
                {
                    findings.Add(new ValidationFinding(false, path, "Thiếu nguồn gốc prov."));
                    return;
                }

                if (prov.Confidence < 0 || prov.Confidence > 1)
                {
                    findings.Add(new ValidationFinding(true, path + ".confidence", "Điểm tin cậy phải trong [0, 1]."));
                }

                if (!prov.Inferred && prov.Sources.Count == 0)
                {
                    findings.Add(new ValidationFinding(false, path + ".sources", "Phần tử không suy ra mà không có đối tượng nguồn."));
                }
            }

            for (var i = 0; i < model.Runs.Count; i++)
            {
                var run = model.Runs[i];
                CheckRef(run.LineId, lineIds, $"runs[{i}].lineId", "tuyến", true);
                if (run.PlanPoints.Count < 2) findings.Add(new ValidationFinding(true, $"runs[{i}].planPoints", "Đoạn chạy cần ít nhất hai điểm."));
                if (run.Dn < 0) findings.Add(new ValidationFinding(true, $"runs[{i}].dn", "DN không được âm."));
                if (run.Elevation.Datum == ElevationDatum.Unknown && run.Elevation.Centerline.HasValue)
                {
                    findings.Add(new ValidationFinding(false, $"runs[{i}].elevation", "Có cao độ tâm nhưng mốc chưa rõ."));
                }

                CheckProv(run.Prov, $"runs[{i}].prov");
            }

            for (var i = 0; i < model.Segments.Count; i++)
            {
                var s = model.Segments[i];
                CheckRef(s.LineId, lineIds, $"segments[{i}].lineId", "tuyến", true);
                CheckRef(s.RunId, runIds, $"segments[{i}].runId", "đoạn chạy", false);
                CheckRef(s.NodeA, nodeIds, $"segments[{i}].nodeA", "nút", true);
                CheckRef(s.NodeB, nodeIds, $"segments[{i}].nodeB", "nút", true);
                if (s.NodeA == s.NodeB && !string.IsNullOrEmpty(s.NodeA))
                {
                    findings.Add(new ValidationFinding(true, $"segments[{i}]", "Hai đầu đoạn ống trùng một nút."));
                }

                if (s.Length < 0) findings.Add(new ValidationFinding(true, $"segments[{i}].length", "Chiều dài không được âm."));
                if (s.TrimA + s.TrimB > s.Length && s.Length > 0)
                {
                    findings.Add(new ValidationFinding(false, $"segments[{i}]", "Phụ kiện hai đầu chiếm hết chiều dài đoạn."));
                }

                CheckProv(s.Prov, $"segments[{i}].prov");
            }

            for (var i = 0; i < model.Nodes.Count; i++)
            {
                var n = model.Nodes[i];
                for (var j = 0; j < n.Segments.Count; j++)
                {
                    CheckRef(n.Segments[j], segmentIds, $"nodes[{i}].segments[{j}]", "đoạn ống", true);
                }

                if (n.Kind == NodeKind.Nozzle) CheckRef(n.EquipmentId, equipmentIds, $"nodes[{i}].equipmentId", "thiết bị", true);
                else if (n.EquipmentId != null) CheckRef(n.EquipmentId, equipmentIds, $"nodes[{i}].equipmentId", "thiết bị", false);

                if (n.Kind == NodeKind.Elbow && n.Segments.Count != 2)
                    findings.Add(new ValidationFinding(false, $"nodes[{i}].segments", "Co phải nối đúng hai đoạn."));
                if (n.Kind == NodeKind.Tee && n.Segments.Count != 3)
                    findings.Add(new ValidationFinding(false, $"nodes[{i}].segments", "Tê phải nối đúng ba đoạn."));
                CheckProv(n.Prov, $"nodes[{i}].prov");
            }

            for (var i = 0; i < model.Components.Count; i++)
            {
                var c = model.Components[i];
                CheckRef(c.SegmentId, segmentIds, $"components[{i}].segmentId", "đoạn ống", true);
                if (string.IsNullOrWhiteSpace(c.Type)) findings.Add(new ValidationFinding(true, $"components[{i}].type", "Thiếu loại cấu kiện."));
                if (c.Attributes != null)
                {
                    foreach (var pair in c.Attributes)
                    {
                        if (pair.Value.Confidence < 0 || pair.Value.Confidence > 1)
                            findings.Add(new ValidationFinding(true, $"components[{i}].attributes.{pair.Key}.confidence", "Điểm tin cậy phải trong [0, 1]."));
                    }
                }

                CheckProv(c.Prov, $"components[{i}].prov");
            }

            for (var i = 0; i < model.Equipment.Count; i++)
            {
                var e = model.Equipment[i];
                if (string.IsNullOrWhiteSpace(e.Type)) findings.Add(new ValidationFinding(true, $"equipment[{i}].type", "Thiếu loại thiết bị."));
                var nozzleIds = new HashSet<string>(StringComparer.Ordinal);
                for (var j = 0; j < e.Nozzles.Count; j++)
                {
                    if (!nozzleIds.Add(e.Nozzles[j].Id))
                        findings.Add(new ValidationFinding(true, $"equipment[{i}].nozzles[{j}].id", "Định danh đầu chờ trùng trong cùng thiết bị."));
                }

                CheckProv(e.Prov, $"equipment[{i}].prov");
            }

            var allElementIds = new HashSet<string>(ids, StringComparer.Ordinal);
            for (var i = 0; i < model.Issues.Count; i++)
            {
                var issue = model.Issues[i];
                if (string.IsNullOrWhiteSpace(issue.RuleId)) findings.Add(new ValidationFinding(true, $"issues[{i}].ruleId", "Thiếu mã luật."));
                if (string.IsNullOrWhiteSpace(issue.Message)) findings.Add(new ValidationFinding(false, $"issues[{i}].message", "Thiếu mô tả cho người dùng."));
                for (var j = 0; j < issue.Elements.Count; j++)
                {
                    if (!allElementIds.Contains(issue.Elements[j]))
                        findings.Add(new ValidationFinding(true, $"issues[{i}].elements[{j}]", $"Không có phần tử '{issue.Elements[j]}'."));
                }

                if (issue.Status == IssueStatus.Waived && issue.Waiver == null)
                    findings.Add(new ValidationFinding(true, $"issues[{i}].waiver", "Vấn đề đã bỏ qua phải có người và lý do."));
            }

            for (var i = 0; i < model.Decisions.Count; i++)
            {
                var d = model.Decisions[i];
                if (d.By == DecidedBy.Agent)
                {
                    if (string.IsNullOrWhiteSpace(d.Rationale))
                        findings.Add(new ValidationFinding(true, $"decisions[{i}].rationale", "Quyết định của agent phải có lý do."));
                    if (!d.Confidence.HasValue)
                        findings.Add(new ValidationFinding(true, $"decisions[{i}].confidence", "Quyết định của agent phải có điểm tin cậy."));
                }

                if (d.Confidence.HasValue && (d.Confidence < 0 || d.Confidence > 1))
                    findings.Add(new ValidationFinding(true, $"decisions[{i}].confidence", "Điểm tin cậy phải trong [0, 1]."));
                if (d.Target.Count == 0)
                    findings.Add(new ValidationFinding(true, $"decisions[{i}].target", "Quyết định phải có phần tử đích."));
                if (d.Status == DecisionStatus.Approved && string.IsNullOrEmpty(d.ApprovedBy))
                    findings.Add(new ValidationFinding(false, $"decisions[{i}].approvedBy", "Quyết định đã duyệt nhưng không ghi người duyệt."));
            }

            return findings;
        }

        public static bool IsValid(PipingModel model) => Validate(model).All(f => !f.IsError);
    }
}
