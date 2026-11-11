using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace PipingAgent.AutoCAD.Ribbon
{
    /// <summary>
    /// Thẻ ribbon "Piping Agent": mỗi lệnh một nút, xếp theo luồng việc. Nút chỉ gửi tên lệnh vào dòng lệnh, nên mọi lệnh vẫn chạy
    /// được bằng tay như cũ. Ribbon chưa sẵn sàng lúc nạp plug-in thì chờ <see cref="ComponentManager.ItemInitialized"/>; đổi
    /// workspace làm mất thẻ thì tạo lại. Chỉ gọi khi có giao diện, accoreconsole không có AdWindows.
    /// </summary>
    public static class PipingRibbon
    {
        public const string TabId = "PIPINGAGENT_TAB";

        private static readonly (string Panel, (string Command, string Title, string Tip, string Glyph, Color Color)[] Buttons)[] Layout =
        {
            ("Isometric sang 3D", new[]
            {
                ("PA_ISO", "Đọc tờ\nISO", "Đọc tờ isometric DWG, DXF hoặc PDF, kiểm kê và nạp IR. Enter lấy tờ đang mở.", "ISO", Color.FromRgb(0x1E, 0x6F, 0xB5)),
                ("PA_BUILD", "Dựng\n3D", "Dựng ống, co, tê, mặt bích, van thành khối 3D. Chọn Moi hoặc Hientai.", "3D", Color.FromRgb(0x2E, 0x8B, 0x57)),
                ("PA_PORT", "Cổng\nnối", "Chọn một khối để xem cổng và khối đối diện.", "⟷", Color.FromRgb(0xC9, 0x7A, 0x1E)),
                ("PA_BUILDIR", "Nạp\nIR", "Nạp tệp IR JSON, ví dụ bản gộp nhiều tờ từ iso-merge.", "IR", Color.FromRgb(0x5B, 0x5B, 0x9E)),
            }),
            ("Mặt bằng", new[]
            {
                ("PA_CONVERT", "Nhận\ndiện", "Nhận diện tuyến ống trên mặt bằng 2D theo hồ sơ quy ước.", "2D", Color.FromRgb(0x1E, 0x6F, 0xB5)),
                ("PA_CHECK", "Kiểm\ntra", "Chạy luật kiểm tra, liệt kê vấn đề.", "✓", Color.FromRgb(0x2E, 0x8B, 0x57)),
                ("PA_FIX", "Sửa", "Sửa chỗ nhận diện sai.", "✎", Color.FromRgb(0xC9, 0x7A, 0x1E)),
                ("PA_WAIVE", "Bỏ\nqua", "Bỏ qua một vấn đề kèm lý do.", "–", Color.FromRgb(0x80, 0x80, 0x80)),
            }),
            ("Trợ lý", new[]
            {
                ("PA_PANEL", "Bảng\nđiều khiển", "Mở bảng điều khiển: hồ sơ, đối tượng, vấn đề.", "☰", Color.FromRgb(0x5B, 0x5B, 0x9E)),
                ("PA_ASK", "Hỏi\nagent", "Hỏi agent về bản vẽ đang mở.", "?", Color.FromRgb(0x8E, 0x44, 0xAD)),
                ("PA_HELLO", "Kiểm tra\nplug-in", "In phiên bản và đường dẫn nhật ký.", "i", Color.FromRgb(0x80, 0x80, 0x80)),
            }),
        };

        private static bool _hooked;

        /// <summary>Gọi từ Initialize khi có giao diện. An toàn gọi nhiều lần.</summary>
        public static void Install()
        {
            if (ComponentManager.Ribbon != null)
            {
                Create();
            }
            else if (!_hooked)
            {
                ComponentManager.ItemInitialized += OnItemInitialized;
            }

            if (!_hooked)
            {
                // Đổi workspace dựng lại ribbon và làm mất thẻ của plug-in.
                AcApp.SystemVariableChanged += (s, e) =>
                {
                    if (string.Equals(e.Name, "WSCURRENT", StringComparison.OrdinalIgnoreCase)) Create();
                };
                _hooked = true;
            }
        }

        private static void OnItemInitialized(object? sender, RibbonItemEventArgs e)
        {
            if (ComponentManager.Ribbon == null) return;
            ComponentManager.ItemInitialized -= OnItemInitialized;
            Create();
        }

        public static void Create()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;
            if (ribbon.FindTab(TabId) != null) return;

            var tab = new RibbonTab { Title = "Piping Agent", Id = TabId };
            foreach (var (panelTitle, buttons) in Layout)
            {
                var source = new RibbonPanelSource { Title = panelTitle };
                foreach (var b in buttons)
                {
                    source.Items.Add(new RibbonButton
                    {
                        Text = b.Title,
                        ShowText = true,
                        ShowImage = true,
                        Size = RibbonItemSize.Large,
                        Orientation = System.Windows.Controls.Orientation.Vertical,
                        LargeImage = Glyph(b.Glyph, b.Color, 32),
                        Image = Glyph(b.Glyph, b.Color, 16),
                        ToolTip = new RibbonToolTip { Title = b.Command, Content = b.Tip, Command = b.Command },
                        CommandParameter = b.Command,
                        CommandHandler = new SendCommand(),
                    });
                }

                tab.Panels.Add(new RibbonPanel { Source = source });
            }

            ribbon.Tabs.Add(tab);
        }

        /// <summary>Biểu tượng vẽ bằng WPF: ô tròn màu, chữ trắng, không cần tệp ảnh đi kèm.</summary>
        private static ImageSource Glyph(string text, Color color, int size)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var r = size / 2.0;
                dc.DrawEllipse(new SolidColorBrush(color), null, new System.Windows.Point(r, r), r - 0.5, r - 0.5);
                var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    text.Length > 2 ? size * 0.36 : size * 0.5, Brushes.White, 1.0);
                dc.DrawText(ft, new System.Windows.Point(r - ft.Width / 2, r - ft.Height / 2));
            }

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }

        /// <summary>Gửi lệnh vào dòng lệnh của bản vẽ đang mở, như người dùng gõ.</summary>
        private sealed class SendCommand : ICommand
        {
            public event EventHandler? CanExecuteChanged { add { } remove { } }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter)
            {
                var name = parameter as string ?? (parameter as RibbonButton)?.CommandParameter as string;
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null || string.IsNullOrEmpty(name)) return;
                doc.SendStringToExecute("_." + name + " ", true, false, true);
            }
        }
    }
}
