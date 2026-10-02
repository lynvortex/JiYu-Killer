using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 工程层杂项：单实例互斥、崩溃日志（含 MiniDump）、文件日志。
    /// 学习 JiYuTrainer 的做法（dbghelp dump + log），是三个样本里唯一做全的。
    /// </summary>
    internal static class AppRuntime
    {
        private static Mutex _mutex;

        /// <summary>单实例检测。返回 false 表示已有实例在运行。</summary>
        public static bool AcquireSingleInstance()
        {
            _mutex = new Mutex(true, "Local\\JiYuKiller_SingleInstance", out bool created);
            if (created) return true;
            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        public static void ReleaseSingleInstance()
        {
            if (_mutex != null) { try { _mutex.ReleaseMutex(); } catch { } _mutex.Dispose(); _mutex = null; }
        }

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(IntPtr process, int processId, string file, int dumpType, IntPtr exc, IntPtr user, IntPtr callback);

        /// <summary>异常时写文本日志 + minidump（尽力而为, 任一失败不影响调用方）。</summary>
        public static void WriteCrashReport(Exception ex)
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                File.AppendAllText(Path.Combine(dir, "crash.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + ex + "\r\n\r\n");
            }
            catch { }
            try
            {
                string dmp = Path.Combine(dir, "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".dmp");
                using (var p = System.Diagnostics.Process.GetCurrentProcess())
                    MiniDumpWriteDump(p.Handle, p.Id, dmp, 0 /* MiniDumpNormal */, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        /// <summary>操作日志追加到文件（与输出台同步, 便于事后回溯）。</summary>
        public static void AppendOpLog(string line)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jylog.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + line + "\r\n");
            }
            catch { }
        }
    }
}
