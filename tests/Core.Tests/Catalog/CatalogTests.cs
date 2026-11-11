using PipingAgent.Core.Catalog;
using PipingAgent.Hosting;

namespace Core.Tests.Catalog;

/// <summary>S2-10: danh mục ống và phụ kiện tra được, DN không có trả về kết quả rõ ràng.</summary>
public class CatalogTests
{
    private static PipeCatalog Load() => CatalogFiles.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "catalog"));

    [Fact]
    public void Both_standards_load_and_are_marked_unverified()
    {
        var c = Load();
        Assert.True(c.HasStandard("ASME-B36.10"));
        Assert.True(c.HasStandard("ASME-B16.9"));
        Assert.False(c.PipeOd("ASME-B36.10", 150).Verified);
    }

    [Theory]
    [InlineData(15, 21.3)]
    [InlineData(80, 88.9)]
    [InlineData(100, 114.3)]
    [InlineData(150, 168.3)]
    [InlineData(200, 219.1)]
    [InlineData(600, 610.0)]
    public void Pipe_od_lookup(int dn, double od)
    {
        var r = Load().PipeOd("ASME-B36.10", dn);
        Assert.True(r.Found, r.Reason);
        Assert.Equal(od, r.Value, 3);
    }

    [Fact]
    public void Elbow_radius_and_tee_dims()
    {
        var c = Load();
        Assert.Equal(229, c.ElbowRadius("ASME-B16.9", 150).Value);
        Assert.Equal(152, c.ElbowRadius("ASME-B16.9", 150, longRadius: false).Value);
        Assert.Equal(305, c.ElbowRadius("ASME-B16.9", 200).Value);
        var tee = c.TeeDims("ASME-B16.9", 150);
        Assert.True(tee.Found);
        Assert.Equal(143, tee.Value.C);
        Assert.Equal(143, tee.Value.M);
        Assert.Equal(140, c.FittingDim("ASME-B16.9", FittingTypes.ReducerConcentric, 150, "H", dn2: 100).Value);
    }

    [Fact]
    public void Missing_dn_or_standard_returns_not_found_without_throwing()
    {
        var c = Load();
        var od = c.PipeOd("ASME-B36.10", 175);
        Assert.False(od.Found);
        Assert.Contains("DN175", od.Reason);

        var sr = c.ElbowRadius("ASME-B16.9", 15, longRadius: false);
        Assert.False(sr.Found);

        var std = c.PipeOd("DIN-2448", 100);
        Assert.False(std.Found);
        Assert.Contains("DIN-2448", std.Reason);

        Assert.Equal("ASME-B16.9/ELL90LR/DN150", PipeCatalog.Ref("ASME-B16.9", FittingTypes.Elbow90LR, 150));
        Assert.Equal("ASME-B16.9/RED/DN150x100", PipeCatalog.Ref("ASME-B16.9", FittingTypes.ReducerConcentric, 150, 100));
    }

    [Fact]
    public void Every_dn_15_to_600_has_pipe_elbow_and_tee()
    {
        var c = Load();
        foreach (var dn in new[] { 15, 20, 25, 32, 40, 50, 65, 80, 90, 100, 125, 150, 200, 250, 300, 350, 400, 450, 500, 550, 600 })
        {
            Assert.True(c.PipeOd("ASME-B36.10", dn).Found, $"DN{dn} ống");
            Assert.True(c.ElbowRadius("ASME-B16.9", dn).Found, $"DN{dn} co");
            Assert.True(c.TeeDims("ASME-B16.9", dn).Found, $"DN{dn} tê");
            var lr = c.ElbowRadius("ASME-B16.9", dn).Value;
            var od = c.PipeOd("ASME-B36.10", dn).Value;
            Assert.True(lr >= od / 2, $"DN{dn}: bán kính co {lr} nhỏ hơn nửa đường kính ngoài {od}");
        }
    }

    [Theory]
    [InlineData("1/2", 15)]
    [InlineData("1/2\"", 15)]
    [InlineData("3/4\"", 20)]
    [InlineData("1\"", 25)]
    [InlineData("1-1/2\"", 40)]
    [InlineData("1 1/2", 40)]
    [InlineData("1.5", 40)]
    [InlineData("2", 50)]
    [InlineData("6", 150)]
    [InlineData("24\"", 600)]
    [InlineData("7", null)]
    [InlineData("abc", null)]
    public void Nps_to_dn(string text, int? dn)
    {
        Assert.Equal(dn, NominalSize.NpsToDn(text));
    }
}
