using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Source;

namespace Core.Tests.Recognition;

/// <summary>S2-08 và S2-09: ba mức gắn nhãn và điểm tin cậy. Hai ứng viên cách đều thì ra vấn đề, không ra liên kết.</summary>
public class TagAttacherTests
{
    private static ConventionProfile Profile()
    {
        var p = new ConventionProfile { Id = "t" };
        p.TagPatterns.Add(new TagPattern
        {
            Name = "p",
            Regex = "^(?<line>[A-Z]+-\\d+) DN(?<dn>\\d+) EL(?<el>[+-]?\\d+)$",
            ElevationDatum = ElevationDatum.CL,
        });
        return p;
    }

    private static Run Run(string id, params (double X, double Y)[] pts) =>
        new() { Id = id, PlanPoints = pts.Select(p => new Point2(p.X, p.Y)).ToList(), Prov = new Provenance() };

    private static SourceEntity Text(string handle, string text, double x, double y, double rotation = 0, double height = 250)
    {
        var e = new SourceEntity
        {
            Handle = handle,
            Kind = SourceKind.Text,
            NativeType = "TEXT",
            Layer = "PIPE-TAG",
            Text = text,
            Center = new Point3(x, y, 0),
            TextHeight = height,
            RotationDeg = rotation,
        };
        e.Bounds = e.ComputeBounds();
        return e;
    }

    private static SourceEntity Leader(string handle, double x1, double y1, double x2, double y2)
    {
        var e = new SourceEntity
        {
            Handle = handle,
            Kind = SourceKind.Line,
            NativeType = "LINE",
            Layer = "PIPE-TAG",
            Points = new List<Point3> { new(x1, y1, 0), new(x2, y2, 0) },
        };
        e.Bounds = e.ComputeBounds();
        return e;
    }

    private static TagAttachResult Attach(IReadOnlyList<Run> runs, IEnumerable<SourceEntity> texts, params SourceEntity[] leaders)
    {
        var profile = Profile();
        var parser = new TagParser(profile);
        return new TagAttacher(profile).Attach(runs, parser.ReadAll(texts), leaders);
    }

    [Fact]
    public void Equidistant_runs_produce_issue_instead_of_link()
    {
        var runs = new[] { Run("run-001", (0, 0), (5000, 0)), Run("run-002", (0, 1650), (5000, 1650)) };
        // Chữ cao 250 đặt giữa hai đoạn: hộp chữ cách mỗi đoạn đúng 700, quá ngưỡng nằm sát 625.
        var tag = Text("T1", "P-1 DN100 EL+100", 1000, 700);

        var r = Attach(runs, new[] { tag });

        var att = Assert.Single(r.Attachments);
        Assert.False(att.Attached);
        Assert.Equal(AttachMethods.Nearest, att.Method);
        Assert.Equal(0, att.Confidence, 3);
        var issue = Assert.Single(r.Issues);
        Assert.Equal("R-TAG-02", issue.RuleId);
        Assert.Equal(2, issue.Suggestions.Count);
        Assert.All(issue.Suggestions, s => Assert.Equal(DecisionOp.AssignTag, s.Op));
        Assert.Equal("run-001", issue.Suggestions[0].Target["run"]);
        Assert.Equal("T1", issue.Suggestions[0].Target["source"]);
    }

    [Fact]
    public void Leader_touching_run_wins_over_closer_run()
    {
        var runs = new[] { Run("run-001", (0, 0), (5000, 0)), Run("run-002", (0, 3000), (5000, 3000)) };
        var tag = Text("T1", "P-1 DN100 EL+100", 6100, 2900);
        var leader = Leader("L1", 2500, 0, 6000, 2950);

        var r = Attach(runs, new[] { tag }, leader);

        var att = Assert.Single(r.Attachments);
        Assert.Equal("run-001", att.Run!.Id);
        Assert.Equal(AttachMethods.Leader, att.Method);
        Assert.Equal(1.0, att.Confidence);
        Assert.Equal(new[] { "L1" }, att.LeaderHandles);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void Parallel_text_close_to_run_is_adjacent()
    {
        var runs = new[] { Run("run-001", (0, 0), (5000, 0)), Run("run-002", (0, 4000), (5000, 4000)) };
        var tag = Text("T1", "P-1 DN100 EL+100", 1000, 300);

        var r = Attach(runs, new[] { tag });

        var att = Assert.Single(r.Attachments);
        Assert.Equal("run-001", att.Run!.Id);
        Assert.Equal(AttachMethods.Adjacent, att.Method);
        Assert.Equal(0.9, att.Confidence);
    }

    [Fact]
    public void Perpendicular_text_falls_back_to_nearest_with_unique_candidate()
    {
        var runs = new[] { Run("run-001", (0, 0), (0, 5000)) };
        var tag = Text("T1", "P-1 DN100 EL+100", 400, 2000);

        var r = Attach(runs, new[] { tag });

        var att = Assert.Single(r.Attachments);
        Assert.Equal("run-001", att.Run!.Id);
        Assert.Equal(AttachMethods.Nearest, att.Method);
        Assert.Equal(0.8, att.Confidence);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void Second_candidate_lowers_confidence_and_warns_in_middle_band()
    {
        // Hộp chữ rộng 3200: d1 = 300 tới run-001, d2 = 900 tới run-002. 0,8 × (1 − 1/3) = 0,533, gắn nhưng cảnh báo.
        var runs = new[] { Run("run-001", (0, 0), (0, 5000)), Run("run-002", (4400, 0), (4400, 5000)) };
        var tag = Text("T1", "P-1 DN100 EL+100", 300, 2000);

        var r = Attach(runs, new[] { tag });

        var att = Assert.Single(r.Attachments);
        Assert.Equal("run-001", att.Run!.Id);
        Assert.Equal(0.533, att.Confidence, 3);
        var issue = Assert.Single(r.Issues);
        Assert.Equal("R-TAG-02", issue.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Contains("run-001", issue.Elements);
    }

    [Fact]
    public void Tag_outside_radius_and_unmatched_text_create_issues()
    {
        var runs = new[] { Run("run-001", (0, 0), (0, 5000)) };
        var far = Text("T1", "P-1 DN100 EL+100", 3000, 2000);
        var junk = Text("T2", "SEE NOTE 3", 100, 100);

        var r = Attach(runs, new[] { far, junk });

        Assert.Equal(2, r.Issues.Count);
        Assert.Contains(r.Issues, i => i.RuleId == "R-TAG-04" && i.Sources.Contains("T1"));
        Assert.Contains(r.Issues, i => i.RuleId == "R-TAG-03" && i.Sources.Contains("T2"));
        Assert.All(r.Attachments, a => Assert.False(a.Attached));
    }

    [Fact]
    public void Rule_turned_off_in_profile_produces_no_issue()
    {
        var profile = Profile();
        profile.Rules["R-TAG-03"] = "off";
        var runs = new[] { Run("run-001", (0, 0), (0, 5000)) };
        var r = new TagAttacher(profile).Attach(runs, new TagParser(profile).ReadAll(new[] { Text("T2", "SEE NOTE 3", 100, 100) }), Array.Empty<SourceEntity>());
        Assert.Empty(r.Issues);
    }
}
