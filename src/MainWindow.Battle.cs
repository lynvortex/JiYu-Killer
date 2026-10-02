using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using JiYuKiller.Core;

namespace JiYuKiller.UI
{
    /// <summary>
    /// 主窗口的"进程与对抗"页（partial）：
    /// 状态面板、多级杀进程链、广播窗口化、注册表解锁、防火墙阻断、路径识别、
    /// 左上角热区开关、targets.txt 名单编辑。
    /// 技术来源：UnMythware V4.5（多级杀/状态面板）、再见极域（解锁清单/左上角热区）、
    /// jiyu_windowing（广播窗口化）、极域Tool（Job Object/路径识别）。
    /// </summary>
    internal partial class MainWindow
    {
        private UIElement BuildBattlePanel()
        {
            var stack = new StackPanel();

            // 状态面板 (实时)
            var statusCard = UiUtil.Card("极域状态 (每秒刷新)", null);
            var inner = (StackPanel)statusCard.Child;
            _statusPanel = new TextBlock
            {
                FontSize = 12.5,
                LineHeight = 20,
                Foreground = UiUtil.C.Text2,
            };
            inner.Children.Add(_statusPanel);
            stack.Children.Add(statusCard);

            // 多级杀进程链
            var killRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            killRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            killRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            killRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            killRow.Children.Add(UiUtil.MakeLabel("多级杀进程链"));
            var killBtn = new Button { Content = "执行杀链", MinWidth = 100, Margin = new Thickness(0, 0, 10, 0), Style = UiUtil.PrimaryButtonStyle() };
            killBtn.Click += (s, e) => RunKillChain();
            Grid.SetColumn(killBtn, 1);
            killRow.Children.Add(killBtn);
            var killHint = UiUtil.MakeText("taskkill → SeDebug → 窗口消息 → 调试器附加 → Job Object, 自动降级", 11.5, muted: true);
            killHint.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(killHint, 2);
            killRow.Children.Add(killHint);
            stack.Children.Add(killRow);

            // 广播窗口化
            var winRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            winRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            winRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            winRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            winRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            winRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            winRow.Children.Add(UiUtil.MakeLabel("广播窗口化"));
            var win1 = new Button { Content = "窗口化广播", MinWidth = 110, Margin = new Thickness(0, 0, 8, 0), Style = UiUtil.ButtonStyle() };
            win1.Click += (s, e) => AddInfo(BroadcastWindow.Windowize("render"));
            Grid.SetColumn(win1, 1);
            winRow.Children.Add(win1);
            var win2 = new Button { Content = "还原广播", MinWidth = 100, Margin = new Thickness(0, 0, 8, 0), Style = UiUtil.ButtonStyle() };
            win2.Click += (s, e) => AddInfo(BroadcastWindow.Restore("render"));
            Grid.SetColumn(win2, 2);
            winRow.Children.Add(win2);
            var win3 = new Button { Content = "窗口化黑屏", MinWidth = 110, Style = UiUtil.ButtonStyle() };
            win3.Click += (s, e) => AddInfo(BroadcastWindow.Windowize("black"));
            Grid.SetColumn(win3, 3);
            winRow.Children.Add(win3);
            var winHint = UiUtil.MakeText("TDDesk Render Window / BlackScreen Window", 11.5, muted: true);
            winHint.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(winHint, 4);
            winRow.Children.Add(winHint);
            stack.Children.Add(winRow);

            // 注册表解锁 + 防火墙阻断
            var unlockRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            unlockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            unlockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            unlockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            unlockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            unlockRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            unlockRow.Children.Add(UiUtil.MakeLabel("注册表解锁"));
            var unlockBtn = new Button { Content = "一键解禁", MinWidth = 100, Margin = new Thickness(0, 0, 10, 0), Style = UiUtil.ButtonStyle() };
            unlockBtn.Click += (s, e) => RunRegistryUnlock();
            Grid.SetColumn(unlockBtn, 1);
            unlockRow.Children.Add(unlockBtn);
            var fwBlock = new Button { Content = "防火墙阻断极域", MinWidth = 130, Margin = new Thickness(0, 0, 10, 0), Style = UiUtil.ButtonStyle() };
            fwBlock.Click += (s, e) => FirewallBlock(true);
            Grid.SetColumn(fwBlock, 2);
            unlockRow.Children.Add(fwBlock);
            var fwAllow = new Button { Content = "恢复联网", MinWidth = 100, Style = UiUtil.ButtonStyle() };
            fwAllow.Click += (s, e) => FirewallBlock(false);
            Grid.SetColumn(fwAllow, 3);
            unlockRow.Children.Add(fwAllow);
            var unlockHint = UiUtil.MakeText("cmd / 注册表 / 任务管理器 / Win+R / taskkill IFEO / 注销 / 键盘锁", 11.5, muted: true);
            unlockHint.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(unlockHint, 4);
            unlockRow.Children.Add(unlockHint);
            stack.Children.Add(unlockRow);

            // 路径识别 + 左上角热区
            var miscRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            miscRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            miscRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            miscRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            miscRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            miscRow.Children.Add(UiUtil.MakeLabel("识别/热区"));
            var pathBtn = new Button { Content = "识别极域路径", MinWidth = 110, Margin = new Thickness(0, 0, 10, 0), Style = UiUtil.ButtonStyle() };
            pathBtn.Click += (s, e) =>
            {
                string path = JyLocator.DetectInstallPath();
                AddInfo(path == null ? "未识别到极域安装路径 (三级识别均失败)。" : "极域安装路径: " + path);
            };
            Grid.SetColumn(pathBtn, 1);
            miscRow.Children.Add(pathBtn);
            _cornerToggle = new CheckBox
            {
                Content = "左上角热区",
                FontSize = 13,
                Foreground = UiUtil.C.Text2,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
            };
            _cornerToggle.Checked += (s, e) => { EnsureHotzone().Start(); AddInfo("左上角热区已启用。"); };
            _cornerToggle.Unchecked += (s, e) => { if (_hotzone != null) _hotzone.Stop(); AddInfo("左上角热区已停用。"); };
            Grid.SetColumn(_cornerToggle, 2);
            miscRow.Children.Add(_cornerToggle);
            var miscHint = UiUtil.MakeText("广播时鼠标移到屏幕左上角即弹询问 (最小化广播)", 11.5, muted: true);
            miscHint.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(miscHint, 3);
            miscRow.Children.Add(miscHint);
            stack.Children.Add(miscRow);

            // targets.txt 名单
            var listRow = new Grid();
            listRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            listRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            listRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            listRow.Children.Add(UiUtil.MakeLabel("进程名单"));
            var listBtn = new Button { Content = "编辑 targets.txt", MinWidth = 130, Style = UiUtil.ButtonStyle() };
            listBtn.Click += (s, e) =>
            {
                JyTargets.Load();   // 确保文件存在
                try { Process.Start("notepad.exe", JyTargets.FilePath); } catch { }
            };
            Grid.SetColumn(listBtn, 1);
            listRow.Children.Add(listBtn);
            var listHint = UiUtil.MakeText("杀链/挂起的目标进程名, 每行一个, 支持 # 注释", 11.5, muted: true);
            listHint.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(listHint, 3);
            listRow.Children.Add(listHint);
            stack.Children.Add(listRow);

            // 状态轮询
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statusTimer.Tick += (s, e) => RefreshBattleStatus();
            _statusTimer.Start();
            RefreshBattleStatus();

            return UiUtil.Card("进程与对抗", stack);
        }

        private void RefreshBattleStatus()
        {
            if (_statusPanel == null) return;
            var st = JyLocator.Probe(LocalOps.JyFrozen);
            string broadcast = st.Broadcasting ? "是" : (st.BlackScreen ? "黑屏中" : "否");
            _statusPanel.Text =
                "极域: " + (st.Running ? "运行中" : "未运行") + "   PID: " + (st.Running ? st.Pid.ToString() : "-")
                + "   版本: " + st.Version
                + Environment.NewLine
                + "广播中: " + broadcast
                + "   已冻结: " + (LocalOps.JyFrozen ? "是" : "否")
                + Environment.NewLine
                + "路径: " + st.Path;
        }

        /// <summary>多级杀进程链（对 targets.txt 名单全部执行, 自动降级）。</summary>
        private void RunKillChain()
        {
            if (!ConfirmAdmin("多级杀进程链会按 targets.txt 名单强制终止进程," + Environment.NewLine
                + "依次尝试 taskkill → SeDebug → 窗口消息 → 调试器附加 → Job Object。" + Environment.NewLine
                + Environment.NewLine + "确定要继续吗?"))
                return;
            SetBusy(true);
            AddInfo("正在执行多级杀进程链…");
            Task.Run(() =>
            {
                try
                {
                    string report = ProcessChain.KillAllFromTargets();
                    Dispatcher.Invoke(() =>
                    {
                        foreach (string line in report.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                            AddInfo(line.TrimEnd());
                        MessageBox.Show(report, "杀链报告", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception ex) { AddInfo("[错误] " + ex.Message); }
                SetBusy(false);
            });
        }

        /// <summary>注册表策略解锁（再见极域清单）。</summary>
        private void RunRegistryUnlock()
        {
            if (!ConfirmAdmin("将删除极域写入的系统策略键:" + Environment.NewLine
                + "cmd / 注册表编辑器 / 任务管理器 / Win+R / 注销 / 键盘锁 / taskkill IFEO 劫持。" + Environment.NewLine
                + "若这些策略是学校有意设置的安全基线, 会降低机器安全性。" + Environment.NewLine
                + Environment.NewLine + "确定要继续吗?"))
                return;
            SetBusy(true);
            Task.Run(() =>
            {
                try
                {
                    string report = RegistryUnlock.UnlockAll();
                    Dispatcher.Invoke(() =>
                    {
                        foreach (string line in report.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                            AddInfo(line);
                        MessageBox.Show(report, "解锁报告", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception ex) { AddInfo("[错误] " + ex.Message); }
                SetBusy(false);
            });
        }

        /// <summary>防火墙阻断/恢复极域联网（可逆断控, 学习 jiyu_windowing 套件）。</summary>
        private void FirewallBlock(bool block)
        {
            if (!ConfirmAdmin((block
                ? "将用 Windows 防火墙阻断极域 StudentMain.exe 的全部网络连接," + Environment.NewLine + "教师端将完全失去对这台机器的控制。"
                : "将恢复极域 StudentMain.exe 的网络连接。")
                + Environment.NewLine + "该操作可逆, 但会真实改写防火墙规则。" + Environment.NewLine
                + Environment.NewLine + "确定要继续吗?"))
                return;
            SetBusy(true);
            AddInfo(block ? "正在阻断极域联网…" : "正在恢复极域联网…");
            Task.Run(() =>
            {
                try
                {
                    string action = block ? "block" : "allow";
                    int rc = LocalOps.RunCmd("netsh advfirewall firewall set rule name=\"StudentMain.exe\" new action=" + action);
                    if (rc != 0)
                        rc = LocalOps.RunCmd("netsh advfirewall firewall add rule name=\"StudentMain.exe\" dir=out action=" + action + " program=\"StudentMain.exe\"");
                    Dispatcher.Invoke(() => AddInfo((block ? "防火墙阻断" : "恢复联网") + (rc == 0 ? "成功。" : "命令返回 " + rc + " (可能需要管理员权限)。")));
                }
                catch (Exception ex) { AddInfo("[错误] " + ex.Message); }
                SetBusy(false);
            });
        }

        private CornerHotzone EnsureHotzone()
        {
            if (_hotzone == null)
            {
                _hotzone = new CornerHotzone();
                _hotzone.Triggered += broadcasting =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show(broadcasting
                                ? "检测到鼠标进入左上角, 且当前正在屏幕广播。" + Environment.NewLine + "要缩小广播窗口吗?"
                                : "检测到鼠标进入左上角热区。" + Environment.NewLine + "没有检测到广播窗口, 仍要缩小广播窗口吗?",
                            "左上角热区", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            AddInfo(BroadcastWindow.MinimizeBroadcast());
                        }
                    });
                };
            }
            return _hotzone;
        }
    }
}
