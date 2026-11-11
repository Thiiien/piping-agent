using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Rules;

namespace PipingAgent.Core.Recognition
{
    /// <summary>
    /// Khép khe hở nhỏ giữa hai đầu mút đoạn chạy, luật R-CON-02: lớn hơn dung sai bắt điểm nhưng nhỏ hơn ngưỡng nghi ngờ
    /// <c>gapTolerance</c> của hồ sơ. Hai đầu được kéo về trung điểm và ghi một vấn đề mức thông tin để người dùng biết.
    /// Chỉ xét đầu mút, không xét đỉnh giữa, và mỗi đầu chỉ khép một lần.
    /// </summary>
    public sealed class GapCloser
    {
        private readonly ConventionProfile _profile;

        public GapCloser(ConventionProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public int Close(IReadOnlyList<Run> runs, List<Issue> issues)
        {
            var gap = _profile.Tolerances.GapTolerance;
            var snap = _profile.Tolerances.Snap;
            if (gap <= snap || runs == null || runs.Count == 0) return 0;

            var ends = new List<(Run Run, bool AtStart)>();
            foreach (var r in runs)
            {
                if (r.PlanPoints.Count < 2) continue;
                ends.Add((r, true));
                ends.Add((r, false));
            }

            Point2 PointOf((Run Run, bool AtStart) e) => e.AtStart ? e.Run.PlanPoints[0] : e.Run.PlanPoints[e.Run.PlanPoints.Count - 1];
            var grid = new SpatialGrid<int>(Math.Max(gap * 4, 10));
            for (var i = 0; i < ends.Count; i++) grid.Insert(i, PointOf(ends[i]));

            var used = new HashSet<int>();
            var closed = 0;
            var issueNo = issues.Count;
            for (var i = 0; i < ends.Count; i++)
            {
                if (used.Contains(i)) continue;
                var p = PointOf(ends[i]);
                var best = -1;
                var bestD = double.MaxValue;
                foreach (var j in grid.QueryNear(p, gap))
                {
                    if (j == i || used.Contains(j) || ends[j].Run == ends[i].Run) continue;
                    var d = PointOf(ends[j]).DistanceTo(p);
                    if (d > snap && d <= gap && d < bestD)
                    {
                        bestD = d;
                        best = j;
                    }
                }

                if (best < 0) continue;
                var q = PointOf(ends[best]);
                var mid = new Point2((p.X + q.X) / 2, (p.Y + q.Y) / 2);
                Set(ends[i], mid);
                Set(ends[best], mid);
                used.Add(i);
                used.Add(best);
                closed++;

                var severity = RuleCatalog.Resolve(_profile, "R-CON-02");
                if (severity != null)
                {
                    issueNo++;
                    var issue = new Issue
                    {
                        Id = "issue-gap-" + issueNo.ToString("000", CultureInfo.InvariantCulture),
                        RuleId = "R-CON-02",
                        Severity = severity.Value,
                        Message = string.Format(CultureInfo.InvariantCulture, "Khe hở {0:0.#} mm giữa {1} và {2} đã được khép tại {3}.", bestD, ends[i].Run.Id, ends[best].Run.Id, mid),
                        Position = mid.At(0),
                    };
                    issue.Elements.Add(ends[i].Run.Id);
                    issue.Elements.Add(ends[best].Run.Id);
                    issue.Sources.AddRange((ends[i].Run.Prov?.Sources ?? Enumerable.Empty<string>()).Concat(ends[best].Run.Prov?.Sources ?? Enumerable.Empty<string>()).Distinct());
                    issues.Add(issue);
                }
            }

            return closed;
        }

        private static void Set((Run Run, bool AtStart) e, Point2 p)
        {
            if (e.AtStart) e.Run.PlanPoints[0] = p; else e.Run.PlanPoints[e.Run.PlanPoints.Count - 1] = p;
        }
    }
}
