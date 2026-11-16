using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PipingAgent.UI
{
    /// <summary>
    /// Giao diện hội thoại với AI (Chat View). Dùng lại được cho cả Cửa sổ Nổi độc lập (ChatWindow)
    /// lẫn Thẻ Trò chuyện trong Bảng điều khiển neo (PaletteSet MainPanel).
    /// </summary>
    public sealed class ChatView : UserControl
    {
        public ChatView(PanelViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            DataContext = model;
            Content = BuildContent();
        }

        public PanelViewModel Model { get; }

        private UIElement BuildContent()
        {
            var panel = new DockPanel { Margin = new Thickness(6) };

            // 1. Thanh trạng thái & tác vụ phía trên (TopBar)
            var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(topBar, Dock.Top);

            var clearBtn = new Button
            {
                Content = "🗑 Xoá đoạn chat",
                Padding = new Thickness(8, 2, 8, 2),
                ToolTip = "Xoá lịch sử hội thoại hiện tại",
            };
            clearBtn.Click += (_, __) => Model.ClearChat();
            DockPanel.SetDock(clearBtn, Dock.Right);
            topBar.Children.Add(clearBtn);

            var statusPill = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(239, 246, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };
            var modelBadge = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(29, 78, 216)),
            };
            modelBadge.SetBinding(TextBlock.TextProperty, new Binding(nameof(PanelViewModel.ConnectionStatusText)));
            statusPill.Child = modelBadge;
            DockPanel.SetDock(statusPill, Dock.Left);
            topBar.Children.Add(statusPill);

            panel.Children.Add(topBar);

            // 2. Khung duyệt hồ sơ quy ước đề xuất (chỉ hiện khi có hồ sơ nháp)
            var reviewBox = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
                Background = new SolidColorBrush(Color.FromRgb(240, 249, 255)),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                CornerRadius = new CornerRadius(8),
            };
            reviewBox.SetBinding(VisibilityProperty, new Binding(nameof(PanelViewModel.HasDraftProfile)) { Converter = new BoolToVisibilityConverter() });
            DockPanel.SetDock(reviewBox, Dock.Top);

            var reviewContent = new StackPanel();
            var reviewTitle = new TextBlock
            {
                Text = "✨ Đề xuất hồ sơ quy ước mới từ Agent",
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 58, 138)),
                Margin = new Thickness(0, 0, 0, 3),
            };
            reviewContent.Children.Add(reviewTitle);

            var reviewDesc = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Margin = new Thickness(0, 0, 0, 6),
            };
            reviewDesc.SetBinding(TextBlock.TextProperty, new Binding("DraftProfile.Description"));
            reviewContent.Children.Add(reviewDesc);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
            var acceptBtn = new Button
            {
                Content = "✓ Duyệt & Lưu hồ sơ...",
                Padding = new Thickness(12, 3, 12, 3),
                Margin = new Thickness(0, 0, 6, 0),
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
            };
            acceptBtn.Click += (_, __) => Model.AcceptDraftProfile();
            btnRow.Children.Add(acceptBtn);

            var rejectBtn = new Button
            {
                Content = "✕ Từ chối",
                Padding = new Thickness(10, 3, 10, 3),
            };
            rejectBtn.Click += (_, __) => Model.RejectDraftProfile();
            btnRow.Children.Add(rejectBtn);

            reviewContent.Children.Add(btnRow);
            reviewBox.Child = reviewContent;
            panel.Children.Add(reviewBox);

            // 3. Khung nhập liệu và thanh gợi ý ở đáy
            var bottomArea = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            DockPanel.SetDock(bottomArea, Dock.Bottom);

            // 3.1. Các nút gợi ý nhanh (Suggestion Chips)
            var chipsBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            chipsBar.Children.Add(new TextBlock
            {
                Text = "💡 Gợi ý:",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            });

            var chipPropose = new Button
            {
                Content = "✨ Đề xuất hồ sơ quy ước",
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 2),
                FontSize = 11,
            };
            chipPropose.Click += (_, __) => Model.ProposeProfile();
            chipPropose.SetBinding(IsEnabledProperty, new Binding(nameof(PanelViewModel.IsAgentRunning)) { Converter = new NotConverter() });
            chipsBar.Children.Add(chipPropose);

            var chipLayers = new Button
            {
                Content = "📋 Thống kê layer & block",
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 2),
                FontSize = 11,
            };
            chipLayers.Click += (_, __) => Model.SendSuggestedQuery("Thống kê chi tiết các layer và block trong bản vẽ");
            chipLayers.SetBinding(IsEnabledProperty, new Binding(nameof(PanelViewModel.IsAgentRunning)) { Converter = new NotConverter() });
            chipsBar.Children.Add(chipLayers);

            var chipRules = new Button
            {
                Content = "🔍 Kiểm tra vấn đề quy tắc",
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 2),
                FontSize = 11,
            };
            chipRules.Click += (_, __) => Model.SendSuggestedQuery("Kiểm tra và tóm tắt các vấn đề quy tắc trong bản vẽ");
            chipRules.SetBinding(IsEnabledProperty, new Binding(nameof(PanelViewModel.IsAgentRunning)) { Converter = new NotConverter() });
            chipsBar.Children.Add(chipRules);

            bottomArea.Children.Add(chipsBar);

            // 3.2. Thanh nhập tin nhắn
            var inputBar = new DockPanel();

            var sendBtn = new Button
            {
                Content = "➤ Gửi",
                Padding = new Thickness(14, 3, 14, 3),
                Margin = new Thickness(6, 0, 0, 0),
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
            };
            sendBtn.Click += (_, __) => Model.SendChatMessage();
            sendBtn.SetBinding(IsEnabledProperty, new Binding(nameof(PanelViewModel.IsAgentRunning)) { Converter = new NotConverter() });
            DockPanel.SetDock(sendBtn, Dock.Right);
            inputBar.Children.Add(sendBtn);

            var inputBox = new TextBox
            {
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 4, 6, 4),
                ToolTip = "Nhập câu hỏi hoặc yêu cầu cho AI (nhấn Enter để gửi)...",
            };
            inputBox.SetBinding(TextBox.TextProperty, new Binding(nameof(PanelViewModel.ChatInput)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            inputBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Model.SendChatMessage();
                    e.Handled = true;
                }
            };
            inputBar.Children.Add(inputBox);
            bottomArea.Children.Add(inputBar);

            panel.Children.Add(bottomArea);

            // 4. Khu vực hiển thị cuộc hội thoại (Conversation Scroll Area)
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(2, 4, 4, 4),
            };

            var chatStack = new StackPanel();

            // 4.1. Thông điệp chào mừng khi chưa có tin nhắn
            var welcomeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(8, 16, 8, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 460,
            };
            var welcomeStack = new StackPanel();
            welcomeStack.Children.Add(new TextBlock
            {
                Text = "💬 Trợ lý AI Bản vẽ Đường ống (Tích hợp MCP)",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 58, 138)),
                Margin = new Thickness(0, 0, 0, 4),
                TextAlignment = TextAlignment.Center,
            });
            welcomeStack.Children.Add(new TextBlock
            {
                Text = "Hỏi đáp về quy ước CAD, kiểm tra tuyến ống qua các công cụ MCP hoặc bấm 'Đề xuất hồ sơ' để AI tự động khảo sát.",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                LineHeight = 16,
            });
            welcomeBorder.Child = welcomeStack;
            chatStack.Children.Add(welcomeBorder);

            // 4.2. Danh sách bong bóng hội thoại
            var messageItems = new ItemsControl();
            messageItems.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(PanelViewModel.ChatMessages)));

            var templateSelector = new ChatMessageTemplateSelector
            {
                UserTemplate = BuildBubbleTemplate(
                    HorizontalAlignment.Right,
                    new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                    new SolidColorBrush(Color.FromRgb(29, 78, 216)),
                    new Thickness(1),
                    new CornerRadius(14, 14, 2, 14),
                    new SolidColorBrush(Color.FromRgb(219, 234, 254)),
                    Brushes.White,
                    new Thickness(36, 4, 4, 4)),
                AgentTemplate = BuildBubbleTemplate(
                    HorizontalAlignment.Left,
                    new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    new Thickness(1),
                    new CornerRadius(14, 14, 14, 2),
                    new SolidColorBrush(Color.FromRgb(2, 132, 199)),
                    new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                    new Thickness(4, 4, 36, 4)),
                ToolTemplate = BuildBubbleTemplate(
                    HorizontalAlignment.Center,
                    new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    new Thickness(1),
                    new CornerRadius(8),
                    new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                    new Thickness(20, 2, 20, 2),
                    isMonospace: true),
                SystemTemplate = BuildBubbleTemplate(
                    HorizontalAlignment.Center,
                    new SolidColorBrush(Color.FromRgb(239, 246, 255)),
                    new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                    new Thickness(1),
                    new CornerRadius(8),
                    new SolidColorBrush(Color.FromRgb(29, 78, 216)),
                    new SolidColorBrush(Color.FromRgb(30, 58, 138)),
                    new Thickness(20, 3, 20, 3)),
                ErrorTemplate = BuildBubbleTemplate(
                    HorizontalAlignment.Center,
                    new SolidColorBrush(Color.FromRgb(254, 242, 242)),
                    new SolidColorBrush(Color.FromRgb(254, 202, 202)),
                    new Thickness(1),
                    new CornerRadius(8),
                    new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                    new SolidColorBrush(Color.FromRgb(153, 27, 27)),
                    new Thickness(20, 3, 20, 3)),
            };
            messageItems.ItemTemplateSelector = templateSelector;
            chatStack.Children.Add(messageItems);

            // 4.3. Bong bóng chỉ báo trạng thái suy nghĩ / loading khi Agent đang chạy
            var loadingBubble = BuildLoadingBubble();
            chatStack.Children.Add(loadingBubble);

            scrollViewer.Content = chatStack;

            void UpdateWelcomeState()
            {
                welcomeBorder.Visibility = Model.ChatMessages.Count == 0 && !Model.IsAgentRunning ? Visibility.Visible : Visibility.Collapsed;
            }

            UpdateWelcomeState();
            ((INotifyCollectionChanged)Model.ChatMessages).CollectionChanged += (_, __) =>
            {
                UpdateWelcomeState();
                scrollViewer.Dispatcher.InvokeAsync(() => scrollViewer.ScrollToEnd());
            };

            Model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PanelViewModel.IsAgentRunning))
                {
                    UpdateWelcomeState();
                    sendBtn.Content = Model.IsAgentRunning ? "⏳ Đang chạy..." : "➤ Gửi";
                    scrollViewer.Dispatcher.InvokeAsync(() => scrollViewer.ScrollToEnd());
                }
                else if (e.PropertyName == nameof(PanelViewModel.AgentCurrentStep))
                {
                    scrollViewer.Dispatcher.InvokeAsync(() => scrollViewer.ScrollToEnd());
                }
            };

            panel.Children.Add(scrollViewer);

            return panel;
        }

        private UIElement BuildLoadingBubble()
        {
            var loadingBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)), // Slate 50
                BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)),  // Blue 200
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14, 14, 14, 3),
                Padding = new Thickness(14, 10, 16, 10),
                Margin = new Thickness(4, 6, 36, 6),
                MaxWidth = 520,
            };
            loadingBorder.SetBinding(VisibilityProperty, new Binding(nameof(PanelViewModel.IsAgentRunning)) { Converter = new BoolToVisibilityConverter() });

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 1. Vòng xoay loading động (Animated Arc Spinner)
            var spinnerContainer = new Grid
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 2, 12, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };

            var bgTrack = new Ellipse
            {
                Width = 20,
                Height = 20,
                Stroke = new SolidColorBrush(Color.FromRgb(226, 232, 240)), // Slate 200
                StrokeThickness = 2.5,
            };
            spinnerContainer.Children.Add(bgTrack);

            var spinArc = new Path
            {
                Width = 20,
                Height = 20,
                Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)), // Blue 600
                StrokeThickness = 2.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = Geometry.Parse("M 10 1 A 9 9 0 1 1 1 10"),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };

            var rotate = new RotateTransform();
            spinArc.RenderTransform = rotate;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(0.85),
                RepeatBehavior = RepeatBehavior.Forever,
            };
            rotate.BeginAnimation(RotateTransform.AngleProperty, animation);
            spinnerContainer.Children.Add(spinArc);

            Grid.SetColumn(spinnerContainer, 0);
            contentGrid.Children.Add(spinnerContainer);

            // 2. Nội dung thông báo trạng thái suy nghĩ
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };

            var titleBlock = new TextBlock
            {
                Text = "🧠 Agent đang suy nghĩ, vui lòng chờ...",
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 58, 138)), // Blue 900
                VerticalAlignment = VerticalAlignment.Center,
            };

            var pulseAnim = new DoubleAnimation
            {
                From = 0.65,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(0.8),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            };
            titleBlock.BeginAnimation(UIElement.OpacityProperty, pulseAnim);
            headerRow.Children.Add(titleBlock);
            textStack.Children.Add(headerRow);

            var stepBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(239, 246, 255)), // Blue 50
                BorderBrush = new SolidColorBrush(Color.FromRgb(219, 234, 254)),  // Blue 100
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 3, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            var stepText = new TextBlock
            {
                FontSize = 11.5,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(29, 78, 216)), // Blue 700
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 420,
            };
            stepText.SetBinding(TextBlock.TextProperty, new Binding(nameof(PanelViewModel.AgentCurrentStep)));
            stepBadge.Child = stepText;
            textStack.Children.Add(stepBadge);

            Grid.SetColumn(textStack, 1);
            contentGrid.Children.Add(textStack);

            loadingBorder.Child = contentGrid;
            return loadingBorder;
        }

        private static DataTemplate BuildBubbleTemplate(
            HorizontalAlignment alignment,
            Brush background,
            Brush borderBrush,
            Thickness borderThickness,
            CornerRadius cornerRadius,
            Brush headerBrush,
            Brush bodyBrush,
            Thickness margin,
            bool isMonospace = false)
        {
            var template = new DataTemplate();

            var rootFactory = new FrameworkElementFactory(typeof(Grid));
            rootFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            rootFactory.SetValue(FrameworkElement.MarginProperty, margin);

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, alignment);
            borderFactory.SetValue(Border.BackgroundProperty, background);
            borderFactory.SetValue(Border.BorderBrushProperty, borderBrush);
            borderFactory.SetValue(Border.BorderThicknessProperty, borderThickness);
            borderFactory.SetValue(Border.CornerRadiusProperty, cornerRadius);
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(12, 7, 12, 7));
            borderFactory.SetValue(FrameworkElement.MaxWidthProperty, 540.0);

            var contentStack = new FrameworkElementFactory(typeof(StackPanel));

            var headerBlock = new FrameworkElementFactory(typeof(TextBlock));
            headerBlock.SetValue(TextBlock.FontSizeProperty, 10.5);
            headerBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            headerBlock.SetValue(TextBlock.ForegroundProperty, headerBrush);
            headerBlock.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 3));
            headerBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(ChatMessageItem.DisplayHeader)));
            contentStack.AppendChild(headerBlock);

            var bodyBlock = new FrameworkElementFactory(typeof(TextBlock));
            bodyBlock.SetValue(TextBlock.FontSizeProperty, 12.0);
            bodyBlock.SetValue(TextBlock.ForegroundProperty, bodyBrush);
            bodyBlock.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            if (isMonospace)
            {
                bodyBlock.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas"));
            }
            bodyBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(ChatMessageItem.Text)));
            contentStack.AppendChild(bodyBlock);

            borderFactory.AppendChild(contentStack);
            rootFactory.AppendChild(borderFactory);
            template.VisualTree = rootFactory;

            return template;
        }

        private sealed class ChatMessageTemplateSelector : DataTemplateSelector
        {
            public DataTemplate UserTemplate { get; set; } = null!;
            public DataTemplate AgentTemplate { get; set; } = null!;
            public DataTemplate ToolTemplate { get; set; } = null!;
            public DataTemplate SystemTemplate { get; set; } = null!;
            public DataTemplate ErrorTemplate { get; set; } = null!;

            public override DataTemplate SelectTemplate(object item, DependencyObject container)
            {
                if (item is ChatMessageItem m)
                {
                    if (m.IsUser) return UserTemplate;
                    if (m.IsAgent) return AgentTemplate;
                    if (m.IsTool) return ToolTemplate;
                    if (m.IsError) return ErrorTemplate;
                    return SystemTemplate;
                }
                return base.SelectTemplate(item, container);
            }
        }
    }
}
