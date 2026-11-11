using PipingAgent.Core.Geometry;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Source;

namespace Core.Tests.Recognition;

/// <summary>S2-03 và S2-04: phân loại theo hồ sơ, báo cáo chưa phân loại.</summary>
public class ClassifierTests
{
    private static ConventionProfile Profile(params RoleRule[] rules)
    {
        var p = new ConventionProfile { Id = "t" };
        p.Roles.AddRange(rules);
        return p;
    }

    private static RoleRule Rule(string role, Action<EntityMatch> set)
    {
        var m = new EntityMatch();
        set(m);
        return new RoleRule { Role = role, Match = m };
    }

    private static SourceEntity Entity(string handle, SourceKind kind, string layer, string? native = null, string? name = null, string? text = null, int? color = null, string? linetype = null) =>
        new()
        {
            Handle = handle,
            Kind = kind,
            NativeType = native ?? kind.ToString().ToUpperInvariant(),
            Layer = layer,
            Name = name,
            Text = text,
            Color = color,
            Linetype = linetype,
            Points = new List<Point3> { new(0, 0, 0), new(1, 0, 0) },
        };

    [Fact]
    public void First_matching_rule_wins_and_result_is_stable()
    {
        var profile = Profile(
            Rule(EntityRoles.PipeTag, m => { m.Layer = "PIPE-TAG"; m.Types = new List<string> { "TEXT" }; }),
            Rule(EntityRoles.PipeTagLeader, m => { m.Layer = "PIPE-TAG"; m.Types = new List<string> { "LINE" }; }),
            Rule(EntityRoles.Ignore, m => m.Layer = "PIPE-*"));
        var c = new Classifier(profile);
        var entities = new[]
        {
            Entity("1", SourceKind.Text, "PIPE-TAG", text: "P-1"),
            Entity("2", SourceKind.Line, "PIPE-TAG"),
            Entity("3", SourceKind.Circle, "PIPE-TAG"),
            Entity("4", SourceKind.Line, "OTHER"),
        };

        var r1 = c.Classify(entities);
        var r2 = c.Classify(entities);

        Assert.Equal(EntityRoles.PipeTag, r1.Items[0].Role);
        Assert.Equal(EntityRoles.PipeTagLeader, r1.Items[1].Role);
        Assert.Equal(EntityRoles.Ignore, r1.Items[2].Role);
        Assert.Equal(EntityRoles.Unclassified, r1.Items[3].Role);
        Assert.Equal(r1.Items.Select(i => i.Role), r2.Items.Select(i => i.Role));
        Assert.Equal(0, r1.Items[0].RuleIndex);
        Assert.Equal(-1, r1.Items[3].RuleIndex);
    }

    [Fact]
    public void Matches_on_native_type_block_name_color_linetype_and_text()
    {
        var profile = Profile(
            Rule(EntityRoles.PipeCenterline, m => { m.Types = new List<string> { "LWPOLYLINE" }; }),
            Rule(EntityRoles.InlineComponent, m => { m.Block = "VALVE_*"; }),
            Rule(EntityRoles.Structure, m => { m.Color = 8; }),
            Rule(EntityRoles.Annotation, m => { m.Linetype = "HIDDEN*"; }),
            Rule(EntityRoles.EquipmentTag, m => { m.TextPattern = "^[A-Z]-\\d{3}$"; }));
        var c = new Classifier(profile);

        Assert.Equal(EntityRoles.PipeCenterline, c.ClassifyOne(Entity("1", SourceKind.Polyline, "X", native: "LWPOLYLINE")).Role);
        Assert.Equal(EntityRoles.Unclassified, c.ClassifyOne(Entity("2", SourceKind.Polyline, "X", native: "POLYLINE")).Role);
        Assert.Equal(EntityRoles.InlineComponent, c.ClassifyOne(Entity("3", SourceKind.Insert, "X", name: "valve_gate")).Role);
        Assert.Equal(EntityRoles.Unclassified, c.ClassifyOne(Entity("4", SourceKind.Text, "X", text: "VALVE_GATE")).Role);
        Assert.Equal(EntityRoles.Structure, c.ClassifyOne(Entity("5", SourceKind.Line, "X", color: 8)).Role);
        Assert.Equal(EntityRoles.Annotation, c.ClassifyOne(Entity("6", SourceKind.Line, "X", linetype: "HIDDEN2")).Role);
        Assert.Equal(EntityRoles.EquipmentTag, c.ClassifyOne(Entity("7", SourceKind.Text, "0", text: "T-101")).Role);
        Assert.Equal(EntityRoles.Unclassified, c.ClassifyOne(Entity("8", SourceKind.Text, "0", text: "H=6000")).Role);
    }

    [Fact]
    public void Report_groups_unclassified_by_layer()
    {
        var profile = Profile(Rule(EntityRoles.PipeCenterline, m => m.Layer = "PIPE"));
        profile.Tolerances.UnclassifiedWarning = 0.5;
        var c = new Classifier(profile);
        var entities = new[]
        {
            Entity("1", SourceKind.Line, "PIPE"),
            Entity("2", SourceKind.Line, "DIMN"),
            Entity("3", SourceKind.Text, "DIMN"),
            Entity("4", SourceKind.Text, "TEXT"),
        };

        var report = c.Classify(entities).Report;

        Assert.Equal(4, report.Total);
        Assert.Equal(3, report.Unclassified);
        Assert.Equal(0.75, report.UnclassifiedRatio, 9);
        Assert.True(report.ExceedsThreshold);
        Assert.Equal("DIMN", report.Layers[0].Layer);
        Assert.Equal(2, report.Layers[0].Unclassified);
        Assert.Equal(1, report.Layers[0].UnclassifiedByType["LINE"]);
        Assert.Equal(1, report.Layers[0].UnclassifiedByType["TEXT"]);
        Assert.Equal(0, report.Layers.Single(l => l.Layer == "PIPE").Unclassified);
        Assert.Equal(1, report.ByRole[EntityRoles.PipeCenterline]);
        Assert.Equal(3, report.ByRole[EntityRoles.Unclassified]);
    }
}
