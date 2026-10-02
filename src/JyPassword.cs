using System;
using System.Text;
using Microsoft.Win32;

namespace JiYuKiller.Core
{
    /// <summary>
    /// 极域密码工具。学习自 mythwarehelper (关闭极域的钥匙/mythwarehelper-master):
    /// 本机安装的极域把设置密码存在
    ///   HKLM\SOFTWARE\TopDomain\e-Learning Class\Student\knock1
    /// (32 位视图), 编码为每 4 字节循环 XOR 0x15,0x0F,0x0F,0x15 的 UTF-16LE。
    /// 读取的是"本机"配置, 用于找回本机被设置的密码; 管理员权限可读。
    /// </summary>
    internal static class JyPassword
    {
        /// <summary>极域设置界面的万能密码（公开已知, 对未改密的版本有效）。</summary>
        public const string SuperPassword = "mythware_super_password";

        private static readonly byte[] XorKey = { 0x50 ^ 0x45, 0x43 ^ 0x4c, 0x4c ^ 0x43, 0x45 ^ 0x50 };

        /// <summary>解码 knock1 数据: 4 字节循环 XOR + UTF-16LE 提取（到双零终止）。</summary>
        public static string DecodeKnock(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;
            byte[] buf = (byte[])data.Clone();
            for (int i = 0; i + 3 < buf.Length; i += 4)
            {
                buf[i] ^= XorKey[0];
                buf[i + 1] ^= XorKey[1];
                buf[i + 2] ^= XorKey[2];
                buf[i + 3] ^= XorKey[3];
            }
            var sb = new StringBuilder();
            for (int i = 0; i + 1 < buf.Length; i += 2)
            {
                if (buf[i] == 0 && buf[i + 1] == 0)
                    break;
                char c = (char)(buf[i] | (buf[i + 1] << 8));
                if (c == 0)
                    break;
                sb.Append(c);
            }
            string result = sb.ToString();
            return result.Length > 0 ? result : null;
        }

        /// <summary>读取本机极域存储的设置密码；不存在/不可读时返回 null。</summary>
        public static string TryReadLocalPassword()
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\TopDomain\e-Learning Class\Student"))
                {
                    if (key == null)
                        return null;
                    object v = key.GetValue("knock1");
                    if (v == null)
                        return null;
                    byte[] data = v as byte[];
                    if (data == null)
                        data = Encoding.ASCII.GetBytes(Convert.ToString(v));
                    return DecodeKnock(data);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
