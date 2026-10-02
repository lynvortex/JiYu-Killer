using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using JiYuKiller.Core;

namespace JiYuKiller.UI
{
    /// <summary>点对点消息窗口（原版 P2PMsg）。</summary>
    internal class P2pWindow : Window
    {
        private static readonly Regex Ipv4Pattern = new Regex(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$");
        private static readonly Regex Ipv6Pattern = new Regex(
            @"^([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}$|^([0-9a-fA-F]{1,4}:){1,7}:|^([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}$" +
            @"|^([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}$|^([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}$" +
            @"|^([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}$|^([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}$" +
            @"|^[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})$|^:((:[0-9a-fA-F]{1,4}){0,5}:)$|^::1$|^::$");

        private TextBox _ipLine;
        private TextBox _msgLine;

        public P2pWindow()
        {
            Title = "点对点消息";
            Width = 621;
            Height = 265;
            MinHeight = 260;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = UiUtil.AppFont;
            Icon = UiUtil.LoadAppIcon();
            Background = UiUtil.C.Window;
            Resources.Add(typeof(Button), UiUtil.ButtonStyle());
            Resources.Add(typeof(TextBox), UiUtil.TextBoxStyle());

            var header = new TextBlock
            {
                Text = "JiYu Killer - 点对点消息",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = UiUtil.C.Accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 10),
            };

            var tip = new TextBlock
            {
                Text = "键入对方和要发送的消息，便可以指定向对方发送消息了",
                FontSize = 12,
                Foreground = UiUtil.C.Muted,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
            };

            var ipRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            ipRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ipRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var ipLabel = UiUtil.MakeLabel("输入对方IP: ");
            _ipLine = new TextBox { Height = 28 };
            _ipLine.SetValue(TextBoxHelper.PlaceholderProperty, "键入一个IP...");
            Grid.SetColumn(ipLabel, 0);
            Grid.SetColumn(_ipLine, 1);
            ipRow.Children.Add(ipLabel);
            ipRow.Children.Add(_ipLine);

            var msgRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            msgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            msgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var msgLabel = UiUtil.MakeLabel("输入发送的消息: ");
            _msgLine = new TextBox { Height = 28 };
            _msgLine.SetValue(TextBoxHelper.PlaceholderProperty, "键入要发送的消息...");
            Grid.SetColumn(msgLabel, 0);
            Grid.SetColumn(_msgLine, 1);
            msgRow.Children.Add(msgLabel);
            msgRow.Children.Add(_msgLine);

            var sendBtn = new Button { Content = "发送", MinWidth = 100, Height = 30 };
            sendBtn.Click += (s, e) => CarryOut();
            var closeBtn = new Button { Content = "关闭", MinWidth = 80, Height = 30, Margin = new Thickness(12, 0, 0, 0) };
            closeBtn.Click += (s, e) => Close();
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
            };
            buttons.Children.Add(sendBtn);
            buttons.Children.Add(closeBtn);

            var root = new StackPanel { Margin = new Thickness(16) };
            root.Children.Add(header);
            root.Children.Add(tip);
            root.Children.Add(ipRow);
            root.Children.Add(msgRow);
            root.Children.Add(buttons);
            Content = root;
        }

        private bool IsValidIp(string ip)
        {
            return Ipv4Pattern.IsMatch(ip) || Ipv6Pattern.IsMatch(ip);
        }

        private void CarryOut()
        {
            string ip = _ipLine.Text.Trim();
            string msg = _msgLine.Text;
            if (!IsValidIp(ip))
            {
                MessageBox.Show("不是一个有效的 IPv4 地址。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (msg.Trim().Length == 0)
            {
                MessageBox.Show("键入要发送的消息...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                IPAddress target = IPAddress.Parse(ip);
                if (target.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    // 报文只支持 4 字节 IPv4 目标; IPv6 会构造出损坏的包
                    MessageBox.Show("目前仅支持 IPv4 目标地址。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                byte[] payload = JyPackets.BuildMessage(target, msg, JyVersion.Port);
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.SendTo(payload, new IPEndPoint(target, JyVersion.Port));
                }
                MessageBox.Show("发送成功! ", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("错误: \n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
