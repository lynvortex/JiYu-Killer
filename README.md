# JiYu Killer

针对极域电子教室（Mythware e-Learning Classroom）的 C# / WPF 控制与辅助工具。
从零重写，不包含任何第三方二进制或资源文件。

> **仅用于学习研究、课堂辅助与合法的整蛊场景。** 请勿对未经授权的计算机使用，
> 使用所造成的一切后果由使用者自行承担。

## 功能

### 远程控制（目标 = IP.txt 全部 IP 或全局广播 `224.50.50.42`）

- 基本操作：打开记事本 / 关机 / 强制重启 / 提示重启 / 关闭所有应用程序 / 杀掉桌面进程
- 快捷启动：计算器 / 画图 / 任务管理器 / 命令提示符 / 记事本 / 控制面板
- 发送消息（极域弹窗）、系统命令（`/h` 前缀 = 隐藏命令窗口）、打开文件/网页
- 延时 / 重复发送，紧急停止（按钮或 Esc），发送统计（仅本机发送结果，不代表对端执行）
- 版本→端口：2010→4605 / 2015→4605 / 2016→4705 / 2021新版→4988 / 2021旧版→4705，
  可勾选"多端口齐发"（4605+4705+4988 同时发送，兼容全部批次）

### 关闭极域（高危）

- 向全局广播地址 `224.50.50.42` 发送 `/h taskkill /f /im studentmain.exe`
  （隐藏指令报文，网段内所有极域客户端均会收到）

### 进程与对抗

- **多级杀进程链**（`ProcessChain.cs`）：taskkill → SeDebugPrivilege+TerminateProcess →
  窗口消息(WM_CLOSE/WM_QUIT) → DebugActiveProcess 调试器附加杀 → Job Object 整体终止，
  每级失败自动降级，逐级报告结果。调试器附加在权限模型上等价于 ntsd `-c q`，
  无需分发 ntsd.exe，也不受极域对 taskkill 的 IFEO 劫持影响
- **广播窗口化 / 还原**：按窗口类名识别全屏广播（`TDDesk Render Window`）与
  黑屏锁定（`BlackScreen Window`），去掉全屏样式缩为普通窗口——广播变窗口、
  不碰极域进程、不联网，最温和的对抗手段
- **注册表解锁套件**：cmd / 注册表编辑器 / 任务管理器 / Win+R / 注销 / 键盘锁
  的策略键还原 + **IFEO taskkill.exe debugger 劫持解除**
  （极域劫持 taskkill 是远程命令失效的常见根因，本工具发送链不受影响）
- **防火墙阻断 / 恢复极域联网**：`netsh advfirewall` 一条命令，可逆的彻底断控
- **挂起 / 恢复极域进程**（`NtSuspendProcess/NtResumeProcess`）：进程还在、
  教师端显示在线，但管控全失效，随时恢复现场
- **极域路径三级识别**：注册表卸载信息 → 运行进程路径 → 磁盘常见位置扫描
- **左上角热区**（可开关）：广播时鼠标移到屏幕左上角即弹询问，一键缩小广播窗口
- **targets.txt 进程名单**：杀链/挂起的目标进程名外部可编辑
  （默认 StudentMain / NCStu / GATESRV / MasterHelper / jfglzs / prozs / REDAgent），
  换机房改一个文件即可，支持 `#` 注释

### 极域状态面板

"进程与对抗"页每秒刷新：极域是否运行 / PID / 版本 / 安装路径 /
是否正在广播（含黑屏）/ 是否已被本程序挂起。

### 密码工具

- 读取本机极域存储的真实密码（两条注册表路径依次尝试：
  `knock1` 4 字节循环 XOR 解密 + `UninstallPasswd`）
- 复制万能密码 `mythware_super_password`（侧栏常驻提示，点击即复制）
- 内置常见默认密码预测列表

### IP.txt 与本机

- IP.txt 生成器（CIDR 一键展开）、局域网扫描（仅本机 /24 网段，可写入 IP.txt）
- 关掉本机极域（含新版 NCStu.exe）、一键解除 U 盘和网络限制、一键恢复
- 提升自身进程优先级（自保）、op=6 指令包（实验，作用待真机验证）
- 配置包导入导出、日志导出

### 窗口隐藏助手（Boss 键）

- `Alt+B` 隐藏前台窗口 / `Alt+N` 恢复 / `Alt+H` 切换虚拟桌面
- `Alt+C` 隐身本程序（托盘图标双击唤回）
- 已隐藏窗口每 100ms 自动重藏，防止被极域强制重新显示

### 工程特性

- 深色 / 浅色双主题，侧边栏导航，无滚动一屏布局
- **x64 + x86 双构建**（`bin\JiYuKiller.exe` / `bin\x86\JiYuKiller.x86.exe`）
- 托盘图标常驻（监听 `TaskbarCreated`，explorer 重启不丢）
- 单实例互斥、崩溃时写 `crash.log` + MiniDump（dbghelp）、操作日志落盘 `jylog.txt`
- 设置持久化（版本 / 发送目标 / 主题 / 命令历史，`jyconfig.ini`）
- **全程不联网**，无遥测无更新检查
- manifest 为 `asInvoker`，需要权限的操作单独确认，侧栏提供 UAC 自提权重启
- 重要电脑标记（`promise.jy`）、免责声明

## 构建与运行

要求：Windows 7+，.NET Framework 4.8（系统自带），VS Build Tools（仅编译时需要 Roslyn csc）。
无 NuGet 依赖、无需联网。

```
build.cmd          生成 bin\JiYuKiller.exe 与 bin\x86\JiYuKiller.x86.exe
build.cmd test     生成并运行协议回归测试（80+ 项断言）
```

直接运行 `bin\JiYuKiller.exe` 即可，所有 UI 为纯 C# 代码构建（无 XAML 编译依赖）。

## 协议说明

极域客户端通过 UDP 接收 `DMOC` 魔数的教师端指令报文，且不校验来源。
本仓库 `src/JyPackets.g.cs` 内含逐字节还原的报文模板（版本头 / 目标 IP /
操作码 / UTF-16 文本体），`src/JyPackets.cs` 提供按 IP 参数化的构造器
（含 2021 版的 DMOC 帧式解锁包与 op=6 实验指令包）。
字段偏移与操作码的推导过程见各文件注释。

## 路线图

- DLL 代理劫持模式（把极域 `LibTDAjust.dll` 代理后选择性放行"锁定键鼠"调用）：
  需要原生 C 编译工具链产出代理 DLL，计划作为可选模式提供，尚未实现
- 消息报文的字节序变体（不同极域批次存在差异），待真机验证后做成开关