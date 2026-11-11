using System;

namespace PipingAgent.Core.Source
{
    /// <summary>Đơn vị bản vẽ khai báo trong tệp. Danh sách theo biến INSUNITS của AutoCAD.</summary>
    public enum DeclaredUnit
    {
        Unitless = 0,
        Inches = 1,
        Feet = 2,
        Miles = 3,
        Millimeters = 4,
        Centimeters = 5,
        Meters = 6,
        Kilometers = 7,
        Microinches = 8,
        Mils = 9,
        Yards = 10,
        Angstroms = 11,
        Nanometers = 12,
        Microns = 13,
        Decimeters = 14,
        Decameters = 15,
        Hectometers = 16,
        Gigameters = 17,
        AstronomicalUnits = 18,
        LightYears = 19,
        Parsecs = 20,
        UsSurveyFeet = 21,
        UsSurveyInch = 22,
        UsSurveyYard = 23,
        UsSurveyMile = 24,
    }

    /// <summary>Kết quả nhận biết đơn vị. FR-ING-04 và luật R-GEN-02.</summary>
    public sealed class UnitResolution
    {
        public UnitResolution(DeclaredUnit declared, double scaleToMm, bool needsConfirmation, string note)
        {
            Declared = declared;
            ScaleToMm = scaleToMm;
            NeedsConfirmation = needsConfirmation;
            Note = note;
        }

        public DeclaredUnit Declared { get; }

        /// <summary>Hệ số nhân vào toạ độ để ra mm. Bằng 1 khi chưa xác định.</summary>
        public double ScaleToMm { get; }

        /// <summary>Đúng khi bản vẽ không khai báo hoặc khai báo mơ hồ. Ứng dụng chủ phải hỏi người dùng trước khi dùng.</summary>
        public bool NeedsConfirmation { get; }

        public string Note { get; }

        public string DeclaredName => DrawingUnits.Name(Declared);
    }

    public static class DrawingUnits
    {
        /// <summary>
        /// Quy đổi từ đơn vị khai báo. Không khai báo thì trả về hệ số 1 kèm cờ cần xác nhận,
        /// vì bản vẽ đường ống không đơn vị thường là mm nhưng không chắc.
        /// </summary>
        public static UnitResolution Resolve(DeclaredUnit declared)
        {
            switch (declared)
            {
                case DeclaredUnit.Millimeters: return Ok(declared, 1.0);
                case DeclaredUnit.Centimeters: return Ok(declared, 10.0);
                case DeclaredUnit.Decimeters: return Ok(declared, 100.0);
                case DeclaredUnit.Meters: return Ok(declared, 1000.0);
                case DeclaredUnit.Decameters: return Ok(declared, 10_000.0);
                case DeclaredUnit.Hectometers: return Ok(declared, 100_000.0);
                case DeclaredUnit.Kilometers: return Ok(declared, 1_000_000.0);
                case DeclaredUnit.Microns: return Ok(declared, 0.001);
                case DeclaredUnit.Nanometers: return Ok(declared, 1e-6);
                case DeclaredUnit.Angstroms: return Ok(declared, 1e-7);
                case DeclaredUnit.Inches: return Ok(declared, 25.4);
                case DeclaredUnit.Feet: return Ok(declared, 304.8);
                case DeclaredUnit.Yards: return Ok(declared, 914.4);
                case DeclaredUnit.Miles: return Ok(declared, 1_609_344.0);
                case DeclaredUnit.Mils: return Ok(declared, 0.0254);
                case DeclaredUnit.Microinches: return Ok(declared, 2.54e-5);
                case DeclaredUnit.UsSurveyFeet: return Ok(declared, 1200.0 / 3.937);
                case DeclaredUnit.UsSurveyInch: return Ok(declared, 100.0 / 3.937);
                case DeclaredUnit.UsSurveyYard: return Ok(declared, 3600.0 / 3.937);
                case DeclaredUnit.UsSurveyMile: return Ok(declared, 6_336_000.0 / 3.937);
                case DeclaredUnit.Unitless:
                    return new UnitResolution(declared, 1.0, true,
                        "Bản vẽ không khai báo đơn vị. Giả định mm, cần người dùng xác nhận.");
                default:
                    return new UnitResolution(declared, 1.0, true,
                        $"Đơn vị {Name(declared)} không dùng cho bản vẽ kỹ thuật. Cần người dùng chọn đơn vị.");
            }
        }

        /// <summary>
        /// Gợi ý đơn vị từ kích thước bản vẽ khi không khai báo. Mặt bằng đường ống thường rộng 10 đến 500 m.
        /// Chỉ là gợi ý để hỏi người dùng, không tự quyết.
        /// </summary>
        public static DeclaredUnit GuessFromExtent(double width, double height)
        {
            var size = Math.Max(width, height);
            if (size <= 0 || double.IsNaN(size) || double.IsInfinity(size)) return DeclaredUnit.Unitless;
            if (size >= 5_000) return DeclaredUnit.Millimeters;     // 5 m trở lên tính bằng mm
            if (size >= 500) return DeclaredUnit.Centimeters;
            if (size >= 5) return DeclaredUnit.Meters;              // 5 đến 500 m tính bằng m
            return DeclaredUnit.Unitless;
        }

        public static string Name(DeclaredUnit unit)
        {
            switch (unit)
            {
                case DeclaredUnit.Unitless: return "không khai báo";
                case DeclaredUnit.Millimeters: return "mm";
                case DeclaredUnit.Centimeters: return "cm";
                case DeclaredUnit.Decimeters: return "dm";
                case DeclaredUnit.Meters: return "m";
                case DeclaredUnit.Kilometers: return "km";
                case DeclaredUnit.Inches: return "inch";
                case DeclaredUnit.Feet: return "ft";
                case DeclaredUnit.Yards: return "yd";
                case DeclaredUnit.Miles: return "mile";
                default: return unit.ToString();
            }
        }

        private static UnitResolution Ok(DeclaredUnit declared, double scale) =>
            new UnitResolution(declared, scale, false, $"Bản vẽ khai báo {Name(declared)}.");
    }
}
