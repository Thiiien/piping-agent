using PipingAgent.Core.Equipment;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Rules;
using PipingAgent.Hosting;

namespace Core.Tests.Equipment;

/// <summary>S4-02, S4-03, S4-07.</summary>
public class ExpressionTests
{
    private static readonly Dictionary<string, double> P = new() { ["D"] = 1000, ["H"] = 3000, ["CL"] = 1500 };

    [Theory]
    [InlineData("H / 2", 1500)]
    [InlineData("D * 0.8", 800)]
    [InlineData("(CL - D / 2) / 2", 500)]
    [InlineData("-L_1 + 2", 1)]
    [InlineData("2 * (3 + 4) - 1", 13)]
    [InlineData("300", 300)]
    public void Evaluates_arithmetic_with_parameters(string text, double expected)
    {
        var p = new Dictionary<string, double>(P) { ["L_1"] = 1 };
        Assert.Equal(expected, Expression.Evaluate(text, p), 9);
    }

    [Theory]
    [InlineData("H / (D - 1000)", "Chia cho không")]
    [InlineData("H +", "kết thúc đột ngột")]
    [InlineData("(H + 1", "đóng ngoặc")]
    [InlineData("X * 2", "không tồn tại")]
    [InlineData("H ^ 2", "Ký tự thừa")]
    [InlineData("H 2", "Ký tự thừa")]
    public void Rejects_bad_expressions_with_reason(string text, string fragment)
    {
        var ex = Assert.Throws<ExpressionException>(() => Expression.Evaluate(text, P));
        Assert.Contains(fragment, ex.Message);
    }

    [Fact]
    public void Validate_lists_parameter_names_without_values()
    {
        Assert.Equal(new[] { "CL", "D" }, Expression.Validate("(CL - D / 2) / 2"));
    }
}

public class EquipmentLibraryTests
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "equipment");

    [Fact]
    public void Default_layer_loads_four_definitions_including_doc_example()
    {
        var lib = EquipmentFiles.LoadLayers(Dir);
        Assert.Empty(lib.Errors);
        Assert.Equal(4, lib.Definitions.Count);
        var hx = lib.Find("exchanger_horizontal")!;
        Assert.Equal(3, hx.Params.Count);
        Assert.Equal(3, hx.Body.Count);
        Assert.Equal(2, hx.Nozzles.Count);
        Assert.Equal("CL", hx.Body[0].Center![2].Text);
        Assert.Equal("default", hx.Layer);
    }

    [Fact]
    public void Later_layer_overrides_earlier_by_id()
    {
        var project = Path.Combine(Path.GetTempPath(), "pa-eq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(project);
        try
        {
            File.WriteAllText(Path.Combine(project, "pump.json"), """{ "id": "pump", "schemaVersion": "1.0", "params": [ { "name": "H", "from": "default", "default": 999 } ], "body": [ { "shape": "box", "size": [1, 1, "H"] } ] }""");
            var lib = EquipmentFiles.LoadLayers(Dir, null, project);
            Assert.Empty(lib.Errors);
            Assert.Equal("project", lib.Find("pump")!.Layer);
            Assert.Equal(999, lib.Find("pump")!.Params[0].Default);
            Assert.Equal("default", lib.Find("tank_vertical")!.Layer);
        }
        finally
        {
            Directory.Delete(project, true);
        }
    }

    [Fact]
    public void Invalid_definition_reports_clear_errors_and_is_not_added()
    {
        var lib = new EquipmentLibrary();
        var json = """{ "id": "bad", "schemaVersion": "1.0", "params": [ { "name": "D", "from": "tag" } ], "body": [ { "shape": "torus" }, { "shape": "box" }, { "shape": "cylinder", "diameter": "D + Q", "length": 1 } ] }""";
        Assert.Null(lib.AddJson(json, "default", "bad.json"));
        Assert.Contains(lib.Errors, e => e.Path == "$.params[0].pattern");
        Assert.Contains(lib.Errors, e => e.Path == "$.body[0].shape" && e.Message.Contains("torus"));
        Assert.Contains(lib.Errors, e => e.Path == "$.body[2].diameter" && e.Message.Contains("'Q'"));
        Assert.Contains(lib.Errors, e => e.Path == "$.body[1].size");
        Assert.False(lib.Has("bad"));
        Assert.Null(lib.AddJson("{ not json", "default", "x.json"));
        Assert.Contains(lib.Errors, e => e.Source == "x.json");
    }
}

public class RuleRunnerTests
{
    private static PipingModel Model()
    {
        var m = new PipingModel();
        m.Runs.Add(new Run { Id = "run-001", PlanPoints = new List<PipingAgent.Core.Geometry.Point2> { new(0, 0), new(1000, 0) }, Prov = new Provenance() });
        return m;
    }

    [Fact]
    public void Sample_rule_runs_and_profile_changes_severity_or_turns_it_off()
    {
        var profile = new ConventionProfile { Id = "t" };
        var r = RuleRunner.Run(Model(), profile);
        var issue = Assert.Single(r.Issues);
        Assert.Equal("R-TAG-01", issue.RuleId);
        Assert.Equal(Severity.Blocker, issue.Severity);

        profile.Rules["R-TAG-01"] = "info";
        Assert.Equal(Severity.Info, Assert.Single(RuleRunner.Run(Model(), profile).Issues).Severity);

        profile.Rules["R-TAG-01"] = "off";
        var off = RuleRunner.Run(Model(), profile);
        Assert.Empty(off.Issues);
        Assert.Equal(1, off.RulesOff);
    }

    [Fact]
    public void Failing_rule_does_not_stop_the_others()
    {
        var profile = new ConventionProfile { Id = "t" };
        var r = RuleRunner.Run(Model(), profile, new IRule[] { new ThrowingRule(), new RuleRunner.UntaggedRunRule() });
        Assert.Single(r.Failures);
        Assert.Single(r.Issues);
    }

    private sealed class ThrowingRule : IRule
    {
        public string Id => "R-TAG-02";

        public void Check(RuleContext context) => throw new InvalidOperationException("hỏng");
    }
}
