using System;
using System.IO;
using System.Linq;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Geometry;
using PipingAgent.Core.Graph;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;
using PipingAgent.Core.Rules;
using PipingAgent.Core.Source;

namespace PipingAgent.Iso
{
    /// <summary>Kết quả một lần chạy trọn đường isometric sang IR: trang nguồn, đồ thị giấy, mô hình 3D và bản kiểm kê.</summary>
    public sealed class IsoPipelineResult
    {
        public string Path { get; set; } = string.Empty;

        public bool IsDwg { get; set; }

        public bool Isogen { get; set; }

        public IsoPdfReader.Page Page { get; set; } = new IsoPdfReader.Page();

        public IsoResult Analysis { get; set; } = new IsoResult();

        public PipingModel Model { get; set; } = new PipingModel();

        public GraphStats GraphStats { get; set; } = new GraphStats();

        public IsoExtraction Extraction { get; set; } = new IsoExtraction();

        /// <summary>Bộ luật chung đã chạy trên mô hình iso, vấn đề nằm trong <see cref="PipingModel.Issues"/>.</summary>
        public RuleRunResult Rules { get; set; } = new RuleRunResult();
    }

    /// <summary>
    /// Một đường chạy chung cho CLI và plug-in AutoCAD: đọc tờ isometric PDF, DWG hoặc DXF, dựng đồ thị giấy, suy toạ độ thật,
    /// dựng đồ thị 3D, gắn cấu kiện vào đoạn, rồi kiểm kê đối chiếu với bản vẽ. Hai nơi gọi chỉ khác cách in và nơi ghi tệp.
    /// </summary>
    public static class IsoPipeline
    {
        public static bool IsDrawingFile(string path) =>
            path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase);

        public static IsoPipelineResult Run(string path, PipeCatalog? catalog, ConventionProfile? profile = null)
        {
            var result = new IsoPipelineResult { Path = path, IsDwg = IsDrawingFile(path) };
            result.Page = result.IsDwg ? IsoDxfReader.Read(path) : IsoPdfReader.Read(path)[0];
            result.Isogen = IsoDxfReader.LooksLikeIsogen(result.Page.Entities);
            profile ??= DefaultProfile();
            var options = result.Isogen ? IsoOptions.ForIsogen() : new IsoOptions();
            // Cùng một bảng OD với GraphBuilder, thay cho bảng chép tay trong IsoGraph.
            if (catalog != null) options.OdLookup = dn => catalog.PipeOd(profile.PipeStandard, dn) is { Found: true } od ? od.Value : (double?)null;
            var graph = new IsoGraph(options);
            result.Analysis = graph.Analyze(result.Page.Entities, result.Page.Width, result.Page.Height);
            var file = System.IO.Path.GetFileName(path);
            result.Model = graph.ToModel(result.Analysis, file);
            (result.GraphStats, result.Rules) = Complete(result.Model, catalog, profile);
            result.Extraction = IsoExtraction.Build(result.Analysis, result.Model, file);
            return result;
        }

        public static ConventionProfile DefaultProfile() => new ConventionProfile
        {
            Id = "iso-default",
            PipeStandard = "ASME-B36.10",
            FittingStandard = "ASME-B16.9",
            // Trên tờ iso, mặt bích sát co hay tê là bố trí thật, không phải nhận diện sai như trên mặt bằng.
            Rules = { ["R-GEO-04"] = "off" },
        };

        /// <summary>Phần chung sau khi có mô hình từ một tờ hay từ nhiều tờ gộp: dựng đồ thị 3D, gắn cấu kiện, chạy bộ luật.</summary>
        public static (GraphStats Stats, RuleRunResult Rules) Complete(PipingModel model, PipeCatalog? catalog, ConventionProfile? profile = null)
        {
            profile ??= DefaultProfile();
            var stats = new GraphBuilder(profile, catalog).Build(model);
            LinkComponents(model);
            return (stats, RuleRunner.Run(model, profile));
        }

        /// <summary>Gắn mỗi cấu kiện vào đoạn ống gần nhất theo mặt bằng và cao độ, để kế hoạch dựng biết cắt ống chỗ nào.</summary>
        public static void LinkComponents(PipingModel model)
        {
            var nodes = model.Nodes.ToDictionary(n => n.Id);
            foreach (var c in model.Components)
            {
                var seg = model.Segments
                    .Select(sg => (sg, d: new Segment2(nodes[sg.NodeA].Position.Plan, nodes[sg.NodeB].Position.Plan).DistanceTo(c.Position.Plan)
                        + Math.Abs(nodes[sg.NodeA].Position.Z - c.Position.Z) * (sg.Kind == SegmentKind.Riser ? 0 : 1)))
                    .OrderBy(x => x.d)
                    .FirstOrDefault();
                if (seg.sg != null && seg.d < Math.Max(20, c.Length / 2 + 300)) c.SegmentId = seg.sg.Id;
            }
        }

        /// <summary>Tên tệp kiểm kê và IR đặt cạnh tờ isometric: <c>&lt;tên&gt;.kiem-ke.md</c> và <c>&lt;tên&gt;.ir.json</c>.</summary>
        public static string ReportPathFor(string isoPath) => System.IO.Path.ChangeExtension(isoPath, null) + ".kiem-ke.md";

        public static string IrPathFor(string isoPath) => System.IO.Path.ChangeExtension(isoPath, null) + ".ir.json";
    }
}
