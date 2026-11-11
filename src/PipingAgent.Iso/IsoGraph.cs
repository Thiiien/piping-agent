using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Source;

namespace PipingAgent.Iso
{
    /// <summary>Ba trục của hình chiếu isometric trên giấy và chiều tương ứng trong không gian thật; Skew là đoạn chéo không theo trục.</summary>
    public enum IsoAxis
    {
        East,
        North,
        Up,
        Skew,
    }

    public sealed class IsoOptions
    {
        /// <summary>Góc trên giấy của trục Đông, Bắc, Đứng, độ, ngược chiều kim đồng hồ.</summary>
        public double EastDeg { get; set; } = 30;

        public double NorthDeg { get; set; } = 150;

        public double UpDeg { get; set; } = 90;

        public double AngleTolDeg { get; set; } = 4;

        /// <summary>Nét dày từ ngưỡng này là ống chính, điểm.</summary>
        public double PipeLineWidth { get; set; } = 2.0;

        /// <summary>Nếu có, chỉ nét trên các layer này mới là ống. ISOGEN dùng layer PIPE.</summary>
        public List<string> PipeLayers { get; set; } = new List<string>();

        public double Snap { get; set; } = 1.5;

        /// <summary>Khe tối đa tại ký hiệu phụ kiện để nối thẳng hàng, điểm.</summary>
        public double FittingGap { get; set; } = 40;

        /// <summary>Hai nét khác trục có đầu hở cách giao điểm không quá ngưỡng này thì nối tại giao điểm, ISOGEN vẽ co bằng cung nên hai nét không chạm nhau.</summary>
        public double CornerGap { get; set; } = 12;

        /// <summary>Đường kích thước cách nét ống tối đa, điểm.</summary>
        public double DimReach { get; set; } = 120;

        /// <summary>Chữ kích thước thấp hơn ngưỡng này bị bỏ, điểm.</summary>
        public double MinDimTextHeight { get; set; } = 7;

        /// <summary>Cạnh ô số thứ tự ngắn nhất, điểm; loại cạnh đầu mũi tên.</summary>
        public double BoxEdgeMin { get; set; } = 7;

        /// <summary>Gán kích thước theo hai đầu đường gióng về hai đỉnh của mạng, cách của ISOGEN. Bản PDF mẫu dùng cách gán đoạn gần nhất cộng ràng buộc neo.</summary>
        public bool UseSpanDimensions { get; set; }

        /// <summary>Sai lệch khép kín cho phép tại neo, mm.</summary>
        public double ClosureTolerance { get; set; } = 5;

        /// <summary>Mốc cao độ của chữ EL trên isometric. Isometric thường ghi theo tâm ống.</summary>
        public ElevationDatum Datum { get; set; } = ElevationDatum.CL;

        /// <summary>Đường kính ngoài theo DN để đổi cao độ đáy ống về tâm; nếu không có thì dùng bảng ASME B36.10 sẵn.</summary>
        public Func<int, double?>? OdLookup { get; set; }

        /// <summary>Quy ước cho bản ISOGEN xuất DWG hoặc DXF, đã đổi mm sang điểm.</summary>
        public static IsoOptions ForIsogen()
        {
            return new IsoOptions
            {
                PipeLineWidth = 2.0,
                PipeLayers = new List<string> { "PIPE" },
                MinDimTextHeight = 3.5,
                BoxEdgeMin = 6,
                CornerGap = 14,
                FittingGap = 45,
                UseSpanDimensions = true,
            };
        }
    }

    public sealed class IsoSegment
    {
        public Point2 A { get; set; }

        public Point2 B { get; set; }

        public IsoAxis Axis { get; set; }

        public bool Bridged { get; set; }

        /// <summary>Hướng thật đơn vị theo chiều A tới B của đoạn chéo.</summary>
        public Vector3? SkewDir { get; set; }

        /// <summary>Cỡ riêng của đoạn khi ô số thứ tự ống chỉ vào, 0 là theo tuyến.</summary>
        public int Dn { get; set; }

        public string? Note { get; set; }

        public List<string> Sources { get; } = new List<string>();

        public List<IsoDimension> Dimensions { get; } = new List<IsoDimension>();

        public double PaperLength => A.DistanceTo(B);
    }

    public sealed class IsoDimension
    {
        public double Value { get; set; }

        public IsoAxis Axis { get; set; }

        public Point2 TextAt { get; set; }

        public string Handle { get; set; } = string.Empty;

        /// <summary><c>text_direction</c>, <c>leader</c>, <c>span</c>, <c>span_split</c>.</summary>
        public string By { get; set; } = string.Empty;

        /// <summary>Hai đầu đường kích thước trên giấy nếu tìm thấy.</summary>
        public Point2? LineA { get; set; }

        public Point2? LineB { get; set; }
    }

    /// <summary>Kích thước đo giữa hai đỉnh của mạng ống, suy từ đường gióng ở hai đầu đường kích thước.</summary>
    public sealed class IsoSpan
    {
        public Point2 From { get; set; }

        public Point2 To { get; set; }

        public IsoDimension Dim { get; set; } = new IsoDimension();
    }

    public sealed class IsoAnchor
    {
        public double E { get; set; }

        public double N { get; set; }

        public double El { get; set; }

        /// <summary>Sai khi neo chỉ có cao độ, không có toạ độ mặt bằng; gốc tuyến đặt tại 0, 0.</summary>
        public bool HasPlan { get; set; } = true;

        public Point2 TextAt { get; set; }

        public Point2? Node { get; set; }

        public string Handle { get; set; } = string.Empty;

        public string? Note { get; set; }
    }

    public sealed class IsoClosure
    {
        public IsoAnchor Anchor { get; set; } = new IsoAnchor();

        public Point3? Derived { get; set; }

        public double? ErrorMm { get; set; }

        public bool Reached => Derived.HasValue;
    }

    public sealed class IsoResult
    {
        public List<IsoSegment> Segments { get; } = new List<IsoSegment>();

        public List<IsoAnchor> Anchors { get; } = new List<IsoAnchor>();

        public List<IsoDimension> UnassignedDimensions { get; } = new List<IsoDimension>();

        public List<IsoSpan> Spans { get; } = new List<IsoSpan>();

        /// <summary>Toạ độ thật tại từng đỉnh trên giấy, sau khi đi từ neo đầu.</summary>
        public Dictionary<Point2, Point3> Positions { get; } = new Dictionary<Point2, Point3>();

        public List<IsoClosure> Closures { get; } = new List<IsoClosure>();

        public List<string> Notes { get; } = new List<string>();

        public string? LineTag { get; set; }

        public int Dn { get; set; }

        public int Bridged { get; set; }

        public int Corners { get; set; }

        public List<Point2> OpenEnds { get; } = new List<Point2>();

        public List<BomRow> Bom { get; } = new List<BomRow>();

        public List<IsoCallout> Callouts { get; } = new List<IsoCallout>();

        public List<IsoBom.CutPiece> CutPieces { get; } = new List<IsoBom.CutPiece>();

        /// <summary>Chữ nối tiếp trên tờ: CONN. TO, SEE ISO, CONNECT TO FUTURE, kèm cao độ nếu có.</summary>
        public List<string> Continuations { get; } = new List<string>();

        /// <summary>Cao độ tâm ống ghi rời tại đỉnh trên giấy, dùng làm ràng buộc khi gán kích thước.</summary>
        public Dictionary<Point2, double> ElevationConstraints { get; } = new Dictionary<Point2, double>();

        public Point2? Start { get; set; }
    }

    /// <summary>
    /// Đồ thị isometric trên giấy và toạ độ thật, I1 và I3. Thuật toán được thử trước bằng tools/iso_graph.py trên bản mẫu PDF,
    /// rồi mở rộng cho bản ISOGEN: co vẽ bằng cung, cao độ FL và đáy ống, không có toạ độ mặt bằng, kích thước gắn theo hai đầu
    /// đường gióng, đoạn chéo 45 độ nằm ngang, nhánh đứng cụt, cấu kiện xếp chồng ngoài đầu hở.
    /// </summary>
    public sealed class IsoGraph
    {
        private static readonly Dictionary<int, double> DefaultOd = new Dictionary<int, double>
        {
            [15] = 21.3, [20] = 26.7, [25] = 33.4, [32] = 42.2, [40] = 48.3, [50] = 60.3, [65] = 73.0, [80] = 88.9, [100] = 114.3,
            [125] = 141.3, [150] = 168.3, [200] = 219.1, [250] = 273.1, [300] = 323.9, [350] = 355.6, [400] = 406.4, [450] = 457.2,
            [500] = 508.0, [600] = 610.0, [700] = 711.0, [800] = 813.0, [900] = 914.0,
        };

        private readonly IsoOptions _o;

        public IsoGraph(IsoOptions? options = null)
        {
            _o = options ?? new IsoOptions();
        }

        private bool IsPipeLine(SourceEntity l) => (l.LineWidth ?? 0) >= _o.PipeLineWidth && (_o.PipeLayers.Count == 0 || _o.PipeLayers.Contains(l.Layer, StringComparer.OrdinalIgnoreCase));

        public IsoResult Analyze(IReadOnlyList<SourceEntity> entities, double pageWidth, double pageHeight)
        {
            var r = new IsoResult();
            var lines = entities.Where(e => e.Kind == SourceKind.Line && e.Points != null && e.Points.Count == 2).ToList();
            var texts = entities.Where(e => e.Kind == SourceKind.Text && !string.IsNullOrWhiteSpace(e.Text) && e.Center.HasValue).ToList();
            var thin = lines.Where(l => !IsPipeLine(l) && l.Points![0].Plan.DistanceTo(l.Points[1].Plan) > 3)
                .Select(l => (A: l.Points![0].Plan, B: l.Points[1].Plan, Axis: AxisOf(l.Points[0].Plan, l.Points[1].Plan))).ToList();

            // 1. Nét ống dày trên trục isometric, snap đầu mút. Nét dày không theo trục là đoạn chéo.
            var points = new List<Point2>();
            Point2 Snap(Point2 p)
            {
                foreach (var q in points)
                {
                    if (q.DistanceTo(p) <= _o.Snap) return q;
                }

                points.Add(p);
                return p;
            }

            foreach (var l in lines.Where(IsPipeLine))
            {
                var a = l.Points![0].Plan;
                var b = l.Points[1].Plan;
                var axis = AxisOf(a, b);
                if (a.DistanceTo(b) < 2) continue;
                if (axis == null && a.DistanceTo(b) < 8) continue;
                var seg = new IsoSegment { A = Snap(a), B = Snap(b), Axis = axis ?? IsoAxis.Skew };
                seg.Sources.Add(l.Handle);
                r.Segments.Add(seg);
            }

            // 1b. Góc co: ISOGEN vẽ co bằng cung nên hai nét khác trục không chạm nhau; kéo cả hai tới giao điểm.
            JoinCorners(r, points);

            // 2. Nối khe tại ký hiệu phụ kiện: đầu hở tới đỉnh gần nhất cùng trục, phía trước.
            for (var pass = 0; pass < 3; pass++)
            {
                var degree = Degrees(r.Segments);
                foreach (var p in points.Where(p => degree.TryGetValue(p, out var d) && d == 1).ToList())
                {
                    if (Degrees(r.Segments)[p] != 1) continue;
                    var seg = r.Segments.First(s => s.A == p || s.B == p);
                    if (seg.Axis == IsoAxis.Skew) continue;
                    var other = seg.A == p ? seg.B : seg.A;
                    Point2? best = null;
                    var bestD = _o.FittingGap;
                    foreach (var q in points)
                    {
                        if (q == p || q == other) continue;
                        var ax = AxisOf(p, q);
                        var d = p.DistanceTo(q);
                        var forward = (q - p).Dot(p - other) > 0;
                        if (ax == seg.Axis && forward && d < bestD)
                        {
                            best = q;
                            bestD = d;
                        }
                    }

                    if (best.HasValue)
                    {
                        var bridge = new IsoSegment { A = p, B = best.Value, Axis = seg.Axis, Bridged = true };
                        bridge.Sources.AddRange(seg.Sources);
                        r.Segments.Add(bridge);
                        r.Bridged++;
                    }
                }
            }

            var degrees = Degrees(r.Segments);
            r.OpenEnds.AddRange(points.Where(p => degrees.TryGetValue(p, out var d) && d == 1));

            // 2b. Hướng thật của đoạn chéo: giải vectơ trên giấy theo hai trục ngang; có chữ 45° gần thì hai thành phần bằng nhau.
            ResolveSkews(r, texts);

            // 3. Neo toạ độ: E, N, EL kề nhau; gắn với đầu hở gần chữ nhất.
            var ordered = texts.OrderByDescending(t => t.Center!.Value.Y).ThenBy(t => t.Center!.Value.X).ToList();
            foreach (var t in ordered.Where(t => t.Text == "E"))
            {
                var n = Nearest(ordered, t, "N");
                var el = Nearest(ordered, t, "EL");
                var ev = ValueAfter(ordered, t);
                var nv = n != null ? ValueAfter(ordered, n) : null;
                var elv = el != null ? ValueAfter(ordered, el) : null;
                if (ev == null || nv == null || elv == null) continue;
                var anchor = new IsoAnchor { E = ev.Value, N = nv.Value, El = elv.Value, TextAt = t.Center!.Value.Plan, Handle = t.Handle };
                // Chữ của điểm nối tiếp nằm phía trước đầu hở theo hướng ống. Neo của nhánh vẽ nét mỏng không gắn vào tuyến chính.
                var forwardEnds = ForwardEnds(r, anchor.TextAt);
                if (forwardEnds.Count > 0) anchor.Node = forwardEnds.OrderBy(p => p.DistanceTo(anchor.TextAt)).First();
                else anchor.Note = "không có đầu hở nào phía trước chữ, có thể là neo của nhánh vẽ nét mỏng";
                r.Anchors.Add(anchor);
            }

            // 4. Bảng vật tư trước, vì cỡ tuyến có thể lấy từ dòng ống; rồi nhãn tuyến và cỡ ống.
            r.Bom.AddRange(IsoBom.ReadRows(texts, pageWidth));
            r.CutPieces.AddRange(IsoBom.ReadCutPieces(texts));
            foreach (var t in texts.Where(t => Regex.IsMatch(t.Text!, "^(CONN|SEE ISO|FROM|CONT)", RegexOptions.IgnoreCase)))
            {
                var below = texts.Where(x => x != t && Math.Abs(x.Center!.Value.X - t.Center!.Value.X) < 30 && t.Center.Value.Y - x.Center.Value.Y > 0 && t.Center.Value.Y - x.Center.Value.Y < 4 * (t.TextHeight ?? 4) * 2.2)
                    .OrderByDescending(x => x.Center!.Value.Y).Select(x => x.Text).Take(4);
                r.Continuations.Add(string.Join(" ", new[] { t.Text }.Concat(below)));
            }
            var lineTag = texts.FirstOrDefault(t => Regex.IsMatch(t.Text!, "^\\d+-[A-Z]+-\\d+-\\d+\"-"))
                ?? texts.Where(t => t.Layer.Equals("FRAME", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(t.Text!, "^[0-9A-Z]+-\\d+-\\d+A-")).OrderByDescending(t => t.Center!.Value.Y).FirstOrDefault();
            r.LineTag = lineTag?.Text;
            var sizes = texts.Select(t => Regex.Match(t.Text!, "^(\\d+)\"?NPD$")).Where(m => m.Success).Select(m => NominalSize.NpsToDn(m.Groups[1].Value)).Where(d => d.HasValue).ToList();
            if (sizes.Count > 0) r.Dn = sizes.GroupBy(d => d).OrderByDescending(g => g.Count()).First().Key!.Value;
            else if (lineTag != null && Regex.IsMatch(lineTag.Text!, "-(\\d+(?:-\\d+/\\d+)?)\"-"))
            {
                r.Dn = NominalSize.NpsToDn(Regex.Match(lineTag.Text!, "-(\\d+(?:-\\d+/\\d+)?)\"-").Groups[1].Value) ?? 0;
            }
            else if (lineTag != null && Regex.IsMatch(lineTag.Text!, "-(\\d+)A\\b"))
            {
                r.Dn = int.Parse(Regex.Match(lineTag.Text!, "-(\\d+)A\\b").Groups[1].Value, CultureInfo.InvariantCulture);
            }

            if (r.Dn == 0)
            {
                var pipeRow = r.Bom.Where(b => b.Type == "pipe" && b.Dn > 0).OrderByDescending(b => b.Dn).FirstOrDefault();
                if (pipeRow != null) r.Dn = pipeRow.Dn;
            }

            // 5. Ô số thứ tự: trước kích thước, vì số trong ô không phải kích thước và ô ống cho biết cỡ riêng của đoạn.
            var thinPairs = thin.Select(t => (t.A, t.B)).ToList();
            r.Callouts.AddRange(IsoBom.ReadCallouts(texts, thinPairs, box => IsBoxed(box, thin), box => FollowLeader(box, thin, r), pageWidth));
            AssignSegmentSizes(r);

            // 6. Kích thước: đường kích thước có hai đầu gióng về hai đỉnh của mạng thì là khoảng cách giữa hai đỉnh đó.
            AssignDimensions(r, thin, texts, pageWidth, pageHeight);
            ResolveSpans(r);

            // 7. Ràng buộc cao độ: EL, FL, B.O.P. gắn với đỉnh theo đường chỉ, hoặc đỉnh gần nhất trên nét đứng.
            ReadElevations(r, texts, thin);

            // 8. Đi từ neo đầu, dùng ràng buộc để chọn tổ hợp kích thước khi gán dư.
            if (r.Anchors.Count == 0) SynthesizeStart(r, texts, thin);
            Walk(r);
            return r;
        }

        private void JoinCorners(IsoResult r, List<Point2> points)
        {
            for (var guard = 0; guard < 200; guard++)
            {
                var degree = Degrees(r.Segments);
                var open = points.Where(p => degree.TryGetValue(p, out var d) && d == 1).ToList();
                (Point2 P, Point2 Q, Point2 X, double D)? best = null;
                foreach (var p in open)
                {
                    var sp = r.Segments.First(s => s.A == p || s.B == p);
                    foreach (var q in open)
                    {
                        if (q == p) continue;
                        var sq = r.Segments.First(s => s.A == q || s.B == q);
                        if (sq == sp || (sp.Axis == sq.Axis && sp.Axis != IsoAxis.Skew)) continue;
                        var x = Intersect(sp.A, sp.B, sq.A, sq.B);
                        if (!x.HasValue) continue;
                        var dp = p.DistanceTo(x.Value);
                        var dq = q.DistanceTo(x.Value);
                        // Giao điểm phải nằm phía ngoài đầu hở, không nằm giữa đoạn.
                        var otherP = sp.A == p ? sp.B : sp.A;
                        var otherQ = sq.A == q ? sq.B : sq.A;
                        if ((x.Value - p).Dot(p - otherP) < -_o.Snap || (x.Value - q).Dot(q - otherQ) < -_o.Snap) continue;
                        if (dp <= _o.CornerGap && dq <= _o.CornerGap && (best == null || dp + dq < best.Value.D)) best = (p, q, x.Value, dp + dq);
                    }
                }

                if (best == null) return;
                var (bp, bq, bx, _) = best.Value;
                var index = points.FindIndex(pt => pt != bp && pt != bq && pt.DistanceTo(bx) <= _o.Snap);
                var node = index >= 0 ? points[index] : bx;
                if (index < 0) points.Add(node);
                foreach (var s in r.Segments)
                {
                    if (s.A == bp || s.A == bq) s.A = node;
                    if (s.B == bp || s.B == bq) s.B = node;
                }

                points.Remove(bp);
                points.Remove(bq);
                r.Corners++;
            }
        }

        private static Point2? Intersect(Point2 a1, Point2 a2, Point2 b1, Point2 b2)
        {
            var u = a2 - a1;
            var v = b2 - b1;
            var den = u.Cross(v);
            if (Math.Abs(den) < 1e-9) return null;
            var t = (b1 - a1).Cross(v) / den;
            return a1 + u * t;
        }

        private void ResolveSkews(IsoResult r, List<SourceEntity> texts)
        {
            var e = AxisVector(IsoAxis.East);
            var n = AxisVector(IsoAxis.North);
            foreach (var s in r.Segments.Where(s => s.Axis == IsoAxis.Skew))
            {
                var p = s.B - s.A;
                // p = a·e + b·n
                var den = e.Cross(n);
                var a = p.Cross(n) / den;
                var b = e.Cross(p) / den;
                var mid = new Segment2(s.A, s.B).MidPoint;
                // Chữ 45° đặt cạnh co ở đầu đoạn chéo, nên đo khoảng cách tới cả đoạn chứ không chỉ điểm giữa.
                var has45 = texts.Any(t => Regex.IsMatch(t.Text!, "^45") && new Segment2(s.A, s.B).DistanceTo(t.Center!.Value.Plan) < 60);
                Vector3 dir;
                if (has45)
                {
                    dir = new Vector3(Math.Sign(a), Math.Sign(b), 0).Normalized();
                    s.Note = "đoạn chéo 45° nằm ngang";
                }
                else
                {
                    dir = new Vector3(a, b, 0).Normalized();
                    s.Note = "đoạn chéo chưa đọc được góc, lấy hướng theo hình";
                    r.Notes.Add($"Đoạn chéo tại {mid} không có chữ 45°, hướng lấy theo hình vẽ nên chỉ gần đúng.");
                }

                s.SkewDir = dir;
            }
        }

        private List<Point2> ForwardEnds(IsoResult r, Point2 textAt)
        {
            return r.OpenEnds.Where(p =>
            {
                var seg = r.Segments.First(sg => sg.A == p || sg.B == p);
                var other = seg.A == p ? seg.B : seg.A;
                return (textAt - p).Dot(p - other) > 0;
            }).ToList();
        }

        /// <summary>Ô số thứ tự của ống đặt cỡ riêng cho đoạn gần nhất, rồi lan theo chuỗi thẳng hàng.</summary>
        private void AssignSegmentSizes(IsoResult r)
        {
            var byItem = r.Bom.ToDictionary(b => b.Item, b => b);
            foreach (var call in r.Callouts)
            {
                if (!byItem.TryGetValue(call.Item, out var row) || row.Type != "pipe" || row.Dn <= 0) continue;
                var seg = r.Segments.Where(s => !s.Bridged).OrderBy(s => new Segment2(s.A, s.B).DistanceTo(call.BoxAt)).FirstOrDefault();
                if (seg == null || new Segment2(seg.A, seg.B).DistanceTo(call.BoxAt) > 28) continue;
                if (seg.Dn == 0) seg.Dn = row.Dn;
            }

            foreach (var chain in Chains(r))
            {
                var dn = chain.Select(s => s.Dn).FirstOrDefault(d => d > 0);
                if (dn > 0) foreach (var s in chain.Where(s => s.Dn == 0)) s.Dn = dn;
            }
        }

        private void AssignDimensions(IsoResult r, List<(Point2 A, Point2 B, IsoAxis? Axis)> thin, List<SourceEntity> texts, double pageWidth, double pageHeight)
        {
            var isoThin = thin.Where(t => t.Axis.HasValue).ToList();
            var anchorValues = new HashSet<double>(r.Anchors.SelectMany(a => new[] { a.E, a.N, a.El }));
            var skewSegs = r.Segments.Where(s => s.Axis == IsoAxis.Skew).ToList();
            foreach (var t in texts)
            {
                var m = Regex.Match(t.Text!, "^(\\d{1,5})(\\(\\*\\))?$");
                if (!m.Success) continue;
                var value = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var reference = m.Groups[2].Success;
                if (anchorValues.Contains(value) || (t.TextHeight ?? 0) < _o.MinDimTextHeight) continue;
                // Số dưới 10 là số thứ tự phụ kiện hoặc số đoạn cắt, không phải kích thước; khe gioăng 3 mm đọc riêng ở I2.
                if (value < 10) continue;
                var box = t.Bounds ?? Box2.Around(t.Center!.Value.Plan, 1);
                if (box.Center.X > pageWidth * 0.66 || box.Center.Y < pageHeight * 0.18) continue; // bảng vật tư và chân trang
                if (IsBoxed(box, thin)) continue; // số thứ tự phụ kiện trong ô vuông

                IsoAxis? axis = null;
                (Point2 A, Point2 B, IsoAxis? Axis)? dline = null;
                var textAxis = AxisOfAngle(t.RotationDeg ?? 0);
                var by = "text_direction";
                if (textAxis.HasValue)
                {
                    axis = textAxis;
                    var near = isoThin.Where(u => u.Axis == axis && new Segment2(u.A, u.B).DistanceTo(box.Center) <= 14).ToList();
                    if (near.Count > 0) dline = near.OrderBy(u => new Segment2(u.A, u.B).DistanceTo(box.Center)).First();
                }
                else if (skewSegs.Count > 0 && skewSegs.Any(s => ParallelDeg(s.A, s.B, t.RotationDeg ?? 0) && new Segment2(s.A, s.B).DistanceTo(box.Center) <= _o.DimReach))
                {
                    axis = IsoAxis.Skew;
                    var near = thin.Where(u => !u.Axis.HasValue && ParallelDeg(u.A, u.B, t.RotationDeg ?? 0) && new Segment2(u.A, u.B).DistanceTo(box.Center) <= 14).ToList();
                    if (near.Count > 0) dline = near.OrderBy(u => new Segment2(u.A, u.B).DistanceTo(box.Center)).First();
                }
                else
                {
                    by = "leader";
                    var grown = box.Expand(10);
                    foreach (var leader in thin.Where(u => !u.Axis.HasValue && (grown.Contains(u.A) ^ grown.Contains(u.B))))
                    {
                        var far = grown.Contains(leader.A) ? leader.B : leader.A;
                        var hits = isoThin.Where(u => new Segment2(u.A, u.B).DistanceTo(far) <= 4).ToList();
                        if (hits.Count == 0) continue;
                        dline = hits.OrderByDescending(u => u.A.DistanceTo(u.B)).First();
                        axis = dline.Value.Axis;
                        break;
                    }
                }

                var dim = new IsoDimension { Value = value, TextAt = box.Center, Handle = t.Handle, By = by + (reference ? "_ref" : string.Empty) };
                if (!axis.HasValue)
                {
                    r.UnassignedDimensions.Add(dim);
                    continue;
                }

                dim.Axis = axis.Value;
                if (dline.HasValue && _o.UseSpanDimensions)
                {
                    // Mỗi đường kích thước là một nét; các kích thước nối tiếp nhau thẳng hàng không được gộp.
                    dim.LineA = dline.Value.A;
                    dim.LineB = dline.Value.B;
                    var span = MapToChain(r, dim, thin);
                    if (span != null)
                    {
                        r.Spans.Add(span);
                        continue;
                    }

                    // Có đường kích thước nhưng không gióng về chuỗi ống nào: kích thước của phụ kiện, để dành cho cấu kiện.
                    r.UnassignedDimensions.Add(dim);
                    continue;
                }

                var reference2 = dline.HasValue ? new Segment2(dline.Value.A, dline.Value.B).MidPoint : box.Center;
                var candidates = r.Segments.Where(s => s.Axis == axis.Value && !s.Bridged).ToList();
                if (candidates.Count == 0)
                {
                    r.UnassignedDimensions.Add(dim);
                    continue;
                }

                var seg = candidates.OrderBy(s => new Segment2(s.A, s.B).DistanceTo(reference2)).First();
                if (new Segment2(seg.A, seg.B).DistanceTo(reference2) > _o.DimReach)
                {
                    r.UnassignedDimensions.Add(dim);
                    continue;
                }

                seg.Dimensions.Add(dim);
            }
        }

        private bool ParallelDeg(Point2 a, Point2 b, double deg)
        {
            var d1 = ((Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI) % 180 + 180) % 180;
            var d2 = ((deg % 180) + 180) % 180;
            var diff = Math.Abs(d1 - d2);
            return Math.Min(diff, 180 - diff) <= _o.AngleTolDeg;
        }

        /// <summary>
        /// Đường kích thước nằm cách chuỗi đoạn cùng trục một đoạn gióng theo trục khác. Dời hai đầu theo trục gióng về đường của chuỗi,
        /// đầu nào trùng đỉnh thì lấy đỉnh, đầu nào rơi giữa đoạn thì tách đoạn tại đó. Đường kích thước không trùm lên chuỗi là của phụ kiện.
        /// Trục gióng phải có đường gióng thật ở đầu đường kích thước, nếu không hai trục ngang có thể cùng cho một ánh xạ hợp lệ.
        /// </summary>
        private IsoSpan? MapToChain(IsoResult r, IsoDimension dim, List<(Point2 A, Point2 B, IsoAxis? Axis)> thin)
        {
            var d1 = dim.LineA!.Value;
            var d2 = dim.LineB!.Value;
            var dimLen = d1.DistanceTo(d2);
            if (dimLen < 2) return null;
            var u = (d2 - d1).Normalized();
            var shiftAxes = new[] { IsoAxis.East, IsoAxis.North, IsoAxis.Up }.Where(a => dim.Axis == IsoAxis.Skew || a != dim.Axis).ToArray();
            (List<IsoSegment> Chain, double T, double Overlap, Point2 P1, Point2 P2)? best = null;
            foreach (var chain in Chains(r))
            {
                if (chain[0].Axis != dim.Axis) continue;
                var cu = (chain[0].B - chain[0].A).Normalized();
                if (Math.Abs(cu.Cross(u)) > 0.1) continue;
                foreach (var ax in shiftAxes)
                {
                    var v = AxisVector(ax);
                    var den = v.Cross(cu);
                    if (Math.Abs(den) < 1e-6) continue;
                    var t = (chain[0].A - d1).Cross(cu) / den;
                    if (Math.Abs(t) > _o.DimReach) continue;
                    var hasExtension = thin.Any(l => l.Axis == ax && ((l.A.DistanceTo(d1) <= 4 && (l.B - d1).Dot(v) * t > 0) || (l.B.DistanceTo(d1) <= 4 && (l.A - d1).Dot(v) * t > 0)
                        || (l.A.DistanceTo(d2) <= 4 && (l.B - d2).Dot(v) * t > 0) || (l.B.DistanceTo(d2) <= 4 && (l.A - d2).Dot(v) * t > 0)));
                    if (!hasExtension) continue;
                    var p1 = d1 + v * t;
                    var p2 = d2 + v * t;
                    var nodes = chain.SelectMany(s => new[] { s.A, s.B }).Distinct().ToList();
                    var cmin = nodes.Min(nd => (nd - p1).Dot(u));
                    var cmax = nodes.Max(nd => (nd - p1).Dot(u));
                    var lo = Math.Max(0, cmin);
                    var hi = Math.Min(dimLen, cmax);
                    var overlap = Math.Max(0, hi - lo) / dimLen;
                    if (overlap < 0.5) continue;
                    if (best == null || overlap > best.Value.Overlap + 0.05 || (Math.Abs(overlap - best.Value.Overlap) <= 0.05 && Math.Abs(t) < Math.Abs(best.Value.T))) best = (chain, t, overlap, p1, p2);
                }
            }

            if (best == null) return null;
            var from = NodeAt(r, best.Value.Chain, best.Value.P1, u);
            var to = NodeAt(r, best.Value.Chain, best.Value.P2, u);
            if (!from.HasValue || !to.HasValue || from.Value == to.Value) return null;
            dim.By = "span";
            return new IsoSpan { From = from.Value, To = to.Value, Dim = dim };
        }

        /// <summary>Đỉnh của chuỗi tại điểm đã dời: đỉnh có sẵn trong 8 điểm, đầu mút chuỗi nếu rơi ngoài không quá 40 điểm, nếu không thì tách đoạn.</summary>
        private Point2? NodeAt(IsoResult r, List<IsoSegment> chain, Point2 p, Vector2 u)
        {
            var nodes = chain.SelectMany(s => new[] { s.A, s.B }).Distinct().ToList();
            var byDist = nodes.OrderBy(nd => Math.Abs((nd - p).Dot(u))).First();
            var c = (byDist - p).Dot(u);
            if (Math.Abs(c) <= 8) return byDist;
            var cs = nodes.Select(nd => (nd, c: (nd - p).Dot(u))).ToList();
            var minNode = cs.OrderBy(x => x.c).First();
            var maxNode = cs.OrderByDescending(x => x.c).First();
            if (minNode.c > 0 && minNode.c <= 40) return minNode.nd;
            if (maxNode.c < 0 && -maxNode.c <= 40) return maxNode.nd;
            if (minNode.c > 0 || maxNode.c < 0) return null;
            // Rơi giữa đoạn: tách đoạn tại điểm chiếu lên đường chuỗi (ví dụ vị trí giá đỡ).
            var seg = chain.FirstOrDefault(s => (s.A - p).Dot(u) * (s.B - p).Dot(u) < 0);
            if (seg == null) return null;
            var foot = seg.A + (seg.B - seg.A) * new Segment2(seg.A, seg.B).ProjectParameter(p);
            var tail = new IsoSegment { A = foot, B = seg.B, Axis = seg.Axis, Bridged = seg.Bridged, Dn = seg.Dn, SkewDir = seg.SkewDir, Note = "tách tại đầu đường gióng" };
            tail.Sources.AddRange(seg.Sources);
            seg.B = foot;
            r.Segments.Add(tail);
            return foot;
        }

        /// <summary>Chuỗi đoạn thẳng hàng cùng trục nối nhau qua đỉnh chung.</summary>
        private List<List<IsoSegment>> Chains(IsoResult r)
        {
            var result = new List<List<IsoSegment>>();
            var seen = new HashSet<IsoSegment>();
            foreach (var s in r.Segments)
            {
                if (seen.Contains(s)) continue;
                var chain = new List<IsoSegment> { s };
                seen.Add(s);
                var queue = new Queue<IsoSegment>();
                queue.Enqueue(s);
                var u = (s.B - s.A).Normalized();
                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    foreach (var o in r.Segments)
                    {
                        if (seen.Contains(o) || o.Axis != s.Axis) continue;
                        if (!(o.A == cur.A || o.A == cur.B || o.B == cur.A || o.B == cur.B)) continue;
                        var ou = (o.B - o.A).Normalized();
                        if (Math.Abs(ou.Cross(u)) > 0.1) continue;
                        seen.Add(o);
                        chain.Add(o);
                        queue.Enqueue(o);
                    }
                }

                result.Add(chain);
            }

            return result;
        }

        /// <summary>Đổi khoảng cách giữa hai đỉnh thành chiều dài từng đoạn trong chuỗi: đoạn còn thiếu nhận phần dư; thiếu nhiều đoạn thì chia theo tỉ lệ trên giấy.</summary>
        private void ResolveSpans(IsoResult r)
        {
            if (r.Spans.Count == 0) return;
            var chains = Chains(r);
            List<IsoSegment> Between(Point2 a, Point2 b)
            {
                var chain = chains.FirstOrDefault(c => c.Any(s => s.A == a || s.B == a) && c.Any(s => s.A == b || s.B == b));
                if (chain == null) return new List<IsoSegment>();
                var u = (chain[0].B - chain[0].A).Normalized();
                var ca = (a - chain[0].A).Dot(u);
                var cb = (b - chain[0].A).Dot(u);
                var lo = Math.Min(ca, cb);
                var hi = Math.Max(ca, cb);
                return chain.Where(s =>
                {
                    var m = (new Segment2(s.A, s.B).MidPoint - chain[0].A).Dot(u);
                    return m > lo && m < hi;
                }).ToList();
            }

            var pending = r.Spans.ToList();
            for (var guard = 0; guard < 20 && pending.Count > 0; guard++)
            {
                var progressed = false;
                foreach (var sp in pending.ToList())
                {
                    var segs = Between(sp.From, sp.To);
                    if (segs.Count == 0)
                    {
                        pending.Remove(sp);
                        r.UnassignedDimensions.Add(sp.Dim);
                        continue;
                    }

                    var unknown = segs.Where(s => s.Dimensions.Count == 0).ToList();
                    var known = segs.Where(s => s.Dimensions.Count > 0).Sum(s => s.Dimensions.Sum(d => d.Value));
                    if (unknown.Count == 1)
                    {
                        var rest = sp.Dim.Value - known;
                        if (rest < -1) r.Notes.Add($"Kích thước {sp.Dim.Value} nhỏ hơn tổng các đoạn con {known}, bỏ.");
                        else unknown[0].Dimensions.Add(new IsoDimension { Value = Math.Max(0, rest), Axis = sp.Dim.Axis, TextAt = sp.Dim.TextAt, Handle = sp.Dim.Handle, By = "span", LineA = sp.Dim.LineA, LineB = sp.Dim.LineB });
                        pending.Remove(sp);
                        progressed = true;
                    }
                    else if (unknown.Count == 0)
                    {
                        if (Math.Abs(known - sp.Dim.Value) > 2) r.Notes.Add($"Kích thước {sp.Dim.Value} không khớp tổng các đoạn con {known}.");
                        pending.Remove(sp);
                        progressed = true;
                    }
                }

                if (!progressed)
                {
                    // Chia phần dư của khoảng ngắn nhất theo tỉ lệ trên giấy; độ tin cậy thấp hơn và được ghi lại.
                    var sp = pending.OrderBy(x => x.Dim.Value).First();
                    var segs = Between(sp.From, sp.To);
                    var unknown = segs.Where(s => s.Dimensions.Count == 0).ToList();
                    var known = segs.Where(s => s.Dimensions.Count > 0).Sum(s => s.Dimensions.Sum(d => d.Value));
                    var rest = Math.Max(0, sp.Dim.Value - known);
                    var paper = unknown.Sum(s => s.PaperLength);
                    foreach (var s in unknown)
                    {
                        s.Dimensions.Add(new IsoDimension { Value = Math.Round(rest * s.PaperLength / paper), Axis = sp.Dim.Axis, TextAt = sp.Dim.TextAt, Handle = sp.Dim.Handle, By = "span_split" });
                        s.Note = "chiều dài chia theo tỉ lệ trên giấy trong khoảng " + sp.Dim.Value.ToString(CultureInfo.InvariantCulture);
                    }

                    pending.Remove(sp);
                }
            }
        }

        private void ReadElevations(IsoResult r, List<SourceEntity> texts, List<(Point2 A, Point2 B, IsoAxis? Axis)> thin)
        {
            var ordered = texts.OrderByDescending(t => t.Center!.Value.Y).ThenBy(t => t.Center!.Value.X).ToList();
            var allNodes = r.Segments.SelectMany(s => new[] { s.A, s.B }).Distinct().ToList();
            var upPoints = r.Segments.Where(s => s.Axis == IsoAxis.Up).SelectMany(s => new[] { s.A, s.B }).Distinct().ToList();
            var labelRx = new Regex("^(EL|FL|TOS|BOP|B\\.O\\.P\\.?)\\s*([+-]?\\d{3,})$", RegexOptions.IgnoreCase);
            foreach (var t in ordered)
            {
                double value;
                var m = labelRx.Match(t.Text!);
                if (m.Success) value = double.Parse(m.Groups[2].Value.Replace("+", string.Empty), CultureInfo.InvariantCulture);
                else if (t.Text == "EL" || t.Text == "FL")
                {
                    var v = ValueAfter(ordered, t);
                    if (v == null) continue;
                    value = v.Value;
                }
                else continue;

                var c = t.Center!.Value.Plan;
                if (r.Anchors.Any(a => a.TextAt.DistanceTo(c) < 30)) continue;
                var box = t.Bounds ?? Box2.Around(c, 1);
                var isBop = Regex.IsMatch(m.Success ? m.Groups[1].Value : string.Empty, "BOP|B\\.O\\.P", RegexOptions.IgnoreCase)
                    || texts.Any(x => Regex.IsMatch(x.Text!, "^B\\.?O\\.?P\\.?$", RegexOptions.IgnoreCase) && x.Center!.Value.Plan.DistanceTo(c) < 3.5 * (t.TextHeight ?? 4));
                // Khối chữ: các dòng kề nhau của cùng ghi chú, vì đường chỉ xuất phát từ mép khối chứ không từ dòng cao độ.
                var block = box;
                foreach (var x in texts)
                {
                    var xb = x.Bounds ?? Box2.Around(x.Center!.Value.Plan, 1);
                    if (Math.Abs(xb.Center.Y - box.Center.Y) < 4 * (t.TextHeight ?? 4) && xb.Min.X < box.Max.X + 20 && xb.Max.X > box.Min.X - 20) block = block.Union(xb);
                }

                Point2? node = null;
                // Bản ISOGEN đặt cao độ ở cuối đường gióng hoặc đường chỉ; bản PDF mẫu đặt chữ cạnh nét đứng.
                var target = _o.UseSpanDimensions ? FollowLeader(block, thin, r) : null;
                if (target.HasValue && allNodes.Count > 0)
                {
                    // Đường chỉ có thể dừng ở ký hiệu phụ kiện cạnh đỉnh, nên cho 20 điểm.
                    var near = allNodes.OrderBy(p => p.DistanceTo(target.Value)).First();
                    if (near.DistanceTo(target.Value) <= 20) node = near;
                }

                // Không có đường chỉ tới đỉnh: chữ EL đứng cạnh nét đứng như trên bản PDF mẫu.
                if (!node.HasValue && upPoints.Count > 0)
                {
                    var near = upPoints.OrderBy(p => p.DistanceTo(c)).First();
                    if (near.DistanceTo(c) <= 90) node = near;
                }

                if (!node.HasValue) continue;
                var centerline = value;
                if (isBop)
                {
                    var dn = r.Segments.Where(s => s.A == node.Value || s.B == node.Value).Select(s => s.Dn).FirstOrDefault(d => d > 0);
                    if (dn == 0) dn = r.Dn;
                    var od = _o.OdLookup?.Invoke(dn) ?? (DefaultOd.TryGetValue(dn, out var o) ? o : 0);
                    centerline = value + od / 2;
                    r.Notes.Add($"Cao độ {value} là đáy ống, tâm ống {centerline:0.#} với DN{dn}.");
                }

                if (!r.ElevationConstraints.ContainsKey(node.Value)) r.ElevationConstraints[node.Value] = centerline;
                else if (Math.Abs(r.ElevationConstraints[node.Value] - centerline) > 1) r.Notes.Add($"Hai cao độ {r.ElevationConstraints[node.Value]:0.#} và {centerline:0.#} cùng trỏ tới đỉnh {node.Value}, giữ cao độ đầu.");
            }
        }

        /// <summary>Theo đường chỉ từ mép khối chữ qua tối đa bốn nét mỏng nối nhau tới điểm trỏ.</summary>
        private Point2? FollowLeader(Box2 block, List<(Point2 A, Point2 B, IsoAxis? Axis)> thin, IsoResult r)
        {
            var grown = block.Expand(8);
            var starts = thin.Where(l => (grown.Contains(l.A) ^ grown.Contains(l.B)) && l.A.DistanceTo(l.B) > 4 && !block.Expand(2).Contains(grown.Contains(l.A) ? l.B : l.A)).ToList();
            if (starts.Count == 0) return null;
            var nodes = r.Segments.SelectMany(s => new[] { s.A, s.B }).Distinct().ToList();
            Point2? bestEnd = null;
            var bestScore = double.MaxValue;
            // Nét chạm sát mép khối chữ là đường chỉ; đường gióng kích thước chỉ đi ngang qua vùng mở rộng.
            var minTouch = starts.Min(l => block.DistanceTo(grown.Contains(l.A) ? l.A : l.B));
            starts = starts.Where(l => block.DistanceTo(grown.Contains(l.A) ? l.A : l.B) <= minTouch + 1.5).ToList();
            foreach (var start in starts)
            {
                var used = new List<(Point2 A, Point2 B, IsoAxis? Axis)> { start };
                var far = grown.Contains(start.A) ? start.B : start.A;
                var prevDir = (far - (grown.Contains(start.A) ? start.A : start.B)).Normalized();
                for (var hop = 0; hop < 4; hop++)
                {
                    if (nodes.Any(n => n.DistanceTo(far) <= 6)) break;
                    var next = thin.Where(l => !used.Contains(l) && (l.A.DistanceTo(far) <= 1.5 || l.B.DistanceTo(far) <= 1.5) && l.A.DistanceTo(l.B) > 4
                        && !grown.Contains(l.A) && !grown.Contains(l.B))
                        .Select(l => (L: l, End: l.A.DistanceTo(far) <= 1.5 ? l.B : l.A))
                        .Where(x => (x.End - far).Normalized().Dot(prevDir) > -0.2)
                        .OrderByDescending(x => (x.End - far).Normalized().Dot(prevDir)).ToList();
                    if (next.Count == 0) break;
                    used.Add(next[0].L);
                    prevDir = (next[0].End - far).Normalized();
                    far = next[0].End;
                }

                var score = nodes.Count > 0 ? nodes.Min(n => n.DistanceTo(far)) : 0;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestEnd = far;
                }
            }

            return bestEnd;
        }

        /// <summary>Không có neo E, N: gốc đặt tại đầu hở gần chữ nối tiếp nhất, cao độ lấy từ ràng buộc tại đó hoặc gần đó.</summary>
        private void SynthesizeStart(IsoResult r, List<SourceEntity> texts, List<(Point2 A, Point2 B, IsoAxis? Axis)> thin)
        {
            if (r.OpenEnds.Count == 0) return;
            var connTexts = texts.Where(t => Regex.IsMatch(t.Text!, "^(CONN|SEE ISO|FROM|CONT)", RegexOptions.IgnoreCase)).ToList();
            Point2? start = null;
            string? handle = null;
            // Ưu tiên chữ nối tiếp có cao độ ghi gần, vì đầu tuyến cần cao độ gốc.
            foreach (var t in connTexts.OrderBy(t => r.ElevationConstraints.Keys.Count == 0 ? 0 : r.ElevationConstraints.Keys.Min(k => k.DistanceTo(t.Center!.Value.Plan))))
            {
                var c = t.Center!.Value.Plan;
                var block = t.Bounds ?? Box2.Around(c, 1);
                foreach (var x in texts)
                {
                    var xb = x.Bounds ?? Box2.Around(x.Center!.Value.Plan, 1);
                    if (Math.Abs(xb.Center.Y - block.Center.Y) < 4 * (t.TextHeight ?? 4) && xb.Min.X < block.Max.X + 20 && xb.Max.X > block.Min.X - 20) block = block.Union(xb);
                }

                var target = FollowLeader(block, thin, r);
                var ends = target.HasValue ? r.OpenEnds.Where(p => p.DistanceTo(target.Value) <= 20).ToList() : new List<Point2>();
                if (ends.Count == 0) ends = ForwardEnds(r, c).Where(p => p.DistanceTo(c) < 120).ToList();
                if (ends.Count == 0) ends = r.OpenEnds.Where(p => p.DistanceTo(c) < 60).ToList();
                if (ends.Count == 0) continue;
                start = ends.OrderBy(p => p.DistanceTo(target ?? c)).First();
                handle = t.Handle;
                break;
            }

            if (!start.HasValue) start = r.OpenEnds[0];
            double el = 0;
            if (r.ElevationConstraints.TryGetValue(start.Value, out var atStart)) el = atStart;
            else if (r.ElevationConstraints.Count > 0)
            {
                var nearest = r.ElevationConstraints.OrderBy(kv => kv.Key.DistanceTo(start.Value)).First();
                el = nearest.Value;
                r.Notes.Add($"Đầu tuyến không có cao độ, lấy {el:0.#} từ đỉnh gần nhất có ghi cao độ.");
            }
            else r.Notes.Add("Không có cao độ nào trên tờ, cao độ gốc đặt 0.");

            r.Anchors.Add(new IsoAnchor { E = 0, N = 0, El = el, HasPlan = false, Node = start, TextAt = start.Value, Handle = handle ?? string.Empty, Note = "không có toạ độ mặt bằng trên tờ, gốc đặt tại đầu tuyến" });
            r.Notes.Add("Tờ không ghi toạ độ E, N; gốc 0, 0 đặt tại đầu tuyến, trục Đông giả định theo góc 30°.");
            r.ElevationConstraints.Remove(start.Value);
        }

        /// <summary>
        /// Số thứ tự phụ kiện nằm trong ô: ô vẽ bằng bốn nét ngắn khép quanh chữ, có thể xoay theo trục isometric.
        /// Bốn nét phải khép thành vòng và vòng đó chứa tâm chữ; ô của số bên cạnh không tính.
        /// </summary>
        private bool IsBoxed(Box2 box, List<(Point2 A, Point2 B, IsoAxis? Axis)> thin)
        {
            var c = box.Center;
            var reach = Math.Max(box.Width, box.Height) + 12;
            // Cạnh ô dài hơn ngưỡng, loại các cạnh của đầu mũi tên kích thước.
            var near = thin.Where(t => t.A.DistanceTo(t.B) >= _o.BoxEdgeMin && t.A.DistanceTo(t.B) <= 2 * reach && t.A.DistanceTo(c) <= reach && t.B.DistanceTo(c) <= reach).ToList();
            if (near.Count < 4) return false;
            foreach (var first in near)
            {
                var ring = new List<(Point2 A, Point2 B, IsoAxis? Axis)> { first };
                var cur = first.B;
                for (var step = 0; step < 6; step++)
                {
                    var next = near.FirstOrDefault(t => !ring.Contains(t) && (t.A.DistanceTo(cur) <= 0.6 || t.B.DistanceTo(cur) <= 0.6));
                    if (next.A == next.B) break;
                    ring.Add(next);
                    cur = next.A.DistanceTo(cur) <= 0.6 ? next.B : next.A;
                    if (cur.DistanceTo(first.A) <= 0.6 && ring.Count >= 4)
                    {
                        var poly = Box2.FromPoints(ring.SelectMany(t => new[] { t.A, t.B }));
                        if (poly.Expand(0.5).Contains(c)) return true;
                        break;
                    }
                }
            }

            return false;
        }

        private void Walk(IsoResult r)
        {
            if (r.Anchors.Count == 0 || r.Segments.Count == 0)
            {
                r.Notes.Add("Không có neo toạ độ, không dựng được toạ độ thật.");
                return;
            }

            var start = r.Anchors.FirstOrDefault(a => a.Node.HasValue);
            if (start == null)
            {
                r.Notes.Add("Không neo nào gắn được vào đầu hở.");
                return;
            }

            r.Start = start.Node;
            r.Positions[start.Node!.Value] = new Point3(start.E, start.N, start.El);
            var exact = new HashSet<Point2> { start.Node.Value };
            var parent = new Dictionary<Point2, (Point2 From, IsoSegment Seg)>();
            var stack = new Stack<Point2>();
            stack.Push(start.Node.Value);
            var visited = new HashSet<IsoSegment>();
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                foreach (var s in r.Segments.Where(s => !visited.Contains(s) && (s.A == p || s.B == p)).ToList())
                {
                    visited.Add(s);
                    var q = s.A == p ? s.B : s.A;
                    if (r.Positions.ContainsKey(q)) continue;
                    parent[q] = (p, s);

                    // Ràng buộc tại q: cao độ ghi rời hoặc neo. Tổng kích thước của chuỗi đoạn cùng trục kể từ đỉnh chính xác
                    // gần nhất phải bằng hiệu toạ độ; sai thì chọn lại tập con kích thước trên cả chuỗi đó.
                    double? requiredTotal = null;
                    var chain = new List<IsoSegment> { s };
                    var back = p;
                    while (!exact.Contains(back) && parent.TryGetValue(back, out var pr) && pr.Seg.Axis == s.Axis)
                    {
                        chain.Add(pr.Seg);
                        back = pr.From;
                    }

                    var origin = r.Positions[back];
                    if (s.Axis == IsoAxis.Up && r.ElevationConstraints.TryGetValue(q, out var zq)) requiredTotal = Math.Abs(zq - origin.Z);
                    var reconciled = false;
                    if (!requiredTotal.HasValue && exact.Contains(back) && s.Axis != IsoAxis.Skew)
                    {
                        // Nhiều neo có thể trỏ cùng một đầu hở khi nhánh vẽ nét mỏng; thử từng neo, neo nào khớp tổ hợp thì nhận.
                        foreach (var anchor in r.Anchors.Where(a => a.Node == q && (a.HasPlan || s.Axis == IsoAxis.Up)))
                        {
                            var need = s.Axis == IsoAxis.East ? Math.Abs(anchor.E - origin.X) : s.Axis == IsoAxis.North ? Math.Abs(anchor.N - origin.Y) : Math.Abs(anchor.El - origin.Z);
                            if (ReconcileChain(chain, need, r))
                            {
                                requiredTotal = need;
                                reconciled = true;
                                break;
                            }
                        }
                    }

                    if (reconciled || (requiredTotal.HasValue && ReconcileChain(chain, requiredTotal.Value, r)))
                    {
                        // Tính lại vị trí dọc chuỗi từ đỉnh gốc.
                        var cur = back;
                        var pos = origin;
                        for (var i = chain.Count - 1; i >= 0; i--)
                        {
                            var cs = chain[i];
                            var nxt = cs.A == cur ? cs.B : cs.A;
                            pos = Step(pos, cs, cur, nxt);
                            r.Positions[nxt] = pos;
                            cur = nxt;
                        }

                        exact.Add(q);
                    }
                    else
                    {
                        r.Positions[q] = Step(r.Positions[p], s, p, q);
                        // Khe nối không có kích thước không làm mất độ chính xác đã biết.
                        if (requiredTotal.HasValue || (exact.Contains(p) && s.Dimensions.Count == 0 && s.Bridged)) exact.Add(q);
                    }

                    stack.Push(q);
                }
            }

            // Vòng hai: với mỗi neo chưa khớp, đối chiếu cả đường đi từ neo đầu theo từng trục ngang và chọn lại tổ hợp.
            foreach (var a in r.Anchors.Where(a => a != start && a.HasPlan && a.Node.HasValue && parent.ContainsKey(a.Node!.Value)))
            {
                var path = new List<IsoSegment>();
                var cur = a.Node!.Value;
                while (parent.TryGetValue(cur, out var pr))
                {
                    path.Add(pr.Seg);
                    cur = pr.From;
                }

                var startPos = r.Positions[start.Node.Value];
                var ok = true;
                foreach (var axis in new[] { IsoAxis.East, IsoAxis.North })
                {
                    var group = path.Where(sg => sg.Axis == axis).ToList();
                    if (group.Count == 0) continue;
                    var need = axis == IsoAxis.East ? Math.Abs(a.E - startPos.X) : Math.Abs(a.N - startPos.Y);
                    var signed = group.Sum(sg => sg.Dimensions.Sum(d => d.Value));
                    if (Math.Abs(signed - need) <= 10) continue;
                    if (!ReconcileChain(group, need, r)) ok = false;
                }

                if (!ok) continue;
                // Tính lại vị trí dọc đường đi.
                path.Reverse();
                cur = start.Node.Value;
                var pos = startPos;
                foreach (var sg in path)
                {
                    var nxt = sg.A == cur ? sg.B : sg.A;
                    pos = Step(pos, sg, cur, nxt);
                    r.Positions[nxt] = pos;
                    cur = nxt;
                }
            }

            foreach (var a in r.Anchors.Where(a => a != start && a.Node.HasValue))
            {
                var c = new IsoClosure { Anchor = a };
                if (r.Positions.TryGetValue(a.Node!.Value, out var got))
                {
                    c.Derived = got;
                    c.ErrorMm = a.HasPlan
                        ? Math.Round(Math.Sqrt(Math.Pow(got.X - a.E, 2) + Math.Pow(got.Y - a.N, 2) + Math.Pow(got.Z - a.El, 2)), 1)
                        : Math.Round(Math.Abs(got.Z - a.El), 1);
                }

                r.Closures.Add(c);
            }

            // Cao độ ghi rời tại đỉnh không phải neo cũng là một phép kiểm tra khép kín theo trục đứng.
            foreach (var kv in r.ElevationConstraints)
            {
                if (r.Anchors.Any(a => a.Node == kv.Key)) continue;
                var c = new IsoClosure { Anchor = new IsoAnchor { El = kv.Value, HasPlan = false, Node = kv.Key, TextAt = kv.Key, Note = "cao độ ghi rời" } };
                if (r.Positions.TryGetValue(kv.Key, out var got))
                {
                    c.Derived = got;
                    c.ErrorMm = Math.Round(Math.Abs(got.Z - kv.Value), 1);
                }

                r.Closures.Add(c);
            }
        }

        private Point3 Step(Point3 from, IsoSegment s, Point2 fromNode, Point2 toNode)
        {
            var length = s.Dimensions.Sum(d => d.Value);
            if (s.Axis == IsoAxis.Skew)
            {
                var dir = s.SkewDir ?? Vector3.UnitX;
                if (toNode == s.A) dir = -dir;
                return from + dir * length;
            }

            var sign = Sign(s.Axis, fromNode, toNode);
            return s.Axis == IsoAxis.East ? new Point3(from.X + sign * length, from.Y, from.Z)
                : s.Axis == IsoAxis.North ? new Point3(from.X, from.Y + sign * length, from.Z)
                : new Point3(from.X, from.Y, from.Z + sign * length);
        }

        /// <summary>Tổng kích thước của cả chuỗi phải bằng độ dài cần có; sai thì tìm tập con có tổng đúng, giữ nhiều kích thước nhất.</summary>
        private static bool ReconcileChain(List<IsoSegment> chain, double required, IsoResult r)
        {
            var all = chain.SelectMany(c => c.Dimensions.Select(d => (Seg: c, Dim: d))).ToList();
            var sum = all.Sum(x => x.Dim.Value);
            if (Math.Abs(sum - required) <= 1.0) return true;
            if (all.Count == 0 || all.Count > 14) return false;
            List<int>? best = null;
            var n = all.Count;
            for (var mask = 1; mask < (1 << n); mask++)
            {
                double t = 0;
                for (var i = 0; i < n; i++) if ((mask & (1 << i)) != 0) t += all[i].Dim.Value;
                if (Math.Abs(t - required) > 10.0) continue; // dung sai nhỏ cho khe gioăng 3 mm chưa đọc ở I1
                var idx = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).ToList();
                var bestErr = best == null ? double.MaxValue : Math.Abs(best.Sum(i => all[i].Dim.Value) - required);
                var err = Math.Abs(t - required);
                if (best == null || err < bestErr - 0.5 || (Math.Abs(err - bestErr) <= 0.5 && idx.Count > best.Count)) best = idx;
            }

            if (best == null) return false;
            var keep = new HashSet<IsoDimension>(best.Select(i => all[i].Dim));
            foreach (var (seg, dim) in all.Where(x => !keep.Contains(x.Dim)))
            {
                seg.Dimensions.Remove(dim);
                r.UnassignedDimensions.Add(dim);
                r.Notes.Add($"Kích thước {dim.Value} bị loại khỏi trục {seg.Axis} vì ràng buộc {required:0} mm.");
            }

            return true;
        }

        /// <summary>Đổ ra Piping IR: mỗi chuỗi thẳng hàng thành một đoạn chạy, chuỗi đứng thành đoạn chạy thẳng đứng; cấu kiện từ ô số thứ tự.</summary>
        public PipingModel ToModel(IsoResult r, string? file)
        {
            var model = new PipingModel { Source = new SourceInfo { File = file, EntityCount = r.Segments.Count } };
            if (r.Positions.Count == 0) return model;
            var line = new Line { Id = "line-001", Tag = r.LineTag ?? "ISO", Prov = new Provenance { Rule = "ISO-LINE-01" } };
            model.Lines.Add(line);

            // Mỗi chuỗi đoạn thẳng hàng là một đoạn chạy từ đỉnh đầu tới đỉnh cuối; mối hàn và giá đỡ ở giữa không cắt đoạn chạy.
            // Chuỗi đứng thành đoạn chạy thẳng đứng, nhờ đó nhánh đứng cụt vẫn được dựng.
            var n = 0;
            foreach (var chain in Chains(r))
            {
                var nodes = chain.SelectMany(s => new[] { s.A, s.B }).Distinct().Where(p => r.Positions.ContainsKey(p)).ToList();
                if (nodes.Count < 2) continue;
                var u = (chain[0].B - chain[0].A).Normalized();
                var ordered = nodes.OrderBy(p => (p - chain[0].A).Dot(u)).ToList();
                var a = r.Positions[ordered.First()];
                var b = r.Positions[ordered.Last()];
                var vertical = chain[0].Axis == IsoAxis.Up;
                if (vertical ? Math.Abs(a.Z - b.Z) < 0.5 : a.Plan.DistanceTo(b.Plan) < 0.5) continue;
                n++;
                var confidence = chain.All(s => s.Dimensions.Count == 0) ? 0.5 : chain.Any(s => s.Dimensions.Any(d => d.By == "span_split")) ? 0.8 : 1.0;
                var chainDn = chain.Select(s => s.Dn).FirstOrDefault(d => d > 0);
                model.Runs.Add(new Run
                {
                    Id = "run-" + n.ToString("000", CultureInfo.InvariantCulture),
                    LineId = line.Id,
                    Dn = chainDn > 0 ? chainDn : r.Dn,
                    Elevation = new Elevation { Raw = a.Z, Datum = _o.Datum },
                    EndElevation = vertical ? b.Z : (double?)null,
                    PlanPoints = vertical ? new List<Point2> { a.Plan } : new List<Point2> { a.Plan, b.Plan },
                    TagRef = r.LineTag != null ? new TagRef { Text = r.LineTag, By = "iso", Confidence = 1.0 } : null,
                    Prov = new Provenance { Sources = chain.SelectMany(s => s.Sources.Concat(s.Dimensions.Select(d => d.Handle))).Distinct().ToList(), Rule = vertical ? "ISO-RUN-03" : chain[0].Axis == IsoAxis.Skew ? "ISO-RUN-02" : "ISO-RUN-01", Confidence = confidence },
                });
            }

            // Cấu kiện từ ô số thứ tự. Điểm trỏ nằm ngoài đầu hở của ống, như mặt bích, van, bích mù ở cuối nhánh, thì xếp chồng
            // từ đầu hở ra theo thứ tự trên giấy; nằm trên đoạn nào thì đặt vào đoạn đó theo tham số t, toạ độ thật nội suy.
            var byItem = r.Bom.ToDictionary(b => b.Item, b => b);
            var cn = 0;
            var stacks = new Dictionary<Point2, List<(Component Component, double Along)>>();
            foreach (var call in r.Callouts)
            {
                if (!byItem.TryGetValue(call.Item, out var row)) continue;
                if (row.Type == "pipe" || row.Type == "other" || row.Type == "support" || row.Type == "tee" || row.Type == "elbow") continue;
                var target = call.Target ?? call.BoxAt;

                Point2? stackEnd = null;
                var stackAlong = 0.0;
                if (call.Target.HasValue)
                {
                    foreach (var end in r.OpenEnds.Where(e => r.Positions.ContainsKey(e)))
                    {
                        var seg = r.Segments.First(s => s.A == end || s.B == end);
                        var other = seg.A == end ? seg.B : seg.A;
                        var dir = (end - other).Normalized();
                        var along = (target - end).Dot(dir);
                        var across = Math.Abs((target - end).Cross(dir));
                        if (along > -2 && along <= 60 && across <= 10)
                        {
                            stackEnd = end;
                            stackAlong = along;
                            break;
                        }
                    }
                }

                if (stackEnd.HasValue)
                {
                    var seg = r.Segments.First(s => s.A == stackEnd.Value || s.B == stackEnd.Value);
                    var dn = row.Dn > 0 ? row.Dn : seg.Dn > 0 ? seg.Dn : r.Dn;
                    cn++;
                    var component = MakeComponent(cn, call, row, r.Positions[stackEnd.Value], Vector3.UnitX, dn, 0.8, "ISO-CMP-02");
                    model.Components.Add(component);
                    if (!stacks.TryGetValue(stackEnd.Value, out var list)) stacks[stackEnd.Value] = list = new List<(Component, double)>();
                    list.Add((component, stackAlong));
                    continue;
                }

                IsoSegment? best = null;
                // Có đường chỉ thì điểm trỏ phải gần ống; không có thì ô thường đặt sát ống, cho tầm với rộng hơn một chút.
                var bestD = call.Target.HasValue ? 15.0 : 28.0;
                foreach (var s in r.Segments)
                {
                    var d = new Segment2(s.A, s.B).DistanceTo(target);
                    if (d < bestD && r.Positions.ContainsKey(s.A) && r.Positions.ContainsKey(s.B))
                    {
                        bestD = d;
                        best = s;
                    }
                }

                if (best == null)
                {
                    model.Issues.Add(new Issue
                    {
                        Id = "issue-iso-" + (model.Issues.Count + 1).ToString("000", CultureInfo.InvariantCulture),
                        RuleId = "R-ISO-03",
                        Severity = Severity.Info,
                        Message = $"Mục {call.Item} ({row.Type}, {row.Size}) không trỏ tới đoạn ống chính nào, có thể nằm trên nhánh vẽ nét mỏng.",
                        Sources = new List<string> { call.Handle },
                    });
                    continue;
                }

                var paper = new Segment2(best.A, best.B);
                var t = Math.Max(0, Math.Min(1, paper.ProjectParameter(target)));
                var pa = r.Positions[best.A];
                var pb = r.Positions[best.B];
                var axis3 = pb - pa;
                var axisDir = axis3.Length > 0 ? axis3.Normalized() : Vector3.UnitX;
                var pos = pa + axis3 * t;
                var dnOn = row.Dn > 0 ? row.Dn : best.Dn > 0 ? best.Dn : r.Dn;
                cn++;
                model.Components.Add(MakeComponent(cn, call, row, pos, axisDir, dnOn, call.Target.HasValue ? 0.9 : 0.6, "ISO-CMP-01"));
            }

            PlaceAtNodes(r, model);
            AssignComponentLengths(r, model);

            // Xếp chồng ngoài đầu hở: mặt bích SO đầu tiên lồng lên ống, mặt bích tại đầu hở; các cấu kiện sau nối tiếp ra ngoài,
            // mặt bích trong chồng quay mặt về cấu kiện trước nó.
            foreach (var kv in stacks)
            {
                var end = kv.Key;
                var seg = r.Segments.First(s => s.A == end || s.B == end);
                var other = seg.A == end ? seg.B : seg.A;
                var pEnd = r.Positions[end];
                var dir3 = pEnd - r.Positions[other];
                var axisDir = dir3.Length > 0 ? dir3.Normalized() : Vector3.UnitX;
                var offset = 0.0;
                var first = true;
                foreach (var (component, _) in kv.Value.OrderBy(x => x.Along))
                {
                    var blind = IsBlind(component);
                    if (first && component.Type == "flange" && !blind)
                    {
                        component.Axis = axisDir;
                        component.Position = pEnd - axisDir * (component.Length / 2);
                        component.Attributes!["placement"] = new AttributeValue { Value = "lồng lên đầu ống, mặt bích tại đầu hở", By = "iso" };
                    }
                    else
                    {
                        component.Axis = component.Type == "flange" ? -axisDir : axisDir;
                        component.Position = pEnd + axisDir * (offset + component.Length / 2);
                        offset += component.Length;
                    }

                    first = false;
                }
            }

            foreach (var c in r.Closures.Where(c => c.Reached && c.ErrorMm > _o.ClosureTolerance))
            {
                model.Issues.Add(new Issue
                {
                    Id = "issue-iso-" + (model.Issues.Count + 1).ToString("000", CultureInfo.InvariantCulture),
                    RuleId = "R-ISO-01",
                    Severity = Severity.Warning,
                    Message = c.Anchor.HasPlan
                        ? string.Format(CultureInfo.InvariantCulture, "Khép kín tại neo E {0} N {1} EL {2} lệch {3} mm, suy ra E {4:0} N {5:0} EL {6:0}.", c.Anchor.E, c.Anchor.N, c.Anchor.El, c.ErrorMm, c.Derived!.Value.X, c.Derived.Value.Y, c.Derived.Value.Z)
                        : string.Format(CultureInfo.InvariantCulture, "Cao độ ghi {0} lệch {1} mm so với cao độ suy ra {2:0}.", c.Anchor.El, c.ErrorMm, c.Derived!.Value.Z),
                    Sources = new List<string> { c.Anchor.Handle },
                });
            }

            foreach (var d in r.UnassignedDimensions)
            {
                model.Issues.Add(new Issue
                {
                    Id = "issue-iso-" + (model.Issues.Count + 1).ToString("000", CultureInfo.InvariantCulture),
                    RuleId = "R-ISO-02",
                    Severity = Severity.Info,
                    Message = $"Kích thước {d.Value} tại ({d.TextAt.X:0}, {d.TextAt.Y:0}) chưa gán được cho đoạn nào.",
                    Sources = new List<string> { d.Handle },
                });
            }

            return model;
        }

        /// <summary>Khoảng cách từ điểm trỏ tới đỉnh đo dọc trục của chuỗi tại đỉnh; vuông góc quá 10 pt thì coi như xa.</summary>
        private static double AlongDistance(IsoResult r, Point2 node, Point2 target)
        {
            var seg = r.Segments.FirstOrDefault(s => s.A == node || s.B == node);
            if (seg == null) return node.DistanceTo(target);
            var u = (seg.B - seg.A).Normalized();
            var w = target - node;
            return Math.Abs(w.Cross(u)) > 10 ? double.MaxValue : Math.Abs(w.Dot(u));
        }

        private static bool IsBlind(Component c) =>
            c.Attributes != null && c.Attributes.TryGetValue("description", out var d) && (d.Value ?? string.Empty).ToUpperInvariant().Contains("BLIND");

        /// <summary>
        /// Đặt cấu kiện theo đỉnh của đồ thị thay vì theo vị trí giấy, ISO-CMP-03. Trên bản ISOGEN nét ống dừng tại ký hiệu mặt bích và
        /// kích thước đi tới mối nối, nên đỉnh tại khe ký hiệu chính là mặt bích: mặt bích SO đặt mặt tại đỉnh, cổ lồng về phía ống;
        /// van nằm trong khe thì dài đúng bằng khe, giữa khe; olet đặt tại đỉnh rẽ nhánh của ống chính với hướng nhánh.
        /// </summary>
        private void PlaceAtNodes(IsoResult r, PipingModel model)
        {
            var degrees = Degrees(r.Segments);
            Point3 Pos(Point2 p) => r.Positions[p];
            foreach (var c in model.Components.Where(c => c.Prov?.Rule == "ISO-CMP-01"))
            {
                var call = r.Callouts.First(k => k.Handle == c.Prov!.Sources[0]);
                var target = call.Target ?? call.BoxAt;

                if (c.Type == "olet")
                {
                    var junctions = r.Positions.Keys.Where(p => degrees.TryGetValue(p, out var d) && d >= 3).OrderBy(p => p.DistanceTo(target)).Take(1).ToList();
                    if (junctions.Count == 0 || junctions[0].DistanceTo(target) > 30) continue;
                    var junction = junctions[0];
                    var at = r.Segments.Where(s => s.A == junction || s.B == junction).ToList();
                    // Nhánh là đoạn có trục khác với trục của phần lớn các đoạn tại đỉnh.
                    var mainAxis = at.GroupBy(s => s.Axis).OrderByDescending(g => g.Count()).First().Key;
                    var branch = at.FirstOrDefault(s => s.Axis != mainAxis) ?? at.FirstOrDefault(s => s.Dn > 0 && s.Dn != c.Dn);
                    if (branch == null) continue;
                    var otherEnd = branch.A == junction ? branch.B : branch.A;
                    if (!r.Positions.ContainsKey(otherEnd)) continue;
                    var dir = Pos(otherEnd) - Pos(junction);
                    if (dir.Length <= 0) continue;
                    var main = at.First(s => s.Axis == mainAxis);
                    var mainDir = Pos(main.B) - Pos(main.A);
                    c.Position = Pos(junction);
                    c.Axis = mainDir.Length > 0 ? mainDir.Normalized() : c.Axis;
                    c.Attributes!["branch"] = new AttributeValue { Value = string.Format(CultureInfo.InvariantCulture, "{0:0.####},{1:0.####},{2:0.####}", dir.Normalized().X, dir.Normalized().Y, dir.Normalized().Z), By = "iso" };
                    c.Attributes["placement"] = new AttributeValue { Value = "tại đỉnh rẽ nhánh trên ống chính", By = "iso" };
                    c.Prov!.Rule = "ISO-CMP-03";
                    continue;
                }

                if (c.Type == "flange")
                {
                    // Ký hiệu mặt bích SO có hai đỉnh sát nhau: đỉnh mối nối, là đầu khe mang kích thước mặt đối mặt của van, và đỉnh nét ống
                    // bắt đầu lại sau cổ. Đường chỉ chạm ký hiệu ở độ cao bất kỳ nên so khoảng cách dọc trục; ưu tiên đỉnh của khe có kích thước
                    // đúng, rồi đỉnh khe, rồi đầu hở.
                    var near = r.Positions.Keys.Select(p => (p, d: AlongDistance(r, p, target))).Where(x => x.d <= 12).OrderBy(x => x.d).Select(x => x.p).ToList();
                    bool Exact(Point2 p) => r.Segments.Any(s => s.Bridged && (s.A == p || s.B == p) && s.Dimensions.Any(d => d.By == "span"));
                    bool AnyGap(Point2 p) => r.Segments.Any(s => s.Bridged && (s.A == p || s.B == p));
                    var nodes = near.Where(Exact)
                        .Concat(near.Where(AnyGap))
                        .Concat(near.Where(p => degrees.TryGetValue(p, out var d) && d == 1))
                        .Take(1).ToList();
                    if (nodes.Count == 0) continue;
                    var node = nodes[0];
                    var at = r.Segments.Where(s => s.A == node || s.B == node).ToList();
                    var gap = at.FirstOrDefault(s => s.Bridged);
                    Point2 faceToward;
                    if (gap != null) faceToward = gap.A == node ? gap.B : gap.A;
                    else if (at.Count == 1) faceToward = node + (node - (at[0].A == node ? at[0].B : at[0].A));
                    else continue;
                    Vector3 dir;
                    if (r.Positions.TryGetValue(faceToward, out var ft)) dir = ft - Pos(node);
                    else
                    {
                        var s0 = at[0];
                        var o = s0.A == node ? s0.B : s0.A;
                        dir = Pos(node) - Pos(o);
                    }

                    if (dir.Length <= 0) continue;
                    dir = dir.Normalized();
                    // Axis luôn chỉ từ cổ về mặt bích: bích SO mặt quay ra khe, bích mù mặt quay về ống.
                    c.Axis = IsBlind(c) ? -dir : dir;
                    c.Position = IsBlind(c) ? Pos(node) + dir * (c.Length / 2) : Pos(node) - dir * (c.Length / 2);
                    c.Attributes!["placement"] = new AttributeValue { Value = gap != null ? "mặt bích tại đỉnh khe ký hiệu" : "mặt bích tại đầu hở", By = "iso" };
                    c.Prov!.Rule = "ISO-CMP-03";
                    continue;
                }

                // Van, côn thu và cấu kiện khác nằm trong khe ký hiệu: dài bằng khe, đặt giữa khe.
                foreach (var s in r.Segments.Where(s => s.Bridged && r.Positions.ContainsKey(s.A) && r.Positions.ContainsKey(s.B)))
                {
                    var paper = new Segment2(s.A, s.B);
                    var len = s.PaperLength;
                    if (len < 1) continue;
                    var u = (s.B - s.A).Normalized();
                    var w = target - s.A;
                    var along = w.Dot(u);
                    var across = Math.Abs(w.Cross(u));
                    if (along < -3 || along > len + 3 || across > 10) continue;
                    var pa = Pos(s.A);
                    var pb = Pos(s.B);
                    var span = pb - pa;
                    if (span.Length < 1) break;
                    var exact = s.Dimensions.Any(d => d.By == "span");
                    c.Position = pa + span * 0.5;
                    c.Axis = span.Normalized();
                    c.Length = span.Length;
                    c.Attributes!["length"] = new AttributeValue { Value = span.Length.ToString("0.#", CultureInfo.InvariantCulture), By = exact ? "span" : "paper", SourceHandle = s.Dimensions.FirstOrDefault()?.Handle };
                    c.Attributes["placement"] = new AttributeValue { Value = "giữa khe ký hiệu, dài bằng khoảng cách hai đỉnh", By = "iso" };
                    c.Prov!.Rule = "ISO-CMP-03";
                    break;
                }
            }
        }

        private static Component MakeComponent(int n, IsoCallout call, BomRow row, Point3 pos, Vector3 axisDir, int dn, double confidence, string rule)
        {
            var catalog = CatalogLength(row, dn);
            var length = catalog ?? DefaultLength(row, dn);
            var attributes = new Dictionary<string, AttributeValue>
            {
                ["description"] = new AttributeValue { Value = row.Description, By = "bom", SourceHandle = call.Handle },
                ["size"] = new AttributeValue { Value = row.Size, By = "bom" },
                ["length"] = new AttributeValue { Value = length.ToString(CultureInfo.InvariantCulture), By = catalog.HasValue ? ComponentDims.Source : "default" },
            };
            if (row.Dn2.HasValue) attributes["dn2"] = new AttributeValue { Value = row.Dn2.Value.ToString(CultureInfo.InvariantCulture), By = "bom" };
            return new Component
            {
                Id = "cmp-" + n.ToString("000", CultureInfo.InvariantCulture),
                Type = row.Type,
                Position = pos,
                Axis = axisDir,
                Dn = dn,
                Length = length,
                Tag = "mục " + call.Item,
                Attributes = attributes,
                Prov = new Provenance { Sources = new List<string> { call.Handle }, Rule = rule, Confidence = confidence },
            };
        }

        /// <summary>
        /// Kích thước chưa gán cho ống mà dải gióng của nó trùm lên điểm trỏ của ô là chiều dài cấu kiện, ví dụ 330 của côn thu, 127 của van.
        /// Mỗi kích thước chỉ cho một cấu kiện, chọn cấu kiện có điểm trỏ gần giữa dải nhất.
        /// </summary>
        private void AssignComponentLengths(IsoResult r, PipingModel model)
        {
            var targets = model.Components.ToDictionary(c => c.Id, c =>
            {
                var call = r.Callouts.First(k => k.Handle == c.Prov!.Sources[0]);
                return call.Target ?? call.BoxAt;
            });
            foreach (var d in r.UnassignedDimensions.Where(d => d.LineA.HasValue && d.Value >= 10 && d.Value <= 2000))
            {
                var d1 = d.LineA!.Value;
                var u = d.LineB!.Value - d1;
                var len = u.Length;
                if (len < 2) continue;
                u = u.Normalized();
                Component? bestC = null;
                var bestScore = double.MaxValue;
                foreach (var c in model.Components.Where(c => c.Attributes["length"].By == "default" || c.Attributes["length"].By == ComponentDims.Source))
                {
                    var target = targets[c.Id];
                    foreach (var ax in new[] { IsoAxis.East, IsoAxis.North, IsoAxis.Up })
                    {
                        var v = AxisVector(ax);
                        var den = u.Cross(v);
                        if (Math.Abs(den) < 0.1) continue;
                        var w = target - d1;
                        var beta = u.Cross(w) / den;
                        var alpha = (w - v * beta).Dot(u);
                        if (alpha < -4 || alpha > len + 4 || Math.Abs(beta) > _o.DimReach) continue;
                        var score = Math.Abs(alpha - len / 2) / len + Math.Abs(beta) / _o.DimReach * 0.1;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestC = c;
                        }
                    }
                }

                if (bestC == null) continue;
                bestC.Length = d.Value;
                bestC.Attributes["length"] = new AttributeValue { Value = d.Value.ToString(CultureInfo.InvariantCulture), By = "dimension", SourceHandle = d.Handle };
            }
        }

        /// <summary>Chiều dài dọc trục theo danh mục JIS 10K chưa ký xác nhận, I2: mặt bích SO cả cổ, bích mù chiều dày, van bướm cả hai gioăng, van mặt bích, côn thu, olet.</summary>
        private static double? CatalogLength(BomRow row, int dn)
        {
            var upper = row.Description.ToUpperInvariant();
            switch (row.Type)
            {
                case "flange":
                {
                    var f = ComponentDims.Flange10K(dn);
                    if (f == null) return null;
                    return upper.Contains("BLIND") ? f.T : f.HubLength;
                }

                case "valve_butterfly":
                {
                    var ff = ComponentDims.ValveFaceToFace(row.Type, dn);
                    return ff.HasValue ? ff.Value + 2 * ComponentDims.Gasket : (double?)null;
                }

                case "valve_globe":
                case "valve_gate":
                case "valve_ball":
                case "valve_check":
                case "valve":
                    return ComponentDims.ValveFaceToFace(row.Type, dn);
                case "reducer":
                    return ComponentDims.ConcentricReducerLength(Math.Max(dn, row.Dn2 ?? 0));
                case "olet":
                    return ComponentDims.HalfCoupling(row.Dn2 ?? dn)?.L;
                default:
                    return null;
            }
        }

        /// <summary>Chiều dài dọc trục của cấu kiện khi chưa tra danh mục: mặt bích theo lớp áp, van theo B16.10 xấp xỉ, côn thu theo B16.9.</summary>
        private static double DefaultLength(BomRow row, int dn)
        {
            switch (row.Type)
            {
                case "flange": return Math.Max(60, dn * 0.25);
                case "reducer": return Math.Max(100, dn * 0.6);
                case "valve_gate": case "valve": case "valve_ball": return Math.Max(150, dn * 1.0);
                case "valve_globe": case "valve_check": return Math.Max(150, dn * 1.3);
                case "valve_butterfly": return Math.Max(50, dn * 0.33);
                case "olet": return Math.Max(40, dn * 0.3);
                case "instrument": return Math.Max(100, dn * 0.5);
                case "nipple": return 100;
                default: return Math.Max(50, dn * 0.5);
            }
        }

        private IsoAxis? AxisOf(Point2 a, Point2 b)
        {
            var deg = (Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI + 360) % 180;
            return AxisOfAngle(deg);
        }

        private IsoAxis? AxisOfAngle(double deg)
        {
            deg = ((deg % 180) + 180) % 180;
            if (Math.Abs(deg - _o.EastDeg) <= _o.AngleTolDeg) return IsoAxis.East;
            if (Math.Abs(deg - _o.NorthDeg) <= _o.AngleTolDeg) return IsoAxis.North;
            if (Math.Abs(deg - _o.UpDeg) <= _o.AngleTolDeg) return IsoAxis.Up;
            return null;
        }

        private Vector2 AxisVector(IsoAxis axis)
        {
            var deg = axis == IsoAxis.East ? _o.EastDeg : axis == IsoAxis.North ? _o.NorthDeg : _o.UpDeg;
            return new Vector2(Math.Cos(deg * Math.PI / 180), Math.Sin(deg * Math.PI / 180));
        }

        private int Sign(IsoAxis axis, Point2 from, Point2 to) => (to - from).Dot(AxisVector(axis)) >= 0 ? 1 : -1;

        private static Dictionary<Point2, int> Degrees(List<IsoSegment> segs)
        {
            var d = new Dictionary<Point2, int>();
            foreach (var s in segs)
            {
                d[s.A] = d.TryGetValue(s.A, out var a) ? a + 1 : 1;
                d[s.B] = d.TryGetValue(s.B, out var b) ? b + 1 : 1;
            }

            return d;
        }

        private static SourceEntity? Nearest(List<SourceEntity> texts, SourceEntity from, string text)
        {
            var c = from.Center!.Value.Plan;
            return texts.Where(t => t.Text == text && t != from && Math.Abs(t.Center!.Value.X - c.X) < 6 && t.Center.Value.Y < c.Y && c.Y - t.Center.Value.Y < 30)
                .OrderBy(t => c.DistanceTo(t.Center!.Value.Plan)).FirstOrDefault();
        }

        private static double? ValueAfter(List<SourceEntity> texts, SourceEntity label)
        {
            var c = label.Center!.Value.Plan;
            // Chữ cao độ có thể viết xoay theo trục, nên chỉ cần giá trị nằm gần nhãn, không cần cùng hàng.
            var candidate = texts.Where(t => t != label && Regex.IsMatch(t.Text!, "^[+-]?\\d{4,}$") && c.DistanceTo(t.Center!.Value.Plan) < 45)
                .OrderBy(t => c.DistanceTo(t.Center!.Value.Plan)).FirstOrDefault();
            return candidate != null ? double.Parse(candidate.Text!.Replace("+", string.Empty), CultureInfo.InvariantCulture) : (double?)null;
        }
    }
}
