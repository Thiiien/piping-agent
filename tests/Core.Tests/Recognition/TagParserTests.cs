using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Hosting;
using PipingAgent.Core.Recognition;

namespace Core.Tests.Recognition;

/// <summary>S2-05: tách số hiệu tuyến, DN, cao độ theo mẫu trong hồ sơ.</summary>
public class TagParserTests
{
    private static ConventionProfile Sample() =>
        ProfileFiles.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "sample-pump-station.json")).Profile!;

    private static ConventionProfile Real() =>
        ProfileFiles.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "nsrp-piping-layout.json")).Profile!;

    [Theory]
    [InlineData("P-1001 DN200 EL+600", "P-1001", 200, 600.0)]
    [InlineData("P-1002 DN200 EL+600", "P-1002", 200, 600.0)]
    [InlineData("P-1003 DN200 EL+600", "P-1003", 200, 600.0)]
    [InlineData("P-1011 DN150 EL+600", "P-1011", 150, 600.0)]
    [InlineData("P-1011 DN150 EL+4100", "P-1011", 150, 4100.0)]
    [InlineData("P-1012 DN150 EL+600", "P-1012", 150, 600.0)]
    [InlineData("P-1012 DN150 EL+4100", "P-1012", 150, 4100.0)]
    [InlineData("P-1021 DN100 EL+4100", "P-1021", 100, 4100.0)]
    [InlineData("P-1021 DN100 EL+5500", "P-1021", 100, 5500.0)]
    [InlineData("P-1022 DN100 EL+5500", "P-1022", 100, 5500.0)]
    [InlineData("U-2001 DN80 EL+4100", "U-2001", 80, 4100.0)]
    public void Sample_tags_are_split(string text, string line, int dn, double el)
    {
        var tag = new TagParser(Sample()).Parse(text);

        Assert.NotNull(tag);
        Assert.Equal("line-dn-el", tag!.PatternName);
        Assert.Equal(line, tag.Line);
        Assert.Equal(dn, tag.Dn);
        Assert.Equal(el, tag.Elevation);
        Assert.Equal(ElevationDatum.CL, tag.Datum);
        Assert.False(tag.IsElevationOnly);
        Assert.Empty(tag.Warnings);
    }

    [Fact]
    public void Mtext_formatting_is_stripped_before_matching()
    {
        var tag = new TagParser(Sample()).Parse("{\\fArial|b1;P-1011} %%UDN150%%U\\PEL+4100");
        Assert.NotNull(tag);
        Assert.Equal("P-1011", tag!.Line);
        Assert.Equal(150, tag.Dn);
        Assert.Equal(4100, tag.Elevation);
    }

    [Fact]
    public void Unmatched_text_returns_null_not_exception()
    {
        var parser = new TagParser(Sample());
        Assert.Null(parser.Parse("PIPE RACK  TOS EL+4000"));
        Assert.Null(parser.Parse("H=6000"));
        Assert.Null(parser.Parse(""));
        Assert.Null(parser.Parse(null));
    }

    [Fact]
    public void Second_pattern_without_elevation_leaves_datum_unknown()
    {
        var tag = new TagParser(Sample()).Parse("P-1001 DN200");
        Assert.NotNull(tag);
        Assert.Equal("line-dn", tag!.PatternName);
        Assert.Null(tag.Elevation);
        Assert.Equal(ElevationDatum.Unknown, tag.Datum);
    }

    [Theory]
    [InlineData("2\"-ABC-123-4567-A1AS-HS(40)", 50, "ABC-123-4567", "A1AS", "HS", 40.0)]
    [InlineData("3/4\"-ABC-123-4567-A1AS-NI", 20, "ABC-123-4567", "A1AS", "NI", null)]
    [InlineData("1-1/2\"-ABC-123-4567-A1AS", 40, "ABC-123-4567", "A1AS", null, null)]
    [InlineData("6\"-ABC-123-4567-A1AS-HC(XX)", 150, "ABC-123-4567", "A1AS", "HC", null)]
    public void Real_drawing_pattern_reads_inch_sizes_and_optional_fields(string text, int dn, string line, string spec, string? insul, double? thk)
    {
        var tag = new TagParser(Real()).Parse(text);

        Assert.NotNull(tag);
        Assert.Equal(dn, tag!.Dn);
        Assert.Equal(line, tag.Line);
        Assert.Equal("ABC", tag.Service);
        Assert.Equal(spec, tag.Spec);
        Assert.Equal(insul, tag.InsulationType);
        Assert.Equal(thk, tag.InsulationThickness);
        Assert.Equal("123", tag.Extra["unit"]);
        Assert.Equal("4567", tag.Extra["seq"]);
    }

    [Theory]
    [InlineData("EL 109000 BOP", 109000.0, ElevationDatum.BOP)]
    [InlineData("EL. 108,500 TOS", 108500.0, ElevationDatum.TOS)]
    [InlineData("BOP EL 107000", 107000.0, ElevationDatum.BOP)]
    public void Elevation_only_texts_are_recognised(string text, double el, ElevationDatum datum)
    {
        var tag = new TagParser(Real()).Parse(text);

        Assert.NotNull(tag);
        Assert.True(tag!.IsElevationOnly);
        Assert.Equal(el, tag.Elevation);
        Assert.Equal(datum, tag.Datum);
    }

    [Fact]
    public void Unknown_inch_size_keeps_tag_but_warns()
    {
        var tag = new TagParser(Real()).Parse("7\"-ABC-123-4567-A1AS");
        Assert.NotNull(tag);
        Assert.Null(tag!.Dn);
        Assert.Equal("7", tag.DnText);
        Assert.Contains(tag.Warnings, w => w.Contains("inch"));
    }
}
