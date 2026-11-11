using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PipingAgent.Core.Equipment;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;

namespace PipingAgent.Core.Build
{
    /// <summary>Cấu kiện nối tiếp: khối theo chiều dài danh mục đặt trên trục ống, S4-09.</summary>
    public sealed class ComponentItem : BuildItem
    {
        public Point3 Position { get; set; }

        public Vector3 Axis { get; set; }

        public double Length { get; set; }

        public string Type { get; set; } = string.Empty;

        public string? Tag { get; set; }

        /// <summary>Kiểu con để chọn hình: mặt bích <c>so</c> hoặc <c>blind</c>, van bướm <c>wafer</c>, van khác <c>flanged</c>.</summary>
        public string Subtype { get; set; } = string.Empty;

        /// <summary>
        /// Kích thước dựng hình, mm, từ <see cref="Catalog.ComponentDims"/>: mặt bích <c>D</c>, <c>C</c>, <c>N</c>, <c>HOLE</c>, <c>T</c>, <c>HUBOD</c>, <c>RF</c>;
        /// côn thu <c>ODNEAR</c> phía ống đang nối và <c>ODFAR</c>; olet <c>BOSSOD</c>, <c>BOSSLEN</c>, <c>RUNOD</c>.
        /// </summary>
        public Dictionary<string, double> Dims { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>Hướng nhánh của olet, từ tâm ống chính ra ngoài. Null với cấu kiện nối tiếp.</summary>
        public Vector3? BranchDirection { get; set; }

        /// <summary>Đúng khi cấu kiện trùm lên ống, như cổ mặt bích SO hay olet, nên không cắt ống.</summary>
        public bool OverlapsPipe { get; set; }

        public override string Kind => "COMPONENT";
    }

    /// <summary>Một khối cơ bản đã tính số, trong hệ trục cục bộ của thiết bị.</summary>
    public sealed class PrimitiveItem
    {
        public string Shape { get; set; } = string.Empty;

        public string Axis { get; set; } = "z";

        public double Diameter { get; set; }

        public double TopDiameter { get; set; }

        public double Length { get; set; }

        public double[] Size { get; set; } = new double[3];

        public double[] Center { get; set; } = new double[3];
    }

    /// <summary>Thiết bị: gốc, góc xoay và các khối cơ bản từ định nghĩa, S4-10. Cách dựng theo mục 7.8.5.</summary>
    public sealed class EquipmentItem : BuildItem
    {
        public string Tag { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public Point3 Origin { get; set; }

        public double RotationDeg { get; set; }

        public List<PrimitiveItem> Primitives { get; } = new List<PrimitiveItem>();

        /// <summary>Đường dẫn block 3D do người dùng cung cấp, cách 3 ở mục 7.8.5. Null thì dựng từ khối cơ bản.</summary>
        public string? Block3d { get; set; }

        public override string Kind => "EQUIPMENT";
    }

    /// <summary>Cột hoặc dầm dạng hộp theo tiết diện, S4-11.</summary>
    public sealed class StructureItem : BuildItem
    {
        public string Type { get; set; } = string.Empty;

        public Point3 Start { get; set; }

        public Point3 End { get; set; }

        /// <summary>Cạnh tiết diện vuông, mm.</summary>
        public double SectionSize { get; set; }

        public override string Kind => "STRUCTURE";
    }

    public static class BuildPlanExtras
    {
        /// <summary>
        /// Bổ sung cấu kiện, thiết bị, kết cấu vào kế hoạch và cắt ống hai bên cấu kiện. Gọi sau <see cref="BuildPlan.FromModel"/>.
        /// </summary>
        public static void AddExtras(this BuildPlan plan, PipingModel model, BuildOptions options, EquipmentLibrary? library, double defaultSectionSize = 300)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (model == null) throw new ArgumentNullException(nameof(model));
            var lines = model.Lines.ToDictionary(l => l.Id, l => l.Tag, StringComparer.Ordinal);
            string Tag(string lineId) => lines.TryGetValue(lineId, out var t) ? t : string.Empty;

            foreach (var c in model.Components)
            {
                if (string.IsNullOrEmpty(c.SegmentId) || c.Dn <= 0)
                {
                    plan.Skipped.Add($"{c.Id}: không nằm trên đoạn ống hoặc thiếu DN");
                    continue;
                }

                // Đoạn ống có thể đã bị cấu kiện trước cắt thành các mảnh <id>-a, <id>-b; lấy mảnh gần vị trí cấu kiện nhất.
                var pipe = plan.Pipes.Where(p => p.IrId == c.SegmentId || p.IrId.StartsWith(c.SegmentId + "-", StringComparison.Ordinal))
                    .OrderBy(p => DistanceToPipe(p, c.Position)).FirstOrDefault();
                if (pipe == null)
                {
                    plan.Skipped.Add($"{c.Id}: đoạn ống {c.SegmentId} không được dựng");
                    continue;
                }

                var length = c.Length > 0 ? c.Length : pipe.Od * 1.5;
                var item = new ComponentItem
                {
                    IrId = c.Id,
                    LineTag = pipe.LineTag,
                    Dn = c.Dn,
                    Od = pipe.Od,
                    Layer = BuildPlan.LayerName(options.LayerPattern, pipe.LineTag, c.Type == "flange" ? "FLANGE" : c.Type == "olet" || c.Type == "reducer" ? "FITTING" : "VALVE", c.Dn),
                    Position = c.Position,
                    Axis = c.Axis,
                    Length = length,
                    Type = c.Type,
                    Tag = c.Tag,
                    SourceHandles = c.Prov?.Sources.ToList() ?? new List<string>(),
                };
                Describe(item, c, pipe, plan);
                plan.Components.Add(item);
                if (!item.OverlapsPipe) SplitPipe(plan, pipe, item);
            }

            foreach (var eq in model.Equipment)
            {
                var item = new EquipmentItem
                {
                    IrId = eq.Id,
                    LineTag = eq.Tag,
                    Tag = eq.Tag,
                    Type = eq.Type,
                    Layer = BuildPlan.LayerName(options.LayerPattern, "EQUIP", eq.Type.ToUpperInvariant(), 0),
                    Origin = eq.Geometry.Origin,
                    RotationDeg = eq.Geometry.RotationDeg,
                    SourceHandles = eq.Prov?.Sources.ToList() ?? new List<string>(),
                };
                var def = library?.Find(eq.Type) ?? library?.Find(EquipmentLibrary.GenericBox);
                try
                {
                    if (def != null)
                    {
                        item.Block3d = Block3dOf(def);
                        foreach (var b in def.Body) item.Primitives.Add(Evaluate(b, eq.Geometry.Params));
                    }
                    else
                    {
                        var p = eq.Geometry.Params;
                        var h = p.TryGetValue("H", out var hh) ? hh : 2000;
                        item.Primitives.Add(new PrimitiveItem { Shape = "box", Size = new[] { p.TryGetValue("L", out var l) ? l : 1000, p.TryGetValue("W", out var w) ? w : 1000, h }, Center = new[] { 0, 0, h / 2 } });
                    }

                    plan.EquipmentItems.Add(item);
                }
                catch (ExpressionException ex)
                {
                    plan.Skipped.Add($"{eq.Id}: {ex.Message}");
                }
            }

            foreach (var st in model.Structures)
            {
                if (st.Points.Count < 2)
                {
                    plan.Skipped.Add($"{st.Id}: thiếu điểm");
                    continue;
                }

                if (!st.Tos.HasValue)
                {
                    // Không biết cao độ mặt trên thì không dựng, vì hộp ở cao độ mặc định chỉ che ống thật.
                    plan.Skipped.Add($"{st.Id}: chưa có cao độ mặt trên kết cấu");
                    continue;
                }

                plan.Structures.Add(new StructureItem
                {
                    IrId = st.Id,
                    LineTag = "STRUCT",
                    Type = st.Type,
                    Layer = BuildPlan.LayerName(options.LayerPattern, "STRUCT", st.Type.ToUpperInvariant(), 0),
                    Start = st.Points[0],
                    End = st.Points[1],
                    SectionSize = SectionSize(st.Section, defaultSectionSize),
                    SourceHandles = st.Prov?.Sources.ToList() ?? new List<string>(),
                });
            }
        }

        /// <summary>
        /// Chọn kiểu con và tra kích thước dựng hình từ <see cref="Catalog.ComponentDims"/>. Chiều dài trong IR giữ nguyên khi đã đọc từ kích thước
        /// hoặc danh mục; chỉ thay chiều dài mặc định bằng số danh mục. Côn thu hướng đầu nhỏ hay đầu lớn về phía ống đang nối theo DN ống đó.
        /// </summary>
        private static void Describe(ComponentItem item, Component c, PipeItem pipe, BuildPlan plan)
        {
            string Attr(string key) => c.Attributes != null && c.Attributes.TryGetValue(key, out var a) ? a.Value ?? string.Empty : string.Empty;
            var description = Attr("description").ToUpperInvariant();
            var lengthBy = c.Attributes != null && c.Attributes.TryGetValue("length", out var lb) ? lb.By : "default";
            var fromDefault = lengthBy == "default" || string.IsNullOrEmpty(lengthBy);
            int.TryParse(Attr("dn2"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dn2);

            switch (c.Type)
            {
                case "flange":
                {
                    var f = Catalog.ComponentDims.Flange10K(c.Dn);
                    item.Subtype = description.Contains("BLIND") || description.Contains("BL ") ? "blind" : "so";
                    if (f != null)
                    {
                        item.Dims["D"] = f.D;
                        item.Dims["C"] = f.C;
                        item.Dims["N"] = f.Bolts;
                        item.Dims["HOLE"] = f.Hole;
                        item.Dims["T"] = f.T;
                        item.Dims["HUBOD"] = f.HubOd;
                        item.Dims["RF"] = f.RaisedFace;
                        if (fromDefault) item.Length = item.Subtype == "blind" ? f.T : f.HubLength;
                    }

                    item.OverlapsPipe = item.Subtype == "so";
                    break;
                }

                case "valve_butterfly":
                {
                    item.Subtype = "wafer";
                    var f = Catalog.ComponentDims.Flange10K(c.Dn);
                    if (f != null)
                    {
                        item.Dims["D"] = f.D;
                        item.Dims["C"] = f.C;
                        item.Dims["HOLE"] = f.Hole;
                    }

                    var ff = Catalog.ComponentDims.ValveFaceToFace(c.Type, c.Dn);
                    if (fromDefault && ff.HasValue) item.Length = ff.Value + 2 * Catalog.ComponentDims.Gasket;
                    break;
                }

                case "valve_globe":
                case "valve_gate":
                case "valve_ball":
                case "valve_check":
                case "valve":
                {
                    item.Subtype = "flanged";
                    var f = Catalog.ComponentDims.Flange10K(c.Dn);
                    if (f != null)
                    {
                        item.Dims["D"] = f.D;
                        item.Dims["T"] = f.T;
                    }

                    var ff = Catalog.ComponentDims.ValveFaceToFace(c.Type, c.Dn);
                    if (fromDefault && ff.HasValue) item.Length = ff.Value;
                    break;
                }

                case "reducer":
                {
                    var large = Catalog.ComponentDims.PipeOd(c.Dn);
                    var small = dn2 > 0 ? Catalog.ComponentDims.PipeOd(dn2) : large * 0.75;
                    // Đầu nào cùng cỡ với ống đang nối thì đầu đó chạm ống.
                    var nearIsSmall = dn2 > 0 && pipe.Dn == dn2;
                    item.Dims["ODNEAR"] = nearIsSmall ? small : large;
                    item.Dims["ODFAR"] = nearIsSmall ? large : small;
                    var h = Catalog.ComponentDims.ConcentricReducerLength(Math.Max(c.Dn, dn2));
                    if (fromDefault && h.HasValue) item.Length = h.Value;
                    break;
                }

                case "olet":
                {
                    var branchDn = dn2 > 0 ? dn2 : c.Dn;
                    var hc = Catalog.ComponentDims.HalfCoupling(branchDn);
                    item.Dims["BOSSOD"] = hc?.Od ?? Catalog.ComponentDims.PipeOd(branchDn) * 1.4;
                    item.Dims["BOSSLEN"] = hc?.L ?? Math.Max(20, branchDn * 1.0);
                    // Ống chính: đoạn ống cùng DN với mục olet gần vị trí nhất; không có thì lấy ống đang nối.
                    var run = plan.Pipes.Where(p => p.Dn == c.Dn).OrderBy(p => DistanceToPipe(p, c.Position)).FirstOrDefault();
                    item.Dims["RUNOD"] = run?.Od ?? Catalog.ComponentDims.PipeOd(c.Dn);
                    var branchDir = c.Attributes != null && c.Attributes.TryGetValue("branch", out var bd) ? ParseVector(bd.Value) : null;
                    item.BranchDirection = branchDir ?? (pipe.Dn == branchDn ? AwayFrom(pipe, c.Position) : (Vector3?)null);
                    item.Length = item.Dims["BOSSLEN"];
                    item.OverlapsPipe = true;
                    break;
                }
            }
        }

        private static Vector3? ParseVector(string? text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var parts = text!.Split(',');
            if (parts.Length != 3) return null;
            var v = new double[3];
            for (var i = 0; i < 3; i++)
            {
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return null;
            }

            var vec = new Vector3(v[0], v[1], v[2]);
            return vec.Length > 0 ? vec.Normalized() : (Vector3?)null;
        }

        /// <summary>Hướng dọc ống nhánh đi ra xa điểm cho trước, lấy từ đầu ống gần điểm đó.</summary>
        private static Vector3 AwayFrom(PipeItem pipe, Point3 p)
        {
            var axis = pipe.End - pipe.Start;
            if (axis.Length <= 0) return Vector3.UnitZ;
            var dir = axis.Normalized();
            return pipe.Start.DistanceTo(p) <= pipe.End.DistanceTo(p) ? dir : dir * -1;
        }

        private static double DistanceToPipe(PipeItem pipe, Point3 p)
        {
            var axis = pipe.End - pipe.Start;
            var total = axis.Length;
            if (total <= 0) return pipe.Start.DistanceTo(p);
            var t = Math.Max(0, Math.Min(total, (p - pipe.Start).Dot(axis.Normalized())));
            return (pipe.Start + axis.Normalized() * t).DistanceTo(p);
        }

        private static void SplitPipe(BuildPlan plan, PipeItem pipe, ComponentItem c)
        {
            var axis = (pipe.End - pipe.Start);
            var total = axis.Length;
            if (total <= 0) return;
            var dir = axis.Normalized();
            var t = (c.Position - pipe.Start).Dot(dir);
            var half = c.Length / 2;
            var before = t - half;
            var after = total - (t + half);
            // Cấu kiện nằm ngoài đoạn ống, ví dụ mặt bích, van, bích mù xếp chồng ngoài đầu hở của nhánh: ống giữ nguyên.
            if (before >= total - 0.01 || t + half <= 0.01) return;
            plan.Pipes.Remove(pipe);
            if (before > 0.01)
            {
                plan.Pipes.Add(Clone(pipe, pipe.IrId + "-a", pipe.Start, pipe.Start + dir * before));
            }

            if (after > 0.01)
            {
                plan.Pipes.Add(Clone(pipe, pipe.IrId + "-b", pipe.Start + dir * (t + half), pipe.End));
            }

            if (before <= 0.01 || after <= 0.01)
            {
                plan.Skipped.Add($"{c.IrId}: cấu kiện chồng lên đầu đoạn ống {pipe.IrId}");
            }
        }

        private static PipeItem Clone(PipeItem p, string id, Point3 start, Point3 end) => new PipeItem
        {
            IrId = id,
            LineTag = p.LineTag,
            Dn = p.Dn,
            Od = p.Od,
            Layer = p.Layer,
            Start = start,
            End = end,
            SourceHandles = p.SourceHandles.ToList(),
        };

        private static PrimitiveItem Evaluate(BodyShape b, IReadOnlyDictionary<string, double> p)
        {
            double Eval(Expr e) => e.IsEmpty ? 0 : e.Evaluate(p);
            var item = new PrimitiveItem
            {
                Shape = b.Shape,
                Axis = b.Axis,
                Diameter = Eval(b.Diameter),
                TopDiameter = Eval(b.TopDiameter),
                Length = Eval(b.Length),
            };
            if (b.Size != null && b.Size.Count == 3) item.Size = b.Size.Select(Eval).ToArray();
            if (b.Center != null && b.Center.Count == 3) item.Center = b.Center.Select(Eval).ToArray();
            if (item.Shape == "sphere" && item.Length <= 0) item.Length = item.Diameter;
            if (item.Shape == "dished_head" && item.Length <= 0) item.Length = item.Diameter / 4;
            return item;
        }

        private static string? Block3dOf(EquipmentDefinition def)
        {
            if (def.Hosts == null || !def.Hosts.TryGetValue("autocad", out var host)) return null;
            if (host.ValueKind != System.Text.Json.JsonValueKind.Object || !host.TryGetProperty("block3d", out var b)) return null;
            return b.ValueKind == System.Text.Json.JsonValueKind.String ? b.GetString() : null;
        }

        private static double SectionSize(string? section, double fallback)
        {
            if (string.IsNullOrEmpty(section)) return fallback;
            var digits = new string(section!.SkipWhile(ch => !char.IsDigit(ch)).TakeWhile(char.IsDigit).ToArray());
            return double.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : fallback;
        }
    }
}
