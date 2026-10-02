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

        /// <summary>当前状态快照（UI 状态面板每秒轮询）。</summary>
        public static Status Probe(bool frozenFlag)
        {
            var st = new Status { Frozen = frozenFlag };
            foreach (string name in JyTargets.Load())
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (!string.Equals(p.ProcessName + ".exe", name, StringComparison.OrdinalIgnoreCase))
                            continue;
                        st.Running = true;
                        st.Pid = p.Id;
                        st.ProcessName = p.ProcessName + ".exe";
                        try { st.Path = p.MainModule.FileName; } catch { }
                        try { st.Version = p.MainModule.FileVersionInfo.ProductVersion ?? p.MainModule.FileVersionInfo.FileVersion ?? "-"; }
                        catch { }
                        return st;   // 取第一个命中的主进程
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            st.Broadcasting = BroadcastWindow.IsBroadcasting();
            st.BlackScreen = BroadcastWindow.IsBlackScreen();
            return st;
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
            foreach (string name in JyTargets.Load())
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (!string.Equals(p.ProcessName + ".exe", name, StringComparison.OrdinalIgnoreCase))
                            continue;
                        return Path.GetDirectoryName(p.MainModule.FileName);
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
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
