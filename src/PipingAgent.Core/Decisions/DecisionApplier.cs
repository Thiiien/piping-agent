using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Decisions
{
    public sealed class DecisionStats
    {
        public int Applied { get; set; }

        public int Orphaned { get; set; }

        public int Skipped { get; set; }

        public List<string> Notes { get; } = new List<string>();

        public override string ToString() => $"{Applied} quyết định đã áp, {Orphaned} mồ côi, {Skipped} bỏ qua";
    }

    /// <summary>
    /// Áp quyết định của người dùng lên kết quả nhận diện, S5-05 và ADR-06. Quyết định lưu tách khỏi IR và khớp lại
    /// theo handle nguồn dưới khoá <c>source</c> của <see cref="Decision.Target"/>, vì định danh IR đổi giữa hai lần chạy.
    /// Quyết định không khớp được phần tử nào được đánh dấu mồ côi và tạo vấn đề R-DEC-01 để người dùng xử lý.
    /// Mỗi thao tác được gọi ở đúng bước của chuỗi nhận diện: bỏ đối tượng trước phân loại, gắn nhãn sau khi dựng đoạn chạy,
    /// đổi loại nút sau đồ thị, miễn trừ sau bộ luật.
    /// </summary>
    public sealed class DecisionApplier
    {
        private readonly IReadOnlyList<Decision> _decisions;
        private readonly HashSet<string> _matched = new HashSet<string>(StringComparer.Ordinal);

        public DecisionApplier(IEnumerable<Decision>? decisions)
        {
            _decisions = (decisions ?? Enumerable.Empty<Decision>()).Where(d => d.Status != DecisionStatus.Rejected).ToList();
        }

        public DecisionStats Stats { get; } = new DecisionStats();

        public bool Any => _decisions.Count > 0;

        private IEnumerable<Decision> Of(DecisionOp op) => _decisions.Where(d => d.Op == op);

        private static string? Source(Decision d) => d.Target.TryGetValue("source", out var s) ? s : null;

        /// <summary>Bước 1: bỏ đối tượng nguồn khỏi đầu vào.</summary>
        public IReadOnlyList<SourceEntity> FilterEntities(IReadOnlyList<SourceEntity> entities)
        {
            var ignored = new HashSet<string>(Of(DecisionOp.IgnoreSource).Select(Source).Where(s => s != null)!, StringComparer.OrdinalIgnoreCase);
            if (ignored.Count == 0) return entities;
            var present = new HashSet<string>(entities.Select(e => e.Handle), StringComparer.OrdinalIgnoreCase);
            foreach (var d in Of(DecisionOp.IgnoreSource))
            {
                var s = Source(d);
                if (s != null && present.Contains(s)) Mark(d);
            }

            return entities.Where(e => !ignored.Contains(e.Handle)).ToList();
        }

        /// <summary>Bước 2: nối hai đầu ống và tách tuyến trên đoạn chạy, trước khi gắn nhãn.</summary>
        public void ApplyToRuns(List<Run> runs, double snap)
        {
            foreach (var d in Of(DecisionOp.Connect))
            {
                var a = FindRunBySource(runs, Source(d));
                var b = FindRunBySource(runs, d.Target.TryGetValue("source2", out var s2) ? s2 : null);
                if (a == null || b == null || a == b) continue;
                var (ea, eb) = ClosestEnds(a, b);
                var mid = new Point2((ea.Point.X + eb.Point.X) / 2, (ea.Point.Y + eb.Point.Y) / 2);
                SetEnd(a, ea.AtStart, mid);
                SetEnd(b, eb.AtStart, mid);
                Mark(d);
            }

            foreach (var d in Of(DecisionOp.SplitLine))
            {
                var run = FindRunBySource(runs, Source(d));
                if (run == null || run.PlanPoints.Count < 3 || !TryPoint(d.Value, out var at)) continue;
                var index = -1;
                var best = double.MaxValue;
                for (var i = 1; i < run.PlanPoints.Count - 1; i++)
                {
                    var dist = run.PlanPoints[i].DistanceTo(at);
                    if (dist < best)
                    {
                        best = dist;
                        index = i;
                    }
                }

                if (index < 0) continue;
                var tail = run.PlanPoints.Skip(index).ToList();
                run.PlanPoints = run.PlanPoints.Take(index + 1).ToList();
                runs.Add(new Run
                {
                    Id = run.Id + "-b",
                    PlanPoints = tail,
                    Elevation = new Elevation { Datum = ElevationDatum.Unknown },
                    Prov = new Provenance { Sources = run.Prov?.Sources.ToList() ?? new List<string>(), Rule = RecognitionRules.RunFromChain, DecidedBy = DecidedBy.User },
                });
                Mark(d);
            }
        }

        /// <summary>Bước 3: gắn nhãn, DN, cao độ lên đoạn chạy sau khi gắn nhãn tự động, ghi đè kết quả tự động.</summary>
        public void ApplyToModelRuns(PipingModel model, ConventionProfile profile)
        {
            var parser = new TagParser(profile);
            foreach (var d in Of(DecisionOp.AssignTag))
            {
                var run = FindRunBySource(model.Runs, Source(d)) ?? FindRunById(model.Runs, d.Target);
                if (run == null) continue;
                var text = d.Value?.ValueKind == JsonValueKind.String ? d.Value.Value.GetString() : null;
                var tag = parser.Parse(text);
                if (tag == null)
                {
                    Stats.Notes.Add($"{d.Id}: nhãn '{text}' không khớp mẫu nào trong hồ sơ.");
                    Stats.Skipped++;
                    continue;
                }

                var line = model.Lines.FirstOrDefault(l => string.Equals(l.Tag, tag.Line, StringComparison.OrdinalIgnoreCase));
                if (line == null)
                {
                    line = new Line
                    {
                        Id = "line-" + (model.Lines.Count + 1).ToString("000", CultureInfo.InvariantCulture),
                        Tag = tag.Line,
                        Service = tag.Service,
                        Spec = tag.Spec,
                        Prov = new Provenance { Rule = RecognitionRules.LineFromTag, DecidedBy = DecidedBy.User },
                    };
                    model.Lines.Add(line);
                }

                run.LineId = line.Id;
                if (tag.Dn.HasValue) run.Dn = tag.Dn.Value;
                if (tag.Elevation.HasValue) run.Elevation = new Elevation { Raw = tag.Elevation.Value, Datum = tag.Datum };
                run.TagRef = new TagRef { Text = tag.Text, By = "user", SourceHandle = d.Target.TryGetValue("tagSource", out var ts) ? ts : null, Confidence = 1.0 };
                UserDecided(run.Prov, d);
                Mark(d);
            }

            foreach (var d in Of(DecisionOp.SetDn))
            {
                var run = FindRunBySource(model.Runs, Source(d)) ?? FindRunById(model.Runs, d.Target);
                if (run == null || d.Value?.ValueKind != JsonValueKind.Number) continue;
                run.Dn = d.Value.Value.GetInt32();
                UserDecided(run.Prov, d);
                Mark(d);
            }

            foreach (var d in Of(DecisionOp.SetElevation))
            {
                var run = FindRunBySource(model.Runs, Source(d)) ?? FindRunById(model.Runs, d.Target);
                if (run == null || d.Value == null) continue;
                var v = d.Value.Value;
                if (v.ValueKind == JsonValueKind.Number)
                {
                    run.Elevation = new Elevation { Raw = v.GetDouble(), Datum = ElevationDatum.CL };
                }
                else if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("raw", out var raw))
                {
                    var datum = ElevationDatum.CL;
                    if (v.TryGetProperty("datum", out var dt) && Enum.TryParse(dt.GetString(), true, out ElevationDatum parsed)) datum = parsed;
                    run.Elevation = new Elevation { Raw = raw.GetDouble(), Datum = datum };
                }
                else
                {
                    continue;
                }

                UserDecided(run.Prov, d);
                Mark(d);
            }
        }

        /// <summary>Bước 4: đổi loại nút và loại cấu kiện sau khi có đồ thị.</summary>
        public void ApplyToGraph(PipingModel model)
        {
            foreach (var d in Of(DecisionOp.SetNodeKind))
            {
                var node = FindBySource(model.Nodes, n => n.Prov, Source(d)) ?? (d.Target.TryGetValue("node", out var id) ? model.Nodes.FirstOrDefault(n => n.Id == id) : null);
                if (node == null || d.Value?.ValueKind != JsonValueKind.String || !Enum.TryParse(d.Value.Value.GetString(), true, out NodeKind kind)) continue;
                node.Kind = kind;
                UserDecided(node.Prov, d);
                Mark(d);
            }

            foreach (var d in Of(DecisionOp.SetComponentType))
            {
                var c = FindBySource(model.Components, x => x.Prov, Source(d)) ?? (d.Target.TryGetValue("component", out var id) ? model.Components.FirstOrDefault(x => x.Id == id) : null);
                if (c == null || d.Value?.ValueKind != JsonValueKind.String) continue;
                c.Type = d.Value.Value.GetString() ?? c.Type;
                UserDecided(c.Prov, d);
                Mark(d);
            }
        }

        /// <summary>Bước 5: miễn trừ vấn đề theo mã luật và handle nguồn, rồi báo quyết định mồ côi.</summary>
        public void ApplyToIssues(PipingModel model)
        {
            foreach (var d in Of(DecisionOp.WaiveIssue))
            {
                var rule = d.Target.TryGetValue("rule", out var r) ? r : null;
                var source = Source(d);
                var element = d.Target.TryGetValue("element", out var el) ? el : null;
                var hits = model.Issues.Where(i => i.Status == IssueStatus.Open
                    && (rule == null || i.RuleId == rule)
                    && ((source != null && i.Sources.Contains(source, StringComparer.OrdinalIgnoreCase)) || (element != null && i.Elements.Contains(element)))).ToList();
                if (hits.Count == 0) continue;
                foreach (var i in hits)
                {
                    i.Status = IssueStatus.Waived;
                    i.Waiver = new Waiver { By = d.By == DecidedBy.Agent ? "agent" : "user", Reason = d.Rationale ?? string.Empty, At = d.At };
                }

                Mark(d);
            }

            var orphanNo = 0;
            foreach (var d in _decisions.Where(d => !_matched.Contains(d.Id)))
            {
                d.Orphan = true;
                Stats.Orphaned++;
                orphanNo++;
                model.Issues.Add(new Issue
                {
                    Id = "issue-dec-" + orphanNo.ToString("000", CultureInfo.InvariantCulture),
                    RuleId = "R-DEC-01",
                    Severity = Severity.Warning,
                    Message = $"Quyết định {d.Id} ({d.Op}) không còn khớp đối tượng nào trong bản vẽ, có thể đối tượng nguồn đã bị xoá. Xem lại hoặc gỡ.",
                    Sources = d.Target.Where(kv => kv.Key.StartsWith("source", StringComparison.Ordinal)).Select(kv => kv.Value).ToList(),
                });
            }

            foreach (var d in _decisions.Where(d => _matched.Contains(d.Id))) d.Orphan = false;
        }

        private void Mark(Decision d)
        {
            if (_matched.Add(d.Id)) Stats.Applied++;
        }

        private static void UserDecided(Provenance? prov, Decision d)
        {
            if (prov == null) return;
            prov.DecidedBy = d.By;
            prov.Confidence = 1.0;
            prov.Inferred = false;
        }

        private static Run? FindRunBySource(IEnumerable<Run> runs, string? source) => FindBySource(runs, r => r.Prov, source);

        private static Run? FindRunById(IEnumerable<Run> runs, Dictionary<string, string> target) =>
            target.TryGetValue("run", out var id) ? runs.FirstOrDefault(r => r.Id == id) : null;

        private static T? FindBySource<T>(IEnumerable<T> items, Func<T, Provenance?> prov, string? source) where T : class
        {
            if (string.IsNullOrEmpty(source)) return null;
            return items.FirstOrDefault(i => prov(i)?.Sources.Contains(source!, StringComparer.OrdinalIgnoreCase) == true);
        }

        private static bool TryPoint(JsonElement? value, out Point2 p)
        {
            p = default;
            if (value == null || value.Value.ValueKind != JsonValueKind.Array || value.Value.GetArrayLength() < 2) return false;
            p = new Point2(value.Value[0].GetDouble(), value.Value[1].GetDouble());
            return true;
        }

        private static ((Point2 Point, bool AtStart) A, (Point2 Point, bool AtStart) B) ClosestEnds(Run a, Run b)
        {
            var ends = new[] { (a.PlanPoints[0], true), (a.PlanPoints[a.PlanPoints.Count - 1], false) };
            var endsB = new[] { (b.PlanPoints[0], true), (b.PlanPoints[b.PlanPoints.Count - 1], false) };
            var best = (ends[0], endsB[0]);
            var bestD = double.MaxValue;
            foreach (var ea in ends)
            {
                foreach (var eb in endsB)
                {
                    var d = ea.Item1.DistanceTo(eb.Item1);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = (ea, eb);
                    }
                }
            }

            return best;
        }

        private static void SetEnd(Run r, bool atStart, Point2 p)
        {
            if (atStart) r.PlanPoints[0] = p; else r.PlanPoints[r.PlanPoints.Count - 1] = p;
        }
    }
}
