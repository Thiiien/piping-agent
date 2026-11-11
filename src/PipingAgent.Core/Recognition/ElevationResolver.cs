using System;
using PipingAgent.Core.Catalog;
using PipingAgent.Core.Ir;
using PipingAgent.Core.Profile;

namespace PipingAgent.Core.Recognition
{
    /// <summary>Kết quả quy đổi cao độ về tâm ống.</summary>
    public readonly struct ElevationResolution
    {
        public ElevationResolution(double? centerline, string? problem)
        {
            Centerline = centerline;
            Problem = problem;
        }

        public double? Centerline { get; }

        /// <summary>Lý do không quy đổi được, để ghi vào vấn đề R-ELV-05.</summary>
        public string? Problem { get; }

        public bool Ok => Centerline.HasValue;
    }

    /// <summary>
    /// Ngữ nghĩa cao độ, S3-01 và FR-REC-07: giá trị ghi trên bản vẽ theo tâm ống, đáy ống, đỉnh ống hoặc mặt trên giá đỡ
    /// được quy về cao độ tâm theo đường kính ngoài tra từ danh mục. Thiếu khai báo thì không đoán, trả về vấn đề.
    /// </summary>
    public static class ElevationResolver
    {
        public static ElevationResolution Resolve(Elevation elevation, int dn, PipeCatalog? catalog, ConventionProfile profile)
        {
            if (elevation == null) throw new ArgumentNullException(nameof(elevation));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            switch (elevation.Datum)
            {
                case ElevationDatum.CL:
                    return new ElevationResolution(elevation.Raw, null);
                case ElevationDatum.Unknown:
                    return new ElevationResolution(null, "chưa rõ mốc cao độ là tâm, đáy hay đỉnh ống");
            }

            var od = OuterDiameter(dn, catalog, profile);
            if (!od.HasValue)
            {
                return new ElevationResolution(null, dn <= 0
                    ? $"mốc {elevation.Datum} cần đường kính ngoài nhưng đoạn chạy chưa có DN"
                    : $"mốc {elevation.Datum} cần đường kính ngoài nhưng danh mục không có DN{dn}");
            }

            switch (elevation.Datum)
            {
                case ElevationDatum.BOP:
                    return new ElevationResolution(elevation.Raw + od.Value / 2, null);
                case ElevationDatum.TOP:
                    return new ElevationResolution(elevation.Raw - od.Value / 2, null);
                case ElevationDatum.TOS:
                    var support = elevation.SupportHeight ?? profile.Tolerances.DefaultSupportHeight;
                    if (!support.HasValue) return new ElevationResolution(null, "mốc TOS cần chiều cao đỡ, hồ sơ chưa khai báo defaultSupportHeight");
                    return new ElevationResolution(elevation.Raw + support.Value + od.Value / 2, null);
                default:
                    return new ElevationResolution(null, "mốc cao độ không hỗ trợ");
            }
        }

        /// <summary>Quy đổi tại chỗ: ghi <see cref="Elevation.Centerline"/> và chiều cao đỡ đã dùng.</summary>
        public static ElevationResolution Apply(Run run, PipeCatalog? catalog, ConventionProfile profile)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            var r = Resolve(run.Elevation, run.Dn, catalog, profile);
            run.Elevation.Centerline = r.Centerline;
            if (r.Ok && run.Elevation.Datum == ElevationDatum.TOS && !run.Elevation.SupportHeight.HasValue)
            {
                run.Elevation.SupportHeight = profile.Tolerances.DefaultSupportHeight;
            }

            return r;
        }

        public static double? OuterDiameter(int dn, PipeCatalog? catalog, ConventionProfile profile)
        {
            if (dn <= 0 || catalog == null || profile.PipeStandard == null) return null;
            var od = catalog.PipeOd(profile.PipeStandard, dn);
            return od.Found ? od.Value : (double?)null;
        }
    }
}
