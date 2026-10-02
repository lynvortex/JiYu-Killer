using System.Windows;

namespace JiYuKiller.UI
{
    /// <summary>
    /// TextBox 占位文本附加属性（对应原版 setPlaceholderText）。
    /// 占位文本由 UiUtil.TextBoxStyle 的模板读取并渲染（Text 为空时显示），
    /// 不再篡改 TextBox.Background —— 否则本地值会覆盖样式背景。
    /// </summary>
    internal static class TextBoxHelper
    {
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.RegisterAttached("Placeholder", typeof(string), typeof(TextBoxHelper),
                new PropertyMetadata(null));

        public static string GetPlaceholder(DependencyObject obj) { return (string)obj.GetValue(PlaceholderProperty); }
        public static void SetPlaceholder(DependencyObject obj, string value) { obj.SetValue(PlaceholderProperty, value); }
    }
}
