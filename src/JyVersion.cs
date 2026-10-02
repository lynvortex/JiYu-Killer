using System;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 极域版本选择对应的端口与消息包头。
    /// 端口表依据：冷麟极域课堂反控制程序反编译源码（2010→4605 / 2016→4705 / 2021→4988）
    /// 与 jiyu_windowing（4605=2010及以前 / 4705=2010以后）。
    /// 注: 不同批次的 2021 版在 4705/4988 上都有报告, 因此提供"多端口齐发"。
    /// </summary>
    internal static class JyVersion
    {
        public const int Port4605 = 4605;
        public const int Port4705 = 4705;
        public const int Port4988 = 4988;

        public static readonly string[] Names = { "2010版", "2015版", "2016版", "2021新版", "2021旧版" };
        public static readonly int[] Ports = { Port4605, Port4605, Port4705, Port4988, Port4705 };

        /// <summary>当前选中的版本下标（0..4），默认 2016版。</summary>
        public static int SelectedIndex = 2;

        /// <summary>多端口齐发：发送时向所有不同候选端口各发一份。</summary>
        public static bool MultiPort = false;

        public static int[] AllDistinctPorts
        {
            get { return new[] { Port4605, Port4705, Port4988 }; }
        }

        public static int Port
        {
            get { return Ports[SelectedIndex]; }
        }

        /// <summary>日志用端口文案: 多端口模式下不再写"某某版本(端口X)", 只写多端口。</summary>
        public static string PortText()
        {
            return MultiPort ? "多端口 4605+4705+4988" : "端口 " + Port;
        }

        /// <summary>
        /// 确认类日志的版本描述: 多端口模式下不报具体版本名
        /// (此时版本不影响发送行为), 只报"多端口齐发"; 单端口时报"版本名 (端口 X)"。
        /// </summary>
        public static string ConfirmText(string versionName)
        {
            return MultiPort ? "多端口齐发 (4605 + 4705 + 4988)" : versionName + " (端口 " + Port + ")";
        }

        public static void SetSelected(int index)
        {
            if (index < 0 || index >= Names.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            SelectedIndex = index;
        }
    }
}
