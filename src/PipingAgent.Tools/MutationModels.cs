using System;
using System.Collections.Generic;

namespace PipingAgent.Tools
{
    /// <summary>
    /// Các loại thao tác can thiệp bản vẽ CAD được hỗ trợ ở Giai đoạn 1 (ADR-12).
    /// </summary>
    public static class MutationOpKind
    {
        public const string AddText = "add_text";
        public const string ModifyText = "modify_text";
        public const string ChangeLayer = "change_layer";
        public const string AddRevisionCloud = "add_revision_cloud";
        public const string WaiveIssue = "waive_issue";
        public const string AddPipe = "add_pipe";
    }

    /// <summary>
    /// Trạng thái của một đề xuất can thiệp bản vẽ (Mutation Proposal).
    /// </summary>
    public enum MutationStatus
    {
        Pending,
        Approved,
        Rejected,
        Applied,
        Failed,
    }

    /// <summary>
    /// Chi tiết một thao tác can thiệp cụ thể do Agent đề xuất.
    /// </summary>
    public sealed class MutationOp
    {
        public string Kind { get; set; } = string.Empty;

        public string? Handle { get; set; }

        public string? Text { get; set; }

        public string? NewText { get; set; }

        public string? TargetLayer { get; set; }

        public double? X { get; set; }

        public double? Y { get; set; }

        public double? Height { get; set; }

        public double? MinX { get; set; }

        public double? MinY { get; set; }

        public double? MaxX { get; set; }

        public double? MaxY { get; set; }

        public string? Comment { get; set; }

        public string? IssueId { get; set; }

        public string? Reason { get; set; }

        public double? LengthMm { get; set; }

        public int? Dn { get; set; }

        public double? Z { get; set; }

        public double? EndX { get; set; }

        public double? EndY { get; set; }

        public double? EndZ { get; set; }

        public string? Direction { get; set; }

        public string? Mode { get; set; }

        public string? Tag { get; set; }
    }

    /// <summary>
    /// Đề xuất can thiệp bản vẽ CAD (Mutation Proposal) lưu trong bộ nhớ tạm Staging chờ kỹ sư duyệt (ADR-12).
    /// </summary>
    public sealed class MutationProposal
    {
        public string ProposalId { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public string Rationale { get; set; } = string.Empty;

        public List<MutationOp> Operations { get; set; } = new List<MutationOp>();

        public MutationStatus Status { get; set; } = MutationStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? ErrorMessage { get; set; }

        /// <summary>Đường dẫn hoặc tên bản vẽ nơi đề xuất được sinh ra (Document Target Binding, ADR-14).</summary>
        public string? DrawingPath { get; set; }

        /// <summary>Dấu vân tay hình học của bản vẽ khi đề xuất được tạo (ADR-14).</summary>
        public string? DrawingFingerprint { get; set; }

        /// <summary>Bằng chứng thực thi sau khi đã commit thành công vào CAD (ADR-14).</summary>
        public MutationEvidence? Evidence { get; set; }
    }

    /// <summary>
    /// Bằng chứng thực thi vật lý sau khi commit can thiệp vào CAD ("Tool success is not task success", ADR-14).
    /// </summary>
    public sealed class MutationEvidence
    {
        public string ProposalId { get; set; } = string.Empty;

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        public List<string> CreatedHandles { get; set; } = new List<string>();

        public List<string> ModifiedHandles { get; set; } = new List<string>();

        public double[]? BoundingBoxMin { get; set; }

        public double[]? BoundingBoxMax { get; set; }

        public Dictionary<string, double> MeasuredMetrics { get; set; } = new Dictionary<string, double>();

        public bool AuditPassed { get; set; } = true;

        public string VerificationSummary { get; set; } = string.Empty;
    }
}
