using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 配置包导入导出：把 IP.txt 与设置打包成单个文本文件，便于多机部署。
    /// 格式（纯文本, 便于手工查看）：
    ///   # JiYuKiller bundle v1
    ///   [ip]
    ///   &lt;每行一个 IP&gt;
    ///   [config]
    ///   &lt;key=value&gt;
    /// </summary>
    internal static class ConfigBundle
    {
        private const string Magic = "# JiYuKiller bundle v1";

        public static void Export(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Magic);
            sb.AppendLine("[ip]");
            foreach (string ip in JySender.ReadTargets())
                sb.AppendLine(ip);
            sb.AppendLine("[config]");
            sb.AppendLine("version=" + JyVersion.SelectedIndex);
            sb.AppendLine("global_broadcast=" + (JySender.UseGlobalBroadcast ? "1" : "0"));
            sb.AppendLine("theme=" + Settings.Theme);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        /// <summary>导入配置包；返回 IP 行数。</summary>
        public static int Import(string path)
        {
            var ips = new List<string>();
            var config = new Dictionary<string, string>();
            string section = null;

            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0)
                    continue;
                if (line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    section = line.Substring(1, line.Length - 2).ToLowerInvariant();
                    continue;
                }
                if (section == "ip")
                {
                    ips.Add(line);
                }
                else if (section == "config")
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0)
                        config[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }

            File.WriteAllLines(JySender.IpFilePath, ips);

            string v;
            if (config.TryGetValue("version", out v))
            {
                int idx;
                if (int.TryParse(v, out idx) && idx >= 0 && idx < JyVersion.Names.Length)
                {
                    JyVersion.SetSelected(idx);
                    Settings.VersionIndex = idx;
                }
            }
            if (config.TryGetValue("global_broadcast", out v))
            {
                JySender.UseGlobalBroadcast = v == "1";
                Settings.UseGlobalBroadcast = JySender.UseGlobalBroadcast;
            }
            if (config.TryGetValue("theme", out v) && Settings.IsValidTheme(v))
                Settings.Theme = v;
            Settings.Save();
            return ips.Count;
        }
    }
}
