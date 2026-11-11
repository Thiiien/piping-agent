using System.Collections.Generic;
using System.Text.Json.Serialization;
using PipingAgent.Core.Geometry;

namespace PipingAgent.Core.Ir
{
    /// <summary>Tuyến ống, nhóm các đoạn chạy cùng số hiệu.</summary>
    public sealed class Line
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>Số hiệu tuyến như trên bản vẽ, ví dụ <c>P-1011</c> hoặc dạng đầy đủ của nhà thầu.</summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>Mã lưu chất, ví dụ <c>LPS</c>.</summary>
        public string? Service { get; set; }

        /// <summary>Cấp ống, ví dụ <c>A1AS</c>.</summary>
        public string? Spec { get; set; }

        public Insulation? Insulation { get; set; }

        public Provenance? Prov { get; set; }
    }

    public sealed class Insulation
    {
        /// <summary>Mã loại bảo ôn, ví dụ <c>HS</c>, <c>HC</c>, <c>NI</c>.</summary>
        public string? Type { get; set; }

        /// <summary>Chiều dày, mm. Null nếu bản vẽ chưa ghi.</summary>
        public double? Thickness { get; set; }
    }

    /// <summary>
    /// Mốc của giá trị cao độ ghi trên bản vẽ. Bản vẽ thật ghi theo đáy ống hoặc mặt giá đỡ nhiều hơn tâm ống,
    /// xem KHAO_SAT_BAN_VE_THAT.md mục 3.3.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ElevationDatum
    {
        /// <summary>Tâm ống.</summary>
        CL,

        /// <summary>Đáy ống, bottom of pipe.</summary>
        BOP,

        /// <summary>Đỉnh ống, top of pipe.</summary>
        TOP,

        /// <summary>Mặt trên giá đỡ, top of steel. Tâm ống bằng TOS cộng chiều cao đỡ cộng nửa đường kính ngoài.</summary>
        TOS,

        /// <summary>Chưa rõ mốc. Bộ luật phải báo vấn đề trước khi dựng.</summary>
        Unknown,
    }

    public sealed class Elevation
    {
        /// <summary>Giá trị ghi trên bản vẽ, mm.</summary>
        public double Raw { get; set; }

        public ElevationDatum Datum { get; set; } = ElevationDatum.CL;

        /// <summary>Cao độ tâm ống đã quy đổi, mm. Null khi chưa quy đổi được, ví dụ thiếu đường kính.</summary>
        public double? Centerline { get; set; }

        /// <summary>Chiều cao đỡ dùng khi quy đổi từ TOS, mm.</summary>
        public double? SupportHeight { get; set; }
    }

    /// <summary>Đoạn chạy: một polyline hoặc chuỗi nét trên mặt bằng có cùng cao độ và đường kính.</summary>
    public sealed class Run
    {
        public string Id { get; set; } = string.Empty;

        public string LineId { get; set; } = string.Empty;

        /// <summary>Đường kính danh nghĩa, mm. 0 nếu chưa biết.</summary>
        public int Dn { get; set; }

        public Elevation Elevation { get; set; } = new Elevation();

        public List<Point2> PlanPoints { get; set; } = new List<Point2>();

        /// <summary>
        /// Cao độ tâm tại đầu cuối khi đoạn chạy thẳng đứng, mm. Isometric cho biết ống đứng tường minh, kể cả nhánh đứng cụt
        /// không có đoạn ngang ở đáy. Khi có giá trị, <see cref="PlanPoints"/> chỉ cần một điểm và <see cref="Elevation"/> là cao độ đầu.
        /// </summary>
        public double? EndElevation { get; set; }

        public TagRef? TagRef { get; set; }

        /// <summary>Nguồn gốc. Bắt buộc với phần tử do bộ nhận diện tạo ra, bộ kiểm tra sẽ cảnh báo nếu thiếu.</summary>
        public Provenance? Prov { get; set; }
    }

    public enum SegmentKind
    {
        Horizontal,
        Riser,
        Sloped,
    }

    /// <summary>Đoạn ống thẳng giữa hai nút.</summary>
    public sealed class Segment
    {
        public string Id { get; set; } = string.Empty;

        public string LineId { get; set; } = string.Empty;

        /// <summary>Đoạn chạy chứa nó. Null với ống đứng suy ra.</summary>
        public string? RunId { get; set; }

        public string NodeA { get; set; } = string.Empty;

        public string NodeB { get; set; } = string.Empty;

        public SegmentKind Kind { get; set; }

        public int Dn { get; set; }

        /// <summary>Đường kính ngoài tra từ danh mục, mm.</summary>
        public double Od { get; set; }

        /// <summary>Chiều dài giữa hai nút, mm.</summary>
        public double Length { get; set; }

        /// <summary>Phần bị phụ kiện ở đầu A chiếm chỗ, mm.</summary>
        public double TrimA { get; set; }

        public double TrimB { get; set; }

        /// <summary>Nguồn gốc. Bắt buộc với phần tử do bộ nhận diện tạo ra, bộ kiểm tra sẽ cảnh báo nếu thiếu.</summary>
        public Provenance? Prov { get; set; }
    }

    public enum NodeKind
    {
        Elbow,
        Tee,
        Reducer,
        Nozzle,
        OpenEnd,
        Continuation,
    }

    /// <summary>Nút: giao điểm các đường tâm, nơi đặt co, tê, côn thu, hoặc đầu chờ.</summary>
    public sealed class Node
    {
        public string Id { get; set; } = string.Empty;

        public Point3 Position { get; set; }

        public NodeKind Kind { get; set; }

        /// <summary>Các đoạn ống nối vào.</summary>
        public List<string> Segments { get; set; } = new List<string>();

        /// <summary>Theo loại: góc và bán kính co, DN nhánh của tê, hai DN của côn thu.</summary>
        public Dictionary<string, double>? Params { get; set; }

        public string? EquipmentId { get; set; }

        public string? NozzleId { get; set; }

        /// <summary>Mục tương ứng trong danh mục, ví dụ <c>B16.9/ELL90LR/DN150</c>.</summary>
        public string? CatalogRef { get; set; }

        /// <summary>Nguồn gốc. Bắt buộc với phần tử do bộ nhận diện tạo ra, bộ kiểm tra sẽ cảnh báo nếu thiếu.</summary>
        public Provenance? Prov { get; set; }
    }

    /// <summary>Một thuộc tính của cấu kiện kèm nguồn gốc riêng. Mục 7.9.</summary>
    public sealed class AttributeValue
    {
        public string Value { get; set; } = string.Empty;

        /// <summary>Nguồn: <c>block_attribute</c>, <c>leader</c>, <c>adjacent</c>, <c>nearest</c>, <c>legend</c>, <c>user</c>.</summary>
        public string By { get; set; } = string.Empty;

        public string? SourceHandle { get; set; }

        /// <summary>Điểm tin cậy 0 đến 1. Null nghĩa là chưa chấm, coi như 1.</summary>
        public double? Confidence { get; set; }
    }

    /// <summary>Cấu kiện nối tiếp trên ống: van, cặp bích, lọc.</summary>
    public sealed class Component
    {
        public string Id { get; set; } = string.Empty;

        public string SegmentId { get; set; } = string.Empty;

        /// <summary>
        /// Loại, danh sách mở: <c>valve_gate</c>, <c>valve_globe</c>, <c>valve_check</c>, <c>valve_ball</c>,
        /// <c>flange_pair</c>, <c>strainer</c>, <c>unclassified</c> và các loại khác theo hồ sơ quy ước.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        public Point3 Position { get; set; }

        /// <summary>Hướng trục ống tại đó.</summary>
        public Vector3 Axis { get; set; } = Vector3.UnitX;

        public int Dn { get; set; }

        /// <summary>Chiều dài tra từ danh mục, mm.</summary>
        public double Length { get; set; }

        /// <summary>Số hiệu cấu kiện, ví dụ số van.</summary>
        public string? Tag { get; set; }

        public TagRef? TagRef { get; set; }

        public Dictionary<string, AttributeValue>? Attributes { get; set; }

        /// <summary>Nguồn gốc. Bắt buộc với phần tử do bộ nhận diện tạo ra, bộ kiểm tra sẽ cảnh báo nếu thiếu.</summary>
        public Provenance? Prov { get; set; }
    }

    /// <summary>Đầu chờ của thiết bị.</summary>
    public sealed class Nozzle
    {
        public string Id { get; set; } = string.Empty;

        public Point3 Position { get; set; }

        /// <summary>Hướng ra khỏi thiết bị.</summary>
        public Vector3 Direction { get; set; } = Vector3.UnitX;

        public int Dn { get; set; }
    }

    /// <summary>Thiết bị. Loại tra trong thư viện thiết bị, mục 7.8.</summary>
    public sealed class Equipment
    {
        public string Id { get; set; } = string.Empty;

        public string Tag { get; set; } = string.Empty;

        /// <summary>Mã định nghĩa trong thư viện thiết bị, ví dụ <c>tank_vertical</c>. <c>generic_box</c> là dự phòng.</summary>
        public string Type { get; set; } = "generic_box";

        public Placement Geometry { get; set; } = new Placement();

        public List<Nozzle> Nozzles { get; set; } = new List<Nozzle>();

        /// <summary>Nguồn gốc. Bắt buộc với phần tử do bộ nhận diện tạo ra, bộ kiểm tra sẽ cảnh báo nếu thiếu.</summary>
        public Provenance? Prov { get; set; }
    }

    /// <summary>Vị trí, góc xoay và tham số hình học của một thiết bị hoặc kết cấu.</summary>
    public sealed class Placement
    {
        public Point3 Origin { get; set; }

        /// <summary>Góc xoay quanh trục Z, độ.</summary>
        public double RotationDeg { get; set; }

        /// <summary>Giá trị các tham số mà định nghĩa loại yêu cầu, ví dụ <c>D</c>, <c>H</c>.</summary>
        public Dictionary<string, double> Params { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>Kết cấu: cột, dầm. Giá đỡ ống đặt lên đây.</summary>
    public sealed class Structure
    {
        public string Id { get; set; } = string.Empty;

        /// <summary><c>column</c> hoặc <c>beam</c>.</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Hai điểm: với cột là chân và đỉnh, với dầm là hai đầu.</summary>
        public List<Point3> Points { get; set; } = new List<Point3>();

        /// <summary>Tiết diện, ví dụ <c>H300x300</c>.</summary>
        public string? Section { get; set; }

        /// <summary>Cao độ mặt trên kết cấu, mm.</summary>
        public double? Tos { get; set; }

        /// <summary>Nguồn gốc. Bắt buộc với phần tử do bộ nhận diện tạo ra, bộ kiểm tra sẽ cảnh báo nếu thiếu.</summary>
        public Provenance? Prov { get; set; }
    }
}
