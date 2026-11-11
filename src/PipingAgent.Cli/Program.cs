using System.Globalization;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Ir.Json;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Recognition;
using PipingAgent.Core.Source;
using PipingAgent.Dxf;
using PipingAgent.Hosting;

// Công cụ dòng lệnh của Piping Agent. Mức tích hợp B và C ở mục 6.12.1: chạy lõi ngoài CAD.
// Lệnh hiện có:
//   dump <bản vẽ.dxf|.dwg> <ra.json> [--explode] [--unit-scale <hệ số>]   xuất đối tượng trung lập
//   compare <a.json> <b.json> [--max <n>]                                   so hai bộ đọc, việc S1-16
//   summary <bản vẽ.dxf|.dwg>                                              in bản tóm tắt
//   recognize <bản vẽ> <hồ sơ.json> [--out <ir.json>] [--catalog <thư mục>] [--unit-scale <hệ số>]
//                                                                          chạy chuỗi nhận diện Sprint 2, in báo cáo

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length == 0)
{
    PrintUsage();
    return 2;
}

try
{
    switch (args[0].ToLowerInvariant())
    {
        case "dump": return Dump(args.Skip(1).ToArray());
        case "compare": return Compare(args.Skip(1).ToArray());
        case "summary": return Summary(args.Skip(1).ToArray());
        case "recognize": return Recognize(args.Skip(1).ToArray());
        case "batch": return Batch(args.Skip(1).ToArray());
        case "iso": return Iso(args.Skip(1).ToArray());
        case "iso-merge": return IsoMergeCommand(args.Skip(1).ToArray());
        default:
            PrintUsage();
            return 2;
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine("Lỗi: " + exception.Message);
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("PipingAgent.Cli");
    Console.WriteLine("  dump <bản vẽ> <ra.json> [--explode] [--unit-scale <hệ số>]");
    Console.WriteLine("  compare <a.json> <b.json> [--max <n>]");
    Console.WriteLine("  summary <bản vẽ>");
    Console.WriteLine("  recognize <bản vẽ> <hồ sơ.json> [--out <ir.json>] [--bom <ra.csv>] [--report <ra.md>] [--expected <expected.json>] [--catalog <thư mục>] [--unit-scale <hệ số>]");
    Console.WriteLine("  batch <thư mục hoặc tệp...> --profile <hồ sơ.json> --out <thư mục ra> [--expected-dir <thư mục đáp án>]");
    Console.WriteLine("  iso <iso.pdf|.dwg|.dxf> [--out <ir.json>] [--report <kiem-ke.md>] [--force]   I1, I3: bước 1 kiểm kê và đối chiếu, bước 2 ghi IR 3D khi đủ");
    Console.WriteLine("  iso-merge <a.ir.json>[@dE,dN[,dZ]] <b.ir.json>[@[?]dE,dN[,dZ]] ... --out <gop.ir.json> [--stub <tuyến>:<E>,<N>,<EL>:<E|N|U>:<DN>[:<dài mỗi bên>]]   I4: gộp nhiều tờ vào một mô hình");
}

// I4: gộp IR nhiều tờ vào một hệ toạ độ. Phép dời sau @ là của người dùng; không có thì tờ giữ nguyên gốc và ghi là giả định.
static int IsoMergeCommand(string[] a)
{
    var sheets = new List<PipingAgent.Iso.MergeSheet>();
    var stubs = new List<PipingAgent.Iso.ReferenceStub>();
    string? outPath = null;
    for (var i = 0; i < a.Length; i++)
    {
        if (a[i] == "--out" && i + 1 < a.Length) { outPath = a[++i]; continue; }
        if (a[i] == "--stub" && i + 1 < a.Length)
        {
            var parts = a[++i].Split(':');
            if (parts.Length < 4) { Console.Error.WriteLine("--stub cần <tuyến>:<E>,<N>,<EL>:<E|N|U>:<DN>[:<dài mỗi bên>]"); return 2; }
            var xyz = parts[1].Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            stubs.Add(new PipingAgent.Iso.ReferenceStub
            {
                Tag = parts[0],
                At = new PipingAgent.Core.Geometry.Point3(xyz[0], xyz[1], xyz[2]),
                Axis = parts[2],
                Dn = int.Parse(parts[3], CultureInfo.InvariantCulture),
                HalfLength = parts.Length > 4 ? double.Parse(parts[4], CultureInfo.InvariantCulture) : 1000,
            });
            continue;
        }

        if (a[i].StartsWith("--", StringComparison.Ordinal)) continue;
        var at = a[i].LastIndexOf('@');
        var path = at > 0 ? a[i].Substring(0, at) : a[i];
        var offset = new PipingAgent.Core.Geometry.Vector3(0, 0, 0);
        var by = "giả định";
        if (at > 0)
        {
            // "@?dE,dN,dZ" là phép dời giả định, ví dụ do người vận hành hay agent đoán khi chưa có tờ ống góp; "@dE,dN,dZ" là người cho.
            var text = a[i].Substring(at + 1);
            var assumed = text.StartsWith("?", StringComparison.Ordinal);
            var v = text.TrimStart('?').Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            offset = new PipingAgent.Core.Geometry.Vector3(v[0], v.Length > 1 ? v[1] : 0, v.Length > 2 ? v[2] : 0);
            by = assumed ? "giả định" : "người cho";
        }

        sheets.Add(new PipingAgent.Iso.MergeSheet
        {
            Name = Path.GetFileName(path).Replace(".ir.json", string.Empty),
            Model = IrJson.Deserialize(File.ReadAllText(path)),
            Offset = offset,
            OffsetBy = by,
        });
    }

    if (sheets.Count < 1 || outPath == null) { PrintUsage(); return 2; }
    var merged = PipingAgent.Iso.IsoMerge.Merge(sheets, stubs, Path.GetFileName(outPath));
    var catalog = CatalogFiles.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "catalog"));
    var profile = new ConventionProfile { Id = "iso-default", PipeStandard = "ASME-B36.10", FittingStandard = "ASME-B16.9" };
    var stats = new PipingAgent.Core.Graph.GraphBuilder(profile, catalog).Build(merged);
    Console.WriteLine($"Gộp {sheets.Count} tờ, {stubs.Count} ống tham chiếu. Đồ thị 3D: {stats}");
    LinkComponents(merged, verbose: false);
    foreach (var issue in merged.Issues.Where(x => x.Severity != Severity.Info)) Console.WriteLine($"  [{issue.Severity}] {issue.RuleId} {issue.Message}");
    File.WriteAllText(outPath, IrJson.Serialize(merged));
    var reportPath = Path.ChangeExtension(outPath, null).Replace(".ir", "") + ".kiem-ke.md";
    File.WriteAllText(reportPath, PipingAgent.Iso.IsoMerge.Report(sheets, stubs, merged, Path.GetFileName(outPath)));
    Console.WriteLine($"Đã ghi IR gộp vào {outPath} và bản kiểm kê vào {reportPath}.");
    foreach (var s in sheets.Skip(1).Where(s => s.OffsetBy == "giả định")) Console.WriteLine($"  [xem] {s.Name}: phép dời giả định ({s.Offset.X:0}, {s.Offset.Y:0}, {s.Offset.Z:0}); cần tờ ống góp chung hoặc người cho toạ độ.");
    return 0;
}

// Gắn cấu kiện vào đoạn ống chứa nó sau khi có đồ thị. Cấu kiện xếp chồng ngoài đầu hở cách đoạn ống tới nửa chiều dài của nó.
static void LinkComponents(PipingModel model, bool verbose)
{
    PipingAgent.Iso.IsoPipeline.LinkComponents(model);
    if (!verbose) return;
    foreach (var c in model.Components) Console.WriteLine($"  cấu kiện {c.Id} {c.Type} {c.Tag} DN{c.Dn} tại {c.Position} trên {c.SegmentId}");
}

// I1: isometric PDF vector sang đồ thị và toạ độ thật, in để kiểm tra, ghi IR nếu có --out.
static int Iso(string[] a)
{
    if (a.Length < 1) { PrintUsage(); return 2; }
    var outIndex = Array.IndexOf(a, "--out");
    var outPath = outIndex >= 0 && outIndex + 1 < a.Length ? a[outIndex + 1] : null;
    var catalog = CatalogFiles.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "catalog"));
    // Cùng một đường chạy với lệnh PA_ISO trong AutoCAD; ở đây chỉ in chi tiết để kiểm tra.
    var run = PipingAgent.Iso.IsoPipeline.Run(a[0], catalog);
    var page = run.Page;
    var r = run.Analysis;
    Console.WriteLine($"{Path.GetFileName(a[0])}: {(run.IsDwg ? "DWG/DXF" : "PDF")}{(run.Isogen ? " kiểu ISOGEN" : "")}, {page.Width:0}x{page.Height:0} pt, {page.Entities.Count(e => e.Kind == SourceKind.Line)} nét, {page.Entities.Count(e => e.Kind == SourceKind.Text)} chữ");
    Console.WriteLine($"Nét ống: {r.Segments.Count(s => !s.Bridged)}, nối khe {r.Bridged}, góc co {r.Corners}, đầu hở {r.OpenEnds.Count}, neo {r.Anchors.Count}, tuyến {r.LineTag}, DN{r.Dn}");
    foreach (var an in r.Anchors) Console.WriteLine($"  neo E {an.E} N {an.N} EL {an.El}{(an.HasPlan ? "" : " (chỉ cao độ)")} tại đỉnh {(an.Node.HasValue ? an.Node.Value.ToString() : "?")}");
    Console.WriteLine("Đoạn và kích thước:");
    foreach (var s in r.Segments.Where(s => !s.Bridged || s.Dimensions.Count > 0))
    {
        var dims = string.Join("+", s.Dimensions.Select(d => d.Value.ToString(CultureInfo.InvariantCulture) + (d.By.StartsWith("span") ? "" : "?")));
        var pa = r.Positions.TryGetValue(s.A, out var ra) ? ra.ToString() : "?";
        var pb = r.Positions.TryGetValue(s.B, out var rb) ? rb.ToString() : "?";
        Console.WriteLine($"  {s.Axis,-5} {dims,-22} {pa} -> {pb}{(s.Dn > 0 ? " DN" + s.Dn : "")}{(s.Bridged ? " [khe]" : "")}{(s.Note != null ? " " + s.Note : "")}");
    }

    Console.WriteLine($"Khoảng cách giữa hai đỉnh đọc từ đường gióng: {r.Spans.Count}");

    foreach (var d in r.UnassignedDimensions) Console.WriteLine($"  chưa gán: {d.Value} tại {d.TextAt}");
    foreach (var note in r.Notes) Console.WriteLine("  " + note);
    foreach (var kv in r.ElevationConstraints) Console.WriteLine($"  ràng buộc EL {kv.Value} tại đỉnh {kv.Key}");
    Console.WriteLine("Khép kín:");
    foreach (var c in r.Closures)
    {
        Console.WriteLine(c.Reached
            ? $"  neo E {c.Anchor.E} N {c.Anchor.N} EL {c.Anchor.El}: suy ra {c.Derived}, lệch {c.ErrorMm} mm"
            : $"  neo E {c.Anchor.E} N {c.Anchor.N} EL {c.Anchor.El}: không tới được");
    }

    var model = run.Model;
    Console.WriteLine($"Đồ thị 3D: {run.GraphStats}");
    LinkComponents(model, verbose: true);

    Console.WriteLine($"Bảng vật tư: {r.Bom.Count} dòng; ô số thứ tự: {r.Callouts.Count}, có đường chỉ {r.Callouts.Count(x => x.Target.HasValue)}");
    if (Array.IndexOf(a, "--verbose") >= 0)
    {
        foreach (var b in r.Bom) Console.WriteLine($"  BOM {b.Item} {b.Type} {b.Size} DN{b.Dn} x{b.Quantity}: {b.Description}");
        foreach (var s in r.Segments) Console.WriteLine($"  đoạn giấy {s.Axis} ({s.A.X / 2.8346:0.0},{s.A.Y / 2.8346:0.0})-({s.B.X / 2.8346:0.0},{s.B.Y / 2.8346:0.0}) mm{(s.Bridged ? " [khe]" : "")} {string.Join("+", s.Dimensions.Select(d => d.Value))}");
        foreach (var c in r.Callouts) Console.WriteLine($"  ô {c.Item} tại ({c.BoxAt.X / 2.8346:0.0}, {c.BoxAt.Y / 2.8346:0.0}) mm trỏ {(c.Target.HasValue ? $"({c.Target.Value.X / 2.8346:0.0}, {c.Target.Value.Y / 2.8346:0.0})" : "-")}");
        foreach (var d in r.UnassignedDimensions) Console.WriteLine($"  kích thước chưa gán {d.Value} {d.By} đường {(d.LineA.HasValue ? $"({d.LineA.Value.X / 2.8346:0.0},{d.LineA.Value.Y / 2.8346:0.0})-({d.LineB!.Value.X / 2.8346:0.0},{d.LineB.Value.Y / 2.8346:0.0})" : "-")}");
    }

    foreach (var i in model.Issues) Console.WriteLine($"  [{i.Severity}] {i.RuleId} {i.Message}");
    // Bước 1: kiểm kê mọi thứ bản vẽ nói và đối chiếu. Bước 2 chỉ dựng khi kiểm kê không còn khoảng trống, trừ khi --force.
    var extraction = run.Extraction;
    var reportIndex = Array.IndexOf(a, "--report");
    var reportPath = reportIndex >= 0 && reportIndex + 1 < a.Length ? a[reportIndex + 1] : outPath != null ? Path.ChangeExtension(outPath, null).Replace(".ir", "") + ".kiem-ke.md" : null;
    if (reportPath != null)
    {
        File.WriteAllText(reportPath, extraction.ToMarkdown());
        Console.WriteLine($"Đã ghi bản kiểm kê vào {reportPath}.");
    }

    Console.WriteLine($"Kiểm kê: {(extraction.Complete ? "đủ để dựng" : extraction.Gaps + " khoảng trống")}, {extraction.Warnings} điểm cần xem.");
    foreach (var c in extraction.Checks.Where(c => c.Status != "ok")) Console.WriteLine($"  [{(c.Status == "gap" ? "THIẾU" : "xem")}] {c.Group}: {c.Subject}, bản vẽ {c.Expected}, đọc được {c.Found}{(c.Note != null ? ", " + c.Note : "")}");
    if (outPath != null)
    {
        if (extraction.Complete || Array.IndexOf(a, "--force") >= 0)
        {
            File.WriteAllText(outPath, IrJson.Serialize(model));
            Console.WriteLine($"Đã ghi IR vào {outPath}.");
        }
        else Console.WriteLine("Chưa ghi IR vì kiểm kê còn khoảng trống; thêm --force để ghi dù vậy.");
    }

    return extraction.Complete ? 0 : 1;
}

// S6-04: xử lý nhiều bản vẽ với một hồ sơ, ghi IR, khối lượng, báo cáo cho từng bản và một bảng tổng hợp.
// S6-05: nếu có đáp án <tên>.expected.json trong thư mục đáp án hoặc cạnh bản vẽ thì chấm điểm hồi quy. Mã thoát 1 khi có bản vẽ không đạt.
static int Batch(string[] a)
{
    string? Option(string name)
    {
        var i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }

    var profilePath = Option("--profile");
    var outDir = Option("--out");
    if (profilePath == null || outDir == null) { PrintUsage(); return 2; }
    var inputs = new List<string>();
    for (var i = 0; i < a.Length; i++)
    {
        if (a[i].StartsWith("--", StringComparison.Ordinal)) { i++; continue; }
        if (Directory.Exists(a[i])) inputs.AddRange(Directory.GetFiles(a[i], "*.dxf").Concat(Directory.GetFiles(a[i], "*.dwg")).OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        else if (File.Exists(a[i])) inputs.Add(a[i]);
    }

    if (inputs.Count == 0) { Console.Error.WriteLine("Không có bản vẽ nào."); return 2; }
    var profileResult = ProfileFiles.Load(profilePath);
    if (!profileResult.Ok)
    {
        foreach (var e in profileResult.Errors) Console.Error.WriteLine("  " + e);
        return 3;
    }

    var catalog = CatalogFiles.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "catalog"));
    var equipment = EquipmentFiles.LoadLayers(Path.Combine(AppContext.BaseDirectory, "equipment"));
    Directory.CreateDirectory(outDir);
    var summary = new List<string> { "Bản vẽ;Đối tượng;Chưa phân loại %;Nhãn khớp;Đoạn chạy;Có nhãn;Đoạn ống;Vấn đề mở;Chặn;Điểm hồi quy;Thời gian s" };
    var anyFail = false;
    foreach (var input in inputs)
    {
        var name = Path.GetFileNameWithoutExtension(input);
        try
        {
            var reader = DxfDrawingReader.Open(input);
            var entities = reader.ReadEntities(EntityFilter.All);
            var sum = reader.GetSummary();
            var source = new SourceInfo { File = Path.GetFileName(input), Fingerprint = sum.Fingerprint, ReadAt = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture), EntityCount = entities.Count, UnitsNeedConfirmation = reader.Units.NeedsConfirmation };
            var sessionPath = PipingAgent.Core.Decisions.SessionFile.PathFor(input);
            var decisions = File.Exists(sessionPath) ? PipingAgent.Core.Decisions.SessionFile.Deserialize(File.ReadAllText(sessionPath)).Decisions : null;
            var result = RecognitionPipeline.Run(entities, profileResult.Profile!, catalog, source, equipment, null, decisions);
            File.WriteAllText(Path.Combine(outDir, name + ".ir.json"), IrJson.Serialize(result.Model));
            File.WriteAllText(Path.Combine(outDir, name + ".bom.csv"), PipingAgent.Core.Export.QuantityExport.ToCsv(result.Model));
            File.WriteAllText(Path.Combine(outDir, name + ".report.md"), PipingAgent.Core.Export.ConversionReport.ToMarkdown(result));
            var score = "-";
            var expectedPath = new[] { Path.Combine(Option("--expected-dir") ?? Path.GetDirectoryName(input)!, name + ".expected.json"), Path.ChangeExtension(input, ".expected.json") }.FirstOrDefault(File.Exists);
            if (expectedPath != null)
            {
                var expected = System.Text.Json.JsonSerializer.Deserialize<PipingAgent.Core.Export.RegressionScorer.Expected>(File.ReadAllText(expectedPath), IrJson.CreateOptions(false))!;
                var sc = PipingAgent.Core.Export.RegressionScorer.Compare(result.Model, expected);
                score = $"{sc.Passed}/{sc.Passed + sc.Failed}" + (sc.MissingLines.Count > 0 ? $" thiếu {sc.MissingLines.Count} tuyến" : string.Empty);
                File.WriteAllLines(Path.Combine(outDir, name + ".score.txt"), sc.Checks.Concat(sc.MissingLines.Select(l => "THIẾU tuyến " + l)).Concat(sc.ExtraLines.Select(l => "THỪA tuyến " + l)));
                if (!sc.Ok) anyFail = true;
            }

            var r = result.Classification.Report;
            var m = result.Model;
            summary.Add(string.Join(";", name, r.Total, (r.UnclassifiedRatio * 100).ToString("0.0", CultureInfo.InvariantCulture), result.TagReads.Count(t => t.Matched), m.Runs.Count,
                m.Runs.Count(x => x.TagRef != null), m.Segments.Count, m.Issues.Count(i => i.Status == IssueStatus.Open), m.Issues.Count(i => i.Status == IssueStatus.Open && i.Severity == Severity.Blocker), score,
                result.Elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture)));
            Console.WriteLine($"{name}: {m.Runs.Count} đoạn chạy, {m.Segments.Count} đoạn ống, {m.Issues.Count(i => i.Status == IssueStatus.Open)} vấn đề, điểm {score}");
        }
        catch (Exception ex)
        {
            summary.Add($"{name};LỖI;{ex.Message}");
            Console.Error.WriteLine($"{name}: LỖI {ex.Message}");
            anyFail = true;
        }
    }

    File.WriteAllText(Path.Combine(outDir, "tong-hop.csv"), "\uFEFF" + string.Join("\n", summary) + "\n");
    Console.WriteLine($"Đã xử lý {inputs.Count} bản vẽ, tổng hợp ở {Path.Combine(outDir, "tong-hop.csv")}.");
    return anyFail ? 1 : 0;
}

static int Recognize(string[] a)
{
    if (a.Length < 2) { PrintUsage(); return 2; }
    string? Option(string name)
    {
        var i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }

    var profileResult = ProfileFiles.Load(a[1]);
    if (!profileResult.Ok)
    {
        Console.Error.WriteLine("Hồ sơ quy ước có lỗi:");
        foreach (var e in profileResult.Errors) Console.Error.WriteLine("  " + e);
        return 3;
    }

    var catalogDir = Option("--catalog") ?? Path.Combine(AppContext.BaseDirectory, "catalog");
    if (!Directory.Exists(catalogDir)) catalogDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "catalog");
    var catalog = CatalogFiles.LoadDirectory(catalogDir);
    var equipmentDir = Option("--equipment") ?? Path.Combine(Path.GetDirectoryName(catalogDir)!, "equipment");
    var equipment = EquipmentFiles.LoadLayers(equipmentDir, Option("--equipment-company"), Option("--equipment-project"));
    foreach (var e in equipment.Errors) Console.Error.WriteLine("Thư viện thiết bị: " + e);

    double? unitScale = null;
    var unitText = Option("--unit-scale");
    if (unitText != null) unitScale = double.Parse(unitText, CultureInfo.InvariantCulture);
    var reader = DxfDrawingReader.Open(a[0], unitScale);
    if (reader.Units.NeedsConfirmation) Console.WriteLine("CẦN XÁC NHẬN đơn vị: " + reader.Units.Note);
    var entities = reader.ReadEntities(EntityFilter.All);
    var summary = reader.GetSummary();
    var source = new SourceInfo
    {
        File = Path.GetFileName(a[0]),
        Fingerprint = summary.Fingerprint,
        ReadAt = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
        EntityCount = entities.Count,
    };

    source.UnitsNeedConfirmation = reader.Units.NeedsConfirmation;
    var sessionPath = Option("--session") ?? PipingAgent.Core.Decisions.SessionFile.PathFor(a[0]);
    List<Decision>? decisions = null;
    if (File.Exists(sessionPath))
    {
        decisions = PipingAgent.Core.Decisions.SessionFile.Deserialize(File.ReadAllText(sessionPath)).Decisions;
        Console.WriteLine($"Đọc {decisions.Count} quyết định từ {sessionPath}.");
    }

    var result = RecognitionPipeline.Run(entities, profileResult.Profile!, catalog, source, equipment, null, decisions);
    Console.WriteLine($"{source.File}: {entities.Count} đối tượng, hồ sơ {profileResult.Profile!.Id} v{profileResult.Profile.Version}, danh mục: {string.Join(", ", catalog.Standards)}");
    Console.Write(RecognitionReport.ToText(result));

    var outPath = Option("--out");
    if (outPath != null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        File.WriteAllText(outPath, IrJson.Serialize(result.Model));
        Console.WriteLine($"Đã ghi IR vào {outPath}.");
    }

    var bomPath = Option("--bom");
    if (bomPath != null)
    {
        File.WriteAllText(bomPath, PipingAgent.Core.Export.QuantityExport.ToCsv(result.Model));
        Console.WriteLine($"Đã ghi bảng khối lượng vào {bomPath}.");
    }

    var reportPath = Option("--report");
    if (reportPath != null)
    {
        File.WriteAllText(reportPath, PipingAgent.Core.Export.ConversionReport.ToMarkdown(result));
        Console.WriteLine($"Đã ghi báo cáo vào {reportPath}.");
    }

    var expectedPath = Option("--expected");
    if (expectedPath != null)
    {
        var expected = System.Text.Json.JsonSerializer.Deserialize<PipingAgent.Core.Export.RegressionScorer.Expected>(File.ReadAllText(expectedPath), IrJson.CreateOptions(false))!;
        var score = PipingAgent.Core.Export.RegressionScorer.Compare(result.Model, expected);
        foreach (var c in score.Checks) Console.WriteLine("  " + c);
        foreach (var l in score.MissingLines) Console.WriteLine("  THIẾU tuyến " + l);
        foreach (var l in score.ExtraLines) Console.WriteLine("  THỪA tuyến " + l);
        Console.WriteLine($"Điểm hồi quy: {score.Passed} đạt, {score.Failed} sai, sai im lặng {score.SilentErrorRate:P0}.");
        if (!score.Ok) return 1;
    }

    return result.Model.Issues.Any(i => i.Severity == Severity.Blocker) ? 1 : 0;
}

static int Dump(string[] a)
{
    if (a.Length < 2) { PrintUsage(); return 2; }
    var explode = a.Contains("--explode");
    double? unitScale = null;
    var unitIndex = Array.IndexOf(a, "--unit-scale");
    if (unitIndex >= 0 && unitIndex + 1 < a.Length) unitScale = double.Parse(a[unitIndex + 1], CultureInfo.InvariantCulture);

    var reader = DxfDrawingReader.Open(a[0], unitScale);
    var entities = reader.ReadEntities(new EntityFilter { ExplodeBlocks = explode });
    var dump = new SourceDump
    {
        Reader = "dxf",
        ReadAt = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
        Summary = reader.GetSummary(),
        Entities = entities.ToList(),
    };
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(a[1]))!);
    using (var stream = File.Create(a[1])) SourceJson.Serialize(dump, stream);
    Console.WriteLine($"Đã ghi {entities.Count} đối tượng vào {a[1]}. Bỏ qua: {reader.Skipped.Count}. Đơn vị: {reader.Units.Note}");
    if (reader.Units.NeedsConfirmation) Console.WriteLine("CẦN XÁC NHẬN đơn vị. Dùng --unit-scale để đặt hệ số.");
    return 0;
}

static int Summary(string[] a)
{
    if (a.Length < 1) { PrintUsage(); return 2; }
    var reader = DxfDrawingReader.Open(a[0]);
    var summary = reader.GetSummary();
    Console.WriteLine($"{summary.File}: {summary.EntityCount} đối tượng, {summary.Layers.Count} layer, {summary.Blocks.Count} block, định dạng {summary.FormatVersion}");
    Console.WriteLine($"Đơn vị: {reader.Units.Note}");
    foreach (var pair in summary.CountsByKind.OrderByDescending(p => p.Value)) Console.WriteLine($"  {pair.Key,-12} {pair.Value,6}");
    Console.WriteLine($"Dấu vân tay: {summary.Fingerprint}");
    return 0;
}

static int Compare(string[] a)
{
    if (a.Length < 2) { PrintUsage(); return 2; }
    var max = 20;
    var maxIndex = Array.IndexOf(a, "--max");
    if (maxIndex >= 0 && maxIndex + 1 < a.Length) max = int.Parse(a[maxIndex + 1], CultureInfo.InvariantCulture);

    var left = SourceJson.Deserialize(File.ReadAllText(a[0]));
    var right = SourceJson.Deserialize(File.ReadAllText(a[1]));
    var result = ReaderComparison.Compare(left, right);

    Console.WriteLine($"A: {left.Reader} {left.Entities.Count} đối tượng, dấu vân tay {left.Summary.Fingerprint}");
    Console.WriteLine($"B: {right.Reader} {right.Entities.Count} đối tượng, dấu vân tay {right.Summary.Fingerprint}");
    Console.WriteLine($"Trùng hoàn toàn: {result.Identical}. Chỉ có ở A: {result.OnlyInLeft.Count}. Chỉ có ở B: {result.OnlyInRight.Count}. Khác nội dung: {result.Different.Count}.");

    foreach (var group in result.Different.GroupBy(d => d.Field).OrderByDescending(g => g.Count()))
    {
        Console.WriteLine($"  Khác ở trường {group.Key}: {group.Count()}, theo loại: " +
            string.Join(", ", group.GroupBy(d => d.Kind).Select(k => $"{k.Key} {k.Count()}")));
    }

    foreach (var handle in result.OnlyInLeft.Take(max)) Console.WriteLine($"  Chỉ A: {handle}");
    foreach (var handle in result.OnlyInRight.Take(max)) Console.WriteLine($"  Chỉ B: {handle}");
    foreach (var d in result.Different.Take(max))
    {
        Console.WriteLine($"  {d.Handle} {d.Kind} {d.Field}:");
        Console.WriteLine($"     A: {Truncate(d.Left)}");
        Console.WriteLine($"     B: {Truncate(d.Right)}");
    }

    return result.Identical ? 0 : 1;
}

static string Truncate(string s) => s.Length <= 160 ? s : s.Substring(0, 157) + "...";
