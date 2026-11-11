using System.Collections.Generic;

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
    }
}
