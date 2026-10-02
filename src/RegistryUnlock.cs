using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 注册表解锁套件（学习 再见极域 的完整清单）：
    /// 极域/机房助手通过以下策略键禁用系统功能，删除即恢复：
    ///   DisableCMD(cmd) / DisableRegistryTools / DisableTaskMgr / NoRun(Win+R) /
    ///   NoLogOff·StartMenuLogOff(注销) / DisableLockWorkstation(键盘锁) /
    ///   IFEO taskkill.exe 的 debugger 劫持（极域防杀的关键, 必须解除否则远程 taskkill 无效）
    /// </summary>
    internal static class RegistryUnlock
    {
        /// <summary>解锁项定义。</summary>
        public sealed class Item
        {
            public string Title;
            public RegistryHive Hive;
            public string SubKey;
            public string ValueName;      // null = 删除整个键（IFEO 场景）
            public bool Wow64_32;         // 在 32 位视图下操作
        }

        public static readonly Item[] Items =
        {
            new Item { Title = "解除 cmd 限制", Hive = RegistryHive.CurrentUser,
                SubKey = @"Software\Policies\Microsoft\Windows\System", ValueName = "DisableCMD" },
            new Item { Title = "解禁注册表编辑器", Hive = RegistryHive.CurrentUser,
                SubKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System", ValueName = "DisableRegistryTools" },
            new Item { Title = "解禁任务管理器", Hive = RegistryHive.CurrentUser,
                SubKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System", ValueName = "DisableTaskMgr" },
            new Item { Title = "解禁 Win+R 运行", Hive = RegistryHive.CurrentUser,
                SubKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", ValueName = "NoRun" },
            new Item { Title = "解禁注销 (HKCU)", Hive = RegistryHive.CurrentUser,
                SubKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", ValueName = "NoLogOff" },
            new Item { Title = "解禁注销 (HKLM)", Hive = RegistryHive.LocalMachine,
                SubKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System", ValueName = "StartMenuLogOff" },
            new Item { Title = "解键盘锁", Hive = RegistryHive.CurrentUser,
                SubKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System", ValueName = "DisableLockWorkstation" },
            new Item { Title = "解除 taskkill IFEO 劫持 (关键)", Hive = RegistryHive.LocalMachine,
                SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\taskkill.exe",
                ValueName = "debugger", Wow64_32 = true },
        };

        /// <summary>执行全部解锁，返回每项结果报告。</summary>
        public static string UnlockAll()
        {
            var report = new List<string>();
            foreach (Item item in Items)
            {
                string err;
                bool changed = UnlockItem(item, out err);
                if (err != null)
                    report.Add(item.Title + ": 失败 (" + err + ")");
                else if (changed)
                    report.Add(item.Title + ": 已解除");
                else
                    report.Add(item.Title + ": 本来就未禁用");
            }
            return string.Join(Environment.NewLine, report);
        }

        private static bool UnlockItem(Item item, out string err)
        {
            err = null;
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(item.Hive,
                    item.Wow64_32 ? RegistryView.Registry32 : RegistryView.Default))
                using (var key = baseKey.OpenSubKey(item.SubKey, true))
                {
                    if (key == null)
                    {
                        err = "键不存在";
                        return false;
                    }
                    if (item.ValueName == null)
                    {
                        // 整键删除（IFEO taskkill 整个键通常只有 debugger 一个值）
                        string[] names = key.GetValueNames();
                        bool had = false;
                        foreach (string n in names)
                        {
                            if (string.Equals(n, "debugger", StringComparison.OrdinalIgnoreCase))
                                had = true;
                            key.DeleteValue(n, false);
                        }
                        return had;
                    }
                    if (key.GetValue(item.ValueName) == null)
                        return false;
                    key.DeleteValue(item.ValueName, false);
                    return true;
                }
            }
            catch (Exception ex)
            {
                err = ex.Message;
                return false;
            }
        }
    }
}
