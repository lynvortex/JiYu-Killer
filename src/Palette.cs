using System.Collections.Generic;

namespace JiYuKiller.UI
{
    /// <summary>主题色板。深色/浅色各一套，UiUtil 与窗口都从这里取色。</summary>
    internal sealed class Palette
    {
        public string WindowBg;      // 窗口底
        public string SidebarBg;     // 侧栏
        public string SidebarLine;   // 侧栏分隔线
        public string TopbarBg;      // 顶栏
        public string CardBg;        // 卡片
        public string CardLine;      // 卡片边框
        public string ControlBg;     // 输入框/下拉底
        public string ControlLine;   // 控件边框
        public string ControlHover;  // 控件边框悬停
        public string TextPrimary;   // 主文本
        public string TextSecondary; // 次要文本
        public string TextMuted;     // 更弱文本
        public string Accent;        // 强调红
        public string AccentHover;
        public string AccentPressed;
        public string DangerCardBg;  // 高危卡底
        public string DangerCardLine;
        public string LogBg;         // 日志台
        public string LogText;
        public string LogLine;
        public string SecondaryBtnBg;
        public string SecondaryBtnHover;
        public string SecondaryBtnPressed;
        public string SelectedBg;    // 选中项底

        public static Palette Current = Dark();

        public static void Apply(string theme)
        {
            Current = theme == "light" ? Light() : Dark();
        }

        public static Palette Dark()
        {
            return new Palette
            {
                WindowBg = "#17191E",
                SidebarBg = "#101216",
                SidebarLine = "#23262D",
                TopbarBg = "#1B1E24",
                CardBg = "#1E2128",
                CardLine = "#31353E",
                ControlBg = "#262A32",
                ControlLine = "#333842",
                ControlHover = "#4A5060",
                TextPrimary = "#E8EAEE",
                TextSecondary = "#B9BEC8",
                TextMuted = "#8A919E",
                Accent = "#E5484D",
                AccentHover = "#F0555A",
                AccentPressed = "#C93A3F",
                DangerCardBg = "#2A1B1D",
                DangerCardLine = "#5A2A2E",
                LogBg = "#14161B",
                LogText = "#C8CCD4",
                LogLine = "#31353E",
                SecondaryBtnBg = "#2A2E36",
                SecondaryBtnHover = "#343944",
                SecondaryBtnPressed = "#3C4250",
                SelectedBg = "#3C4250",
            };
        }

        public static Palette Light()
        {
            return new Palette
            {
                WindowBg = "#F3F4F6",
                SidebarBg = "#FFFFFF",
                SidebarLine = "#E2E4E8",
                TopbarBg = "#FFFFFF",
                CardBg = "#FFFFFF",
                CardLine = "#E2E4E8",
                ControlBg = "#FFFFFF",
                ControlLine = "#CCCCCC",
                ControlHover = "#0078D4",
                TextPrimary = "#1F1F1F",
                TextSecondary = "#444444",
                TextMuted = "#6B7280",
                Accent = "#D93A3F",
                AccentHover = "#E5484D",
                AccentPressed = "#B92F34",
                DangerCardBg = "#FFF6F6",
                DangerCardLine = "#F3C4C6",
                LogBg = "#1E1E1E",
                LogText = "#D4D4D4",
                LogLine = "#3C3C3C",
                SecondaryBtnBg = "#FFFFFF",
                SecondaryBtnHover = "#F0F0F0",
                SecondaryBtnPressed = "#E0E0E0",
                SelectedBg = "#E8EAED",
            };
        }
    }
}
