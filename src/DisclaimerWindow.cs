using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JiYuKiller.UI
{
    /// <summary>免责声明窗口（原版启动时的 messageBox，自定义按钮）。</summary>
    internal class DisclaimerWindow : Window
    {
        /// <summary>点击"退出"时为 true —— 需要写入"重要电脑"标记。</summary>
        public bool MarkImportantComputer { get; private set; }

        public DisclaimerWindow()
        {
            Title = "免责声明及提醒";
            Width = 560;
            Height = 320;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Icon = UiUtil.LoadAppIcon();
            Background = UiUtil.C.Window;

            var title = new TextBlock
            {
                Text = "免责声明及提醒",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = UiUtil.C.Text,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10),
            };

            var body = new TextBlock
            {
                Text = "此软件仅用于整蛊, 请不要用于其他用途!所造成的一切后果与软件制作人无关!\n\n同时, 运行时请关闭杀毒软件!",
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = UiUtil.C.Text2,
                Margin = new Thickness(24, 0, 24, 10),
            };

            var buttonY = new Button
            {
                Content = "   我已认真阅读并同意  ",
                Width = 180,
                Height = 32,
                Style = UiUtil.PrimaryButtonStyle(),
            };
            var buttonN = new Button
            {
                Content = "退出",
                Width = 90,
                Height = 32,
                Style = UiUtil.ButtonStyle(),
            };

            buttonY.Click += (s, e) => { DialogResult = true; };
            buttonN.Click += (s, e) => { MarkImportantComputer = true; DialogResult = false; };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 12),
            };
            buttons.Children.Add(buttonY);
            buttons.Children.Add(new FrameworkElement { Margin = new Thickness(20, 0, 0, 0), Width = 20 });
            buttons.Children.Add(buttonN);

            var root = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            root.Children.Add(title);
            root.Children.Add(body);
            root.Children.Add(buttons);
            Content = root;
        }
    }
}
