using System.Collections.Generic;
using System.Text.Json.Serialization;
using PipingAgent.Core.Ir;

namespace PipingAgent.Core.Profile
{
    /// <summary>
    /// Hồ sơ quy ước vẽ của một dự án hoặc nhà thầu, mục 7.7 của tài liệu dự án. Tệp JSON đi kèm IR.
    /// Mọi quy tắc nhận diện phụ thuộc quy ước đều lấy từ đây, không viết cứng trong mã (FR-PRF-01).
    /// </summary>
    public sealed class ConventionProfile
    {
        [JsonRequired]
        public string SchemaVersion { get; set; } = CoreInfo.ProfileSchemaVersion;

        /// <summary>Định danh, dùng làm tên tệp và để hồ sơ khác <see cref="Extends"/>.</summary>
        [JsonRequired]
        public string Id { get; set; } = string.Empty;

        public int Version { get; set; } = 1;

        /// <summary>Định danh hồ sơ cha. Quy tắc của hồ sơ con được xét trước quy tắc của cha.</summary>
        public string? Extends { get; set; }

        /// <summary>Đơn vị của các dung sai và bán kính trong hồ sơ. Hiện chỉ hỗ trợ <c>mm</c>.</summary>
        public string Units { get; set; } = "mm";

        public string? PipeStandard { get; set; }

        public string? FittingStandard { get; set; }

        /// <summary>Tiêu chuẩn chiều dài van, mặc định <c>ASME-B16.10</c>.</summary>
        public string? ValveStandard { get; set; }

        private ProfileTolerances? _tolerances;

        /// <summary>Dung sai. Hồ sơ con không khai báo thì kế thừa của cha khi hợp nhất.</summary>
        public ProfileTolerances Tolerances
        {
            get => _tolerances ??= new ProfileTolerances();
            set => _tolerances = value;
        }

        [JsonIgnore]
        public bool HasTolerances => _tolerances != null;

        /// <summary>Quy tắc gán vai trò cho đối tượng. Xét theo thứ tự, quy tắc đầu tiên khớp thắng.</summary>
        public List<RoleRule> Roles { get; set; } = new List<RoleRule>();

        /// <summary>Mẫu nhãn tuyến ống. Xét theo thứ tự.</summary>
        public List<TagPattern> TagPatterns { get; set; } = new List<TagPattern>();

        /// <summary>
        /// Mẫu chữ cao độ ghi rời, không kèm số hiệu tuyến, ví dụ <c>EL 109000 BOP</c> trên bản vẽ thật.
        /// Chỉ cần nhóm <c>el</c>, có thể có <c>datum</c>. Xét sau <see cref="TagPatterns"/>.
        /// </summary>
        public List<TagPattern> ElevationPatterns { get; set; } = new List<TagPattern>();

        /// <summary>Ánh xạ tên block sang loại cấu kiện, kết cấu hoặc thiết bị. Tên có ký tự đại diện, không phân biệt hoa thường.</summary>
        public List<BlockRule> Blocks { get; set; } = new List<BlockRule>();

        public List<TagPattern> ComponentTagPatterns { get; set; } = new List<TagPattern>();

        public List<EquipmentRule> EquipmentRules { get; set; } = new List<EquipmentRule>();

        public StructureRules? StructureRules { get; set; }

        /// <summary>Mức của từng luật kiểm tra, khoá là mã luật, giá trị là <c>blocker</c>, <c>warning</c>, <c>info</c> hoặc <c>off</c>.</summary>
        public Dictionary<string, string> Rules { get; set; } = new Dictionary<string, string>();
    }

    public sealed class ProfileTolerances
    {
        /// <summary>Dung sai bắt điểm, mm. Hai đầu mút cách nhau không quá giá trị này coi là một.</summary>
        public double Snap { get; set; } = 1.0;

        /// <summary>Hai đoạn nối tiếp lệch góc không quá giá trị này thì gộp thành một, độ.</summary>
        public double CollinearDeg { get; set; } = 0.5;

        /// <summary>Bán kính tìm đoạn chạy quanh một nhãn, mm.</summary>
        public double TagSearchRadius { get; set; } = 1500;

        /// <summary>Đầu đường dẫn cách đoạn chạy không quá giá trị này thì coi là chạm, mm.</summary>
        public double LeaderTouch { get; set; } = 10;

        /// <summary>Nhãn song song cách đoạn chạy không quá chiều cao chữ nhân hệ số này thì coi là nằm sát.</summary>
        public double TagAdjacentFactor { get; set; } = 2.5;

        /// <summary>Nét ngắn hơn giá trị này bị loại khi làm sạch, mm. Null thì dùng <see cref="Snap"/>.</summary>
        public double? MinSegmentLength { get; set; }

        /// <summary>Tỷ lệ chưa phân loại vượt ngưỡng này thì luật R-GEN-01 báo, 0 đến 1.</summary>
        public double UnclassifiedWarning { get; set; } = 0.30;

        /// <summary>
        /// Chiều cao đỡ mặc định, mm, dùng khi cao độ ghi theo mặt trên giá đỡ (TOS) mà nhãn không ghi chiều cao đỡ.
        /// Null thì TOS không quy đổi được và tạo vấn đề R-ELV-05.
        /// </summary>
        public double? DefaultSupportHeight { get; set; }

        /// <summary>
        /// Bán kính tìm đầu mút đoạn chạy quanh một block phụ kiện để nối hai đoạn bị ngắt tại block, mm.
        /// Bản vẽ thật vẽ ống bằng nét rời dừng tại mép ký hiệu co, tê. 0 thì tắt.
        /// </summary>
        public double FittingReach { get; set; } = 0;

        /// <summary>
        /// Ngưỡng nghi ngờ khe hở giữa hai đầu mút đoạn chạy, mm, luật R-CON-02. Khe lớn hơn dung sai bắt điểm nhưng nhỏ hơn
        /// ngưỡng này được khép lại và ghi vấn đề mức thông tin. 0 thì tắt.
        /// </summary>
        public double GapTolerance { get; set; } = 0;

        /// <summary>Cấu kiện cách trục ống không quá giá trị này thì coi là nằm trên ống, mm. Null thì dùng dung sai bắt điểm.</summary>
        public double ComponentTolerance { get; set; } = 5;

        /// <summary>Đầu ống cách biên thiết bị không quá giá trị này thì là đầu chờ, mm.</summary>
        public double NozzleTolerance { get; set; } = 5;

        /// <summary>Đầu ống cách biên thiết bị trong khoảng này mà không chạm thì R-CON-05, mm.</summary>
        public double NearEquipment { get; set; } = 300;

        /// <summary>Chiều cao mặc định của thiết bị và kết cấu khi bản vẽ không ghi, mm.</summary>
        public double DefaultEquipmentHeight { get; set; } = 2000;

        /// <summary>Cạnh tiết diện mặc định của cột và dầm, mm.</summary>
        public double DefaultSectionSize { get; set; } = 300;

        /// <summary>Khoảng cao độ tâm hợp lý, mm, hai số. Ngoài khoảng thì R-ELV-03. Null thì không kiểm tra.</summary>
        public List<double>? ElevationRange { get; set; }

        /// <summary>Góc đổi hướng cho phép tại co, độ. Góc khác tạo vấn đề R-GEO-01.</summary>
        public List<double> AllowedBendAngles { get; set; } = new List<double> { 90, 45 };
    }

    public sealed class RoleRule
    {
        [JsonRequired]
        public string Role { get; set; } = string.Empty;

        [JsonRequired]
        public EntityMatch Match { get; set; } = new EntityMatch();
    }

    /// <summary>Điều kiện khớp một đối tượng nguồn. Mọi trường có giá trị đều phải thoả.</summary>
    public sealed class EntityMatch
    {
        /// <summary>Tên layer, có ký tự đại diện <c>*</c> và <c>?</c>, không phân biệt hoa thường.</summary>
        public string? Layer { get; set; }

        /// <summary>Nhiều tên layer, khớp một trong số đó.</summary>
        public List<string>? Layers { get; set; }

        /// <summary>Loại đối tượng: tên gốc của CAD như <c>LWPOLYLINE</c> hoặc tên trung lập như <c>polyline</c>.</summary>
        public List<string>? Types { get; set; }

        /// <summary>Tên block với INSERT, có ký tự đại diện.</summary>
        public string? Block { get; set; }

        /// <summary>Chỉ số màu ACI đã phân giải.</summary>
        public int? Color { get; set; }

        /// <summary>Kiểu nét, có ký tự đại diện.</summary>
        public string? Linetype { get; set; }

        /// <summary>Biểu thức chính quy trên nội dung chữ đã bỏ mã định dạng.</summary>
        public string? TextPattern { get; set; }
    }

    public sealed class TagPattern
    {
        [JsonRequired]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Biểu thức chính quy .NET với nhóm đặt tên. Nhóm chuẩn: <c>line</c>, <c>dn</c>, <c>el</c>, <c>datum</c>,
        /// <c>service</c>, <c>unit</c>, <c>seq</c>, <c>spec</c>, <c>insul</c>, <c>insulThk</c>, <c>tag</c>. Nhóm khác đưa vào trường tuỳ chọn.
        /// </summary>
        [JsonRequired]
        public string Regex { get; set; } = string.Empty;

        /// <summary>Mốc cao độ mặc định khi mẫu không có nhóm <c>datum</c>.</summary>
        public ElevationDatum? ElevationDatum { get; set; }

        /// <summary>Đơn vị của nhóm <c>dn</c>: <c>mm</c> hoặc <c>inch</c>. Mặc định <c>mm</c>, trừ khi chữ có dấu <c>"</c>.</summary>
        public string? DnUnit { get; set; }

        /// <summary>Đơn vị của nhóm <c>el</c>: <c>mm</c> hoặc <c>m</c>. Mặc định <c>mm</c>.</summary>
        public string? ElevationUnit { get; set; }

        /// <summary>Bán kính tìm đối tượng cho chú thích cấu kiện, mm. Null thì dùng bán kính chung.</summary>
        public double? SearchRadius { get; set; }
    }

    public sealed class BlockRule
    {
        /// <summary>Tên block, có ký tự đại diện, không phân biệt hoa thường.</summary>
        [JsonRequired]
        public string Name { get; set; } = string.Empty;

        /// <summary>Loại cấu kiện, ví dụ <c>valve_gate</c>.</summary>
        public string? Component { get; set; }

        /// <summary>Loại kết cấu, ví dụ <c>column</c>.</summary>
        public string? Structure { get; set; }

        /// <summary>Loại thiết bị trong thư viện, ví dụ <c>pump</c>.</summary>
        public string? Equipment { get; set; }

        /// <summary>Loại ký hiệu phụ trợ, ví dụ <c>flow_arrow</c>, <c>riser</c>. Không dựng.</summary>
        public string? Symbol { get; set; }

        /// <summary>Thẻ thuộc tính block sang trường đích, ví dụ <c>TAG</c> sang <c>tag</c>.</summary>
        public Dictionary<string, string>? Attributes { get; set; }
    }

    public sealed class EquipmentRule
    {
        [JsonRequired]
        public EquipmentMatch Match { get; set; } = new EquipmentMatch();

        [JsonRequired]
        public string Type { get; set; } = string.Empty;
    }

    public sealed class EquipmentMatch
    {
        /// <summary><c>circle</c>, <c>closed_rectangle</c>, <c>closed_polyline</c>, <c>block</c>.</summary>
        public string? Shape { get; set; }

        public string? Layer { get; set; }

        public string? Block { get; set; }

        /// <summary>Tiền tố số hiệu của chữ nằm trong hoặc sát hình bao, ví dụ <c>E-</c>.</summary>
        public string? TagPrefix { get; set; }
    }

    public sealed class StructureRules
    {
        /// <summary>Biểu thức chính quy đọc cao độ mặt trên kết cấu, nhóm <c>tos</c>.</summary>
        public string? TosPattern { get; set; }
    }

    /// <summary>Vai trò chuẩn của đối tượng. Hồ sơ dùng tên dạng snake_case.</summary>
    public static class EntityRoles
    {
        public const string PipeCenterline = "pipe_centerline";
        public const string PipeTag = "pipe_tag";
        public const string PipeTagLeader = "pipe_tag_leader";
        public const string RiserSymbol = "riser_symbol";
        public const string FlowArrow = "flow_arrow";
        public const string InlineComponent = "inline_component";
        public const string ComponentTag = "component_tag";
        public const string Legend = "legend";
        public const string Equipment = "equipment";
        public const string EquipmentTag = "equipment_tag";
        public const string Structure = "structure";
        public const string StructureTag = "structure_tag";
        public const string Dimension = "dimension";
        public const string Annotation = "annotation";
        public const string TitleBlock = "title_block";
        public const string Ignore = "ignore";

        /// <summary>Vai trò gán cho đối tượng không khớp quy tắc nào.</summary>
        public const string Unclassified = "unclassified";

        public static readonly string[] All =
        {
            PipeCenterline, PipeTag, PipeTagLeader, RiserSymbol, FlowArrow, InlineComponent, ComponentTag, Legend,
            Equipment, EquipmentTag, Structure, StructureTag, Dimension, Annotation, TitleBlock, Ignore,
        };

        public static bool IsKnown(string role)
        {
            foreach (var r in All)
            {
                if (r == role) return true;
            }

            return false;
        }
    }
}
