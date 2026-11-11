using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using PipingAgent.Core.Build;
using AcPoint3d = Autodesk.AutoCAD.Geometry.Point3d;
using AcVector3d = Autodesk.AutoCAD.Geometry.Vector3d;

namespace PipingAgent.AutoCAD.Builder
{
    public sealed class BuildStats
    {
        public int Pipes { get; set; }

        public int Elbows { get; set; }

        public int Tees { get; set; }

        public int Components { get; set; }

        public int Equipment { get; set; }

        public int Structures { get; set; }

        public int Layers { get; set; }

        /// <summary>Mối nối giữa hai cổng trùng nhau, S4-12.</summary>
        public int Connections { get; set; }

        /// <summary>Cổng không khớp với khối nào: đầu hở thật hoặc chỗ dựng chưa khít.</summary>
        public int OpenPorts { get; set; }

        public List<string> Failures { get; } = new List<string>();

        public int Total => Pipes + Elbows + Tees + Components + Equipment + Structures;

        public override string ToString() => $"{Pipes} ống, {Elbows} co, {Tees} tê, {Components} cấu kiện, {Equipment} thiết bị, {Structures} kết cấu trên {Layers} layer, {Connections} mối nối, {OpenPorts} cổng hở" + (Failures.Count > 0 ? $", {Failures.Count} phần tử lỗi" : string.Empty);
    }

    /// <summary>
    /// Dựng solid từ kế hoạch dựng vào một không gian mô hình, S3-11 đến S3-15. Toàn bộ trong một giao dịch do bên gọi mở,
    /// nên một lần Undo đưa bản vẽ về trước (S3-16) và lỗi giữa chừng thì bên gọi huỷ giao dịch, không để lại gì.
    /// Toạ độ kế hoạch là mm, chia cho <see cref="ScaleToMm"/> để ra đơn vị bản vẽ.
    /// </summary>
    public sealed class AcadBuilder
    {
        public const string AppName = "PIPINGAGENT";

        private readonly Database _db;
        private readonly Transaction _tr;
        private readonly BlockTableRecord _space;
        private readonly Dictionary<string, ObjectId> _layers = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);

        public AcadBuilder(Database db, Transaction tr, double scaleToMm = 1.0)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _tr = tr ?? throw new ArgumentNullException(nameof(tr));
            ScaleToMm = scaleToMm > 0 ? scaleToMm : 1.0;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            _space = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            EnsureRegApp();
            // Khung dây mặc định vẽ mỗi trụ bằng 4 đường sinh nên ống tròn trông như ống vuông. Tăng số đường sinh và độ mịn mặt.
            if (db.Isolines < 24) db.Isolines = 24;
            if (db.Facetres < 8) db.Facetres = 8;
        }

        public double ScaleToMm { get; }

        public BuildStats Build(BuildPlan plan, BuildOptions options)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var stats = new BuildStats();
            var built = 0;
            var placed = new List<(BuildItem item, Solid3d solid)>();
            foreach (var item in plan.Items)
            {
                if (options?.FailAfterItems.HasValue == true && built >= options.FailAfterItems.Value)
                {
                    throw new InvalidOperationException($"Lỗi tiêm thử sau {built} phần tử.");
                }

                Solid3d? solid = null;
                try
                {
                    switch (item)
                    {
                        case PipeItem p: solid = Pipe(p); stats.Pipes++; break;
                        case ElbowItem e: solid = Elbow(e); stats.Elbows++; break;
                        case TeeItem t: solid = Tee(t); stats.Tees++; break;
                        case ComponentItem c: solid = Component(c); stats.Components++; break;
                        case EquipmentItem q: solid = Equipment(q); stats.Equipment++; break;
                        case StructureItem st: solid = Structure(st); stats.Structures++; break;
                    }
                }
                catch (System.Exception ex) when (!(ex is InvalidOperationException))
                {
                    stats.Failures.Add($"{item.IrId}: {ex.Message}");
                    solid?.Dispose();
                    continue;
                }

                if (solid == null) continue;
                solid.LayerId = Layer(item.Layer, item.LineTag);
                _space.AppendEntity(solid);
                _tr.AddNewlyCreatedDBObject(solid, true);
                solid.XData = XData(item);
                placed.Add((item, solid));
                built++;
            }

            // Cổng và mối nối, S4-12: tính sau khi mọi khối đã dựng, ghi vào xdata và vẽ vòng đánh dấu.
            BuildPorts.Compute(plan);
            var connections = BuildPorts.Connect(plan);
            stats.Connections = connections.Count;
            stats.OpenPorts = BuildPorts.OpenPorts(plan);
            foreach (var (item, solid) in placed)
            {
                if (item.Ports.Count == 0) continue;
                solid.XData = XData(item);
                foreach (var port in item.Ports) PortRing(item, port);
            }

            stats.Layers = _layers.Count;
            return stats;
        }

        /// <summary>Vòng tròn tại cổng, đường kính bằng ống, vuông góc trục: xanh lá khi đã nối, đỏ khi hở, xdata ghi cổng và khối đối diện.</summary>
        private void PortRing(BuildItem item, Port port)
        {
            var layer = BuildPlan.LayerName("PA-{line}-{kind}", item.LineTag, "PORT", item.Dn);
            var ring = new Circle(P(port.At), V(port.Direction).GetNormal(), Math.Max(item.Od / 2 / ScaleToMm, 5));
            ring.LayerId = Layer(layer, item.LineTag);
            ring.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)(port.Connected ? 3 : 1));
            _space.AppendEntity(ring);
            _tr.AddNewlyCreatedDBObject(ring, true);
            ring.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, "port=" + item.IrId + ":" + port.Name),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, "mate=" + (port.Connected ? port.MateIrId + ":" + port.MatePort : "hở")),
                new TypedValue((int)DxfCode.ExtendedDataInteger32, port.Dn));
        }

        /// <summary>Ống thẳng: trụ theo đường kính ngoài, đặt theo trục đoạn ống, S3-11.</summary>
        public Solid3d Pipe(PipeItem p)
        {
            var start = P(p.Start);
            var end = P(p.End);
            var axis = end - start;
            var length = axis.Length;
            if (length <= 0) throw new ArgumentException("ống dài 0");
            var r = p.Od / 2 / ScaleToMm;
            var solid = new Solid3d();
            solid.CreateFrustum(length, r, r, r);
            solid.TransformBy(Align(start + axis * 0.5, axis.GetNormal()));
            return solid;
        }

        /// <summary>Co: quét tiết diện tròn theo cung tâm, bằng cách xoay tiết diện quanh trục qua tâm cung, S3-12.</summary>
        public Solid3d Elbow(ElbowItem e)
        {
            var tangentA = P(e.TangentA);
            var corner = P(e.Corner);
            var center = P(e.ArcCenter);
            var normal = V(e.Normal).GetNormal();
            var r = e.Od / 2 / ScaleToMm;
            var axisIn = (corner - tangentA).GetNormal();

            using (var circle = new Circle(tangentA, axisIn, r))
            using (var curves = new DBObjectCollection { circle })
            using (var regions = Region.CreateFromCurves(curves))
            {
                var region = (Region)regions[0];
                var angle = e.AngleDeg * Math.PI / 180.0;
                // Chọn chiều xoay đưa tiết diện từ điểm tiếp A tới điểm tiếp B.
                var rA = tangentA - center;
                var rB = P(e.TangentB) - center;
                var plus = rA.TransformBy(Matrix3d.Rotation(angle, normal, AcPoint3d.Origin));
                var minus = rA.TransformBy(Matrix3d.Rotation(-angle, normal, AcPoint3d.Origin));
                if ((minus - rB).Length < (plus - rB).Length) angle = -angle;

                var solid = new Solid3d();
                solid.CreateRevolvedSolid(region, center, normal, angle, 0, new RevolveOptions());
                region.Dispose();
                return solid;
            }
        }

        /// <summary>Tê: hợp hai hình trụ, S3-13.</summary>
        public Solid3d Tee(TeeItem t)
        {
            var center = P(t.Center);
            var runAxis = V(t.RunAxis).GetNormal();
            var branchDir = V(t.BranchDirection).GetNormal();
            var runLen = 2 * t.CenterToRun / ScaleToMm;
            var branchLen = t.CenterToBranch / ScaleToMm;
            var run = new Solid3d();
            run.CreateFrustum(runLen, t.Od / 2 / ScaleToMm, t.Od / 2 / ScaleToMm, t.Od / 2 / ScaleToMm);
            run.TransformBy(Align(center, runAxis));
            using (var branch = new Solid3d())
            {
                branch.CreateFrustum(branchLen, t.BranchOd / 2 / ScaleToMm, t.BranchOd / 2 / ScaleToMm, t.BranchOd / 2 / ScaleToMm);
                branch.TransformBy(Align(center + branchDir * (branchLen / 2), branchDir));
                run.BooleanOperation(BooleanOperationType.BoolUnite, branch);
            }

            return run;
        }

        /// <summary>
        /// Van và cấu kiện nối tiếp, S4-09 và I2: hình theo loại và kích thước danh mục trong <see cref="ComponentItem.Dims"/>.
        /// Mặt bích SO là đĩa có lỗ bu lông cộng cổ về phía ống, mặt bích nằm ở <c>Position + Axis·Length/2</c>; bích mù là đĩa.
        /// Van bướm wafer là thân tròn giữa hai mặt bích, cổ, hộp số và tay quay. Van mặt bích có thân, hai bích đầu, cổ và tay quay.
        /// Côn thu là nón cụt giữa hai đường kính ống. Olet là ống lót ngắn trên mặt ống chính theo hướng nhánh.
        /// </summary>
        public Solid3d Component(ComponentItem c)
        {
            var center = P(c.Position);
            var axis = V(c.Axis).GetNormal();
            var k = 1.0 / ScaleToMm;
            var length = c.Length * k;
            double Dim(string key, double fallback) => c.Dims.TryGetValue(key, out var v) && v > 0 ? v * k : fallback * k;
            var pipeR = c.Od / 2 * k;
            var up = Math.Abs(axis.Z) < 0.9 ? AcVector3d.ZAxis : AcVector3d.XAxis;

            switch (c.Type)
            {
                case "flange":
                    return Flange(c, center, axis, length, Dim, pipeR);
                case "valve_butterfly":
                    return ButterflyValve(center, axis, up, length, Dim, pipeR);
                case "reducer":
                {
                    var body = new Solid3d();
                    body.CreateFrustum(length, Dim("ODNEAR", c.Od) / 2, Dim("ODNEAR", c.Od) / 2, Dim("ODFAR", c.Od * 0.7) / 2);
                    body.TransformBy(Align(center, axis));
                    return body;
                }

                case "olet":
                case "nipple":
                case "instrument":
                {
                    var dir = c.BranchDirection.HasValue ? V(c.BranchDirection.Value).GetNormal() : up;
                    var bossR = Dim("BOSSOD", Math.Max(c.Od * 0.3, 30)) / 2;
                    var bossLen = Dim("BOSSLEN", Math.Max(20, c.Dn * 0.5));
                    var runR = Dim("RUNOD", c.Od) / 2;
                    // Bắt đầu hơi lún vào ống chính để không hở khe tại mặt cong.
                    var sink = Math.Min(bossR * 0.6, runR * 0.5);
                    var total = bossLen + sink;
                    var body = new Solid3d();
                    body.CreateFrustum(total, bossR, bossR, bossR);
                    body.TransformBy(Align(center + dir * (runR - sink + total / 2), dir));
                    return body;
                }

                default:
                    return FlangedValve(c, center, axis, up, length, Dim, pipeR);
            }
        }

        private Solid3d Flange(ComponentItem c, AcPoint3d center, AcVector3d axis, double length, Func<string, double, double> dim, double pipeR)
        {
            var d = dim("D", c.Od * 1.8);
            var t = Math.Min(dim("T", Math.Max(12, c.Od * 0.09)), length);
            var face = center + axis * (length / 2);
            var disc = new Solid3d();
            disc.CreateFrustum(t, d / 2, d / 2, d / 2);
            disc.TransformBy(Align(face - axis * (t / 2), axis));
            // Mặt nâng RF nhô 2 mm.
            var rf = dim("RF", d * 0.78);
            using (var raised = new Solid3d())
            {
                var h = 2.0 / ScaleToMm;
                raised.CreateFrustum(h, rf / 2, rf / 2, rf / 2);
                raised.TransformBy(Align(face + axis * (h / 2), axis));
                disc.BooleanOperation(BooleanOperationType.BoolUnite, raised);
            }

            BoltHoles(disc, face - axis * (t / 2), axis, dim("C", d * 0.85), (int)Math.Round(dim("N", 8) * ScaleToMm), dim("HOLE", d * 0.06), t);
            if (c.Subtype == "blind" || length <= t + 0.01) return disc;

            // Cổ mặt bích SO từ mặt sau đĩa tới đuôi, trùm lên ống.
            var hubOd = dim("HUBOD", c.Od * 1.2);
            using (var hub = new Solid3d())
            {
                var hubLen = length - t;
                hub.CreateFrustum(hubLen, hubOd / 2, hubOd / 2, Math.Max(pipeR * 1.02, hubOd / 2 * 0.85));
                // Đầu to của nón cụt nằm ở -Z của solid gốc; đặt đầu to sát đĩa, đầu nhỏ ở đuôi cổ.
                hub.TransformBy(Align(face - axis * (t + hubLen / 2), -axis));
                disc.BooleanOperation(BooleanOperationType.BoolUnite, hub);
            }

            return disc;
        }

        /// <summary>Khoét lỗ bu lông trên vòng tròn C quanh trục, lỗ xuyên hết chiều dày.</summary>
        private void BoltHoles(Solid3d disc, AcPoint3d discCenter, AcVector3d axis, double c, int n, double hole, double t)
        {
            if (n <= 0 || n > 48 || hole <= 0) return;
            var helper = Math.Abs(axis.Z) < 0.9 ? AcVector3d.ZAxis : AcVector3d.XAxis;
            var u = helper.CrossProduct(axis).GetNormal();
            var v = axis.CrossProduct(u).GetNormal();
            // Lỗ lệch nửa bước so với trục đứng, như quy ước đặt bu lông.
            var offset = Math.PI / n;
            for (var i = 0; i < n; i++)
            {
                var ang = offset + 2 * Math.PI * i / n;
                var p = discCenter + u * (c / 2 * Math.Cos(ang)) + v * (c / 2 * Math.Sin(ang));
                using (var cyl = new Solid3d())
                {
                    cyl.CreateFrustum(t * 1.5, hole / 2, hole / 2, hole / 2);
                    cyl.TransformBy(Align(p, axis));
                    disc.BooleanOperation(BooleanOperationType.BoolSubtract, cyl);
                }
            }
        }

        private Solid3d ButterflyValve(AcPoint3d center, AcVector3d axis, AcVector3d up, double length, Func<string, double, double> dim, double pipeR)
        {
            var d = dim("D", pipeR * 2 * 1.6);
            var bodyD = dim("C", d * 0.88) - dim("HOLE", d * 0.06) - 4.0 / ScaleToMm;
            var body = new Solid3d();
            body.CreateFrustum(length, bodyD / 2, bodyD / 2, bodyD / 2);
            body.TransformBy(Align(center, axis));
            // (axis, side, up) thuận tay phải để AlignCoordinateSystem nhận.
            var side = up.CrossProduct(axis).GetNormal();
            // Cổ van lên tới hộp số.
            var neckR = Math.Max(pipeR * 0.18, 12.0 / ScaleToMm);
            var neckLen = pipeR * 0.9;
            Unite(body, Cylinder(center + up * (bodyD / 2 * 0.8 + neckLen / 2), up, neckR, neckLen));
            // Hộp số: hộp đặt trên cổ, dài theo trục ống.
            var boxL = Math.Max(pipeR * 0.9, 60.0 / ScaleToMm);
            var boxW = boxL * 0.6;
            var boxH = boxL * 0.55;
            var boxCenter = center + up * (bodyD / 2 * 0.8 + neckLen + boxH / 2);
            Unite(body, Box(boxCenter, axis, side, up, boxL, boxW, boxH));
            // Tay quay: đĩa mỏng vuông góc với trục ngang của hộp số.
            var wheelD = Math.Max(pipeR * 1.6, 150.0 / ScaleToMm);
            var wheelT = Math.Max(wheelD * 0.05, 6.0 / ScaleToMm);
            var wheelAxisLen = boxW / 2 + 25.0 / ScaleToMm;
            Unite(body, Cylinder(boxCenter + side * (wheelAxisLen / 2), side, neckR * 0.5, wheelAxisLen));
            Unite(body, Ring(boxCenter + side * wheelAxisLen, side, wheelD / 2, wheelD / 2 - wheelT * 1.5, wheelT));
            return body;
        }

        private Solid3d FlangedValve(ComponentItem c, AcPoint3d center, AcVector3d axis, AcVector3d up, double length, Func<string, double, double> dim, double pipeR)
        {
            var d = dim("D", pipeR * 2 * 1.8);
            var t = Math.Min(dim("T", pipeR * 0.2), length / 3);
            var bodyR = pipeR * (c.Type == "valve_globe" || c.Type == "valve_check" ? 1.35 : 1.2);
            var body = new Solid3d();
            if (c.Type == "valve_globe")
            {
                // Van cầu: thân cầu giữa hai đoạn trụ.
                body.CreateSphere(Math.Min(bodyR * 1.25, length / 2 - t));
                body.TransformBy(Matrix3d.Displacement(center - AcPoint3d.Origin));
                Unite(body, Cylinder(center, axis, pipeR * 1.1, length - 2 * t));
            }
            else
            {
                body.CreateFrustum(length - 2 * t, bodyR, bodyR, bodyR);
                body.TransformBy(Align(center, axis));
            }

            // Hai bích đầu.
            Unite(body, Cylinder(center - axis * ((length - t) / 2), axis, d / 2, t));
            Unite(body, Cylinder(center + axis * ((length - t) / 2), axis, d / 2, t));
            // Cổ, trục và tay quay nằm ngang.
            var bonnetR = bodyR * 0.7;
            var bonnetLen = bodyR * 1.2;
            Unite(body, Cylinder(center + up * (bodyR * 0.6 + bonnetLen / 2), up, bonnetR, bonnetLen));
            var stemLen = bodyR * 1.6;
            var stemTop = center + up * (bodyR * 0.6 + bonnetLen + stemLen);
            Unite(body, Cylinder(center + up * (bodyR * 0.6 + bonnetLen + stemLen / 2), up, Math.Max(bodyR * 0.12, 5.0 / ScaleToMm), stemLen));
            var wheelD = Math.Max(bodyR * 2.6, 120.0 / ScaleToMm);
            var wheelT = Math.Max(wheelD * 0.05, 6.0 / ScaleToMm);
            Unite(body, Ring(stemTop, up, wheelD / 2, wheelD / 2 - wheelT * 1.5, wheelT));
            var side = up.CrossProduct(axis).GetNormal();
            Unite(body, Box(stemTop, axis, side, up, wheelD * 0.95, wheelT, wheelT * 0.8));
            Unite(body, Box(stemTop, axis, side, up, wheelT, wheelD * 0.95, wheelT * 0.8));
            return body;
        }

        private static void Unite(Solid3d target, Solid3d part)
        {
            using (part)
            {
                target.BooleanOperation(BooleanOperationType.BoolUnite, part);
            }
        }

        private static Solid3d Cylinder(AcPoint3d center, AcVector3d axis, double r, double len)
        {
            var s = new Solid3d();
            s.CreateFrustum(Math.Max(len, 0.01), r, r, r);
            s.TransformBy(Align(center, axis));
            return s;
        }

        private static Solid3d Ring(AcPoint3d center, AcVector3d axis, double rOuter, double rInner, double thickness)
        {
            var s = Cylinder(center, axis, rOuter, thickness);
            if (rInner > 0 && rInner < rOuter)
            {
                using (var hole = Cylinder(center, axis, rInner, thickness * 1.5))
                {
                    s.BooleanOperation(BooleanOperationType.BoolSubtract, hole);
                }
            }

            return s;
        }

        private static Solid3d Box(AcPoint3d center, AcVector3d x, AcVector3d y, AcVector3d z, double lx, double ly, double lz)
        {
            var s = new Solid3d();
            s.CreateBox(Math.Max(lx, 0.01), Math.Max(ly, 0.01), Math.Max(lz, 0.01));
            s.TransformBy(Matrix3d.AlignCoordinateSystem(AcPoint3d.Origin, AcVector3d.XAxis, AcVector3d.YAxis, AcVector3d.ZAxis, center, x.GetNormal(), y.GetNormal(), z.GetNormal()));
            return s;
        }

        /// <summary>Thiết bị từ khối cơ bản, S4-10. Hệ trục cục bộ: gốc tại tâm hình bao, X theo cạnh dài, Z hướng lên.</summary>
        public Solid3d Equipment(EquipmentItem e)
        {
            if (e.Primitives.Count == 0) throw new ArgumentException("thiết bị không có khối nào");
            var local = Matrix3d.Rotation(e.RotationDeg * Math.PI / 180.0, AcVector3d.ZAxis, AcPoint3d.Origin)
                .PreMultiplyBy(Matrix3d.Displacement(P(e.Origin) - AcPoint3d.Origin));
            Solid3d? result = null;
            foreach (var prim in e.Primitives)
            {
                var solid = Primitive(prim);
                solid.TransformBy(local);
                if (result == null)
                {
                    result = solid;
                }
                else
                {
                    result.BooleanOperation(BooleanOperationType.BoolUnite, solid);
                    solid.Dispose();
                }
            }

            return result!;
        }

        private Solid3d Primitive(PrimitiveItem p)
        {
            var k = 1.0 / ScaleToMm;
            var center = new AcPoint3d(p.Center[0] * k, p.Center[1] * k, p.Center[2] * k);
            var axis = p.Axis == "x" ? AcVector3d.XAxis : p.Axis == "y" ? AcVector3d.YAxis : AcVector3d.ZAxis;
            var solid = new Solid3d();
            switch (p.Shape)
            {
                case "box":
                    solid.CreateBox(Math.Max(p.Size[0], 1) * k, Math.Max(p.Size[1], 1) * k, Math.Max(p.Size[2], 1) * k);
                    solid.TransformBy(Matrix3d.Displacement(center - AcPoint3d.Origin));
                    return solid;
                case "sphere":
                    solid.CreateSphere(p.Diameter / 2 * k);
                    solid.TransformBy(Matrix3d.Displacement(center - AcPoint3d.Origin));
                    return solid;
                case "cone":
                    solid.CreateFrustum(p.Length * k, p.Diameter / 2 * k, p.Diameter / 2 * k, Math.Max(p.TopDiameter, 0) / 2 * k);
                    solid.TransformBy(Align(center, axis));
                    return solid;
                case "dished_head":
                    // Xấp xỉ đáy cong bằng nón cụt đầu nhỏ bằng nửa đường kính, đủ cho định tuyến và va chạm.
                    solid.CreateFrustum(p.Length * k, p.Diameter / 2 * k, p.Diameter / 2 * k, p.Diameter / 4 * k);
                    solid.TransformBy(Align(center, axis));
                    return solid;
                default:
                    solid.CreateFrustum(Math.Max(p.Length, 1) * k, p.Diameter / 2 * k, p.Diameter / 2 * k, p.Diameter / 2 * k);
                    solid.TransformBy(Align(center, axis));
                    return solid;
            }
        }

        /// <summary>Cột và dầm dạng hộp, S4-11. Dầm có mặt trên tại cao độ mặt trên kết cấu.</summary>
        public Solid3d Structure(StructureItem s)
        {
            var a = P(s.Start);
            var b = P(s.End);
            var axis = b - a;
            var length = axis.Length;
            if (length <= 0) throw new ArgumentException("kết cấu dài 0");
            var size = s.SectionSize / ScaleToMm;
            var solid = new Solid3d();
            if (s.Type == "column")
            {
                solid.CreateBox(size, size, length);
                solid.TransformBy(Matrix3d.Displacement((a + axis * 0.5) - AcPoint3d.Origin));
            }
            else
            {
                solid.CreateBox(length, size, size);
                var dir = axis.GetNormal();
                var x = dir;
                var z = AcVector3d.ZAxis;
                var y = z.CrossProduct(x).GetNormal();
                var mid = a + axis * 0.5 - z * (size / 2);
                solid.TransformBy(Matrix3d.AlignCoordinateSystem(AcPoint3d.Origin, AcVector3d.XAxis, AcVector3d.YAxis, AcVector3d.ZAxis, mid, x, y, z));
            }

            return solid;
        }

        /// <summary>Layer theo tuyến và loại, màu theo tuyến, S3-14.</summary>
        public ObjectId Layer(string name, string lineTag)
        {
            if (_layers.TryGetValue(name, out var id)) return id;
            var lt = (LayerTable)_tr.GetObject(_db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name))
            {
                id = lt[name];
            }
            else
            {
                lt.UpgradeOpen();
                var ltr = new LayerTableRecord { Name = name, Color = Color.FromColorIndex(ColorMethod.ByAci, ColorFor(lineTag, name)) };
                id = lt.Add(ltr);
                _tr.AddNewlyCreatedDBObject(ltr, true);
            }

            _layers[name] = id;
            return id;
        }

        /// <summary>Dữ liệu gắn vào solid, S3-15: tuyến, DN, loại, mã IR, handle nguồn 2D.</summary>
        public static ResultBuffer XData(BuildItem item) => new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, "line=" + item.LineTag),
            new TypedValue((int)DxfCode.ExtendedDataInteger32, item.Dn),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, "kind=" + item.Kind),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, "ir=" + item.IrId),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, "src=" + string.Join(",", item.SourceHandles)),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, "ports=" + BuildPorts.Describe(item)));

        /// <summary>Đọc lại năm trường từ xdata của một đối tượng đã dựng. Null nếu không phải của Piping Agent.</summary>
        public static Dictionary<string, string>? ReadXData(DBObject obj)
        {
            using (var rb = obj.GetXDataForApplication(AppName))
            {
                if (rb == null) return null;
                var d = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var tv in rb.AsArray())
                {
                    if (tv.TypeCode == (int)DxfCode.ExtendedDataInteger32) d["dn"] = Convert.ToString(tv.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                    else if (tv.TypeCode == (int)DxfCode.ExtendedDataAsciiString && tv.Value is string s)
                    {
                        var eq = s.IndexOf('=');
                        if (eq > 0) d[s.Substring(0, eq)] = s.Substring(eq + 1);
                    }
                }

                return d;
            }
        }

        private void EnsureRegApp()
        {
            var table = (RegAppTable)_tr.GetObject(_db.RegAppTableId, OpenMode.ForRead);
            if (table.Has(AppName)) return;
            table.UpgradeOpen();
            var rec = new RegAppTableRecord { Name = AppName };
            table.Add(rec);
            _tr.AddNewlyCreatedDBObject(rec, true);
        }

        /// <summary>Màu layer: ống, co, tê theo tuyến; mặt bích, van, phụ kiện nối mỗi loại một màu để khi tô bóng phân biệt được ngay.</summary>
        private static short ColorFor(string lineTag, string layerName)
        {
            var kind = layerName.Substring(layerName.LastIndexOf('-') + 1).ToUpperInvariant();
            switch (kind)
            {
                case "FLANGE": return 4;
                case "VALVE": return 1;
                case "FITTING": return 2;
                case "EQUIPMENT": return 8;
                case "STRUCTURE": return 252;
                default: return ColorFor(lineTag);
            }
        }

        private static short ColorFor(string lineTag)
        {
            if (string.IsNullOrEmpty(lineTag)) return 8;
            var hash = 0;
            foreach (var c in lineTag) hash = unchecked(hash * 31 + c);
            var palette = new short[] { 1, 2, 3, 4, 5, 6, 30, 40, 50, 90, 130, 170, 210 };
            return palette[Math.Abs(hash) % palette.Length];
        }

        /// <summary>Ma trận đưa trục Z của solid gốc về <paramref name="axis"/> và gốc về <paramref name="origin"/>.</summary>
        private static Matrix3d Align(AcPoint3d origin, AcVector3d axis)
        {
            var z = axis.GetNormal();
            var helper = Math.Abs(z.Z) < 0.9 ? AcVector3d.ZAxis : AcVector3d.XAxis;
            var x = helper.CrossProduct(z).GetNormal();
            var y = z.CrossProduct(x).GetNormal();
            return Matrix3d.AlignCoordinateSystem(AcPoint3d.Origin, AcVector3d.XAxis, AcVector3d.YAxis, AcVector3d.ZAxis, origin, x, y, z);
        }

        private AcPoint3d P(Core.Geometry.Point3 p) => new AcPoint3d(p.X / ScaleToMm, p.Y / ScaleToMm, p.Z / ScaleToMm);

        private static AcVector3d V(Core.Geometry.Vector3 v) => new AcVector3d(v.X, v.Y, v.Z);
    }
}
