using System.IO;
using System.Windows;
using System.Windows.Controls;
using JiYuKiller.Core;

namespace JiYuKiller.UI
{
    /// <summary>IP.txt 编辑对话框（原版 Edit_IP）。</summary>
    internal class EditIpDialog : Window
    {
        private readonly TextBox _editor;

        public EditIpDialog()
        {
            Title = "编辑IP";
            Width = 583;
            Height = 316;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = UiUtil.AppFont;
            Icon = UiUtil.LoadAppIcon();
            Background = UiUtil.C.Window;
            Resources.Add(typeof(Button), UiUtil.ButtonStyle());
            Resources.Add(typeof(TextBox), UiUtil.TextBoxStyle());

            _editor = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontSize = 13,
                Foreground = UiUtil.C.Text,
                CaretBrush = UiUtil.C.Text,
                Background = UiUtil.C.Control,
                BorderBrush = UiUtil.C.ControlLine,
                Margin = new Thickness(0, 0, 0, 10),
            };
            try
            {
                if (File.Exists(JySender.IpFilePath))
                    _editor.Text = File.ReadAllText(JySender.IpFilePath);
            }
            catch { }

            var saveBtn = new Button { Content = "保存并关闭", MinWidth = 100, Height = 30 };
            saveBtn.Click += (s, e) => { Save(); DialogResult = true; };
            var cancelBtn = new Button { Content = "取消", MinWidth = 80, Height = 30, Margin = new Thickness(12, 0, 0, 0) };
            cancelBtn.Click += (s, e) => DialogResult = false;
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            buttons.Children.Add(saveBtn);
            buttons.Children.Add(cancelBtn);

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(_editor, 0);
            Grid.SetRow(buttons, 1);
            root.Children.Add(_editor);
            root.Children.Add(buttons);
            Content = root;
        }

        private void Save()
        {
            File.WriteAllText(JySender.IpFilePath, _editor.Text);
        }
    }
}
