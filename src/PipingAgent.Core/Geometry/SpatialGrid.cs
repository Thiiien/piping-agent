using System;
using System.Collections.Generic;

namespace PipingAgent.Core.Geometry
{
    /// <summary>
    /// Chỉ mục không gian dạng lưới đều trên mặt bằng, cho NFR-PRF-01.
    /// Mỗi phần tử được đăng ký vào mọi ô mà hộp bao của nó chạm tới.
    /// Truy vấn trả về đúng tập phần tử như duyệt toàn bộ, chỉ nhanh hơn.
    /// Không giữ trạng thái tĩnh, mỗi bản vẽ một lưới. Quy tắc 4 mục 6.12.3.
    /// </summary>
    public sealed class SpatialGrid<T>
    {
        private readonly Dictionary<long, List<Entry>> _cells = new Dictionary<long, List<Entry>>();
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly double _cellSize;
        private int _queryStamp;

        /// <param name="cellSize">Cạnh ô lưới, mm. Nên cỡ chiều dài trung bình của đoạn ống.</param>
        public SpatialGrid(double cellSize)
        {
            if (cellSize <= 0 || double.IsNaN(cellSize) || double.IsInfinity(cellSize))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cạnh ô lưới phải là số dương hữu hạn.");
            }

            _cellSize = cellSize;
        }

        public int Count => _entries.Count;

        public double CellSize => _cellSize;

        /// <summary>Chọn cạnh ô từ hộp bao tổng và số phần tử, nhắm khoảng 4 phần tử mỗi ô.</summary>
        public static double SuggestCellSize(Box2 extent, int itemCount)
        {
            if (itemCount <= 0) return Math.Max(extent.Width, extent.Height) + 1;
            var area = Math.Max(extent.Width, 1) * Math.Max(extent.Height, 1);
            var size = Math.Sqrt(area * 4.0 / itemCount);
            return Math.Max(size, 1);
        }

        public void Insert(T item, Box2 bounds)
        {
            var entry = new Entry(item, bounds);
            _entries.Add(entry);
            var (ix0, iy0) = CellOf(bounds.Min);
            var (ix1, iy1) = CellOf(bounds.Max);
            for (var ix = ix0; ix <= ix1; ix++)
            {
                for (var iy = iy0; iy <= iy1; iy++)
                {
                    var key = Key(ix, iy);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        list = new List<Entry>();
                        _cells[key] = list;
                    }

                    list.Add(entry);
                }
            }
        }

        public void Insert(T item, Point2 point) => Insert(item, new Box2(point, point));

        /// <summary>Phần tử có hộp bao giao với vùng cho trước.</summary>
        public List<T> Query(Box2 region)
        {
            var result = new List<T>();
            var stamp = ++_queryStamp;
            var (ix0, iy0) = CellOf(region.Min);
            var (ix1, iy1) = CellOf(region.Max);
            for (var ix = ix0; ix <= ix1; ix++)
            {
                for (var iy = iy0; iy <= iy1; iy++)
                {
                    if (!_cells.TryGetValue(Key(ix, iy), out var list)) continue;
                    foreach (var entry in list)
                    {
                        if (entry.Stamp == stamp) continue;
                        entry.Stamp = stamp;
                        if (entry.Bounds.Intersects(region))
                        {
                            result.Add(entry.Item);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>Phần tử có hộp bao cách điểm không quá bán kính.</summary>
        public List<T> QueryNear(Point2 point, double radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            var candidates = Query(Box2.Around(point, radius));
            var result = new List<T>(candidates.Count);
            var stamp = _queryStamp;
            foreach (var entry in _entries)
            {
                if (entry.Stamp != stamp) continue;
                if (entry.Bounds.DistanceTo(point) <= radius) result.Add(entry.Item);
            }

            return result;
        }

        /// <summary>
        /// Phần tử gần điểm nhất theo khoảng cách thật do hàm đo cung cấp. Tìm theo vòng tròn nở dần.
        /// Trả về false nếu lưới rỗng.
        /// </summary>
        public bool TryFindNearest(Point2 point, Func<T, double> distance, double maxRadius, out T nearest, out double nearestDistance)
        {
            nearest = default!;
            nearestDistance = double.PositiveInfinity;
            if (_entries.Count == 0) return false;

            var radius = _cellSize;
            while (true)
            {
                foreach (var item in QueryNear(point, radius))
                {
                    var d = distance(item);
                    if (d < nearestDistance)
                    {
                        nearestDistance = d;
                        nearest = item;
                    }
                }

                // Mọi phần tử gần hơn nearestDistance đều có hộp bao trong bán kính đã quét.
                if (nearestDistance <= radius) return nearestDistance <= maxRadius;
                if (radius >= maxRadius) return nearestDistance <= maxRadius;
                radius = Math.Min(radius * 2, maxRadius);
            }
        }

        private (long, long) CellOf(Point2 p) => ((long)Math.Floor(p.X / _cellSize), (long)Math.Floor(p.Y / _cellSize));

        private static long Key(long ix, long iy) => unchecked((ix << 32) ^ (iy & 0xFFFFFFFFL));

        private sealed class Entry
        {
            public Entry(T item, Box2 bounds)
            {
                Item = item;
                Bounds = bounds;
            }

            public T Item { get; }
            public Box2 Bounds { get; }
            public int Stamp { get; set; }
        }
    }
}
