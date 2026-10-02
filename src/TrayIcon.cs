using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 托盘图标（学习 再见极域 的 SetupTrayIcon + TaskbarCreated 重挂）：
    /// explorer 重启后任务栏会重建, 监听 "TaskbarCreated" 广播消息重新添加图标。
    /// 双击 = 显示主窗口; 右键菜单由调用方通过 ContextMenu 提供。
    /// 注意: NOTIFYICONDATA 必须按完整的 V3 布局声明(hWnd/hIcon 为 IntPtr),
    /// 只声明到 szTip 的截断结构在 64 位构建下字段整体错位, NIM_ADD 传出的 hWnd 是垃圾值;
    /// 回调消息里 lParam 本身就是鼠标消息(没有高低字拆分), wParam 才是 uID。
    /// </summary>
    internal sealed class TrayIcon : IDisposable
    {
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern bool Shell_NotifyIconW(int message, ref NOTIFYICONDATA data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint load);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadImage(IntPtr hInst, IntPtr name, uint type, int cx, int cy, uint load);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        private const int NimAdd = 0, NimModify = 1, NimDelete = 2;
        private const int NifMessage = 1, NifIcon = 2, NifTip = 4;
        private const int ImageIcon = 1, LoadFromFile = 0x10, LoadShared = 0x8000;
        private const int WmLbuttondblclk = 0x0203;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
        }

        private readonly HwndSource _source;
        private bool _added;
        private readonly uint _id = 0x4A59;
        private uint _taskbarCreatedMsg;

        /// <summary>托盘图标被双击（= 显示主窗口）。</summary>
        public event Action DoubleClick;

        public TrayIcon(Window window)
        {
            _source = (HwndSource)HwndSource.FromVisual(window);
            _source.AddHook(WndProc);
            _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");
        }

        public void Show()
        {
            var data = NewData();
            data.uFlags = NifMessage | NifIcon | NifTip;
            data.uCallbackMessage = 0x0F33;     // 自定义回调消息
            data.szTip = "JiYu Killer";
            data.hIcon = LoadIconHandle();
            Shell_NotifyIconW(NimAdd, ref data);
            _added = true;
        }

        public void Hide()
        {
            if (!_added) return;
            var data = NewData();
            Shell_NotifyIconW(NimDelete, ref data);
            _added = false;
        }

        private NOTIFYICONDATA NewData()
        {
            return new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _source.Handle,
                uID = _id,
            };
        }

        private IntPtr LoadIconHandle()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Control.ico");
            if (System.IO.File.Exists(path))
            {
                IntPtr h = LoadImage(IntPtr.Zero, path, ImageIcon, 16, 16, LoadFromFile | LoadShared);
                if (h != IntPtr.Zero) return h;
            }
            // LoadImage 不解析 "#32512" 文本形式(那是 LoadIcon 的行为), 序号资源必须传 MAKEINTRESOURCE
            return LoadImage(GetModuleHandle(null), (IntPtr)32512 /* IDI_APPLICATION */, ImageIcon, 16, 16, LoadShared);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0F33)
            {
                if (lParam.ToInt32() == WmLbuttondblclk)
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

        public void Dispose()
        {
            _source.RemoveHook(WndProc);
            Hide();
        }
    }
}
