using System;
using System.Collections.Generic;

namespace JiYuKiller.Core
{
    /// <summary>极域版本选择对应的端口与消息包头（对应原版 jy_comboBox / set_version）。</summary>
    internal static class JyVersion
    {
        public const int Port4605 = 4605;   // 2010/2015/2016 版
        public const int Port4705 = 4705;   // 2021 版

        public static readonly string[] Names = { "2010版", "2015版", "2016版", "2021版" };

        /// <summary>当前选中的版本下标（0..3），默认 2016版。</summary>
        public static int SelectedIndex = 2;

        public static int Port
        {
            get { return SelectedIndex >= 3 ? Port4705 : Port4605; }
        }

        public static void SetSelected(int index)
        {
            if (index < 0 || index >= Names.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            SelectedIndex = index;
        }
    }
}
