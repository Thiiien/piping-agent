using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Rules;

namespace PipingAgent.Core.Export
{
    /// <summary>Bảng khối lượng, S6-02 và FR-EXP-01: chiều dài ống theo tuyến và DN, số co, tê, van, mặt bích.</summary>
    public static class QuantityExport
    {
        public sealed class Row
        {
            public string Line { get; set; } = string.Empty;

            public int Dn { get; set; }

            public double PipeLengthM { get; set; }

            public int Elbows { get; set; }

            public int Tees { get; set; }

            public int Valves { get; set; }

            public int Flanges { get; set; }

            public int OtherComponents { get; set; }
        }

        public static List<Row> Rows(PipingModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var lines = model.Lines.ToDictionary(l => l.Id, l => l.Tag, StringComparer.Ordinal);
            string Tag(string id) => lines.TryGetValue(id, out var t) && t.Length > 0 ? t : "(không nhãn)";
            var rows = new Dictionary<(string, int), Row>();
            Row Get(string line, int dn)
            {
                if (!rows.TryGetValue((line, dn), out var r))
                {
                    r = new Row { Line = line, Dn = dn };
                    rows[(line, dn)] = r;
                }

                return r;
            }

            var segById = model.Segments.ToDictionary(s => s.Id, StringComparer.Ordinal);
            foreach (var s in model.Segments) Get(Tag(s.LineId), s.Dn).PipeLengthM += (s.Length - s.TrimA - s.TrimB) / 1000.0;
            foreach (var n in model.Nodes)
            {
                var seg = n.Segments.Select(id => segById.TryGetValue(id, out var s) ? s : null).FirstOrDefault(s => s != null);
                if (seg == null) continue;
                var row = Get(Tag(seg.LineId), n.Segments.Select(id => segById.TryGetValue(id, out var s) ? s.Dn : 0).Max());
                if (n.Kind == NodeKind.Elbow) row.Elbows++;
                else if (n.Kind == NodeKind.Tee) row.Tees++;
            }

            foreach (var c in model.Components)
            {
                var line = segById.TryGetValue(c.SegmentId ?? string.Empty, out var s) ? Tag(s.LineId) : "(không nhãn)";
                var row = Get(line, c.Dn);
                if (c.Type.StartsWith("valve", StringComparison.OrdinalIgnoreCase)) row.Valves++;
                else if (c.Type.StartsWith("flange", StringComparison.OrdinalIgnoreCase)) row.Flanges++;
                else row.OtherComponents++;
            }

            return rows.Values.OrderBy(r => r.Line, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Dn).ToList();
        }

        /// <summary>CSV có BOM UTF-8 để Excel mở đúng tiếng Việt, dấu chấm phẩy làm dấu phân cách như Excel bản Việt.</summary>
        public static string ToCsv(PipingModel model, char separator = ';')
        {
            var sb = new StringBuilder();
            sb.Append('﻿');
            sb.AppendLine(string.Join(separator.ToString(), "Tuyến", "DN", "Ống (m)", "Co", "Tê", "Van", "Mặt bích", "Cấu kiện khác"));
            foreach (var r in Rows(model))
            {
                sb.AppendLine(string.Join(separator.ToString(), Csv(r.Line), r.Dn.ToString(CultureInfo.InvariantCulture),
                    r.PipeLengthM.ToString("0.00", CultureInfo.InvariantCulture), r.Elbows, r.Tees, r.Valves, r.Flanges, r.OtherComponents));
            }

            var total = Rows(model);
            sb.AppendLine(string.Join(separator.ToString(), "Tổng", string.Empty, total.Sum(r => r.PipeLengthM).ToString("0.00", CultureInfo.InvariantCulture),
                total.Sum(r => r.Elbows), total.Sum(r => r.Tees), total.Sum(r => r.Valves), total.Sum(r => r.Flanges), total.Sum(r => r.OtherComponents)));
            return sb.ToString();
        }

        private static string Csv(string s) => s.IndexOfAny(new[] { ';', ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>Báo cáo chuyển đổi, S6-03 và FR-EXP-02: thống kê, vấn đề, vấn đề đã bỏ qua kèm lý do. Markdown, đọc được bằng mọi trình soạn thảo.</summary>
    public static class ConversionReport
    {
        public static string ToMarkdown(RecognitionResult result, string? title = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var m = result.Model;
            var w = new StringWriter(CultureInfo.InvariantCulture);
            w.WriteLine("# Báo cáo chuyển đổi " + (title ?? m.Source.File ?? string.Empty));
            w.WriteLine();
            w.WriteLine($"- Bản vẽ: `{m.Source.File}`, dấu vân tay `{m.Source.Fingerprint}`, đọc lúc {m.Source.ReadAt}");
            w.WriteLine($"- Hồ sơ: `{m.Profile?.Id}` v{m.Profile?.Version}. Phần mềm: {m.Generator?.Name} {m.Generator?.Version}" + (m.Generator?.CatalogVerified == false ? ". Danh mục chưa ký xác nhận" : string.Empty));
            w.WriteLine();
            w.WriteLine("## Thống kê");
            w.WriteLine();
            w.WriteLine("```");
            RecognitionReport.Write(result, w, maxLayers: 10, maxIssues: 0);
            w.WriteLine("```");
            w.WriteLine();
            w.WriteLine("## Khối lượng");
            w.WriteLine();
            w.WriteLine("| Tuyến | DN | Ống (m) | Co | Tê | Van | Mặt bích |");
            w.WriteLine("|---|---|---|---|---|---|---|");
            foreach (var r in QuantityExport.Rows(m))
            {
                w.WriteLine($"| {r.Line} | {r.Dn} | {r.PipeLengthM:0.00} | {r.Elbows} | {r.Tees} | {r.Valves} | {r.Flanges} |");
            }

            w.WriteLine();
            var open = m.Issues.Where(i => i.Status == IssueStatus.Open).ToList();
            w.WriteLine($"## Vấn đề đang mở: {open.Count}");
            w.WriteLine();
            foreach (var g in open.GroupBy(i => i.RuleId).OrderBy(g => g.Min(i => i.Severity)).ThenBy(g => g.Key, StringComparer.Ordinal))
            {
                w.WriteLine($"### {g.Key} {RecognitionReport.SeverityText(g.First().Severity)} x{g.Count()}: {RuleCatalog.Find(g.Key)?.Description}");
                w.WriteLine();
                foreach (var i in g.Take(50)) w.WriteLine($"- {i.Id}: {i.Message}" + (i.Sources.Count > 0 ? $" (handle {string.Join(", ", i.Sources.Take(4))})" : string.Empty));
                if (g.Count() > 50) w.WriteLine($"- ... còn {g.Count() - 50}");
                w.WriteLine();
            }

            var waived = m.Issues.Where(i => i.Status == IssueStatus.Waived).ToList();
            w.WriteLine($"## Vấn đề đã bỏ qua: {waived.Count}");
            w.WriteLine();
            foreach (var i in waived) w.WriteLine($"- {i.Id} {i.RuleId}: {i.Message} Lý do: {i.Waiver?.Reason} ({i.Waiver?.By}, {i.Waiver?.At})");
            if (m.Decisions.Count > 0)
            {
                w.WriteLine();
                w.WriteLine($"## Quyết định của người dùng: {m.Decisions.Count}" + (result.Decisions.Orphaned > 0 ? $", {result.Decisions.Orphaned} mồ côi" : string.Empty));
                w.WriteLine();
                foreach (var d in m.Decisions) w.WriteLine($"- {d.Id} {d.Op} {string.Join(" ", d.Target.Select(kv => kv.Key + "=" + kv.Value))}" + (d.Orphan ? " MỒ CÔI" : string.Empty));
            }

            return w.ToString();
        }
    }

    /// <summary>
    /// Bộ chấm điểm hồi quy, S6-05 và mục 13.4: so IR với đáp án <c>expected.json</c> của bộ bản vẽ chuẩn.
    /// Đáp án là các con số đếm và chiều dài theo tuyến, do chuyên gia lập ở S1-18, S2-17, S3-18.
    /// </summary>
    public static class RegressionScorer
    {
        public sealed class Expected
        {
            public string? Drawing { get; set; }

            public int? Runs { get; set; }

            public int? Segments { get; set; }

            public int? Elbows { get; set; }

            public int? Tees { get; set; }

            public int? Risers { get; set; }

            public int? Nozzles { get; set; }

            public int? Valves { get; set; }

            public int? Equipment { get; set; }

            public double? PipeLengthM { get; set; }

            /// <summary>Số hiệu tuyến phải có.</summary>
            public List<string> Lines { get; set; } = new List<string>();

            /// <summary>Tuyến sang chiều dài tâm, m.</summary>
            public Dictionary<string, double> LineLengthM { get; set; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Dung sai tương đối cho chiều dài, mặc định 2%.</summary>
            public double LengthTolerance { get; set; } = 0.02;
        }

        public sealed class Score
        {
            public List<string> Checks { get; } = new List<string>();

            public int Passed { get; set; }

            public int Failed { get; set; }

            /// <summary>Tuyến có trong đáp án nhưng IR không có, hoặc ngược lại: sai im lặng.</summary>
            public List<string> MissingLines { get; } = new List<string>();

            public List<string> ExtraLines { get; } = new List<string>();

            public double SilentErrorRate => Checks.Count == 0 ? 0 : (double)(MissingLines.Count + ExtraLines.Count) / Math.Max(1, Checks.Count);

            public bool Ok => Failed == 0 && MissingLines.Count == 0;
        }

        public static Score Compare(PipingModel model, Expected expected)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            var s = new Score();
            void Check(string name, int? want, int got)
            {
                if (!want.HasValue) return;
                var ok = want.Value == got;
                s.Checks.Add($"{(ok ? "ĐẠT" : "SAI")} {name}: mong {want}, được {got}");
                if (ok) s.Passed++; else s.Failed++;
            }

            void CheckLength(string name, double? want, double got)
            {
                if (!want.HasValue) return;
                var ok = Math.Abs(want.Value - got) <= Math.Max(0.05, want.Value * expected.LengthTolerance);
                s.Checks.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1}: mong {2:0.00} m, được {3:0.00} m", ok ? "ĐẠT" : "SAI", name, want, got));
                if (ok) s.Passed++; else s.Failed++;
            }

            Check("đoạn chạy", expected.Runs, model.Runs.Count);
            Check("đoạn ống", expected.Segments, model.Segments.Count);
            Check("co", expected.Elbows, model.Nodes.Count(n => n.Kind == NodeKind.Elbow));
            Check("tê", expected.Tees, model.Nodes.Count(n => n.Kind == NodeKind.Tee));
            Check("ống đứng", expected.Risers, model.Segments.Count(x => x.Kind == SegmentKind.Riser));
            Check("đầu chờ", expected.Nozzles, model.Nodes.Count(n => n.Kind == NodeKind.Nozzle));
            Check("van", expected.Valves, model.Components.Count(c => c.Type.StartsWith("valve", StringComparison.OrdinalIgnoreCase)));
            Check("thiết bị", expected.Equipment, model.Equipment.Count);
            CheckLength("tổng chiều dài", expected.PipeLengthM, model.Segments.Sum(x => x.Length) / 1000.0);

            var got = new HashSet<string>(model.Lines.Select(l => l.Tag), StringComparer.OrdinalIgnoreCase);
            foreach (var l in expected.Lines) if (!got.Contains(l)) s.MissingLines.Add(l);
            if (expected.Lines.Count > 0) foreach (var l in got) if (!expected.Lines.Contains(l, StringComparer.OrdinalIgnoreCase)) s.ExtraLines.Add(l);
            var byLine = model.Lines.ToDictionary(l => l.Tag, l => model.Segments.Where(x => x.LineId == l.Id).Sum(x => x.Length) / 1000.0, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in expected.LineLengthM) CheckLength("tuyến " + kv.Key, kv.Value, byLine.TryGetValue(kv.Key, out var v) ? v : 0);
            return s;
        }
    }
}
