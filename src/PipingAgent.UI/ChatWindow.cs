using System;
using System.Windows;
using System.Windows.Media;

namespace PipingAgent.UI
{
    /// <summary>
    /// Cửa sổ Nổi Trò chuyện (Modeless Chat Window) với Trợ lý AI.
    /// Nổi tự do phía trên AutoCAD, cho phép kỹ sư vừa xem/pan/zoom bản vẽ vừa tương tác trực tiếp với Agent.
    /// </summary>
    public sealed class ChatWindow : Window
    {
        public ChatWindow(PanelViewModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            DataContext = model;

            Title = "Piping AI Assistant - Trợ lý Bản vẽ";
            Width = 520;
            Height = 680;
            MinWidth = 380;
            MinHeight = 450;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));

            Content = new ChatView(model);
        }

        public PanelViewModel Model { get; }
    }
}
