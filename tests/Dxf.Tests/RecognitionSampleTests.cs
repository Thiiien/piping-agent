using PipingAgent.Core.Catalog;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Ir.Json;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Source;
using PipingAgent.Dxf;
using PipingAgent.Hosting;

namespace Dxf.Tests;

/// <summary>
/// Tiêu chí Sprint 2 trên bản vẽ mẫu: mọi đối tượng được gán vai trò, 11 nhãn tách đúng, 11 đoạn chạy,
/// 11 nhãn gắn đúng 11 đoạn chạy.
/// </summary>
public class RecognitionSampleTests
{
    private static string Data(string name) => Path.Combine(AppContext.BaseDirectory, "Data", name);

    private static (RecognitionResult Result, IReadOnlyList<SourceEntity> Entities) Run()
    {
        var profile = ProfileFiles.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "sample-pump-station.json"));
        Assert.True(profile.Ok, string.Join("\n", profile.Errors));
        var catalog = CatalogFiles.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "catalog"));
        var reader = DxfDrawingReader.Open(Data("piping_plan_2d.dxf"));
        var entities = reader.ReadEntities(EntityFilter.All);
        var result = RecognitionPipeline.Run(entities, profile.Profile!, catalog);
        return (result, entities);
    }

    /// <summary>Nhãn và một điểm nằm trên đoạn chạy mà nhãn phải gắn vào, theo prototype/make_sample_drawing.py.</summary>
    private static readonly (string Tag, double X, double Y)[] Expected =
    {
        ("P-1001 DN200 EL+600", 8500, 14000),
        ("P-1002 DN200 EL+600", 8500, 6000),
        ("P-1003 DN200 EL+600", 12500, 10000),
        ("P-1011 DN150 EL+600", 15300, 12550),
        ("P-1011 DN150 EL+4100", 23000, 19500),
        ("P-1012 DN150 EL+600", 15300, 7450),
        ("P-1012 DN150 EL+4100", 23700, 19500),
        ("P-1021 DN100 EL+4100", 22300, 19500),
        ("P-1021 DN100 EL+5500", 6000, 16500),
        ("P-1022 DN100 EL+5500", 6000, 8500),
        ("U-2001 DN80 EL+4100", 24400, 1500),
    };

    [Fact]
    public void Every_entity_of_the_sample_is_classified()
    {
        var (r, _) = Run();
        var report = r.Classification.Report;
        Assert.Equal(80, report.Total);
        Assert.Equal(0, report.Unclassified);
        Assert.Equal(11, report.ByRole[EntityRoles.PipeCenterline]);
        Assert.Equal(11, report.ByRole[EntityRoles.PipeTag]);
        Assert.Equal(4, report.ByRole[EntityRoles.PipeTagLeader]);
        Assert.Equal(3, report.ByRole[EntityRoles.RiserSymbol]);
        Assert.Equal(7, report.ByRole[EntityRoles.InlineComponent]);
    }

    [Fact]
    public void Eleven_tags_parse_and_eleven_runs_are_built()
    {
        var (r, _) = Run();
        Assert.Equal(11, r.TagReads.Count);
        Assert.All(r.TagReads, t => Assert.True(t.Matched, t.Entity.Text));
        Assert.Equal(11, r.RunBuild.Runs.Count);
        Assert.Equal(11, r.Model.Runs.Count);
    }

    [Fact]
    public void Eleven_tags_attach_to_the_right_runs()
    {
        var (r, _) = Run();
        var tol = new Tolerance(1.0, 0.5);
        foreach (var (text, x, y) in Expected)
        {
            var att = Assert.Single(r.TagAttach.Attachments, a => a.Tag.Tag?.Text == text);
            Assert.True(att.Attached, $"'{text}' chưa gắn, ứng viên: {string.Join(", ", att.Candidates.Select(c => $"{c.Run.Id}@{c.Distance:0}"))}");
            var run = att.Run!;
            var onRun = run.PlanPoints.Zip(run.PlanPoints.Skip(1), (a, b) => new Segment2(a, b)).Any(s => s.Contains(new Point2(x, y), tol));
            Assert.True(onRun, $"'{text}' gắn vào {run.Id} nhưng điểm ({x}, {y}) không nằm trên đoạn chạy đó. Cách gắn {att.Method}, tin cậy {att.Confidence}");
            Assert.True(att.Confidence >= TagAttacher.RejectThreshold);
        }

        Assert.Equal(11, r.Model.Runs.Count(run => run.TagRef != null));
        Assert.Equal(8, r.Model.Lines.Count);
        Assert.DoesNotContain(r.Model.Issues, i => i.RuleId == "R-TAG-01" || i.RuleId == "R-TAG-03" || i.RuleId == "R-TAG-04" || i.RuleId == "R-TAG-05");
    }

    [Fact]
    public void Leader_tags_use_leader_method_with_full_confidence()
    {
        var (r, _) = Run();
        foreach (var text in new[] { "P-1011 DN150 EL+4100", "P-1012 DN150 EL+4100", "P-1021 DN100 EL+4100", "U-2001 DN80 EL+4100" })
        {
            var att = r.TagAttach.Attachments.Single(a => a.Tag.Tag?.Text == text);
            Assert.Equal(AttachMethods.Leader, att.Method);
            Assert.Equal(1.0, att.Confidence);
        }
    }

    [Fact]
    public void Model_has_dn_elevation_and_provenance_and_validates()
    {
        var (r, _) = Run();
        foreach (var run in r.Model.Runs)
        {
            Assert.True(run.Dn > 0, run.Id);
            Assert.Equal(ElevationDatum.CL, run.Elevation.Datum);
            Assert.NotNull(run.Prov);
            Assert.NotEmpty(run.Prov!.Sources);
            Assert.False(string.IsNullOrEmpty(run.LineId));
        }

        var findings = ModelValidator.Validate(r.Model);
        Assert.DoesNotContain(findings, f => f.IsError);
        var json = IrJson.Serialize(r.Model);
        var back = IrJson.Deserialize(json);
        Assert.Equal(11, back.Runs.Count);
    }

    [Fact]
    public void Pipeline_is_deterministic()
    {
        var (a, _) = Run();
        var (b, _) = Run();
        Assert.Equal(IrJson.Serialize(a.Model), IrJson.Serialize(b.Model));
    }
}

/// <summary>Tiêu chí Sprint 3 trên bản vẽ mẫu: 26 đoạn ống, 3 ống đứng, 15 co, 3 tê, tổng chiều dài tâm 115,9 m.</summary>
public class GraphSampleTests
{
    [Fact]
    public void Sample_graph_matches_plan_numbers()
    {
        var profile = ProfileFiles.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "sample-pump-station.json")).Profile!;
        var catalog = CatalogFiles.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "catalog"));
        var entities = DxfDrawingReader.Open(Path.Combine(AppContext.BaseDirectory, "Data", "piping_plan_2d.dxf")).ReadEntities(EntityFilter.All);

        var equipment = EquipmentFiles.LoadLayers(Path.Combine(AppContext.BaseDirectory, "equipment"));
        var r = RecognitionPipeline.Run(entities, profile, catalog, null, equipment);

        Assert.Equal(26, r.Graph.Segments);
        Assert.Equal(3, r.Graph.Risers);
        Assert.Equal(15, r.Graph.Elbows);
        Assert.Equal(3, r.Graph.Tees);
        Assert.Equal(115.9, r.Graph.TotalLength / 1000.0, 1);
        // Chỉ còn 5 đầu hở tại ranh giới và một thông tin về danh mục chưa ký.
        Assert.DoesNotContain(r.Model.Issues, i => i.Severity == Severity.Blocker);
        Assert.Equal(5, r.Model.Issues.Count(i => i.RuleId == "R-CON-01"));
        Assert.Equal(6, r.Model.Issues.Count);
        Assert.All(r.Model.Segments, s => Assert.True(s.Length - s.TrimA - s.TrimB >= 0, s.Id));
        Assert.All(r.Model.Segments, s => Assert.NotNull(s.Prov));
        Assert.All(r.Model.Nodes, n => Assert.NotNull(n.Prov));
        Assert.All(r.Model.Nodes.Where(n => n.Kind == NodeKind.Elbow), n => Assert.Equal(90, n.Params!["angle"], 3));
        Assert.DoesNotContain(r.Model.Issues, i => i.RuleId == "R-CON-03");
        Assert.Equal(11, r.Model.Runs.Count(x => x.Elevation.Centerline.HasValue));
        var findings = ModelValidator.Validate(r.Model);
        Assert.DoesNotContain(findings, f => f.IsError);
    }
}

/// <summary>Tiêu chí Sprint 4 trên bản vẽ mẫu: 7 van, 2 bồn, 2 bơm, 8 đầu chờ, 5 đầu hở, 8 cột, 10 dầm.</summary>
public class Sprint4SampleTests
{
    private static RecognitionResult Run()
    {
        var profile = ProfileFiles.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "sample-pump-station.json")).Profile!;
        var catalog = CatalogFiles.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "catalog"));
        var equipment = EquipmentFiles.LoadLayers(Path.Combine(AppContext.BaseDirectory, "equipment"));
        Assert.Empty(equipment.Errors);
        var entities = DxfDrawingReader.Open(Path.Combine(AppContext.BaseDirectory, "Data", "piping_plan_2d.dxf")).ReadEntities(EntityFilter.All);
        return RecognitionPipeline.Run(entities, profile, catalog, null, equipment);
    }

    [Fact]
    public void Components_equipment_nozzles_and_structures_match_plan()
    {
        var r = Run();
        var m = r.Model;
        Assert.Equal(7, m.Components.Count);
        Assert.All(m.Components, c => Assert.Equal("valve_gate", c.Type));
        Assert.All(m.Components, c => Assert.False(string.IsNullOrEmpty(c.SegmentId), c.Id));
        Assert.All(m.Components, c => Assert.True(c.Length > 0, c.Id));
        Assert.Contains(m.Components, c => Math.Abs(c.Axis.Y) > 0.99);

        Assert.Equal(4, m.Equipment.Count);
        Assert.Equal(2, m.Equipment.Count(e => e.Type == "tank_vertical"));
        Assert.Equal(2, m.Equipment.Count(e => e.Type == "pump"));
        var tank = m.Equipment.Single(e => e.Tag == "T-101");
        Assert.Equal(5000, tank.Geometry.Params["D"]);
        Assert.Equal(6000, tank.Geometry.Params["H"]);
        Assert.Equal(2, tank.Nozzles.Count);
        var pump = m.Equipment.Single(e => e.Tag == "P-101A");
        Assert.Equal(2400, pump.Geometry.Params["L"]);
        Assert.Equal(1100, pump.Geometry.Params["W"]);

        Assert.Equal(8, r.Nozzles.Nozzles);
        Assert.Equal(5, r.Nozzles.OpenEnds);
        Assert.Equal(8, m.Nodes.Count(n => n.Kind == NodeKind.Nozzle));
        Assert.All(m.Nodes.Where(n => n.Kind == NodeKind.Nozzle), n => Assert.False(string.IsNullOrEmpty(n.EquipmentId)));

        Assert.Equal(8, m.Structures.Count(s => s.Type == "column"));
        Assert.Equal(10, m.Structures.Count(s => s.Type == "beam"));
        Assert.All(m.Structures, s => Assert.Equal(4000, s.Tos));

        Assert.DoesNotContain(m.Issues, i => i.RuleId == "R-EQP-04" || i.RuleId == "R-GEO-06" || i.RuleId == "R-CAT-02" || i.RuleId == "R-CON-05");
        Assert.Equal(5, m.Issues.Count(i => i.RuleId == "R-CON-01"));
        Assert.DoesNotContain(ModelValidator.Validate(m), f => f.IsError);
        Assert.NotNull(IrJson.Deserialize(IrJson.Serialize(m)));
    }
}

/// <summary>S5-04: đối chứng tự động với prototype/data/model.json trên bản vẽ mẫu, mục 13.3.</summary>
public class PrototypeComparisonTests
{
    [Fact]
    public void All_counters_match_the_prototype_statistics()
    {
        var profile = ProfileFiles.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "sample-pump-station.json")).Profile!;
        var catalog = CatalogFiles.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "catalog"));
        var equipment = EquipmentFiles.LoadLayers(Path.Combine(AppContext.BaseDirectory, "equipment"));
        var entities = DxfDrawingReader.Open(Path.Combine(AppContext.BaseDirectory, "Data", "piping_plan_2d.dxf")).ReadEntities(EntityFilter.All);
        var r = RecognitionPipeline.Run(entities, profile, catalog, null, equipment);
        var m = r.Model;

        // Số liệu của prototype, chép từ prototype/data/model.json mục stats.
        Assert.Equal(11, m.Runs.Count);
        Assert.Equal(26, m.Segments.Count);
        Assert.Equal(11, r.TagReads.Count(t => t.Matched));
        Assert.Equal(new[] { "P-1001", "P-1002", "P-1003", "P-1011", "P-1012", "P-1021", "P-1022", "U-2001" }, m.Lines.Select(l => l.Tag).OrderBy(t => t));
        Assert.Equal(3, m.Segments.Count(s => s.Kind == SegmentKind.Riser));
        Assert.Equal(15, m.Nodes.Count(n => n.Kind == NodeKind.Elbow));
        Assert.Equal(3, m.Nodes.Count(n => n.Kind == NodeKind.Tee));
        Assert.Equal(8, m.Nodes.Count(n => n.Kind == NodeKind.Nozzle));
        Assert.Equal(5, m.Nodes.Count(n => n.Kind == NodeKind.OpenEnd));
        Assert.Equal(7, m.Components.Count);
        Assert.Equal(2, m.Equipment.Count(e => e.Type == "tank_vertical"));
        Assert.Equal(2, m.Equipment.Count(e => e.Type == "pump"));
        Assert.Equal(8, m.Structures.Count(s => s.Type == "column"));
        Assert.Equal(10, m.Structures.Count(s => s.Type == "beam"));
        Assert.Equal(115.9, m.Segments.Sum(s => s.Length) / 1000.0, 1);
        var byDn = m.Segments.GroupBy(s => s.Dn).ToDictionary(g => g.Key, g => Math.Round(g.Sum(s => s.Length) / 1000.0, 1));
        Assert.Equal(18.0, byDn[80]);
        Assert.Equal(33.7, byDn[100]);
        Assert.Equal(43.6, byDn[150]);
        Assert.Equal(20.6, byDn[200]);
        // Chiều dài là chiều dài tâm chưa trừ phụ kiện, giống prototype. Chiều dài ống thẳng sau khi trừ co và tê theo ASME B16.9
        // nhỏ hơn, 106,2 m, vì prototype không có bảng phụ kiện.
    }
}
