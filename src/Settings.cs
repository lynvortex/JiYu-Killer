using System;
using System.Collections.Generic;
using System.IO;

namespace JiYuKiller.Core
{
    /// <summary>轻量设置持久化（exe 同目录 jyconfig.ini）。</summary>
    internal static class Settings
    {
        private const int MaxHistory = 30;

        private static string Path
        {
            get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jyconfig.ini"); }
        }

        public static int VersionIndex;
        public static bool UseGlobalBroadcast;
        /// <summary>主题: "dark"（默认）或 "light"。</summary>
        public static string Theme = "dark";
        /// <summary>命令历史（最近在后）。</summary>
        public static readonly List<string> History = new List<string>();

        public static bool IsValidTheme(string t)
        {
            return t == "dark" || t == "light";
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(Path))
                    return;
                foreach (var raw in File.ReadAllLines(Path))
                {
                    var kv = raw.Split(new[] { '=' }, 2);
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim(), val = kv[1].Trim();
                    if (key == "version") { int v; if (int.TryParse(val, out v)) VersionIndex = v; }
                    else if (key == "global_broadcast") UseGlobalBroadcast = val == "1";
                    else if (key == "theme" && IsValidTheme(val)) Theme = val;
                    else if (key == "history") AddHistory(val, save: false);
                }
            }
            catch { }
            if (VersionIndex < 0 || VersionIndex >= JyVersion.Names.Length)
                VersionIndex = 2;
        }

        public static void Save()
        {
            try
            {
                var lines = new List<string>
                {
                    "version=" + VersionIndex,
                    "global_broadcast=" + (UseGlobalBroadcast ? "1" : "0"),
                    "theme=" + Theme,
                };
                // 历史逐条存（单行输入框不可能含换行, 直接存）
                foreach (string h in History)
                    lines.Add("history=" + h.Replace("\r", "").Replace("\n", " "));
                File.WriteAllLines(Path, lines);
            }
            catch { }
        }

        /// <summary>记录一条命令历史（去重、置顶、限长）。</summary>
        public static void AddHistory(string cmd, bool save = true)
        {
            if (string.IsNullOrEmpty(cmd)) return;
            cmd = cmd.Trim();
            if (cmd.Length == 0) return;
            History.Remove(cmd);
            History.Add(cmd);
            while (History.Count > MaxHistory)
                History.RemoveAt(0);
            if (save) Save();
        }
    }
}
