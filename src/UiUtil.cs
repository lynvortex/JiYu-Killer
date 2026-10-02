using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JiYuKiller.UI
{
    /// <summary>
    /// 主题样式（深色/浅色由 Palette 决定）。所有颜色均从 Palette.Current 取，
    /// 切换主题后重建窗口即可生效。
    /// </summary>
    internal static class UiUtil
    {
        public static readonly FontFamily AppFont = new FontFamily("微软雅黑");

        public static Brush Brush(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>语义化取色，避免每处重复写颜色。</summary>
        internal static class C
        {
            public static Brush Window { get { return Brush(Palette.Current.WindowBg); } }
            public static Brush Sidebar { get { return Brush(Palette.Current.SidebarBg); } }
            public static Brush SidebarLine { get { return Brush(Palette.Current.SidebarLine); } }
            public static Brush Topbar { get { return Brush(Palette.Current.TopbarBg); } }
            public static Brush Card { get { return Brush(Palette.Current.CardBg); } }
            public static Brush CardLine { get { return Brush(Palette.Current.CardLine); } }
            public static Brush Control { get { return Brush(Palette.Current.ControlBg); } }
            public static Brush ControlLine { get { return Brush(Palette.Current.ControlLine); } }
            public static Brush ControlHover { get { return Brush(Palette.Current.ControlHover); } }
            public static Brush Text { get { return Brush(Palette.Current.TextPrimary); } }
            public static Brush Text2 { get { return Brush(Palette.Current.TextSecondary); } }
            public static Brush Muted { get { return Brush(Palette.Current.TextMuted); } }
            public static Brush Accent { get { return Brush(Palette.Current.Accent); } }
            public static Brush AccentHover { get { return Brush(Palette.Current.AccentHover); } }
            public static Brush AccentPressed { get { return Brush(Palette.Current.AccentPressed); } }
            public static Brush DangerCard { get { return Brush(Palette.Current.DangerCardBg); } }
            public static Brush DangerCardLine { get { return Brush(Palette.Current.DangerCardLine); } }
            public static Brush LogBg { get { return Brush(Palette.Current.LogBg); } }
            public static Brush LogText { get { return Brush(Palette.Current.LogText); } }
            public static Brush LogLine { get { return Brush(Palette.Current.LogLine); } }
            public static Brush Btn2 { get { return Brush(Palette.Current.SecondaryBtnBg); } }
            public static Brush Btn2Hover { get { return Brush(Palette.Current.SecondaryBtnHover); } }
            public static Brush Btn2Pressed { get { return Brush(Palette.Current.SecondaryBtnPressed); } }
            public static Brush Selected { get { return Brush(Palette.Current.SelectedBg); } }
        }

        public static ImageSource LoadAppIcon()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Control.ico");
                if (File.Exists(path))
                    return new BitmapImage(new Uri(path));
            }
            catch { }
            return null;
        }

        // ------------------------------------------------------------------ helpers

        private static ControlTemplate FillButtonTemplate(Brush normal, Brush hover, Brush pressed,
                                                         Brush border, double radius)
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "border";
            borderFactory.SetValue(Border.BackgroundProperty, normal);
            borderFactory.SetValue(Border.BorderBrushProperty, border);
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(content);
            template.VisualTree = borderFactory;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hover, "border"));
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, pressed, "border"));
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(Border.OpacityProperty, 0.45, "border"));
            disabled.Setters.Add(new Setter(Control.ForegroundProperty, C.Muted));
            template.Triggers.Add(hoverTrigger);
            template.Triggers.Add(pressedTrigger);
            template.Triggers.Add(disabled);
            return template;
        }

        private static Style MakeButtonStyle(ControlTemplate template, Brush fg, double size)
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.FontFamilyProperty, AppFont));
            style.Setters.Add(new Setter(Control.FontSizeProperty, size));
            style.Setters.Add(new Setter(Control.ForegroundProperty, fg));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 30.0));
            style.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        // ------------------------------------------------------------------ button styles

        /// <summary>次要按钮。</summary>
        public static Style ButtonStyle()
        {
            return MakeButtonStyle(FillButtonTemplate(C.Btn2, C.Btn2Hover, C.Btn2Pressed, C.ControlLine, 4), C.Text, 13);
        }

        /// <summary>主要按钮（强调红填充）。</summary>
        public static Style PrimaryButtonStyle()
        {
            return MakeButtonStyle(FillButtonTemplate(C.Accent, C.AccentHover, C.AccentPressed, C.Accent, 4), Brushes.White, 13);
        }

        /// <summary>高危按钮（大号强调红）。</summary>
        public static Style DangerButtonStyle()
        {
            var style = MakeButtonStyle(FillButtonTemplate(C.Accent, C.AccentHover, C.AccentPressed, C.Accent, 4), Brushes.White, 14);
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 38.0));
            style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 170.0));
            return style;
        }

        /// <summary>侧边导航项（选中: 浅底 + 左侧红色竖条）。</summary>
        public static Style NavItemStyle()
        {
            var style = new Style(typeof(ToggleButton));
            style.Setters.Add(new Setter(Control.FontFamilyProperty, AppFont));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.5));
            style.Setters.Add(new Setter(Control.ForegroundProperty, C.Text2));
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 38.0));
            style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(10, 2, 10, 2)));
            style.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));

            var template = new ControlTemplate(typeof(ToggleButton));
            var grid = new FrameworkElementFactory(typeof(Grid));
            var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(4));
            var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            grid.AppendChild(col1);
            grid.AppendChild(col2);
            var bar = new FrameworkElementFactory(typeof(Border));
            bar.Name = "bar";
            bar.SetValue(Grid.ColumnProperty, 0);
            bar.SetValue(Border.WidthProperty, 3.0);
            bar.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            bar.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Grid.ColumnProperty, 1);
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            border.SetValue(Border.PaddingProperty, new Thickness(12, 0, 8, 0));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            grid.AppendChild(bar);
            grid.AppendChild(border);
            template.VisualTree = grid;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, C.Selected, "border"));
            var check = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            check.Setters.Add(new Setter(Border.BackgroundProperty, C.Selected, "border"));
            check.Setters.Add(new Setter(Border.BackgroundProperty, C.Accent, "bar"));
            check.Setters.Add(new Setter(Control.ForegroundProperty, C.Text));
            template.Triggers.Add(hover);
            template.Triggers.Add(check);
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        /// <summary>顶部工具栏按钮（紧凑, 无边框）。</summary>
        public static Style ToolbarButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.FontFamilyProperty, AppFont));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
            style.Setters.Add(new Setter(Control.ForegroundProperty, C.Text2));
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 28.0));
            style.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
            style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            border.SetValue(Border.PaddingProperty, new Thickness(10, 2, 10, 2));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, C.Selected, "border"));
            var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Border.BackgroundProperty, C.Btn2Pressed, "border"));
            template.Triggers.Add(hover);
            template.Triggers.Add(pressed);
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        // ------------------------------------------------------------------ input styles

        /// <summary>输入框（占位文本做进模板, Text 为空时显示）。</summary>
        public static Style TextBoxStyle()
        {
            var style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(Control.FontFamilyProperty, AppFont));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            style.Setters.Add(new Setter(Control.ForegroundProperty, C.Text));
            style.Setters.Add(new Setter(TextBox.CaretBrushProperty, C.Text));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(Control.BackgroundProperty, C.Control));

            var template = new ControlTemplate(typeof(TextBox));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, C.ControlLine);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            var inner = new FrameworkElementFactory(typeof(Grid));
            var placeholder = new FrameworkElementFactory(typeof(TextBlock));
            placeholder.Name = "placeholder";
            placeholder.SetValue(TextBlock.TextProperty, new TemplateBindingExtension(TextBoxHelper.PlaceholderProperty));
            placeholder.SetValue(TextBlock.ForegroundProperty, C.Muted);
            placeholder.SetValue(Control.FontFamilyProperty, AppFont);
            placeholder.SetValue(Control.FontSizeProperty, 13.0);
            placeholder.SetValue(UIElement.IsHitTestVisibleProperty, false);
            placeholder.SetValue(FrameworkElement.MarginProperty, new Thickness(9, 0, 0, 0));
            placeholder.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            placeholder.SetValue(FrameworkElement.VisibilityProperty, Visibility.Collapsed);
            inner.AppendChild(placeholder);
            var scroller = new FrameworkElementFactory(typeof(ScrollViewer));
            scroller.Name = "PART_ContentHost";
            scroller.SetValue(ScrollViewer.MarginProperty, new Thickness(6, 2, 6, 2));
            inner.AppendChild(scroller);
            border.AppendChild(inner);
            template.VisualTree = border;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, C.ControlHover, "border"));
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, C.Accent, "border"));
            var emptyText = new Trigger { Property = TextBox.TextProperty, Value = "" };
            emptyText.Setters.Add(new Setter(FrameworkElement.VisibilityProperty, Visibility.Visible, "placeholder"));
            template.Triggers.Add(hover);
            template.Triggers.Add(focus);
            template.Triggers.Add(emptyText);
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        /// <summary>下拉框（自定义模板；关键是把 ToggleButton.IsChecked 双向绑定到 IsDropDownOpen）。</summary>
        public static Style ComboBoxStyle()
        {
            var style = new Style(typeof(ComboBox));
            style.Setters.Add(new Setter(Control.FontFamilyProperty, AppFont));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            style.Setters.Add(new Setter(Control.ForegroundProperty, C.Text));
            style.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));

            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            var itemTemplate = new ControlTemplate(typeof(ComboBoxItem));
            var itemBorder = new FrameworkElementFactory(typeof(Border));
            itemBorder.Name = "border";
            itemBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            itemBorder.SetValue(Border.PaddingProperty, new Thickness(9, 6, 9, 6));
            var itemContent = new FrameworkElementFactory(typeof(ContentPresenter));
            itemBorder.AppendChild(itemContent);
            itemTemplate.VisualTree = itemBorder;
            var itemHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            itemHover.Setters.Add(new Setter(Border.BackgroundProperty, C.Control, "border"));
            var itemSelected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            itemSelected.Setters.Add(new Setter(Border.BackgroundProperty, C.Selected, "border"));
            itemTemplate.Triggers.Add(itemHover);
            itemTemplate.Triggers.Add(itemSelected);
            itemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));
            style.Setters.Add(new Setter(ComboBox.ItemContainerStyleProperty, itemStyle));

            var template = new ControlTemplate(typeof(ComboBox));
            var grid = new FrameworkElementFactory(typeof(Grid));
            var toggle = new FrameworkElementFactory(typeof(ToggleButton), "toggle");
            toggle.SetValue(Grid.ColumnSpanProperty, 2);
            toggle.SetValue(ButtonBase.ClickModeProperty, ClickMode.Press);
            toggle.SetValue(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
                Mode = BindingMode.TwoWay,
            });
            var toggleTemplate = new ControlTemplate(typeof(ToggleButton));
            var toggleBorder = new FrameworkElementFactory(typeof(Border));
            toggleBorder.Name = "border";
            toggleBorder.SetValue(Border.BackgroundProperty, C.Control);
            toggleBorder.SetValue(Border.BorderBrushProperty, C.ControlLine);
            toggleBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            toggleBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            toggleTemplate.VisualTree = toggleBorder;
            var toggleHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            toggleHover.Setters.Add(new Setter(Border.BorderBrushProperty, C.ControlHover, "border"));
            var toggleChecked = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            toggleChecked.Setters.Add(new Setter(Border.BorderBrushProperty, C.Accent, "border"));
            toggleTemplate.Triggers.Add(toggleHover);
            toggleTemplate.Triggers.Add(toggleChecked);
            toggle.SetValue(Control.TemplateProperty, toggleTemplate);

            var selectedItem = new FrameworkElementFactory(typeof(ContentPresenter));
            selectedItem.SetValue(Grid.ColumnProperty, 0);
            selectedItem.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
            selectedItem.SetValue(ContentPresenter.MarginProperty, new Thickness(9, 0, 0, 0));
            selectedItem.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            selectedItem.SetValue(UIElement.IsHitTestVisibleProperty, false);

            var arrow = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            arrow.SetValue(Grid.ColumnProperty, 1);
            arrow.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 0 0 L 4 4 L 8 0 Z"));
            arrow.SetValue(System.Windows.Shapes.Path.FillProperty, C.Muted);
            arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 2, 0));
            arrow.SetValue(UIElement.IsHitTestVisibleProperty, false);

            var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            popup.SetValue(Popup.StaysOpenProperty, false);
            popup.SetValue(Popup.IsOpenProperty, new Binding("IsDropDownOpen")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
            });
            popup.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 0));
            var popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.BackgroundProperty, C.Control);
            popupBorder.SetValue(Border.BorderBrushProperty, C.ControlLine);
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            popupBorder.SetValue(FrameworkElement.MinWidthProperty, new Binding("ActualWidth")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
            });
            var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
            scroll.SetValue(ScrollViewer.CanContentScrollProperty, false);
            scroll.SetValue(Control.BackgroundProperty, C.Control);
            scroll.SetValue(FrameworkElement.MaxHeightProperty, 220.0);
            var items = new FrameworkElementFactory(typeof(ItemsPresenter));
            scroll.AppendChild(items);
            popupBorder.AppendChild(scroll);
            popup.AppendChild(popupBorder);

            grid.AppendChild(toggle);
            grid.AppendChild(selectedItem);
            grid.AppendChild(arrow);
            grid.AppendChild(popup);
            template.VisualTree = grid;
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        /// <summary>日志列表（深色底衬托彩色输出）。</summary>
        public static Style LogListStyle()
        {
            var style = new Style(typeof(ListBox));
            style.Setters.Add(new Setter(Control.FontFamilyProperty, AppFont));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
            style.Setters.Add(new Setter(Control.BackgroundProperty, C.LogBg));
            style.Setters.Add(new Setter(Control.ForegroundProperty, C.LogText));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, C.LogLine));
            return style;
        }

        /// <summary>分区卡片：圆角面板 + 标题。</summary>
        public static Border Card(string header, UIElement content)
        {
            var root = new StackPanel();
            if (!string.IsNullOrEmpty(header))
            {
                root.Children.Add(new TextBlock
                {
                    Text = header,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13.5,
                    Foreground = C.Text,
                    Margin = new Thickness(0, 0, 0, 8),
                });
            }
            root.Children.Add(content);
            return new Border
            {
                Background = C.Card,
                BorderBrush = C.CardLine,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Child = root,
            };
        }

        public static Label MakeLabel(string text)
        {
            return new Label
            {
                Content = text,
                FontFamily = AppFont,
                FontSize = 13,
                Foreground = C.Text2,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 0, 8, 0),
            };
        }

        public static TextBlock MakeText(string text, double size = 13, bool muted = false)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = AppFont,
                FontSize = size,
                Foreground = muted ? C.Muted : C.Text2,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
    }
}
