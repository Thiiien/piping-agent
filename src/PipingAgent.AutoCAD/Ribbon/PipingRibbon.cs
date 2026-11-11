using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace PipingAgent.AutoCAD.Ribbon
{
    /// <summary>
    /// Thẻ ribbon "Piping Agent" theo quy ước của ribbon AutoCAD: nút lớn 32 px chữ dưới cho lệnh chính, nút nhỏ 16 px chữ bên
    /// xếp cột ba hàng cho lệnh phụ, nút tách cho lệnh có chế độ, chú thích có tiêu đề và phần mở rộng, phím tắt KeyTip, biểu
    /// tượng đổi theo COLORTHEME. Nút chỉ gửi tên lệnh vào dòng lệnh nên mọi lệnh vẫn chạy được bằng tay. Ribbon chưa sẵn
    /// sàng lúc nạp plug-in thì chờ <see cref="ComponentManager.ItemInitialized"/>; đổi workspace làm mất thẻ thì tạo lại.
    /// Chỉ gọi khi có giao diện, accoreconsole không có AdWindows.
    /// </summary>
    public static class PipingRibbon
    {
        public const string TabId = "PIPINGAGENT_TAB";

        private sealed class Spec
        {
            public string Command = string.Empty;
            public string Icon = string.Empty;
            public string Title = string.Empty;
            public string Tip = string.Empty;
            public string More = string.Empty;
            public string KeyTip = string.Empty;
            /// <summary>Từ khoá trả lời cho câu hỏi đầu tiên của lệnh, ví dụ chế độ của PA_BUILD.</summary>
            public string? Mode;
        }

        private sealed class Action
        {
            public string Command = string.Empty;
            public string? Mode;
        }

        private static readonly List<(RibbonItem Item, string Icon)> Themed = new List<(RibbonItem, string)>();
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
                AcApp.SystemVariableChanged += (s, e) =>
                {
                    // Đổi workspace dựng lại ribbon và làm mất thẻ; đổi theme thì chỉ vẽ lại biểu tượng.
                    if (string.Equals(e.Name, "WSCURRENT", StringComparison.OrdinalIgnoreCase)) Create();
                    else if (string.Equals(e.Name, "COLORTHEME", StringComparison.OrdinalIgnoreCase)) Retheme();
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

            Themed.Clear();
            var tab = new RibbonTab { Title = "Piping Agent", Id = TabId, KeyTip = "PA" };

            // Isometric sang 3D: luồng chính của sản phẩm.
            var iso = Panel("Isometric sang 3D");
            iso.Items.Add(Large(new Spec
            {
                Command = "PA_ISO", Icon = "iso", Title = "Đọc tờ\nISO", KeyTip = "DI",
                Tip = "Đọc tờ isometric DWG, DXF hoặc PDF, kiểm kê và nạp IR.",
                More = "Enter để lấy tờ đang mở. Bản kiểm kê ghi cạnh tờ iso dưới tên <tờ>.kiem-ke.md. Chỉ dựng 3D khi kiểm kê không còn thiếu.",
            }));
            iso.Items.Add(Split(new Spec
            {
                Command = "PA_BUILD", Icon = "build", Title = "Dựng\n3D", KeyTip = "D3", Mode = "Moi",
                Tip = "Dựng ống, co, tê, mặt bích, van thành khối 3D từ kết quả gần nhất.",
                More = "Bấm nút để dựng vào bản vẽ mới <tên>-3D.dwg. Mũi tên để chọn dựng vào bản đang mở, chỉ xem trước số lượng, hoặc thử chế độ lỗi.",
            }, new[]
            {
                new Spec { Command = "PA_BUILD", Mode = "Moi", Icon = "build", Title = "Bản vẽ mới", Tip = "Dựng vào tệp <tên>-3D.dwg rồi mở." },
                new Spec { Command = "PA_BUILD", Mode = "Hientai", Icon = "build", Title = "Bản đang mở", Tip = "Dựng vào bản vẽ hiện tại trên layer PA-<tuyến>-<loại>, một lần Undo bỏ hết." },
                new Spec { Command = "PA_BUILD", Mode = "Xemtruoc", Icon = "check", Title = "Xem trước", Tip = "Chỉ đếm số khối sẽ dựng, không vẽ." },
            }));
            iso.Items.Add(Column(
                new Spec { Command = "PA_PORT", Icon = "port", Title = "Cổng nối", KeyTip = "CN", Tip = "Chọn một khối hoặc vòng cổng để xem nối với khối nào.", More = "Vòng xanh là cổng đã nối, vòng đỏ là cổng hở." },
                new Spec { Command = "PA_BUILDIR", Icon = "ir", Title = "Nạp IR", KeyTip = "IR", Tip = "Nạp tệp IR JSON, ví dụ bản gộp nhiều tờ từ iso-merge." },
                new Spec { Command = "PA_EXPORT", Icon = "export", Title = "Xuất JSON", KeyTip = "XJ", Tip = "Ghi đối tượng đã nhận diện ra tệp JSON." }));
            tab.Panels.Add(new RibbonPanel { Source = iso });

            // Mặt bằng 2D: chức năng phụ, giữ nguyên quy trình Nhận diện, Kiểm tra, Sửa.
            var plan = Panel("Mặt bằng");
            plan.Items.Add(Split(new Spec
            {
                Command = "PA_CONVERT", Icon = "convert", Title = "Nhận\ndiện", KeyTip = "ND",
                Tip = "Nhận diện tuyến ống trên mặt bằng 2D theo hồ sơ quy ước đang chọn.",
                More = "Bấm nút để quét cả bản vẽ. Mũi tên để quét trong một cửa sổ chọn.",
            }, new[]
            {
                new Spec { Command = "PA_CONVERT", Icon = "convert", Title = "Cả bản vẽ", Tip = "Quét toàn bộ bản vẽ hoặc tập đối tượng đã chọn." },
                new Spec { Command = "PA_SCANWINDOW", Icon = "scanwindow", Title = "Trong cửa sổ", Tip = "Chọn hai góc, chỉ quét trong cửa sổ đó." },
            }));
            plan.Items.Add(Large(new Spec
            {
                Command = "PA_CHECK", Icon = "check", Title = "Kiểm\ntra", KeyTip = "KT",
                Tip = "Chạy lại bộ luật và liệt kê vấn đề theo luật.",
                More = "Vấn đề mức chặn khoá lệnh Dựng 3D cho tới khi được sửa hoặc bỏ qua có lý do.",
            }));
            plan.Items.Add(Column(
                new Spec { Command = "PA_FIX", Icon = "fix", Title = "Sửa", KeyTip = "SU", Tip = "Sửa kết quả nhận diện: nhãn, DN, cao độ, nút, cấu kiện, nối, tách." },
                new Spec { Command = "PA_WAIVE", Icon = "waive", Title = "Bỏ qua", KeyTip = "BQ", Tip = "Bỏ qua một vấn đề kèm lý do, lưu trong tệp phiên." },
                new Spec { Command = "PA_RERUN", Icon = "rerun", Title = "Chạy lại", KeyTip = "CL", Tip = "Chạy lại nhận diện với các quyết định đã lưu." }));
            tab.Panels.Add(new RibbonPanel { Source = plan });

            // Trợ lý và tiện ích.
            var tools = Panel("Trợ lý");
            tools.Items.Add(Large(new Spec
            {
                Command = "PA_PANEL", Icon = "panel", Title = "Bảng\nđiều khiển", KeyTip = "BD",
                Tip = "Mở bảng điều khiển: Tổng quan, Vấn đề, Tuyến ống, Hồ sơ, Nhật ký.",
                More = "Bấm một dòng ở thẻ Tuyến ống hoặc Vấn đề thì bản vẽ phóng tới và tô sáng.",
            }));
            tools.Items.Add(Column(
                new Spec { Command = "PA_ASK", Icon = "ask", Title = "Hỏi agent", KeyTip = "HA", Tip = "Hỏi agent về bản vẽ đang mở." },
                new Spec { Command = "PA_AGENTCONFIG", Icon = "config", Title = "Cấu hình agent", KeyTip = "CH", Tip = "Chọn điểm cuối và mô hình cho agent." },
                new Spec { Command = "PA_HELLO", Icon = "hello", Title = "Kiểm tra plug-in", KeyTip = "KP", Tip = "In phiên bản và đường dẫn nhật ký." }));
            tab.Panels.Add(new RibbonPanel { Source = tools });

            ribbon.Tabs.Add(tab);
        }

        private static RibbonPanelSource Panel(string title) => new RibbonPanelSource { Title = title };

        private static RibbonButton Large(Spec s)
        {
            var b = new RibbonButton();
            Fill(b, s);
            b.Size = RibbonItemSize.Large;
            b.Orientation = Orientation.Vertical;
            return b;
        }

        private static RibbonButton Small(Spec s)
        {
            var b = new RibbonButton();
            Fill(b, s);
            b.Size = RibbonItemSize.Standard;
            b.Orientation = Orientation.Horizontal;
            return b;
        }

        /// <summary>Nút tách: phần trên chạy chế độ mặc định, mũi tên mở danh sách các chế độ.</summary>
        private static RibbonSplitButton Split(Spec main, Spec[] options)
        {
            var b = new RibbonSplitButton
            {
                IsSplit = true,
                IsSynchronizedWithCurrentItem = false,
                ListStyle = RibbonSplitButtonListStyle.List,
                ListImageSize = RibbonImageSize.Standard,
                Size = RibbonItemSize.Large,
                Orientation = Orientation.Vertical,
            };
            Fill(b, main);
            foreach (var o in options)
            {
                var item = new RibbonButton { ShowText = true, ShowImage = true, Size = RibbonItemSize.Standard, Orientation = Orientation.Horizontal };
                Fill(item, o);
                b.Items.Add(item);
            }

            return b;
        }

        /// <summary>Cột nút nhỏ tối đa ba hàng, như các panel chuẩn của AutoCAD.</summary>
        private static RibbonRowPanel Column(params Spec[] specs)
        {
            var row = new RibbonRowPanel();
            for (var i = 0; i < specs.Length; i++)
            {
                if (i > 0) row.Items.Add(new RibbonRowBreak());
                row.Items.Add(Small(specs[i]));
            }

            return row;
        }

        private static void Fill(RibbonButton b, Spec s)
        {
            var theme = CurrentPalette();
            b.Id = "PA_RIBBON_" + s.Command + (s.Mode ?? string.Empty);
            b.Name = s.Command;
            b.Text = s.Title;
            b.ShowText = true;
            b.ShowImage = true;
            b.KeyTip = s.KeyTip;
            b.LargeImage = RibbonIcons.Render(s.Icon, theme, 32);
            b.Image = RibbonIcons.Render(s.Icon, theme, 16);
            b.ToolTip = new RibbonToolTip
            {
                Title = s.Title.Replace('\n', ' '),
                Content = s.Tip,
                ExpandedContent = s.More.Length > 0 ? s.More : null,
                Command = s.Mode == null ? s.Command : s.Command + " " + s.Mode,
                IsHelpEnabled = false,
            };
            b.CommandParameter = new Action { Command = s.Command, Mode = s.Mode };
            b.CommandHandler = new SendCommand();
            Themed.Add((b, s.Icon));
        }

        private static RibbonIcons.Palette CurrentPalette()
        {
            try
            {
                var v = AcApp.GetSystemVariable("COLORTHEME");
                if (v != null && Convert.ToInt32(v) == 0) return RibbonIcons.Palette.Dark;
            }
            catch
            {
                // Không đọc được thì coi là nền sáng.
            }

            return RibbonIcons.Palette.Light;
        }

        private static void Retheme()
        {
            var theme = CurrentPalette();
            foreach (var (item, icon) in Themed)
            {
                if (item is RibbonButton b)
                {
                    b.LargeImage = RibbonIcons.Render(icon, theme, 32);
                    b.Image = RibbonIcons.Render(icon, theme, 16);
                }
            }
        }

        /// <summary>Gửi lệnh vào dòng lệnh của bản vẽ đang mở, như người dùng gõ. Chế độ, nếu có, được đặt sẵn cho lệnh.</summary>
        private sealed class SendCommand : ICommand
        {
            public event EventHandler? CanExecuteChanged { add { } remove { } }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter)
            {
                var action = parameter as Action ?? (parameter as RibbonButton)?.CommandParameter as Action;
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null || action == null) return;
                Commands.CommandPresets.Set(action.Command, action.Mode);
                doc.SendStringToExecute("_." + action.Command + " ", true, false, true);
            }
        }
    }
}
