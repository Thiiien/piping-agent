using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PipingAgent.AutoCAD.Ribbon
{
    /// <summary>
    /// Biểu tượng ribbon vẽ bằng hình học WPF theo phong cách biểu tượng mặc định của AutoCAD: nét xám phẳng, một màu nhấn
    /// xanh Autodesk, mặt nền trắng, đổi sang nét sáng trên nền tối khi COLORTHEME = 0. Lưới thiết kế 32 đơn vị, nét 2, bản
    /// 16 px là bản thu nửa. Không cần tệp ảnh đi kèm plug-in.
    /// </summary>
    internal static class RibbonIcons
    {
        internal sealed class Palette
        {
            public Color Stroke;
            public Color Accent;
            public Color Warn;
            public Color Fill;

            public static Palette Light => new Palette
            {
                Stroke = Color.FromRgb(0x4A, 0x4A, 0x4A),
                Accent = Color.FromRgb(0x06, 0x96, 0xD7),
                Warn = Color.FromRgb(0xE8, 0x7D, 0x1E),
                Fill = Color.FromRgb(0xFF, 0xFF, 0xFF),
            };

            public static Palette Dark => new Palette
            {
                Stroke = Color.FromRgb(0xD9, 0xD9, 0xD9),
                Accent = Color.FromRgb(0x40, 0xB4, 0xFF),
                Warn = Color.FromRgb(0xF2, 0x9A, 0x3C),
                Fill = Color.FromRgb(0x3A, 0x3A, 0x3A),
            };
        }

        private enum Paint { Stroke, Accent, Warn, Shape, AccentFill, Dashed }

        private static readonly Dictionary<string, (string Data, Paint Paint)[]> Icons = new Dictionary<string, (string, Paint)[]>
        {
            // Tờ giấy có nếp gấp và đường ống isometric.
            ["iso"] = new[]
            {
                ("M7,3 H20 L25,8 V29 H7 Z", Paint.Shape),
                ("M20,3 V8 H25", Paint.Stroke),
                ("M10,24 L14,17 L20,20 L23,13", Paint.Accent),
            },
            // Khối lập phương isometric, mặt trên tô màu nhấn.
            ["build"] = new[]
            {
                ("M16,3 L28,10 V22 L16,29 L4,22 V10 Z", Paint.Shape),
                ("M16,3 L28,10 L16,17 L4,10 Z", Paint.AccentFill),
                ("M4,10 L16,17 L28,10 M16,17 V29", Paint.Stroke),
            },
            // Hai đầu ống và vòng cổng ở giữa.
            ["port"] = new[]
            {
                ("M2,11 H11 V21 H2 Z", Paint.Shape),
                ("M21,11 H30 V21 H21 Z", Paint.Shape),
                ("E16,16,5.5,5.5", Paint.Accent),
            },
            // Tệp JSON: tờ giấy và cặp ngoặc nhọn.
            ["ir"] = new[]
            {
                ("M7,3 H20 L25,8 V29 H7 Z", Paint.Shape),
                ("M20,3 V8 H25", Paint.Stroke),
                ("M13,13 C11,13 12,16 10,16 C12,16 11,19 13,19", Paint.Accent),
                ("M19,13 C21,13 20,16 22,16 C20,16 21,19 19,19", Paint.Accent),
            },
            // Tuyến ống trên mặt bằng và kính lúp.
            ["convert"] = new[]
            {
                ("M3,8 H17 V16 H11 M3,8 V27", Paint.Stroke),
                ("E20,20,6,6", Paint.Accent),
                ("M24.5,24.5 L30,30", Paint.Accent),
            },
            // Cửa sổ chọn nét đứt và kính lúp.
            ["scanwindow"] = new[]
            {
                ("M4,5 H28 V25 H4 Z", Paint.Dashed),
                ("E19,19,5,5", Paint.Accent),
                ("M22.5,22.5 L28,28", Paint.Accent),
            },
            // Dấu kiểm trong vòng.
            ["check"] = new[]
            {
                ("E16,16,12.5,12.5", Paint.Shape),
                ("M9,16 L14,21 L23,11", Paint.Accent),
            },
            // Bút chì.
            ["fix"] = new[]
            {
                ("M5,25 L19,11 L23,15 L9,29 Z", Paint.Shape),
                ("M5,25 L9,29 L3,31 Z", Paint.AccentFill),
                ("M17,13 L21,17", Paint.Stroke),
                ("M19,11 L22,8 L26,12 L23,15", Paint.Stroke),
            },
            // Vòng có gạch chéo: bỏ qua.
            ["waive"] = new[]
            {
                ("E16,16,12,12", Paint.Shape),
                ("M8,24 L24,8", Paint.Warn),
            },
            // Mũi tên vòng: chạy lại.
            ["rerun"] = new[]
            {
                ("M24,8 A11,11 0 1 0 27,16", Paint.Stroke),
                ("M27,11 L23,17 L31,17 Z", Paint.AccentFill),
            },
            // Cửa sổ có cột bên và các dòng.
            ["panel"] = new[]
            {
                ("M3,5 H29 V27 H3 Z", Paint.Shape),
                ("M3,10 H29 M11,10 V27", Paint.Stroke),
                ("M14,15 H25 M14,19 H25 M14,23 H21", Paint.Accent),
            },
            // Bong bóng thoại và dấu hỏi.
            ["ask"] = new[]
            {
                ("M4,5 H28 V21 H14 L8,27 V21 H4 Z", Paint.Shape),
                ("M13,11 C13,8 19,8 19,11 C19,14 16,13 16,16", Paint.Accent),
                ("M15,18 H17 V20 H15 Z", Paint.AccentFill),
            },
            // Bánh răng.
            ["config"] = new[]
            {
                ("M16,3 V7 M16,25 V29 M3,16 H7 M25,16 H29 M6.8,6.8 L9.6,9.6 M22.4,22.4 L25.2,25.2 M6.8,25.2 L9.6,22.4 M22.4,9.6 L25.2,6.8", Paint.Stroke),
                ("E16,16,9,9", Paint.Shape),
                ("E16,16,3,3", Paint.Accent),
            },
            // Phích cắm: kiểm tra plug-in.
            ["hello"] = new[]
            {
                ("M13,12 V5 M19,12 V5", Paint.Stroke),
                ("M10,12 H22 V20 C22,24 19,26 16,26 C13,26 10,24 10,20 Z", Paint.Shape),
                ("M16,26 V31", Paint.Accent),
            },
            // Hộp và mũi tên đi ra.
            ["export"] = new[]
            {
                ("M4,13 V28 H28 V13", Paint.Stroke),
                ("M16,22 V4 M10,10 L16,4 L22,10", Paint.Accent),
            },
        };

        public static bool Has(string key) => Icons.ContainsKey(key);

        /// <summary>Vẽ biểu tượng <paramref name="key"/> ở kích thước 16 hoặc 32 px.</summary>
        public static ImageSource Render(string key, Palette palette, int size)
        {
            if (!Icons.TryGetValue(key, out var shapes)) shapes = Icons["hello"];
            var scale = size / 32.0;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(scale, scale));
                foreach (var (data, paint) in shapes)
                {
                    var geometry = Parse(data);
                    dc.DrawGeometry(FillFor(paint, palette), PenFor(paint, palette), geometry);
                }

                dc.Pop();
            }

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }

        private static Geometry Parse(string data)
        {
            if (data.StartsWith("E", StringComparison.Ordinal))
            {
                var p = data.Substring(1).Split(',');
                var g = new EllipseGeometry(new Point(D(p[0]), D(p[1])), D(p[2]), D(p[3]));
                g.Freeze();
                return g;
            }

            var geometry = Geometry.Parse(data);
            geometry.Freeze();
            return geometry;
        }

        private static double D(string s) => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        private static Brush? FillFor(Paint paint, Palette p)
        {
            switch (paint)
            {
                case Paint.Shape: return Frozen(p.Fill);
                case Paint.AccentFill: return Frozen(p.Accent);
                default: return null;
            }
        }

        private static Pen? PenFor(Paint paint, Palette p)
        {
            Pen pen;
            switch (paint)
            {
                case Paint.AccentFill: return null;
                case Paint.Accent: pen = new Pen(Frozen(p.Accent), 2); break;
                case Paint.Warn: pen = new Pen(Frozen(p.Warn), 2.5); break;
                case Paint.Dashed: pen = new Pen(Frozen(p.Stroke), 2) { DashStyle = new DashStyle(new double[] { 2, 2 }, 0) }; break;
                default: pen = new Pen(Frozen(p.Stroke), 2); break;
            }

            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            pen.LineJoin = PenLineJoin.Round;
            pen.Freeze();
            return pen;
        }

        private static SolidColorBrush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
