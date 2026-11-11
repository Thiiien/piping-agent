using System.Collections.Generic;
using PipingAgent.Core.Geometry;

namespace PipingAgent.Core.Source
{
    /// <summary>Loại đối tượng nguồn sau khi quy về dạng trung lập.</summary>
    public enum SourceKind
    {
        Line,
        Polyline,
        Arc,
        Circle,
        Ellipse,
        Text,
        MText,
        Insert,
        Attribute,
        Leader,
        MultiLeader,
        Dimension,
        Point,
        Hatch,
        Solid,
        Spline,
        Other,
    }

    /// <summary>
    /// Đối tượng nguồn 2D ở dạng trung lập với CAD. Hợp đồng đầu vào của lõi, mục 6.12.2 và 10.4.
    /// Một lớp phẳng, trường nào không áp dụng cho loại đó thì để null. Bộ đọc không được bỏ sót handle,
    /// vì FR-ING-07 cần nó để truy vết. Toạ độ đã quy về mm và hệ toạ độ thế giới.
    /// </summary>
    public sealed class SourceEntity
    {
        /// <summary>Mã định danh trong bản vẽ nguồn, handle của AutoCAD. Duy nhất trong một bản vẽ.</summary>
        public string Handle { get; set; } = string.Empty;

        public SourceKind Kind { get; set; } = SourceKind.Other;

        /// <summary>Tên loại gốc của phần mềm CAD, ví dụ <c>LWPOLYLINE</c>, để hiển thị và gỡ lỗi.</summary>
        public string? NativeType { get; set; }

        public string Layer { get; set; } = "0";

        /// <summary>Màu đã phân giải, chỉ số ACI 1 đến 255. Null nếu không xác định.</summary>
        public int? Color { get; set; }

        /// <summary>Kiểu nét đã phân giải, ví dụ <c>CENTER</c>. Null nếu không xác định.</summary>
        public string? Linetype { get; set; }

        /// <summary>
        /// Đường dẫn block chứa đối tượng, từ ngoài vào trong, mỗi phần tử là handle của lần chèn.
        /// Rỗng với đối tượng nằm trực tiếp trong không gian mô hình.
        /// </summary>
        public List<string> BlockPath { get; set; } = new List<string>();

        /// <summary>Hộp bao trên mặt bằng.</summary>
        public Box2? Bounds { get; set; }

        /// <summary>Đỉnh của đường, polyline, đường dẫn, spline. Với đường thẳng là hai điểm.</summary>
        public List<Point3>? Points { get; set; }

        /// <summary>Độ phình của từng cạnh polyline, cùng chiều dài với Points. Null nếu không có cung.</summary>
        public List<double>? Bulges { get; set; }

        public bool? Closed { get; set; }

        /// <summary>Tâm của cung, tròn, elip, hoặc điểm chèn block, chữ.</summary>
        public Point3? Center { get; set; }

        public double? Radius { get; set; }

        /// <summary>Góc đầu và cuối của cung, độ, ngược chiều kim đồng hồ.</summary>
        public double? StartAngleDeg { get; set; }

        public double? EndAngleDeg { get; set; }

        /// <summary>Bán trục lớn của elip, véc-tơ từ tâm.</summary>
        public Vector3? MajorAxis { get; set; }

        /// <summary>Tỉ số bán trục nhỏ trên bán trục lớn của elip.</summary>
        public double? AxisRatio { get; set; }

        /// <summary>Nội dung chữ đã bỏ mã định dạng, ví dụ <c>%%U</c>, <c>\P</c>. Áp dụng cho Text, MText, Attribute, Dimension, MultiLeader.</summary>
        public string? Text { get; set; }

        /// <summary>Nội dung chữ nguyên văn, còn mã định dạng.</summary>
        public string? RawText { get; set; }

        public double? TextHeight { get; set; }

        /// <summary>Góc xoay của chữ hoặc block, độ.</summary>
        public double? RotationDeg { get; set; }

        /// <summary>Tên block với Insert, thẻ thuộc tính với Attribute.</summary>
        public string? Name { get; set; }

        /// <summary>Tỉ lệ chèn block theo ba trục.</summary>
        public Vector3? Scale { get; set; }

        /// <summary>Thuộc tính của block, thẻ sang giá trị. Chỉ với Insert.</summary>
        public Dictionary<string, string>? Attributes { get; set; }

        /// <summary>Handle của đối tượng mà đường dẫn hoặc kích thước gắn vào, nếu bản vẽ có liên kết.</summary>
        public List<string>? AttachedTo { get; set; }

        /// <summary>Giá trị đo của kích thước, mm.</summary>
        public double? Measurement { get; set; }

        /// <summary>Độ dày nét theo đơn vị bản vẽ, với PDF là điểm. Null nếu nguồn không có. Isometric phân biệt ống bằng nét dày.</summary>
        public double? LineWidth { get; set; }
    }

    /// <summary>Bộ lọc khi đọc đối tượng. Mọi trường null nghĩa là không lọc theo tiêu chí đó.</summary>
    public sealed class EntityFilter
    {
        public static readonly EntityFilter All = new EntityFilter();

        /// <summary>Chỉ lấy các loại này.</summary>
        public List<SourceKind>? Kinds { get; set; }

        /// <summary>Chỉ lấy đối tượng trên các layer này, không phân biệt hoa thường.</summary>
        public List<string>? Layers { get; set; }

        /// <summary>Bỏ qua các layer này.</summary>
        public List<string>? ExcludeLayers { get; set; }

        /// <summary>Chỉ lấy đối tượng có hộp bao giao với vùng này.</summary>
        public Box2? Region { get; set; }

        /// <summary>Nổ block và trả về cả đối tượng bên trong, kèm BlockPath. Mặc định không.</summary>
        public bool ExplodeBlocks { get; set; }

        /// <summary>Độ sâu nổ block tối đa khi ExplodeBlocks bật. Bản vẽ thật có block lồng đến 13 cấp.</summary>
        public int MaxBlockDepth { get; set; } = 16;

        /// <summary>Không gian giấy cũng đọc. Mặc định chỉ không gian mô hình.</summary>
        public bool IncludePaperSpace { get; set; }
    }
}
