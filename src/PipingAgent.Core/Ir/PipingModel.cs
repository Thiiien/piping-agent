using System.Collections.Generic;

namespace PipingAgent.Core.Ir
{
    /// <summary>
    /// Piping IR, mô hình trung gian trung lập với CAD. Gốc của toàn bộ dữ liệu một lần nhận diện.
    /// Theo mục 7.4 của docs/TAI_LIEU_DU_AN.md. Mọi toạ độ mm, trục Z hướng lên.
    /// </summary>
    public sealed class PipingModel
    {
        /// <summary>Bắt buộc có trong JSON. Thiếu thì bộ đọc từ chối, vì không biết đọc theo lược đồ nào.</summary>
        [System.Text.Json.Serialization.JsonRequired]
        public string SchemaVersion { get; set; } = CoreInfo.IrSchemaVersion;

        [System.Text.Json.Serialization.JsonRequired]
        public string Units { get; set; } = CoreInfo.Units;

        public SourceInfo Source { get; set; } = new SourceInfo();

        public ProfileRef? Profile { get; set; }

        public GeneratorInfo? Generator { get; set; }

        public List<Line> Lines { get; set; } = new List<Line>();

        public List<Run> Runs { get; set; } = new List<Run>();

        public List<Segment> Segments { get; set; } = new List<Segment>();

        public List<Node> Nodes { get; set; } = new List<Node>();

        public List<Component> Components { get; set; } = new List<Component>();

        public List<Equipment> Equipment { get; set; } = new List<Equipment>();

        public List<Structure> Structures { get; set; } = new List<Structure>();

        public List<Issue> Issues { get; set; } = new List<Issue>();

        public List<Decision> Decisions { get; set; } = new List<Decision>();
    }

    /// <summary>Bản vẽ nguồn của mô hình.</summary>
    public sealed class SourceInfo
    {
        /// <summary>Tên hoặc đường dẫn tệp bản vẽ, chỉ để hiển thị. Lõi không mở tệp này.</summary>
        public string? File { get; set; }

        /// <summary>Dấu vân tay nội dung, dạng <c>sha256:...</c>, để biết bản vẽ đã đổi hay chưa.</summary>
        public string? Fingerprint { get; set; }

        /// <summary>Thời điểm đọc, ISO 8601.</summary>
        public string? ReadAt { get; set; }

        public int? EntityCount { get; set; }

        /// <summary>Vùng xử lý trên mặt bằng, null nếu cả bản vẽ.</summary>
        public Geometry.Box2? Region { get; set; }

        /// <summary>Tờ vẽ đã chọn khi một tệp chứa nhiều tờ. Xem KHAO_SAT_BAN_VE_THAT.md mục 3.5.</summary>
        public string? Sheet { get; set; }

        /// <summary>Đơn vị bản vẽ đã xác định hay còn cần người dùng xác nhận. Null nếu bộ đọc không báo. Luật R-GEN-02.</summary>
        public bool? UnitsNeedConfirmation { get; set; }
    }

    /// <summary>Hồ sơ quy ước đã dùng.</summary>
    public sealed class ProfileRef
    {
        public string Id { get; set; } = string.Empty;

        public int Version { get; set; }
    }

    /// <summary>Phần mềm đã sinh ra mô hình.</summary>
    public sealed class GeneratorInfo
    {
        public string Name { get; set; } = "PipingAgent";

        public string Version { get; set; } = string.Empty;

        /// <summary>Danh mục ống dùng khi tạo mô hình đã được ký xác nhận chưa. Null nếu không dùng danh mục.</summary>
        public bool? CatalogVerified { get; set; }
    }
}
