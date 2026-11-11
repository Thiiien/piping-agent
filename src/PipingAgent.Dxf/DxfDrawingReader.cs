using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Source;

namespace PipingAgent.Dxf
{
    /// <summary>
    /// Bộ đọc DXF và DWG chạy ngoài CAD, cài đặt <see cref="IDrawingReader"/> bằng ACadSharp.
    /// Mọi toạ độ được nhân với hệ số đơn vị để ra mm. Handle giữ nguyên dạng hex của AutoCAD.
    /// Đối tượng lỗi được bỏ qua và ghi vào <see cref="Skipped"/>, không dừng cả tệp, theo FR-REC-10.
    /// </summary>
    public sealed class DxfDrawingReader : IDrawingReader
    {
        private readonly CadDocument _document;
        private readonly string? _fileName;
        private readonly double _scale;
        private readonly Dictionary<string, Layer> _layers;
        private List<SourceEntity>? _modelSpaceCache;

        private DxfDrawingReader(CadDocument document, string? fileName, double? unitOverrideToMm)
        {
            _document = document;
            _fileName = fileName;
            Units = DrawingUnits.Resolve(MapUnit(document.Header.InsUnits));
            _scale = unitOverrideToMm ?? Units.ScaleToMm;
            _layers = new Dictionary<string, Layer>(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in document.Layers)
            {
                _layers[layer.Name] = layer;
            }
        }

        /// <summary>Kết quả nhận biết đơn vị của tệp. Nếu <c>NeedsConfirmation</c> thì hỏi người dùng rồi mở lại với hệ số đã chọn.</summary>
        public UnitResolution Units { get; }

        /// <summary>Hệ số đang dùng để quy toạ độ về mm.</summary>
        public double ScaleToMm => _scale;

        /// <summary>Các đối tượng không đọc được, kèm lý do.</summary>
        public List<string> Skipped { get; } = new List<string>();

        /// <param name="path">Tệp .dxf hoặc .dwg.</param>
        /// <param name="unitOverrideToMm">Hệ số đơn vị do người dùng xác nhận, thay cho giá trị khai báo trong tệp.</param>
        public static DxfDrawingReader Open(string path, double? unitOverrideToMm = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            CadDocument document;
            if (path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new ACadSharp.IO.DwgReader(path);
                document = reader.Read();
            }
            else
            {
                using var reader = new ACadSharp.IO.DxfReader(path);
                document = reader.Read();
            }

            return new DxfDrawingReader(document, Path.GetFileName(path), unitOverrideToMm);
        }

        public static DxfDrawingReader Open(Stream stream, bool isDwg, string? displayName = null, double? unitOverrideToMm = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            CadDocument document;
            if (isDwg)
            {
                using var reader = new ACadSharp.IO.DwgReader(stream);
                document = reader.Read();
            }
            else
            {
                using var reader = new ACadSharp.IO.DxfReader(stream);
                document = reader.Read();
            }

            return new DxfDrawingReader(document, displayName, unitOverrideToMm);
        }

        public DrawingSummary GetSummary()
        {
            var entities = ModelSpace();
            var summary = new DrawingSummary
            {
                File = _fileName,
                FormatVersion = _document.Header.Version.ToString(),
                DeclaredUnits = Units.DeclaredName,
                UnitScaleToMm = _scale,
                EntityCount = entities.Count,
                Fingerprint = DrawingFingerprint.Compute(entities),
            };

            foreach (var e in entities)
            {
                summary.CountsByKind[e.Kind.ToString()] = summary.CountsByKind.TryGetValue(e.Kind.ToString(), out var n) ? n + 1 : 1;
            }

            var byLayer = entities.GroupBy(e => e.Layer, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (var layer in _document.Layers.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
            {
                var info = new LayerInfo
                {
                    Name = layer.Name,
                    Color = layer.Color.IsTrueColor ? (int?)null : layer.Color.Index,
                    Linetype = layer.LineType?.Name,
                    IsOff = !layer.IsOn,
                    IsFrozen = (layer.Flags & LayerFlags.Frozen) != 0,
                };
                if (byLayer.TryGetValue(layer.Name, out var list))
                {
                    info.EntityCount = list.Count;
                    foreach (var g in list.GroupBy(e => e.Kind)) info.CountsByKind[g.Key.ToString()] = g.Count();
                }

                summary.Layers.Add(info);
            }

            var insertCounts = entities.Where(e => e.Kind == SourceKind.Insert && e.Name != null)
                .GroupBy(e => e.Name!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            foreach (var record in _document.BlockRecords)
            {
                if (record.Name.StartsWith("*", StringComparison.Ordinal)) continue; // không gian mô hình, giấy, block vô danh của hệ thống
                insertCounts.TryGetValue(record.Name, out var count);
                summary.Blocks.Add(new BlockUsage
                {
                    Name = record.Name,
                    InsertCount = count,
                    EntityCount = record.Entities.Count,
                    HasAttributes = record.AttributeDefinitions.Any(),
                    NestedDepth = NestedDepth(record, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
                });
                if (record.IsUnloaded || record.BlockEntity?.Flags.HasFlag(ACadSharp.Blocks.BlockTypeFlags.XRef) == true)
                {
                    summary.HasXrefs = true;
                }
            }

            summary.Blocks.Sort((a, b) => b.InsertCount.CompareTo(a.InsertCount));
            summary.HasProxyEntities = _document.Entities.Any(e => e is UnknownEntity);

            var boxes = entities.Where(e => e.Bounds.HasValue).Select(e => e.Bounds!.Value).ToList();
            if (boxes.Count > 0)
            {
                var box = boxes[0];
                foreach (var b in boxes.Skip(1)) box = box.Union(b);
                summary.Extents = box;
            }

            return summary;
        }

        public IReadOnlyList<SourceEntity> ReadEntities(EntityFilter filter)
        {
            filter ??= EntityFilter.All;
            IEnumerable<SourceEntity> source = ModelSpace();
            if (filter.IncludePaperSpace)
            {
                source = source.Concat(Convert(_document.PaperSpace.Entities, new List<string>(), Affine.Scale(_scale), null, null, filter, 0));
            }

            if (filter.ExplodeBlocks)
            {
                source = source.SelectMany(e => e.Kind == SourceKind.Insert && e.BlockPath.Count == 0
                    ? new[] { e }.Concat(Explode(e, filter))
                    : new[] { e });
            }

            return source.ApplyFilter(filter);
        }

        private List<SourceEntity> ModelSpace()
        {
            if (_modelSpaceCache != null) return _modelSpaceCache;
            _modelSpaceCache = Convert(_document.Entities, new List<string>(), Affine.Scale(_scale), null, null, EntityFilter.All, 0).ToList();
            return _modelSpaceCache;
        }

        // Lần chèn và ma trận tại mức của nó, lưu khi chuyển đổi, để nổ block lồng nhau đúng phép biến đổi.
        private readonly Dictionary<string, (Insert Insert, Affine Matrix)> _inserts = new Dictionary<string, (Insert, Affine)>(StringComparer.Ordinal);

        private static string PathKey(SourceEntity e) => e.BlockPath.Count == 0 ? e.Handle : string.Join("/", e.BlockPath) + "/" + e.Handle;

        private IEnumerable<SourceEntity> Explode(SourceEntity insert, EntityFilter filter)
        {
            if (!_inserts.TryGetValue(PathKey(insert), out var found) || found.Insert.Block == null) yield break;
            var matrix = found.Matrix.Multiply(Affine.FromInsert(found.Insert));
            var path = new List<string>(insert.BlockPath) { insert.Handle };
            foreach (var child in Convert(found.Insert.Block.Entities, path, matrix, insert.Layer, insert.Color, filter, 1))
            {
                yield return child;
                if (child.Kind == SourceKind.Insert && path.Count < filter.MaxBlockDepth)
                {
                    foreach (var grandChild in Explode(child, filter)) yield return grandChild;
                }
            }
        }

        private IEnumerable<SourceEntity> Convert(
            IEnumerable<Entity> entities, List<string> blockPath, Affine matrix,
            string? parentLayer, int? parentColor, EntityFilter filter, int depth)
        {
            foreach (var entity in entities)
            {
                SourceEntity? converted = null;
                try
                {
                    converted = ConvertOne(entity, blockPath, matrix, parentLayer, parentColor);
                }
                catch (Exception exception)
                {
                    Skipped.Add($"{entity.Handle:X} {entity.ObjectName}: {exception.Message}");
                }

                if (converted != null)
                {
                    if (entity is Insert insertEntity) _inserts[PathKey(converted)] = (insertEntity, matrix);
                    yield return converted;
                }
            }
        }

        private SourceEntity? ConvertOne(Entity entity, List<string> blockPath, Affine matrix, string? parentLayer, int? parentColor)
        {
            var layerName = entity.Layer?.Name ?? "0";
            // Đối tượng trong block nằm trên layer 0 thì mang layer của lần chèn.
            if (blockPath.Count > 0 && layerName == "0" && parentLayer != null) layerName = parentLayer;

            var e = new SourceEntity
            {
                Handle = entity.Handle.ToString("X", CultureInfo.InvariantCulture),
                NativeType = entity.ObjectName,
                Layer = layerName,
                Color = ResolveColor(entity, layerName, parentColor),
                Linetype = ResolveLinetype(entity, layerName),
                BlockPath = new List<string>(blockPath),
                LineWidth = ResolveLineWidth(entity),
            };

            switch (entity)
            {
                case Line line:
                    e.Kind = SourceKind.Line;
                    e.Points = new List<Point3> { matrix.Apply(line.StartPoint), matrix.Apply(line.EndPoint) };
                    break;

                case LwPolyline lw:
                    e.Kind = SourceKind.Polyline;
                    e.Points = lw.Vertices.Select(v => matrix.Apply(OcsToWcs(new XYZ(v.Location.X, v.Location.Y, lw.Elevation), lw.Normal))).ToList();
                    if (lw.Vertices.Any(v => Math.Abs(v.Bulge) > 1e-12)) e.Bulges = lw.Vertices.Select(v => v.Bulge).ToList();
                    e.Closed = lw.IsClosed;
                    break;

                case IPolyline poly:
                    e.Kind = SourceKind.Polyline;
                    e.Points = poly.Vertices.Select(v => matrix.Apply(ToXyz(v.Location))).ToList();
                    e.Closed = poly.IsClosed;
                    if (entity is Polyline3D == false && entity is Polyline2D p2 && p2.Vertices.Any(v => Math.Abs(v.Bulge) > 1e-12))
                    {
                        e.Bulges = p2.Vertices.Select(v => v.Bulge).ToList();
                    }

                    break;

                case Arc arc:
                    e.Kind = SourceKind.Arc;
                    e.Center = matrix.Apply(OcsToWcs(arc.Center, arc.Normal));
                    e.Radius = arc.Radius * matrix.UniformScale;
                    e.StartAngleDeg = ToDeg(arc.StartAngle) + matrix.RotationDeg;
                    e.EndAngleDeg = ToDeg(arc.EndAngle) + matrix.RotationDeg;
                    break;

                case Circle circle:
                    e.Kind = SourceKind.Circle;
                    e.Center = matrix.Apply(OcsToWcs(circle.Center, circle.Normal));
                    e.Radius = circle.Radius * matrix.UniformScale;
                    break;

                case Ellipse ellipse:
                    e.Kind = SourceKind.Ellipse;
                    e.Center = matrix.Apply(ellipse.Center);
                    e.MajorAxis = matrix.ApplyVector(ellipse.MajorAxisEndPoint);
                    e.AxisRatio = ellipse.RadiusRatio;
                    e.StartAngleDeg = ToDeg(ellipse.StartParameter);
                    e.EndAngleDeg = ToDeg(ellipse.EndParameter);
                    break;

                case TextEntity text when !(entity is AttributeBase):
                    e.Kind = SourceKind.Text;
                    e.RawText = text.Value;
                    e.Text = CadText.Strip(text.Value);
                    e.Center = matrix.Apply(OcsToWcs(text.InsertPoint, text.Normal));
                    e.TextHeight = text.Height * matrix.UniformScale;
                    e.RotationDeg = ToDeg(text.Rotation) + matrix.RotationDeg;
                    break;

                case AttributeEntity attribute:
                    e.Kind = SourceKind.Attribute;
                    e.Name = attribute.Tag;
                    e.RawText = attribute.Value;
                    e.Text = CadText.Strip(attribute.Value);
                    e.Center = matrix.Apply(OcsToWcs(attribute.InsertPoint, attribute.Normal));
                    e.TextHeight = attribute.Height * matrix.UniformScale;
                    e.RotationDeg = ToDeg(attribute.Rotation) + matrix.RotationDeg;
                    break;

                case AttributeDefinition definition:
                    // Định nghĩa thuộc tính trong block, không phải nội dung bản vẽ. Chỉ gặp khi nổ block.
                    e.Kind = SourceKind.Other;
                    e.Name = definition.Tag;
                    e.Text = CadText.Strip(definition.Value);
                    e.Center = matrix.Apply(OcsToWcs(definition.InsertPoint, definition.Normal));
                    break;

                case MText mtext:
                    e.Kind = SourceKind.MText;
                    e.RawText = mtext.Value;
                    e.Text = CadText.Strip(mtext.Value);
                    e.Center = matrix.Apply(mtext.InsertPoint);
                    e.TextHeight = mtext.Height * matrix.UniformScale;
                    e.RotationDeg = ToDeg(mtext.Rotation) + matrix.RotationDeg;
                    break;

                case Insert insert:
                    e.Kind = SourceKind.Insert;
                    e.Name = insert.Block?.Name;
                    e.Center = matrix.Apply(OcsToWcs(insert.InsertPoint, insert.Normal));
                    e.Scale = new Vector3(insert.XScale * matrix.UniformScale, insert.YScale * matrix.UniformScale, insert.ZScale * matrix.UniformScale);
                    e.RotationDeg = ToDeg(insert.Rotation) + matrix.RotationDeg;
                    if (insert.HasAttributes)
                    {
                        e.Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var attribute in insert.Attributes)
                        {
                            e.Attributes[attribute.Tag] = CadText.Strip(attribute.Value);
                        }
                    }

                    break;

                case Leader leader:
                    e.Kind = SourceKind.Leader;
                    e.Points = leader.Vertices.Select(v => matrix.Apply(v)).ToList();
                    if (leader.AssociatedAnnotation != null)
                    {
                        e.AttachedTo = new List<string> { leader.AssociatedAnnotation.Handle.ToString("X", CultureInfo.InvariantCulture) };
                    }

                    break;

                case MultiLeader multiLeader:
                    e.Kind = SourceKind.MultiLeader;
                    e.Points = new List<Point3>();
                    var context = multiLeader.ContextData;
                    if (context != null)
                    {
                        foreach (var root in context.LeaderRoots)
                        {
                            foreach (var line in root.Lines)
                            {
                                foreach (var p in line.Points) e.Points.Add(matrix.Apply(p));
                            }

                            // ACadSharp không đưa đỉnh cuối vào Points, AutoCAD thì có. Thêm vào để hai bộ đọc khớp nhau.
                            e.Points.Add(matrix.Apply(root.ConnectionPoint));
                        }

                        if (context.HasTextContents && context.TextLabel != null)
                        {
                            e.RawText = context.TextLabel;
                            e.Text = CadText.Strip(context.TextLabel);
                        }

                        if (context.HasContentsBlock && context.BlockContent != null) e.Name = context.BlockContent.Name;
                        // Chỉ ghi điểm gốc nội dung khi có nội dung. Đường dẫn trống thì giá trị này tuỳ phần mềm, không so được.
                        if (context.HasTextContents) e.Center = matrix.Apply(context.TextLocation);
                        else if (context.HasContentsBlock) e.Center = matrix.Apply(context.BlockContentLocation);
                    }

                    break;

                case Dimension dimension:
                    e.Kind = SourceKind.Dimension;
                    e.Measurement = dimension.Measurement * matrix.UniformScale;
                    e.RawText = dimension.Text;
                    e.Text = CadText.Strip(dimension.Text);
                    e.Center = matrix.Apply(OcsToWcs(dimension.TextMiddlePoint, dimension.Normal));
                    break;

                case ACadSharp.Entities.Point point:
                    e.Kind = SourceKind.Point;
                    e.Center = matrix.Apply(point.Location);
                    break;

                case Solid solid:
                    e.Kind = SourceKind.Solid;
                    e.Points = new List<Point3>
                    {
                        matrix.Apply(OcsToWcs(solid.FirstCorner, solid.Normal)), matrix.Apply(OcsToWcs(solid.SecondCorner, solid.Normal)),
                        matrix.Apply(OcsToWcs(solid.ThirdCorner, solid.Normal)), matrix.Apply(OcsToWcs(solid.FourthCorner, solid.Normal)),
                    };
                    e.Closed = true;
                    break;

                case Spline spline:
                    e.Kind = SourceKind.Spline;
                    var pts = spline.FitPoints.Count > 0 ? spline.FitPoints : spline.ControlPoints;
                    e.Points = pts.Select(p => matrix.Apply(p)).ToList();
                    e.Closed = spline.IsClosed;
                    break;

                case Hatch hatch:
                    e.Kind = SourceKind.Hatch;
                    e.Name = hatch.Pattern?.Name;
                    e.Points = new List<Point3>();
                    foreach (var path in hatch.Paths)
                    {
                        foreach (var edge in path.Edges)
                        {
                            if (edge is Hatch.BoundaryPath.Line hl)
                            {
                                e.Points.Add(matrix.Apply(OcsToWcs(new XYZ(hl.Start.X, hl.Start.Y, hatch.Elevation), hatch.Normal)));
                                e.Points.Add(matrix.Apply(OcsToWcs(new XYZ(hl.End.X, hl.End.Y, hatch.Elevation), hatch.Normal)));
                            }
                            else if (edge is Hatch.BoundaryPath.Polyline hp)
                            {
                                foreach (var v in hp.Vertices) e.Points.Add(matrix.Apply(OcsToWcs(new XYZ(v.X, v.Y, hatch.Elevation), hatch.Normal)));
                            }
                        }
                    }

                    break;

                default:
                    e.Kind = SourceKind.Other;
                    break;
            }

            e.NormalizeAngles();
            e.Bounds = e.ComputeBounds();
            return e;
        }

        /// <summary>Độ dày nét theo mm, đã giải ByLayer và ByBlock; mặc định của AutoCAD là 0,25 mm. Isometric ISOGEN phân biệt ống bằng độ dày này.</summary>
        private static double? ResolveLineWidth(Entity entity)
        {
            try
            {
                var lw = (short)entity.GetActiveLineWeightType();
                if (lw < 0) return 0.25;
                return lw / 100.0;
            }
            catch
            {
                return null;
            }
        }

        private int? ResolveColor(Entity entity, string layerName, int? parentColor)
        {
            var color = entity.Color;
            if (color.IsByLayer)
            {
                return _layers.TryGetValue(layerName, out var layer) && !layer.Color.IsTrueColor ? layer.Color.Index : (int?)null;
            }

            if (color.IsByBlock) return parentColor;
            if (color.IsTrueColor) return null;
            return color.Index;
        }

        private string? ResolveLinetype(Entity entity, string layerName)
        {
            var name = entity.LineType?.Name;
            if (name == null || string.Equals(name, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                return _layers.TryGetValue(layerName, out var layer) ? layer.LineType?.Name : null;
            }

            return name;
        }

        private int NestedDepth(BlockRecord record, int depth, HashSet<string> visiting)
        {
            if (depth > 32 || !visiting.Add(record.Name)) return depth;
            var max = depth;
            foreach (var insert in record.Entities.OfType<Insert>())
            {
                if (insert.Block == null) continue;
                max = Math.Max(max, NestedDepth(insert.Block, depth + 1, visiting));
            }

            visiting.Remove(record.Name);
            return max;
        }

        private static DeclaredUnit MapUnit(ACadSharp.Types.Units.UnitsType units)
        {
            var value = (int)units;
            return Enum.IsDefined(typeof(DeclaredUnit), value) ? (DeclaredUnit)value : DeclaredUnit.Unitless;
        }

        private static double ToDeg(double radians) => radians * 180.0 / Math.PI;

        /// <summary>
        /// Đổi điểm từ hệ toạ độ đối tượng, OCS, sang hệ thế giới theo thuật toán trục tuỳ ý của AutoCAD.
        /// DXF lưu điểm của INSERT, CIRCLE, ARC, TEXT, LWPOLYLINE, SOLID, HATCH theo OCS. ACadSharp trả nguyên, nên phải đổi ở đây.
        /// Block lật gương có pháp tuyến (0, 0, -1) là trường hợp gặp trong bản vẽ thật.
        /// </summary>
        private static XYZ OcsToWcs(XYZ p, XYZ normal)
        {
            if (Math.Abs(normal.X) < 1e-12 && Math.Abs(normal.Y) < 1e-12 && normal.Z > 0) return p;
            var length = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
            if (length < 1e-12) return p;
            double nx = normal.X / length, ny = normal.Y / length, nz = normal.Z / length;

            // Ax = W × N với W là (0,1,0) khi N gần trục Z, ngược lại là (0,0,1).
            double ax, ay, az;
            if (Math.Abs(nx) < 1.0 / 64 && Math.Abs(ny) < 1.0 / 64)
            {
                ax = nz; ay = 0; az = -nx;              // (0,1,0) × N
            }
            else
            {
                ax = -ny; ay = nx; az = 0;              // (0,0,1) × N
            }

            var al = Math.Sqrt(ax * ax + ay * ay + az * az);
            ax /= al; ay /= al; az /= al;

            // Ay = N × Ax
            double bx = ny * az - nz * ay, by = nz * ax - nx * az, bz = nx * ay - ny * ax;

            return new XYZ(
                p.X * ax + p.Y * bx + p.Z * nx,
                p.X * ay + p.Y * by + p.Z * ny,
                p.X * az + p.Y * bz + p.Z * nz);
        }

        private static XYZ ToXyz(IVector v) => new XYZ(v[0], v[1], v.Dimension > 2 ? v[2] : 0);

        /// <summary>
        /// Phép biến đổi affine 3D, 3 hàng 4 cột. Dựng từ một lần chèn block theo đúng thứ tự của AutoCAD:
        /// tịnh tiến tới điểm chèn trong hệ thế giới, đổi trục OCS theo pháp tuyến, xoay quanh Z, rồi tỉ lệ ba trục.
        /// Góc xoay và tỉ lệ đều suy từ trục X đã biến đổi, giống cách bộ đọc AutoCAD suy từ Matrix3d, để hai bộ đọc khớp nhau.
        /// </summary>
        private readonly struct Affine
        {
            private readonly double[] _m; // m[r*4+c], r in 0..2, c in 0..3

            private Affine(double[] m) => _m = m;

            public static Affine Identity => Scale(1);

            public static Affine Scale(double s) => new Affine(new[] { s, 0, 0, 0, 0, s, 0, 0, 0, 0, s, 0 });

            public double RotationDeg => Math.Atan2(_m[4], _m[0]) * 180.0 / Math.PI;

            public double UniformScale => Math.Sqrt(_m[0] * _m[0] + _m[4] * _m[4] + _m[8] * _m[8]);

            public static Affine FromInsert(Insert insert)
            {
                var r = insert.Rotation;
                double c = Math.Cos(r), sn = Math.Sin(r);
                double sx = insert.XScale, sy = insert.YScale, sz = insert.ZScale;
                // Rz * S
                var rs = new[] { c * sx, -sn * sy, 0, 0, sn * sx, c * sy, 0, 0, 0, 0, sz, 0 };
                // OCS basis
                var n = insert.Normal;
                var ocs = OcsBasis(n);
                var local = Compose(ocs, rs);
                var origin = OcsToWcs(insert.InsertPoint, n);
                local[3] = origin.X;
                local[7] = origin.Y;
                local[11] = origin.Z;
                return new Affine(local);
            }

            private static double[] OcsBasis(XYZ normal)
            {
                var length = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
                if (length < 1e-12 || (Math.Abs(normal.X) < 1e-12 && Math.Abs(normal.Y) < 1e-12 && normal.Z > 0))
                {
                    return new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0 };
                }

                double nx = normal.X / length, ny = normal.Y / length, nz = normal.Z / length;
                double ax, ay, az;
                if (Math.Abs(nx) < 1.0 / 64 && Math.Abs(ny) < 1.0 / 64) { ax = nz; ay = 0; az = -nx; }
                else { ax = -ny; ay = nx; az = 0; }
                var al = Math.Sqrt(ax * ax + ay * ay + az * az);
                ax /= al; ay /= al; az /= al;
                double bx = ny * az - nz * ay, by = nz * ax - nx * az, bz = nx * ay - ny * ax;
                // Cột là Ax, Ay, N.
                return new[] { ax, bx, nx, 0, ay, by, ny, 0, az, bz, nz, 0 };
            }

            public Affine Multiply(Affine other) => new Affine(Compose(_m, other._m));

            private static double[] Compose(double[] a, double[] b)
            {
                var r = new double[12];
                for (var row = 0; row < 3; row++)
                {
                    for (var col = 0; col < 4; col++)
                    {
                        var v = a[row * 4] * b[col] + a[row * 4 + 1] * b[4 + col] + a[row * 4 + 2] * b[8 + col];
                        if (col == 3) v += a[row * 4 + 3];
                        r[row * 4 + col] = v;
                    }
                }

                return r;
            }

            public Point3 Apply(XYZ p) => new Point3(
                _m[0] * p.X + _m[1] * p.Y + _m[2] * p.Z + _m[3],
                _m[4] * p.X + _m[5] * p.Y + _m[6] * p.Z + _m[7],
                _m[8] * p.X + _m[9] * p.Y + _m[10] * p.Z + _m[11]);

            public Vector3 ApplyVector(XYZ v) => new Vector3(
                _m[0] * v.X + _m[1] * v.Y + _m[2] * v.Z,
                _m[4] * v.X + _m[5] * v.Y + _m[6] * v.Z,
                _m[8] * v.X + _m[9] * v.Y + _m[10] * v.Z);
        }
    }
}
