using System.Text.Json;
using PipingAgent.Core.Decisions;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Rules;
using PipingAgent.Core.Source;
using PipingAgent.Hosting;

namespace Core.Tests.Decisions;

/// <summary>S5-05, S5-06: quyết định áp lại theo handle nguồn, mồ côi khi đối tượng mất, tệp phiên đọc ghi.</summary>
public class DecisionTests
{
    private static ConventionProfile Profile() => ProfileFiles.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "sample-pump-station.json")).Profile!;

    private static List<SourceEntity> Entities()
    {
        SourceEntity Line(string h, double x1, double y1, double x2, double y2)
        {
            var e = new SourceEntity { Handle = h, Kind = SourceKind.Line, NativeType = "LINE", Layer = "PIPE", Points = new List<Point3> { new(x1, y1, 0), new(x2, y2, 0) } };
            e.Bounds = e.ComputeBounds();
            return e;
        }

        return new List<SourceEntity> { Line("A1", 0, 0, 5000, 0), Line("B1", 0, 3000, 5000, 3000) };
    }

    [Fact]
    public void Assign_tag_decision_survives_rerun_and_goes_orphan_when_source_disappears()
    {
        var profile = Profile();
        var decision = SessionFile.NewDecision(DecisionOp.AssignTag, new Dictionary<string, string> { ["source"] = "A1" }, JsonSerializer.SerializeToElement("P-9001 DN150 EL+1200"));

        var first = RecognitionPipeline.Run(Entities(), profile, null, null, null, null, new[] { decision });
        var run = first.Model.Runs.Single(r => r.Prov!.Sources.Contains("A1"));
        Assert.Equal(150, run.Dn);
        Assert.Equal(1200, run.Elevation.Raw);
        Assert.Equal("user", run.TagRef!.By);
        Assert.Equal(DecidedBy.User, run.Prov!.DecidedBy);
        Assert.Contains(first.Model.Lines, l => l.Tag == "P-9001");
        Assert.Equal(1, first.Decisions.Applied);
        Assert.False(decision.Orphan);
        Assert.DoesNotContain(first.Model.Issues, i => i.RuleId == "R-TAG-01" && i.Elements.Contains(run.Id));

        // Chạy lại với bản vẽ không còn đối tượng A1: quyết định thành mồ côi, có vấn đề để người dùng xử lý.
        var second = RecognitionPipeline.Run(Entities().Where(e => e.Handle != "A1").ToList(), profile, null, null, null, null, new[] { decision });
        Assert.True(decision.Orphan);
        Assert.Equal(1, second.Decisions.Orphaned);
        Assert.Contains(second.Model.Issues, i => i.RuleId == "R-DEC-01" && i.Sources.Contains("A1"));
    }

    [Fact]
    public void Ignore_connect_waive_and_dn_decisions_apply()
    {
        var profile = Profile();
        var entities = Entities();
        entities.Add(new SourceEntity { Handle = "C1", Kind = SourceKind.Line, NativeType = "LINE", Layer = "PIPE", Points = new List<Point3> { new(5100, 0, 0), new(9000, 0, 0) } });
        var decisions = new[]
        {
            SessionFile.NewDecision(DecisionOp.IgnoreSource, new Dictionary<string, string> { ["source"] = "B1" }, null),
            SessionFile.NewDecision(DecisionOp.Connect, new Dictionary<string, string> { ["source"] = "A1", ["source2"] = "C1" }, null),
            SessionFile.NewDecision(DecisionOp.SetDn, new Dictionary<string, string> { ["source"] = "A1" }, JsonSerializer.SerializeToElement(100)),
            SessionFile.NewDecision(DecisionOp.WaiveIssue, new Dictionary<string, string> { ["rule"] = "R-TAG-01", ["source"] = "A1" }, null, "đoạn thử"),
        };

        var r = RecognitionPipeline.Run(entities, profile, null, null, null, null, decisions);

        Assert.DoesNotContain(r.Model.Runs, run => run.Prov!.Sources.Contains("B1"));
        var a = r.Model.Runs.Single(run => run.Prov!.Sources.Contains("A1"));
        var c = r.Model.Runs.Single(run => run.Prov!.Sources.Contains("C1"));
        Assert.Equal(a.PlanPoints[^1], c.PlanPoints[0]);
        Assert.Equal(100, a.Dn);
        var waived = r.Model.Issues.Single(i => i.RuleId == "R-TAG-01" && i.Elements.Contains(a.Id));
        Assert.Equal(IssueStatus.Waived, waived.Status);
        Assert.Equal("đoạn thử", waived.Waiver!.Reason);
        Assert.Equal(4, r.Decisions.Applied);
        Assert.Equal(4, r.Model.Decisions.Count);
    }

    [Fact]
    public void Session_file_round_trips()
    {
        var s = new SessionFile
        {
            Drawing = "x.dwg",
            Fingerprint = "sha256:abc",
            ProfileId = "sample-pump-station",
            SavedAt = SessionFile.Now(),
            Decisions = { SessionFile.NewDecision(DecisionOp.SetDn, new Dictionary<string, string> { ["source"] = "A1" }, JsonSerializer.SerializeToElement(150)) },
            Model = new PipingModel(),
        };
        var json = SessionFile.Serialize(s);
        var back = SessionFile.Deserialize(json);
        Assert.Equal("sha256:abc", back.Fingerprint);
        Assert.Single(back.Decisions);
        Assert.Equal(150, back.Decisions[0].Value!.Value.GetInt32());
        Assert.Equal(DecisionStatus.Approved, back.Decisions[0].Status);
        Assert.Equal("x.dwg.pa.json", SessionFile.PathFor("x.dwg"));
        Assert.Throws<PipingAgent.Core.Ir.Json.IrFormatException>(() => SessionFile.Deserialize("{\"schemaVersion\":\"9.9\"}"));
    }
}

/// <summary>S5-01 đến S5-03: mỗi luật có một ca phát hiện đúng và một ca không báo nhầm; bản vẽ mẫu sạch không có vấn đề chặn.</summary>
public class RuleMatrixTests
{
    private static ConventionProfile Profile() => new() { Id = "t", PipeStandard = "ASME-B36.10", FittingStandard = "ASME-B16.9" };

    [Fact]
    public void R_GEN_02_units_and_R_GEO_04_overlap_detect_and_stay_quiet()
    {
        var model = new PipingModel { Source = new SourceInfo { UnitsNeedConfirmation = true } };
        Assert.Contains(RuleRunner.Run(model, Profile()).Issues, i => i.RuleId == "R-GEN-02");
        model.Source.UnitsNeedConfirmation = false;
        Assert.DoesNotContain(RuleRunner.Run(model, Profile()).Issues, i => i.RuleId == "R-GEN-02");

        var m = new PipingModel();
        m.Nodes.Add(new Node { Id = "n1", Position = new Point3(0, 0, 0) });
        m.Nodes.Add(new Node { Id = "n2", Position = new Point3(2000, 0, 0) });
        m.Segments.Add(new Segment { Id = "s1", NodeA = "n1", NodeB = "n2", Length = 2000, TrimA = 229, Dn = 150, Od = 168.3 });
        m.Components.Add(new Component { Id = "c1", SegmentId = "s1", Position = new Point3(1000, 0, 0), Length = 267 });
        m.Components.Add(new Component { Id = "c2", SegmentId = "s1", Position = new Point3(1700, 0, 0), Length = 267 });
        Assert.DoesNotContain(RuleRunner.Run(m, Profile()).Issues, i => i.RuleId == "R-GEO-04");
        m.Components.Add(new Component { Id = "c3", SegmentId = "s1", Position = new Point3(1100, 0, 0), Length = 267 });
        m.Components.Add(new Component { Id = "c4", SegmentId = "s1", Position = new Point3(250, 0, 0), Length = 267 });
        var issues = RuleRunner.Run(m, Profile()).Issues.Where(i => i.RuleId == "R-GEO-04").ToList();
        Assert.Contains(issues, i => i.Message.Contains("chồng lên nhau"));
        Assert.Contains(issues, i => i.Message.Contains("phụ kiện ở đầu"));
    }

    [Fact]
    public void Every_phase_one_rule_is_defined_and_has_a_producer()
    {
        // Luật nào cũng phải có định nghĩa trong danh mục để hồ sơ ghi đè được mức, và có mã sinh ra nó trong lõi.
        var phaseOne = RuleCatalog.All.Where(r => r.Phase == 1).Select(r => r.Id).ToList();
        Assert.True(phaseOne.Count >= 27, $"Chỉ có {phaseOne.Count} luật giai đoạn 1");
        var sources = Directory.GetFiles(FindRepo(), "*.cs", SearchOption.AllDirectories)
            .Where(p => p.Contains(Path.Combine("src", "PipingAgent.Core")) && !p.Contains("RuleCatalog"))
            .Select(File.ReadAllText).ToList();
        var missing = phaseOne.Where(id => !sources.Any(s => s.Contains("\"" + id + "\""))).ToList();
        Assert.True(missing.Count == 0, "Luật chưa có mã sinh: " + string.Join(", ", missing));
    }

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PipingAgent.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }
}

/// <summary>S6-02, S6-03, S6-05: xuất khối lượng, báo cáo, chấm điểm hồi quy.</summary>
public class ExportTests
{
    private static PipingModel Model()
    {
        var m = new PipingModel();
        m.Lines.Add(new PipingAgent.Core.Ir.Line { Id = "l1", Tag = "P-1" });
        m.Nodes.Add(new Node { Id = "n1", Position = new Point3(0, 0, 0), Kind = NodeKind.OpenEnd, Segments = { "s1" } });
        m.Nodes.Add(new Node { Id = "n2", Position = new Point3(5000, 0, 0), Kind = NodeKind.Elbow, Segments = { "s1" } });
        m.Segments.Add(new Segment { Id = "s1", LineId = "l1", NodeA = "n1", NodeB = "n2", Dn = 150, Od = 168.3, Length = 5000, TrimB = 229 });
        m.Components.Add(new Component { Id = "c1", SegmentId = "s1", Type = "valve_gate", Dn = 150, Length = 267 });
        m.Issues.Add(new Issue { Id = "i1", RuleId = "R-CON-01", Severity = Severity.Warning, Message = "hở", Status = IssueStatus.Waived, Waiver = new Waiver { By = "user", Reason = "ranh giới", At = "t" } });
        return m;
    }

    [Fact]
    public void Quantity_csv_report_and_scorer_reflect_the_model()
    {
        var m = Model();
        var rows = PipingAgent.Core.Export.QuantityExport.Rows(m);
        var row = Assert.Single(rows);
        Assert.Equal("P-1", row.Line);
        Assert.Equal(4.771, row.PipeLengthM, 3);
        Assert.Equal(1, row.Elbows);
        Assert.Equal(1, row.Valves);
        var csv = PipingAgent.Core.Export.QuantityExport.ToCsv(m);
        Assert.StartsWith("﻿Tuyến;DN", csv);
        Assert.Contains("P-1;150;4.77;1;0;1;0;0", csv);

        var report = PipingAgent.Core.Export.ConversionReport.ToMarkdown(new RecognitionResult { Model = m }, "thử");
        Assert.Contains("# Báo cáo chuyển đổi thử", report);
        Assert.Contains("Vấn đề đã bỏ qua: 1", report);
        Assert.Contains("Lý do: ranh giới", report);

        var score = PipingAgent.Core.Export.RegressionScorer.Compare(m, new PipingAgent.Core.Export.RegressionScorer.Expected { Segments = 1, Elbows = 1, Valves = 2, PipeLengthM = 5.0, Lines = { "P-1", "P-2" } });
        Assert.Equal(3, score.Passed);
        Assert.Equal(1, score.Failed);
        Assert.Equal(new[] { "P-2" }, score.MissingLines);
        Assert.False(score.Ok);
    }
}
