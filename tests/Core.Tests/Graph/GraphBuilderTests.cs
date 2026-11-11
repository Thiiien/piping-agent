using PipingAgent.Core.Catalog;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Graph;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;
using PipingAgent.Hosting;

namespace Core.Tests.Graph;

/// <summary>S3-01 đến S3-08 trên các ca dựng sẵn.</summary>
public class GraphBuilderTests
{
    private static PipeCatalog Catalog() => CatalogFiles.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "catalog"));

    private static ConventionProfile Profile() => new()
    {
        Id = "t",
        PipeStandard = "ASME-B36.10",
        FittingStandard = "ASME-B16.9",
    };

    private static Run Run(string id, string line, int dn, double el, ElevationDatum datum, params (double X, double Y)[] pts) => new()
    {
        Id = id,
        LineId = line,
        Dn = dn,
        Elevation = new Elevation { Raw = el, Datum = datum },
        PlanPoints = pts.Select(p => new Point2(p.X, p.Y)).ToList(),
        TagRef = new TagRef { Text = line, By = "adjacent", SourceHandle = "T" + id, Confidence = 0.9 },
        Prov = new Provenance { Sources = new List<string> { "H" + id }, Rule = RecognitionRules.RunFromChain },
    };

    [Theory]
    [InlineData(ElevationDatum.CL, 4100.0, 4100.0)]
    [InlineData(ElevationDatum.BOP, 4100.0, 4184.15)]
    [InlineData(ElevationDatum.TOP, 4100.0, 4015.85)]
    public void Elevation_semantics_with_dn150(ElevationDatum datum, double raw, double centerline)
    {
        var r = ElevationResolver.Resolve(new Elevation { Raw = raw, Datum = datum }, 150, Catalog(), Profile());
        Assert.True(r.Ok, r.Problem);
        Assert.Equal(centerline, r.Centerline!.Value, 2);
    }

    [Fact]
    public void Tos_needs_support_height_and_unknown_datum_is_rejected()
    {
        var profile = Profile();
        var tos = ElevationResolver.Resolve(new Elevation { Raw = 4000, Datum = ElevationDatum.TOS }, 150, Catalog(), profile);
        Assert.False(tos.Ok);
        profile.Tolerances.DefaultSupportHeight = 100;
        tos = ElevationResolver.Resolve(new Elevation { Raw = 4000, Datum = ElevationDatum.TOS }, 150, Catalog(), profile);
        Assert.Equal(4184.15, tos.Centerline!.Value, 2);
        var unknown = ElevationResolver.Resolve(new Elevation { Raw = 4000, Datum = ElevationDatum.Unknown }, 150, Catalog(), profile);
        Assert.False(unknown.Ok);
        Assert.False(ElevationResolver.Resolve(new Elevation { Raw = 1, Datum = ElevationDatum.BOP }, 0, Catalog(), profile).Ok);
    }

    [Fact]
    public void Missing_datum_creates_blocking_issue_and_other_runs_still_build()
    {
        var model = new PipingModel();
        model.Runs.Add(Run("run-001", "line-001", 150, 600, ElevationDatum.CL, (0, 0), (5000, 0)));
        model.Runs.Add(Run("run-002", "line-001", 150, 600, ElevationDatum.Unknown, (0, 3000), (5000, 3000)));

        var stats = new GraphBuilder(Profile(), Catalog()).Build(model);

        Assert.Equal(1, stats.RunsUsed);
        Assert.Equal(1, stats.RunsSkipped);
        Assert.Single(model.Segments);
        var issue = Assert.Single(model.Issues, i => i.RuleId == "R-ELV-05");
        Assert.Equal(Severity.Blocker, issue.Severity);
        Assert.Contains("run-002", issue.Elements);
    }

    [Fact]
    public void Crossing_runs_at_different_elevations_are_not_connected()
    {
        var model = new PipingModel();
        model.Runs.Add(Run("run-001", "line-001", 150, 600, ElevationDatum.CL, (0, 0), (5000, 0)));
        model.Runs.Add(Run("run-002", "line-002", 100, 5500, ElevationDatum.CL, (2500, -2000), (2500, 2000)));

        var stats = new GraphBuilder(Profile(), Catalog()).Build(model);

        Assert.Equal(2, stats.Segments);
        Assert.Equal(4, stats.Nodes);
        Assert.Equal(4, stats.OpenEnds);
        Assert.DoesNotContain(model.Issues, i => i.RuleId == "R-CON-03");
    }

    [Fact]
    public void Crossing_runs_at_same_elevation_without_node_warn()
    {
        var model = new PipingModel();
        model.Runs.Add(Run("run-001", "line-001", 150, 600, ElevationDatum.CL, (0, 0), (5000, 0)));
        model.Runs.Add(Run("run-002", "line-002", 150, 600, ElevationDatum.CL, (2500, -2000), (2500, 2000)));

        new GraphBuilder(Profile(), Catalog()).Build(model);

        Assert.Contains(model.Issues, i => i.RuleId == "R-CON-03");
    }

    [Fact]
    public void Riser_is_inferred_and_elbows_get_catalog_trims()
    {
        var model = new PipingModel();
        model.Runs.Add(Run("run-001", "line-001", 150, 600, ElevationDatum.CL, (0, 0), (5000, 0)));
        model.Runs.Add(Run("run-002", "line-001", 150, 4100, ElevationDatum.CL, (5000, 0), (5000, 4000)));

        var stats = new GraphBuilder(Profile(), Catalog()).Build(model);

        Assert.Equal(3, stats.Segments);
        Assert.Equal(1, stats.Risers);
        Assert.Equal(2, stats.Elbows);
        Assert.Equal(2, stats.OpenEnds);
        var riser = Assert.Single(model.Segments, s => s.Kind == SegmentKind.Riser);
        Assert.Equal(3500, riser.Length, 6);
        Assert.True(riser.Prov!.Inferred);
        Assert.Null(riser.RunId);
        Assert.Equal(168.3, riser.Od, 3);
        var elbow = model.Nodes.First(n => n.Kind == NodeKind.Elbow);
        Assert.Equal("ASME-B16.9/ELL90LR/DN150", elbow.CatalogRef);
        Assert.Equal(229, elbow.Params!["radius"]);
        Assert.Equal(90, elbow.Params["angle"], 6);
        var bottom = model.Segments.First(s => s.Kind == SegmentKind.Horizontal && s.RunId == "run-001");
        Assert.Equal(229, bottom.TrimB);
        Assert.Equal(0, bottom.TrimA);
        Assert.Contains(model.Issues, i => i.RuleId == "R-ELV-01");
        Assert.All(model.Segments.Concat<object>(model.Nodes), e => Assert.NotNull(e is Segment s ? s.Prov : ((Node)e).Prov));
    }

    [Fact]
    public void Tee_splits_the_through_segment_and_trims_run_and_branch()
    {
        var model = new PipingModel();
        model.Runs.Add(Run("run-001", "line-001", 200, 600, ElevationDatum.CL, (0, 0), (10000, 0)));
        model.Runs.Add(Run("run-002", "line-001", 200, 600, ElevationDatum.CL, (4000, 0), (4000, 3000)));

        var stats = new GraphBuilder(Profile(), Catalog()).Build(model);

        Assert.Equal(3, stats.Segments);
        Assert.Equal(1, stats.Tees);
        Assert.Equal(1, stats.TeeSplits);
        var tee = Assert.Single(model.Nodes, n => n.Kind == NodeKind.Tee);
        Assert.Equal("ASME-B16.9/TEE/DN200", tee.CatalogRef);
        Assert.Equal(3, tee.Segments.Count);
        var branch = model.Segments.Single(s => s.RunId == "run-002");
        Assert.Equal(178, branch.TrimA + branch.TrimB);
        Assert.Equal(13000, stats.TotalLength, 6);
    }

    [Fact]
    public void Reducing_tee_with_smaller_branch_is_not_a_dn_mismatch()
    {
        // I4: nhánh 250 rẽ từ ống góp 600 là tê thu hợp lệ, không được báo R-CON-04; hai đoạn chạy khác DN tại nút vẫn báo.
        var model = new PipingModel();
        model.Runs.Add(Run("run-001", "line-001", 600, 5605, ElevationDatum.CL, (-1500, 0), (1500, 0)));
        model.Runs.Add(Run("run-002", "line-002", 250, 5605, ElevationDatum.CL, (0, 0), (0, 1056)));

        new GraphBuilder(Profile(), Catalog()).Build(model);

        var tee = Assert.Single(model.Nodes, n => n.Kind == NodeKind.Tee);
        Assert.Equal(250, tee.Params!["branchDn"]);
        Assert.DoesNotContain(model.Issues, i => i.RuleId == "R-CON-04");

        var mismatch = new PipingModel();
        mismatch.Runs.Add(Run("run-001", "line-001", 600, 5605, ElevationDatum.CL, (-1500, 0), (0, 0)));
        mismatch.Runs.Add(Run("run-003", "line-001", 400, 5605, ElevationDatum.CL, (0, 0), (1500, 0)));
        mismatch.Runs.Add(Run("run-002", "line-002", 250, 5605, ElevationDatum.CL, (0, 0), (0, 1056)));
        new GraphBuilder(Profile(), Catalog()).Build(mismatch);
        Assert.Contains(mismatch.Issues, i => i.RuleId == "R-CON-04");
    }

    [Fact]
    public void Short_segment_between_fittings_is_blocked_and_odd_angle_warns()
    {
        var model = new PipingModel();
        model.Runs.Add(Run("run-001", "line-001", 200, 600, ElevationDatum.CL, (0, 0), (300, 0), (300, 300), (600, 300)));
        model.Runs.Add(Run("run-002", "line-002", 100, 900, ElevationDatum.CL, (0, 5000), (3000, 5000), (5000, 6000)));

        new GraphBuilder(Profile(), Catalog()).Build(model);

        Assert.Contains(model.Issues, i => i.RuleId == "R-GEO-02");
        Assert.Contains(model.Issues, i => i.RuleId == "R-GEO-01");
    }

    [Fact]
    public void Untagged_run_inherits_from_collinear_and_elbow_neighbours()
    {
        var tagged = Run("run-001", "line-001", 150, 4100, ElevationDatum.BOP, (0, 0), (5000, 0));
        var straight = new Run { Id = "run-002", PlanPoints = new List<Point2> { new(5000, 0), new(9000, 0) }, Prov = new Provenance() };
        var bent = new Run { Id = "run-003", PlanPoints = new List<Point2> { new(9000, 0), new(9000, 3000) }, Prov = new Provenance() };
        var afterTee = new Run { Id = "run-004", PlanPoints = new List<Point2> { new(9000, 3000), new(12000, 3000) }, Prov = new Provenance() };
        var teeBranch = new Run { Id = "run-005", PlanPoints = new List<Point2> { new(9000, 3000), new(9000, 6000) }, Prov = new Provenance() };

        var stats = new TagPropagator(1.0, 0.5).Propagate(new[] { tagged, straight, bent, afterTee, teeBranch });

        // Tại tê (9000, 3000): đoạn đi thẳng qua tê thừa hưởng, nhánh rẽ ngang thì không.
        Assert.Equal(3, stats.Inherited);
        Assert.Equal("line-001", straight.LineId);
        Assert.Equal(150, straight.Dn);
        Assert.Equal(ElevationDatum.BOP, straight.Elevation.Datum);
        Assert.Equal(0.85, straight.TagRef!.Confidence);
        Assert.True(straight.Prov!.Inferred);
        Assert.Equal("line-001", bent.LineId);
        Assert.Equal(0.75, bent.TagRef!.Confidence);
        Assert.Equal(string.Empty, afterTee.LineId);
        Assert.Equal("line-001", teeBranch.LineId);
        Assert.Equal(0.75, teeBranch.TagRef!.Confidence);
    }

    [Fact]
    public void Fitting_block_between_two_run_ends_bridges_them()
    {
        var a = new Run { Id = "run-001", PlanPoints = new List<Point2> { new(0, 0), new(4800, 0) }, Prov = new Provenance() };
        var b = new Run { Id = "run-002", PlanPoints = new List<Point2> { new(5000, 200), new(5000, 4000) }, Prov = new Provenance() };
        var elbow = new PipingAgent.Core.Source.SourceEntity
        {
            Handle = "E1",
            Kind = PipingAgent.Core.Source.SourceKind.Insert,
            Name = "eC6M",
            Center = new Point3(4950, 50, 0),
            Bounds = Box2.Around(new Point2(4950, 50), 150),
        };

        var stats = new FittingBridger(300, 1.0).Bridge(new[] { a, b }, new[] { elbow });

        Assert.Equal(1, stats.Bridged);
        Assert.Equal(new Point2(5000, 0), a.PlanPoints[1]);
        Assert.Equal(new Point2(5000, 0), b.PlanPoints[0]);
        Assert.Contains("E1", a.Prov!.Sources);
    }
}

/// <summary>S3-10: kế hoạch dựng từ IR, xem trước số lượng, không đổi IR.</summary>
public class BuildPlanTests
{
    [Fact]
    public void Plan_from_riser_model_has_two_pipes_a_riser_and_two_elbows()
    {
        var catalog = CatalogFiles.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "catalog"));
        var profile = new ConventionProfile { Id = "t", PipeStandard = "ASME-B36.10", FittingStandard = "ASME-B16.9" };
        var model = new PipingModel();
        model.Lines.Add(new PipingAgent.Core.Ir.Line { Id = "line-001", Tag = "P-1011" });
        model.Runs.Add(new Run { Id = "run-001", LineId = "line-001", Dn = 150, Elevation = new Elevation { Raw = 600, Datum = ElevationDatum.CL }, PlanPoints = new List<Point2> { new(0, 0), new(5000, 0) }, Prov = new Provenance { Sources = new List<string> { "A" } } });
        model.Runs.Add(new Run { Id = "run-002", LineId = "line-001", Dn = 150, Elevation = new Elevation { Raw = 4100, Datum = ElevationDatum.CL }, PlanPoints = new List<Point2> { new(5000, 0), new(5000, 4000) }, Prov = new Provenance { Sources = new List<string> { "B" } } });
        new GraphBuilder(profile, catalog).Build(model);
        var json = PipingAgent.Core.Ir.Json.IrJson.Serialize(model);

        var plan = PipingAgent.Core.Build.BuildPlan.FromModel(model, new PipingAgent.Core.Build.BuildOptions { LayerPattern = "X-{line}-{kind}" });

        Assert.Equal(3, plan.Pipes.Count);
        Assert.Equal(2, plan.Elbows.Count);
        Assert.Empty(plan.Tees);
        Assert.Empty(plan.Skipped);
        Assert.Equal(json, PipingAgent.Core.Ir.Json.IrJson.Serialize(model));
        var bottom = plan.Pipes.Single(p => p.IrId == "seg-001");
        Assert.Equal(new Point3(0, 0, 600), bottom.Start);
        Assert.Equal(new Point3(5000 - 229, 0, 600), bottom.End);
        Assert.Equal("X-P-1011-PIPE", bottom.Layer);
        var elbow = plan.Elbows.First(e => e.Corner == new Point3(5000, 0, 600));
        Assert.Equal(229, elbow.Radius);
        Assert.Equal(new Point3(5000 - 229, 0, 600), elbow.TangentA);
        Assert.Equal(new Point3(5000, 0, 600 + 229), elbow.TangentB);
        Assert.Equal(new Point3(5000 - 229, 0, 600 + 229), elbow.ArcCenter);
        Assert.Equal(229, elbow.ArcCenter.DistanceTo(elbow.TangentA), 6);
        Assert.Equal(229, elbow.ArcCenter.DistanceTo(elbow.TangentB), 6);
        Assert.Equal(3500 - 2 * 229, plan.Pipes.Single(p => p.IrId == "seg-003").Length, 6);
        Assert.Equal(new[] { "X-P-1011-ELBOW", "X-P-1011-PIPE" }, plan.Layers);
        Assert.Equal("A1_B_2", PipingAgent.Core.Build.BuildPlan.Sanitize("A1 B/2"));
    }
}
