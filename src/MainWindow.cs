using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using JiYuKiller.Core;

namespace JiYuKiller.UI
{
    /// <summary>
    /// 主窗口：深色/浅色主题 + 侧边栏导航。
    /// 主题切换通过"保存设置并重建窗口"实现（Settings.Theme）。
    /// </summary>
    internal partial class MainWindow : Window
    {
        private ComboBox _jyComboBox;
        private ComboBox _orderComboBox;
        private ComboBox _launchComboBox;
        private ComboBox _msgPresetComboBox;
        private ComboBox _targetComboBox;
        private TextBox _msgLine;
        private TextBox _commandLine;
        private TextBox _webLine;
        private TextBox _ipLine;
        private TextBox _delayBox;
        private TextBox _roundsBox;
        private TextBox _intervalBox;
        private ListBox _logViewer;
        private Button _sendMsgBtn;
        private Button _commandBtn;
        private Button _webBtn;
        private Button _generateBtn;
        private Button _closeAllBtn;
        private Button _stopBtn;
        private Button _scanBtn;
        private TextBlock _ipStatus;
        private TextBlock _adminStatus;
        private ContentControl _pageHost;
        private readonly Dictionary<string, ToggleButton> _navButtons = new Dictionary<string, ToggleButton>();
        private readonly Dictionary<string, UIElement> _pages = new Dictionary<string, UIElement>();
        private CancellationTokenSource _scanCts;
        private int _historyIndex = -1;
        private bool _busy;
        private volatile bool _closed;
        private TextBlock _statusPanel;
        private DispatcherTimer _statusTimer;
        private CornerHotzone _hotzone;
        private CheckBox _cornerToggle;
        private CheckBox _multiPortToggle;
        private TrayIcon _tray;

        public MainWindow()
        {
            Settings.Load();
            Palette.Apply(Settings.Theme);
            JyVersion.SetSelected(Settings.VersionIndex);
            JySender.UseGlobalBroadcast = Settings.UseGlobalBroadcast;

            Title = "JiYu Killer";
            Width = 1121;
            Height = 660;
            MinWidth = 900;
            MinHeight = 580;
            WindowStartupLocation = WindowStartupLocation.Manual;
            FontFamily = UiUtil.AppFont;
            Background = UiUtil.C.Window;
            Icon = UiUtil.LoadAppIcon();
            SourceInitialized += (s, e) =>
            {
                EnableDarkTitleBar();
                SetupTrayAndSelfHotkey();
            };
            Closed += (s, e) => _closed = true;
            BuildLayout();
            PreviewKeyDown += OnPreviewKeyDown;
            Loaded += (s, e) => PlayEntryAnimation();
        }

        private void EnableDarkTitleBar()
        {
            try
            {
                int on = Settings.Theme == "dark" ? 1 : 0;
                DwmHelper.DwmSetWindowAttribute(
                    new System.Windows.Interop.WindowInteropHelper(this).Handle, 20, ref on, 4);
            }
            catch { }
        }

        private void PlayEntryAnimation()
        {
            var workArea = SystemParameters.WorkArea;
            double targetTop = Math.Max(workArea.Bottom - Height, 0) / 2 + 40;
            Left = (workArea.Width - Width) / 2 + workArea.Left;
            Top = workArea.Bottom;
            var anim = new DoubleAnimation(targetTop, TimeSpan.FromMilliseconds(450))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut },
            };
            BeginAnimation(Window.TopProperty, anim);
        }

        // ------------------------------------------------------------------ layout

        private void BuildLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(BuildMain());
            var status = BuildStatusBar();
            Grid.SetRow(status, 1);
            root.Children.Add(status);
            Content = root;
            SwitchPage("remote");
            RefreshIpStatus();
        }

        private UIElement BuildMain()
        {
            var main = new Grid();
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var sidebar = BuildSidebar();
            Grid.SetColumn(sidebar, 0);
            main.Children.Add(sidebar);

            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(content, 1);
            main.Children.Add(content);

            var topbar = BuildTopbar();
            Grid.SetRow(topbar, 0);
            content.Children.Add(topbar);

            _pageHost = new ContentControl { Margin = new Thickness(12, 12, 12, 0) };
            Grid.SetRow(_pageHost, 1);
            content.Children.Add(_pageHost);

            var logCard = BuildLogCard();
            Grid.SetRow(logCard, 2);
            content.Children.Add(logCard);

            return main;
        }

        private UIElement BuildSidebar()
        {
            var sidebar = new Border
            {
                Background = UiUtil.C.Sidebar,
                BorderBrush = UiUtil.C.SidebarLine,
                BorderThickness = new Thickness(0, 0, 1, 0),
            };
            var dock = new DockPanel();

            var exit = new Button { Content = "退出", Style = UiUtil.ButtonStyle(), Margin = new Thickness(10, 8, 10, 10) };
            exit.Click += (s, e) => Close();
            DockPanel.SetDock(exit, Dock.Bottom);
            dock.Children.Add(exit);

            var stack = new StackPanel();

            // Logo
            var logo = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 14, 10, 14) };
            logo.Children.Add(new Image { Width = 30, Height = 30, Source = UiUtil.LoadAppIcon() });
            var logoText = new StackPanel { Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            logoText.Children.Add(new TextBlock
            {
                Text = "JiYu Killer",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = UiUtil.C.Text,
            });
            logoText.Children.Add(new TextBlock { Text = "v1.0.0", FontSize = 11, Foreground = UiUtil.C.Muted });
            logo.Children.Add(logoText);
            stack.Children.Add(logo);

            _pages["remote"] = BuildRemotePanel();
            _pages["danger"] = BuildDangerPanel();
            _pages["tools"] = BuildToolsPanel();
            _pages["battle"] = BuildBattlePanel();

            AddNavItem(stack, "remote", "远程控制");
            AddNavItem(stack, "danger", "关闭极域 (高危)");
            AddNavItem(stack, "tools", "IP.txt 与本机");
            AddNavItem(stack, "battle", "进程与对抗");

            stack.Children.Add(new Border
            {
                Height = 1,
                Background = UiUtil.C.SidebarLine,
                Margin = new Thickness(10, 10, 10, 6),
            });

            var theme = new Button { Content = Settings.Theme == "dark" ? "切换浅色主题" : "切换深色主题", Style = UiUtil.ButtonStyle(), Margin = new Thickness(10, 3, 10, 3) };
            theme.Click += (s, e) => ToggleTheme();
            stack.Children.Add(theme);

            if (!LocalOps.IsAdmin())
            {
                var elevate = new Button { Content = "以管理员身份重启", Style = UiUtil.ButtonStyle(), Margin = new Thickness(10, 3, 10, 3) };
                elevate.Click += (s, e) => Elevate();
                stack.Children.Add(elevate);
            }

            var p2p = new Button { Content = "点对点消息", Style = UiUtil.ButtonStyle(), Margin = new Thickness(10, 3, 10, 3) };
            p2p.Click += (s, e) => new P2pWindow { Owner = this }.Show();
            stack.Children.Add(p2p);

            var boss = new Button { Content = "窗口隐藏助手", Style = UiUtil.ButtonStyle(), Margin = new Thickness(10, 3, 10, 3) };
            boss.Click += (s, e) => new BossKeyWindow(this) { Owner = this }.Show();
            stack.Children.Add(boss);

            var help = new Button { Content = "使用说明", Style = UiUtil.ButtonStyle(), Margin = new Thickness(10, 3, 10, 3) };
            help.Click += (s, e) => ShowHelp();
            stack.Children.Add(help);

            var about = new Button { Content = "关于", Style = UiUtil.ButtonStyle(), Margin = new Thickness(10, 3, 10, 3) };
            about.Click += (s, e) => ShowAbout();
            stack.Children.Add(about);

            // 万能密码提示(点击复制) —— 放在关于按钮下方的空白区
            var pwdTip = new TextBlock
            {
                Text = "万能密码\n(点击复制)\nmythware_super_password",
                FontSize = 11,
                LineHeight = 17,
                Foreground = UiUtil.C.Muted,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(10, 6, 10, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            pwdTip.MouseDown += (s, e) =>
            {
                try { Clipboard.SetText(JyPassword.SuperPassword); AddInfo("万能密码已复制到剪贴板: " + JyPassword.SuperPassword); }
                catch (Exception ex) { AddInfo("[错误] 复制失败: " + ex.Message); }
            };
            ToolTipService.SetToolTip(pwdTip, "极域设置/卸载窗口的密码框直接输入; 点击复制");
            stack.Children.Add(pwdTip);

            dock.Children.Add(stack);
            sidebar.Child = dock;
            return sidebar;
        }

        private void AddNavItem(StackPanel stack, string key, string label)
        {
            var btn = new ToggleButton { Content = label, Style = UiUtil.NavItemStyle(), FocusVisualStyle = null };
            btn.Checked += (s, e) => SwitchPage(key);
            btn.Click += (s, e) => { if (btn.IsChecked != true) btn.IsChecked = true; else SwitchPage(key); };
            _navButtons[key] = btn;
            stack.Children.Add(btn);
        }

        private bool _switchingPage;

        private void SwitchPage(string key)
        {
            if (_switchingPage || _pageHost == null)
                return;
            _switchingPage = true;
            try
            {
                foreach (var kv in _navButtons)
                    kv.Value.IsChecked = kv.Key == key;
                _pageHost.Content = _pages[key];
            }
            finally
            {
                _switchingPage = false;
            }
        }

        private UIElement BuildTopbar()
        {
            var bar = new Border
            {
                Background = UiUtil.C.Topbar,
                BorderBrush = UiUtil.C.SidebarLine,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8),
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            g.Children.Add(UiUtil.MakeLabel("极域版本"));
            var versionBox = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _jyComboBox = new ComboBox { Width = 130, Height = 30, Style = UiUtil.ComboBoxStyle(), VerticalContentAlignment = VerticalAlignment.Center };
            foreach (var name in JyVersion.Names)
                _jyComboBox.Items.Add(name);
            _jyComboBox.SelectedIndex = JyVersion.SelectedIndex;
            var ok = new Button { Content = "确定", MinWidth = 64, Margin = new Thickness(8, 0, 0, 0), Style = UiUtil.PrimaryButtonStyle() };
            ok.Click += (s, e) =>
            {
                JyVersion.SetSelected(_jyComboBox.SelectedIndex);
                Settings.VersionIndex = JyVersion.SelectedIndex;
                Settings.Save();
                AddInfo("极域版本已设置为: " + JyVersion.Names[JyVersion.SelectedIndex] + " (端口 " + JyVersion.Port + ")");
            };
            versionBox.Children.Add(_jyComboBox);
            versionBox.Children.Add(ok);
            Grid.SetColumn(versionBox, 1);
            g.Children.Add(versionBox);

            var targetLabel = UiUtil.MakeLabel("发送目标");
            targetLabel.Margin = new Thickness(18, 0, 0, 0);
            Grid.SetColumn(targetLabel, 2);
            g.Children.Add(targetLabel);

            _targetComboBox = new ComboBox { Width = 185, Height = 30, Style = UiUtil.ComboBoxStyle(), VerticalContentAlignment = VerticalAlignment.Center };
            _targetComboBox.Items.Add("IP.txt 中的全部 IP");
            _targetComboBox.Items.Add("全局广播 " + JySender.MulticastGroup);
            _targetComboBox.SelectedIndex = JySender.UseGlobalBroadcast ? 1 : 0;
            _targetComboBox.SelectionChanged += (s, e) =>
            {
                JySender.UseGlobalBroadcast = _targetComboBox.SelectedIndex == 1;
                Settings.UseGlobalBroadcast = JySender.UseGlobalBroadcast;
                Settings.Save();
                RefreshIpStatus();
                AddInfo(JySender.UseGlobalBroadcast
                    ? "发送目标已切换为全局广播 " + JySender.MulticastGroup
                    : "发送目标已切换为 IP.txt");
            };
            Grid.SetColumn(_targetComboBox, 3);
            g.Children.Add(_targetComboBox);

            var hint = UiUtil.MakeText(JySender.MulticastGroup + " 是全局广播: 指令对网段内所有极域客户端生效, 无需配置 IP.txt。", 11.5, muted: true);
            hint.TextTrimming = TextTrimming.CharacterEllipsis;
            hint.HorizontalAlignment = HorizontalAlignment.Right;
            hint.Margin = new Thickness(10, 0, 10, 0);
            Grid.SetColumn(hint, 4);
            g.Children.Add(hint);

            _stopBtn = new Button
            {
                Content = "紧急停止 (Esc)",
                MinWidth = 130,
                Style = UiUtil.ButtonStyle(),
                IsEnabled = false,
            };
            _stopBtn.Click += (s, e) => StopSending();
            Grid.SetColumn(_stopBtn, 5);
            g.Children.Add(_stopBtn);

            bar.Child = g;
            return bar;
        }

        // ------------------------------------------------------------------ pages

        private UIElement BuildRemotePanel()
        {
            var stack = new StackPanel();

            var basicRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            basicRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            basicRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            basicRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            basicRow.Children.Add(UiUtil.MakeLabel("基本操作"));
            _orderComboBox = new ComboBox { Height = 30, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Style = UiUtil.ComboBoxStyle() };
            foreach (var name in new[] { "打开记事本", "关机", "强制重启", "提示重启", "关闭所有应用程序", "杀掉桌面进程" })
                _orderComboBox.Items.Add(name);
            _orderComboBox.SelectedIndex = 0;
            Grid.SetColumn(_orderComboBox, 1);
            basicRow.Children.Add(_orderComboBox);
            var go = new Button { Content = "确定", MinWidth = 76, Style = UiUtil.PrimaryButtonStyle() };
            go.Click += (s, e) => BasicClick();
            Grid.SetColumn(go, 2);
            basicRow.Children.Add(go);
            stack.Children.Add(basicRow);

            // 快捷程序（走已验证的程序启动模板, 只换路径）
            var launchRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            launchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            launchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            launchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            launchRow.Children.Add(UiUtil.MakeLabel("快捷启动"));
            _launchComboBox = new ComboBox { Height = 30, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Style = UiUtil.ComboBoxStyle() };
            foreach (var name in JyPackets.LaunchPresetNames)
                _launchComboBox.Items.Add(name);
            _launchComboBox.SelectedIndex = 0;
            Grid.SetColumn(_launchComboBox, 1);
            launchRow.Children.Add(_launchComboBox);
            var launchBtn = new Button { Content = "启动", MinWidth = 76, Style = UiUtil.PrimaryButtonStyle() };
            launchBtn.Click += (s, e) => LaunchClick();
            Grid.SetColumn(launchBtn, 2);
            launchRow.Children.Add(launchBtn);
            stack.Children.Add(launchRow);

            Button sendMsg, sendCmd, sendWeb;
            stack.Children.Add(MakeActionRow("发送消息", "输入要发送的信息...", out _msgLine, out sendMsg, "发送",
                () => SendClick(() =>
                {
                    string text = _msgLine.Text.Trim();
                    if (text.Length == 0) return null;
                    return target => JyPackets.BuildMessage(target, text, JyVersion.Port);
                })));
            _sendMsgBtn = sendMsg;

            // 消息预设
            var presetRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            presetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            presetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            presetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            presetRow.Children.Add(UiUtil.MakeLabel("消息模板"));
            _msgPresetComboBox = new ComboBox { Height = 30, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Style = UiUtil.ComboBoxStyle() };
            foreach (var m in MessagePresets)
                _msgPresetComboBox.Items.Add(m);
            _msgPresetComboBox.SelectedIndex = 0;
            Grid.SetColumn(_msgPresetComboBox, 1);
            presetRow.Children.Add(_msgPresetComboBox);
            var presetToBox = new Button { Content = "填入", MinWidth = 76, Style = UiUtil.ButtonStyle() };
            presetToBox.Click += (s, e) => { _msgLine.Text = (string)_msgPresetComboBox.SelectedItem; _msgLine.Focus(); };
            Grid.SetColumn(presetToBox, 2);
            presetRow.Children.Add(presetToBox);
            stack.Children.Add(presetRow);

            stack.Children.Add(MakeActionRow("系统命令 (/h=隐藏)", "输入要发送的命令...", out _commandLine, out sendCmd, "发送",
                () => SendClick(() =>
                {
                    string text = _commandLine.Text.Trim();
                    if (text.Length == 0) return null;
                    Settings.AddHistory(text);
                    _historyIndex = -1;
                    bool hidden = text.StartsWith("/h ");
                    string cmd = hidden ? text.Substring(3) : text;
                    return target => hidden
                        ? JyPackets.BuildHiddenCommand(target, cmd, JyVersion.Port)
                        : JyPackets.BuildCommand(target, cmd, false);
                })));
            _commandBtn = sendCmd;
            _commandLine.KeyDown += CommandHistoryKey;

            stack.Children.Add(MakeActionRow("打开文件/网页", "输入要打开的文件或网页...", out _webLine, out sendWeb, "发送",
                () => SendClick(() =>
                {
                    string text = _webLine.Text.Trim();
                    if (text.Length == 0) return null;
                    return target => JyPackets.BuildStartOpen(target, text);
                })));
            _webBtn = sendWeb;

            // 延时 / 重复
            var optRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i < 6; i++)
                optRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            optRow.Children.Add(UiUtil.MakeLabel("延时(秒)"));
            _delayBox = MakeSmallBox("0");
            Grid.SetColumn(_delayBox, 1);
            optRow.Children.Add(_delayBox);
            var roundsLabel = UiUtil.MakeLabel("重复(次)");
            roundsLabel.Margin = new Thickness(14, 0, 0, 0);
            Grid.SetColumn(roundsLabel, 2);
            optRow.Children.Add(roundsLabel);
            _roundsBox = MakeSmallBox("1");
            Grid.SetColumn(_roundsBox, 3);
            optRow.Children.Add(_roundsBox);
            var intervalLabel = UiUtil.MakeLabel("间隔(秒)");
            intervalLabel.Margin = new Thickness(14, 0, 0, 0);
            Grid.SetColumn(intervalLabel, 4);
            optRow.Children.Add(intervalLabel);
            _intervalBox = MakeSmallBox("5");
            Grid.SetColumn(_intervalBox, 5);
            optRow.Children.Add(_intervalBox);
            var optHint = UiUtil.MakeText("(仅对 发送/启动 生效)", 11.5, muted: true);
            optHint.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(optHint, 6);
            optRow.Children.Add(optHint);
            stack.Children.Add(optRow);

            var multiPort = new CheckBox
            {
                Content = "多端口齐发 (4605 + 4705 + 4988, 兼容全部极域版本)",
                FontSize = 12.5,
                Foreground = UiUtil.C.Text2,
                Margin = new Thickness(0, 8, 0, 2),
            };
            multiPort.IsChecked = JyVersion.MultiPort;
            multiPort.Checked += (s, e) => { JyVersion.MultiPort = true; AddInfo("已启用多端口齐发, 每个目标向三个候选端口各发一份。"); };
            multiPort.Unchecked += (s, e) => { JyVersion.MultiPort = false; AddInfo("已关闭多端口齐发。"); };
            stack.Children.Add(multiPort);

            return UiUtil.Card("远程控制", stack);
        }

        private static readonly string[] MessagePresets =
        {
            "请大家安静，开始上课了。",
            "请不要使用与课堂无关的软件。",
            "还有 5 分钟下课，请保存好文件。",
            "本次操作已完成。",
        };

        private TextBox MakeSmallBox(string text)
        {
            return new TextBox
            {
                Text = text,
                Width = 52,
                Height = 28,
                Style = UiUtil.TextBoxStyle(),
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
        }

        private Grid MakeActionRow(string labelText, string placeholder, out TextBox line, out Button button,
                                   string buttonText, Action send)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l = UiUtil.MakeLabel(labelText);
            Grid.SetColumn(l, 0);
            line = new TextBox { Height = 30, Style = UiUtil.TextBoxStyle() };
            line.SetValue(TextBoxHelper.PlaceholderProperty, placeholder);
            line.KeyDown += (s, e) => { if (e.Key == Key.Enter) send(); };
            Grid.SetColumn(line, 1);
            button = new Button { Content = buttonText, MinWidth = 76, Margin = new Thickness(8, 0, 0, 0), Style = UiUtil.PrimaryButtonStyle() };
            button.Click += (s, e) => send();
            Grid.SetColumn(button, 2);
            g.Children.Add(l);
            g.Children.Add(line);
            g.Children.Add(button);
            return g;
        }

        private UIElement BuildDangerPanel()
        {
            var card = new Border
            {
                Background = UiUtil.C.DangerCard,
                BorderBrush = UiUtil.C.DangerCardLine,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 16, 20, 16),
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "(高危操作!) 关闭所有极域连接",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = UiUtil.C.Accent,
            });
            stack.Children.Add(new TextBlock
            {
                Text = "向全局广播地址 " + JySender.MulticastGroup + " 发送 /h taskkill /f /im studentmain.exe (/h = 隐藏命令窗口)。\n"
                     + JySender.MulticastGroup + " 是全局广播地址: 网段内所有极域客户端(含教师机)均会收到并关闭极域主进程, 无需配置 IP.txt。\n"
                     + "如需恢复本机状态, 可在\"IP.txt 与本机\"页使用一键恢复。",
                FontSize = 13,
                LineHeight = 22,
                Foreground = UiUtil.C.Text2,
                Margin = new Thickness(0, 10, 0, 16),
                TextWrapping = TextWrapping.Wrap,
            });
            _closeAllBtn = new Button
            {
                Content = "关闭所有极域连接",
                Style = UiUtil.DangerButtonStyle(),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            _closeAllBtn.Click += (s, e) => CloseAllJyConnectFlow();
            stack.Children.Add(_closeAllBtn);
            card.Child = stack;
            return card;
        }

        private UIElement BuildToolsPanel()
        {
            var stack = new StackPanel();

            // IP.txt 生成器
            var genRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            for (int i = 0; i < 5; i++)
                genRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            genRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), FontSize = 13, LineHeight = 18 };
            label.Inlines.Add(new Run("IP.txt 生成器") { FontWeight = FontWeights.Bold, Foreground = UiUtil.C.Text });
            label.Inlines.Add(new Run("\n输入网段一键展开全部 IP (示例: 10.132.5.0/24)") { FontSize = 11, Foreground = UiUtil.C.Muted });
            Grid.SetColumn(label, 0);
            genRow.Children.Add(label);

            var fillLocal = new Button { Content = "填入本机网段", MinWidth = 110, Margin = new Thickness(0, 0, 8, 0), Style = UiUtil.ButtonStyle() };
            fillLocal.Click += (s, e) => FillLocalSubnet();
            Grid.SetColumn(fillLocal, 1);
            genRow.Children.Add(fillLocal);

            _ipLine = new TextBox { Height = 30, MinWidth = 150, Margin = new Thickness(0, 0, 8, 0), Style = UiUtil.TextBoxStyle() };
            _ipLine.SetValue(TextBoxHelper.PlaceholderProperty, "输入要生成的IP段...");
            _ipLine.KeyDown += (s, e) => { if (e.Key == Key.Enter) GenerateHosts(); };
            Grid.SetColumn(_ipLine, 2);
            genRow.Children.Add(_ipLine);

            _generateBtn = new Button { Content = "生成", MinWidth = 70, Margin = new Thickness(0, 0, 8, 0), Style = UiUtil.PrimaryButtonStyle() };
            _generateBtn.Click += (s, e) => GenerateHosts();
            Grid.SetColumn(_generateBtn, 3);
            genRow.Children.Add(_generateBtn);

            var edit = new Button { Content = "编辑IP.txt", MinWidth = 90 };
            edit.Style = UiUtil.ButtonStyle();
            edit.Click += (s, e) =>
            {
                var dlg = new EditIpDialog { Owner = this };
                dlg.ShowDialog();
                AddInfo("IP.txt 已保存。");
                RefreshIpStatus();
            };
            Grid.SetColumn(edit, 4);
            genRow.Children.Add(edit);
            stack.Children.Add(genRow);

            // 局域网扫描（仅本机网段）
            var scanRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            scanRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            scanRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            scanRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            scanRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            scanRow.Children.Add(UiUtil.MakeLabel("局域网扫描"));
            _scanBtn = new Button { Content = "扫描本机网段", MinWidth = 120, Margin = new Thickness(0, 0, 8, 0), Style = UiUtil.ButtonStyle() };
            _scanBtn.Click += (s, e) => ScanSubnet();
            Grid.SetColumn(_scanBtn, 1);
            scanRow.Children.Add(_scanBtn);
            var scanWrite = new Button { Content = "存活主机写入IP.txt", MinWidth = 150 };
            scanWrite.Style = UiUtil.ButtonStyle();
            scanWrite.Click += (s, e) => WriteScanResult();
            Grid.SetColumn(scanWrite, 2);
            scanRow.Children.Add(scanWrite);
            var scanHint = UiUtil.MakeText("仅扫描本机所在 /24 网段, 结果记入日志", 11.5, muted: true);
            scanHint.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(scanHint, 3);
            scanRow.Children.Add(scanHint);
            stack.Children.Add(scanRow);

            // 本机操作
            var localRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            localRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            localRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            localRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            localRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            localRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            localRow.Children.Add(UiUtil.MakeLabel("本机操作"));
            var closeJy = new Button { Content = "关掉此电脑上的极域", MinWidth = 160, Margin = new Thickness(0, 0, 10, 0), Style = UiUtil.ButtonStyle() };
            closeJy.Click += (s, e) => CloseLocalJy();
            Grid.SetColumn(closeJy, 2);
            localRow.Children.Add(closeJy);
            var clearWeb = new Button { Content = "一键解除U盘和网络限制", MinWidth = 180, Margin = new Thickness(0, 0, 10, 0), Style = UiUtil.ButtonStyle() };
            clearWeb.Click += (s, e) => ClearWebControl();
            Grid.SetColumn(clearWeb, 3);
            localRow.Children.Add(clearWeb);
            var restore = new Button { Content = "一键恢复", MinWidth = 90, Style = UiUtil.ButtonStyle() };
            restore.Click += (s, e) => RestoreLocal();
            Grid.SetColumn(restore, 4);
            localRow.Children.Add(restore);
            stack.Children.Add(localRow);

            // 自保与实验（来自翘课3.0 的功能借鉴）
            var extraRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            extraRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            extraRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            extraRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            extraRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            extraRow.Children.Add(UiUtil.MakeLabel("自保/实验"));
            var boost = new Button { Content = "提升自身优先级", MinWidth = 130, Margin = new Thickness(0, 0, 10, 0), Style = UiUtil.ButtonStyle() };
            boost.Click += (s, e) => AddInfo(LocalOps.BoostSelfPriority());
            Grid.SetColumn(boost, 1);
            extraRow.Children.Add(boost);
            var op6 = new Button { Content = "发送 op=6 指令包 (实验)", MinWidth = 170, Style = UiUtil.ButtonStyle() };
            op6.Click += (s, e) => SendOp6();
            Grid.SetColumn(op6, 2);
            extraRow.Children.Add(op6);
            var extraHint = UiUtil.MakeText("op=6 来自翘课3.0, 作用待真机验证", 11.5, muted: true);
            extraHint.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(extraHint, 3);
            extraRow.Children.Add(extraHint);
            stack.Children.Add(extraRow);
            // 密码工具 (学习自 mythwarehelper)
            var pwdRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            pwdRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pwdRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pwdRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pwdRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pwdRow.Children.Add(UiUtil.MakeLabel("密码工具"));
            var readPwd = new Button { Content = "读取本机极域密码", MinWidth = 140, Margin = new Thickness(0, 0, 10, 0), Style = UiUtil.ButtonStyle() };
            readPwd.Click += (s, e) => ReadLocalPassword();
            Grid.SetColumn(readPwd, 1);
            pwdRow.Children.Add(readPwd);
            var copyPwd = new Button { Content = "复制万能密码", MinWidth = 120, Style = UiUtil.ButtonStyle() };
            copyPwd.Click += (s, e) =>
            {
                try { Clipboard.SetText(JyPassword.SuperPassword); AddInfo("万能密码已复制到剪贴板: " + JyPassword.SuperPassword); }
                catch (Exception ex) { AddInfo("[错误] 复制失败: " + ex.Message); }
            };
            Grid.SetColumn(copyPwd, 2);
            pwdRow.Children.Add(copyPwd);
            var pwdHint = UiUtil.MakeText("knock1 解密, 读到的是本机被设置的真实密码", 11.5, muted: true);
            pwdHint.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(pwdHint, 3);
            pwdRow.Children.Add(pwdHint);
            stack.Children.Add(pwdRow);

            // 进程控制 (挂起/恢复, 学习自 mythwarehelper)
            var susRow = new Grid();
            susRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            susRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            susRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            susRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            susRow.Children.Add(UiUtil.MakeLabel("进程控制"));
            var suspend = new Button { Content = "挂起极域", MinWidth = 100, Margin = new Thickness(0, 0, 10, 0), Style = UiUtil.ButtonStyle() };
            suspend.Click += (s, e) => SuspendJy(true);
            Grid.SetColumn(suspend, 1);
            susRow.Children.Add(suspend);
            var resume = new Button { Content = "恢复极域", MinWidth = 100, Margin = new Thickness(0, 0, 10, 0), Style = UiUtil.ButtonStyle() };
            resume.Click += (s, e) => SuspendJy(false);
            Grid.SetColumn(resume, 2);
            susRow.Children.Add(resume);
            var susHint = UiUtil.MakeText("挂起后广播/控制立即失效, 恢复后极域无需重开; 比杀进程优雅", 11.5, muted: true);
            susHint.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(susHint, 3);
            susRow.Children.Add(susHint);
            stack.Children.Add(susRow);

            // 配置与日志
            var cfgRow = new Grid();
            cfgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cfgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cfgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cfgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cfgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cfgRow.Children.Add(UiUtil.MakeLabel("配置/日志"));
            var exportCfg = new Button { Content = "导出配置", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), Style = UiUtil.ButtonStyle() };
            exportCfg.Click += (s, e) => ExportConfig();
            Grid.SetColumn(exportCfg, 1);
            cfgRow.Children.Add(exportCfg);
            var importCfg = new Button { Content = "导入配置", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), Style = UiUtil.ButtonStyle() };
            importCfg.Click += (s, e) => ImportConfig();
            Grid.SetColumn(importCfg, 2);
            cfgRow.Children.Add(importCfg);
            var exportLog = new Button { Content = "导出日志", MinWidth = 90, Style = UiUtil.ButtonStyle() };
            exportLog.Click += (s, e) => ExportLog();
            Grid.SetColumn(exportLog, 3);
            cfgRow.Children.Add(exportLog);
            var cfgHint = UiUtil.MakeText("配置包含 IP.txt 与设置, 便于多机部署", 11.5, muted: true);
            cfgHint.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(cfgHint, 4);
            cfgRow.Children.Add(cfgHint);
            stack.Children.Add(cfgRow);

            return UiUtil.Card("IP.txt 与本机", stack);
        }

        private UIElement BuildLogCard()
        {
            _logViewer = new ListBox
            {
                Height = 130,
                Style = UiUtil.LogListStyle(),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            AddInfo("这里是输出台！所有命令的输出都会显示在这里。");
            var menu = new ContextMenu();
            var clear = new MenuItem { Header = "清空日志" };
            clear.Click += (s, e) => ClearLog();
            var save = new MenuItem { Header = "导出日志…" };
            save.Click += (s, e) => ExportLog();
            menu.Items.Add(clear);
            menu.Items.Add(save);
            _logViewer.ContextMenu = menu;

            var card = UiUtil.Card(null, _logViewer);
            card.Margin = new Thickness(0, 0, 0, 12);
            return card;
        }

        // ------------------------------------------------------------------ status bar

        private UIElement BuildStatusBar()
        {
            var bar = new Border
            {
                Background = UiUtil.C.Sidebar,
                BorderBrush = UiUtil.C.SidebarLine,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(12, 4, 12, 4),
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string localIp = "未知";
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        localIp = ip.ToString();
                        break;
                    }
                }
            }
            catch { }
            g.Children.Add(UiUtil.MakeText("本机IP: " + localIp, 12));
            _ipStatus = UiUtil.MakeText("", 12, muted: true);
            _ipStatus.Margin = new Thickness(18, 0, 0, 0);
            Grid.SetColumn(_ipStatus, 1);
            g.Children.Add(_ipStatus);
            _adminStatus = UiUtil.MakeText("", 12, muted: true);
            _adminStatus.Margin = new Thickness(18, 0, 0, 0);
            Grid.SetColumn(_adminStatus, 2);
            g.Children.Add(_adminStatus);
            var ver = UiUtil.MakeText("JiYu Killer v1.0.0", 12, muted: true);
            ver.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(ver, 4);
            g.Children.Add(ver);
            bar.Child = g;
            return bar;
        }

        // ------------------------------------------------------------------ theme / elevation

        private void CloseOwnedWindows()
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (!ReferenceEquals(w, this) && w.Owner == this)
                    w.Close();
            }
        }

        private void ToggleTheme()
        {
            Settings.Theme = Settings.Theme == "dark" ? "light" : "dark";
            Settings.Save();
            // 重建窗口以应用新主题（保留位置/尺寸）
            var next = new MainWindow();
            if (WindowState == WindowState.Normal)
            {
                next.Left = Left;
                next.Top = Top;
                next.Width = Width;
                next.Height = Height;
                next.WindowStartupLocation = WindowStartupLocation.Manual;
            }
            CloseOwnedWindows();
            Application.Current.MainWindow = next;
            next.Show();
            Close();
        }

        private void Elevate()
        {
            if (MessageBox.Show("将以管理员身份重新启动本程序（会弹出 UAC 提示）。\n\n确定要继续吗？",
                    "需要管理员权限", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            if (LocalOps.Elevate())
                Close();
            else
                MessageBox.Show("提权被取消，或系统拒绝了请求。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ------------------------------------------------------------------ actions

        private SendOptions ReadSendOptions()
        {
            var opt = new SendOptions();
            int v;
            if (int.TryParse((_delayBox.Text ?? "").Trim(), out v) && v > 0) opt.DelaySeconds = v;
            if (int.TryParse((_roundsBox.Text ?? "").Trim(), out v) && v > 0) opt.Rounds = v;
            if (int.TryParse((_intervalBox.Text ?? "").Trim(), out v) && v > 0) opt.IntervalSeconds = v;
            return opt;
        }

        private BossKeyEngine _bossKey;

        /// <summary>懒加载的窗口隐藏引擎（挂在主窗口上, 生命周期与主窗口一致）。</summary>
        public BossKeyEngine GetBossKeyEngine()
        {
            if (_bossKey == null)
                _bossKey = new BossKeyEngine(this, AddInfo);
            return _bossKey;
        }

        private void AddInfo(string text)
        {
            if (_closed) return;   // 窗口已关闭(主题切换/导入重建)后的异步回调直接丢弃
            string time = DateTime.Now.ToString("HH:mm:ss");
            Dispatcher.Invoke(() =>
            {
                var item = new ListBoxItem { Content = time + " [Info] " + text, Padding = new Thickness(4, 2, 4, 2) };
                if (text.StartsWith("[错误]") || text.StartsWith("错误") || text.StartsWith("已中止"))
                    item.Foreground = UiUtil.Brush("#FF6B6E");
                else if (text.StartsWith("已发送") || text.Contains("发送完成") || text.Contains("生成成功")
                         || text.StartsWith("扫描完成") || text.Contains("任务已完成"))
                    item.Foreground = UiUtil.Brush("#6BCB77");
                else if (text.StartsWith("[跳过]") || text.StartsWith("IP.txt") || text.StartsWith("扫描中"))
                    item.Foreground = UiUtil.Brush("#E5C07B");
                else
                    item.Foreground = UiUtil.Brush("#C8CCD4");
                _logViewer.Items.Add(item);
                _logViewer.ScrollIntoView(_logViewer.Items[_logViewer.Items.Count - 1]);
            });
        }

        private void RefreshIpStatus()
        {
            int count = JySender.ReadTargets().Count;
            Dispatcher.Invoke(() =>
            {
                _ipStatus.Text = JySender.UseGlobalBroadcast
                    ? "目标: 全局广播 " + JySender.MulticastGroup
                    : "IP.txt: " + (count > 0 ? count + " 个目标" : "未配置");
                if (_adminStatus != null)
                    _adminStatus.Text = LocalOps.IsAdmin() ? "权限: 管理员" : "权限: 普通用户";
            });
        }

        private void SetBusy(bool busy)
        {
            if (_closed) return;   // 同上
            _busy = busy;
            Dispatcher.Invoke(() =>
            {
                _sendMsgBtn.IsEnabled = !busy;
                _commandBtn.IsEnabled = !busy;
                _webBtn.IsEnabled = !busy;
                _generateBtn.IsEnabled = !busy;
                _closeAllBtn.IsEnabled = !busy;
                _stopBtn.IsEnabled = busy;
            });
        }

        private void StopSending()
        {
            JySender.CancelAll();
            try
            {
                var cts = _scanCts;
                if (cts != null)
                    cts.Cancel();
            }
            catch (ObjectDisposedException) { }   // 扫描恰好刚结束
            AddInfo("已请求停止。");
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _busy)
            {
                StopSending();
                e.Handled = true;
            }
        }

        private void CommandHistoryKey(object sender, KeyEventArgs e)
        {
            if (Settings.History.Count == 0) return;
            if (e.Key == Key.Up)
            {
                if (_historyIndex < 0) _historyIndex = Settings.History.Count;
                if (_historyIndex > 0) _historyIndex--;
                _commandLine.Text = Settings.History[_historyIndex];
                _commandLine.CaretIndex = _commandLine.Text.Length;
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (_historyIndex >= 0 && _historyIndex < Settings.History.Count - 1)
                {
                    _historyIndex++;
                    _commandLine.Text = Settings.History[_historyIndex];
                }
                else
                {
                    _historyIndex = -1;
                    _commandLine.Text = "";
                }
                _commandLine.CaretIndex = _commandLine.Text.Length;
                e.Handled = true;
            }
        }

        private void FillLocalSubnet()
        {
            string cidr = NetScan.LocalCidr();
            if (cidr == null)
            {
                AddInfo("未找到可用的本机 IPv4 地址。");
                return;
            }
            _ipLine.Text = cidr;
        }

        private async void ScanSubnet()
        {
            string cidr = NetScan.LocalCidr();
            if (cidr == null)
            {
                AddInfo("未找到可用的本机 IPv4 地址，无法确定网段。");
                return;
            }
            _scanBtn.IsEnabled = false;
            _scanCts = new CancellationTokenSource();
            AddInfo("开始扫描 " + cidr + " (仅本机网段)…");
            try
            {
                var alive = await NetScan.ScanAsync(cidr, AddInfo, _scanCts.Token);
                _lastScan = alive;
                AddInfo("扫描完成: " + cidr + " 存活 " + alive.Count + " 台" + (alive.Count > 0 ? " -> " + string.Join(", ", alive) : ""));
            }
            catch (Exception ex)
            {
                AddInfo("[错误] 扫描失败: " + ex.Message);
            }
            finally
            {
                _scanBtn.IsEnabled = true;
                _scanCts.Dispose();
                _scanCts = null;
            }
        }

        private List<string> _lastScan;

        private void WriteScanResult()
        {
            if (_lastScan == null || _lastScan.Count == 0)
            {
                AddInfo("还没有扫描结果，请先点\"扫描本机网段\"。");
                return;
            }
            System.IO.File.WriteAllLines(JySender.IpFilePath, _lastScan);
            AddInfo("已将 " + _lastScan.Count + " 台存活主机写入 IP.txt。");
            RefreshIpStatus();
        }

        private void SendClick(Func<Func<IPAddress, byte[]>> builderFactory)
        {
            if (_busy) return;
            Func<IPAddress, byte[]> builder;
            try
            {
                builder = builderFactory();
            }
            catch (FormatException ex)
            {
                AddInfo("错误: " + ex.Message);
                MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (builder == null)
            {
                AddInfo("请先填写内容。");
                return;
            }
            SetBusy(true);
            AddInfo("正在发送命令, 请稍候...");
            JySender.Broadcast(builder, AddInfo,
                onDone: () => SetBusy(false),
                onError: ex => { AddInfo("[错误] " + ex.Message); SetBusy(false); },
                options: ReadSendOptions());
        }

        private void LaunchClick()
        {
            if (_busy) return;
            int idx = _launchComboBox.SelectedIndex;
            if (idx < 0 || idx >= JyPackets.LaunchPresets.Length) return;
            string path = JyPackets.LaunchPresets[idx];
            string name = JyPackets.LaunchPresetNames[idx];
            SetBusy(true);
            AddInfo("正在启动 " + name + " (" + path + ")…");
            JySender.Broadcast(target => JyPackets.BuildLaunch(target, path), AddInfo,
                onDone: () => SetBusy(false),
                onError: ex => { AddInfo("[错误] " + ex.Message); SetBusy(false); },
                options: ReadSendOptions());
        }

        private void BasicClick()
        {
            if (_busy) return;
            var op = (JyPackets.BasicOp)_orderComboBox.SelectedIndex;
            SetBusy(true);
            AddInfo("正在发送命令, 请稍候...");
            JySender.Broadcast(target => JyPackets.BuildBasic(op, target), AddInfo,
                onDone: () => SetBusy(false),
                onError: ex => { AddInfo("[错误] " + ex.Message); SetBusy(false); },
                options: ReadSendOptions());
        }

        private bool ConfirmAdmin(string text)
        {
            if (LocalOps.IsAdmin())
                return MessageBox.Show(text, "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            MessageBox.Show("此功能需要管理员权限。可在左侧点\"以管理员身份重启\"。\n\n" + text,
                "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return false;
        }

        private void CloseLocalJy()
        {
            if (!ConfirmAdmin("贸然关闭极域可能会导致你被老师骂一顿。\n\n确定要继续吗?"))
                return;
            SetBusy(true);
            AddInfo("正在执行本机命令, 请稍候...");
            Task.Run(() =>
            {
                try
                {
                    string status = LocalOps.CloseJy();
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(status, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        AddInfo("关掉此电脑上的极域: 任务已完成。");
                    });
                }
                catch (Exception ex) { AddInfo("[错误] " + ex.Message); }
                SetBusy(false);
            });
        }

        private void ClearWebControl()
        {
            if (!ConfirmAdmin("贸然关闭网络与U盘控制可能会导致你被老师骂一顿。\n\n确定要继续吗?"))
                return;
            SetBusy(true);
            AddInfo("正在执行, 请稍候...");
            // 2021(4705) 用翘课3.0 的 DMOC 帧式解锁包, 其余版本用原 43 字节包
            JySender.Broadcast(target => JyPackets.BuildUnlockForPort(target, JyVersion.Port), AddInfo,
                onDone: () =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            string status = LocalOps.ClearWebControl();
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(status, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                                AddInfo("一键解除U盘和网络限制: 任务已完成。");
                            });
                        }
                        catch (Exception ex) { AddInfo("[错误] " + ex.Message); }
                        SetBusy(false);
                    });
                },
                onError: ex => { AddInfo("[错误] " + ex.Message); SetBusy(false); });
        }

        /// <summary>读取本机极域存储的真实设置密码（knock1 解密）。</summary>
        private void ReadLocalPassword()
        {
            string pwd = JyPassword.TryReadLocalPassword();
            if (pwd == null)
            {
                AddInfo("未读到本机极域密码 (可能未安装极域、密码项不存在或需要管理员权限)。");
                MessageBox.Show("未读到本机极域密码。\n可能原因: 未安装极域 / 密码项不存在 / 需要管理员权限。\n\n可以先试试万能密码: " + JyPassword.SuperPassword,
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            AddInfo("本机极域密码: " + pwd);
            MessageBox.Show("本机极域设置密码:\n\n" + pwd + "\n\n(来源: 注册表 knock1 解密, 仅用于找回本机密码)",
                "读取成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>挂起/恢复本机极域进程（NtSuspendProcess/NtResumeProcess）。</summary>
        private void SuspendJy(bool suspend)
        {
            if (suspend && !ConfirmAdmin("挂起极域后老师端的广播与控制会立即失效。\n\n确定要继续吗?"))
                return;
            SetBusy(true);
            string what = suspend ? "挂起" : "恢复";
            AddInfo("正在" + what + "极域进程…");
            Task.Run(() =>
            {
                try
                {
                    string status = LocalOps.SetJySuspended(suspend);
                    Dispatcher.Invoke(() =>
                    {
                        AddInfo(what + "完成: " + status);
                        MessageBox.Show(status + (suspend ? "\n\n下课后记得点\"恢复极域\"。" : ""),
                            "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception ex) { AddInfo("[错误] " + ex.Message); }
                SetBusy(false);
            });
        }

        /// <summary>发送翘课3.0 的 op=6 指令包（实验功能, 作用待真机验证）。</summary>
        private void SendOp6()
        {
            if (_busy) return;
            var w = MessageBox.Show(
                "即将按当前\"发送目标\"发送翘课3.0 内置的 op=6 指令包 (177 字节)。\n"
                + "该指令的具体作用尚未考证 (疑为锁屏/窗口锁定类), 未在真机验证前请当作实验功能。\n\n确定要继续吗？",
                "实验功能", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (w != MessageBoxResult.Yes) return;
            SetBusy(true);
            AddInfo("正在发送 op=6 指令包 (翘课3.0 同款)…");
            JySender.Broadcast(JyPackets.BuildOp6, AddInfo,
                onDone: () => SetBusy(false),
                onError: ex => { AddInfo("[错误] " + ex.Message); SetBusy(false); });
        }

        private void RestoreLocal()
        {            if (!ConfirmAdmin("将尝试恢复本机状态：重启 tdnetfilter 服务、确保桌面进程在运行。\n\n确定要继续吗?"))
                return;
            SetBusy(true);
            AddInfo("正在恢复本机状态…");
            Task.Run(() =>
            {
                try
                {
                    string status = LocalOps.RestoreJy();
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(status, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        AddInfo("一键恢复: 任务已完成。");
                    });
                }
                catch (Exception ex) { AddInfo("[错误] " + ex.Message); }
                SetBusy(false);
            });
        }

        private void CloseAllJyConnectFlow()
        {
            if (_busy) return;
            var w1 = MessageBox.Show(
                "警告! 此操作不可逆! \n\n即将向全局广播地址 " + JySender.MulticastGroup + " 发送 /h taskkill /f /im studentmain.exe。\n"
                + JySender.MulticastGroup + " 是全局广播地址: 同一网段内所有运行极域的电脑（包括教师机）都会收到并关闭极域主进程。\n\n确定要继续吗？",
                "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (w1 != MessageBoxResult.Yes) return;

            var w2 = MessageBox.Show(
                "这是最后一次警告! 此操作不可逆!\n\n指令一旦发出, 网段内所有极域客户端都会被关闭, 只能手动重新打开。\n\n真的确定要继续吗？",
                "最后的警告!", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (w2 != MessageBoxResult.Yes) return;

            SetBusy(true);
            AddInfo("正在向全局广播地址 " + JySender.MulticastGroup + " 发送隐藏关闭指令…");
            JySender.SendKill(AddInfo,
                onDone: () => SetBusy(false),
                onError: ex => { AddInfo("[错误] " + ex.Message); SetBusy(false); });
        }

        private void GenerateHosts()
        {
            try
            {
                var hosts = IpGenerator.GenerateHosts(_ipLine.Text);
                System.IO.File.WriteAllLines(JySender.IpFilePath, hosts);
                AddInfo("生成成功! (" + hosts.Count + " 个IP 已写入 IP.txt)");
                RefreshIpStatus();
            }
            catch (Exception ex)
            {
                AddInfo("错误: " + ex.Message);
                MessageBox.Show("错误: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ------------------------------------------------------------------ files

        private void ExportConfig()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JiYu Killer 配置包 (*.jyk)|*.jyk|所有文件 (*.*)|*.*",
                FileName = "jiyu-config.jyk",
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                ConfigBundle.Export(dlg.FileName);
                AddInfo("配置已导出: " + dlg.FileName);
            }
            catch (Exception ex) { AddInfo("[错误] 导出失败: " + ex.Message); }
        }

        private void ImportConfig()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JiYu Killer 配置包 (*.jyk)|*.jyk|所有文件 (*.*)|*.*",
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                int n = ConfigBundle.Import(dlg.FileName);
                // 导入会改变版本/目标/主题, 重建窗口以全部生效
                AddInfo("配置已导入 (" + n + " 个 IP)。");
                CloseOwnedWindows();
                var next = new MainWindow();
                if (WindowState == WindowState.Normal)
                {
                    next.Left = Left; next.Top = Top; next.Width = Width; next.Height = Height;
                    next.WindowStartupLocation = WindowStartupLocation.Manual;
                }
                Application.Current.MainWindow = next;
                next.Show();
                Close();
            }
            catch (Exception ex)
            {
                AddInfo("[错误] 导入失败: " + ex.Message);
                MessageBox.Show("导入失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportLog()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = "jiyu-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt",
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var lines = new List<string>();
                foreach (var item in _logViewer.Items)
                {
                    var lbi = item as ListBoxItem;
                    lines.Add(lbi != null ? Convert.ToString(lbi.Content) : Convert.ToString(item));
                }
                System.IO.File.WriteAllLines(dlg.FileName, lines);
                AddInfo("日志已导出: " + dlg.FileName);
            }
            catch (Exception ex) { AddInfo("[错误] 导出日志失败: " + ex.Message); }
        }

        private void ClearLog()
        {
            _logViewer.Items.Clear();
            AddInfo("日志已清空。");
        }

        private void ShowHelp()
        {
            MessageBox.Show(
                "远程功能按顶部\"发送目标\"发送:\n"
                + "  · IP.txt 中的全部 IP —— 每行一个 ip, 见\"IP.txt 与本机\"页;\n"
                + "  · 全局广播 " + JySender.MulticastGroup + " —— 无需 IP.txt。\n\n"
                + "快捷启动: 远程打开计算器/画图/任务管理器等（走已验证的程序启动报文, 仅换路径）。\n"
                + "\"延时/重复\": 延时 N 秒后开始, 重复 M 次, 每轮间隔 S 秒; 发送中可点\"紧急停止\"或按 Esc。\n\n"
                + "\"关闭极域\"页固定向 " + JySender.MulticastGroup + " 发送 /h taskkill /f /im studentmain.exe (/h = 隐藏命令窗口)。\n"
                + "系统命令默认显示命令窗口; 在命令前加 /h 可隐藏命令窗口, 命令框按 ↑/↓ 可调历史。\n\n"
                + "局域网扫描仅扫描本机所在 /24 网段。",
                "使用说明", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>静默关于窗口（MessageBox 会播放系统提示音, 按需求改为无声）。</summary>
        private void ShowAbout()
        {
            var w = new Window
            {
                Title = "关于",
                Width = 520,
                Height = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                FontFamily = UiUtil.AppFont,
                Background = UiUtil.C.Window,
                Icon = UiUtil.LoadAppIcon(),
                ResizeMode = ResizeMode.NoResize,
            };

            var root = new StackPanel { Margin = new Thickness(24, 20, 24, 16) };

            root.Children.Add(new TextBlock
            {
                Text = "JiYu Killer  v1.0.0",
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = UiUtil.C.Text,
            });

            root.Children.Add(new TextBlock
            {
                Text = "开源地址:",
                FontSize = 13,
                Margin = new Thickness(0, 12, 0, 2),
                Foreground = UiUtil.C.Text2,
            });
            var link = new TextBlock { FontSize = 13 };
            var hyperlink = new Hyperlink(new Run("https://github.com/lynvortex/JiYu-Killer"))
            {
                Foreground = UiUtil.Brush("#4A9EFF"),
                TextDecorations = null,
            };
            hyperlink.Click += (s2, e2) =>
            {
                try { Process.Start("https://github.com/lynvortex/JiYu-Killer"); }
                catch (Exception ex) { AddInfo("[错误] 打开链接失败: " + ex.Message); }
            };
            link.Inlines.Add(hyperlink);
            root.Children.Add(link);

            root.Children.Add(new TextBlock
            {
                Text = "运行环境: .NET Framework 4.8 / WPF",
                FontSize = 13,
                Margin = new Thickness(0, 12, 0, 0),
                Foreground = UiUtil.C.Text2,
            });

            root.Children.Add(new TextBlock
            {
                Text = "(若机房改过密码, 可在\"IP.txt 与本机\"页读取本机存储的真实密码)",
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0),
                Foreground = UiUtil.C.Muted,
                TextWrapping = TextWrapping.Wrap,
            });

            root.Children.Add(new TextBlock
            {
                Text = "本工具仅用于学习研究与合法的整蛊场景, 请勿用于其他用途。\n使用所造成的一切后果由使用者自行承担。",
                FontSize = 12,
                LineHeight = 19,
                Margin = new Thickness(0, 14, 0, 0),
                Foreground = UiUtil.C.Muted,
                TextWrapping = TextWrapping.Wrap,
            });

            var ok = new Button
            {
                Content = "确定",
                Width = 110,
                Height = 30,
                Style = UiUtil.PrimaryButtonStyle(),
                Margin = new Thickness(0, 18, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            ok.Click += (s2, e2) => w.Close();
            root.Children.Add(ok);

            w.Content = root;
            w.ShowDialog();
        }

        // ---- Alt+C 隐身热键 (切换主窗口显示/隐藏) ----
        private const int WmHotkeySelf = 0x0312;
        private const int HkSelfId = 4;
        private bool _selfHidden;

        private void SetupTrayAndSelfHotkey()
        {
            var src = (HwndSource)HwndSource.FromVisual(this);
            src.AddHook(WndProcSelfHotkey);
            NativeMethods.RegisterHotKey(src.Handle, HkSelfId, 0x1 | 0x4000, 0x43);   // Alt+C
            _tray = new TrayIcon(this);
            _tray.DoubleClick += () =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            };
            _tray.Show();
        }

        private IntPtr WndProcSelfHotkey(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkeySelf && wParam.ToInt32() == HkSelfId)
            {
                _selfHidden = !_selfHidden;
                Visibility = _selfHidden ? Visibility.Hidden : Visibility.Visible;
                if (!_selfHidden) { WindowState = WindowState.Normal; Activate(); }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
        }

        private static class DwmHelper
        {
            [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
            public static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        }
    }
}
