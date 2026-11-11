using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace PipingAgent.UI
{
    /// <summary>
    /// Bảng điều khiển dùng chung cho mọi ứng dụng chủ, S2-11. Năm thẻ: Tổng quan, Vấn đề, Tuyến ống, Hồ sơ, Nhật ký.
    /// Viết bằng mã thay vì XAML để dựng được cho cả .NET Framework 4.8 lẫn .NET 8 mà không phụ thuộc bộ biên dịch XAML.
    /// Mọi dữ liệu đi qua <see cref="PanelViewModel"/>, mọi thao tác lên bản vẽ đi qua <see cref="IHostActions"/>.
    /// </summary>
    public sealed class MainPanel : UserControl
    {
        private readonly ProgressBar _progress;
        private readonly Button _cancel;

        public MainPanel() : this(new PanelViewModel(null))
        {
        }

        public MainPanel(PanelViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            DataContext = model;

            var root = new DockPanel { Margin = new Thickness(6), LastChildFill = true };

            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(header, Dock.Top);
            var scan = new Button { Content = "Quét", Padding = new Thickness(10, 2, 10, 2), MinWidth = 70 };
            scan.Click += (_, __) => Model.Host?.Rescan();
            scan.SetBinding(IsEnabledProperty, new Binding(nameof(PanelViewModel.Busy)) { Converter = new NotConverter() });
            DockPanel.SetDock(scan, Dock.Right);
            header.Children.Add(scan);
            var title = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            title.SetBinding(TextBlock.TextProperty, new Binding(nameof(PanelViewModel.DrawingName)) { StringFormat = "Piping Agent  {0}" });
            header.Children.Add(title);
            root.Children.Add(header);

            var footer = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            DockPanel.SetDock(footer, Dock.Bottom);
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
            status.SetBinding(TextBlock.TextProperty, new Binding(nameof(PanelViewModel.Status)));
            footer.Children.Add(status);
            var progressRow = new DockPanel();
            progressRow.SetBinding(VisibilityProperty, new Binding(nameof(PanelViewModel.Busy)) { Converter = new BoolToVisibilityConverter() });
            _cancel = new Button { Content = "Hủy", Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(6, 0, 0, 0) };
            _cancel.Click += (_, __) => CancelRequested?.Invoke(this, EventArgs.Empty);
            DockPanel.SetDock(_cancel, Dock.Right);
            progressRow.Children.Add(_cancel);
            _progress = new ProgressBar { Height = 14, Minimum = 0, Maximum = 1 };
            _progress.SetBinding(System.Windows.Controls.Primitives.RangeBase.ValueProperty, new Binding(nameof(PanelViewModel.Progress)));
            progressRow.Children.Add(_progress);
            footer.Children.Add(progressRow);
            var progressText = new TextBlock { FontSize = 11, Foreground = SystemColors.GrayTextBrush };
            progressText.SetBinding(TextBlock.TextProperty, new Binding(nameof(PanelViewModel.ProgressText)));
            footer.Children.Add(progressText);
            root.Children.Add(footer);

            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "Tổng quan", Content = BuildOverview() });
            tabs.Items.Add(new TabItem { Header = "Vấn đề", Content = BuildIssues() });
            tabs.Items.Add(new TabItem { Header = "Tuyến ống", Content = BuildLines() });
            tabs.Items.Add(new TabItem { Header = "Đoạn chạy", Content = BuildRuns() });
            tabs.Items.Add(new TabItem { Header = "Hồ sơ", Content = BuildProfile() });
            tabs.Items.Add(new TabItem { Header = "Nhật ký", Content = BuildLog() });
            root.Children.Add(tabs);

            Content = root;
        }

        public PanelViewModel Model { get; }

        /// <summary>Người dùng bấm Hủy khi đang quét.</summary>
        public event EventHandler? CancelRequested;

        /// <summary>Giữ tương thích với Sprint 1.</summary>
        public void SetStatus(string text) => Model.Status = text;

        private UIElement BuildOverview()
        {
            var panel = new DockPanel();
            var summary = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6) };
            summary.SetBinding(TextBlock.TextProperty, new Binding(nameof(PanelViewModel.OverviewSummary)));
            DockPanel.SetDock(summary, Dock.Top);
            panel.Children.Add(summary);
            var preview = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6), FontStyle = FontStyles.Italic };
            preview.SetBinding(TextBlock.TextProperty, new Binding(nameof(PanelViewModel.BuildPreview)) { StringFormat = "Sẽ dựng: {0}" });
            DockPanel.SetDock(preview, Dock.Top);
            panel.Children.Add(preview);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var roles = List(nameof(PanelViewModel.Roles),
                ("Vai trò", nameof(RoleRow.Label), 150.0), ("Số", nameof(RoleRow.Count), 60.0), ("Tỷ lệ", nameof(RoleRow.Share), 60.0));
            Grid.SetRow(roles, 0);
            grid.Children.Add(roles);

            var label = new TextBlock { Text = "Chưa phân loại theo layer", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) };
            Grid.SetRow(label, 1);
            grid.Children.Add(label);

            var layers = List(nameof(PanelViewModel.UnclassifiedLayers),
                ("Layer", nameof(LayerRowItem.Layer), 120.0), ("Chưa", nameof(LayerRowItem.Unclassified), 50.0), ("Tổng", nameof(LayerRowItem.Total), 50.0),
                ("Tỷ lệ", nameof(LayerRowItem.Share), 50.0), ("Loại", nameof(LayerRowItem.Types), 200.0));
            Grid.SetRow(layers, 2);
            grid.Children.Add(layers);
            panel.Children.Add(grid);
            return panel;
        }

        private UIElement BuildIssues()
        {
            var panel = new DockPanel();
            var filters = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            DockPanel.SetDock(filters, Dock.Top);
            var severity = new ComboBox { Width = 90, Margin = new Thickness(0, 0, 6, 0) };
            severity.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(PanelViewModel.SeverityChoices)));
            severity.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(PanelViewModel.IssueSeverityFilter)) { Mode = BindingMode.TwoWay });
            filters.Children.Add(severity);
            var rule = new TextBox { Width = 80, Margin = new Thickness(0, 0, 6, 0), ToolTip = "Lọc theo mã luật" };
            rule.SetBinding(TextBox.TextProperty, new Binding(nameof(PanelViewModel.IssueRuleFilter)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            filters.Children.Add(rule);
            var line = new TextBox { Width = 90, Margin = new Thickness(0, 0, 6, 0), ToolTip = "Lọc theo tuyến" };
            line.SetBinding(TextBox.TextProperty, new Binding(nameof(PanelViewModel.IssueLineFilter)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            filters.Children.Add(line);
            var status = new ComboBox { Width = 90, Margin = new Thickness(0, 0, 6, 0) };
            status.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(PanelViewModel.StatusChoices)));
            status.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(PanelViewModel.IssueStatusFilter)) { Mode = BindingMode.TwoWay });
            filters.Children.Add(status);
            var count = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            count.SetBinding(TextBlock.TextProperty, new Binding(nameof(PanelViewModel.IssueTotal)) { StringFormat = "tổng {0}" });
            filters.Children.Add(count);
            panel.Children.Add(filters);

            var actions = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            DockPanel.SetDock(actions, Dock.Bottom);
            var fix = new Button { Content = "Sửa (PA_FIX)", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 0) };
            fix.Click += (_, __) => Model.Host?.RunCommand("PA_FIX");
            DockPanel.SetDock(fix, Dock.Left);
            actions.Children.Add(fix);
            var waive = new Button { Content = "Bỏ qua", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
            waive.Click += (_, __) => Model.Status = Model.WaiveSelected();
            DockPanel.SetDock(waive, Dock.Right);
            actions.Children.Add(waive);
            var reason = new TextBox { ToolTip = "Lý do bỏ qua vấn đề đang chọn, bắt buộc" };
            reason.SetBinding(TextBox.TextProperty, new Binding(nameof(PanelViewModel.WaiveReason)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            actions.Children.Add(reason);
            panel.Children.Add(actions);

            var list = List(nameof(PanelViewModel.Issues),
                ("Mức", nameof(IssueRow.Severity), 70.0), ("Luật", nameof(IssueRow.Rule), 70.0), ("Tuyến", nameof(IssueRow.Line), 80.0),
                ("Trạng thái", nameof(IssueRow.Status), 64.0), ("Nội dung", nameof(IssueRow.Message), 320.0), ("Gợi ý", nameof(IssueRow.Suggestions), 160.0));
            list.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(PanelViewModel.SelectedIssue)) { Mode = BindingMode.TwoWay });
            panel.Children.Add(list);
            return panel;
        }

        private UIElement BuildLines()
        {
            var list = List(nameof(PanelViewModel.Lines),
                ("Tuyến", nameof(LineRow.Tag), 120.0), ("DN", nameof(LineRow.Dn), 90.0), ("Dài", nameof(LineRow.Length), 70.0),
                ("Đoạn chạy", nameof(LineRow.Runs), 64.0), ("Vấn đề", nameof(LineRow.Issues), 56.0));
            list.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(PanelViewModel.SelectedLine)) { Mode = BindingMode.TwoWay });
            return list;
        }

        private UIElement BuildRuns()
        {
            var list = List(nameof(PanelViewModel.Runs),
                ("Đoạn", nameof(RunRow.Id), 64.0), ("Tuyến", nameof(RunRow.Line), 110.0), ("DN", nameof(RunRow.Dn), 50.0),
                ("Cao độ", nameof(RunRow.Elevation), 80.0), ("Dài", nameof(RunRow.Length), 60.0), ("Gắn", nameof(RunRow.Method), 60.0),
                ("Tin cậy", nameof(RunRow.Confidence), 50.0), ("Nhãn", nameof(RunRow.Tag), 200.0));
            list.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(PanelViewModel.SelectedRun)) { Mode = BindingMode.TwoWay });
            return list;
        }

        private UIElement BuildProfile()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 4, 4, 0) };
            var row = new DockPanel();
            var pick = new Button { Content = "Chọn tệp...", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
            pick.Click += (_, __) =>
            {
                var path = Model.Host?.PickProfileFile();
                if (string.IsNullOrEmpty(path)) return;
                var choice = new ProfileChoice { Path = path!, Label = System.IO.Path.GetFileNameWithoutExtension(path) };
                Model.Profiles.Add(choice);
                Model.SelectedProfile = choice;
            };
            DockPanel.SetDock(pick, Dock.Right);
            row.Children.Add(pick);
            var combo = new ComboBox();
            combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(PanelViewModel.Profiles)));
            combo.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(PanelViewModel.SelectedProfile)) { Mode = BindingMode.TwoWay });
            row.Children.Add(combo);
            panel.Children.Add(row);

            var summary = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
            summary.SetBinding(TextBlock.TextProperty, new Binding(nameof(PanelViewModel.ProfileSummary)));
            panel.Children.Add(summary);
            var errors = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MinHeight = 40, Margin = new Thickness(0, 6, 0, 0), Foreground = System.Windows.Media.Brushes.DarkRed };
            errors.SetBinding(TextBox.TextProperty, new Binding(nameof(PanelViewModel.ProfileErrors)));
            panel.Children.Add(errors);

            // Trình soạn hồ sơ, S5-12 và S5-13.
            panel.Children.Add(new TextBlock { Text = "Soạn hồ sơ từ bản vẽ đang mở", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2) });
            var idRow = new DockPanel();
            idRow.Children.Add(new TextBlock { Text = "Mã hồ sơ ", VerticalAlignment = VerticalAlignment.Center });
            var id = new TextBox();
            id.SetBinding(TextBox.TextProperty, new Binding(nameof(PanelViewModel.EditorProfileId)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            idRow.Children.Add(id);
            panel.Children.Add(idRow);
            var roles = new ListView { MaxHeight = 220, Margin = new Thickness(0, 4, 0, 4) };
            var view = new GridView();
            view.Columns.Add(new GridViewColumn { Header = "Layer / Block", Width = 150, DisplayMemberBinding = new Binding(nameof(LayerRoleRow.Name)) });
            view.Columns.Add(new GridViewColumn { Header = "Số", Width = 50, DisplayMemberBinding = new Binding(nameof(LayerRoleRow.Count)) });
            view.Columns.Add(new GridViewColumn { Header = "Loại", Width = 120, DisplayMemberBinding = new Binding(nameof(LayerRoleRow.Types)) });
            var roleFactory = new FrameworkElementFactory(typeof(ComboBox));
            roleFactory.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(PanelViewModel.RoleChoices)) { Source = Model });
            roleFactory.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(LayerRoleRow.Role)) { Mode = BindingMode.TwoWay });
            roleFactory.SetValue(WidthProperty, 140.0);
            view.Columns.Add(new GridViewColumn { Header = "Vai trò", Width = 150, CellTemplate = new DataTemplate { VisualTree = roleFactory } });
            roles.View = view;
            roles.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(PanelViewModel.EditorRows)));
            panel.Children.Add(roles);
            var patternRow = new DockPanel();
            var test = new Button { Content = "Thử mẫu", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
            test.Click += (_, __) => Model.TestTagPattern();
            DockPanel.SetDock(test, Dock.Right);
            patternRow.Children.Add(test);
            var pattern = new TextBox { ToolTip = "Mẫu nhãn, biểu thức chính quy có nhóm line, dn, el" };
            pattern.SetBinding(TextBox.TextProperty, new Binding(nameof(PanelViewModel.TagPatternText)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            patternRow.Children.Add(pattern);
            panel.Children.Add(patternRow);
            var patternResult = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) };
            patternResult.SetBinding(TextBlock.TextProperty, new Binding(nameof(PanelViewModel.TagPatternResult)));
            panel.Children.Add(patternResult);
            var save = new Button { Content = "Xuất hồ sơ ra tệp...", Padding = new Thickness(8, 1, 8, 1), HorizontalAlignment = HorizontalAlignment.Left };
            save.Click += (_, __) => Model.Status = Model.SaveEditorProfile();
            panel.Children.Add(save);
            var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            return scroll;
        }

        private UIElement BuildLog()
        {
            var list = new ListBox { FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 11 };
            list.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(PanelViewModel.LogLines)));
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Auto);
            return list;
        }

        private static ListView List(string itemsPath, params (string Header, string Member, double Width)[] columns)
        {
            var view = new GridView();
            foreach (var (header, member, width) in columns)
            {
                view.Columns.Add(new GridViewColumn { Header = header, Width = width, DisplayMemberBinding = new Binding(member) });
            }

            var list = new ListView { View = view };
            list.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(itemsPath));
            return list;
        }

        private sealed class NotConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => !(value is bool b && b);

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => !(value is bool b && b);
        }

        private sealed class BoolToVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
                value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
                value is Visibility v && v == Visibility.Visible;
        }
    }
}
