using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;

namespace PipingAgent.Core.Build
{
    /// <summary>Tuỳ chọn dựng, FR-BLD. Mẫu tên layer có các chỗ giữ <c>{line}</c>, <c>{kind}</c>, <c>{dn}</c>.</summary>
    public sealed class BuildOptions
    {
        public string LayerPattern { get; set; } = "PA-{line}-{kind}";

        /// <summary>Chỉ để kiểm thử nhóm hoàn tác: ném lỗi sau khi dựng chừng này phần tử.</summary>
        public int? FailAfterItems { get; set; }
    }

    public abstract class BuildItem
    {
        public string IrId { get; set; } = string.Empty;

        public string LineTag { get; set; } = string.Empty;

        public int Dn { get; set; }

        public double Od { get; set; }

        public string Layer { get; set; } = string.Empty;

        public List<string> SourceHandles { get; set; } = new List<string>();

        /// <summary>Cổng nối của khối, tính bởi <see cref="BuildPorts.Compute"/>; mối nối ghi vào <see cref="Port.MateIrId"/>.</summary>
        public List<Port> Ports { get; } = new List<Port>();

        public abstract string Kind { get; }
    }

    /// <summary>
    /// Cổng nối của một khối, như port trong S3D hay Plant 3D: điểm trên trục, hướng ra ngoài khối, DN. Hai cổng trùng điểm và
    /// ngược hướng là một mối nối. Cổng <c>host</c> là chỗ cấu kiện hàn lên thân ống chính, như olet, nối với ống đi qua điểm đó.
    /// </summary>
    public sealed class Port
    {
        public string Name { get; set; } = string.Empty;

        public Point3 At { get; set; }

        public Vector3 Direction { get; set; }

        public int Dn { get; set; }

        /// <summary><c>end</c> mặt nối thường; <c>host</c> hàn lên thân ống.</summary>
        public string Kind { get; set; } = "end";

        public string? MateIrId { get; set; }

        public string? MatePort { get; set; }

        public bool Connected => MateIrId != null;
    }

    /// <summary>Một mối nối giữa hai cổng, để ghi vào bản vẽ và báo cáo.</summary>
    public sealed class Connection
    {
        public string AIrId { get; set; } = string.Empty;

        public string APort { get; set; } = string.Empty;

        public string BIrId { get; set; } = string.Empty;

        public string BPort { get; set; } = string.Empty;

        public Point3 At { get; set; }

        public double GapMm { get; set; }
    }

    /// <summary>Ống thẳng: hình trụ từ <see cref="Start"/> đến <see cref="End"/>, đã trừ phần phụ kiện chiếm chỗ.</summary>
    public sealed class PipeItem : BuildItem
    {
        public Point3 Start { get; set; }

        public Point3 End { get; set; }

        public double Length => Start.DistanceTo(End);

        public override string Kind => "PIPE";
    }

    /// <summary>Co: quét tiết diện tròn theo cung tâm từ <see cref="TangentA"/> tới <see cref="TangentB"/> quanh <see cref="ArcCenter"/>.</summary>
    public sealed class ElbowItem : BuildItem
    {
        public Point3 Corner { get; set; }

        public Point3 TangentA { get; set; }

        public Point3 TangentB { get; set; }

        public Point3 ArcCenter { get; set; }

        public double Radius { get; set; }

        public double AngleDeg { get; set; }

        /// <summary>Pháp tuyến mặt phẳng uốn.</summary>
        public Vector3 Normal { get; set; }

        public override string Kind => "ELBOW";
    }

    /// <summary>Tê: trụ chạy dài 2C theo trục chạy, hợp với trụ nhánh dài M từ tâm theo hướng nhánh.</summary>
    public sealed class TeeItem : BuildItem
    {
        public Point3 Center { get; set; }

        public Vector3 RunAxis { get; set; }

        public double CenterToRun { get; set; }

        public Vector3 BranchDirection { get; set; }

        public double CenterToBranch { get; set; }

        public double BranchOd { get; set; }

        public override string Kind => "TEE";
    }

    /// <summary>Kế hoạch dựng: danh sách phần tử hình học thuần, độc lập CAD. Xem trước số lượng mà không đổi bản vẽ, S3-10.</summary>
    public sealed class BuildPlan
    {
        public List<PipeItem> Pipes { get; } = new List<PipeItem>();

        public List<ElbowItem> Elbows { get; } = new List<ElbowItem>();

        public List<TeeItem> Tees { get; } = new List<TeeItem>();

        public List<ComponentItem> Components { get; } = new List<ComponentItem>();

        public List<EquipmentItem> EquipmentItems { get; } = new List<EquipmentItem>();

        public List<StructureItem> Structures { get; } = new List<StructureItem>();

        /// <summary>Phần tử IR bị bỏ qua và lý do.</summary>
        public List<string> Skipped { get; } = new List<string>();

        public IEnumerable<BuildItem> Items => Pipes.Cast<BuildItem>().Concat(Elbows).Concat(Tees).Concat(Components).Concat(EquipmentItems).Concat(Structures);

        public int Count => Pipes.Count + Elbows.Count + Tees.Count + Components.Count + EquipmentItems.Count + Structures.Count;

        public IEnumerable<string> Layers => Items.Select(i => i.Layer).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(l => l, StringComparer.OrdinalIgnoreCase);

        public double TotalPipeLength => Pipes.Sum(p => p.Length);

        public string Summary() => string.Format(CultureInfo.InvariantCulture,
            "{0} ống dài {1:0.0} m, {2} co, {3} tê, {4} cấu kiện, {5} thiết bị, {6} kết cấu, {7} layer, bỏ qua {8}",
            Pipes.Count, TotalPipeLength / 1000.0, Elbows.Count, Tees.Count, Components.Count, EquipmentItems.Count, Structures.Count, Layers.Count(), Skipped.Count);

        public static string LayerName(string pattern, string lineTag, string kind, int dn)
        {
            var safeLine = string.IsNullOrEmpty(lineTag) ? "UNTAGGED" : Sanitize(lineTag);
            return pattern.Replace("{line}", safeLine).Replace("{kind}", kind).Replace("{dn}", dn.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Tên layer AutoCAD không được chứa các ký tự <c>&lt;&gt;/\":;?*|,=`</c>.</summary>
        public static string Sanitize(string name)
        {
            var bad = "<>/\\\":;?*|,=`".ToCharArray();
            var chars = name.Trim().Select(c => bad.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray();
            var s = new string(chars);
            return s.Length == 0 ? "UNNAMED" : s;
        }

        /// <summary>Lập kế hoạch từ IR. Không đổi IR. Phần tử thiếu dữ liệu được ghi vào <see cref="Skipped"/>.</summary>
        public static BuildPlan FromModel(PipingModel model, BuildOptions options)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (options == null) throw new ArgumentNullException(nameof(options));
            var plan = new BuildPlan();
            var lines = model.Lines.ToDictionary(l => l.Id, l => l.Tag, StringComparer.Ordinal);
            var nodes = model.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            var segments = model.Segments.ToDictionary(s => s.Id, StringComparer.Ordinal);
            string Tag(string lineId) => lines.TryGetValue(lineId, out var t) ? t : string.Empty;

            foreach (var seg in model.Segments)
            {
                if (!nodes.TryGetValue(seg.NodeA, out var a) || !nodes.TryGetValue(seg.NodeB, out var b))
                {
                    plan.Skipped.Add($"{seg.Id}: thiếu nút");
                    continue;
                }

                if (seg.Od <= 0)
                {
                    plan.Skipped.Add($"{seg.Id}: không có đường kính ngoài (DN{seg.Dn})");
                    continue;
                }

                var axis = (b.Position - a.Position);
                var length = axis.Length;
                if (length <= 0)
                {
                    plan.Skipped.Add($"{seg.Id}: dài 0");
                    continue;
                }

                var dir = axis.Normalized();
                var start = a.Position + dir * seg.TrimA;
                var end = b.Position - dir * seg.TrimB;
                if (length - seg.TrimA - seg.TrimB <= 0.01)
                {
                    plan.Skipped.Add($"{seg.Id}: bị phụ kiện chiếm hết chiều dài");
                    continue;
                }

                var tag = Tag(seg.LineId);
                plan.Pipes.Add(new PipeItem
                {
                    IrId = seg.Id,
                    LineTag = tag,
                    Dn = seg.Dn,
                    Od = seg.Od,
                    Layer = LayerName(options.LayerPattern, tag, "PIPE", seg.Dn),
                    Start = start,
                    End = end,
                    SourceHandles = seg.Prov?.Sources.ToList() ?? new List<string>(),
                });
            }

            foreach (var node in model.Nodes)
            {
                var segs = node.Segments.Where(segments.ContainsKey).Select(id => segments[id]).ToList();
                try
                {
                    PlanNode(plan, node, segs, nodes, options, Tag);
                }
                catch (System.Exception ex)
                {
                    plan.Skipped.Add($"{node.Id}: {ex.Message}");
                }
            }

            return plan;
        }

        private static void PlanNode(BuildPlan plan, Node node, List<Segment> segs, Dictionary<string, Node> nodes, BuildOptions options, Func<string, string> Tag)
        {
            {
                switch (node.Kind)
                {
                    case NodeKind.Elbow when segs.Count == 2 && node.Params != null && node.Params.TryGetValue("radius", out var radius):
                        {
                            var s1 = segs[0];
                            var s2 = segs[1];
                            var od = Math.Max(s1.Od, s2.Od);
                            if (od <= 0)
                            {
                                plan.Skipped.Add($"{node.Id}: không có đường kính ngoài");
                                break;
                            }

                            var d1 = Direction(node, s1, nodes);
                            var d2 = Direction(node, s2, nodes);
                            var bend = node.Params.TryGetValue("angle", out var ang) ? ang : 90;
                            var tangent = radius * Math.Tan(bend * Math.PI / 360.0);
                            var bisector = (d1 + d2);
                            if (bisector.Length < 1e-9)
                            {
                                plan.Skipped.Add($"{node.Id}: hai đoạn thẳng hàng, không phải co");
                                break;
                            }

                            var half = (180 - bend) / 2.0 * Math.PI / 180.0;
                            var center = node.Position + bisector.Normalized() * (radius / Math.Sin(half));
                            var tag = Tag(s1.LineId);
                            plan.Elbows.Add(new ElbowItem
                            {
                                IrId = node.Id,
                                LineTag = tag,
                                Dn = Math.Max(s1.Dn, s2.Dn),
                                Od = od,
                                Layer = LayerName(options.LayerPattern, tag, "ELBOW", Math.Max(s1.Dn, s2.Dn)),
                                Corner = node.Position,
                                TangentA = node.Position + d1 * tangent,
                                TangentB = node.Position + d2 * tangent,
                                ArcCenter = center,
                                Radius = radius,
                                AngleDeg = bend,
                                Normal = d1.Cross(d2).Normalized(),
                                SourceHandles = node.Prov?.Sources.ToList() ?? new List<string>(),
                            });
                            break;
                        }

                    case NodeKind.Tee when segs.Count == 3 && node.Params != null && node.Params.TryGetValue("centerToRun", out var c) && node.Params.TryGetValue("centerToBranch", out var m):
                        {
                            // Nhánh là đoạn mà hai đoạn kia thẳng hàng với nhau.
                            var dirs = segs.Select(s => Direction(node, s, nodes)).ToList();
                            var branchIndex = 0;
                            var best = -2.0;
                            for (var i = 0; i < 3; i++)
                            {
                                var straight = -dirs[(i + 1) % 3].Dot(dirs[(i + 2) % 3]);
                                if (straight > best)
                                {
                                    best = straight;
                                    branchIndex = i;
                                }
                            }

                            var runSegs = segs.Where((_, i) => i != branchIndex).ToList();
                            var od = runSegs.Max(s => s.Od);
                            if (od <= 0)
                            {
                                plan.Skipped.Add($"{node.Id}: không có đường kính ngoài");
                                break;
                            }

                            var tag = Tag(runSegs[0].LineId);
                            var dn = runSegs.Max(s => s.Dn);
                            plan.Tees.Add(new TeeItem
                            {
                                IrId = node.Id,
                                LineTag = tag,
                                Dn = dn,
                                Od = od,
                                Layer = LayerName(options.LayerPattern, tag, "TEE", dn),
                                Center = node.Position,
                                RunAxis = dirs[(branchIndex + 1) % 3],
                                CenterToRun = c,
                                BranchDirection = dirs[branchIndex],
                                CenterToBranch = m,
                                BranchOd = segs[branchIndex].Od > 0 ? segs[branchIndex].Od : od,
                                SourceHandles = node.Prov?.Sources.ToList() ?? new List<string>(),
                            });
                            break;
                        }

                    case NodeKind.Tee when node.Params != null && node.Params.ContainsKey("olet"):
                        break; // nhánh qua olet hoặc half coupling, cấu kiện riêng đã dựng, ống chính liền

                    case NodeKind.Elbow:
                    case NodeKind.Tee:
                        plan.Skipped.Add($"{node.Id}: {node.Kind} thiếu kích thước danh mục");
                        break;
                }
            }
        }

        private static Vector3 Direction(Node node, Segment seg, Dictionary<string, Node> nodes)
        {
            var other = nodes[seg.NodeA == node.Id ? seg.NodeB : seg.NodeA];
            return (other.Position - node.Position).Normalized();
        }
    }
}
