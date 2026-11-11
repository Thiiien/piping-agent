using System.Collections.Generic;
using System.Text.Json;
using PipingAgent.Core.Geometry;

namespace PipingAgent.Core.Ir
{
    public enum Severity
    {
        Blocker,
        Warning,
        Info,
    }

    public enum IssueStatus
    {
        Open,
        Fixed,
        Waived,
    }

    /// <summary>Thao tác của một quyết định. Danh sách đóng theo mục 7.4.</summary>
    public enum DecisionOp
    {
        AssignTag,
        SetDn,
        SetElevation,
        SetNodeKind,
        Connect,
        SplitLine,
        IgnoreSource,
        SetComponentType,
        WaiveIssue,
    }

    public enum DecisionStatus
    {
        Pending,
        Approved,
        Rejected,
    }

    /// <summary>Vấn đề do bộ luật hoặc bộ nhận diện phát hiện. Chương 9.</summary>
    public sealed class Issue
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>Mã luật, ví dụ <c>R-TAG-02</c>.</summary>
        public string RuleId { get; set; } = string.Empty;

        public Severity Severity { get; set; } = Severity.Warning;

        public IssueStatus Status { get; set; } = IssueStatus.Open;

        /// <summary>Định danh phần tử IR liên quan.</summary>
        public List<string> Elements { get; set; } = new List<string>();

        /// <summary>Handle đối tượng nguồn 2D liên quan.</summary>
        public List<string> Sources { get; set; } = new List<string>();

        /// <summary>Vị trí để phóng tới.</summary>
        public Point3? Position { get; set; }

        public string Message { get; set; } = string.Empty;

        /// <summary>Các cách sửa có thể, ở dạng quyết định mẫu.</summary>
        public List<Suggestion> Suggestions { get; set; } = new List<Suggestion>();

        public Waiver? Waiver { get; set; }
    }

    /// <summary>Quyết định mẫu đính kèm một vấn đề. Người dùng chọn thì thành <see cref="Decision"/>.</summary>
    public sealed class Suggestion
    {
        public DecisionOp Op { get; set; }

        /// <summary>Phần tử đích, khoá là loại: <c>run</c>, <c>segment</c>, <c>node</c>, <c>component</c>, <c>issue</c>, <c>source</c>.</summary>
        public Dictionary<string, string> Target { get; set; } = new Dictionary<string, string>();

        /// <summary>Giá trị mới, kiểu tuỳ <see cref="Op"/>.</summary>
        public JsonElement? Value { get; set; }

        public string? Label { get; set; }
    }

    public sealed class Waiver
    {
        public string By { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        /// <summary>ISO 8601.</summary>
        public string At { get; set; } = string.Empty;
    }

    /// <summary>Quyết định của người dùng hoặc agent. Lưu tách khỏi phần tử để chạy lại được, ADR-06.</summary>
    public sealed class Decision
    {
        public string Id { get; set; } = string.Empty;

        public DecisionOp Op { get; set; }

        /// <summary>
        /// Phần tử đích. Ngoài định danh IR nên kèm handle nguồn dưới khoá <c>source</c>
        /// để khớp lại ở lần chạy sau khi định danh IR đổi.
        /// </summary>
        public Dictionary<string, string> Target { get; set; } = new Dictionary<string, string>();

        public JsonElement? Value { get; set; }

        public DecidedBy By { get; set; } = DecidedBy.User;

        public DecisionStatus Status { get; set; } = DecisionStatus.Pending;

        /// <summary>Lý do. Bắt buộc khi <see cref="By"/> là agent.</summary>
        public string? Rationale { get; set; }

        /// <summary>Điểm tin cậy 0 đến 1. Bắt buộc khi <see cref="By"/> là agent.</summary>
        public double? Confidence { get; set; }

        /// <summary>ISO 8601.</summary>
        public string At { get; set; } = string.Empty;

        public string? ApprovedBy { get; set; }

        public string? ApprovedAt { get; set; }

        /// <summary>Đúng khi lần chạy gần nhất không khớp được đối tượng nào cho quyết định này. Không ghi khi sai.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public bool Orphan { get; set; }
    }
}
