using PipingAgent.Core.Profile;
using PipingAgent.Hosting;

namespace Core.Tests.Profile;

/// <summary>S2-01: hồ sơ nạp được, hồ sơ sai báo lỗi chỉ rõ dòng và trường.</summary>
public class ProfileLoaderTests
{
    private static string ProfilesDir => Path.Combine(AppContext.BaseDirectory, "profiles");

    [Fact]
    public void Sample_profile_loads_with_extends_resolved()
    {
        var r = ProfileFiles.Load(Path.Combine(ProfilesDir, "sample-pump-station.json"));

        Assert.True(r.Ok, string.Join("\n", r.Errors));
        var p = r.Profile!;
        Assert.Equal("sample-pump-station", p.Id);
        Assert.Equal("ASME-B36.10", p.PipeStandard);
        // Quy tắc của con đứng trước quy tắc của cha.
        Assert.Equal(EntityRoles.PipeCenterline, p.Roles[0].Role);
        Assert.Contains(p.Roles, x => x.Role == EntityRoles.Dimension);
        Assert.Equal(2, p.TagPatterns.Count);
        Assert.Equal("blocker", p.Rules["R-TAG-01"]);
    }

    [Fact]
    public void Real_drawing_profile_loads()
    {
        var r = ProfileFiles.Load(Path.Combine(ProfilesDir, "nsrp-piping-layout.json"));
        Assert.True(r.Ok, string.Join("\n", r.Errors));
        Assert.NotEmpty(r.Profile!.ElevationPatterns);
    }

    [Fact]
    public void Syntax_error_reports_line()
    {
        var json = "{\n  \"schemaVersion\": \"1.0\",\n  \"id\": \"x\",\n  \"roles\": [ { \"role\": \"ignore\" \"match\": {} } ]\n}";
        var r = ProfileLoader.Parse(json);

        Assert.False(r.Ok);
        var e = Assert.Single(r.Errors);
        Assert.Equal(4, e.Line);
    }

    [Fact]
    public void Unknown_role_reports_line_and_path()
    {
        var json = """
            {
              "schemaVersion": "1.0",
              "id": "x",
              "roles": [
                { "role": "pipe_centerline", "match": { "layer": "PIPE" } },
                { "role": "pipe_centreline", "match": { "layer": "PIPE2" } }
              ]
            }
            """;
        var r = ProfileLoader.Parse(json);

        Assert.False(r.Ok);
        var e = Assert.Single(r.Errors);
        Assert.Equal("$.roles[1].role", e.Path);
        Assert.Equal(6, e.Line);
        Assert.Contains("pipe_centreline", e.Message);
    }

    [Fact]
    public void Bad_regex_and_missing_group_are_reported_with_paths()
    {
        var json = """
            {
              "schemaVersion": "1.0",
              "id": "x",
              "tagPatterns": [
                { "name": "a", "regex": "(?<line>[A-Z]+" },
                { "name": "b", "regex": "^DN(?<dn>\\d+)$" }
              ]
            }
            """;
        var r = ProfileLoader.Parse(json);

        Assert.Equal(2, r.Errors.Count);
        Assert.Equal("$.tagPatterns[0].regex", r.Errors[0].Path);
        Assert.Equal(5, r.Errors[0].Line);
        Assert.Equal("$.tagPatterns[1].regex", r.Errors[1].Path);
        Assert.Contains("'line'", r.Errors[1].Message);
    }

    [Fact]
    public void Missing_required_fields_are_reported()
    {
        var r = ProfileLoader.Parse("{ \"schemaVersion\": \"1.0\" }");
        Assert.False(r.Ok);
        Assert.Contains(r.Errors, e => e.Message.Contains("id") || e.Message.Contains("bắt buộc"));
    }

    [Fact]
    public void Invalid_tolerances_and_rule_levels_are_reported()
    {
        var json = """
            {
              "schemaVersion": "1.0",
              "id": "x",
              "tolerances": { "snap": 0 },
              "rules": { "R-TAG-01": "loud", "TAG1": "warning" }
            }
            """;
        var r = ProfileLoader.Parse(json);

        Assert.Contains(r.Errors, e => e.Path == "$.tolerances.snap" && e.Line == 4);
        Assert.Contains(r.Errors, e => e.Path == "$.rules.R-TAG-01");
        Assert.Contains(r.Errors, e => e.Path == "$.rules.TAG1");
    }

    [Fact]
    public void Extends_cycle_is_detected()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pa-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.json"), "{ \"schemaVersion\": \"1.0\", \"id\": \"a\", \"extends\": \"b\" }");
            File.WriteAllText(Path.Combine(dir, "b.json"), "{ \"schemaVersion\": \"1.0\", \"id\": \"b\", \"extends\": \"a\" }");
            var r = ProfileFiles.Load(Path.Combine(dir, "a.json"));
            Assert.False(r.Ok);
            Assert.Contains(r.Errors, e => e.Message.Contains("lặp vòng"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Profile_round_trips_through_json()
    {
        var r = ProfileFiles.Load(Path.Combine(ProfilesDir, "sample-pump-station.json"));
        var json = ProfileLoader.Serialize(r.Profile!);
        var again = ProfileLoader.Parse(json);
        Assert.True(again.Ok, string.Join("\n", again.Errors));
        Assert.Equal(r.Profile!.Roles.Count, again.Profile!.Roles.Count);
        Assert.Equal(r.Profile.TagPatterns[0].Regex, again.Profile.TagPatterns[0].Regex);
    }

    [Theory]
    [InlineData("PIPE", "PIPE", true)]
    [InlineData("pipe", "PIPE", true)]
    [InlineData("PIPE*", "PIPE-TAG", true)]
    [InlineData("*TAG", "PIPE-TAG", true)]
    [InlineData("LINE-?", "LINE-5", true)]
    [InlineData("LINE-?", "LINE-55", false)]
    [InlineData("e#C*", "e1C6swM", true)]
    [InlineData("e#C*", "eXC6swM", false)]
    [InlineData("A$C*", "A$C1234", true)]
    public void Wildcard_matches_like_autocad(string pattern, string value, bool expected)
    {
        Assert.Equal(expected, Wildcard.Match(pattern, value));
    }
}
