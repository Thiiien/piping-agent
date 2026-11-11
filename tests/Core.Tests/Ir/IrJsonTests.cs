using System.Text.Json;
using System.Text.Json.Nodes;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Ir.Json;

namespace Core.Tests.Ir;

/// <summary>Kiểm thử cho việc S1-02: đọc ví dụ của mục 7.5 vào, ghi ra, kết quả giống hệt về nội dung.</summary>
public class IrJsonTests
{
    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "Data", "sample_ir.json");

    [Fact]
    public void Sample_round_trips_without_loss()
    {
        var original = File.ReadAllText(SamplePath);
        var model = IrJson.Deserialize(original);
        var written = IrJson.Serialize(model);
        var reread = IrJson.Deserialize(written);
        var writtenAgain = IrJson.Serialize(reread);

        // Bỏ các giá trị null vì bộ ghi không ghi trường null, rồi so cây JSON.
        var expected = StripNulls(JsonNode.Parse(original)!);
        var actual = JsonNode.Parse(written)!;
        var difference = FirstDifference(expected, actual, "$");
        Assert.True(difference == null, "JSON ghi ra khác JSON đọc vào tại " + difference + "\n" + written);
        Assert.Equal(written, writtenAgain);
    }

    /// <summary>So hai cây JSON theo giá trị: số so bằng double, nên 1.0 và 1 là một.</summary>
    private static string? FirstDifference(JsonNode? a, JsonNode? b, string path)
    {
        if (a is null || b is null) return a is null && b is null ? null : path;
        switch (a)
        {
            case JsonObject oa when b is JsonObject ob:
                foreach (var key in oa.Select(p => p.Key).Union(ob.Select(p => p.Key)))
                {
                    if (!oa.ContainsKey(key) || !ob.ContainsKey(key)) return path + "." + key;
                    var d = FirstDifference(oa[key], ob[key], path + "." + key);
                    if (d != null) return d;
                }

                return null;
            case JsonArray aa when b is JsonArray ab:
                if (aa.Count != ab.Count) return path + ".length";
                for (var i = 0; i < aa.Count; i++)
                {
                    var d = FirstDifference(aa[i], ab[i], $"{path}[{i}]");
                    if (d != null) return d;
                }

                return null;
            case JsonValue va when b is JsonValue vb:
                var ea = va.GetValue<JsonElement>();
                var eb = vb.GetValue<JsonElement>();
                if (ea.ValueKind != eb.ValueKind) return path;
                return ea.ValueKind switch
                {
                    JsonValueKind.Number => ea.GetDouble() == eb.GetDouble() ? null : path,
                    JsonValueKind.String => ea.GetString() == eb.GetString() ? null : path,
                    _ => null,
                };
            default:
                return path;
        }
    }

    [Fact]
    public void Sample_values_are_parsed_into_typed_model()
    {
        var model = IrJson.Deserialize(File.ReadAllText(SamplePath));

        Assert.Equal("1.0", model.SchemaVersion);
        Assert.Equal("mm", model.Units);
        Assert.Equal(80, model.Source.EntityCount);
        Assert.Equal("sample-pump-station", model.Profile!.Id);

        var run = Assert.Single(model.Runs);
        Assert.Equal(150, run.Dn);
        Assert.Equal(ElevationDatum.CL, run.Elevation.Datum);
        Assert.Equal(new Point2(24900, 12000), run.PlanPoints[1]);
        Assert.Equal("leader", run.TagRef!.By);
        Assert.Equal(DecidedBy.Rule, run.Prov!.DecidedBy);

        var riser = model.Segments[0];
        Assert.Null(riser.RunId);
        Assert.Equal(SegmentKind.Riser, riser.Kind);
        Assert.True(riser.Prov!.Inferred);
        Assert.False(model.Segments[1].Prov!.Inferred);

        var elbow = model.Nodes[1];
        Assert.Equal(NodeKind.Elbow, elbow.Kind);
        Assert.Equal(new Point3(21500, 12000, 4100), elbow.Position);
        Assert.Equal(228.6, elbow.Params!["radius"]);
        Assert.Equal(NodeKind.OpenEnd, model.Nodes[2].Kind);

        var valve = Assert.Single(model.Components);
        Assert.Equal(Vector3.UnitX, valve.Axis);
        Assert.Equal("150#", valve.Attributes!["rating"].Value);

        var issue = Assert.Single(model.Issues);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Equal(DecisionOp.AssignTag, issue.Suggestions[0].Op);
        Assert.Equal("P-1001", issue.Suggestions[0].Value!.Value.GetString());

        var decision = Assert.Single(model.Decisions);
        Assert.Equal(DecisionOp.SetDn, decision.Op);
        Assert.Equal(DecidedBy.Agent, decision.By);
        Assert.Equal(150, decision.Value!.Value.GetInt32());
    }

    [Fact]
    public void Empty_model_serializes_with_version_and_units()
    {
        var json = IrJson.Serialize(new PipingModel(), indented: false);
        var node = JsonNode.Parse(json)!;
        Assert.Equal("1.0", (string)node["schemaVersion"]!);
        Assert.Equal("mm", (string)node["units"]!);
        Assert.Empty(node["lines"]!.AsArray());
    }

    [Fact]
    public void Stream_round_trip()
    {
        var model = IrJson.Deserialize(File.ReadAllText(SamplePath));
        using var stream = new MemoryStream();
        IrJson.Serialize(model, stream);
        stream.Position = 0;
        var reread = IrJson.Deserialize(stream);
        Assert.Equal(model.Nodes.Count, reread.Nodes.Count);
        Assert.Equal(IrJson.Serialize(model), IrJson.Serialize(reread));
    }

    [Fact]
    public void Unknown_schema_version_is_rejected_with_clear_message()
    {
        var ex = Assert.Throws<IrFormatException>(() => IrJson.Deserialize("""{ "schemaVersion": "9.7", "units": "mm" }"""));
        Assert.Contains("9.7", ex.Message);
        Assert.Contains("1.0", ex.Message);
    }

    [Fact]
    public void Missing_schema_version_is_rejected()
    {
        var ex = Assert.Throws<IrFormatException>(() => IrJson.Deserialize("""{ "units": "mm" }"""));
        Assert.Contains("schemaVersion", ex.Message);
    }

    [Fact]
    public void Malformed_json_is_rejected()
    {
        Assert.Throws<IrFormatException>(() => IrJson.Deserialize("{ \"schemaVersion\": "));
        Assert.Throws<IrFormatException>(() => IrJson.Deserialize("null"));
    }

    [Fact]
    public void Point_with_wrong_arity_is_rejected()
    {
        var json = """{ "schemaVersion": "1.0", "units": "mm", "nodes": [ { "id": "N1", "position": [1, 2], "kind": "elbow" } ] }""";
        var ex = Assert.Throws<IrFormatException>(() => IrJson.Deserialize(json));
        Assert.Contains("3 số", ex.Message);
    }

    [Fact]
    public void Enums_are_written_as_snake_case_and_datum_as_is()
    {
        var model = new PipingModel();
        model.Nodes.Add(new Node { Id = "N1", Kind = NodeKind.OpenEnd });
        model.Runs.Add(new Run { Id = "R1", LineId = "L1", Elevation = new Elevation { Raw = 109000, Datum = ElevationDatum.BOP } });
        var json = IrJson.Serialize(model, indented: false);
        Assert.Contains("\"kind\":\"open_end\"", json);
        Assert.Contains("\"datum\":\"BOP\"", json);
        Assert.Contains("\"decidedBy\"", IrJson.Serialize(IrJson.Deserialize(File.ReadAllText(SamplePath))));
    }

    private static JsonNode StripNulls(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                var clean = new JsonObject();
                foreach (var pair in obj)
                {
                    if (pair.Value is null) continue;
                    clean[pair.Key] = StripNulls(pair.Value);
                }

                return clean;
            case JsonArray array:
                var list = new JsonArray();
                foreach (var item in array) list.Add(item is null ? null : StripNulls(item));
                return list;
            default:
                return JsonNode.Parse(node.ToJsonString())!;
        }
    }
}
