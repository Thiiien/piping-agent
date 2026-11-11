using System.Collections.Generic;
using PipingAgent.Core.Geometry;

namespace PipingAgent.Core.Source
{
    /// <summary>Tóm tắt một bản vẽ, đủ để agent và hồ sơ quy ước định hướng trước khi đọc chi tiết.</summary>
    public sealed class DrawingSummary
    {
        /// <summary>Tên hoặc đường dẫn, chỉ để hiển thị.</summary>
        public string? File { get; set; }

        /// <summary>Dấu vân tay nội dung, <c>sha256:...</c>.</summary>
        public string? Fingerprint { get; set; }

        /// <summary>Phiên bản định dạng gốc, ví dụ <c>AC1032</c>.</summary>
        public string? FormatVersion { get; set; }

        /// <summary>Đơn vị bản vẽ khai báo trong tệp, ví dụ <c>mm</c>, <c>inch</c>, <c>unitless</c>. Bộ đọc đã quy mọi toạ độ về mm.</summary>
        public string? DeclaredUnits { get; set; }

        /// <summary>Hệ số đã nhân vào toạ độ để ra mm.</summary>
        public double UnitScaleToMm { get; set; } = 1.0;

        public int EntityCount { get; set; }

        public Dictionary<string, int> CountsByKind { get; set; } = new Dictionary<string, int>();

        public List<LayerInfo> Layers { get; set; } = new List<LayerInfo>();

        public List<BlockUsage> Blocks { get; set; } = new List<BlockUsage>();

        public Box2? Extents { get; set; }

        /// <summary>Các khung tên tìm thấy, mỗi khung là một tờ. Xem KHAO_SAT_BAN_VE_THAT.md mục 3.5.</summary>
        public List<SheetInfo> Sheets { get; set; } = new List<SheetInfo>();

        public bool HasXrefs { get; set; }

        public bool HasProxyEntities { get; set; }
    }

    public sealed class LayerInfo
    {
        public string Name { get; set; } = string.Empty;

        public int? Color { get; set; }

        public string? Linetype { get; set; }

        public bool IsOff { get; set; }

        public bool IsFrozen { get; set; }

        public int EntityCount { get; set; }

        public Dictionary<string, int> CountsByKind { get; set; } = new Dictionary<string, int>();
    }

    public sealed class BlockUsage
    {
        public string Name { get; set; } = string.Empty;

        public int InsertCount { get; set; }

        public int EntityCount { get; set; }

        public bool HasAttributes { get; set; }

        public int NestedDepth { get; set; }
    }

    /// <summary>Một tờ vẽ trong không gian mô hình, nhận ra từ khung tên.</summary>
    public sealed class SheetInfo
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>Handle lần chèn khung tên.</summary>
        public string? TitleBlockHandle { get; set; }

        public Box2 Frame { get; set; }

        /// <summary>Tỉ lệ vẽ suy ra từ khung, ví dụ 60 với tờ 1/60.</summary>
        public double? Scale { get; set; }

        /// <summary>Thuộc tính khung tên: tên bản vẽ, số tờ, đơn vị.</summary>
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    }
}
