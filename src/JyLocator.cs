using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 极域路径三级识别 + 运行状态快照（学习 JiYuTrainer 与 极域Tool：
    /// 注册表 → GetExtendedTcpTable 按端口定位进程 → 磁盘扫描，保证换机房也能用）。
    /// </summary>
    internal static class JyLocator
    {
        // ---- 进程/系统信息 ----
        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr tcpTable, ref int outLen, bool sort, int ipVersion, int tableClass, int reserved);

        // ---- 状态快照 ----
        public sealed class Status
        {
            public bool Running;
            public int Pid;
            public string ProcessName = "-";
            public string Path = "未识别";
            public string Version = "-";
            public bool Broadcasting;
            public bool BlackScreen;
            public bool Frozen;
        }

        /// <summary>当前状态快照（UI 状态面板每秒轮询）。进程只枚举一次, 广播/黑屏检测无条件执行。</summary>
        public static Status Probe(bool frozenFlag)
        {
            var st = new Status { Frozen = frozenFlag };
            st.Broadcasting = BroadcastWindow.IsBroadcasting();
            st.BlackScreen = BroadcastWindow.IsBlackScreen();
            var names = JyTargets.Load();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (!IsTarget(p, names)) continue;
                    st.Running = true;
                    st.Pid = p.Id;
                    st.ProcessName = p.ProcessName + ".exe";
                    try { st.Path = p.MainModule.FileName; } catch { }
                    try { st.Version = p.MainModule.FileVersionInfo.ProductVersion ?? p.MainModule.FileVersionInfo.FileVersion ?? "-"; }
                    catch { }
                    break;   // 取第一个命中的主进程
                }
                catch { }
                finally { p.Dispose(); }
            }
            return st;
        }

        /// <summary>进程名是否命中名单（p.ProcessName 在进程恰好退出时会抛异常, 调用方需兜底）。</summary>
        private static bool IsTarget(Process p, List<string> names)
        {
            string exe = p.ProcessName + ".exe";
            foreach (string name in names)
                if (string.Equals(exe, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>
        /// 从运行中的极域进程读取版本信息并推断版本下标（JyVersion.Names 的 0..4）。
        /// 依据: 安装目录名关键字（2021/2020/2016/2015/2010、豪华、专版）+ ProductVersion 主版本号。
        /// 返回 -1 表示无法判断。
        /// </summary>
        public static int DetectVersionIndex()
        {
            var names = JyTargets.Load();
            foreach (var p in Process.GetProcesses())
            {
                int result = -1;
                try
                {
                    if (!IsTarget(p, names)) continue;
                    string path = null, productVersion = null;
                    try { path = p.MainModule.FileName; } catch { }
                    try { productVersion = p.MainModule.FileVersionInfo.ProductVersion; } catch { }
                    string hay = ((path ?? "") + " " + (productVersion ?? ""));
                    if (hay.Contains("2021")) result = 3;             // 2021 新版 -> 4988
                    else if (hay.Contains("2020") || hay.Contains("豪华")) result = 2;  // 2020 豪华 -> 4705
                    else if (hay.Contains("2016") || productVersion != null && productVersion.StartsWith("6"))
                        result = 2;                                    // v6.x -> 4705
                    else if (hay.Contains("2015")) result = 1;
                    else if (hay.Contains("2010") || productVersion != null && productVersion.StartsWith("5"))
                        result = 0;                                    // v5.x -> 4605
                    if (result >= 0)
                    {
                        // 2021 有新旧两个端口批次, 目录含"旧"字样或 ProductVersion 4.x 时用 4705
                        if (result == 3 && (hay.Contains("旧") || (productVersion != null && productVersion.StartsWith("4"))))
                            result = 4;
                    }
                }
                catch { }
                finally { p.Dispose(); }
                if (result >= 0) return result;
            }
            // 进程未运行: 退化为注册表卸载信息里的目录名关键字
            string path2 = FromRegistry();
            if (path2 != null)
            {
                if (path2.Contains("2021")) return 3;
                if (path2.Contains("2020") || path2.Contains("豪华")) return 2;
                if (path2.Contains("2016")) return 2;
                if (path2.Contains("2015")) return 1;
                if (path2.Contains("2010") || path2.Contains("e-Learning")) return 0;   // V4 目录名
            }
            return -1;
        }

        /// <summary>三级路径识别。返回极域安装目录或 null。</summary>
        public static string DetectInstallPath()
        {
            // 1) 注册表卸载信息
            string fromReg = FromRegistry();
            if (fromReg != null) return fromReg;

            // 2) 正在运行的进程路径
            string fromProc = FromRunningProcess();
            if (fromProc != null) return fromProc;

            // 3) 磁盘扫描（限定常见位置, 避免全盘扫描太慢）
            return FromDiskScan();
        }

        private static string FromRegistry()
        {
            try
            {
                foreach (var hive in new[] { Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry32), Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Default) })
                {
                    using (hive)
                    using (var k = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (k == null) continue;
                        foreach (string sub in k.GetSubKeyNames())
                        {
                            using (var sk = k.OpenSubKey(sub))
                            {
                                if (sk == null) continue;
                                string name = Convert.ToString(sk.GetValue("DisplayName") ?? "");
                                if (name.IndexOf("极域", StringComparison.Ordinal) < 0 &&
                                    name.IndexOf("Mythware", StringComparison.OrdinalIgnoreCase) < 0 &&
                                    name.IndexOf("e-Learning", StringComparison.OrdinalIgnoreCase) < 0)
                                    continue;
                                string loc = Convert.ToString(sk.GetValue("InstallLocation") ?? "");
                                if (!string.IsNullOrWhiteSpace(loc) && Directory.Exists(loc))
                                    return loc;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string FromRunningProcess()
        {
            var names = JyTargets.Load();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (!IsTarget(p, names)) continue;
                    return Path.GetDirectoryName(p.MainModule.FileName);
                }
                catch { }
                finally { p.Dispose(); }
            }
            return null;
        }

        private static string FromDiskScan()
        {
            string[] roots =
            {
                @"C:\Program Files (x86)\Mythware",
                @"C:\Program Files\Mythware",
                @"C:\Mythware",
                @"D:\Program Files (x86)\Mythware",
                @"D:\Mythware",
            };
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (var dir in new DirectoryInfo(root).GetDirectories())
                        if (File.Exists(Path.Combine(dir.FullName, "StudentMain.exe")))
                            return dir.FullName;
                    if (File.Exists(Path.Combine(root, "StudentMain.exe")))
                        return root;
                }
                catch { }
            }
            return null;
        }
    }
}
