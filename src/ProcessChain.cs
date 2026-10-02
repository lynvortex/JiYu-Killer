using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 多级杀进程链（学习 UnMythware V4.5 + 极域Tool 的组合，全部用户态、不用驱动）：
    ///   1. taskkill /f /im
    ///   2. SeDebugPrivilege + OpenProcess/TerminateProcess
    ///   3. 向目标窗口投递 WM_CLOSE/WM_QUIT
    ///   4. DebugActiveProcess 调试器附加（极域的 IFEO 防杀对调试器无效）
    ///   5. Job Object 整体终止（ZwTerminateJobObject，连守护子进程一锅端）
    /// 每一级失败自动降级到下一级，结束后报告每一级的结果。
    /// 注意: 极域常劫持 IFEO 中 taskkill.exe 的 debugger（见注册表解锁套件），
    /// 本链条第 2 级起不经过 taskkill，不受该劫持影响。
    /// </summary>
    internal static class ProcessChain
    {
        // ---- Win32 ----
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")]
        private static extern bool TerminateProcess(IntPtr handle, uint exitCode);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DebugActiveProcess(int pid);
        [DllImport("kernel32.dll")]
        private static extern bool DebugActiveProcessStop(int pid);
        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateJobObjectA(IntPtr attrs, string name);
        [DllImport("kernel32.dll")]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")]
        private static extern bool TerminateJobObject(IntPtr job, uint exitCode);
        [DllImport("kernel32.dll")]
        private static extern void CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint desired, out IntPtr token);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string system, string name, ref long luid);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll,
            ref TOKEN_PRIVILEGES newState, int len, IntPtr prev, IntPtr ret);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        private const uint TokenAdjustPrivileges = 0x20, TokenQuery = 0x8;
        private const uint WmClose = 0x0010, WmQuit = 0x0012;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES { public long Luid; public uint Attributes; }
        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public int PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        }
        private const uint SePrivilegeEnabled = 0x2;

        /// <summary>启用 SeDebugPrivilege（杀受保护进程的前提，成功一次即可）。</summary>
        public static bool EnableDebugPrivilege()
        {
            try
            {
                IntPtr token;
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle,
                    TokenAdjustPrivileges | TokenQuery, out token))
                    return false;
                try
                {
                    var tp = new TOKEN_PRIVILEGES
                    {
                        PrivilegeCount = 1,
                        Privileges = new LUID_AND_ATTRIBUTES { Attributes = SePrivilegeEnabled },
                    };
                    if (!LookupPrivilegeValue(null, "SeDebugPrivilege", ref tp.Privileges.Luid))
                        return false;
                    // AdjustTokenPrivileges 返回 true 不代表权限已生效,
                    // 未持有该特权时 GetLastError = ERROR_NOT_ALL_ASSIGNED(1300)
                    if (!AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                        return false;
                    return Marshal.GetLastWin32Error() != 1300;
                }
                finally { CloseHandle(token); }
            }
            catch { return false; }
        }

        private static List<int> PidsOf(string processName)
        {
            var pids = new List<int>();
            string want = processName.Trim().EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName.Trim() : processName.Trim() + ".exe";
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (string.Equals(p.ProcessName + ".exe", want, StringComparison.OrdinalIgnoreCase))
                        pids.Add(p.Id);
                }
                catch { }
                finally { p.Dispose(); }
            }
            return pids;
        }

        private static IntPtr OpenFullAccess(int pid)
        {
            // SeDebug 下 PROCESS_ALL_ACCESS(0x1F0FFF)；这里取终止+挂起恢复+查询所需权限的组合
            return OpenProcess(0x1F0FFF, false, pid);
        }

        /// <summary>
        /// 执行多级杀进程链。返回每级结果描述（供日志/消息框显示）。
        /// </summary>
        public static string KillChain(string processName)
        {
            var report = new StringBuilder();
            var results = new List<string>
            {
                "L1 taskkill: " + Describe(RunTaskkill(processName)),
                "L2 SeDebug+Terminate: " + Describe(KillByTerminate(processName)),
                "L3 窗口消息: " + Describe(KillByWindowMessage(processName)),
                "L4 DebugActiveProcess: " + Describe(KillByDebuggerAttach(processName)),
                "L5 Job Object: " + Describe(KillByJobObject(processName)),
            };
            foreach (string r in results)
                report.AppendLine(r);

            bool alive = PidsOf(processName).Count > 0;
            report.AppendLine(alive
                ? "结果: " + processName + " 仍存活（可能需要驱动或先解除其防杀保护）。"
                : "结果: " + processName + " 已终止。");
            return report.ToString();
        }

        private static string Describe(int killed, int total)
        {
            if (total == 0) return "目标不存在（视为成功）";
            return killed > 0 ? "已终止 " + killed + "/" + total : "无效";
        }

        // L1
        private static Tuple<int, int> RunTaskkill(string name)
        {
            var pids = PidsOf(name);
            if (pids.Count == 0) return Tuple.Create(0, 0);
            LocalOps.RunCmd("taskkill /f /t /im " + name);
            int left = PidsOf(name).Count;
            return Tuple.Create(pids.Count - left, pids.Count);
        }

        // L2
        private static Tuple<int, int> KillByTerminate(string name)
        {
            EnableDebugPrivilege();
            int killed = 0, total = 0;
            foreach (int pid in PidsOf(name))
            {
                total++;
                IntPtr h = OpenFullAccess(pid);
                if (h != IntPtr.Zero)
                {
                    if (TerminateProcess(h, 1)) killed++;
                    CloseHandle(h);
                }
            }
            return Tuple.Create(killed, total);
        }

        // L3: 向目标进程的顶层窗口投递 WM_CLOSE/WM_QUIT
        private static Tuple<int, int> KillByWindowMessage(string name)
        {
            int sent = 0, total = 0;
            var pids = new HashSet<int>(PidsOf(name));
            foreach (IntPtr w in EnumTopWindows())
            {
                uint pid;
                GetWindowThreadProcessId(w, out pid);
                if (pids.Contains((int)pid))
                {
                    total++;
                    if (PostMessage(w, WmClose, IntPtr.Zero, IntPtr.Zero) ||
                        PostMessage(w, WmQuit, IntPtr.Zero, IntPtr.Zero))
                        sent++;
                }
            }
            return Tuple.Create(sent, total);
        }

        private static IEnumerable<IntPtr> EnumTopWindows()
        {
            var list = new List<IntPtr>();
            EnumWindows((h, l) => { list.Add(h); return true; }, IntPtr.Zero);
            return list;
        }

        private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumProc cb, IntPtr lParam);

        // L4: 调试器附加 —— 成为调试器后 TerminateProcess 不再受保护限制
        private static Tuple<int, int> KillByDebuggerAttach(string name)
        {
            EnableDebugPrivilege();
            int killed = 0, total = 0;
            foreach (int pid in PidsOf(name))
            {
                total++;
                bool ok = false;
                if (DebugActiveProcess(pid))
                {
                    IntPtr h = OpenFullAccess(pid);
                    if (h != IntPtr.Zero)
                    {
                        ok = TerminateProcess(h, 1);
                        CloseHandle(h);
                    }
                    DebugActiveProcessStop(pid);   // 无论成败都解除附加
                }
                if (ok) killed++;
            }
            return Tuple.Create(killed, total);
        }

        // L5: Job Object 整体终止（连守护子进程）
        private static Tuple<int, int> KillByJobObject(string name)
        {
            int killed = 0, total = 0;
            foreach (int pid in PidsOf(name))
            {
                total++;
                IntPtr proc = OpenFullAccess(pid);
                if (proc == IntPtr.Zero) continue;
                IntPtr job = CreateJobObjectA(IntPtr.Zero, null);
                if (job != IntPtr.Zero)
                {
                    if (AssignProcessToJobObject(job, proc))
                    {
                        TerminateJobObject(job, 1);
                        killed++;
                    }
                    CloseHandle(job);
                }
                CloseHandle(proc);
            }
            return Tuple.Create(killed, total);
        }

        /// <summary>对名单里的全部进程执行杀链。</summary>
        public static string KillAllFromTargets()
        {
            var report = new StringBuilder();
            foreach (string name in JyTargets.Load())
            {
                report.AppendLine("== " + name + " ==");
                report.Append(KillChain(name));
            }
            return report.ToString();
        }

        private static string Describe(Tuple<int, int> r)
        {
            return Describe(r.Item1, r.Item2);
        }
    }
}
