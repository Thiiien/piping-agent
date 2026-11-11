using PipingAgent.Core.Catalog;

namespace Core.Tests.Catalog;

/// <summary>I2: kích thước cấu kiện JIS 10K để dựng hình. Bảng chưa ký xác nhận nên mọi kết quả phải mang cờ chưa xác nhận.</summary>
public class ComponentDimsTests
{
    [Fact]
    public void Table_is_marked_unverified()
    {
        Assert.False(ComponentDims.Verified);
        Assert.Equal("catalog_unverified", ComponentDims.Source);
    }

    [Theory]
    [InlineData(25, 125, 90, 4, 14)]
    [InlineData(250, 400, 355, 12, 24)]
    [InlineData(350, 490, 445, 16, 26)]
    public void Flange_10K_has_disc_bolt_circle_bolts_and_thickness(int dn, double d, double c, int bolts, double t)
    {
        var f = ComponentDims.Flange10K(dn);
        Assert.NotNull(f);
        Assert.Equal(d, f!.D);
        Assert.Equal(c, f.C);
        Assert.Equal(bolts, f.Bolts);
        Assert.Equal(t, f.T);
        Assert.True(f.HubLength > f.T, "mặt bích SO có cổ dài hơn chiều dày đĩa");
        Assert.True(f.HubOd > ComponentDims.PipeOd(dn), "cổ to hơn ống");
        Assert.True(f.RaisedFace < f.C - f.Hole, "mặt nâng nằm trong vòng bu lông");
    }

    [Fact]
    public void Unknown_dn_returns_null_not_a_guess()
    {
        Assert.Null(ComponentDims.Flange10K(27));
        Assert.Null(ComponentDims.ValveFaceToFace("valve_globe", 27));
        Assert.Null(ComponentDims.ConcentricReducerLength(27));
        Assert.Null(ComponentDims.HalfCoupling(27));
    }

    [Theory]
    [InlineData("valve_globe", 25, 127)]
    [InlineData("valve_butterfly", 250, 68)]
    [InlineData("valve_gate", 350, 381)]
    public void Valve_face_to_face_by_type(string type, int dn, double expected)
    {
        Assert.Equal(expected, ComponentDims.ValveFaceToFace(type, dn));
    }

    [Fact]
    public void Reducer_and_half_coupling_lengths()
    {
        // Côn thu 350x250 dài 330 trùng với kích thước ghi trên tờ 3E204-1-250A-SUS.
        Assert.Equal(330, ComponentDims.ConcentricReducerLength(350));
        var hc = ComponentDims.HalfCoupling(25);
        Assert.NotNull(hc);
        Assert.Equal(46, hc!.Value.Od);
        Assert.Equal(25.5, hc.Value.L);
    }
}
