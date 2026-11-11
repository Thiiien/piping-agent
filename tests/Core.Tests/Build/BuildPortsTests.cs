using PipingAgent.Core.Build;
using PipingAgent.Core.Geometry;

namespace Core.Tests.Build;

/// <summary>S4-12: cổng nối và mối nối giữa các khối, thay cho khái niệm nối của S3D mà AutoCAD thuần không có.</summary>
public class BuildPortsTests
{
    private static BuildPlan Chain()
    {
        // Ống 0..1000 theo X, bích SO lồng lên đầu ống mặt tại x=1000 (L=50, Axis +X, Position 975),
        // van wafer dài 100 đặt 1000..1100, bích SO thứ hai mặt tại 1100 quay ngược, ống tiếp 1100..2000, olet tại x=500 nhánh +Z.
        var plan = new BuildPlan();
        plan.Pipes.Add(new PipeItem { IrId = "p1", Dn = 250, Od = 273, Start = new Point3(0, 0, 0), End = new Point3(1000, 0, 0) });
        plan.Components.Add(new ComponentItem { IrId = "f1", Type = "flange", Subtype = "so", Dn = 250, Od = 273, Length = 50, Axis = Vector3.UnitX, Position = new Point3(975, 0, 0), OverlapsPipe = true });
        plan.Components.Add(new ComponentItem { IrId = "v1", Type = "valve_butterfly", Subtype = "wafer", Dn = 250, Od = 273, Length = 100, Axis = Vector3.UnitX, Position = new Point3(1050, 0, 0) });
        plan.Components.Add(new ComponentItem { IrId = "f2", Type = "flange", Subtype = "so", Dn = 250, Od = 273, Length = 50, Axis = -Vector3.UnitX, Position = new Point3(1125, 0, 0), OverlapsPipe = true });
        plan.Pipes.Add(new PipeItem { IrId = "p2", Dn = 250, Od = 273, Start = new Point3(1100, 0, 0), End = new Point3(2000, 0, 0) });
        plan.Components.Add(new ComponentItem { IrId = "o1", Type = "olet", Dn = 25, Od = 34, Length = 25, Axis = Vector3.UnitX, Position = new Point3(500, 0, 0), BranchDirection = Vector3.UnitZ, OverlapsPipe = true });
        plan.Pipes.Add(new PipeItem { IrId = "b1", Dn = 25, Od = 34, Start = new Point3(500, 0, 0), End = new Point3(500, 0, 300) });
        return plan;
    }

    [Fact]
    public void Flange_valve_flange_chain_connects_every_face()
    {
        var plan = Chain();
        BuildPorts.Compute(plan);
        var connections = BuildPorts.Connect(plan);

        string? Mate(string id, string port) => plan.Items.First(i => i.IrId == id).Ports.First(p => p.Name == port).MateIrId;
        Assert.Equal("f1", Mate("p1", "B"));
        Assert.Equal("v1", Mate("f1", "FACE"));
        Assert.Equal("f2", Mate("v1", "B"));
        Assert.Equal("p2", Mate("f2", "PIPE"));
        Assert.Equal("b1", Mate("o1", "BRANCH"));
        Assert.Equal("p1", Mate("o1", "HOST"));
        // Hai đầu tuyến và đầu nhánh hở thật.
        Assert.Equal(3, BuildPorts.OpenPorts(plan));
        Assert.Null(Mate("p1", "A"));
        Assert.Null(Mate("p2", "B"));
        Assert.Null(Mate("b1", "B"));
        Assert.Equal(6, connections.Count);
    }

    [Fact]
    public void Gap_beyond_tolerance_stays_open()
    {
        var plan = new BuildPlan();
        plan.Pipes.Add(new PipeItem { IrId = "p1", Dn = 100, Od = 114, Start = new Point3(0, 0, 0), End = new Point3(1000, 0, 0) });
        plan.Pipes.Add(new PipeItem { IrId = "p2", Dn = 100, Od = 114, Start = new Point3(1010, 0, 0), End = new Point3(2000, 0, 0) });
        BuildPorts.Compute(plan);
        Assert.Empty(BuildPorts.Connect(plan));
        Assert.Single(BuildPorts.Connect(plan, tolerance: 12));
    }

    [Fact]
    public void Same_direction_ports_do_not_mate()
    {
        // Hai ống nối đuôi nhau cùng chiều: cổng B của p1 (+X) và cổng A của p2 (−X) khớp; cổng A của p1 và A của p2 không bao giờ khớp.
        var plan = new BuildPlan();
        plan.Pipes.Add(new PipeItem { IrId = "p1", Dn = 100, Od = 114, Start = new Point3(1000, 0, 0), End = new Point3(0, 0, 0) });
        plan.Pipes.Add(new PipeItem { IrId = "p2", Dn = 100, Od = 114, Start = new Point3(1000, 0, 0), End = new Point3(2000, 0, 0) });
        BuildPorts.Compute(plan);
        var c = Assert.Single(BuildPorts.Connect(plan));
        Assert.Equal("A", c.APort);
        Assert.Equal("A", c.BPort);
    }

    [Fact]
    public void Describe_lists_ports_with_mates()
    {
        var plan = Chain();
        BuildPorts.Compute(plan);
        BuildPorts.Connect(plan);
        var s = BuildPorts.Describe(plan.Items.First(i => i.IrId == "f1"));
        Assert.Contains("FACE@1000,0,0>v1:A", s);
        Assert.Contains("PIPE@1000,0,0>p1:B", s);
    }
}
