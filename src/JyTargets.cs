using System;
using System.Collections.Generic;
using System.IO;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 目标进程名单（外部可编辑配置 targets.txt，学习自"去除控制"套件的 ktr.dat 做法）。
    /// 所有杀/挂起/冻结功能统一从这里取进程名，换机房只需改这一个文件。
    /// </summary>
    internal static class JyTargets
    {
        private static readonly string[] Defaults =
        {
            "StudentMain.exe",   // 极域学生端主进程
            "NCStu.exe",         // 新版极域学生端
            "GATESRV.exe",       // 极域服务
            "MasterHelper.exe",  // 极域辅助
            "jfglzs.exe",        // 学生机房管理助手
            "prozs.exe",         // 极域同门/联想云教室相关
            "REDAgent.exe",      // 红蜘蛛多媒体
        };

        public static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "targets.txt"); }
        }

        /// <summary>
        /// 名单里的进程名会被拼进 taskkill / cmd 命令行执行,
        /// 含 cmd 元字符(&amp;|&lt;&gt;^%"'! 等)或路径分隔符的行一律丢弃, 防止配置文件被替换后注入命令。
        /// </summary>
        private static readonly char[] ForbiddenChars =
            { '&', '|', '<', '>', '^', '%', '"', '\'', '!', '/', '\\', ',', ';', '`' };

        private static bool IsSafeProcessName(string name)
        {
            if (name.Length == 0 || name.Length > 260)
                return false;
            foreach (char c in name)
                if (char.IsControl(c) || Array.IndexOf(ForbiddenChars, c) >= 0)
                    return false;
            return true;
        }

        /// <summary>读取名单（targets.txt 不存在时写出默认名单并返回默认值）。</summary>
        public static List<string> Load()
        {
            var list = new List<string>();
            try
            {
                if (!File.Exists(FilePath))
                {
                    File.WriteAllLines(FilePath, Defaults);
                    list.AddRange(Defaults);
                    return list;
                }
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//"))
                        continue;
                    if (!IsSafeProcessName(line))
                        continue;   // 非法字符行直接忽略
                    if (!list.Contains(line))
                        list.Add(line);
                }
                if (list.Count == 0)
                    list.AddRange(Defaults);
            }
            catch
            {
                list.AddRange(Defaults);
            }
            return list;
        }
    }
}
