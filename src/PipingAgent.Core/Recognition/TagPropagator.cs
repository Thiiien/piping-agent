using System;
using System.Collections.Generic;
using System.Linq;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;

namespace PipingAgent.Core.Recognition
{
    public sealed class PropagationStats
    {
        public int Inherited { get; set; }

        public int Passes { get; set; }

        public int ElevationInherited { get; set; }

        public override string ToString() => $"{Inherited} đoạn chạy thừa hưởng nhãn qua {Passes} vòng, {ElevationInherited} thừa hưởng cao độ";
    }

    /// <summary>
    /// Lan truyền thuộc tính, S3-02 và FR-REC-04: đoạn chạy không nhãn nối với đoạn có nhãn thì thừa hưởng tuyến, DN và cao độ.
    /// Nối thẳng hàng: điểm tin cậy 0,85 theo mục 9.4. Nối gập góc tại đỉnh chỉ có hai đoạn, tức qua co: 0,75.
    /// Không lan truyền qua đỉnh có ba đoạn trở lên, vì sau tê có thể là tuyến khác. Mọi giá trị thừa hưởng đánh dấu suy ra.
    /// </summary>
    public sealed class TagPropagator
    {
        public const double CollinearConfidence = 0.85;
        public const double ElbowConfidence = 0.75;

        /// <summary>Cao độ lan sang đoạn khác tuyến hoặc chưa có tuyến chỉ vì nối nhau trên mặt bằng.</summary>
        public const double ElevationNetworkConfidence = 0.6;

        private readonly double _snap;
        private readonly double _collinearDeg;

        public TagPropagator(double snap, double collinearDeg)
        {
            _snap = snap;
            _collinearDeg = collinearDeg;
        }

        public PropagationStats Propagate(IReadOnlyList<Run> runs)
        {
            var stats = new PropagationStats();
            if (runs == null || runs.Count == 0) return stats;
            var key = new Func<Point2, (long, long)>(p => ((long)Math.Round(p.X / _snap), (long)Math.Round(p.Y / _snap)));
            var byPoint = new Dictionary<(long, long), List<(Run Run, bool AtStart)>>();
            foreach (var run in runs)
            {
                if (run.PlanPoints.Count < 2) continue;
                Add(byPoint, key(run.PlanPoints[0]), (run, true));
                Add(byPoint, key(run.PlanPoints[run.PlanPoints.Count - 1]), (run, false));
            }

            bool changed;
            do
            {
                changed = false;
                stats.Passes++;
                foreach (var run in runs.Where(r => !HasTag(r) && r.PlanPoints.Count >= 2).OrderBy(r => r.Id, StringComparer.Ordinal))
                {
                    var best = BestDonor(run, byPoint, key);
                    if (best == null) continue;
                    Inherit(run, best.Value.Donor, best.Value.Confidence);
                    stats.Inherited++;
                    changed = true;
                }
            }
            while (changed && stats.Passes < 100);

            // Giai đoạn hai: cao độ lan theo mạng ống. Cao độ ghi một lần ở đầu tuyến áp cho mọi đoạn nối tiếp, qua co, qua tê,
            // cho tới khi gặp đoạn có cao độ riêng. Đoạn cùng tuyến được ưu tiên; đoạn khác tuyến hoặc chưa có tuyến nhận với
            // điểm tin cậy thấp hơn và cờ suy ra, để ống đứng và bộ luật kiểm tra lại.
            do
            {
                changed = false;
                foreach (var run in runs.Where(r => r.Elevation.Datum == ElevationDatum.Unknown && r.PlanPoints.Count >= 2).OrderBy(r => r.Id, StringComparer.Ordinal))
                {
                    var donor = ElevationDonor(run, byPoint, key);
                    if (donor == null) continue;
                    run.Elevation = new Elevation { Raw = donor.Elevation.Raw, Datum = donor.Elevation.Datum, SupportHeight = donor.Elevation.SupportHeight };
                    // Mốc đáy ống hay đỉnh ống cần đường kính để quy về tâm, nên DN cũng theo mạng ống khi đoạn chưa có DN riêng.
                    if (run.Dn == 0 && donor.Dn > 0) run.Dn = donor.Dn;
                    run.Prov ??= new Provenance();
                    run.Prov.Inferred = true;
                    run.Prov.Confidence = Math.Min(run.Prov.Confidence, donor.LineId == run.LineId && !string.IsNullOrEmpty(run.LineId) ? CollinearConfidence : ElevationNetworkConfidence);
                    if (donor.TagRef?.SourceHandle != null && !run.Prov.Sources.Contains(donor.TagRef.SourceHandle)) run.Prov.Sources.Add(donor.TagRef.SourceHandle);
                    stats.ElevationInherited++;
                    changed = true;
                }
            }
            while (changed);

            return stats;
        }

        private static Run? ElevationDonor(Run run, Dictionary<(long, long), List<(Run Run, bool AtStart)>> byPoint, Func<Point2, (long, long)> key)
        {
            Run? fallback = null;
            foreach (var atStart in new[] { true, false })
            {
                var p = atStart ? run.PlanPoints[0] : run.PlanPoints[run.PlanPoints.Count - 1];
                if (!byPoint.TryGetValue(key(p), out var ends)) continue;
                var known = ends.Select(e => e.Run).Where(r => r != run && r.Elevation.Datum != ElevationDatum.Unknown).ToList();
                var sameLine = known.FirstOrDefault(r => !string.IsNullOrEmpty(run.LineId) && r.LineId == run.LineId);
                if (sameLine != null) return sameLine;
                // Hai đoạn kề có cao độ khác nhau tại cùng một điểm là chỗ đổi cao độ, không lan qua.
                if (known.Count > 0 && known.Select(r => (r.Elevation.Raw, r.Elevation.Datum)).Distinct().Count() == 1) fallback ??= known[0];
            }

            return fallback;
        }

        private (Run Donor, double Confidence)? BestDonor(Run run, Dictionary<(long, long), List<(Run Run, bool AtStart)>> byPoint, Func<Point2, (long, long)> key)
        {
            (Run Donor, double Confidence)? best = null;
            foreach (var atStart in new[] { true, false })
            {
                var p = atStart ? run.PlanPoints[0] : run.PlanPoints[run.PlanPoints.Count - 1];
                if (!byPoint.TryGetValue(key(p), out var ends)) continue;
                var others = ends.Where(e => e.Run != run).ToList();
                if (others.Count == 0 || others.Count > 2) continue;
                var myDir = Direction(run, atStart);
                foreach (var o in others)
                {
                    var donor = o.Run;
                    if (!HasTag(donor)) continue;
                    var donorDir = Direction(donor, o.AtStart);
                    var angle = Math.Acos(Math.Max(-1, Math.Min(1, myDir.Dot(donorDir)))) * 180 / Math.PI;
                    var straight = Math.Abs(angle - 180) <= Math.Max(_collinearDeg, 1.0);
                    // Tại tê, ba đầu gặp nhau: chỉ đoạn đi thẳng qua tê thừa hưởng, nhánh rẽ thì không vì có thể là tuyến khác.
                    if (others.Count == 2 && !straight) continue;
                    var confidence = straight ? CollinearConfidence : ElbowConfidence;
                    var donorConfidence = donor.TagRef?.Confidence ?? (donor.Prov?.Confidence ?? 1.0);
                    confidence = Math.Round(Math.Min(confidence, donorConfidence), 3);
                    if (best == null || confidence > best.Value.Confidence) best = (donor, confidence);
                }


            }

            return best;
        }

        private static void Inherit(Run run, Run donor, double confidence)
        {
            run.LineId = donor.LineId;
            if (run.Dn == 0) run.Dn = donor.Dn;
            if (run.TagRef == null)
            {
                run.Elevation = new Elevation
                {
                    Raw = donor.Elevation.Raw,
                    Datum = donor.Elevation.Datum,
                    SupportHeight = donor.Elevation.SupportHeight,
                };
            }

            run.TagRef = new TagRef
            {
                Text = donor.TagRef?.Text ?? string.Empty,
                By = "inherited",
                SourceHandle = donor.TagRef?.SourceHandle,
                Confidence = confidence,
            };
            run.Prov ??= new Provenance();
            run.Prov.Inferred = true;
            run.Prov.Confidence = Math.Min(run.Prov.Confidence, confidence);
            run.Prov.Rule = RecognitionRules.TagInherited;
            if (donor.TagRef?.SourceHandle != null && !run.Prov.Sources.Contains(donor.TagRef.SourceHandle)) run.Prov.Sources.Add(donor.TagRef.SourceHandle);
        }

        private static bool HasTag(Run r) => !string.IsNullOrEmpty(r.LineId);

        private static Vector2 Direction(Run run, bool atStart)
        {
            var pts = run.PlanPoints;
            var v = atStart ? pts[0] - pts[1] : pts[pts.Count - 1] - pts[pts.Count - 2];
            return v.LengthSquared > 0 ? v.Normalized() : Vector2.Zero;
        }

        private static void Add<TKey, TValue>(Dictionary<TKey, List<TValue>> d, TKey k, TValue v)
        {
            if (!d.TryGetValue(k, out var list))
            {
                list = new List<TValue>();
                d[k] = list;
            }

            list.Add(v);
        }
    }
}
