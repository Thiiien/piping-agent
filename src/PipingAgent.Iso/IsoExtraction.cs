using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;

namespace PipingAgent.Iso
{
    /// <summary>Một phép đối chiếu trong bản kiểm kê: điều bản vẽ nói, điều đã đọc được, khớp hay không.</summary>
    public sealed class ExtractionCheck
    {
        public string Group { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string Expected { get; set; } = string.Empty;

        public string Found { get; set; } = string.Empty;

        /// <summary><c>ok</c>, <c>gap</c> chặn dựng, <c>warn</c> dựng được nhưng cần người xem.</summary>
        public string Status { get; set; } = "ok";

        public string? Note { get; set; }
    }

    /// <summary>
    /// Bước 1 của quy trình hai bước: kiểm kê mọi thứ bản vẽ nói ra và đối chiếu trước khi dựng. Bản vẽ isometric tự chứa
    /// các phép kiểm tra chéo: số lượng trong bảng vật tư với số ô trên hình, chiều dài ống trong bảng cắt với kích thước
    /// trên hình, cao độ ghi với cao độ suy ra, mọi kích thước phải có chỗ dùng. Chỉ khi không còn khoảng trống thì mới dựng.
    /// </summary>
    public sealed class IsoExtraction
    {
        public string File { get; set; } = string.Empty;

        public string? LineTag { get; set; }

        public int Dn { get; set; }

        public List<string> Continuations { get; } = new List<string>();

        public List<ExtractionCheck> Checks { get; } = new List<ExtractionCheck>();

        public List<string> Runs { get; } = new List<string>();

        public List<string> Components { get; } = new List<string>();

        public List<string> Dimensions { get; } = new List<string>();

        public List<string> Elevations { get; } = new List<string>();

        public List<string> Bom { get; } = new List<string>();

        public List<string> CutPieces { get; } = new List<string>();

        public bool Complete => Checks.All(c => c.Status != "gap");

        public int Gaps => Checks.Count(c => c.Status == "gap");

        public int Warnings => Checks.Count(c => c.Status == "warn");

        public static IsoExtraction Build(IsoResult r, PipingModel model, string file)
        {
            var x = new IsoExtraction { File = file, LineTag = r.LineTag, Dn = r.Dn };
            x.Continuations.AddRange(r.Continuations);
            var inv = CultureInfo.InvariantCulture;

            // 1. Tuyến và cỡ.
            x.Checks.Add(new ExtractionCheck { Group = "Tuyến", Subject = "Số hiệu tuyến", Expected = "có trên khung tên", Found = r.LineTag ?? "không đọc được", Status = r.LineTag != null ? "ok" : "warn" });
            x.Checks.Add(new ExtractionCheck { Group = "Tuyến", Subject = "Cỡ ống chính", Expected = "từ số hiệu hoặc bảng vật tư", Found = r.Dn > 0 ? "DN" + r.Dn : "không đọc được", Status = r.Dn > 0 ? "ok" : "gap" });
            x.Checks.Add(new ExtractionCheck { Group = "Tuyến", Subject = "Đầu hở", Expected = "2 đầu tuyến, hoặc hơn khi có nhánh cụt", Found = r.OpenEnds.Count.ToString(inv), Status = r.OpenEnds.Count >= 2 ? "ok" : "gap" });
            x.Checks.Add(new ExtractionCheck { Group = "Tuyến", Subject = "Gốc toạ độ", Expected = "neo E, N, EL", Found = r.Anchors.Any(a => a.HasPlan) ? "neo tuyệt đối" : "chỉ có cao độ, gốc 0,0 tại đầu tuyến", Status = r.Anchors.Any(a => a.HasPlan) ? "ok" : "warn", Note = r.Anchors.Any(a => a.HasPlan) ? null : "toạ độ mặt bằng tương đối, cần tuyến nối tiếp hoặc người cho một điểm gốc" });

            // 2. Bảng vật tư với ô trên hình.
            var calloutCount = r.Callouts.GroupBy(c => c.Item).ToDictionary(g => g.Key, g => g.Count());
            var placed = model.Components.Select(c => c.Prov?.Sources.FirstOrDefault()).Where(h => h != null).Select(h => h!).ToList();
            foreach (var b in r.Bom)
            {
                x.Bom.Add($"{b.Item} | {b.Type} | {b.Size} | {b.Quantity} | {b.Description}");
                if (b.Type == "pipe" || b.Type == "other") continue;
                var qty = int.TryParse(b.Quantity, NumberStyles.Integer, inv, out var q) ? q : (int?)null;
                var found = calloutCount.TryGetValue(b.Item, out var n) ? n : 0;
                var built = r.Callouts.Where(c => c.Item == b.Item).Count(c => placed.Contains(c.Handle));
                string status;
                string? note = null;
                if (b.Type == "elbow" || b.Type == "tee" || b.Type == "support")
                {
                    var nodeKind = b.Type == "elbow" ? NodeKind.Elbow : NodeKind.Tee;
                    var nodes = b.Type == "support" ? found : model.Nodes.Count(nd => nd.Kind == nodeKind && (b.Type != "elbow" || Math.Abs((nd.Params?.TryGetValue("angle", out var ang) ?? false ? ang : 90) - (Regex.IsMatch(b.Description, "45") ? 45 : 90)) <= 1));
                    status = qty == null ? "warn" : nodes >= qty ? "ok" : "gap";
                    note = b.Type == "support" ? "giá đỡ chưa dựng, chỉ kiểm số ô" : $"{nodes} nút loại này trong đồ thị";
                    x.Checks.Add(new ExtractionCheck { Group = "Bảng vật tư", Subject = $"Mục {b.Item} {b.Type} {b.Size}", Expected = $"{b.Quantity} cái", Found = $"{found} ô, {nodes} trong đồ thị", Status = status, Note = note });
                    continue;
                }

                status = qty == null ? "warn" : built >= qty ? "ok" : found >= qty ? "gap" : "gap";
                if (qty != null && found < qty) note = $"thiếu {qty - found} ô trên hình";
                else if (qty != null && built < qty) note = $"{found - built} ô không trỏ tới ống nào";
                else if (qty != null && found > qty) { status = "warn"; note = $"dư {found - qty} ô, có thể ô cỡ ống hoặc số đoạn cắt bị nhận nhầm"; }
                x.Checks.Add(new ExtractionCheck { Group = "Bảng vật tư", Subject = $"Mục {b.Item} {b.Type} {b.Size}", Expected = $"{b.Quantity} cái", Found = $"{found} ô, đặt được {built}", Status = status, Note = note });
            }

            if (r.Bom.Count == 0) x.Checks.Add(new ExtractionCheck { Group = "Bảng vật tư", Subject = "Bảng vật tư", Expected = "có", Found = "không đọc được", Status = "gap" });

            // 3. Kích thước: mọi số trên hình phải có chỗ dùng.
            var usedByComponent = new HashSet<string>(model.Components.Where(c => c.Attributes != null && c.Attributes.TryGetValue("length", out var l) && l.SourceHandle != null).Select(c => c.Attributes!["length"].SourceHandle!));
            foreach (var s in r.Segments)
            {
                foreach (var d in s.Dimensions) x.Dimensions.Add($"{d.Value} | {d.Axis} | ống | {d.By}");
            }

            foreach (var d in r.UnassignedDimensions)
            {
                var used = usedByComponent.Contains(d.Handle);
                x.Dimensions.Add($"{d.Value} | {d.Axis} | {(used ? "chiều dài cấu kiện" : "chưa dùng")} | {d.By}");
                if (!used) x.Checks.Add(new ExtractionCheck { Group = "Kích thước", Subject = $"Số {d.Value} tại ({d.TextAt.X:0}, {d.TextAt.Y:0})", Expected = "gán cho ống hoặc cấu kiện", Found = "chưa dùng", Status = d.By.Contains("_ref") ? "warn" : "warn", Note = d.By.Contains("_ref") ? "kích thước tham khảo (*)" : "có thể là cỡ lệch của hộp chéo hoặc kích thước phụ kiện không đặt được" });
            }

            // Một chuỗi thẳng hàng không có kích thước nào thì chiều dài bằng 0 khi dựng.
            foreach (var run in model.Runs.Where(run => run.Prov != null && run.Prov.Confidence <= 0.5))
            {
                x.Checks.Add(new ExtractionCheck { Group = "Kích thước", Subject = $"Đoạn chạy {run.Id} DN{run.Dn}", Expected = "có kích thước", Found = "không có", Status = "gap", Note = "chiều dài đoạn này bằng 0 khi dựng" });
            }

            // 4. Cao độ ghi với cao độ suy ra.
            foreach (var c in r.Closures)
            {
                var label = c.Anchor.HasPlan ? $"neo E {c.Anchor.E} N {c.Anchor.N} EL {c.Anchor.El}" : $"cao độ {c.Anchor.El:0.#}";
                x.Elevations.Add($"{label} | {(c.Reached ? c.Derived.ToString() : "không tới")} | lệch {(c.ErrorMm.HasValue ? c.ErrorMm.Value.ToString("0.#", inv) : "?")} mm");
                x.Checks.Add(new ExtractionCheck { Group = "Khép kín", Subject = label, Expected = "lệch không quá 5 mm", Found = c.Reached ? $"lệch {c.ErrorMm:0.#} mm" : "không tới được", Status = !c.Reached ? "gap" : c.ErrorMm <= 5 ? "ok" : c.ErrorMm <= 25 ? "warn" : "gap" });
            }

            if (r.Closures.Count == 0) x.Checks.Add(new ExtractionCheck { Group = "Khép kín", Subject = "Kiểm tra khép kín", Expected = "ít nhất một cao độ hoặc neo thứ hai", Found = "không có", Status = "warn", Note = "không có gì để đối chiếu tổng kích thước" });

            // 5. Bảng cắt ống với chiều dài ống suy ra.
            foreach (var p in r.CutPieces) x.CutPieces.Add($"{p.No} | DN{p.Dn} | {p.Length}");
            if (r.CutPieces.Count > 0)
            {
                var cut = r.CutPieces.Sum(p => p.Length);
                // Cấu kiện xếp ngoài đầu hở không chiếm chỗ của ống.
                var pipes = model.Segments.Sum(s => Math.Max(0, s.Length - s.TrimA - s.TrimB)) - model.Components.Where(c => c.Prov?.Rule != "ISO-CMP-02").Sum(c => c.Length);
                var diff = pipes - cut;
                x.Checks.Add(new ExtractionCheck { Group = "Bảng cắt ống", Subject = "Tổng chiều dài ống", Expected = $"{cut:0} mm trong {r.CutPieces.Count} đoạn cắt", Found = $"{pipes:0} mm sau khi trừ phụ kiện và cấu kiện", Status = Math.Abs(diff) <= 0.03 * cut + 60 ? "ok" : "warn", Note = $"chênh {diff:+0;-0} mm; chênh dương thường là phụ kiện ở điểm nối tiếp nằm trên tờ khác, chênh nhỏ là khe gioăng, mặt bích trượt và chiều dài cấu kiện ước lượng" });
                var bomPipeM = r.Bom.Where(b => b.Type == "pipe").Select(b => Regex.Match(b.Quantity, "([\\d.]+)\\s*M", RegexOptions.IgnoreCase)).Where(m => m.Success).Sum(m => double.Parse(m.Groups[1].Value, inv));
                if (bomPipeM > 0) x.Checks.Add(new ExtractionCheck { Group = "Bảng cắt ống", Subject = "Ống trong bảng vật tư", Expected = $"{bomPipeM:0.0} m", Found = $"{cut / 1000:0.0} m trong bảng cắt", Status = Math.Abs(bomPipeM * 1000 - cut) <= 150 ? "ok" : "warn" });
            }

            // 6. Đoạn chạy và cấu kiện đã đọc.
            foreach (var run in model.Runs)
            {
                var len = run.EndElevation.HasValue ? Math.Abs(run.EndElevation.Value - run.Elevation.Raw) : run.PlanPoints[0].DistanceTo(run.PlanPoints[1]);
                x.Runs.Add($"{run.Id} | DN{run.Dn} | {(run.EndElevation.HasValue ? "đứng" : "ngang")} | {len:0} mm | EL {run.Elevation.Raw:0}{(run.EndElevation.HasValue ? " tới " + run.EndElevation.Value.ToString("0", inv) : "")} | tin cậy {run.Prov?.Confidence:0.0}");
                if ((run.Prov?.Confidence ?? 1) < 0.9) x.Checks.Add(new ExtractionCheck { Group = "Đoạn chạy", Subject = run.Id, Expected = "mọi đoạn con có kích thước", Found = "có đoạn con chia theo tỉ lệ giấy", Status = "warn", Note = "hai đầu đoạn chạy đúng, mối hàn hoặc giá đỡ ở giữa chỉ gần đúng" });
            }

            foreach (var c in model.Components)
            {
                var by = c.Attributes != null && c.Attributes.TryGetValue("length", out var l) ? l.By : "?";
                x.Components.Add($"{c.Id} | {c.Type} | {c.Tag} | DN{c.Dn} | dài {c.Length:0} ({LengthSource(by)}) | {c.Position} | {(string.IsNullOrEmpty(c.SegmentId) ? "chưa gắn đoạn" : c.SegmentId)}");
                if (by != "dimension" && by != "span" && (c.Type.StartsWith("valve", StringComparison.Ordinal) || c.Type == "reducer")) x.Checks.Add(new ExtractionCheck { Group = "Cấu kiện", Subject = $"{c.Tag} {c.Type}", Expected = "chiều dài theo hình", Found = $"{c.Length:0} mm {LengthSource(by)}", Status = "warn", Note = by == "catalog_unverified" ? "danh mục JIS 10K trong mã chưa được chuyên gia đối chiếu" : null });
            }

            foreach (var i in model.Issues.Where(i => i.RuleId == "R-ISO-03"))
            {
                x.Checks.Add(new ExtractionCheck { Group = "Cấu kiện", Subject = i.Message, Expected = "trỏ tới ống", Found = "không", Status = "gap" });
            }

            return x;
        }

        private static string LengthSource(string by)
        {
            switch (by)
            {
                case "dimension": return "theo hình";
                case "span": return "theo khe ký hiệu trên hình";
                case "paper": return "theo tỉ lệ giấy";
                case "catalog_unverified": return "theo danh mục chưa xác nhận";
                default: return "ước lượng";
            }
        }

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Kiểm kê trích xuất: {File}");
            sb.AppendLine();
            sb.AppendLine($"Tuyến {LineTag ?? "?"}, DN{Dn}. Kết luận: **{(Complete ? "đủ để dựng" : "chưa đủ, " + Gaps + " khoảng trống")}**, {Warnings} điểm cần người xem.");
            sb.AppendLine();
            if (Continuations.Count > 0)
            {
                sb.AppendLine("Nối tiếp: " + string.Join("; ", Continuations));
                sb.AppendLine();
            }

            sb.AppendLine("## Đối chiếu");
            sb.AppendLine();
            sb.AppendLine("| Nhóm | Đối tượng | Bản vẽ nói | Đọc được | Kết quả | Ghi chú |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var c in Checks.OrderBy(c => c.Status == "gap" ? 0 : c.Status == "warn" ? 1 : 2))
            {
                var mark = c.Status == "ok" ? "khớp" : c.Status == "warn" ? "xem lại" : "THIẾU";
                sb.AppendLine($"| {c.Group} | {c.Subject} | {c.Expected} | {c.Found} | {mark} | {c.Note ?? ""} |");
            }

            void Section(string title, string header, List<string> rows)
            {
                sb.AppendLine();
                sb.AppendLine("## " + title);
                sb.AppendLine();
                if (rows.Count == 0) { sb.AppendLine("Không có."); return; }
                sb.AppendLine(header);
                sb.AppendLine("|" + string.Join("|", header.Split('|').Skip(1).Take(header.Split('|').Length - 2).Select(_ => "---")) + "|");
                foreach (var row in rows) sb.AppendLine("| " + row + " |");
            }

            Section("Đoạn chạy", "| Mã | Cỡ | Hướng | Dài | Cao độ | Tin cậy |", Runs);
            Section("Cấu kiện", "| Mã | Loại | Mục | Cỡ | Chiều dài | Vị trí | Đoạn |", Components);
            Section("Bảng vật tư", "| Mục | Loại | Cỡ | Số lượng | Mô tả |", Bom);
            Section("Bảng cắt ống", "| Đoạn | Cỡ | Dài |", CutPieces);
            Section("Kích thước", "| Giá trị | Trục | Dùng cho | Cách đọc |", Dimensions);
            Section("Cao độ và khép kín", "| Ghi trên tờ | Suy ra | Lệch |", Elevations);
            return sb.ToString();
        }
    }
}
