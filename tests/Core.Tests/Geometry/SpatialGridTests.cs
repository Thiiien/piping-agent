using System.Diagnostics;
using PipingAgent.Core.Geometry;

namespace Core.Tests.Geometry;

/// <summary>
/// Kiểm thử cho việc S1-05: chỉ mục không gian trả đúng tập như duyệt toàn bộ trên 5.000 đoạn, và nhanh hơn rõ rệt.
/// </summary>
public class SpatialGridTests
{
    private static List<Segment2> MakeSegments(int count, int seed)
    {
        // Mô phỏng một tờ mặt bằng cỡ 180 m x 80 m, đoạn ống dài 50 đến 5.000 mm, phần lớn trực giao.
        var random = new Random(seed);
        var segments = new List<Segment2>(count);
        for (var i = 0; i < count; i++)
        {
            var a = new Point2(random.NextDouble() * 180_000, random.NextDouble() * 80_000);
            var length = 50 + random.NextDouble() * 4_950;
            var direction = random.Next(10) switch
            {
                < 5 => new Vector2(length, 0),
                < 9 => new Vector2(0, length),
                _ => new Vector2(length * 0.7071, length * 0.7071),
            };
            segments.Add(new Segment2(a, a + direction));
        }

        return segments;
    }

    private static SpatialGrid<Segment2> Build(List<Segment2> segments)
    {
        var extent = Box2.FromPoints(segments.SelectMany(s => new[] { s.A, s.B }));
        var grid = new SpatialGrid<Segment2>(SpatialGrid<Segment2>.SuggestCellSize(extent, segments.Count));
        foreach (var s in segments) grid.Insert(s, s.Bounds);
        return grid;
    }

    [Fact]
    public void Rejects_invalid_cell_size()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid<int>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid<int>(double.NaN));
    }

    [Fact]
    public void Region_query_matches_brute_force_on_5000_segments()
    {
        var segments = MakeSegments(5_000, seed: 42);
        var grid = Build(segments);
        var random = new Random(7);

        for (var i = 0; i < 200; i++)
        {
            var center = new Point2(random.NextDouble() * 180_000, random.NextDouble() * 80_000);
            var region = Box2.Around(center, 200 + random.NextDouble() * 3_000);

            var expected = segments.Where(s => s.Bounds.Intersects(region)).ToHashSet();
            var actual = grid.Query(region).ToHashSet();

            Assert.Equal(expected.Count, actual.Count);
            Assert.True(expected.SetEquals(actual), $"Truy vấn {i} trả tập khác duyệt toàn bộ.");
        }
    }

    [Fact]
    public void Near_query_matches_brute_force()
    {
        var segments = MakeSegments(5_000, seed: 3);
        var grid = Build(segments);
        var random = new Random(11);

        for (var i = 0; i < 200; i++)
        {
            var p = new Point2(random.NextDouble() * 180_000, random.NextDouble() * 80_000);
            var radius = 100 + random.NextDouble() * 2_000;
            var expected = segments.Where(s => s.Bounds.DistanceTo(p) <= radius).ToHashSet();
            var actual = grid.QueryNear(p, radius).ToHashSet();
            Assert.True(expected.SetEquals(actual), $"Truy vấn gần {i} trả tập khác duyệt toàn bộ.");
        }
    }

    [Fact]
    public void Nearest_by_true_distance_matches_brute_force()
    {
        var segments = MakeSegments(5_000, seed: 5);
        var grid = Build(segments);
        var random = new Random(13);

        for (var i = 0; i < 200; i++)
        {
            var p = new Point2(random.NextDouble() * 180_000, random.NextDouble() * 80_000);
            var expected = segments.Min(s => s.DistanceTo(p));

            var found = grid.TryFindNearest(p, s => s.DistanceTo(p), maxRadius: 50_000, out var nearest, out var distance);
            Assert.True(found);
            Assert.Equal(expected, distance, 6);
            Assert.Equal(expected, nearest.DistanceTo(p), 6);
        }
    }

    [Fact]
    public void Nearest_respects_max_radius_and_empty_grid()
    {
        var grid = new SpatialGrid<Segment2>(1000);
        Assert.False(grid.TryFindNearest(Point2.Origin, s => 0, 100, out _, out _));

        grid.Insert(new Segment2(new Point2(5_000, 0), new Point2(6_000, 0)), Box2.FromPoints(new Point2(5_000, 0), new Point2(6_000, 0)));
        Assert.False(grid.TryFindNearest(Point2.Origin, s => s.DistanceTo(Point2.Origin), 1_000, out _, out _));
        Assert.True(grid.TryFindNearest(Point2.Origin, s => s.DistanceTo(Point2.Origin), 10_000, out _, out var d));
        Assert.Equal(5_000, d, 9);
    }

    [Fact]
    public void Item_spanning_many_cells_is_returned_once()
    {
        var grid = new SpatialGrid<string>(100);
        grid.Insert("dài", Box2.FromPoints(new Point2(0, 0), new Point2(10_000, 0)));
        var result = grid.Query(Box2.FromPoints(new Point2(-50, -50), new Point2(20_000, 50)));
        Assert.Single(result);
    }

    [Fact]
    public void Grid_is_clearly_faster_than_brute_force()
    {
        var segments = MakeSegments(5_000, seed: 99);
        var grid = Build(segments);
        var random = new Random(1);
        var queries = Enumerable.Range(0, 2_000)
            .Select(_ => Box2.Around(new Point2(random.NextDouble() * 180_000, random.NextDouble() * 80_000), 1_000))
            .ToArray();

        // Khởi động để loại ảnh hưởng của JIT.
        foreach (var q in queries.Take(50)) { _ = grid.Query(q); _ = segments.Count(s => s.Bounds.Intersects(q)); }

        var brute = Stopwatch.StartNew();
        long bruteHits = 0;
        foreach (var q in queries) bruteHits += segments.Count(s => s.Bounds.Intersects(q));
        brute.Stop();

        var indexed = Stopwatch.StartNew();
        long gridHits = 0;
        foreach (var q in queries) gridHits += grid.Query(q).Count;
        indexed.Stop();

        Assert.Equal(bruteHits, gridHits);
        // Yêu cầu nhanh hơn ít nhất 5 lần. Trên máy phát triển mức chênh thực tế lớn hơn nhiều.
        Assert.True(indexed.Elapsed.TotalMilliseconds * 5 < brute.Elapsed.TotalMilliseconds,
            $"Lưới {indexed.ElapsedMilliseconds} ms so với duyệt toàn bộ {brute.ElapsedMilliseconds} ms.");
    }
}
