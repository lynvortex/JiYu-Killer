using System;
using System.Diagnostics;
using System.Security.Principal;

namespace JiYuKiller.Core
{
    /// <summary>本机操作：进程查杀、服务停止、管理员检测（对应原版 close_jy_main 等）。</summary>
    internal static class LocalOps
    {
        public static bool IsAdmin()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>隐藏窗口执行命令并返回退出码（原版 os.system）。</summary>
        public static int RunCmd(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + command,
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        /// <summary>
        /// 关掉此电脑上的极域（原版 close_jy_main + 翘课3.0 的 NCStu 支持）：
        /// taskkill studentmain + NCStu（新版极域学生端, 翘课3.0 同款进程名）+ MasterHelper。
        /// </summary>
        public static string CloseJy()
        {
            int re1 = RunCmd("taskkill /f /im studentmain.exe");
            int re2 = RunCmd("taskkill /f /im NCStu.exe");
            int re3 = RunCmd("taskkill /f /im MasterHelper.exe");
            return "极域主进程状态: " + Describe(re1)
                 + "\nNCStu状态: " + Describe(re2)
                 + "\nMasterHelper状态: " + Describe(re3);
        }

        /// <summary>
        /// 提升自身进程优先级到 Above Normal（翘课3.0 同款自保手段）：
        /// 极域冻结/降低其他进程优先级时, 本程序仍能保持响应。返回描述文本。
        /// </summary>
        public static string BoostSelfPriority()
        {
            try
            {
                var p = Process.GetCurrentProcess();
                var old = p.PriorityClass;
                p.PriorityClass = ProcessPriorityClass.AboveNormal;
                return "优先级已从 " + old + " 调整为 AboveNormal。";
            }
            catch (Exception ex)
            {
                return "调整失败: " + ex.Message;
            }
        }

        // ------------------------------------------------------------------ 进程挂起/恢复
        //  学习自 mythwarehelper: NtSuspendProcess/NtResumeProcess 一调用冻结/解冻整个进程,
        //  比杀掉极域优雅 —— 广播/控制立即失效, 恢复后极域无需重新登录即可继续使用。

        [System.Runtime.InteropServices.DllImport("ntdll.dll")]
        private static extern int NtSuspendProcess(IntPtr processHandle);
        [System.Runtime.InteropServices.DllImport("ntdll.dll")]
        private static extern int NtResumeProcess(IntPtr processHandle);

        private const uint ProcessSuspendResume = 0x0800;
        private static readonly string[] JyProcessNames = { "StudentMain", "NCStu" };

        /// <summary>极域是否处于被本程序挂起的状态（状态面板用）。</summary>
        public static bool JyFrozen;

        /// <summary>挂起/恢复本机极域进程。suspend=true 挂起, false 恢复。返回描述文本。</summary>
        public static string SetJySuspended(bool suspend)
        {
            int ok = 0, fail = 0;
            foreach (string name in JyProcessNames)
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try
                    {
                        IntPtr handle = p.Handle;   // 触发访问检查, 无权限时抛异常
                        int status = suspend ? NtSuspendProcess(handle) : NtResumeProcess(handle);
                        if (status == 0) ok++; else fail++;
                    }
                    catch
                    {
                        fail++;   // 权限不足或进程恰好退出
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            if (ok > 0) JyFrozen = suspend;
            if (ok == 0 && fail == 0)
                return suspend ? "没有找到运行中的极域进程, 无需挂起。" : "没有找到被挂起的极域进程。";
            return (suspend ? "已挂起 " : "已恢复 ") + ok + " 个极域进程"
                 + (fail > 0 ? "（" + fail + " 个失败, 通常需要管理员权限）" : "") + "。";
        }

        /// <summary>
        /// 一键解除U盘和网络限制（原版 close_masterhelper_main）：
        /// sc stop tdnetfilter + taskkill masterhelper + GATESRV。
        /// </summary>
        public static string ClearWebControl()
        {
            int re1 = RunCmd("sc stop tdnetfilter");
            int re2 = RunCmd("taskkill /f /im masterhelper.exe");
            int re3 = RunCmd("taskkill /f /im GATESRV.exe");
            return "观察状态码以确认是否解除成功。\n(0 = OK) (128 = OK) (5 = 权限错误) (1060 = 没有找到服务(≈成功)) (其他: 可能成功)\n"
                 + "极域主进程状态: " + Describe(re1)
                 + "\nGATESRV.EXE状态: " + Describe(re3)
                 + "\nMasterHelper状态: " + Describe(re2);
        }

        private static string Describe(int code)
        {
            return code + (code == 0 || code == 128 ? " (成功)" : code == 1060 ? " (服务不存在≈成功)" : " (失败)");
        }

        /// <summary>
        /// 一键恢复：把"关闭极域"的操作尽量还原——重启 tdnetfilter 服务、
        /// 确保 explorer.exe 在运行。只能在本机执行, 需要管理员权限。
        /// </summary>
        public static string RestoreJy()
        {
            int svc = RunCmd("sc start tdnetfilter");
            bool explorerRunning = IsProcessRunning("explorer");
            int exp = 0;
            if (!explorerRunning)
                exp = RunCmd("start \"\" explorer.exe");
            return "已尝试恢复本机状态：\n"
                 + "  tdnetfilter 服务: " + Describe(svc) + "\n"
                 + "  桌面(explorer.exe): " + (explorerRunning ? "本就在运行" : Describe(exp)) + "\n"
                 + "注: 极域学生端若已被关闭, 需在教师机或本机手动重启极域客户端。";
        }

        public static bool IsProcessRunning(string name)
        {
            try
            {
                var procs = System.Diagnostics.Process.GetProcessesByName(name);
                try { return procs.Length > 0; }
                finally { foreach (var p in procs) p.Dispose(); }
            }
            catch { return false; }
        }

        /// <summary>
        /// 以管理员身份重启自身（UAC 自提权）。返回 false 表示用户取消了 UAC。
        /// </summary>
        public static bool Elevate()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = System.Reflection.Assembly.GetEntryAssembly().Location,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                };
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;   // 用户在 UAC 对话框点了"否"
            }
        }
    }
}
