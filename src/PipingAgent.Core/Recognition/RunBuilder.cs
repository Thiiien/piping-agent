using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Recognition
{
    /// <summary>Mã các quy tắc nhận diện, ghi vào <see cref="Provenance.Rule"/>.</summary>
    public static class RecognitionRules
    {
        public const string RunFromChain = "REC-RUN-01";
        public const string TagByLeader = "REC-TAG-01";
        public const string TagAdjacent = "REC-TAG-02";
        public const string TagNearest = "REC-TAG-03";
        public const string LineFromTag = "REC-LINE-01";
        public const string TagInherited = "REC-TAG-04";
        public const string SegmentFromRun = "REC-SEG-01";
        public const string RiserInferred = "REC-SEG-02";
        public const string NodeFromGraph = "REC-NODE-01";
        public const string ComponentFromBlock = "REC-CMP-01";
        public const string EquipmentFromShape = "REC-EQP-01";
        public const string NozzleFromBoundary = "REC-NOZ-01";
        public const string StructureFromEntity = "REC-STR-01";
    }

    public sealed class RunBuildResult
    {
        public List<Run> Runs { get; } = new List<Run>();

        public CleanupStats Cleanup { get; set; } = new CleanupStats();

        /// <summary>Chuỗi sau làm sạch, cùng chỉ số với <see cref="Runs"/>.</summary>
        public List<Chain> Chains { get; } = new List<Chain>();
    }

    /// <summary>Dựng đoạn chạy từ polyline và chuỗi nét thẳng trên layer đường tâm, S2-07.</summary>
    public sealed class RunBuilder
    {
        private readonly CenterlineCleaner _cleaner;

        public RunBuilder(ConventionProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var t = profile.Tolerances;
            _cleaner = new CenterlineCleaner(t.Snap, t.CollinearDeg, t.MinSegmentLength);
        }

        public RunBuilder(CenterlineCleaner cleaner)
        {
            _cleaner = cleaner ?? throw new ArgumentNullException(nameof(cleaner));
        }

        /// <summary>Dựng từ các đối tượng đã gán vai trò <c>pipe_centerline</c>. Đối tượng không phải đường, polyline bị bỏ qua.</summary>
        public RunBuildResult Build(IEnumerable<SourceEntity> centerlines)
        {
            if (centerlines == null) throw new ArgumentNullException(nameof(centerlines));
            var chains = centerlines
                .Where(e => (e.Kind == SourceKind.Line || e.Kind == SourceKind.Polyline) && e.Points != null && e.Points.Count >= 2)
                .OrderBy(e => e.Handle, StringComparer.Ordinal)
                .Select(Chain.FromEntity);
            return Build(chains);
        }

        public RunBuildResult Build(IEnumerable<Chain> chains)
        {
            var cleaned = _cleaner.Clean(chains);
            var result = new RunBuildResult { Cleanup = cleaned.Stats };
            var ordered = cleaned.Chains
                .OrderBy(c => c.Points.Min(p => p.X))
                .ThenBy(c => c.Points.Min(p => p.Y))
                .ThenBy(c => c.Sources.FirstOrDefault() ?? string.Empty, StringComparer.Ordinal)
                .ToList();
            var n = 0;
            foreach (var chain in ordered)
            {
                n++;
                var run = new Run
                {
                    Id = "run-" + n.ToString("000", CultureInfo.InvariantCulture),
                    PlanPoints = new List<Geometry.Point2>(chain.Points),
                    Elevation = new Elevation { Datum = ElevationDatum.Unknown },
                    Prov = new Provenance
                    {
                        Sources = new List<string>(chain.Sources),
                        Rule = RecognitionRules.RunFromChain,
                        Confidence = 1.0,
                        DecidedBy = DecidedBy.Rule,
                    },
                };
                result.Runs.Add(run);
                result.Chains.Add(chain);
            }

            return result;
        }
    }
}
