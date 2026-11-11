using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PipingAgent.Core.Geometry;

namespace PipingAgent.Core.Build
{
    /// <summary>
    /// Cổng nối và mối nối giữa các khối của kế hoạch dựng, S4-12. AutoCAD thuần không có khái niệm nối như S3D hay Plant 3D,
    /// nên Piping Agent tự tính: mỗi khối có cổng ở mặt nối, hai cổng trùng điểm trong dung sai và ngược hướng là một mối nối.
    /// Kết quả ghi vào xdata của khối và vẽ vòng đánh dấu tại cổng, xanh là đã nối, đỏ là hở, để người xem thấy ngay chỗ chưa khớp.
    /// </summary>
    public static class BuildPorts
    {
        /// <summary>Dung sai khớp cổng, mm. Đủ cho gioăng 4,5 mm nằm trong chiều dài van wafer và sai số làm tròn.</summary>
        public const double DefaultTolerance = 6.0;

        public static void Compute(BuildPlan plan)
        {
            foreach (var item in plan.Items)
            {
                item.Ports.Clear();
                switch (item)
                {
                    case PipeItem p:
                    {
                        var d = p.End - p.Start;
                        if (d.Length <= 0) break;
                        var n = d.Normalized();
                        item.Ports.Add(new Port { Name = "A", At = p.Start, Direction = -n, Dn = p.Dn });
                        item.Ports.Add(new Port { Name = "B", At = p.End, Direction = n, Dn = p.Dn });
                        break;
                    }

                    case ElbowItem e:
                        item.Ports.Add(new Port { Name = "A", At = e.TangentA, Direction = Dir(e.TangentA - e.Corner), Dn = e.Dn });
                        item.Ports.Add(new Port { Name = "B", At = e.TangentB, Direction = Dir(e.TangentB - e.Corner), Dn = e.Dn });
                        break;

                    case TeeItem t:
                    {
                        var run = Dir(t.RunAxis);
                        var br = Dir(t.BranchDirection);
                        item.Ports.Add(new Port { Name = "A", At = t.Center + run * t.CenterToRun, Direction = run, Dn = t.Dn });
                        item.Ports.Add(new Port { Name = "B", At = t.Center - run * t.CenterToRun, Direction = -run, Dn = t.Dn });
                        item.Ports.Add(new Port { Name = "BRANCH", At = t.Center + br * t.CenterToBranch, Direction = br, Dn = t.Dn });
                        break;
                    }

                    case ComponentItem c:
                        ComponentPorts(c);
                        break;
                }
            }
        }

        private static void ComponentPorts(ComponentItem c)
        {
            var axis = Dir(c.Axis);
            var half = c.Length / 2;
            switch (c.Type)
            {
                case "flange" when c.Subtype == "blind":
                    // Bích mù: Axis chỉ về mặt bích, tức về phía ống; chỉ có một cổng tại mặt.
                    c.Ports.Add(new Port { Name = "FACE", At = c.Position + axis * half, Direction = axis, Dn = c.Dn });
                    break;

                case "flange":
                    // Bích SO lồng lên đầu ống: mặt bích và đầu ống cùng một điểm, nên cổng ống đặt tại mặt, hướng ngược.
                    c.Ports.Add(new Port { Name = "FACE", At = c.Position + axis * half, Direction = axis, Dn = c.Dn });
                    c.Ports.Add(new Port { Name = "PIPE", At = c.Position + axis * half, Direction = -axis, Dn = c.Dn, Kind = c.OverlapsPipe ? "slip" : "end" });
                    break;

                case "olet":
                case "nipple":
                case "instrument":
                {
                    var br = c.BranchDirection.HasValue ? Dir(c.BranchDirection.Value) : Vector3.UnitZ;
                    // Ống nhánh bắt đầu từ tâm ống chính, nên cổng nhánh đặt tại tâm; cổng host là chỗ hàn lên thân ống chính.
                    c.Ports.Add(new Port { Name = "BRANCH", At = c.Position, Direction = br, Dn = c.Dn });
                    c.Ports.Add(new Port { Name = "HOST", At = c.Position, Direction = -br, Dn = c.Dn, Kind = "host" });
                    break;
                }

                default:
                    c.Ports.Add(new Port { Name = "A", At = c.Position - axis * half, Direction = -axis, Dn = c.Dn });
                    c.Ports.Add(new Port { Name = "B", At = c.Position + axis * half, Direction = axis, Dn = c.Dn });
                    break;
            }
        }

        /// <summary>Ghép cổng thành mối nối. Trả về danh sách mối nối; cổng không ghép được giữ <see cref="Port.MateIrId"/> null.</summary>
        public static List<Connection> Connect(BuildPlan plan, double tolerance = DefaultTolerance)
        {
            var result = new List<Connection>();
            var all = plan.Items.SelectMany(i => i.Ports.Select(p => (item: i, port: p))).ToList();
            foreach (var (item, port) in all) { port.MateIrId = null; port.MatePort = null; }

            // Tại mặt bích SO có ba cổng trùng điểm: đầu ống, cổng ống của bích và mặt bích. Ghép cổng ống của bích với đầu ống trước,
            // để mặt bích còn lại cho van hoặc bích đối diện; sau đó ghép phần còn lại theo khoảng cách gần nhất.
            Pair(all, tolerance, result, (ia, pa, ib, pb) => pa.Kind == "slip" && ib is PipeItem);
            Pair(all, tolerance, result, (ia, pa, ib, pb) => true);

            // Cổng host: hàn lên ống chính có trục đi qua điểm đó.
            var pipes = plan.Items.OfType<PipeItem>().ToList();
            foreach (var (item, port) in all.Where(x => x.port.Kind == "host"))
            {
                var hit = pipes
                    .Select(p => (p, d: DistanceToSegment(port.At, p.Start, p.End)))
                    .Where(x => x.d <= Math.Max(tolerance, x.p.Od / 2))
                    .OrderBy(x => x.d)
                    .FirstOrDefault();
                if (hit.p == null) continue;
                port.MateIrId = hit.p.IrId;
                port.MatePort = "BODY";
                result.Add(new Connection { AIrId = item.IrId, APort = port.Name, BIrId = hit.p.IrId, BPort = "BODY", At = port.At, GapMm = hit.d });
            }

            return result;
        }

        private static void Pair(List<(BuildItem item, Port port)> all, double tolerance, List<Connection> result, Func<BuildItem, Port, BuildItem, Port, bool> allow)
        {
            for (var i = 0; i < all.Count; i++)
            {
                var (ia, pa) = all[i];
                if (pa.Connected || pa.Kind == "host") continue;
                (BuildItem item, Port port)? best = null;
                var bestGap = double.MaxValue;
                for (var j = 0; j < all.Count; j++)
                {
                    if (i == j) continue;
                    var (ib, pb) = all[j];
                    if (pb.Connected || pb.Kind == "host" || ReferenceEquals(ia, ib)) continue;
                    if (!allow(ia, pa, ib, pb) && !allow(ib, pb, ia, pa)) continue;
                    var gap = pa.At.DistanceTo(pb.At);
                    if (gap > tolerance) continue;
                    if (pa.Direction.Dot(pb.Direction) > -0.9) continue;
                    if (gap < bestGap) { bestGap = gap; best = (ib, pb); }
                }

                if (best == null) continue;
                pa.MateIrId = best.Value.item.IrId;
                pa.MatePort = best.Value.port.Name;
                best.Value.port.MateIrId = ia.IrId;
                best.Value.port.MatePort = pa.Name;
                result.Add(new Connection { AIrId = ia.IrId, APort = pa.Name, BIrId = best.Value.item.IrId, BPort = best.Value.port.Name, At = pa.At, GapMm = bestGap });
            }
        }

        public static int OpenPorts(BuildPlan plan) => plan.Items.Sum(i => i.Ports.Count(p => !p.Connected));

        /// <summary>Chuỗi ghi vào xdata: <c>TÊN@x,y,z&gt;mã:cổng</c> nối bằng <c>|</c>; cổng hở không có phần sau <c>&gt;</c>.</summary>
        public static string Describe(BuildItem item) => string.Join("|", item.Ports.Select(p =>
            string.Format(CultureInfo.InvariantCulture, "{0}@{1:0.#},{2:0.#},{3:0.#}{4}", p.Name, p.At.X, p.At.Y, p.At.Z,
                p.Connected ? ">" + p.MateIrId + ":" + p.MatePort : string.Empty)));

        private static Vector3 Dir(Vector3 v) => v.Length > 0 ? v.Normalized() : Vector3.UnitX;

        internal static double DistanceToSegment(Point3 p, Point3 a, Point3 b)
        {
            var ab = b - a;
            var len2 = ab.Dot(ab);
            if (len2 <= 0) return p.DistanceTo(a);
            var t = Math.Max(0, Math.Min(1, (p - a).Dot(ab) / len2));
            return p.DistanceTo(a + ab * t);
        }
    }
}
