using System;
using System.Collections.Generic;

namespace PipingAgent.Core.Catalog
{
    /// <summary>Kích thước mặt bích theo DN, mm. Mặt bích SO: đĩa dày <see cref="T"/> cộng cổ dài tới <see cref="HubLength"/> tính từ mặt bích.</summary>
    public sealed class FlangeDims
    {
        public int Dn { get; set; }

        /// <summary>Đường kính ngoài đĩa.</summary>
        public double D { get; set; }

        /// <summary>Đường kính vòng bu lông.</summary>
        public double C { get; set; }

        /// <summary>Số bu lông.</summary>
        public int Bolts { get; set; }

        /// <summary>Đường kính lỗ bu lông.</summary>
        public double Hole { get; set; }

        /// <summary>Chiều dày đĩa.</summary>
        public double T { get; set; }

        /// <summary>Đường kính ngoài cổ mặt bích SO.</summary>
        public double HubOd { get; set; }

        /// <summary>Chiều dài toàn bộ mặt bích SO kể cả cổ, từ mặt bích tới đuôi cổ.</summary>
        public double HubLength { get; set; }

        /// <summary>Đường kính mặt nâng RF.</summary>
        public double RaisedFace { get; set; }
    }

    /// <summary>
    /// Kích thước cấu kiện để dựng hình và tính chiều dài khi bản vẽ không ghi, I2.
    /// Bảng JIS 10K lấy theo trí nhớ về JIS B2220, B2032, B2002 và ASME B16.9, B16.11, CHƯA được chuyên gia đối chiếu,
    /// nên mọi giá trị trả về kèm <see cref="Verified"/> sai và kết quả dùng chúng ghi nguồn <c>catalog_unverified</c>.
    /// Đủ để hình 3D có tỉ lệ đúng; không dùng cho bản vẽ chế tạo.
    /// </summary>
    public static class ComponentDims
    {
        public const bool Verified = false;

        public const string Source = "catalog_unverified";

        /// <summary>Chiều dày gioăng mặc định, mm.</summary>
        public const double Gasket = 4.5;

        // DN: D, C, n, hole, t theo JIS B2220 10K.
        private static readonly Dictionary<int, (double D, double C, int N, double H, double T)> Jis10K = new Dictionary<int, (double, double, int, double, double)>
        {
            [15] = (95, 70, 4, 15, 12),
            [20] = (100, 75, 4, 15, 14),
            [25] = (125, 90, 4, 19, 14),
            [32] = (135, 100, 4, 19, 16),
            [40] = (140, 105, 4, 19, 16),
            [50] = (155, 120, 4, 19, 16),
            [65] = (175, 140, 4, 19, 18),
            [80] = (185, 150, 8, 19, 18),
            [100] = (210, 175, 8, 19, 18),
            [125] = (250, 210, 8, 23, 20),
            [150] = (280, 240, 8, 23, 22),
            [200] = (330, 290, 12, 23, 22),
            [250] = (400, 355, 12, 25, 24),
            [300] = (445, 400, 16, 25, 24),
            [350] = (490, 445, 16, 25, 26),
            [400] = (560, 510, 16, 27, 28),
            [450] = (620, 565, 20, 27, 30),
            [500] = (675, 620, 20, 27, 30),
            [600] = (795, 730, 24, 33, 32),
        };

        // Mặt đối mặt van bướm wafer JIS 10K, JIS B2032.
        private static readonly Dictionary<int, double> ButterflyWafer = new Dictionary<int, double>
        {
            [40] = 33, [50] = 43, [65] = 46, [80] = 46, [100] = 52, [125] = 56, [150] = 56, [200] = 60,
            [250] = 68, [300] = 78, [350] = 78, [400] = 102, [450] = 114, [500] = 127, [600] = 154,
        };

        // Mặt đối mặt van cầu và van một chiều mặt bích JIS 10K, JIS B2002.
        private static readonly Dictionary<int, double> GlobeFlanged = new Dictionary<int, double>
        {
            [15] = 108, [20] = 117, [25] = 127, [32] = 140, [40] = 165, [50] = 203, [65] = 216, [80] = 241,
            [100] = 292, [125] = 330, [150] = 356, [200] = 495, [250] = 622, [300] = 698,
        };

        // Mặt đối mặt van cổng và van bi mặt bích JIS 10K.
        private static readonly Dictionary<int, double> GateFlanged = new Dictionary<int, double>
        {
            [15] = 108, [20] = 117, [25] = 127, [32] = 140, [40] = 165, [50] = 178, [65] = 190, [80] = 203,
            [100] = 229, [125] = 254, [150] = 267, [200] = 292, [250] = 330, [300] = 356, [350] = 381, [400] = 406,
            [450] = 432, [500] = 457, [600] = 508,
        };

        // Côn thu đồng tâm, chiều dài H theo DN lớn, ASME B16.9 trùng JIS B2313.
        private static readonly Dictionary<int, double> ReducerH = new Dictionary<int, double>
        {
            [20] = 38, [25] = 51, [32] = 51, [40] = 64, [50] = 76, [65] = 89, [80] = 89, [100] = 102, [125] = 127, [150] = 140,
            [200] = 152, [250] = 178, [300] = 203, [350] = 330, [400] = 356, [450] = 381, [500] = 508, [600] = 508,
        };

        // Khớp nối nửa 3000# theo DN nhánh: đường kính ngoài và chiều dài, ASME B16.11.
        private static readonly Dictionary<int, (double Od, double L)> HalfCoupling3000 = new Dictionary<int, (double, double)>
        {
            [15] = (33, 19), [20] = (38, 22), [25] = (46, 25.5), [32] = (56, 28.5), [40] = (62, 32), [50] = (76, 35),
        };

        public static FlangeDims? Flange10K(int dn)
        {
            if (!Jis10K.TryGetValue(dn, out var f)) return null;
            var od = PipeOd(dn);
            return new FlangeDims
            {
                Dn = dn,
                D = f.D,
                C = f.C,
                Bolts = f.N,
                Hole = f.H,
                T = f.T,
                HubOd = od + 2 * (8 + dn * 0.05),
                HubLength = f.T + 8 + dn * 0.1,
                RaisedFace = f.C - f.H - 6,
            };
        }

        /// <summary>Mặt đối mặt van theo loại. Van bướm là loại wafer, chưa kể gioăng hai bên.</summary>
        public static double? ValveFaceToFace(string type, int dn)
        {
            switch (type)
            {
                case "valve_butterfly": return ButterflyWafer.TryGetValue(dn, out var b) ? b : (double?)null;
                case "valve_globe": case "valve_check": return GlobeFlanged.TryGetValue(dn, out var g) ? g : (double?)null;
                case "valve_gate": case "valve_ball": case "valve": return GateFlanged.TryGetValue(dn, out var t) ? t : (double?)null;
                default: return null;
            }
        }

        public static double? ConcentricReducerLength(int dnLarge) => ReducerH.TryGetValue(dnLarge, out var h) ? h : (double?)null;

        public static (double Od, double L)? HalfCoupling(int branchDn) => HalfCoupling3000.TryGetValue(branchDn, out var c) ? c : ((double, double)?)null;

        /// <summary>Đường kính ngoài ống theo DN, bảng B36.10 rút gọn, chỉ để tính cổ mặt bích khi không có danh mục.</summary>
        public static double PipeOd(int dn)
        {
            switch (dn)
            {
                case 15: return 21.7;
                case 20: return 27.2;
                case 25: return 34.0;
                case 32: return 42.7;
                case 40: return 48.6;
                case 50: return 60.5;
                case 65: return 76.3;
                case 80: return 89.1;
                case 100: return 114.3;
                case 125: return 139.8;
                case 150: return 165.2;
                case 200: return 216.3;
                case 250: return 267.4;
                case 300: return 318.5;
                case 350: return 355.6;
                case 400: return 406.4;
                case 450: return 457.2;
                case 500: return 508.0;
                case 600: return 609.6;
                default: return dn * 1.1;
            }
        }
    }
}
