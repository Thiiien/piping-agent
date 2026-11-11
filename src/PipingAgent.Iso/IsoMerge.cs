using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;

namespace PipingAgent.Iso
{
    /// <summary>Một tờ đưa vào gộp: IR của tờ và phép dời đưa gốc tờ về hệ chung.</summary>
    public sealed class MergeSheet
    {
        public string Name { get; set; } = string.Empty;

        public PipingModel Model { get; set; } = new PipingModel();

        /// <summary>Dời E, N, EL cộng vào mọi toạ độ của tờ.</summary>
        public Vector3 Offset { get; set; }

        /// <summary>Người cho hay giả định; ghi vào bản kiểm kê.</summary>
        public string OffsetBy { get; set; } = "giả định";
    }

    /// <summary>Đoạn ống tham chiếu vẽ thêm, ví dụ ống góp 600A mà hai tờ cùng nối vào nhưng không có tờ riêng.</summary>
    public sealed class ReferenceStub
    {
        public string Tag { get; set; } = string.Empty;

        public Point3 At { get; set; }

        /// <summary>Trục: <c>E</c>, <c>N</c> hoặc <c>U</c>.</summary>
        public string Axis { get; set; } = "E";

        public int Dn { get; set; }

        /// <summary>Chiều dài mỗi bên của điểm nối, mm.</summary>
        public double HalfLength { get; set; } = 1000;
    }

    /// <summary>
    /// Gộp IR của nhiều tờ isometric vào một mô hình, I4. Mỗi tờ có gốc riêng, nên cần phép dời từng tờ về hệ chung; khi không có
    /// toạ độ E, N trên tờ và không có tờ ống góp chung thì phép dời là giả định và được ghi rõ trong bản kiểm kê gộp.
    /// Mã đối tượng của từng tờ được thêm tiền tố để không trùng. Đồ thị 3D phải dựng lại sau khi gộp.
    /// </summary>
    public static class IsoMerge
    {
        public static PipingModel Merge(IReadOnlyList<MergeSheet> sheets, IReadOnlyList<ReferenceStub> stubs, string? file)
        {
            var merged = new PipingModel { Source = new SourceInfo { File = file, EntityCount = sheets.Sum(s => s.Model.Source.EntityCount) } };
            var i = 0;
            foreach (var sheet in sheets)
            {
                i++;
                var prefix = "s" + i.ToString(CultureInfo.InvariantCulture) + "-";
                var m = sheet.Model;
                var o = sheet.Offset;
                foreach (var line in m.Lines)
                {
                    line.Id = prefix + line.Id;
                    merged.Lines.Add(line);
                }

                foreach (var run in m.Runs)
                {
                    run.Id = prefix + run.Id;
                    run.LineId = prefix + run.LineId;
                    run.PlanPoints = run.PlanPoints.Select(p => new Point2(p.X + o.X, p.Y + o.Y)).ToList();
                    run.Elevation.Raw += o.Z;
                    if (run.Elevation.Centerline.HasValue) run.Elevation.Centerline += o.Z;
                    if (run.EndElevation.HasValue) run.EndElevation += o.Z;
                    merged.Runs.Add(run);
                }

                foreach (var c in m.Components)
                {
                    c.Id = prefix + c.Id;
                    c.SegmentId = string.Empty;
                    c.Position = c.Position + o;
                    c.Tag = sheet.Name + " " + c.Tag;
                    merged.Components.Add(c);
                }

                foreach (var eq in m.Equipment)
                {
                    eq.Id = prefix + eq.Id;
                    eq.Geometry.Origin = eq.Geometry.Origin + o;
                    merged.Equipment.Add(eq);
                }

                foreach (var st in m.Structures)
                {
                    st.Id = prefix + st.Id;
                    st.Points = st.Points.Select(p => p + o).ToList();
                    merged.Structures.Add(st);
                }

                foreach (var issue in m.Issues)
                {
                    issue.Id = prefix + issue.Id;
                    issue.Message = sheet.Name + ": " + issue.Message;
                    merged.Issues.Add(issue);
                }

                merged.Decisions.AddRange(m.Decisions);
            }

            var k = 0;
            foreach (var stub in stubs)
            {
                k++;
                var line = new Line { Id = "ref-line-" + k.ToString("000", CultureInfo.InvariantCulture), Tag = stub.Tag, Prov = new Provenance { Rule = "ISO-REF-01", Confidence = 0.3 } };
                merged.Lines.Add(line);
                var vertical = stub.Axis.Equals("U", StringComparison.OrdinalIgnoreCase);
                var dir = stub.Axis.Equals("N", StringComparison.OrdinalIgnoreCase) ? new Vector2(0, 1) : new Vector2(1, 0);
                merged.Runs.Add(new Run
                {
                    Id = "ref-run-" + k.ToString("000", CultureInfo.InvariantCulture),
                    LineId = line.Id,
                    Dn = stub.Dn,
                    Elevation = new Elevation { Raw = vertical ? stub.At.Z - stub.HalfLength : stub.At.Z, Datum = ElevationDatum.CL },
                    EndElevation = vertical ? stub.At.Z + stub.HalfLength : (double?)null,
                    PlanPoints = vertical
                        ? new List<Point2> { stub.At.Plan }
                        : new List<Point2> { stub.At.Plan - dir * stub.HalfLength, stub.At.Plan + dir * stub.HalfLength },
                    Prov = new Provenance { Rule = "ISO-REF-01", Confidence = 0.3 },
                });
            }

            return merged;
        }

        /// <summary>Bản kiểm kê gộp: tờ nào dời bao nhiêu, do ai, ống tham chiếu nào, cộng số lượng từng tờ.</summary>
        public static string Report(IReadOnlyList<MergeSheet> sheets, IReadOnlyList<ReferenceStub> stubs, PipingModel merged, string? file)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Kiểm kê gộp nhiều tờ: " + (file ?? string.Empty));
            sb.AppendLine();
            // Tờ đầu là hệ quy chiếu; các tờ sau không có phép dời do người cho đều là giả định, kể cả dời bằng 0.
            var assumed = sheets.Skip(1).Count(s => s.OffsetBy == "giả định");
            sb.AppendLine(assumed > 0
                ? $"Kết luận: **vị trí tương đối giữa các tờ là giả định** ({assumed} tờ), vì tờ không ghi toạ độ E, N và chưa có tờ ống góp chung. Hình đúng trong từng tờ; khoảng cách giữa các tờ cần người xác nhận hoặc tờ ống góp."
                : "Kết luận: mọi phép dời giữa các tờ do người cho.");
            sb.AppendLine();
            sb.AppendLine("## Các tờ");
            sb.AppendLine();
            sb.AppendLine("| Tờ | Tuyến | Dời E | Dời N | Dời EL | Nguồn phép dời | Đoạn chạy | Cấu kiện |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            var i = 0;
            foreach (var s in sheets)
            {
                i++;
                var prefix = "s" + i.ToString(CultureInfo.InvariantCulture) + "-";
                var tag = merged.Lines.Where(l => l.Id.StartsWith(prefix, StringComparison.Ordinal)).Select(l => l.Tag).FirstOrDefault() ?? "?";
                var runs = merged.Runs.Count(r => r.Id.StartsWith(prefix, StringComparison.Ordinal));
                var comps = merged.Components.Count(c => c.Id.StartsWith(prefix, StringComparison.Ordinal));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} | {2:0} | {3:0} | {4:0} | {5} | {6} | {7} |", s.Name, tag, s.Offset.X, s.Offset.Y, s.Offset.Z, s.OffsetBy, runs, comps));
            }

            if (stubs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Ống tham chiếu vẽ thêm");
                sb.AppendLine();
                sb.AppendLine("| Tuyến | Điểm nối | Trục | DN | Dài mỗi bên |");
                sb.AppendLine("|---|---|---|---|---|");
                foreach (var st in stubs)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0} | ({1:0}, {2:0}, {3:0}) | {4} | DN{5} | {6:0} mm |", st.Tag, st.At.X, st.At.Y, st.At.Z, st.Axis, st.Dn, st.HalfLength));
                }

                sb.AppendLine();
                sb.AppendLine("Ống tham chiếu chỉ để thấy chỗ nối, không có trong bảng vật tư của tờ nào, độ tin cậy 0,3.");
            }

            sb.AppendLine();
            sb.AppendLine("## Cấu kiện sau gộp");
            sb.AppendLine();
            sb.AppendLine("| Mã | Loại | Mục | Cỡ | Dài | Vị trí | Đoạn |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var c in merged.Components)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} | {2} | DN{3} | {4:0} | ({5:0}, {6:0}, {7:0}) | {8} |", c.Id, c.Type, c.Tag, c.Dn, c.Length, c.Position.X, c.Position.Y, c.Position.Z, string.IsNullOrEmpty(c.SegmentId) ? "chưa gắn" : c.SegmentId));
            }

            var open = merged.Issues.Count(x => x.Status == IssueStatus.Open);
            sb.AppendLine();
            sb.AppendLine($"Vấn đề mang theo từ các tờ: {merged.Issues.Count}, đang mở {open}.");
            return sb.ToString();
        }
    }
}
