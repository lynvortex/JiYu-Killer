using System;
using System.Runtime.InteropServices;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 广播窗口化/检测（学习 jiyu_windowing 与 UnMythware 的做法，最温和的对抗手段：
    /// 不碰极域进程、不联网，只改造窗口本身）。
    ///   全屏广播渲染窗口类名: TDDesk Render Window
    ///   黑屏锁定窗口类名:     BlackScreen Window
    /// 窗口化 = 去掉全屏置顶样式并缩到一个普通窗口矩形；还原 = 恢复全屏置顶。
    /// </summary>
    internal static class BroadcastWindow
    {
        public const string RenderClass = "TDDesk Render Window";
        public const string BlackScreenClass = "BlackScreen Window";

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private const int GwlStyle = -16;
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsVisible = 0x10000000, WsCaption = 0x00C00000,
                           WsThickframe = 0x00040000, WsSysmenu = 0x00080000, WsMinimizebox = 0x20000, WsMaximizebox = 0x10000;
        private static readonly IntPtr HwndNotopmost = new IntPtr(-2);
        private const uint SwpNozorder = 0x4, SwpFramechanged = 0x20;

        private static bool _renderWindowed, _blackWindowed;

        public static bool IsBroadcasting()
        {
            IntPtr w = FindWindow(RenderClass, null);
            return w != IntPtr.Zero && IsWindowVisible(w);
        }

        public static bool IsBlackScreen()
        {
            IntPtr w = FindWindow(BlackScreenClass, null);
            return w != IntPtr.Zero && IsWindowVisible(w);
        }

        /// <summary>把一个全屏极域窗口窗口化。kind: "render" 或 "black"。</summary>
        public static string Windowize(string kind)
        {
            IntPtr w = FindWindow(kind == "black" ? BlackScreenClass : RenderClass, null);
            if (w == IntPtr.Zero || !IsWindowVisible(w))
                return (kind == "black" ? "未检测到黑屏窗口。" : "未检测到正在进行的屏幕广播。");

            // 去掉 popup 全屏样式，加上标题栏/边框/系统菜单
            int style = GetWindowLong(w, GwlStyle);
            style &= ~WsPopup;
            style |= (int)(WsCaption | WsThickframe | WsSysmenu | WsMinimizebox | WsMaximizebox | WsVisible);
            SetWindowLong(w, GwlStyle, style);

            // 置为非置顶并缩到屏幕内一个舒服的矩形
            int sw = GetSystemMetrics(0), sh = GetSystemMetrics(1);
            int ww = Math.Min(960, sw - 80), wh = Math.Min(600, sh - 120);
            SetWindowPos(w, HwndNotopmost, (sw - ww) / 2, (sh - wh) / 2, ww, wh, SwpNozorder | SwpFramechanged);

            if (kind == "black") _blackWindowed = true; else _renderWindowed = true;
            return kind == "black" ? "黑屏窗口已窗口化。" : "屏幕广播已窗口化。";
        }

        /// <summary>还原为全屏置顶（教师端停止广播后窗口会自行销毁，通常无需手动还原）。</summary>
        public static string Restore(string kind)
        {
            IntPtr w = FindWindow(kind == "black" ? BlackScreenClass : RenderClass, null);
            if (w == IntPtr.Zero || !IsWindowVisible(w))
                return "窗口不存在（可能广播已结束）。";

            int style = GetWindowLong(w, GwlStyle);
            style |= WsPopup;
            style &= ~(int)(WsCaption | WsThickframe | WsSysmenu | WsMinimizebox | WsMaximizebox);
            SetWindowLong(w, GwlStyle, style);
            SetWindowPos(w, IntPtr.Zero, 0, 0, GetSystemMetrics(0), GetSystemMetrics(1), SwpNozorder | SwpFramechanged);

            if (kind == "black") _blackWindowed = false; else _renderWindowed = false;
            return "已还原为全屏。";
        }

        /// <summary>最小化广播窗口（配合左上角热区）。</summary>
        public static string MinimizeBroadcast()
        {
            IntPtr w = FindWindow(RenderClass, null);
            if (w == IntPtr.Zero) return "未检测到正在进行的屏幕广播。";
            MoveWindow(w, 0, 0, 160, 90, true);
            return "广播窗口已缩小到左上角。";
        }
    }
}
