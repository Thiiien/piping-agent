using PipingAgent.Core.Source;
using PipingAgent.Dxf;

namespace Dxf.Tests;

/// <summary>
/// Kiểm thử trên bản vẽ thật trong data/private, nếu có trên máy. Trên máy chủ dựng tự động không có tệp này nên bỏ qua.
/// Số liệu đối chứng lấy từ docs/KHAO_SAT_BAN_VE_THAT.md.
/// </summary>
public class RealDrawingTests
{
    private static string? FindRealDrawing(string extension)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PipingAgent.sln"))) directory = directory.Parent;
        if (directory == null) return null;
        var path = Path.Combine(directory.FullName, "data", "private", "A410B4001" + extension);
        if (!File.Exists(path)) path = Path.Combine(directory.FullName, "docs", "dataTemplate", "A410B4001" + extension);
        return File.Exists(path) ? path : null;
    }

    [SkippableFact]
    public void Real_dxf_reads_all_19054_entities_without_skipping()
    {
        var path = FindRealDrawing(".dxf");
        Skip.If(path == null, "Không có bản vẽ thật trên máy này.");

        var reader = DxfDrawingReader.Open(path!);
        var entities = reader.ReadEntities(EntityFilter.All);

        Assert.Equal(19054, entities.Count);
        Assert.Empty(reader.Skipped);
        var counts = entities.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(9095, counts[SourceKind.Line]);
        Assert.Equal(4705, counts[SourceKind.Insert]);
        Assert.Equal(2095, counts[SourceKind.Text]);
        Assert.Equal(57, counts[SourceKind.MultiLeader]);
        Assert.Equal(339, counts[SourceKind.Leader]);
        Assert.Equal(375, counts[SourceKind.Dimension]);
        Assert.Equal(243, counts[SourceKind.Ellipse]);
        Assert.Equal(entities.Count, entities.Select(e => e.Handle).Distinct().Count());
        Assert.Equal(DeclaredUnit.Millimeters, reader.Units.Declared);
    }

    [SkippableFact]
    public void Real_dwg_gives_same_fingerprint_as_its_dxf_export()
    {
        var dxf = FindRealDrawing(".dxf");
        var dwg = FindRealDrawing(".dwg");
        Skip.If(dxf == null || dwg == null, "Không có cặp DWG và DXF thật trên máy này.");

        var a = DxfDrawingReader.Open(dxf!).GetSummary();
        var b = DxfDrawingReader.Open(dwg!).GetSummary();
        Assert.Equal(a.EntityCount, b.EntityCount);
        Assert.Equal(a.Fingerprint, b.Fingerprint);
    }

    [SkippableFact]
    public void Real_drawing_title_block_attributes_and_leaders_are_read()
    {
        var path = FindRealDrawing(".dxf");
        Skip.If(path == null, "Không có bản vẽ thật trên máy này.");

        var reader = DxfDrawingReader.Open(path!);
        var entities = reader.ReadEntities(EntityFilter.All);

        var titleBlocks = entities.Where(e => e.Kind == SourceKind.Insert && e.Attributes != null && e.Attributes.Count > 50).ToList();
        Assert.Equal(4, titleBlocks.Count);
        Assert.All(titleBlocks, t => Assert.Equal("PIPING LAYOUT", t.Attributes!["DTTL0"]));

        var leaders = entities.Where(e => e.Kind == SourceKind.MultiLeader).ToList();
        Assert.All(leaders, l => Assert.True(l.Points!.Count >= 2, $"MULTILEADER {l.Handle} không có điểm"));

        var summary = reader.GetSummary();
        Assert.Equal(61, summary.Layers.Count);
        Assert.True(summary.Blocks.Count >= 400);
        Assert.False(summary.HasProxyEntities);

        var exploded = reader.ReadEntities(new EntityFilter { ExplodeBlocks = true });
        Assert.True(exploded.Count > 30000, $"Nổ block chỉ ra {exploded.Count} đối tượng");
    }
}
