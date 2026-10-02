using System;
using System.Collections.Generic;
using System.Net;

namespace JiYuKiller.Core
{
    /// <summary>
    /// IP.txt 生成器：输入 CIDR 网段（如 10.132.5.0/24），展开全部主机地址。
    /// 对应原版 ipaddress.ip_network(strict=False).hosts()。
    /// </summary>
    internal static class IpGenerator
    {
        public static List<string> GenerateHosts(string cidr)
        {
            var result = new List<string>();
            cidr = (cidr ?? "").Trim();
            int slash = cidr.IndexOf('/');
            if (slash <= 0)
                throw new FormatException("格式应为 网段/前缀长度，示例: 10.132.5.0/24");

            IPAddress baseIp;
            if (!IPAddress.TryParse(cidr.Substring(0, slash).Trim(), out baseIp)
                || baseIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                throw new FormatException("不是一个有效的ipv4网段");

            int prefix;
            if (!int.TryParse(cidr.Substring(slash + 1).Trim(), out prefix) || prefix < 0 || prefix > 32)
                throw new FormatException("前缀长度应为 0-32");

            uint addr = ToUInt(baseIp);
            uint mask = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
            uint network = addr & mask;
            uint broadcast = network | (~mask);
            int hostBits = 32 - prefix;

            // 资源保护: 过大网段会生成千万级字符串导致 UI 冻结/内存耗尽
            if (hostBits > 16)
                throw new FormatException("网段过大 (超过 65536 个地址), 请使用更长前缀 (如 /16 或更小范围)");

            // /31 /32 特殊处理: 全部地址
            if (hostBits <= 1)
            {
                for (uint a = network; a <= broadcast; a++)
                    result.Add(FromUInt(a).ToString());
                return result;
            }

            // hosts(): 排除网络地址与广播地址
            for (uint a = network + 1; a < broadcast; a++)
                result.Add(FromUInt(a).ToString());
            return result;
        }

        private static uint ToUInt(IPAddress ip)
        {
            byte[] b = ip.GetAddressBytes();
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        }

        private static IPAddress FromUInt(uint a)
        {
            return new IPAddress(new byte[] { (byte)(a >> 24), (byte)(a >> 16), (byte)(a >> 8), (byte)a });
        }
    }
}
