// Đánh giá thư viện đọc DXF cho .NET (việc S0-13, quyết định Q11).
// Cách dùng: dotnet run -c Release --project tools/DxfLibEval -- <tệp.dxf> [truth.json]
// truth.json do tools/dxf_ground_truth.py sinh ra bằng ezdxf, dùng làm số liệu đối chứng.
using System.Diagnostics;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;
if (args.Length < 1)
{
    Console.Error.WriteLine("Thiếu đường dẫn tệp DXF.");
    return 2;
}

var path = Path.GetFullPath(args[0]);
Summary? truth = args.Length > 1
    ? JsonSerializer.Deserialize<Summary>(File.ReadAllText(args[1]), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    : null;

var readers = new (string Name, Func<string, Summary> Read)[]
{
    ("netDxf.netstandard 3.0.1", ReadWithNetDxf),
    ("ACadSharp 3.8.35", ReadWithACadSharp),
    ("IxMilia.Dxf 0.8.4", ReadWithIxMilia),
};

Console.WriteLine($"Tệp: {Path.GetFileName(path)}, {new FileInfo(path).Length / 1024.0:F0} KB");
if (truth != null)
{
    Console.WriteLine($"Đối chứng {truth.Library}: {truth.Total} đối tượng, {truth.VertexTotal} đỉnh, {truth.LayerCount} layer");
}

foreach (var (name, read) in readers)
{
    Console.WriteLine();
    Console.WriteLine("== " + name);
    try
    {
        read(path); // Lần đầu để nạp mã, không tính giờ.
        var times = new List<double>();
        Summary summary = null!;
        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            summary = read(path);
            times.Add(watch.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        Console.WriteLine($"Thời gian đọc, trung vị 5 lần: {times[2]:F1} ms");
        Console.WriteLine($"Đối tượng: {summary.Total}, đỉnh polyline: {summary.VertexTotal}, layer: {summary.LayerCount}, có handle: {summary.EntitiesWithHandle}");
        Console.WriteLine("Theo loại: " + string.Join(", ", summary.Counts.Select(pair => $"{pair.Key} {pair.Value}")));
        if (truth != null)
        {
            Compare(summary, truth);
        }
    }
    catch (Exception exception)
    {
        Console.WriteLine($"LỖI: {exception.GetType().Name}: {exception.Message}");
    }
}

return 0;

static void Compare(Summary actual, Summary truth)
{
    var problems = new List<string>();
    foreach (var kind in truth.Counts.Keys.Union(actual.Counts.Keys).OrderBy(k => k, StringComparer.Ordinal))
    {
        truth.Counts.TryGetValue(kind, out var expected);
        actual.Counts.TryGetValue(kind, out var got);
        if (expected != got)
        {
            problems.Add($"{kind}: đối chứng {expected}, đọc được {got}");
        }
    }

    if (actual.VertexTotal != truth.VertexTotal)
    {
        problems.Add($"số đỉnh: đối chứng {truth.VertexTotal}, đọc được {actual.VertexTotal}");
    }

    if (!actual.Texts.SequenceEqual(truth.Texts, StringComparer.Ordinal))
    {
        var missing = truth.Texts.Except(actual.Texts).Take(3).ToArray();
        var extra = actual.Texts.Except(truth.Texts).Take(3).ToArray();
        problems.Add($"nội dung chữ khác. Thiếu: [{string.Join(" | ", missing)}]. Thừa: [{string.Join(" | ", extra)}]");
    }

    if (!actual.Inserts.SequenceEqual(truth.Inserts, StringComparer.Ordinal))
    {
        problems.Add("tên block của INSERT khác");
    }

    if (actual.LayerCount != truth.LayerCount)
    {
        problems.Add($"số layer: đối chứng {truth.LayerCount}, đọc được {actual.LayerCount}");
    }

    if (actual.EntitiesWithHandle != truth.EntitiesWithHandle)
    {
        problems.Add($"số đối tượng có handle: đối chứng {truth.EntitiesWithHandle}, đọc được {actual.EntitiesWithHandle}");
    }

    Console.WriteLine(problems.Count == 0
        ? "So với đối chứng: KHỚP hoàn toàn"
        : "So với đối chứng: KHÁC\n  - " + string.Join("\n  - ", problems));
}

static Summary ReadWithNetDxf(string path)
{
    var document = netDxf.DxfDocument.Load(path);
    var summary = new Summary { Library = "netDxf" };
    foreach (var entity in document.Entities.All)
    {
        var kind = entity switch
        {
            netDxf.Entities.Polyline2D => "LWPOLYLINE",
            netDxf.Entities.Polyline3D => "POLYLINE",
            netDxf.Entities.PolyfaceMesh => "POLYLINE",
            netDxf.Entities.PolygonMesh => "POLYLINE",
            _ => entity.Type.ToString().ToUpperInvariant(),
        };
        summary.Add(kind, !string.IsNullOrEmpty(entity.Handle));
        switch (entity)
        {
            case netDxf.Entities.Polyline2D polyline:
                summary.VertexTotal += polyline.Vertexes.Count;
                break;
            case netDxf.Entities.Polyline3D polyline3D:
                summary.VertexTotal += polyline3D.Vertexes.Count;
                break;
            case netDxf.Entities.PolyfaceMesh mesh:
                summary.VertexTotal += mesh.Vertexes.Length + mesh.Faces.Count;
                break;
            case netDxf.Entities.Text text:
                summary.Texts.Add(text.Value);
                break;
            case netDxf.Entities.MText mtext:
                summary.Texts.Add(mtext.Value);
                break;
            case netDxf.Entities.Insert insert:
                summary.Inserts.Add(insert.Block.Name);
                break;
        }
    }

    summary.LayerCount = document.Layers.Count;
    return summary.Finish();
}

static Summary ReadWithACadSharp(string path)
{
    ACadSharp.CadDocument document;
    if (path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
    {
        // Chỉ ACadSharp đọc được DWG. Hai thư viện kia sẽ báo lỗi với tệp này.
        using var dwgReader = new ACadSharp.IO.DwgReader(path);
        document = dwgReader.Read();
    }
    else
    {
        using var reader = new ACadSharp.IO.DxfReader(path);
        document = reader.Read();
    }

    var summary = new Summary { Library = "ACadSharp" };
    foreach (var entity in document.Entities)
    {
        summary.Add(entity.ObjectName, entity.Handle != 0);
        switch (entity)
        {
            case ACadSharp.Entities.LwPolyline polyline:
                summary.VertexTotal += polyline.Vertices.Count;
                break;
            case ACadSharp.Entities.PolyfaceMesh mesh:
                // ACadSharp tách đỉnh và mặt. ezdxf đếm chung cả hai trong danh sách đỉnh.
                summary.VertexTotal += mesh.Vertices.Count() + mesh.Faces.Count();
                break;
            case ACadSharp.Entities.IPolyline polylineBase:
                summary.VertexTotal += polylineBase.Vertices.Count();
                break;
            case ACadSharp.Entities.MText mtext:
                summary.Texts.Add(mtext.Value);
                break;
            case ACadSharp.Entities.TextEntity text:
                summary.Texts.Add(text.Value);
                break;
            case ACadSharp.Entities.Insert insert:
                summary.Inserts.Add(insert.Block.Name);
                break;
        }
    }

    summary.LayerCount = document.Layers.Count();
    return summary.Finish();
}

// IxMilia.Dxf 0.8.4 không có thuộc tính Handle công khai trên DxfEntity.
// Hàm này kiểm tra bằng phản chiếu để kết quả tự đổi nếu bản sau có thêm.
static bool HasPublicHandle(object entity)
{
    var property = entity.GetType().GetProperty(
        "Handle", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
    var text = property?.GetValue(entity)?.ToString();
    return !string.IsNullOrEmpty(text) && text != "0";
}

static Summary ReadWithIxMilia(string path)
{
    var file = IxMilia.Dxf.DxfFile.Load(path);
    var summary = new Summary { Library = "IxMilia.Dxf" };
    foreach (var entity in file.Entities)
    {
        var kind = entity switch
        {
            IxMilia.Dxf.Entities.DxfLwPolyline => "LWPOLYLINE",
            IxMilia.Dxf.Entities.DxfPolyline => "POLYLINE",
            IxMilia.Dxf.Entities.DxfMText => "MTEXT",
            _ => entity.EntityType.ToString().ToUpperInvariant(),
        };
        summary.Add(kind, HasPublicHandle(entity));
        switch (entity)
        {
            case IxMilia.Dxf.Entities.DxfLwPolyline polyline:
                summary.VertexTotal += polyline.Vertices.Count;
                break;
            case IxMilia.Dxf.Entities.DxfPolyline polyline:
                summary.VertexTotal += polyline.Vertices.Count;
                break;
            case IxMilia.Dxf.Entities.DxfText text:
                summary.Texts.Add(text.Value);
                break;
            case IxMilia.Dxf.Entities.DxfMText mtext:
                summary.Texts.Add(mtext.Text);
                break;
            case IxMilia.Dxf.Entities.DxfInsert insert:
                summary.Inserts.Add(insert.Name);
                break;
        }
    }

    summary.LayerCount = file.Layers.Count;
    return summary.Finish();
}

internal sealed class Summary
{
    public string Library { get; set; } = string.Empty;

    public SortedDictionary<string, int> Counts { get; set; } = new(StringComparer.Ordinal);

    public int Total { get; set; }

    public int VertexTotal { get; set; }

    public List<string> Texts { get; set; } = new();

    public List<string> Inserts { get; set; } = new();

    public int LayerCount { get; set; }

    public int EntitiesWithHandle { get; set; }

    public void Add(string kind, bool hasHandle)
    {
        Counts[kind] = Counts.TryGetValue(kind, out var current) ? current + 1 : 1;
        Total++;
        if (hasHandle)
        {
            EntitiesWithHandle++;
        }
    }

    public Summary Finish()
    {
        Texts.Sort(StringComparer.Ordinal);
        Inserts.Sort(StringComparer.Ordinal);
        return this;
    }
}
