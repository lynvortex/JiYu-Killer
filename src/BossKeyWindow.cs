using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace JiYuKiller.UI
{
    /// <summary>
    /// 窗口隐藏助手窗口（控制面板）。
    /// 引擎挂在主窗口上 —— 关闭本窗口后热键依然有效, 重新打开可停止。
    /// </summary>
    internal class BossKeyWindow : Window
    {
        private readonly BossKeyEngine _engine;
        private readonly Button _toggle;
        private readonly TextBlock _status;
        private readonly ListBox _list;
        private readonly DispatcherTimer _refresh;

        public BossKeyWindow(MainWindow owner)
        {
            _engine = owner.GetBossKeyEngine();

            Title = "窗口隐藏助手";
            Width = 560;
            Height = 430;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = UiUtil.AppFont;
            Background = UiUtil.C.Window;
            Icon = UiUtil.LoadAppIcon();
            Owner = owner;

            var header = new TextBlock
            {
                Text = "Boss 键 —— 一键藏起当前窗口",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = UiUtil.C.Text,
                Margin = new Thickness(0, 0, 0, 8),
            };

            var help = new TextBlock
            {
                Text = "Alt+B  隐藏当前前台窗口（可连按隐藏多个）\n"
                     + "Alt+N  恢复最近隐藏的窗口\n"
                     + "Alt+H  首次按创建并切入第 2 虚拟桌面, 再按在两个桌面间切换\n"
                     + "已隐藏的窗口每 100ms 自动再隐藏一次, 防止被极域强制重新显示。",
                FontSize = 12.5,
                LineHeight = 21,
                Foreground = UiUtil.C.Text2,
                Margin = new Thickness(0, 0, 0, 12),
            };

            _toggle = new Button { Content = "启动热键", MinWidth = 110, Height = 32, Style = UiUtil.PrimaryButtonStyle() };
            _toggle.Click += (s, e) => ToggleEngine();
            var restoreAll = new Button { Content = "恢复全部", MinWidth = 100, Height = 32, Margin = new Thickness(10, 0, 0, 0), Style = UiUtil.ButtonStyle() };
            restoreAll.Click += (s, e) => { _engine.RestoreAll(); Refresh(); };
            var clear = new Button { Content = "清空列表", MinWidth = 100, Height = 32, Margin = new Thickness(10, 0, 0, 0), Style = UiUtil.ButtonStyle() };
            clear.Click += (s, e) => { _engine.ClearHiddenList(); Refresh(); };

            _status = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
                FontSize = 12.5,
                Foreground = UiUtil.C.Muted,
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            row.Children.Add(_toggle);
            row.Children.Add(restoreAll);
            row.Children.Add(clear);
            row.Children.Add(_status);

            _list = new ListBox
            {
                Height = 150,
                Style = UiUtil.LogListStyle(),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };

            var note = new TextBlock
            {
                Text = "说明: 本程序自己的窗口不会被隐藏; 关闭本窗口后热键仍有效, 重新打开这里可停止。",
                FontSize = 11.5,
                Foreground = UiUtil.C.Muted,
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            };

            var root = new StackPanel { Margin = new Thickness(16) };
            root.Children.Add(header);
            root.Children.Add(help);
            root.Children.Add(row);
            root.Children.Add(_list);
            root.Children.Add(note);
            Content = root;

            _refresh = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _refresh.Tick += (s, e) => Refresh();
            Loaded += (s, e) => { Refresh(); _refresh.Start(); };
            Closed += (s, e) => _refresh.Stop();
        }

        private void ToggleEngine()
        {
            if (_engine.Active)
                _engine.Stop();
            else
                _engine.Start();
            Refresh();
        }

        private void Refresh()
        {
            _toggle.Content = _engine.Active ? "停止热键" : "启动热键";
            _status.Text = _engine.Active ? "运行中 · 已隐藏 " + _engine.HiddenCount + " 个窗口" : "未启动";
            _list.Items.Clear();
            foreach (string title in _engine.HiddenTitles())
                _list.Items.Add(title);
        }
    }
}
