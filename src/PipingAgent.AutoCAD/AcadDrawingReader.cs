using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Source;
using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcPoint = Autodesk.AutoCAD.DatabaseServices.DBPoint;

namespace PipingAgent.AutoCAD
{
    /// <summary>
    /// Bộ đọc bản vẽ đang mở trong AutoCAD, cài đặt <see cref="IDrawingReader"/>. Chỉ đọc, không sửa bản vẽ.
    /// Dùng chung quy ước với bộ đọc DXF: handle hex, layer 0 trong block mang layer của lần chèn, toạ độ ra mm.
    /// Phải gọi trong một giao dịch đang mở và khi tài liệu đã khoá.
    /// </summary>
    public sealed class AcadDrawingReader : IDrawingReader
    {
        private readonly Database _database;
        private readonly Transaction _transaction;
        private readonly double _scale;
        private readonly HashSet<string>? _onlyHandles;
        private readonly Dictionary<string, (int? Color, string? Linetype)> _layerCache = new Dictionary<string, (int?, string?)>(StringComparer.OrdinalIgnoreCase);
        private List<SourceEntity>? _modelSpaceCache;

        /// <param name="onlyHandles">Chỉ đọc các đối tượng có handle trong tập này, ví dụ tập đã chọn. Null là cả không gian mô hình.</param>
        public AcadDrawingReader(Database database, Transaction transaction, double? unitOverrideToMm = null, IEnumerable<string>? onlyHandles = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            Units = DrawingUnits.Resolve(MapUnit(database.Insunits));
            _scale = unitOverrideToMm ?? Units.ScaleToMm;
            _onlyHandles = onlyHandles == null ? null : new HashSet<string>(onlyHandles, StringComparer.OrdinalIgnoreCase);
        }

        public UnitResolution Units { get; }

        public double ScaleToMm => _scale;

        public List<string> Skipped { get; } = new List<string>();

        public DrawingSummary GetSummary()
        {
            var entities = ModelSpace();
            var summary = ((IReadOnlyList<SourceEntity>)entities).Summarize();
            summary.File = _database.Filename;
            summary.FormatVersion = _database.LastSavedAsVersion.ToString();
            summary.DeclaredUnits = Units.DeclaredName;
            summary.UnitScaleToMm = _scale;

            // Danh sách layer đầy đủ, kể cả layer không có đối tượng.
            var byName = summary.Layers.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
            summary.Layers.Clear();
            var layerTable = (LayerTable)_transaction.GetObject(_database.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId id in layerTable)
            {
                var record = (LayerTableRecord)_transaction.GetObject(id, OpenMode.ForRead);
                var info = byName.TryGetValue(record.Name, out var existing) ? existing : new LayerInfo { Name = record.Name };
                info.Color = record.Color.IsByAci ? record.Color.ColorIndex : (int?)null;
                info.IsOff = record.IsOff;
                info.IsFrozen = record.IsFrozen;
                info.Linetype = ((LinetypeTableRecord)_transaction.GetObject(record.LinetypeObjectId, OpenMode.ForRead)).Name;
                summary.Layers.Add(info);
            }

            summary.Layers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            var blockTable = (BlockTable)_transaction.GetObject(_database.BlockTableId, OpenMode.ForRead);
            var inserts = summary.Blocks.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
            summary.Blocks.Clear();
            foreach (ObjectId id in blockTable)
            {
                var record = (BlockTableRecord)_transaction.GetObject(id, OpenMode.ForRead);
                if (record.IsLayout || record.IsAnonymous) continue;
                if (record.IsFromExternalReference || record.IsFromOverlayReference) summary.HasXrefs = true;
                var usage = inserts.TryGetValue(record.Name, out var existing) ? existing : new BlockUsage { Name = record.Name };
                usage.EntityCount = record.Cast<ObjectId>().Count();
                usage.HasAttributes = record.HasAttributeDefinitions;
                usage.NestedDepth = NestedDepth(record, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                summary.Blocks.Add(usage);
            }

            summary.Blocks.Sort((a, b) => b.InsertCount.CompareTo(a.InsertCount));
            summary.HasProxyEntities = entities.Any(e => e.Kind == SourceKind.Other && e.NativeType != null && e.NativeType.StartsWith("ACAD_PROXY", StringComparison.OrdinalIgnoreCase));
            return summary;
        }

        public IReadOnlyList<SourceEntity> ReadEntities(EntityFilter filter)
        {
            filter ??= EntityFilter.All;
            IEnumerable<SourceEntity> source = ModelSpace();
            if (filter.IncludePaperSpace)
            {
                var blockTable = (BlockTable)_transaction.GetObject(_database.BlockTableId, OpenMode.ForRead);
                var paper = (BlockTableRecord)_transaction.GetObject(blockTable[BlockTableRecord.PaperSpace], OpenMode.ForRead);
                source = source.Concat(Convert(paper, new List<string>(), Matrix3d.Scaling(_scale, Point3d.Origin), null, null, false));
            }

            if (filter.ExplodeBlocks)
            {
                source = source.SelectMany(e => e.Kind == SourceKind.Insert && e.BlockPath.Count == 0
                    ? new[] { e }.Concat(Explode(e, filter, 1))
                    : new[] { e });
            }

            return source.ApplyFilter(filter);
        }

        /// <summary>Chưa cài đặt trong giai đoạn 1. Trả mảng rỗng theo hợp đồng.</summary>
        public byte[] RenderRegion(Box2 region, RenderOptions options) => Array.Empty<byte>();

        private List<SourceEntity> ModelSpace()
        {
            if (_modelSpaceCache != null) return _modelSpaceCache;
            var blockTable = (BlockTable)_transaction.GetObject(_database.BlockTableId, OpenMode.ForRead);
            var model = (BlockTableRecord)_transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            _modelSpaceCache = Convert(model, new List<string>(), Matrix3d.Scaling(_scale, Point3d.Origin), null, null, true).ToList();
            return _modelSpaceCache;
        }

        private IEnumerable<SourceEntity> Explode(SourceEntity insert, EntityFilter filter, int depth)
        {
            if (depth > filter.MaxBlockDepth) yield break;
            var reference = FindReference(insert.Handle);
            if (reference == null) yield break;
            var record = (BlockTableRecord)_transaction.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
            var matrix = Matrix3d.Scaling(_scale, Point3d.Origin) * ParentTransform(insert) * reference.BlockTransform;
            var path = new List<string>(insert.BlockPath) { insert.Handle };
            foreach (var child in Convert(record, path, matrix, insert.Layer, insert.Color, false))
            {
                yield return child;
                if (child.Kind == SourceKind.Insert)
                {
                    foreach (var grandChild in Explode(child, filter, depth + 1)) yield return grandChild;
                }
            }
        }

        // Với block lồng nhau, lần chèn con nằm trong hệ toạ độ của block cha. BlockTransform của con đã gồm
        // vị trí trong block cha, nên chỉ cần ma trận của chuỗi cha. Ở đây lưu lại qua handle của cha.
        private readonly Dictionary<string, Matrix3d> _parentTransforms = new Dictionary<string, Matrix3d>(StringComparer.OrdinalIgnoreCase);

        private Matrix3d ParentTransform(SourceEntity insert) =>
            _parentTransforms.TryGetValue(insert.Handle, out var m) ? m : Matrix3d.Identity;

        private BlockReference? FindReference(string handle)
        {
            if (!long.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)) return null;
            if (!_database.TryGetObjectId(new Handle(value), out var id)) return null;
            return _transaction.GetObject(id, OpenMode.ForRead) as BlockReference;
        }

        private IEnumerable<SourceEntity> Convert(BlockTableRecord record, List<string> blockPath, Matrix3d matrix, string? parentLayer, int? parentColor, bool applySelection)
        {
            foreach (ObjectId id in record)
            {
                Entity? entity;
                try
                {
                    entity = _transaction.GetObject(id, OpenMode.ForRead) as Entity;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception exception)
                {
                    Skipped.Add($"{id.Handle} {id.ObjectClass.DxfName}: {exception.Message}");
                    continue;
                }

                if (entity == null) continue;
                var handle = entity.Handle.ToString();
                if (applySelection && _onlyHandles != null && !_onlyHandles.Contains(handle)) continue;

                SourceEntity? converted = null;
                try
                {
                    converted = ConvertOne(entity, blockPath, matrix, parentLayer, parentColor);
                }
                catch (Exception exception)
                {
                    Skipped.Add($"{handle} {id.ObjectClass.DxfName}: {exception.Message}");
                }

                if (converted != null)
                {
                    if (converted.Kind == SourceKind.Insert && blockPath.Count > 0)
                    {
                        // Ma trận cha cho lần chèn lồng: ma trận hiện tại, chưa có BlockTransform của chính nó.
                        _parentTransforms[converted.Handle] = Matrix3d.Scaling(1 / _scale, Point3d.Origin) * matrix;
                    }

                    yield return converted;
                }
            }
        }

        private SourceEntity ConvertOne(Entity entity, List<string> blockPath, Matrix3d matrix, string? parentLayer, int? parentColor)
        {
            var layerName = entity.Layer;
            if (blockPath.Count > 0 && layerName == "0" && parentLayer != null) layerName = parentLayer;

            var uniformScale = matrix.CoordinateSystem3d.Xaxis.Length;
            var rotationDeg = Math.Atan2(matrix.CoordinateSystem3d.Xaxis.Y, matrix.CoordinateSystem3d.Xaxis.X) * 180.0 / Math.PI;

            var e = new SourceEntity
            {
                Handle = entity.Handle.ToString(),
                NativeType = entity.GetType().Name == "Polyline" ? "LWPOLYLINE" : GetDxfName(entity),
                Layer = layerName,
                Color = ResolveColor(entity.Color, layerName, parentColor),
                Linetype = ResolveLinetype(entity.Linetype, layerName),
                BlockPath = new List<string>(blockPath),
            };

            Point3 P(Point3d p) => ToPoint(p.TransformBy(matrix));
            Vector3 V(Vector3d v) => ToVector(v.TransformBy(matrix));

            switch (entity)
            {
                case Line line:
                    e.Kind = SourceKind.Line;
                    e.Points = new List<Point3> { P(line.StartPoint), P(line.EndPoint) };
                    break;

                case Polyline polyline:
                    e.Kind = SourceKind.Polyline;
                    e.Points = new List<Point3>();
                    var bulges = new List<double>();
                    for (var i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        e.Points.Add(P(polyline.GetPoint3dAt(i)));
                        bulges.Add(polyline.GetBulgeAt(i));
                    }

                    if (bulges.Any(b => Math.Abs(b) > 1e-12)) e.Bulges = bulges;
                    e.Closed = polyline.Closed;
                    break;

                case Polyline2d polyline2d:
                    e.Kind = SourceKind.Polyline;
                    e.Points = new List<Point3>();
                    var bulges2d = new List<double>();
                    foreach (ObjectId vertexId in polyline2d)
                    {
                        var vertex = (Vertex2d)_transaction.GetObject(vertexId, OpenMode.ForRead);
                        e.Points.Add(P(vertex.Position));
                        bulges2d.Add(vertex.Bulge);
                    }

                    if (bulges2d.Any(b => Math.Abs(b) > 1e-12)) e.Bulges = bulges2d;
                    e.Closed = polyline2d.Closed;
                    break;

                case Polyline3d polyline3d:
                    e.Kind = SourceKind.Polyline;
                    e.Points = new List<Point3>();
                    foreach (ObjectId vertexId in polyline3d)
                    {
                        var vertex = (PolylineVertex3d)_transaction.GetObject(vertexId, OpenMode.ForRead);
                        e.Points.Add(P(vertex.Position));
                    }

                    e.Closed = polyline3d.Closed;
                    break;

                case Arc arc:
                    e.Kind = SourceKind.Arc;
                    e.Center = P(arc.Center);
                    e.Radius = arc.Radius * uniformScale;
                    e.StartAngleDeg = arc.StartAngle * 180.0 / Math.PI + rotationDeg;
                    e.EndAngleDeg = arc.EndAngle * 180.0 / Math.PI + rotationDeg;
                    break;

                case Circle circle:
                    e.Kind = SourceKind.Circle;
                    e.Center = P(circle.Center);
                    e.Radius = circle.Radius * uniformScale;
                    break;

                case Ellipse ellipse:
                    e.Kind = SourceKind.Ellipse;
                    e.Center = P(ellipse.Center);
                    e.MajorAxis = V(ellipse.MajorAxis);
                    e.AxisRatio = ellipse.RadiusRatio;
                    e.StartAngleDeg = ellipse.StartParam * 180.0 / Math.PI;
                    e.EndAngleDeg = ellipse.EndParam * 180.0 / Math.PI;
                    break;

                case AttributeReference attribute:
                    e.Kind = SourceKind.Attribute;
                    e.Name = attribute.Tag;
                    e.RawText = attribute.TextString;
                    e.Text = CadText.Strip(attribute.TextString);
                    e.Center = P(attribute.Position);
                    e.TextHeight = attribute.Height * uniformScale;
                    e.RotationDeg = attribute.Rotation * 180.0 / Math.PI + rotationDeg;
                    break;

                case AttributeDefinition definition:
                    e.Kind = SourceKind.Other;
                    e.Name = definition.Tag;
                    e.Text = CadText.Strip(definition.TextString);
                    e.Center = P(definition.Position);
                    break;

                case DBText text:
                    e.Kind = SourceKind.Text;
                    e.RawText = text.TextString;
                    e.Text = CadText.Strip(text.TextString);
                    e.Center = P(text.Position);
                    e.TextHeight = text.Height * uniformScale;
                    e.RotationDeg = text.Rotation * 180.0 / Math.PI + rotationDeg;
                    break;

                case MText mtext:
                    e.Kind = SourceKind.MText;
                    e.RawText = mtext.Contents;
                    e.Text = CadText.Strip(mtext.Contents);
                    e.Center = P(mtext.Location);
                    e.TextHeight = mtext.TextHeight * uniformScale;
                    e.RotationDeg = mtext.Rotation * 180.0 / Math.PI + rotationDeg;
                    break;

                case BlockReference reference:
                    e.Kind = SourceKind.Insert;
                    e.Name = ((BlockTableRecord)_transaction.GetObject(reference.BlockTableRecord, OpenMode.ForRead)).Name;
                    e.Center = P(reference.Position);
                    e.Scale = new Vector3(reference.ScaleFactors.X * uniformScale, reference.ScaleFactors.Y * uniformScale, reference.ScaleFactors.Z * uniformScale);
                    e.RotationDeg = reference.Rotation * 180.0 / Math.PI + rotationDeg;
                    if (reference.AttributeCollection.Count > 0)
                    {
                        e.Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (ObjectId attributeId in reference.AttributeCollection)
                        {
                            var attribute = (AttributeReference)_transaction.GetObject(attributeId, OpenMode.ForRead);
                            e.Attributes[attribute.Tag] = CadText.Strip(attribute.TextString);
                        }
                    }

                    break;

                case Leader leader:
                    e.Kind = SourceKind.Leader;
                    e.Points = new List<Point3>();
                    for (var i = 0; i < leader.NumVertices; i++) e.Points.Add(P(leader.VertexAt(i)));
                    if (!leader.Annotation.IsNull) e.AttachedTo = new List<string> { leader.Annotation.Handle.ToString() };
                    break;

                case MLeader mleader:
                    e.Kind = SourceKind.MultiLeader;
                    e.Points = new List<Point3>();
                    foreach (int leaderIndex in mleader.GetLeaderIndexes())
                    {
                        var lineIndexes = mleader.GetLeaderLineIndexes(leaderIndex).Cast<int>().ToList();
                        foreach (var lineIndex in lineIndexes)
                        {
                            var count = mleader.VerticesCount(lineIndex);
                            for (var v = 0; v < count; v++) e.Points.Add(P(mleader.GetVertex(lineIndex, v)));
                        }

                    }

                    // Chỉ ghi điểm gốc nội dung khi có nội dung, giống bộ đọc DXF.
                    if (mleader.ContentType == ContentType.MTextContent && mleader.MText != null)
                    {
                        e.RawText = mleader.MText.Contents;
                        e.Text = CadText.Strip(mleader.MText.Contents);
                        e.Center = P(mleader.TextLocation);
                    }
                    else if (mleader.ContentType == ContentType.BlockContent && !mleader.BlockContentId.IsNull)
                    {
                        e.Name = ((BlockTableRecord)_transaction.GetObject(mleader.BlockContentId, OpenMode.ForRead)).Name;
                        e.Center = P(mleader.BlockPosition);
                    }

                    break;

                case Dimension dimension:
                    e.Kind = SourceKind.Dimension;
                    e.Measurement = dimension.Measurement * uniformScale;
                    e.RawText = dimension.DimensionText;
                    e.Text = CadText.Strip(dimension.DimensionText);
                    e.Center = P(dimension.TextPosition);
                    break;

                case AcPoint point:
                    e.Kind = SourceKind.Point;
                    e.Center = P(point.Position);
                    break;

                case Solid solid:
                    e.Kind = SourceKind.Solid;
                    e.Points = new List<Point3> { P(solid.GetPointAt(0)), P(solid.GetPointAt(1)), P(solid.GetPointAt(2)), P(solid.GetPointAt(3)) };
                    e.Closed = true;
                    break;

                case Spline spline:
                    e.Kind = SourceKind.Spline;
                    e.Points = new List<Point3>();
                    if (spline.NumFitPoints > 0)
                    {
                        for (var i = 0; i < spline.NumFitPoints; i++) e.Points.Add(P(spline.GetFitPointAt(i)));
                    }
                    else
                    {
                        for (var i = 0; i < spline.NumControlPoints; i++) e.Points.Add(P(spline.GetControlPointAt(i)));
                    }

                    e.Closed = spline.Closed;
                    break;

                case Hatch hatch:
                    e.Kind = SourceKind.Hatch;
                    e.Name = hatch.PatternName;
                    e.Points = new List<Point3>();
                    for (var i = 0; i < hatch.NumberOfLoops; i++)
                    {
                        var loop = hatch.GetLoopAt(i);
                        if (loop.IsPolyline)
                        {
                            var vertices = loop.Polyline.Cast<BulgeVertex>().Select(v => P(new Point3d(v.Vertex.X, v.Vertex.Y, hatch.Elevation))).ToList();
                            // AutoCAD lặp lại đỉnh đầu ở cuối vòng kín, DXF thì không. Bỏ để hai bộ đọc khớp nhau.
                            if (vertices.Count > 2 && vertices[0].DistanceTo(vertices[vertices.Count - 1]) < 1e-6) vertices.RemoveAt(vertices.Count - 1);
                            e.Points.AddRange(vertices);
                        }
                        else
                        {
                            foreach (Curve2d curve in loop.Curves)
                            {
                                if (curve is LineSegment2d segment)
                                {
                                    e.Points.Add(P(new Point3d(segment.StartPoint.X, segment.StartPoint.Y, hatch.Elevation)));
                                    e.Points.Add(P(new Point3d(segment.EndPoint.X, segment.EndPoint.Y, hatch.Elevation)));
                                }
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

        private int? ResolveColor(AcColor color, string layerName, int? parentColor)
        {
            if (color.IsByLayer) return LayerInfo(layerName).Color;
            if (color.IsByBlock) return parentColor;
            if (!color.IsByAci) return null;
            return color.ColorIndex;
        }

        private string? ResolveLinetype(string linetype, string layerName)
        {
            if (string.IsNullOrEmpty(linetype) || string.Equals(linetype, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                return LayerInfo(layerName).Linetype;
            }

            return linetype;
        }

        private (int? Color, string? Linetype) LayerInfo(string layerName)
        {
            if (_layerCache.TryGetValue(layerName, out var cached)) return cached;
            var layerTable = (LayerTable)_transaction.GetObject(_database.LayerTableId, OpenMode.ForRead);
            (int?, string?) result = (null, null);
            if (layerTable.Has(layerName))
            {
                var record = (LayerTableRecord)_transaction.GetObject(layerTable[layerName], OpenMode.ForRead);
                var linetype = (LinetypeTableRecord)_transaction.GetObject(record.LinetypeObjectId, OpenMode.ForRead);
                result = (record.Color.IsByAci ? record.Color.ColorIndex : (int?)null, linetype.Name);
            }

            _layerCache[layerName] = result;
            return result;
        }

        private int NestedDepth(BlockTableRecord record, int depth, HashSet<string> visiting)
        {
            if (depth > 32 || !visiting.Add(record.Name)) return depth;
            var max = depth;
            foreach (ObjectId id in record)
            {
                if (id.ObjectClass.DxfName != "INSERT") continue;
                var reference = (BlockReference)_transaction.GetObject(id, OpenMode.ForRead);
                var child = (BlockTableRecord)_transaction.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
                max = Math.Max(max, NestedDepth(child, depth + 1, visiting));
            }

            visiting.Remove(record.Name);
            return max;
        }

        private static string GetDxfName(Entity entity) => entity.ObjectId.IsNull ? entity.GetType().Name.ToUpperInvariant() : entity.ObjectId.ObjectClass.DxfName;

        private static DeclaredUnit MapUnit(UnitsValue units)
        {
            var value = (int)units;
            return Enum.IsDefined(typeof(DeclaredUnit), value) ? (DeclaredUnit)value : DeclaredUnit.Unitless;
        }

        private static Point3 ToPoint(Point3d p) => new Point3(p.X, p.Y, p.Z);

        private static Vector3 ToVector(Vector3d v) => new Vector3(v.X, v.Y, v.Z);
    }
}
