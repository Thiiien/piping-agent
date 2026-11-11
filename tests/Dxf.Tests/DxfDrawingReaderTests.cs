using PipingAgent.Core.Geometry;
using PipingAgent.Core.Source;
using PipingAgent.Dxf;

namespace Dxf.Tests;

/// <summary>Kiểm thử cho việc S1-07 và S1-08 trên bản vẽ mẫu của prototype và các tệp đơn vị.</summary>
public class DxfDrawingReaderTests
{
    private static string Data(string name) => Path.Combine(AppContext.BaseDirectory, "Data", name);

    [Fact]
    public void Sample_drawing_reads_80_entities_like_prototype()
    {
        var reader = DxfDrawingReader.Open(Data("piping_plan_2d.dxf"));
        var entities = reader.ReadEntities(EntityFilter.All);

        Assert.Equal(80, entities.Count);
        Assert.Empty(reader.Skipped);
        var counts = entities.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(18, counts[SourceKind.Line]);
        Assert.Equal(15, counts[SourceKind.Polyline]);
        Assert.Equal(6, counts[SourceKind.Circle]);
        Assert.Equal(26, counts[SourceKind.Text]);
        Assert.Equal(15, counts[SourceKind.Insert]);
    }

    [Fact]
    public void Every_entity_has_a_unique_hex_handle_and_bounds()
    {
        var entities = DxfDrawingReader.Open(Data("piping_plan_2d.dxf")).ReadEntities(EntityFilter.All);
        Assert.All(entities, e => Assert.Matches("^[0-9A-F]+$", e.Handle));
        Assert.Equal(entities.Count, entities.Select(e => e.Handle).Distinct().Count());
        Assert.All(entities, e => Assert.True(e.Bounds.HasValue, $"{e.Handle} {e.Kind} không có hộp bao"));
    }

    [Fact]
    public void Layer_color_and_linetype_are_resolved_from_layer()
    {
        var entities = DxfDrawingReader.Open(Data("piping_plan_2d.dxf")).ReadEntities(EntityFilter.All);
        var pipe = entities.First(e => e.Layer == "PIPE" && e.Kind == SourceKind.Polyline);
        Assert.NotNull(pipe.Color);
        Assert.NotNull(pipe.Linetype);
        Assert.Equal("LWPOLYLINE", pipe.NativeType);
        Assert.True(pipe.Points!.Count >= 2);
    }

    [Fact]
    public void Text_is_stripped_and_tags_keep_their_content()
    {
        var entities = DxfDrawingReader.Open(Data("piping_plan_2d.dxf")).ReadEntities(new EntityFilter { Kinds = new() { SourceKind.Text } });
        Assert.Contains(entities, e => e.Text!.StartsWith("P-10") && e.Text.Contains("DN") && e.Text.Contains("EL"));
        Assert.All(entities, e => Assert.NotNull(e.TextHeight));
    }

    [Fact]
    public void Inserts_carry_block_name_scale_and_rotation()
    {
        var inserts = DxfDrawingReader.Open(Data("piping_plan_2d.dxf")).ReadEntities(new EntityFilter { Kinds = new() { SourceKind.Insert } });
        Assert.Equal(15, inserts.Count);
        Assert.Contains(inserts, e => e.Name == "VALVE_GATE");
        Assert.All(inserts, e => { Assert.NotNull(e.Center); Assert.NotNull(e.Scale); Assert.NotNull(e.RotationDeg); });
    }

    [Fact]
    public void Exploding_blocks_adds_children_with_block_path()
    {
        var reader = DxfDrawingReader.Open(Data("piping_plan_2d.dxf"));
        var plain = reader.ReadEntities(EntityFilter.All);
        var exploded = reader.ReadEntities(new EntityFilter { ExplodeBlocks = true });

        Assert.True(exploded.Count > plain.Count);
        var children = exploded.Where(e => e.BlockPath.Count > 0).ToList();
        Assert.NotEmpty(children);
        var parents = plain.Where(e => e.Kind == SourceKind.Insert).Select(e => e.Handle).ToHashSet();
        Assert.All(children, c => Assert.Contains(c.BlockPath[0], parents));

        // Đối tượng trong block phải nằm gần điểm chèn của block, sau khi biến đổi.
        var valve = plain.First(e => e.Kind == SourceKind.Insert && e.Name == "VALVE_GATE");
        var valveChildren = children.Where(c => c.BlockPath[0] == valve.Handle).ToList();
        Assert.NotEmpty(valveChildren);
        Assert.All(valveChildren, c => Assert.True(c.Bounds!.Value.DistanceTo(valve.Center!.Value.Plan) < 2000));
    }

    [Fact]
    public void Region_and_layer_filters_restrict_results()
    {
        var reader = DxfDrawingReader.Open(Data("piping_plan_2d.dxf"));
        var all = reader.ReadEntities(EntityFilter.All);
        var extent = reader.GetSummary().Extents!.Value;
        var half = new Box2(extent.Min, new Point2(extent.Center.X, extent.Max.Y));

        var inRegion = reader.ReadEntities(new EntityFilter { Region = half });
        Assert.True(inRegion.Count > 0 && inRegion.Count < all.Count);
        Assert.All(inRegion, e => Assert.True(e.Bounds!.Value.Intersects(half)));

        var onlyPipe = reader.ReadEntities(new EntityFilter { Layers = new() { "pipe" } });
        Assert.NotEmpty(onlyPipe);
        Assert.All(onlyPipe, e => Assert.Equal("PIPE", e.Layer, ignoreCase: true));

        var withoutPipe = reader.ReadEntities(new EntityFilter { ExcludeLayers = new() { "PIPE" } });
        Assert.Equal(all.Count, onlyPipe.Count + withoutPipe.Count);
    }

    [Fact]
    public void Summary_lists_layers_blocks_counts_and_fingerprint()
    {
        var reader = DxfDrawingReader.Open(Data("piping_plan_2d.dxf"));
        var summary = reader.GetSummary();

        Assert.Equal(80, summary.EntityCount);
        Assert.Equal("piping_plan_2d.dxf", summary.File);
        Assert.StartsWith("sha256:", summary.Fingerprint);
        Assert.Contains(summary.Layers, l => l.Name == "PIPE" && l.EntityCount > 0);
        Assert.Contains(summary.Blocks, b => b.Name == "VALVE_GATE" && b.InsertCount > 0 && b.EntityCount > 0);
        Assert.Equal(80, summary.CountsByKind.Values.Sum());
        Assert.NotNull(summary.Extents);
        Assert.False(summary.HasXrefs);
        Assert.False(summary.HasProxyEntities);
    }

    [Fact]
    public void Fingerprint_is_stable_across_reads()
    {
        var a = DxfDrawingReader.Open(Data("piping_plan_2d.dxf")).GetSummary().Fingerprint;
        var b = DxfDrawingReader.Open(Data("piping_plan_2d.dxf")).GetSummary().Fingerprint;
        Assert.Equal(a, b);
        var other = DxfDrawingReader.Open(Data("units_mm.dxf")).GetSummary().Fingerprint;
        Assert.NotEqual(a, other);
    }

    [Theory]
    [InlineData("units_mm.dxf", DeclaredUnit.Millimeters, 1000.0, false)]
    [InlineData("units_m.dxf", DeclaredUnit.Meters, 1_000_000.0, false)]
    [InlineData("units_inch.dxf", DeclaredUnit.Inches, 25_400.0, false)]
    [InlineData("units_none.dxf", DeclaredUnit.Unitless, 1000.0, true)]
    public void Units_are_detected_and_coordinates_scaled_to_mm(string file, DeclaredUnit expected, double expectedLengthMm, bool needsConfirmation)
    {
        var reader = DxfDrawingReader.Open(Data(file));
        Assert.Equal(expected, reader.Units.Declared);
        Assert.Equal(needsConfirmation, reader.Units.NeedsConfirmation);

        var line = reader.ReadEntities(new EntityFilter { Kinds = new() { SourceKind.Line } }).Single();
        var length = line.Points![0].DistanceTo(line.Points[1]);
        Assert.Equal(expectedLengthMm, length, 6);
        Assert.Equal(reader.ScaleToMm, reader.GetSummary().UnitScaleToMm);
    }

    [Fact]
    public void User_confirmed_unit_overrides_declared_unit()
    {
        var reader = DxfDrawingReader.Open(Data("units_none.dxf"), unitOverrideToMm: 1000.0);
        var line = reader.ReadEntities(new EntityFilter { Kinds = new() { SourceKind.Line } }).Single();
        Assert.Equal(1_000_000.0, line.Points![0].DistanceTo(line.Points[1]), 6);
        Assert.Equal(1000.0, reader.ScaleToMm);
    }

    [Fact]
    public void Text_and_mtext_formatting_codes_are_stripped()
    {
        var entities = DxfDrawingReader.Open(Data("units_mm.dxf")).ReadEntities(EntityFilter.All);
        var text = entities.Single(e => e.Kind == SourceKind.Text);
        Assert.Equal("P-1 Ø100 EL+2500", text.Text);
        Assert.Contains("%%C", text.RawText);

        var mtext = entities.Single(e => e.Kind == SourceKind.MText);
        Assert.Equal("Dòng một\nDòng hai chữ Ø100", mtext.Text);
    }

    [Fact]
    public void Unsupported_file_fails_with_exception_not_crash()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "not_a_drawing.dxf");
        File.WriteAllText(path, "đây không phải DXF");
        Assert.ThrowsAny<Exception>(() => DxfDrawingReader.Open(path));
    }
}
