using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Ir.Json;

namespace Core.Tests.Ir;

/// <summary>
/// Kiểm thử cho việc S1-03: IR hợp lệ qua được lược đồ ở schemas/piping-ir-1.0.json,
/// IR sai bị từ chối kèm đường dẫn tới trường sai.
/// </summary>
public class SchemaTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromFile(
        Path.Combine(AppContext.BaseDirectory, "schemas", "piping-ir-1.0.json"));

    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "Data", "sample_ir.json");

    private static readonly EvaluationOptions Options = new() { OutputFormat = OutputFormat.Hierarchical };

    private static EvaluationResults Evaluate(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Schema.Evaluate(document.RootElement.Clone(), Options);
    }

    /// <summary>Vị trí trong tài liệu của mọi nút kết quả có lỗi, duyệt đệ quy cây kết quả.</summary>
    private static IEnumerable<string> FailedPaths(EvaluationResults results)
    {
        if (!results.IsValid && results.Errors is { Count: > 0 })
        {
            yield return results.InstanceLocation.ToString();
        }

        foreach (var child in results.Details ?? new List<EvaluationResults>())
        {
            foreach (var path in FailedPaths(child)) yield return path;
        }
    }

    [Fact]
    public void Sample_passes_schema()
    {
        var results = Evaluate(File.ReadAllText(SamplePath));
        Assert.True(results.IsValid, string.Join("\n", FailedPaths(results)));
    }

    [Fact]
    public void Serialized_output_of_sample_passes_schema()
    {
        var model = IrJson.Deserialize(File.ReadAllText(SamplePath));
        var results = Evaluate(IrJson.Serialize(model));
        Assert.True(results.IsValid, string.Join("\n", FailedPaths(results)));
    }

    [Fact]
    public void Empty_model_passes_schema()
    {
        var results = Evaluate(IrJson.Serialize(new PipingModel()));
        Assert.True(results.IsValid, string.Join("\n", FailedPaths(results)));
    }

    [Fact]
    public void Fully_populated_model_passes_schema()
    {
        var model = new PipingModel
        {
            Source = new SourceInfo
            {
                File = "a.dxf", Fingerprint = "sha256:1", ReadAt = "2026-11-10T00:00:00Z", EntityCount = 3,
                Region = new PipingAgent.Core.Geometry.Box2(new(0, 0), new(10, 10)), Sheet = "005",
            },
            Profile = new ProfileRef { Id = "p", Version = 2 },
            Generator = new GeneratorInfo { Name = "PipingAgent", Version = "0.1.0" },
        };
        model.Lines.Add(new Line { Id = "L1", Tag = "T", Service = "LPS", Spec = "A1AS", Insulation = new Insulation { Type = "HS", Thickness = 40 }, Prov = new Provenance { Sources = { "1" } } });
        model.Runs.Add(new Run
        {
            Id = "R1", LineId = "L1", Dn = 100,
            Elevation = new Elevation { Raw = 109000, Datum = ElevationDatum.BOP, Centerline = 109057.15, SupportHeight = 0 },
            PlanPoints = { new(0, 0), new(1000, 0) },
            TagRef = new TagRef { Text = "t", By = "adjacent", SourceHandle = "2", MergedHandles = new List<string> { "3" }, Position = new(5, 5), Confidence = 0.85 },
            Prov = new Provenance { Sources = { "4" }, Rule = "R", Confidence = 1, DecidedBy = DecidedBy.Rule },
        });
        model.Nodes.Add(new Node { Id = "N1", Kind = NodeKind.Continuation, Prov = new Provenance { Inferred = true } });
        model.Nodes.Add(new Node { Id = "N2", Kind = NodeKind.Tee, Params = new Dictionary<string, double> { ["branchDn"] = 50 }, Prov = new Provenance { Inferred = true } });
        model.Segments.Add(new Segment { Id = "S1", LineId = "L1", RunId = "R1", NodeA = "N1", NodeB = "N2", Kind = SegmentKind.Sloped, Dn = 100, Od = 114.3, Length = 1000, Prov = new Provenance { Sources = { "4" } } });
        model.Components.Add(new Component
        {
            Id = "C1", SegmentId = "S1", Type = "unclassified", Dn = 100, Length = 0, Tag = "V1",
            TagRef = new TagRef { Text = "V1", By = "block_attribute" },
            Attributes = new Dictionary<string, AttributeValue> { ["rating"] = new AttributeValue { Value = "150#", By = "legend", Confidence = 0.7 } },
            Prov = new Provenance { Sources = { "5" } },
        });
        model.Equipment.Add(new PipingAgent.Core.Ir.Equipment { Id = "E1", Tag = "P-1", Type = "pump", Nozzles = { new Nozzle { Id = "N1", Dn = 100 } }, Prov = new Provenance { Sources = { "6" } } });
        model.Structures.Add(new Structure { Id = "ST1", Type = "beam", Points = { new(0, 0, 5000), new(1000, 0, 5000) }, Section = "H300x300", Tos = 5000, Prov = new Provenance { Sources = { "7" } } });
        model.Issues.Add(new Issue
        {
            Id = "I1", RuleId = "R-X", Severity = Severity.Blocker, Status = IssueStatus.Waived, Elements = { "S1" }, Sources = { "4" },
            Position = new(0, 0, 0), Message = "m",
            Suggestions = { new Suggestion { Op = DecisionOp.SplitLine, Target = { ["segment"] = "S1" }, Label = "Tách" } },
            Waiver = new Waiver { By = "user", Reason = "ok", At = "2026-11-10T00:00:00Z" },
        });
        model.Decisions.Add(new Decision { Id = "D1", Op = DecisionOp.WaiveIssue, Target = { ["issue"] = "I1" }, By = DecidedBy.User, Status = DecisionStatus.Approved, At = "2026-11-10T00:00:00Z", ApprovedBy = "user", ApprovedAt = "2026-11-10T00:00:00Z" });

        var json = IrJson.Serialize(model);
        var results = Evaluate(json);
        Assert.True(results.IsValid, string.Join("\n", FailedPaths(results)) + "\n" + json);
        Assert.Empty(ModelValidator.Validate(model).Where(f => f.IsError));
    }

    [Theory]
    [InlineData("""{ "schemaVersion": "1.0", "units": "inch", "source": {} }""", "/units")]
    [InlineData("""{ "schemaVersion": "1.0", "units": "mm", "source": {}, "nodes": [ { "id": "N1", "position": [1, 2, 3], "kind": "bend", "segments": [] } ] }""", "/nodes/0/kind")]
    [InlineData("""{ "schemaVersion": "1.0", "units": "mm", "source": {}, "runs": [ { "id": "R1", "lineId": "L1", "dn": 100, "elevation": { "raw": 1, "datum": "CL" }, "planPoints": [[0, 0]] } ] }""", "/runs/0/planPoints")]
    [InlineData("""{ "schemaVersion": "1.0", "units": "mm", "source": {}, "lines": [ { "id": "L1", "tag": "T", "colour": "red" } ] }""", "/lines/0")]
    [InlineData("""{ "schemaVersion": "1.0", "units": "mm", "source": {}, "decisions": [ { "id": "D1", "op": "set_dn", "target": { "run": "R1" }, "by": "agent", "status": "pending", "at": "x" } ] }""", "/decisions/0")]
    [InlineData("""{ "schemaVersion": "1.0", "units": "mm", "source": {}, "segments": [ { "id": "S1", "lineId": "L1", "nodeA": "a", "nodeB": "b", "kind": "riser", "dn": 100, "od": 1, "length": -5, "trimA": 0, "trimB": 0 } ] }""", "/segments/0/length")]
    public void Invalid_documents_are_rejected_at_the_offending_field(string json, string expectedPath)
    {
        var results = Evaluate(json);
        Assert.False(results.IsValid);
        var paths = FailedPaths(results).ToList();
        Assert.Contains(paths, p => p == expectedPath);
    }

    [Fact]
    public void Schema_file_declares_version_1_0()
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schemas", "piping-ir-1.0.json")))!;
        Assert.Equal("1.0", (string)node["properties"]!["schemaVersion"]!["const"]!);
        Assert.Equal(PipingAgent.Core.CoreInfo.IrSchemaVersion, (string)node["properties"]!["schemaVersion"]!["const"]!);
    }
}
