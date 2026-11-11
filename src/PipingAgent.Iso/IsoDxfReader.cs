using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Source;
using PipingAgent.Dxf;

namespace PipingAgent.Iso
{
    /// <summary>
    /// Đọc isometric DWG hoặc DXF thành đối tượng nguồn cùng đơn vị với bộ đọc PDF, I3. Bản vẽ ISOGEN có tờ giấy theo mm,
    /// nên toạ độ được nhân 72/25,4 để ra điểm, nhờ đó mọi ngưỡng của <see cref="IsoGraph"/> dùng chung cho cả hai nguồn.
    /// Độ dày nét lấy từ lineweight, cũng đổi sang điểm. Layer giữ nguyên để hồ sơ quy ước chọn nét ống theo tên layer.
    /// </summary>
    public static class IsoDxfReader
    {
        public const double PointsPerMm = 72.0 / 25.4;

        public static IsoPdfReader.Page Read(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Không thấy tệp bản vẽ.", path);
            var reader = DxfDrawingReader.Open(path, 1.0);
            var raw = reader.ReadEntities(new EntityFilter { ExplodeBlocks = true });
            var page = new IsoPdfReader.Page { Number = 1 };
            var k = PointsPerMm;
            double maxX = 0, maxY = 0;
            foreach (var e in raw)
            {
                SourceEntity? copy = null;
                if (e.Kind == SourceKind.Line && e.Points != null && e.Points.Count == 2)
                {
                    copy = Clone(e, SourceKind.Line);
                    copy.Points = e.Points.Select(p => new Point3(p.X * k, p.Y * k, 0)).ToList();
                    copy.LineWidth = (e.LineWidth ?? 0.25) * k;
                }
                else if (e.Kind == SourceKind.Polyline && e.Points != null && e.Points.Count >= 2)
                {
                    // Đa tuyến thành từng nét thẳng, giữ cùng handle, để ô số thứ tự và đường kích thước vẽ bằng đa tuyến vẫn đọc được.
                    var pts = e.Points.Select(p => new Point2(p.X * k, p.Y * k)).ToList();
                    if (e.Closed == true) pts.Add(pts[0]);
                    for (var i = 0; i + 1 < pts.Count; i++)
                    {
                        var seg = Clone(e, SourceKind.Line);
                        seg.Points = new List<Point3> { new Point3(pts[i].X, pts[i].Y, 0), new Point3(pts[i + 1].X, pts[i + 1].Y, 0) };
                        seg.LineWidth = (e.LineWidth ?? 0.25) * k;
                        page.Entities.Add(seg);
                        maxX = Math.Max(maxX, Math.Max(pts[i].X, pts[i + 1].X));
                        maxY = Math.Max(maxY, Math.Max(pts[i].Y, pts[i + 1].Y));
                    }

                    continue;
                }
                else if ((e.Kind == SourceKind.Text || e.Kind == SourceKind.MText) && e.Center.HasValue && !string.IsNullOrWhiteSpace(e.Text))
                {
                    copy = Clone(e, SourceKind.Text);
                    copy.Text = e.Text!.Trim();
                    copy.Center = new Point3(e.Center.Value.X * k, e.Center.Value.Y * k, 0);
                    copy.TextHeight = (e.TextHeight ?? 2.5) * k;
                    copy.RotationDeg = e.RotationDeg ?? 0;
                    copy.Bounds = TextBounds(copy.Center.Value.Plan, copy.Text.Length, copy.TextHeight.Value, copy.RotationDeg.Value);
                }
                else if (e.Kind == SourceKind.Arc && e.Center.HasValue && e.Radius.HasValue)
                {
                    copy = Clone(e, SourceKind.Arc);
                    copy.Center = new Point3(e.Center.Value.X * k, e.Center.Value.Y * k, 0);
                    copy.Radius = e.Radius.Value * k;
                    copy.StartAngleDeg = e.StartAngleDeg;
                    copy.EndAngleDeg = e.EndAngleDeg;
                    copy.LineWidth = (e.LineWidth ?? 0.25) * k;
                }
                else if (e.Kind == SourceKind.Circle && e.Center.HasValue && e.Radius.HasValue)
                {
                    copy = Clone(e, SourceKind.Circle);
                    copy.Center = new Point3(e.Center.Value.X * k, e.Center.Value.Y * k, 0);
                    copy.Radius = e.Radius.Value * k;
                }

                if (copy == null) continue;
                page.Entities.Add(copy);
                if (copy.Points != null) foreach (var p in copy.Points) { maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
                if (copy.Center.HasValue) { maxX = Math.Max(maxX, copy.Center.Value.X); maxY = Math.Max(maxY, copy.Center.Value.Y); }
            }

            page.Width = maxX;
            page.Height = maxY;
            return page;
        }

        /// <summary>Bản vẽ ISOGEN nhận ra bằng bộ layer chuẩn của nó.</summary>
        public static bool LooksLikeIsogen(IEnumerable<SourceEntity> entities)
        {
            var layers = new HashSet<string>(entities.Select(e => e.Layer), StringComparer.OrdinalIgnoreCase);
            return layers.Contains("PIPE") && (layers.Contains("DIMLINES") || layers.Contains("ISOTEXT") || layers.Contains("MATLIST"));
        }

        private static SourceEntity Clone(SourceEntity e, SourceKind kind)
        {
            return new SourceEntity
            {
                Handle = e.Handle,
                Kind = kind,
                NativeType = e.NativeType,
                Layer = e.Layer,
                Color = e.Color,
                Linetype = e.Linetype,
                BlockPath = new List<string>(e.BlockPath),
            };
        }

        private static Box2 TextBounds(Point2 at, int chars, double height, double rotationDeg)
        {
            var w = Math.Max(1, chars) * height * 0.8;
            var rad = rotationDeg * Math.PI / 180;
            var dir = new Vector2(Math.Cos(rad), Math.Sin(rad));
            var up = new Vector2(-Math.Sin(rad), Math.Cos(rad));
            var corners = new[] { at, at + dir * w, at + up * height, at + dir * w + up * height };
            return Box2.FromPoints(corners);
        }
    }
}
