using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Source;

namespace PipingAgent.Iso
{
    /// <summary>Một dòng bảng vật tư trên isometric.</summary>
    public sealed class BomRow
    {
        public int Item { get; set; }

        public string Description { get; set; } = string.Empty;

        public string Size { get; set; } = string.Empty;

        public string Quantity { get; set; } = string.Empty;

        /// <summary>Loại cấu kiện suy từ mô tả: <c>flange</c>, <c>valve_gate</c>, <c>reducer</c>, <c>olet</c>, <c>instrument</c>, <c>nipple</c>, <c>tee</c>, <c>elbow</c>, <c>pipe</c>, <c>other</c>.</summary>
        public string Type { get; set; } = "other";

        public int Dn { get; set; }

        public int? Dn2 { get; set; }
    }

    /// <summary>Ô số thứ tự trên hình kèm điểm nó trỏ tới trên mạng ống.</summary>
    public sealed class IsoCallout
    {
        public int Item { get; set; }

        public Point2 BoxAt { get; set; }

        public Point2? Target { get; set; }

        public string Handle { get; set; } = string.Empty;
    }

    /// <summary>Đọc bảng vật tư và ô số thứ tự, I3. Hai bố cục đã gặp: bảng DESCRIPTION, SIZE, QTY cỡ inch và bảng ISOGEN PT.NO, DESCRIPTION, SIZE(MM), Q'TY.</summary>
    public static class IsoBom
    {
        public static List<BomRow> ReadRows(IReadOnlyList<SourceEntity> texts, double pageWidth)
        {
            // Bản ISOGEN đặt bảng vật tư trên layer MATLIST; bản khác lấy phần bên phải tờ.
            var matlist = texts.Where(t => t.Center.HasValue && t.Layer.Equals("MATLIST", StringComparison.OrdinalIgnoreCase)).ToList();
            var left = matlist.Count > 0 ? matlist.Min(t => t.Center!.Value.X) - 5 : pageWidth * 0.66;
            var words = texts.Where(t => t.Center.HasValue && t.Center.Value.X > left && !string.IsNullOrWhiteSpace(t.Text)).ToList();
            var header = words.FirstOrDefault(w => w.Text == "DESCRIPTION");
            var sizeHeader = words.FirstOrDefault(w => w.Text!.StartsWith("SIZE", StringComparison.Ordinal));
            var qtyHeader = words.FirstOrDefault(w => Regex.IsMatch(w.Text!, "^Q'?TY"));
            if (header == null || sizeHeader == null) return new List<BomRow>();
            var descX = header.Center!.Value.X;
            var sizeX = sizeHeader.Center!.Value.X;
            var qtyX = qtyHeader?.Center!.Value.X ?? sizeX + 80;
            var headerY = header.Center.Value.Y;
            var sizeInMm = sizeHeader.Text!.IndexOf("MM", StringComparison.OrdinalIgnoreCase) >= 0;
            // Bảng cắt ống hoặc bảng khác phía dưới có tiêu đề riêng; bảng vật tư dừng ở đó.
            var floor = words.Where(w => w.Center!.Value.Y < headerY - 5 && Regex.IsMatch(w.Text!, "^(PIECE|CUT PIPE|LENGTH|SPOOL|WELD)", RegexOptions.IgnoreCase))
                .Select(w => w.Center!.Value.Y).DefaultIfEmpty(double.NegativeInfinity).Max();
            var rows = new List<BomRow>();
            var height = header.TextHeight ?? 6;
            if (!sizeInMm) floor = double.NegativeInfinity;
            var lines = words.Where(w => w.Center!.Value.Y < headerY - height * 0.5 && w.Center.Value.Y > floor + height)
                .GroupBy(w => Math.Round(w.Center!.Value.Y / 3)).OrderByDescending(g => g.Key);
            BomRow? current = null;
            foreach (var line in lines)
            {
                var ws = line.OrderBy(w => w.Center!.Value.X).ToList();
                var first = ws[0];
                if (Regex.IsMatch(first.Text!, "^\\d{1,3}$") && first.Center!.Value.X < descX - 5 && ws.Count > 2)
                {
                    current = new BomRow { Item = int.Parse(first.Text!, CultureInfo.InvariantCulture) };
                    if (sizeInMm && rows.Any(x => x.Item == current.Item)) break; // sang bảng khác đánh số lại từ đầu
                    rows.Add(current);
                    ws = ws.Skip(1).ToList();
                }

                if (current == null) continue;
                var desc = ws.Where(w => w.Center!.Value.X < sizeX - 8).Select(w => w.Text);
                var size = ws.Where(w => w.Center!.Value.X >= sizeX - 8 && w.Center.Value.X < Math.Min(sizeX + 40, qtyX - 8)).Select(w => w.Text);
                var qty = ws.Where(w => Math.Abs(w.Center!.Value.X - qtyX) < 20 && w.Center.Value.X >= qtyX - 8).Select(w => w.Text);
                current.Description = (current.Description + " " + string.Join(" ", desc)).Trim();
                if (string.IsNullOrEmpty(current.Size)) current.Size = string.Join("", size);
                if (string.IsNullOrEmpty(current.Quantity)) current.Quantity = string.Join("", qty);
            }

            foreach (var r in rows) Classify(r, sizeInMm);
            return rows;
        }

        private static void Classify(BomRow r, bool sizeInMm)
        {
            var d = r.Description.ToUpperInvariant();
            if (d.StartsWith("PIPE")) r.Type = "pipe";
            else if (d.Contains("GSKT") || d.Contains("GASKET") || d.Contains("BOLT") || d.Contains("NUT")) r.Type = "other";
            else if (d.Contains("BLIND")) r.Type = "flange";
            else if (d.Contains("FLG") || d.Contains("FLANGE") || Regex.IsMatch(d, "\\b(SO|WN|SW|LJ|BL)\\b.*\\b(RF|FF|RJ)\\b") || d.Contains("B2220") || d.Contains("B16.5")) r.Type = "flange";
            else if (d.Contains("BUTTERFLY")) r.Type = "valve_butterfly";
            else if (d.Contains("GATE")) r.Type = "valve_gate";
            else if (d.Contains("GLOBE")) r.Type = "valve_globe";
            else if (d.Contains("CHECK")) r.Type = "valve_check";
            else if (d.Contains("BALL")) r.Type = "valve_ball";
            else if (Regex.IsMatch(d, "\\bVA(LVE)?\\b")) r.Type = "valve";
            else if (d.Contains("RED")) r.Type = "reducer";
            else if (d.Contains("OLET") || d.Contains("COUPLING")) r.Type = "olet";
            else if (d.Contains("NPL") || d.Contains("NIPPLE")) r.Type = "nipple";
            else if (d.Contains("TEE")) r.Type = "tee";
            else if (d.Contains("ELL") || d.Contains("ELBOW") || Regex.IsMatch(d, "^EL-?\\s?\\d")) r.Type = "elbow";
            else if (d.Contains("CAP")) r.Type = "cap";
            else if (d.Contains("INSTRUMENT")) r.Type = "instrument";
            else if (d.Contains("SUPPORT") || d.Contains("SPRING") || d.Contains("TRUNNION")) r.Type = "support";
            var m = Regex.Match(r.Size, "^(\\d+(?:-\\d+/\\d+|/\\d+|\\.\\d+)?)(?:X(\\d+(?:-\\d+/\\d+|/\\d+)?))?");
            if (m.Success)
            {
                r.Dn = sizeInMm ? ParseMm(m.Groups[1].Value) : NominalSize.NpsToDn(m.Groups[1].Value) ?? 0;
                if (m.Groups[2].Success) r.Dn2 = sizeInMm ? ParseMm(m.Groups[2].Value) : NominalSize.NpsToDn(m.Groups[2].Value);
            }
        }

        private static int ParseMm(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

        /// <summary>Một đoạn ống cắt trong bảng cắt ống của ISOGEN: số đoạn, cỡ, chiều dài mm.</summary>
        public sealed class CutPiece
        {
            public int No { get; set; }

            public int Dn { get; set; }

            public double Length { get; set; }
        }

        /// <summary>Bảng cắt ống ISOGEN dưới bảng vật tư: tiêu đề PIECE NO., LENGTH(mm), SIZE; có thể xếp hai cột.</summary>
        public static List<CutPiece> ReadCutPieces(IReadOnlyList<SourceEntity> texts)
        {
            var result = new List<CutPiece>();
            var headers = texts.Where(t => t.Center.HasValue && Regex.IsMatch(t.Text ?? string.Empty, "^PIECE\\s*NO", RegexOptions.IgnoreCase)).ToList();
            if (headers.Count == 0) return result;
            var headerY = headers.Max(h => h.Center!.Value.Y);
            var lengthHeaders = texts.Where(t => t.Center.HasValue && Regex.IsMatch(t.Text ?? string.Empty, "^LENGTH", RegexOptions.IgnoreCase) && Math.Abs(t.Center.Value.Y - headerY) < 10).ToList();
            var sizeHeaders = texts.Where(t => t.Center.HasValue && Regex.IsMatch(t.Text ?? string.Empty, "^SIZE", RegexOptions.IgnoreCase) && Math.Abs(t.Center.Value.Y - headerY) < 10).ToList();
            var minX = headers.Min(h => h.Center!.Value.X) - 10;
            var rows = texts.Where(t => t.Center.HasValue && t.Center.Value.X > minX && t.Center.Value.Y < headerY - 3 && Regex.IsMatch(t.Text ?? string.Empty, "^\\d+(\\.\\d+)?$"))
                .GroupBy(t => Math.Round(t.Center!.Value.Y / 3)).OrderByDescending(g => g.Key);
            foreach (var row in rows)
            {
                // Mỗi cột con của bảng có tiêu đề PIECE NO. riêng; số gần tiêu đề nào thuộc cột đó.
                foreach (var h in headers)
                {
                    var noText = row.Where(t => Math.Abs(t.Center!.Value.X - h.Center!.Value.X) < 12).OrderBy(t => Math.Abs(t.Center!.Value.X - h.Center!.Value.X)).FirstOrDefault();
                    if (noText == null || !int.TryParse(noText.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var no)) continue;
                    var lh = lengthHeaders.OrderBy(x => Math.Abs(x.Center!.Value.X - h.Center!.Value.X)).FirstOrDefault();
                    var sh = sizeHeaders.OrderBy(x => Math.Abs(x.Center!.Value.X - h.Center!.Value.X)).FirstOrDefault();
                    var lenText = lh == null ? null : row.Where(t => t != noText && Math.Abs(t.Center!.Value.X - lh.Center!.Value.X) < 20).OrderBy(t => Math.Abs(t.Center!.Value.X - lh.Center!.Value.X)).FirstOrDefault();
                    var sizeText = sh == null ? null : row.Where(t => t != noText && t != lenText && Math.Abs(t.Center!.Value.X - sh.Center!.Value.X) < 20).OrderBy(t => Math.Abs(t.Center!.Value.X - sh.Center!.Value.X)).FirstOrDefault();
                    if (lenText == null) continue;
                    var piece = new CutPiece { No = no, Length = double.Parse(lenText.Text!, CultureInfo.InvariantCulture) };
                    if (sizeText != null)
                    {
                        var sizeInInch = sh!.Text!.IndexOf("INCH", StringComparison.OrdinalIgnoreCase) >= 0;
                        piece.Dn = sizeInInch && !Regex.IsMatch(sizeText.Text!, "^\\d{2,}$") ? NominalSize.NpsToDn(sizeText.Text) ?? 0 : ParseMm(sizeText.Text!);
                    }

                    if (!result.Any(p => p.No == piece.No)) result.Add(piece);
                }
            }

            return result.OrderBy(p => p.No).ToList();
        }

        /// <summary>Ô số thứ tự: số nằm trong vòng bốn nét, kèm đường chỉ trỏ tới điểm trên mạng ống.</summary>
        public static List<IsoCallout> ReadCallouts(IReadOnlyList<SourceEntity> texts, List<(Point2 A, Point2 B)> thinLines, Func<Box2, bool> isBoxed, Func<Box2, Point2?> followLeader, double pageWidth)
        {
            var result = new List<IsoCallout>();
            // Hai dạng: số thứ tự trong ô, và mã mặt bích dạng F7 G10 B12 (mặt bích 7, gioăng 10, bu lông 12) trong ô dài.
            foreach (var t in texts.Where(t => t.Center.HasValue && Regex.IsMatch(t.Text ?? string.Empty, "^(\\d{1,2}|F\\d{1,2}( [GB]\\d{1,2})*)$") && t.Center.Value.X < pageWidth * 0.66))
            {
                var box = t.Bounds ?? Box2.Around(t.Center!.Value.Plan, 1);
                var isFlange = t.Text!.StartsWith("F", StringComparison.Ordinal);
                if (!isFlange && !isBoxed(box)) continue;
                var item = int.Parse(Regex.Match(t.Text!, "\\d+").Value, CultureInfo.InvariantCulture);
                var callout = new IsoCallout { Item = item, BoxAt = box.Center, Handle = t.Handle };
                if (isFlange && t.Text.Length <= 3) box = box.Expand(30); // ô mã mặt bích dài hơn chữ khi PDF tách từng từ
                // Khối chữ của ô gồm cả tag ghi ngay dưới ô, vì đường chỉ có thể xuất phát từ mép tag.
                var h = t.TextHeight ?? 4;
                var block = box;
                foreach (var x in texts)
                {
                    // Tag thiết bị như V-E2084-2 hoặc cỡ như 350X250A ghi ngay dưới ô; số thuần là kích thước, không thuộc khối.
                    if (x == t || !x.Center.HasValue || string.IsNullOrWhiteSpace(x.Text) || Regex.IsMatch(x.Text!, "^[+-]?\\d+$")) continue;
                    if (!Regex.IsMatch(x.Text!, "^[A-Z]{1,3}-|^\\d+(X\\d+)?A$|^\\d+(X\\d+)?\"")) continue;
                    var xb = x.Bounds ?? Box2.Around(x.Center.Value.Plan, 1);
                    if (xb.Center.Y < box.Min.Y - 3 * h || xb.Center.Y > box.Max.Y + h) continue;
                    if (xb.Max.X < box.Min.X - 10 || xb.Min.X > box.Max.X + 10) continue;
                    block = block.Union(xb);
                }

                var target = followLeader(block);
                if (!target.HasValue)
                {
                    // Đường chỉ: nét mỏng có một đầu cách ô không quá 12 pt, đầu kia là điểm trỏ tới.
                    var grown = box.Expand(12);
                    var leader = thinLines.Where(l => (grown.Contains(l.A) ^ grown.Contains(l.B)) && l.A.DistanceTo(l.B) > 8)
                        .OrderByDescending(l => l.A.DistanceTo(l.B)).FirstOrDefault();
                    if (leader.A != leader.B) target = grown.Contains(leader.A) ? leader.B : leader.A;
                }

                callout.Target = target;
                result.Add(callout);
            }

            return result;
        }
    }
}
