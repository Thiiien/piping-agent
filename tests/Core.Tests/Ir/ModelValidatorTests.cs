using PipingAgent.Core.Ir;
using PipingAgent.Core.Ir.Json;

namespace Core.Tests.Ir;

public class ModelValidatorTests
{
    private static PipingModel Sample() =>
        IrJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "sample_ir.json")));

    [Fact]
    public void Sample_has_no_errors_only_missing_prov_warnings()
    {
        var findings = ModelValidator.Validate(Sample());
        Assert.Empty(findings.Where(f => f.IsError));
        // Ví dụ rút gọn ở mục 7.5 không ghi prov cho nút, bộ kiểm tra chỉ cảnh báo.
        Assert.All(findings, f => Assert.EndsWith(".prov", f.Path));
        Assert.True(ModelValidator.IsValid(Sample()));
    }

    [Fact]
    public void Duplicate_ids_are_errors()
    {
        var model = Sample();
        model.Nodes.Add(new Node { Id = "N-0032", Kind = NodeKind.OpenEnd });
        var f = ModelValidator.Validate(model).Single(x => x.IsError);
        Assert.Equal("nodes[3].id", f.Path);
        Assert.Contains("trùng", f.Message);
    }

    [Fact]
    public void Dangling_references_are_errors_with_paths()
    {
        var model = Sample();
        model.Segments[1].NodeB = "N-KHONG-CO";
        model.Components[0].SegmentId = "S-KHONG-CO";
        model.Issues[0].Elements.Add("X");
        var errors = ModelValidator.Validate(model).Where(x => x.IsError).Select(x => x.Path).ToList();
        Assert.Contains("segments[1].nodeB", errors);
        Assert.Contains("components[0].segmentId", errors);
        Assert.Contains("issues[0].elements[1]", errors);
    }

    [Fact]
    public void Nozzle_node_requires_existing_equipment()
    {
        var model = Sample();
        model.Nodes[0].EquipmentId = null;
        Assert.Contains(ModelValidator.Validate(model), f => f.IsError && f.Path == "nodes[0].equipmentId");
    }

    [Fact]
    public void Agent_decision_needs_rationale_and_confidence()
    {
        var model = Sample();
        model.Decisions[0].Rationale = null;
        model.Decisions[0].Confidence = null;
        var errors = ModelValidator.Validate(model).Where(x => x.IsError).Select(x => x.Path).ToList();
        Assert.Contains("decisions[0].rationale", errors);
        Assert.Contains("decisions[0].confidence", errors);
    }

    [Fact]
    public void Confidence_out_of_range_is_error()
    {
        var model = Sample();
        model.Runs[0].Prov!.Confidence = 1.5;
        Assert.Contains(ModelValidator.Validate(model), f => f.IsError && f.Path == "runs[0].prov.confidence");
    }

    [Fact]
    public void Wrong_units_is_error()
    {
        var model = Sample();
        model.Units = "inch";
        Assert.Contains(ModelValidator.Validate(model), f => f.IsError && f.Path == "units");
    }

    [Fact]
    public void Elbow_with_three_segments_is_warning_not_error()
    {
        var model = Sample();
        model.Nodes[1].Segments.Add("S-0013");
        var f = ModelValidator.Validate(model).Single(x => x.Path == "nodes[1].segments");
        Assert.False(f.IsError);
    }
}
