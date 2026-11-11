using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Decisions;
using PipingAgent.Core.Equipment;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Graph;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Rules;
using PipingAgent.Core.Source;

namespace PipingAgent.Core.Recognition
{
    /// <summary>Tiến độ một bước xử lý dài, NFR-PRF-05. Ứng dụng chủ hiện thanh tiến độ từ đây.</summary>
    public readonly struct ProgressInfo
    {
        public ProgressInfo(string step, double fraction, string? detail = null)
        {
            Step = step;
            Fraction = fraction;
            Detail = detail;
        }

        public string Step { get; }

        /// <summary>0 đến 1.</summary>
        public double Fraction { get; }

        public string? Detail { get; }
    }

    public sealed class RecognitionResult
    {
        public PipingModel Model { get; set; } = new PipingModel();

        public ClassificationResult Classification { get; set; } = new ClassificationResult();

        public List<TagReadResult> TagReads { get; set; } = new List<TagReadResult>();

        public RunBuildResult RunBuild { get; set; } = new RunBuildResult();

        public TagAttachResult TagAttach { get; set; } = new TagAttachResult();

        public BridgeStats Bridge { get; set; } = new BridgeStats();

        public int GapsClosed { get; set; }

        /// <summary>Vấn đề R-CON-02 của bước khép khe hở, được gộp vào mô hình khi ghi.</summary>
        public List<Issue> GapIssues { get; } = new List<Issue>();

        public PropagationStats Propagation { get; set; } = new PropagationStats();

        public GraphStats Graph { get; set; } = new GraphStats();

        public ComponentStats Components { get; set; } = new ComponentStats();

        public EquipmentStats Equipment { get; set; } = new EquipmentStats();

        public NozzleStats Nozzles { get; set; } = new NozzleStats();

        public StructureStats Structures { get; set; } = new StructureStats();

        public RuleRunResult Rules { get; set; } = new RuleRunResult();

        public DecisionStats Decisions { get; set; } = new DecisionStats();

        public TimeSpan Elapsed { get; set; }

        /// <summary>Đúng nếu bị hủy giữa chừng; mô hình khi đó không đầy đủ.</summary>
        public bool Cancelled { get; set; }
    }

    /// <summary>
    /// Chuỗi nhận diện của Sprint 2: phân loại, đọc nhãn, làm sạch, dựng đoạn chạy, gắn nhãn, ghi IR.
    /// Thuần lõi, không biết gì về CAD. Hủy được qua <see cref="CancellationToken"/>.
    /// </summary>
    public static class RecognitionPipeline
    {
        public static RecognitionResult Run(
            IReadOnlyList<SourceEntity> entities,
            ConventionProfile profile,
            PipeCatalog? catalog = null,
            SourceInfo? source = null,
            EquipmentLibrary? equipment = null,
            IProgress<ProgressInfo>? progress = null,
            IEnumerable<Decision>? decisions = null,
            CancellationToken cancellation = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var watch = Stopwatch.StartNew();
            var result = new RecognitionResult();
            var model = result.Model;
            model.Source = source ?? new SourceInfo { EntityCount = entities.Count };
            model.Profile = new ProfileRef { Id = profile.Id, Version = profile.Version };
            model.Generator = new GeneratorInfo
            {
                Version = typeof(RecognitionPipeline).Assembly.GetName().Version?.ToString() ?? string.Empty,
                CatalogVerified = catalog == null ? (bool?)null : catalog.AllVerified,
            };

            var applier = new DecisionApplier(decisions);
            try
            {
                progress?.Report(new ProgressInfo("Phân loại đối tượng", 0.05));
                cancellation.ThrowIfCancellationRequested();
                entities = applier.FilterEntities(entities);
                var classifier = new Classifier(profile);
                result.Classification = classifier.Classify(entities);

                progress?.Report(new ProgressInfo("Đọc nhãn", 0.30));
                cancellation.ThrowIfCancellationRequested();
                var parser = new TagParser(profile);
                result.TagReads = parser.ReadAll(result.Classification.EntitiesWithRole(EntityRoles.PipeTag)
                    .Where(e => !string.IsNullOrWhiteSpace(e.Text)));

                progress?.Report(new ProgressInfo("Làm sạch và dựng đoạn chạy", 0.50));
                cancellation.ThrowIfCancellationRequested();
                var builder = new RunBuilder(profile);
                result.RunBuild = builder.Build(result.Classification.EntitiesWithRole(EntityRoles.PipeCenterline));

                progress?.Report(new ProgressInfo("Nối đoạn chạy qua block phụ kiện", 0.60));
                cancellation.ThrowIfCancellationRequested();
                var bridger = new FittingBridger(profile.Tolerances.FittingReach, profile.Tolerances.Snap);
                result.Bridge = bridger.Bridge(result.RunBuild.Runs, result.Classification.EntitiesWithRole(EntityRoles.InlineComponent).Concat(result.Classification.EntitiesWithRole(EntityRoles.FlowArrow)));

                result.GapsClosed = new GapCloser(profile).Close(result.RunBuild.Runs, result.GapIssues);
                applier.ApplyToRuns(result.RunBuild.Runs, profile.Tolerances.Snap);

                progress?.Report(new ProgressInfo("Gắn nhãn", 0.70));
                cancellation.ThrowIfCancellationRequested();
                var leaders = result.Classification.EntitiesWithRole(EntityRoles.PipeTagLeader)
                    .Concat(result.Classification.EntitiesWithRole(EntityRoles.PipeTag).Where(e => e.Kind == SourceKind.Leader || e.Kind == SourceKind.MultiLeader));
                var attacher = new TagAttacher(profile);
                result.TagAttach = attacher.Attach(result.RunBuild.Runs, result.TagReads, leaders);

                progress?.Report(new ProgressInfo("Ghi mô hình", 0.80));
                cancellation.ThrowIfCancellationRequested();
                Assemble(result, profile, catalog);
                applier.ApplyToModelRuns(result.Model, profile);

                progress?.Report(new ProgressInfo("Lan truyền nhãn", 0.85));
                cancellation.ThrowIfCancellationRequested();
                var propagator = new TagPropagator(profile.Tolerances.Snap, profile.Tolerances.CollinearDeg);
                result.Propagation = propagator.Propagate(result.Model.Runs);

                progress?.Report(new ProgressInfo("Dựng đồ thị kết nối 3D", 0.90));
                cancellation.ThrowIfCancellationRequested();
                var graph = new GraphBuilder(profile, catalog);
                result.Graph = graph.Build(result.Model, result.Classification.EntitiesWithRole(EntityRoles.RiserSymbol));
                applier.ApplyToGraph(result.Model);

                progress?.Report(new ProgressInfo("Cấu kiện, thiết bị, kết cấu", 0.94));
                cancellation.ThrowIfCancellationRequested();
                result.Components = new ComponentRecognizer(profile, catalog).Recognize(result.Model, result.Classification.EntitiesWithRole(EntityRoles.InlineComponent));
                var library = equipment ?? new EquipmentLibrary();
                var eqRecognizer = new EquipmentRecognizer(profile, library);
                result.Equipment = eqRecognizer.Recognize(result.Model, result.Classification.EntitiesWithRole(EntityRoles.Equipment), result.Classification.EntitiesWithRole(EntityRoles.EquipmentTag));
                result.Nozzles = new NozzleRecognizer(profile).Recognize(result.Model, eqRecognizer.Footprints);
                result.Structures = new StructureRecognizer(profile).Recognize(result.Model, result.Classification.EntitiesWithRole(EntityRoles.Structure), result.Classification.EntitiesWithRole(EntityRoles.StructureTag));

                progress?.Report(new ProgressInfo("Chạy bộ luật", 0.97));
                cancellation.ThrowIfCancellationRequested();
                result.Rules = RuleRunner.Run(result.Model, profile);
                applier.ApplyToIssues(result.Model);
                result.Decisions = applier.Stats;
                if (decisions != null) result.Model.Decisions.AddRange(decisions);
            }
            catch (OperationCanceledException)
            {
                result.Cancelled = true;
            }

            watch.Stop();
            result.Elapsed = watch.Elapsed;
            progress?.Report(new ProgressInfo("Xong", 1.0));
            return result;
        }

        private static void Assemble(RecognitionResult result, ConventionProfile profile, PipeCatalog? catalog)
        {
            var model = result.Model;
            model.Runs.AddRange(result.RunBuild.Runs);
            model.Issues.AddRange(result.TagAttach.Issues);
            model.Issues.AddRange(result.GapIssues);

            var lines = new Dictionary<string, Line>(StringComparer.OrdinalIgnoreCase);
            var tagsByRun = new Dictionary<string, List<TagAttachment>>(StringComparer.Ordinal);
            foreach (var att in result.TagAttach.Attachments.Where(a => a.Attached))
            {
                if (!tagsByRun.TryGetValue(att.Run!.Id, out var list))
                {
                    list = new List<TagAttachment>();
                    tagsByRun[att.Run.Id] = list;
                }

                list.Add(att);
            }

            var issueNo = model.Issues.Count;
            foreach (var run in model.Runs)
            {
                var elevationOnly = tagsByRun.TryGetValue(run.Id, out var all)
                    ? all.Where(a => a.Tag.Tag!.IsElevationOnly).OrderByDescending(a => a.Confidence).ThenBy(a => a.Tag.Entity.Handle, StringComparer.Ordinal).ToList()
                    : new List<TagAttachment>();
                var atts = all?.Where(a => !a.Tag.Tag!.IsElevationOnly).ToList();
                if (atts == null || atts.Count == 0)
                {
                    // R-TAG-01 do bộ luật báo sau bước lan truyền, để đoạn thừa hưởng được nhãn không bị báo oan.
                    ApplyElevationOnly(run, elevationOnly, model, profile, ref issueNo);
                    continue;
                }

                var best = atts.OrderByDescending(a => a.Confidence).ThenBy(a => a.Tag.Entity.Handle, StringComparer.Ordinal).First();
                var tag = best.Tag.Tag!;
                foreach (var other in atts.Where(a => a != best))
                {
                    var o = other.Tag.Tag!;
                    if (!string.Equals(o.Line, tag.Line, StringComparison.OrdinalIgnoreCase)
                        || (o.Dn.HasValue && tag.Dn.HasValue && o.Dn != tag.Dn)
                        || (o.Elevation.HasValue && tag.Elevation.HasValue && Math.Abs(o.Elevation.Value - tag.Elevation.Value) > 0.5))
                    {
                        AddIssue(model, profile, ref issueNo, "R-TAG-05",
                            $"Đoạn chạy {run.Id} nhận hai nhãn mâu thuẫn: '{tag.Text}' và '{o.Text}'.",
                            run, new[] { best.Tag.Entity.Handle, other.Tag.Entity.Handle });
                    }
                }

                var line = GetOrAddLine(lines, model, tag, best);
                run.LineId = line.Id;
                run.Dn = tag.Dn ?? 0;
                run.Elevation = new Elevation
                {
                    Raw = tag.Elevation ?? 0,
                    Datum = tag.Elevation.HasValue ? tag.Datum : ElevationDatum.Unknown,
                };
                run.TagRef = new TagRef
                {
                    Text = tag.Text,
                    By = best.Method ?? AttachMethods.Nearest,
                    SourceHandle = best.Tag.Entity.Handle,
                    Position = best.Tag.Entity.Center?.Plan ?? best.Tag.Entity.Bounds?.Center,
                    Confidence = best.Confidence,
                };
                if (run.Prov != null)
                {
                    run.Prov.Sources.Add(best.Tag.Entity.Handle);
                    run.Prov.Sources.AddRange(best.LeaderHandles);
                    run.Prov.Confidence = Math.Min(run.Prov.Confidence, best.Confidence);
                }

                if (tag.Dn.HasValue && catalog != null && profile.PipeStandard != null)
                {
                    var od = catalog.PipeOd(profile.PipeStandard, tag.Dn.Value);
                    if (!od.Found)
                    {
                        AddIssue(model, profile, ref issueNo, "R-TAG-06", $"Đoạn chạy {run.Id}: {od.Reason}", run, new[] { best.Tag.Entity.Handle });
                    }
                }

                ApplyElevationOnly(run, elevationOnly, model, profile, ref issueNo);
            }

            var report = result.Classification.Report;
            if (report.ExceedsThreshold)
            {
                AddIssue(model, profile, ref issueNo, "R-GEN-01",
                    string.Format(CultureInfo.InvariantCulture, "{0:P1} đối tượng chưa phân loại, vượt ngưỡng {1:P0}.", report.UnclassifiedRatio, report.WarningThreshold),
                    null, null);
            }
        }

        /// <summary>Chữ cao độ rời bổ sung cao độ khi nhãn tuyến không ghi. Hai giá trị khác nhau thì báo R-TAG-05.</summary>
        private static void ApplyElevationOnly(Run run, List<TagAttachment> elevationOnly, PipingModel model, ConventionProfile profile, ref int issueNo)
        {
            if (elevationOnly.Count == 0) return;
            var best = elevationOnly[0];
            var tag = best.Tag.Tag!;
            var hasOwn = run.TagRef != null && run.Elevation.Datum != ElevationDatum.Unknown;
            if (!hasOwn)
            {
                run.Elevation = new Elevation { Raw = tag.Elevation!.Value, Datum = tag.Datum };
                run.Prov?.Sources.Add(best.Tag.Entity.Handle);
                run.Prov?.Sources.AddRange(best.LeaderHandles);
            }
            else if (Math.Abs(run.Elevation.Raw - tag.Elevation!.Value) > 0.5 || run.Elevation.Datum != tag.Datum)
            {
                AddIssue(model, profile, ref issueNo, "R-TAG-05",
                    $"Đoạn chạy {run.Id} có cao độ trên nhãn tuyến khác chữ cao độ rời '{tag.Text}'.",
                    run, new[] { run.TagRef!.SourceHandle ?? string.Empty, best.Tag.Entity.Handle });
            }

            foreach (var other in elevationOnly.Skip(1))
            {
                var o = other.Tag.Tag!;
                if (Math.Abs(o.Elevation!.Value - tag.Elevation!.Value) > 0.5 || o.Datum != tag.Datum)
                {
                    AddIssue(model, profile, ref issueNo, "R-TAG-05",
                        $"Đoạn chạy {run.Id} nhận hai cao độ khác nhau: '{tag.Text}' và '{o.Text}'.",
                        run, new[] { best.Tag.Entity.Handle, other.Tag.Entity.Handle });
                }
            }
        }

        private static Line GetOrAddLine(Dictionary<string, Line> lines, PipingModel model, ParsedTag tag, TagAttachment att)
        {
            if (lines.TryGetValue(tag.Line, out var line))
            {
                line.Prov?.Sources.Add(att.Tag.Entity.Handle);
                return line;
            }

            line = new Line
            {
                Id = "line-" + (lines.Count + 1).ToString("000", CultureInfo.InvariantCulture),
                Tag = tag.Line,
                Service = tag.Service,
                Spec = tag.Spec,
                Insulation = tag.InsulationType != null || tag.InsulationThickness.HasValue
                    ? new Insulation { Type = tag.InsulationType, Thickness = tag.InsulationThickness }
                    : null,
                Prov = new Provenance
                {
                    Sources = new List<string> { att.Tag.Entity.Handle },
                    Rule = RecognitionRules.LineFromTag,
                    Confidence = 1.0,
                },
            };
            lines[tag.Line] = line;
            model.Lines.Add(line);
            return line;
        }

        private static void AddIssue(PipingModel model, ConventionProfile profile, ref int issueNo, string ruleId, string message, Run? run, IEnumerable<string>? sources)
        {
            var severity = RuleCatalog.Resolve(profile, ruleId);
            if (severity == null) return;
            issueNo++;
            var issue = new Issue
            {
                Id = "issue-" + issueNo.ToString("000", CultureInfo.InvariantCulture),
                RuleId = ruleId,
                Severity = severity.Value,
                Message = message,
            };
            if (run != null)
            {
                issue.Elements.Add(run.Id);
                if (run.PlanPoints.Count > 0) issue.Position = Box2.FromPoints(run.PlanPoints).Center.At(0);
            }

            if (sources != null) issue.Sources.AddRange(sources.Distinct());
            model.Issues.Add(issue);
        }
    }
}
