using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using PipingAgent.Core.Ir.Json;

namespace PipingAgent.Core.Catalog
{
    /// <summary>Tệp danh mục ống hoặc phụ kiện. Một tệp một tiêu chuẩn. Định dạng ở <c>data/catalog/README.md</c>.</summary>
    public sealed class CatalogFile
    {
        [JsonRequired]
        public string SchemaVersion { get; set; } = CoreInfo.CatalogSchemaVersion;

        /// <summary>Mã tiêu chuẩn, ví dụ <c>ASME-B36.10</c>, <c>ASME-B16.9</c>.</summary>
        [JsonRequired]
        public string Standard { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string Units { get; set; } = "mm";

        /// <summary>Đã được chuyên gia đối chiếu với tiêu chuẩn gốc chưa, S2-16. Chưa thì mọi kết quả tra kèm cảnh báo.</summary>
        public bool Verified { get; set; }

        public string? Note { get; set; }

        /// <summary>Kích thước ống theo DN: đường kính ngoài và chiều dày theo schedule.</summary>
        public List<PipeSize> Pipes { get; set; } = new List<PipeSize>();

        /// <summary>Kích thước phụ kiện theo loại và DN.</summary>
        public List<FittingSize> Fittings { get; set; } = new List<FittingSize>();
    }

    public sealed class PipeSize
    {
        public int Dn { get; set; }

        public string? Nps { get; set; }

        /// <summary>Đường kính ngoài, mm.</summary>
        public double Od { get; set; }

        /// <summary>Chiều dày theo schedule, mm. Khoá ví dụ <c>STD</c>, <c>XS</c>, <c>40</c>, <c>80</c>.</summary>
        public Dictionary<string, double>? Wall { get; set; }
    }

    public sealed class FittingSize
    {
        /// <summary>Loại: <c>ELL90LR</c>, <c>ELL90SR</c>, <c>ELL45LR</c>, <c>TEE</c>, <c>RED</c>, <c>CAP</c>.</summary>
        public string Type { get; set; } = string.Empty;

        public int Dn { get; set; }

        /// <summary>DN nhánh với tê thu, DN nhỏ với côn thu. Null với phụ kiện cùng cỡ.</summary>
        public int? Dn2 { get; set; }

        /// <summary>Kích thước đặt tên theo bảng của tiêu chuẩn, ví dụ với co là <c>A</c> tâm tới đầu, với tê <c>C</c> và <c>M</c>, với côn <c>H</c>.</summary>
        public Dictionary<string, double> Dims { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>Kết quả tra. Không tìm thấy thì <see cref="Found"/> sai và <see cref="Reason"/> nói vì sao, không ném lỗi.</summary>
    public readonly struct CatalogLookup<T>
    {
        public CatalogLookup(T value, bool verified)
        {
            Found = true;
            Value = value;
            Verified = verified;
            Reason = null;
        }

        public CatalogLookup(string reason)
        {
            Found = false;
            Value = default!;
            Verified = false;
            Reason = reason;
        }

        public bool Found { get; }

        public T Value { get; }

        /// <summary>Danh mục chứa giá trị này đã được ký xác nhận.</summary>
        public bool Verified { get; }

        public string? Reason { get; }
    }

    /// <summary>
    /// Danh mục ống và phụ kiện đã nạp, S2-10. Tra theo tiêu chuẩn, loại và DN.
    /// Nhiều tệp nạp chung; tiêu chuẩn mặc định lấy từ hồ sơ quy ước.
    /// </summary>
    public sealed class PipeCatalog
    {
        private static readonly JsonSerializerOptions Options = IrJson.CreateOptions(indented: true);

        private readonly Dictionary<string, CatalogFile> _files = new Dictionary<string, CatalogFile>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> Standards => _files.Keys.ToList();

        public static CatalogFile ParseFile(string json)
        {
            var file = JsonSerializer.Deserialize<CatalogFile>(json, Options) ?? throw new IrFormatException("Danh mục rỗng.");
            if (file.SchemaVersion != CoreInfo.CatalogSchemaVersion) throw new IrFormatException($"Phiên bản danh mục '{file.SchemaVersion}' không đọc được.");
            if (file.Units != "mm") throw new IrFormatException($"Danh mục '{file.Standard}' dùng đơn vị '{file.Units}', hiện chỉ hỗ trợ mm.");
            return file;
        }

        public void Add(CatalogFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (string.IsNullOrWhiteSpace(file.Standard)) throw new IrFormatException("Danh mục thiếu mã tiêu chuẩn.");
            _files[file.Standard] = file;
        }

        public void AddJson(string json) => Add(ParseFile(json));

        public bool HasStandard(string standard) => _files.ContainsKey(standard);

        /// <summary>Đúng khi mọi danh mục đã nạp đều được ký xác nhận.</summary>
        public bool AllVerified => _files.Count > 0 && _files.Values.All(f => f.Verified);

        /// <summary>Đường kính ngoài theo DN.</summary>
        public CatalogLookup<double> PipeOd(string standard, int dn)
        {
            var p = Pipe(standard, dn);
            return p.Found ? new CatalogLookup<double>(p.Value.Od, p.Verified) : new CatalogLookup<double>(p.Reason!);
        }

        public CatalogLookup<PipeSize> Pipe(string standard, int dn)
        {
            if (!_files.TryGetValue(standard ?? string.Empty, out var file))
            {
                return new CatalogLookup<PipeSize>($"Chưa nạp danh mục ống '{standard}'.");
            }

            var size = file.Pipes.FirstOrDefault(p => p.Dn == dn);
            if (size == null) return new CatalogLookup<PipeSize>($"DN{dn} không có trong {file.Standard}.");
            return new CatalogLookup<PipeSize>(size, file.Verified);
        }

        public CatalogLookup<FittingSize> Fitting(string standard, string type, int dn, int? dn2 = null)
        {
            if (!_files.TryGetValue(standard ?? string.Empty, out var file))
            {
                return new CatalogLookup<FittingSize>($"Chưa nạp danh mục phụ kiện '{standard}'.");
            }

            // Khớp đúng cỡ nhánh trước; mục không ghi dn2 áp cho mọi cỡ nhánh, ví dụ chiều dài côn thu theo đầu lớn.
            var candidates = file.Fittings.Where(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase) && x.Dn == dn).ToList();
            var f = candidates.FirstOrDefault(x => x.Dn2 == dn2) ?? (dn2.HasValue ? candidates.FirstOrDefault(x => x.Dn2 == null) : null);
            if (f == null)
            {
                var size = dn2.HasValue ? $"DN{dn}x{dn2}" : $"DN{dn}";
                return new CatalogLookup<FittingSize>($"{type} {size} không có trong {file.Standard}.");
            }

            return new CatalogLookup<FittingSize>(f, file.Verified);
        }

        /// <summary>Một kích thước đặt tên của phụ kiện, ví dụ <c>A</c> của co.</summary>
        public CatalogLookup<double> FittingDim(string standard, string type, int dn, string dim, int? dn2 = null)
        {
            var f = Fitting(standard, type, dn, dn2);
            if (!f.Found) return new CatalogLookup<double>(f.Reason!);
            if (!f.Value.Dims.TryGetValue(dim, out var v)) return new CatalogLookup<double>($"{type} DN{dn} không có kích thước '{dim}'.");
            return new CatalogLookup<double>(v, f.Verified);
        }

        /// <summary>Bán kính uốn co 90 độ, bằng kích thước tâm tới đầu <c>A</c>.</summary>
        public CatalogLookup<double> ElbowRadius(string standard, int dn, bool longRadius = true) =>
            FittingDim(standard, longRadius ? FittingTypes.Elbow90LR : FittingTypes.Elbow90SR, dn, "A");

        /// <summary>Tâm tới đầu chạy <c>C</c> và tâm tới đầu nhánh <c>M</c> của tê cùng cỡ.</summary>
        public CatalogLookup<(double C, double M)> TeeDims(string standard, int dn)
        {
            var f = Fitting(standard, FittingTypes.Tee, dn);
            if (!f.Found) return new CatalogLookup<(double, double)>(f.Reason!);
            if (!f.Value.Dims.TryGetValue("C", out var c) || !f.Value.Dims.TryGetValue("M", out var m))
            {
                return new CatalogLookup<(double, double)>($"Tê DN{dn} thiếu kích thước C hoặc M.");
            }

            return new CatalogLookup<(double, double)>((c, m), f.Verified);
        }

        /// <summary>Mã tham chiếu danh mục để ghi vào IR, ví dụ <c>ASME-B16.9/ELL90LR/DN150</c>.</summary>
        public static string Ref(string standard, string type, int dn, int? dn2 = null) =>
            dn2.HasValue ? $"{standard}/{type}/DN{dn}x{dn2}" : $"{standard}/{type}/DN{dn}";
    }

    public static class FittingTypes
    {
        public const string Elbow90LR = "ELL90LR";
        public const string Elbow90SR = "ELL90SR";
        public const string Elbow45LR = "ELL45LR";
        public const string Tee = "TEE";
        public const string ReducerConcentric = "RED";
    }
}
