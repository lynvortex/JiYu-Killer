using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 局域网扫描：仅扫描本机所在网段（或指定网段）的存活主机，用于核对 IP.txt。
    /// 不向全网段做无差别探测，也不做端口指纹识别。
    /// </summary>
    internal static class NetScan
    {
        /// <summary>本机所在网段（形如 192.168.56.0/24）；取不到时返回 null。</summary>
        public static string LocalCidr()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    byte[] b = ip.GetAddressBytes();
                    if (b[0] == 127 || (b[0] == 169 && b[1] == 254))
                        continue;
                    return b[0] + "." + b[1] + "." + b[2] + ".0/24";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 扫描网段内的存活主机。批量并发（每批 64 个），可用 <paramref name="ct"/> 中止。
        /// </summary>
        public static async Task<List<string>> ScanAsync(string cidr, Action<string> progress,
                                                        CancellationToken ct, int timeoutMs = 600)
        {
            List<string> hosts = IpGenerator.GenerateHosts(cidr);
            var alive = new List<string>();
            const int batchSize = 64;

            for (int i = 0; i < hosts.Count; i += batchSize)
            {
                if (ct.IsCancellationRequested)
                    break;
                if (progress != null)
                    progress("扫描中… " + Math.Min(i + batchSize, hosts.Count) + "/" + hosts.Count);

                var batch = new List<Task>();
                var results = new List<string>();
                for (int j = i; j < Math.Min(i + batchSize, hosts.Count); j++)
                {
                    string host = hosts[j];
                    batch.Add(Task.Run(async () =>
                    {
                        try
                        {
                            using (var ping = new Ping())
                            {
                                var reply = await ping.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
                                if (reply != null && reply.Status == IPStatus.Success)
                                {
                                    lock (results) results.Add(host);
                                }
                            }
                        }
                        catch { }
                    }, ct));
                }
                try { await Task.WhenAll(batch).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }

                alive.AddRange(results);   // WhenAll 之后 results 已完整, 顺序收集
            }

            alive.Sort(CompareIp);
            return alive;
        }

        private static int CompareIp(string a, string b)
        {
            byte[] x = IPAddress.Parse(a).GetAddressBytes();
            byte[] y = IPAddress.Parse(b).GetAddressBytes();
            for (int i = 0; i < 4; i++)
            {
                int d = x[i].CompareTo(y[i]);
                if (d != 0) return d;
            }
            return 0;
        }
    }
}
