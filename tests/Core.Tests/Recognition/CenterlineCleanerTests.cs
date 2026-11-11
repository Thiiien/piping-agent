using PipingAgent.Core.Geometry;
using PipingAgent.Core.Recognition;

namespace Core.Tests.Recognition;

/// <summary>S2-06: khe hở nhỏ, đoạn trùng, polyline bị cắt vụn. S2-07: chuỗi thành đoạn chạy.</summary>
public class CenterlineCleanerTests
{
    private static Chain Line(string handle, double x1, double y1, double x2, double y2) =>
        new(new[] { new Point2(x1, y1), new Point2(x2, y2) }, new[] { handle }, isPolyline: false);

    private static Chain Poly(string handle, params (double X, double Y)[] pts) =>
        new(pts.Select(p => new Point2(p.X, p.Y)), new[] { handle }, isPolyline: true);

    private static readonly CenterlineCleaner Cleaner = new(snap: 1.0, collinearDeg: 0.5);

    [Fact]
    public void Small_gap_between_endpoints_is_closed_and_lines_are_joined()
    {
        var r = Cleaner.Clean(new[]
        {
            Line("A", 0, 0, 1000, 0),
            Line("B", 1000.6, 0.4, 1000.6, 800),
        });

        var chain = Assert.Single(r.Chains);
        Assert.Equal(3, chain.Points.Count);
        Assert.Equal(new Point2(1000, 0), chain.Points[1]);
        Assert.Equal(new[] { "A", "B" }, chain.Sources);
        Assert.Equal(1, r.Stats.SnappedVertices);
        Assert.Equal(1, r.Stats.JoinedChains);
    }

    [Fact]
    public void Gap_larger_than_snap_is_left_open()
    {
        var r = Cleaner.Clean(new[]
        {
            Line("A", 0, 0, 1000, 0),
            Line("B", 1003, 0, 1003, 800),
        });

        Assert.Equal(2, r.Chains.Count);
        Assert.Equal(0, r.Stats.JoinedChains);
    }

    [Fact]
    public void Duplicate_segment_is_removed_and_its_handle_kept_as_source()
    {
        var r = Cleaner.Clean(new[]
        {
            Line("A", 0, 0, 1000, 0),
            Line("B", 1000, 0, 0, 0),
            Poly("C", (0, 0), (1000, 0), (1000, 500)),
        });

        var chain = Assert.Single(r.Chains);
        Assert.True(chain.IsPolyline);
        Assert.Equal(2, r.Stats.RemovedDuplicates);
        Assert.Contains("A", chain.Sources);
        Assert.Contains("B", chain.Sources);
        Assert.Contains("C", chain.Sources);
    }

    [Fact]
    public void Fragmented_collinear_lines_become_one_straight_chain()
    {
        var r = Cleaner.Clean(new[]
        {
            Line("A", 0, 0, 300, 0),
            Line("B", 300, 0, 650, 0.2),
            Line("C", 650, 0.2, 1000, 0),
            Line("D", 1000, 0, 1000, 400),
        });

        var chain = Assert.Single(r.Chains);
        Assert.Equal(3, chain.Points.Count);
        Assert.Equal(new Point2(0, 0), chain.Points[0]);
        Assert.Equal(new Point2(1000, 0), chain.Points[1]);
        Assert.Equal(new Point2(1000, 400), chain.Points[2]);
        Assert.Equal(2, r.Stats.MergedCollinear);
        Assert.Equal(4, chain.Sources.Count);
    }

    [Fact]
    public void Zero_length_and_tiny_segments_are_dropped()
    {
        var r = Cleaner.Clean(new[]
        {
            Line("A", 0, 0, 0, 0),
            Line("B", 5, 5, 5.5, 5),
            Poly("C", (0, 0), (0.3, 0), (1000, 0)),
        });

        var chain = Assert.Single(r.Chains);
        Assert.Equal(2, chain.Points.Count);
        Assert.True(r.Stats.DroppedDegenerate >= 3);
    }

    [Fact]
    public void Polylines_are_never_joined_and_branch_points_split_chains()
    {
        var r = Cleaner.Clean(new[]
        {
            Poly("P1", (0, 0), (1000, 0)),
            Poly("P2", (1000, 0), (1000, 1000)),
            Line("L1", 2000, 0, 3000, 0),
            Line("L2", 3000, 0, 4000, 0.3),
            Line("L3", 3000, 0, 3000, 500),
        });

        Assert.Equal(5, r.Chains.Count);
        Assert.Equal(0, r.Stats.JoinedChains);
    }

    [Fact]
    public void Run_builder_numbers_runs_and_records_sources()
    {
        var builder = new RunBuilder(Cleaner);
        var result = builder.Build(new[]
        {
            Poly("P1", (5000, 0), (6000, 0)),
            Line("L1", 0, 0, 1000, 0),
            Line("L2", 1000, 0, 1000, 900),
        });

        Assert.Equal(2, result.Runs.Count);
        Assert.Equal("run-001", result.Runs[0].Id);
        Assert.Equal(new[] { "L1", "L2" }, result.Runs[0].Prov!.Sources);
        Assert.Equal(RecognitionRules.RunFromChain, result.Runs[0].Prov!.Rule);
        Assert.Equal("run-002", result.Runs[1].Id);
        Assert.Equal(3, result.Runs[0].PlanPoints.Count);
    }
}
