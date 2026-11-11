using PipingAgent.Core.Diagnostics;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Source;

namespace Core.Tests.Source;

public class CadTextTests
{
    [Theory]
    [InlineData("P-1011 %%UDN150%%U EL+4100", "P-1011 DN150 EL+4100")]
    [InlineData("AH %%C500", "AH Ø500")]
    [InlineData("A421_30_90%%%_27", "A421_30_90%_27")]
    [InlineData("45%%D", "45°")]
    [InlineData("%%P0.5", "±0.5")]
    [InlineData("%%065BC", "ABC")]
    [InlineData("Dòng một\\PDòng hai", "Dòng một\nDòng hai")]
    [InlineData("{\\fArial|b1;chữ đậm} thường", "chữ đậm thường")]
    [InlineData("\\A1;\\H2.5x;EL 109000 BOP", "EL 109000 BOP")]
    [InlineData("\\S1/2;\"", "1/2\"")]
    [InlineData("\\U+00D8100", "Ø100")]
    [InlineData("  nhiều   khoảng  ", "nhiều   khoảng")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Strip_removes_formatting_codes(string? raw, string expected)
    {
        Assert.Equal(expected, CadText.Strip(raw));
    }

    [Fact]
    public void Normalize_collapses_whitespace()
    {
        Assert.Equal("EL 109000 BOP", CadText.Normalize("  EL   109000\n BOP "));
    }
}

public class DrawingUnitsTests
{
    [Theory]
    [InlineData(DeclaredUnit.Millimeters, 1.0, false)]
    [InlineData(DeclaredUnit.Meters, 1000.0, false)]
    [InlineData(DeclaredUnit.Inches, 25.4, false)]
    [InlineData(DeclaredUnit.Feet, 304.8, false)]
    [InlineData(DeclaredUnit.Unitless, 1.0, true)]
    [InlineData(DeclaredUnit.LightYears, 1.0, true)]
    public void Resolve_gives_scale_and_confirmation_flag(DeclaredUnit unit, double scale, bool needsConfirmation)
    {
        var r = DrawingUnits.Resolve(unit);
        Assert.Equal(scale, r.ScaleToMm, 9);
        Assert.Equal(needsConfirmation, r.NeedsConfirmation);
        Assert.False(string.IsNullOrWhiteSpace(r.Note));
    }

    [Theory]
    [InlineData(180_000, 80_000, DeclaredUnit.Millimeters)]
    [InlineData(180, 80, DeclaredUnit.Meters)]
    [InlineData(1, 1, DeclaredUnit.Unitless)]
    public void Guess_from_extent_is_only_a_hint(double w, double h, DeclaredUnit expected)
    {
        Assert.Equal(expected, DrawingUnits.GuessFromExtent(w, h));
    }
}

public class DrawingFingerprintTests
{
    private static SourceEntity Line(string handle, double x) => new()
    {
        Handle = handle, Kind = SourceKind.Line, Layer = "PIPE", Color = 4, Linetype = "Continuous",
        Points = new() { new Point3(0, 0, 0), new Point3(x, 0, 0) },
    };

    [Fact]
    public void Fingerprint_is_order_independent_and_content_sensitive()
    {
        var a = DrawingFingerprint.Compute(new[] { Line("A1", 100), Line("A2", 200) });
        var b = DrawingFingerprint.Compute(new[] { Line("A2", 200), Line("A1", 100) });
        var c = DrawingFingerprint.Compute(new[] { Line("A1", 100), Line("A2", 200.01) });
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.StartsWith("sha256:", a);
        Assert.Equal(7 + 64, a.Length);
    }

    [Fact]
    public void Fingerprint_ignores_differences_below_a_micron_and_case_of_layer()
    {
        var a = Line("A1", 100);
        var b = Line("A1", 100.0004);
        b.Layer = "pipe";
        b.Linetype = "CONTINUOUS";
        Assert.Equal(DrawingFingerprint.Canonical(a), DrawingFingerprint.Canonical(b));
    }
}

public class JsonLineLoggerTests
{
    [Fact]
    public void Writes_one_json_object_per_line_with_session_id()
    {
        var writer = new StringWriter();
        string session;
        using (var log = new JsonLineLogger(writer, "test", "1.0", ownsWriter: false))
        {
            session = log.SessionId;
            log.Info("scan", new Dictionary<string, object?> { ["entities"] = 80 });
            log.Error("boom", new InvalidOperationException("hỏng"));
        }

        var lines = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length); // start, scan, boom, end.
        foreach (var line in lines)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            Assert.Equal(session, doc.RootElement.GetProperty("session").GetString());
            Assert.True(doc.RootElement.TryGetProperty("ts", out _));
        }

        Assert.Contains("\"event\":\"scan\"", lines[1]);
        Assert.Contains("\"entities\":80", lines[1]);
        Assert.Contains("InvalidOperationException", lines[2]);
    }
}
