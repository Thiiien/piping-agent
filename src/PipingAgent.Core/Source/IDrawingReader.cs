using System.Collections.Generic;
using PipingAgent.Core.Geometry;

namespace PipingAgent.Core.Source
{
    /// <summary>
    /// Đọc bản vẽ nguồn thành đối tượng trung lập. Mục 10.4.
    /// Ba bộ cài đặt dự kiến: DXF chạy ngoài CAD, AutoCAD, Smart 3D. Lõi chỉ biết giao diện này.
    /// </summary>
    public interface IDrawingReader
    {
        DrawingSummary GetSummary();

        IReadOnlyList<SourceEntity> ReadEntities(EntityFilter filter);

        /// <summary>Ảnh PNG của một vùng, cho agent xem. Trả về mảng rỗng nếu bộ đọc không hỗ trợ.</summary>
        byte[] RenderRegion(Box2 region, RenderOptions options);
    }

    public sealed class RenderOptions
    {
        public int WidthPx { get; set; } = 1024;

        public int HeightPx { get; set; } = 768;

        /// <summary>Tô sáng các đối tượng này trong ảnh.</summary>
        public List<string>? HighlightHandles { get; set; }

        public bool ShowLayerColors { get; set; } = true;
    }
}
