using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 托盘图标（学习 再见极域 的 SetupTrayIcon + TaskbarCreated 重挂）：
    /// explorer 重启后任务栏会重建, 监听 "TaskbarCreated" 广播消息重新添加图标。
    /// 双击 = 显示主窗口; 右键菜单由调用方通过 ContextMenu 提供。
    /// </summary>
    internal sealed class TrayIcon : IDisposable
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern void Shell_NotifyIconA(int message, ref NOTIFYICONDATA data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint load);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        private const int NimAdd = 0, NimModify = 1, NimDelete = 2;
        private const int NifMessage = 1, NifIcon = 2, NifTip = 4;
        private const int ImageIcon = 1, LoadFromFile = 0x10, LoadDefault = 0;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize, hWnd;
            public int uID, uFlags, uCallbackMessage, hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        private readonly Window _window;
        private readonly HwndSource _source;
        private bool _added;
        private readonly int _id = 0x4A59;
        private uint _taskbarCreatedMsg;

        /// <summary>托盘图标被双击（= 显示主窗口）。</summary>
        public event Action DoubleClick;

        public TrayIcon(Window window)
        {
            _window = window;
            _source = (HwndSource)HwndSource.FromVisual(window);
            _source.AddHook(WndProc);
            _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");
        }

        public void Show()
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                uID = _id,
                uFlags = NifMessage | NifIcon | NifTip,
                uCallbackMessage = 0x0F33,     // 自定义回调消息
                szTip = "JiYu Killer",
            };
            data.hWnd = (int)_source.Handle;
            data.hIcon = (int)LoadIconHandle();
            Shell_NotifyIconA(NimAdd, ref data);
            _added = true;
        }

        public void Hide()
        {
            if (!_added) return;
            var data = new NOTIFYICONDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = (int)_source.Handle,
                uID = _id,
            };
            Shell_NotifyIconA(NimDelete, ref data);
            _added = false;
        }

        private IntPtr LoadIconHandle()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Control.ico");
            if (System.IO.File.Exists(path))
            {
                IntPtr h = LoadImage(IntPtr.Zero, path, ImageIcon, 16, 16, LoadFromFile);
                if (h != IntPtr.Zero) return h;
            }
            return LoadImage(GetModuleHandle(null), "#32512", ImageIcon, 16, 16, LoadDefault); // IDI_APPLICATION
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0F33)
            {
                int mouseMsg = (int)((long)lParam >> 16) & 0xFFFF;  // 高 16 位为鼠标消息
                if (mouseMsg == 0x0203 /* WM_LBUTTONDBLCLK */)
                {
                    var cb = DoubleClick;
                    if (cb != null) cb();
                    handled = true;
                }
                return IntPtr.Zero;
            }
            if (msg == (int)_taskbarCreatedMsg && _added)
            {
                Show();   // explorer 重启后重建
            }
            return IntPtr.Zero;
        }

        public void Dispose() { Hide(); }
    }
}
