using PipingAgent.Core.Geometry;

namespace Core.Tests.Geometry;

/// <summary>Kiểm thử cho việc S1-04: từng phép hình học, gồm các ca biên.</summary>
public class GeometryOpsTests
{
    private static readonly Tolerance Tol = Tolerance.Default;

    private static Segment2 Seg(double ax, double ay, double bx, double by) =>
        new(new Point2(ax, ay), new Point2(bx, by));

    [Fact]
    public void Tolerance_rejects_non_positive_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tolerance(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tolerance(1, -1));
    }

    [Theory]
    [InlineData(0, 0, 3, 4, 5)]
    [InlineData(1, 1, 1, 1, 0)]
    [InlineData(-2, 0, 2, 0, 4)]
    public void Point_distance(double ax, double ay, double bx, double by, double expected)
    {
        Assert.Equal(expected, new Point2(ax, ay).DistanceTo(new Point2(bx, by)), 9);
    }

    [Fact]
    public void Points_within_tolerance_are_same_but_not_equal()
    {
        var a = new Point2(100, 200);
        var b = new Point2(100.005, 200);
        Assert.True(a.IsSame(b, Tol));
        Assert.NotEqual(a, b);
        Assert.False(a.IsSame(new Point2(100.02, 200), Tol));
    }

    [Fact]
    public void Point3_distance_and_plan_projection()
    {
        var p = new Point3(1, 2, 3);
        Assert.Equal(new Point2(1, 2), p.Plan);
        Assert.Equal(Math.Sqrt(14), p.DistanceTo(Point3.Origin), 9);
        Assert.Equal(new Point3(1, 2, 9), p.WithZ(9));
    }

    [Fact]
    public void Vector_angles_parallel_and_perpendicular()
    {
        var v = new Vector2(10, 0);
        Assert.True(v.IsParallelTo(new Vector2(-3, 0), Tol));
        Assert.True(v.IsPerpendicularTo(new Vector2(0, 7), Tol));
        Assert.False(v.IsParallelTo(new Vector2(1, 1), Tol));
        Assert.Equal(Math.PI / 2, new Vector2(0, 5).AngleRad, 9);
        Assert.Equal(3 * Math.PI / 2, new Vector2(0, -5).AngleRad, 9);
        Assert.False(Vector2.Zero.IsParallelTo(v, Tol));
    }

    [Fact]
    public void Vector3_vertical_horizontal_and_cross()
    {
        Assert.True(new Vector3(0, 0, -4).IsVertical(Tol));
        Assert.True(new Vector3(3, 4, 0).IsHorizontal(Tol));
        Assert.False(new Vector3(3, 4, 1).IsHorizontal(Tol));
        Assert.Equal(Vector3.UnitZ, Vector3.UnitX.Cross(Vector3.UnitY));
        Assert.Equal(Math.PI / 2, Vector3.UnitX.AngleTo(Vector3.UnitY), 9);
        Assert.Throws<InvalidOperationException>(() => Vector3.Zero.Normalized());
    }

    [Fact]
    public void Box2_from_points_contains_and_distance()
    {
        var box = Box2.FromPoints(new[] { new Point2(5, 5), new Point2(0, 10), new Point2(10, 0) });
        Assert.Equal(new Point2(0, 0), box.Min);
        Assert.Equal(new Point2(10, 10), box.Max);
        Assert.True(box.Contains(new Point2(10, 10)));
        Assert.False(box.Contains(new Point2(10.001, 10)));
        Assert.True(box.Contains(new Point2(10.001, 10), Tol));
        Assert.Equal(5, box.DistanceTo(new Point2(15, 5)), 9);
        Assert.Equal(0, box.DistanceTo(new Point2(5, 5)), 9);
        Assert.True(box.Intersects(Box2.FromPoints(new Point2(10, 10), new Point2(20, 20))));
        Assert.False(box.Intersects(Box2.FromPoints(new Point2(10.1, 10.1), new Point2(20, 20))));
        Assert.Throws<ArgumentException>(() => new Box2(new Point2(1, 0), new Point2(0, 0)));
        Assert.Throws<ArgumentException>(() => Box2.FromPoints(Array.Empty<Point2>()));
    }

    [Theory]
    [InlineData(5, 3, 3)]      // hình chiếu trong đoạn
    [InlineData(-4, 3, 5)]     // hình chiếu trước A, đo tới A
    [InlineData(14, 3, 5)]     // hình chiếu sau B, đo tới B
    [InlineData(10, 0, 0)]     // nằm đúng đầu mút
    public void Distance_point_to_segment(double px, double py, double expected)
    {
        var seg = Seg(0, 0, 10, 0);
        Assert.Equal(expected, seg.DistanceTo(new Point2(px, py)), 9);
        Assert.Equal(expected, GeometryOps.DistancePointToSegment(new Point2(px, py), seg.A, seg.B), 9);
    }

    [Fact]
    public void Distance_point_to_line_extends_beyond_segment()
    {
        Assert.Equal(3, GeometryOps.DistancePointToLine(new Point2(50, 3), new Point2(0, 0), new Point2(10, 0)), 9);
        Assert.Equal(5, GeometryOps.DistancePointToLine(new Point2(3, 4), new Point2(0, 0), new Point2(0, 0)), 9);
    }

    [Fact]
    public void Segment_contains_distinguishes_interior_and_endpoints()
    {
        var seg = Seg(0, 0, 100, 0);
        Assert.True(seg.Contains(new Point2(50, 0.005), Tol));
        Assert.True(seg.ContainsInterior(new Point2(50, 0), Tol));
        Assert.False(seg.ContainsInterior(new Point2(100, 0), Tol));
        Assert.True(seg.TouchesEndpoint(new Point2(100.004, 0), Tol));
        Assert.False(seg.Contains(new Point2(50, 0.02), Tol));
    }

    [Fact]
    public void Collinearity_of_three_points()
    {
        Assert.True(GeometryOps.AreCollinear(new Point2(0, 0), new Point2(5, 5), new Point2(10, 10), Tol));
        Assert.False(GeometryOps.AreCollinear(new Point2(0, 0), new Point2(5, 5.1), new Point2(10, 10), Tol));
        // Hai điểm trùng nhau luôn thẳng hàng với điểm thứ ba.
        Assert.True(GeometryOps.AreCollinear(new Point2(0, 0), new Point2(0, 0), new Point2(7, -3), Tol));
        // Điểm giữa lệch nhỏ nhưng hai đầu rất xa: lấy cặp xa nhất làm trục.
        Assert.True(GeometryOps.AreCollinear(new Point2(0, 0), new Point2(50000, 0.005), new Point2(100000, 0), Tol));
    }

    [Fact]
    public void Segments_collinear_check()
    {
        Assert.True(Seg(0, 0, 10, 0).IsCollinearWith(Seg(20, 0, 30, 0.005), Tol));
        Assert.False(Seg(0, 0, 10, 0).IsCollinearWith(Seg(20, 1, 30, 1), Tol));
        Assert.False(Seg(0, 0, 10, 0).IsCollinearWith(Seg(5, 5, 5, 5), Tol));
    }

    [Fact]
    public void Intersect_crossing_in_interior()
    {
        var r = GeometryOps.Intersect(Seg(0, 0, 10, 10), Seg(0, 10, 10, 0), Tol);
        Assert.Equal(SegmentRelation.Crossing, r.Relation);
        Assert.True(r.HasPoint);
        Assert.True(r.Point!.Value.IsSame(new Point2(5, 5), Tol));
        Assert.Equal(0.5, r.TFirst, 9);
        Assert.Equal(0.5, r.TSecond, 9);
    }

    [Fact]
    public void Intersect_t_junction_is_touching()
    {
        // Nhánh chạm vào giữa ống chính: hình chữ T.
        var r = GeometryOps.Intersect(Seg(0, 0, 100, 0), Seg(40, 0, 40, 50), Tol);
        Assert.Equal(SegmentRelation.Touching, r.Relation);
        Assert.True(r.Point!.Value.IsSame(new Point2(40, 0), Tol));
        Assert.Equal(0.4, r.TFirst, 9);
        Assert.Equal(0, r.TSecond, 9);
    }

    [Fact]
    public void Intersect_l_corner_within_tolerance_is_touching()
    {
        // Hai nét không chạm hẳn nhau, hở 0,005 mm: vẫn coi là góc chữ L.
        var r = GeometryOps.Intersect(Seg(0, 0, 100, 0), Seg(100.005, 0, 100.005, 60), Tol);
        Assert.Equal(SegmentRelation.Touching, r.Relation);
        Assert.True(r.Point!.Value.IsSame(new Point2(100, 0), Tol));
    }

    [Fact]
    public void Intersect_near_miss_beyond_tolerance_is_disjoint()
    {
        var r = GeometryOps.Intersect(Seg(0, 0, 100, 0), Seg(100.05, 0, 100.05, 60), Tol);
        Assert.Equal(SegmentRelation.Disjoint, r.Relation);
        Assert.False(r.HasPoint);
    }

    [Fact]
    public void Intersect_lines_would_cross_but_segments_do_not()
    {
        var r = GeometryOps.Intersect(Seg(0, 0, 10, 0), Seg(20, -5, 20, 5), Tol);
        Assert.Equal(SegmentRelation.Disjoint, r.Relation);
    }

    [Fact]
    public void Intersect_parallel_offset()
    {
        var r = GeometryOps.Intersect(Seg(0, 0, 10, 0), Seg(0, 1, 10, 1), Tol);
        Assert.Equal(SegmentRelation.Parallel, r.Relation);
        Assert.False(r.HasPoint);
    }

    [Fact]
    public void Intersect_collinear_overlapping()
    {
        var r = GeometryOps.Intersect(Seg(0, 0, 10, 0), Seg(5, 0, 15, 0), Tol);
        Assert.Equal(SegmentRelation.Overlapping, r.Relation);
        // Đoạn nằm trọn trong đoạn kia cũng là chồng lấn.
        Assert.Equal(SegmentRelation.Overlapping, GeometryOps.Intersect(Seg(0, 0, 10, 0), Seg(2, 0, 3, 0), Tol).Relation);
        Assert.Equal(SegmentRelation.Overlapping, GeometryOps.Intersect(Seg(0, 0, 10, 0), Seg(10, 0, 0, 0), Tol).Relation);
    }

    [Fact]
    public void Intersect_collinear_touching_at_end()
    {
        var r = GeometryOps.Intersect(Seg(0, 0, 10, 0), Seg(10, 0, 20, 0), Tol);
        Assert.Equal(SegmentRelation.CollinearTouching, r.Relation);
        Assert.True(r.Point!.Value.IsSame(new Point2(10, 0), Tol));
        Assert.Equal(1, r.TFirst, 9);
        Assert.Equal(0, r.TSecond, 9);

        var reversed = GeometryOps.Intersect(Seg(0, 0, 10, 0), Seg(-5, 0, 0, 0), Tol);
        Assert.Equal(SegmentRelation.CollinearTouching, reversed.Relation);
        Assert.Equal(0, reversed.TFirst, 9);
        Assert.Equal(1, reversed.TSecond, 9);
    }

    [Fact]
    public void Intersect_collinear_disjoint()
    {
        var r = GeometryOps.Intersect(Seg(0, 0, 10, 0), Seg(11, 0, 20, 0), Tol);
        Assert.Equal(SegmentRelation.CollinearDisjoint, r.Relation);
    }

    [Fact]
    public void Intersect_with_degenerate_segment()
    {
        var onSegment = GeometryOps.Intersect(Seg(0, 0, 10, 0), Seg(4, 0, 4, 0), Tol);
        Assert.Equal(SegmentRelation.Touching, onSegment.Relation);
        Assert.Equal(0.4, onSegment.TFirst, 9);

        var offSegment = GeometryOps.Intersect(Seg(4, 1, 4, 1), Seg(0, 0, 10, 0), Tol);
        Assert.Equal(SegmentRelation.Disjoint, offSegment.Relation);
    }

    [Fact]
    public void Intersect_is_symmetric_in_relation()
    {
        var a = Seg(0, 0, 100, 0);
        var b = Seg(40, -10, 40, 10);
        var ab = GeometryOps.Intersect(a, b, Tol);
        var ba = GeometryOps.Intersect(b, a, Tol);
        Assert.Equal(ab.Relation, ba.Relation);
        Assert.Equal(ab.TFirst, ba.TSecond, 9);
        Assert.Equal(ab.TSecond, ba.TFirst, 9);
    }
}
