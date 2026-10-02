using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace JiYuKiller.UI
{
    /// <summary>
    /// 窗口隐藏助手引擎（Boss 键）—— 学习自一段 C++ 小工具并做了工程化改进:
    ///   Alt+B 隐藏前台窗口(压栈), Alt+N 恢复最近隐藏(弹栈),
    ///   Alt+H 首次按下创建并切入新虚拟桌面, 之后在两个桌面间往返;
    ///   后台每 100ms 把已隐藏的窗口再隐藏一遍, 防止被极域强制重新显示。
    /// 相比原版的改进:
    ///   · RegisterHotKey 全局热键代替 GetAsyncKeyState 轮询(不抢键、省 CPU);
    ///   · 隐藏列表自动清理已销毁的窗口句柄;
    ///   · 跳过桌面/任务栏/本程序自身的窗口;
    ///   · 虚拟桌面组合键在后台线程发送, 不阻塞 UI。
    /// </summary>
    internal sealed class BossKeyEngine : IDisposable
    {
        private const int HkHide = 1, HkRestore = 2, HkDesktop = 3;
        private const int WmHotkey = 0x0312;
        private const uint ModAlt = 0x1, ModNoRepeat = 0x4000;
        private const int SwHide = 0, SwShowNormal = 1;
        private const byte VkControl = 0x11, VkLwin = 0x5B, VkD = 0x44,
                           VkLeft = 0x25, VkRight = 0x27, KeyeventfKeyup = 0x2;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

        private static readonly int OwnPid = Process.GetCurrentProcess().Id;

        private readonly IntPtr _hwnd;
        private readonly List<IntPtr> _hidden = new List<IntPtr>();
        private readonly DispatcherTimer _rehider;
        private int _desktopState;   // 0=尚未创建第二个桌面, 1/2=当前在哪个桌面
        private bool _active;

        /// <summary>日志输出（已连接主窗口输出台）。</summary>
        public event Action<string> Log;

        public BossKeyEngine(Window owner, Action<string> log)
        {
            Log = log;
            _hwnd = new WindowInteropHelper(owner).EnsureHandle();
            HwndSource.FromHwnd(_hwnd).AddHook(WndProc);
            _rehider = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _rehider.Tick += (s, e) => RehideAll();
        }

        public bool Active { get { return _active; } }
        public int HiddenCount { get { return _hidden.Count; } }

        /// <summary>当前隐藏窗口的标题快照。</summary>
        public List<string> HiddenTitles()
        {
            var titles = new List<string>();
            foreach (IntPtr h in _hidden)
            {
                if (IsWindow(h))
                    titles.Add(GetTitle(h));
            }
            return titles;
        }

        public bool Start()
        {
            if (_active) return true;
            // 逐个注册, 便于报告是哪个键冲突
            bool hide = RegisterHotKey(_hwnd, HkHide, ModAlt | ModNoRepeat, 0x42);    // Alt+B
            bool restore = RegisterHotKey(_hwnd, HkRestore, ModAlt | ModNoRepeat, 0x4E); // Alt+N
            bool desktop = RegisterHotKey(_hwnd, HkDesktop, ModAlt | ModNoRepeat, 0x48); // Alt+H
            if (!hide || !restore || !desktop)
            {
                string conflict = !hide ? "Alt+B" : !restore ? "Alt+N" : "Alt+H";
                SafeLog("热键注册失败: " + conflict + " 可能已被其他程序占用。");
                Stop();
                return false;
            }
            _active = true;
            _rehider.Start();
            SafeLog("窗口隐藏助手已启动: Alt+B 隐藏 / Alt+N 恢复 / Alt+H 虚拟桌面。");
            return true;
        }

        public void Stop()
        {
            UnregisterHotKey(_hwnd, HkHide);
            UnregisterHotKey(_hwnd, HkRestore);
            UnregisterHotKey(_hwnd, HkDesktop);
            _rehider.Stop();
            if (_active)
            {
                _active = false;
                SafeLog("窗口隐藏助手已停止。已隐藏的窗口仍保持隐藏, 可用\"恢复全部\"还原。");
            }
        }

        /// <summary>清空隐藏列表（不再自动重藏这些窗口, 但不显示它们）。</summary>
        public void ClearHiddenList()
        {
            int n = _hidden.Count;
            _hidden.Clear();
            SafeLog("已清空隐藏列表 (" + n + " 项), 这些窗口不会再被自动重藏。");
        }

        /// <summary>恢复所有已隐藏的窗口（"恢复全部"按钮）。</summary>
        public void RestoreAll()
        {
            int n = 0;
            foreach (IntPtr h in _hidden.ToArray())
            {
                if (IsWindow(h))
                {
                    ShowWindow(h, SwShowNormal);
                    n++;
                }
            }
            _hidden.Clear();
            SafeLog("已恢复 " + n + " 个窗口。");
        }

        // ------------------------------------------------------------------ hotkey handlers

        private void HideForeground()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return;
            if (IsOwnWindow(h))
            {
                SafeLog("已跳过本程序自己的窗口。");
                return;
            }
            if (IsShellWindow(h)) return;
            if (_hidden.Contains(h)) return;
            if (ShowWindow(h, SwHide))
            {
                _hidden.Add(h);
                SafeLog("已隐藏: " + GetTitle(h));
            }
        }

        private void RestoreTop()
        {
            while (_hidden.Count > 0)
            {
                IntPtr h = _hidden[_hidden.Count - 1];
                _hidden.RemoveAt(_hidden.Count - 1);
                if (IsWindow(h))
                {
                    ShowWindow(h, SwShowNormal);
                    SafeLog("已恢复: " + GetTitle(h));
                    return;
                }
            }
            SafeLog("没有可恢复的窗口。");
        }

        /// <summary>重隐藏循环: 防止极域把窗口强制重新显示。</summary>
        private void RehideAll()
        {
            for (int i = _hidden.Count - 1; i >= 0; i--)
            {
                IntPtr h = _hidden[i];
                if (!IsWindow(h))
                {
                    _hidden.RemoveAt(i);   // 清理已销毁的句柄
                    continue;
                }
                ShowWindow(h, SwHide);
            }
        }

        /// <summary>
        /// 虚拟桌面切换（复刻原版三态）: 首次按下 Ctrl+Win+D 新建桌面并切入,
        /// 之后在两个桌面间左右往返 —— 极域抓屏看到的是干净的桌面。
        /// </summary>
        private void SwitchDesktop()
        {
            int state = _desktopState;
            Task.Run(() =>
            {
                if (state == 0)
                {
                    SendChord(VkD);
                    SendChord(VkRight);
                }
                else if (state == 1)
                {
                    SendChord(VkLeft);
                }
                else
                {
                    SendChord(VkRight);
                }
            });
            _desktopState = state == 0 ? 1 : state == 1 ? 2 : 1;
            SafeLog("虚拟桌面: " + (_desktopState == 1 ? "已创建并切入第 2 桌面" : _desktopState == 2 ? "切回第 1 桌面" : "切回第 2 桌面"));
        }

        private static void SendChord(byte key)
        {
            keybd_event(VkControl, 0, 0, UIntPtr.Zero);
            keybd_event(VkLwin, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(40);
            keybd_event(key, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(40);
            keybd_event(key, 0, KeyeventfKeyup, UIntPtr.Zero);
            keybd_event(VkLwin, 0, KeyeventfKeyup, UIntPtr.Zero);
            keybd_event(VkControl, 0, KeyeventfKeyup, UIntPtr.Zero);
            System.Threading.Thread.Sleep(40);
        }

        // ------------------------------------------------------------------ helpers

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkey && _active)
            {
                switch ((int)wParam)
                {
                    case HkHide: HideForeground(); break;
                    case HkRestore: RestoreTop(); break;
                    case HkDesktop: SwitchDesktop(); break;
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private bool IsOwnWindow(IntPtr h)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            return pid == (uint)OwnPid;
        }

        private static bool IsShellWindow(IntPtr h)
        {
            string cls = GetClassName(h);
            return cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd"
                || cls == "Shell_SecondaryTrayWnd";
        }

        private static string GetClassName(IntPtr h)
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            return sb.ToString();
        }

        private static string GetTitle(IntPtr h)
        {
            var sb = new StringBuilder(256);
            GetWindowText(h, sb, 256);
            string t = sb.ToString().Trim();
            return t.Length > 0 ? t : "(无标题窗口)";
        }

        private void SafeLog(string msg)
        {
            var log = Log;
            if (log != null)
                log(msg);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
