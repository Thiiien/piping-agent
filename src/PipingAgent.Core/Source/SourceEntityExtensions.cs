using System;
using System.Collections.Generic;
using System.Linq;
using PipingAgent.Core.Geometry;

namespace PipingAgent.Core.Source
{
    public static class SourceEntityExtensions
    {
        /// <summary>Đưa mọi góc về [0, 360). Hai phần mềm trả góc của đối tượng trong block theo khoảng khác nhau.</summary>
        public static void NormalizeAngles(this SourceEntity e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (e.RotationDeg.HasValue) e.RotationDeg = Wrap(e.RotationDeg.Value);
            if (e.StartAngleDeg.HasValue) e.StartAngleDeg = Wrap(e.StartAngleDeg.Value);
            if (e.EndAngleDeg.HasValue) e.EndAngleDeg = Wrap(e.EndAngleDeg.Value);
        }

        private static double Wrap(double degrees)
        {
            var d = degrees % 360.0;
            if (d < 0) d += 360.0;
            if (Math.Abs(d - 360.0) < 1e-9) d = 0;
            return d;
        }

        /// <summary>
        /// Hộp bao trên mặt bằng từ các trường hình học của đối tượng. Cả hai bộ đọc dùng chung hàm này
        /// để lọc theo vùng cho kết quả giống nhau. Chữ ước lượng rộng 0,8 lần chiều cao mỗi ký tự.
        /// </summary>
        public static Box2? ComputeBounds(this SourceEntity e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var pts = new List<Point2>();
            if (e.Points != null) pts.AddRange(e.Points.Select(p => p.Plan));
            if (e.Center.HasValue)
            {
                var c = e.Center.Value.Plan;
                if (e.Radius.HasValue)
                {
                    var r = e.Radius.Value;
                    pts.Add(new Point2(c.X - r, c.Y - r));
                    pts.Add(new Point2(c.X + r, c.Y + r));
                }
                else if (e.MajorAxis.HasValue)
                {
                    var a = e.MajorAxis.Value.Plan.Length;
                    pts.Add(new Point2(c.X - a, c.Y - a));
                    pts.Add(new Point2(c.X + a, c.Y + a));
                }
                else
                {
                    pts.Add(c);
                    if (e.TextHeight.HasValue && !string.IsNullOrEmpty(e.Text))
                    {
                        var w = e.TextHeight.Value * 0.8 * Math.Max(1, e.Text!.Length);
                        var h = e.TextHeight.Value;
                        var angle = (e.RotationDeg ?? 0) * Math.PI / 180.0;
                        var dir = new Vector2(Math.Cos(angle), Math.Sin(angle));
                        var up = dir.Perpendicular();
                        pts.Add(c + dir * w);
                        pts.Add(c + up * h);
                        pts.Add(c + dir * w + up * h);
                    }
                }
            }

            return pts.Count == 0 ? (Box2?)null : Box2.FromPoints(pts);
        }

        /// <summary>Áp bộ lọc lên một danh sách đối tượng đã chuyển đổi. Dùng chung cho mọi bộ đọc.</summary>
        public static List<SourceEntity> ApplyFilter(this IEnumerable<SourceEntity> source, EntityFilter filter)
        {
            filter ??= EntityFilter.All;
            var layers = filter.Layers == null ? null : new HashSet<string>(filter.Layers, StringComparer.OrdinalIgnoreCase);
            var excluded = filter.ExcludeLayers == null ? null : new HashSet<string>(filter.ExcludeLayers, StringComparer.OrdinalIgnoreCase);
            var kinds = filter.Kinds == null ? null : new HashSet<SourceKind>(filter.Kinds);

            var result = new List<SourceEntity>();
            foreach (var e in source)
            {
                if (kinds != null && !kinds.Contains(e.Kind)) continue;
                if (layers != null && !layers.Contains(e.Layer)) continue;
                if (excluded != null && excluded.Contains(e.Layer)) continue;
                if (filter.Region.HasValue && (!e.Bounds.HasValue || !e.Bounds.Value.Intersects(filter.Region.Value))) continue;
                result.Add(e);
            }

            return result;
        }

        /// <summary>Lập bản tóm tắt từ danh sách đối tượng. Bộ đọc bổ sung thêm phần nó biết: layer đủ, block đủ, đơn vị, tờ vẽ.</summary>
        public static DrawingSummary Summarize(this IReadOnlyList<SourceEntity> entities)
        {
            var summary = new DrawingSummary
            {
                EntityCount = entities.Count,
                Fingerprint = DrawingFingerprint.Compute(entities),
            };
            foreach (var e in entities)
            {
                summary.CountsByKind[e.Kind.ToString()] = summary.CountsByKind.TryGetValue(e.Kind.ToString(), out var n) ? n + 1 : 1;
            }

            foreach (var group in entities.GroupBy(e => e.Layer, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var info = new LayerInfo { Name = group.Key, EntityCount = group.Count() };
                foreach (var k in group.GroupBy(e => e.Kind)) info.CountsByKind[k.Key.ToString()] = k.Count();
                summary.Layers.Add(info);
            }

            foreach (var group in entities.Where(e => e.Kind == SourceKind.Insert && e.Name != null && e.BlockPath.Count == 0)
                         .GroupBy(e => e.Name!, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()))
            {
                summary.Blocks.Add(new BlockUsage
                {
                    Name = group.Key,
                    InsertCount = group.Count(),
                    HasAttributes = group.Any(e => e.Attributes != null && e.Attributes.Count > 0),
                });
            }

            var boxes = entities.Where(e => e.Bounds.HasValue).Select(e => e.Bounds!.Value).ToList();
            if (boxes.Count > 0)
            {
                var box = boxes[0];
                foreach (var b in boxes.Skip(1)) box = box.Union(b);
                summary.Extents = box;
            }

            return summary;
        }
    }
}
