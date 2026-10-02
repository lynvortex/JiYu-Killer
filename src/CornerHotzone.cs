using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 左上角热区（学习 再见极域 的招牌功能）：
    /// 200ms 轮询 GetCursorPos，鼠标进入屏幕左上角热区且此前不在热区时触发一次事件；
    /// 离开热区后重新武装。用于广播时快速唤起"最小化广播"询问。
    /// </summary>
    internal sealed class CornerHotzone : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT p);
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private readonly DispatcherTimer _timer;
        private bool _armed;

        /// <summary>触发时回调（参数为当前是否检测到广播）。</summary>
        public event Action<bool> Triggered;

        /// <summary>热区边长（像素, 逻辑坐标）。</summary>
        public int Size = 24;

        public bool Enabled
        {
            get { return _timer.IsEnabled; }
            set { _timer.IsEnabled = value; if (!value) _armed = false; }
        }

        public CornerHotzone()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += (s, e) => Poll();
        }

        public void Start() { _timer.Start(); }
        public void Stop() { _timer.Stop(); _armed = false; }

        private void Poll()
        {
            POINT p;
            if (!GetCursorPos(out p)) return;
            bool inZone = p.X <= Size && p.Y <= Size;
            if (inZone && _armed)
            {
                _armed = false;
                var cb = Triggered;
                if (cb != null) cb(BroadcastWindow.IsBroadcasting());
            }
            else if (!inZone)
            {
                _armed = true;
            }
        }

        public void Dispose() { Stop(); }
    }
}
