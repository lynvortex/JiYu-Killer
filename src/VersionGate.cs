using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using JiYuKiller.Core;

namespace JiYuKiller.UI
{
    /// <summary>
    /// 极域版本确认对话框：自动检测成功时只展示结果；
    /// 检测不到时让用户手选，并列出可能的极域软件位置。
    /// </summary>
    internal static class VersionGate
    {
        /// <summary>极域官网（下载/了解极域课堂）。</summary>
        public const string MythwareSite = "https://www.mythware.com/";

        /// <summary>
        /// 极域常见安装位置（完整 StudentMain.exe 路径 + 版本注释）。
        /// 来源: 公开资料与各反控工具的内置路径 (Program Files 与 x86 两个变体)。
        /// </summary>
        private static readonly string[][] KnownPaths =
        {
            new[] { @"C:\Program Files\Mythware\e-Learning Class\StudentMain.exe", "2010版 V4" },
            new[] { @"C:\Program Files (x86)\Mythware\e-Learning Class\StudentMain.exe", "2010版 V4" },
            new[] { @"C:\Program Files\Mythware\极域课堂管理系统软件V6.0 2016 豪华版\StudentMain.exe", "2016版" },
            new[] { @"C:\Program Files (x86)\Mythware\极域课堂管理系统软件V6.0 2016 豪华版\StudentMain.exe", "2016版" },
            new[] { @"C:\Program Files\Mythware\极域课堂管理系统软件v6.0 2021豪华版\StudentMain.exe", "2021版" },
            new[] { @"C:\Program Files (x86)\Mythware\极域课堂管理系统软件v6.0 2021豪华版\StudentMain.exe", "2021版" },
        };

        /// <summary>
        /// 构建位置列表: 已识别路径 + 常见路径(标注本机是否存在) + 本机 Mythware
        /// 根目录下实际存在的子目录。全部为完整真实路径, 可复制。
        /// </summary>
        private static string BuildLocationList(string detectedPath)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(detectedPath))
                lines.Add("[本机检测到] " + detectedPath);

            foreach (var k in KnownPaths)
            {
                bool exists = File.Exists(k[0]);
                lines.Add((exists ? "[存在]   " : "[未发现] ") + k[0] + "  (" + k[1] + ")");
            }

            string[] roots =
            {
                @"C:\Program Files\Mythware",
                @"C:\Program Files (x86)\Mythware",
                @"D:\Mythware",
                @"D:\Program Files\Mythware",
                @"D:\Program Files (x86)\Mythware",
            };
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (string dir in Directory.GetDirectories(root))
                    {
                        if (lines.Any(l => l.Contains(dir))) continue;
                        bool has = File.Exists(Path.Combine(dir, "StudentMain.exe"));
                        lines.Add((has ? "[存在]   " : "[未发现] ") + dir);
                    }
                }
                catch { }
            }

            if (!lines.Any(l => l.StartsWith("[存在]") || l.StartsWith("[本机检测到]")))
                lines.Add("以上路径在本机均不存在 —— 极域可能安装在自定义位置或未安装。");
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// 尝试自动检测版本。成功 -> 写入设置并返回描述；失败 -> 弹手动选择框，
        /// 返回用户是否完成了确认（cancelled = false 但仍未确认时也返回 false）。
        /// </summary>
        public static bool EnsureConfirmed(Window owner, Action<string> log)
        {
            if (Settings.VersionConfirmed)
                return true;

            int detected = JyLocator.DetectVersionIndex();
            if (detected >= 0)
            {
                JyVersion.SetSelected(detected);
                Settings.VersionIndex = detected;
                Settings.VersionConfirmed = true;
                Settings.Save();
                log?.Invoke("已自动识别极域版本: " + JyVersion.Names[detected]
                    + " (端口 " + JyVersion.Port + ")");
                return true;
            }

            return ShowManualDialog(owner, log);
        }

        /// <summary>手动选择对话框。</summary>
        public static bool ShowManualDialog(Window owner, Action<string> log)
        {
            var w = new Window
            {
                Title = "请确认极域版本",
                Width = 560,
                SizeToContent = SizeToContent.Height,   // 自适应高度, 保证位置列表完整显示
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                FontFamily = UiUtil.AppFont,
                Background = UiUtil.C.Window,
                Icon = UiUtil.LoadAppIcon(),
                ResizeMode = ResizeMode.NoResize,
            };

            var root = new StackPanel { Margin = new Thickness(24, 18, 24, 14) };

            root.Children.Add(new TextBlock
            {
                Text = "无法自动识别极域版本（极域可能未在本机运行）。",
                FontSize = 13.5,
                Foreground = UiUtil.C.Text,
                TextWrapping = TextWrapping.Wrap,
            });
            root.Children.Add(new TextBlock
            {
                Text = "远程发送依赖正确的端口，请选择教室里实际安装的极域版本：",
                FontSize = 12.5,
                Foreground = UiUtil.C.Text2,
                Margin = new Thickness(0, 8, 0, 10),
                TextWrapping = TextWrapping.Wrap,
            });

            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(UiUtil.MakeLabel("极域版本"));
            var combo = new ComboBox { Height = 30, Style = UiUtil.ComboBoxStyle(), VerticalContentAlignment = VerticalAlignment.Center };
            foreach (var name in JyVersion.Names)
                combo.Items.Add(name);
            combo.SelectedIndex = JyVersion.SelectedIndex;
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);
            root.Children.Add(row);

            var multi = new CheckBox
            {
                Content = "不确定端口? 勾选多端口齐发 (4605 + 4705 + 4988 各发一份)",
                FontSize = 12.5,
                Foreground = UiUtil.C.Text2,
                Margin = new Thickness(0, 0, 0, 10),
            };
            multi.IsChecked = JyVersion.MultiPort;
            root.Children.Add(multi);

            // 可能的极域软件位置 (探测到的安装路径 + 常见位置)
            root.Children.Add(new TextBlock
            {
                Text = "可能的极域软件位置:",
                FontSize = 12.5,
                Foreground = UiUtil.C.Muted,
                Margin = new Thickness(0, 2, 0, 2),
            });
            string detectedPath = JyLocator.DetectInstallPath();
            var locBox = new TextBox
            {
                IsReadOnly = true,               // 可选中复制
                FontSize = 12,
                FontFamily = UiUtil.AppFont,
                Foreground = UiUtil.C.Text2,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 0, 0, 4),
                MaxHeight = 120,                 // 子目录多时滚动, 不撑爆窗口
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = BuildLocationList(detectedPath),
            };
            root.Children.Add(locBox);
            if (detectedPath != null && System.IO.Directory.Exists(detectedPath))
            {
                var openBtn = new Button
                {
                    Content = "打开该文件夹",
                    MinWidth = 110,
                    Height = 26,
                    Style = UiUtil.ButtonStyle(),
                    Margin = new Thickness(12, 2, 0, 6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                openBtn.Click += (s2, e2) =>
                {
                    try { Process.Start("explorer.exe", detectedPath); }
                    catch (Exception ex) { log?.Invoke("[错误] " + ex.Message); }
                };
                root.Children.Add(openBtn);
            }

            bool confirmed = false;

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
            };
            var ok = new Button { Content = "确定", Width = 110, Height = 30, Style = UiUtil.PrimaryButtonStyle() };
            ok.Click += (s2, e2) =>
            {
                if (combo.SelectedIndex < 0) return;
                JyVersion.SetSelected(combo.SelectedIndex);
                JyVersion.MultiPort = multi.IsChecked == true;
                Settings.VersionIndex = JyVersion.SelectedIndex;
                Settings.UseGlobalBroadcast = JySender.UseGlobalBroadcast;
                Settings.VersionConfirmed = true;
                Settings.Save();
                confirmed = true;
                log?.Invoke("极域版本已确认: " + JyVersion.ConfirmText(JyVersion.Names[JyVersion.SelectedIndex]));
                w.Close();
            };
            var later = new Button { Content = "以后再说", Width = 90, Height = 30, Margin = new Thickness(12, 0, 0, 0), Style = UiUtil.ButtonStyle() };
            later.Click += (s2, e2) => w.Close();
            buttons.Children.Add(ok);
            buttons.Children.Add(later);
            root.Children.Add(buttons);

            w.Content = root;
            try
            {
                if (owner != null && owner.IsLoaded) w.Owner = owner;
            }
            catch { }   // Owner 窗口尚未显示时保持无属主
            w.ShowDialog();
            return confirmed;
        }
    }
}
