param(
  [string]$ExePath = "$PSScriptRoot/../bin/JiYuKiller.exe",
  [string]$ShotPath = "$PSScriptRoot/../shots/16_boss_e2e.png"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -Namespace W -Name N -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
public delegate bool EnumProc(IntPtr h, IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder sb, int n);
public static System.Collections.Generic.List<IntPtr> TopsOfPid(int pid) {
  var r = new System.Collections.Generic.List<IntPtr>();
  EnumWindows(delegate(IntPtr h, IntPtr l) { uint p; GetWindowThreadProcessId(h, out p); if (p == (uint)pid) r.Add(h); return true; }, IntPtr.Zero);
  return r;
}
'@

$BTN = [System.Windows.Automation.ControlType]::Button
function Get-Wins($proc) {
  $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
  return [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $c)
}
function Click-In($win, [string]$like) {
  $bc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $BTN)
  $btns = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $bc)
  Write-Host ("  buttons found: " + $btns.Count)
  foreach ($b in $btns) {
    if ($b.Current.Name -like ("*" + $like + "*")) { Write-Host ("  clicking: " + $b.Current.Name); $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); return $true }
  }
  return $false
}
function Find-TopByTitle([int]$pidNum, [string]$contains) {
  foreach ($h in [W.N]::TopsOfPid($pidNum)) {
    $sb = New-Object System.Text.StringBuilder 256
    [W.N]::GetWindowText($h, $sb, 256) | Out-Null
    if ($sb.ToString().Contains($contains)) { return $h }
  }
  return [IntPtr]::Zero
}

# 1) 主程序: 同意
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 3
foreach ($w in (Get-Wins $proc)) { if (Click-In $w "同意") { break } }
Start-Sleep -Seconds 3

# 2) 打开助手窗口并启动热键
foreach ($w in (Get-Wins $proc)) {
  if ($w.Current.Name -eq "JiYu Killer") { Click-In $w "隐藏" | Out-Null; break }
}
Start-Sleep -Seconds 2
$bossHwnd = Find-TopByTitle $proc.Id ([string][char]0x7A97 + [string][char]0x53E3)
Write-Host ("boss hwnd: " + $bossHwnd)
foreach ($h in [W.N]::TopsOfPid($proc.Id)) {
  $sb = New-Object System.Text.StringBuilder 256
  [W.N]::GetWindowText($h, $sb, 256) | Out-Null
  Write-Host ("  top: [" + $sb.ToString() + "]")
}
if ($bossHwnd -ne [IntPtr]::Zero) {
  $bossEl = [System.Windows.Automation.AutomationElement]::FromHandle($bossHwnd)
  if (-not (Click-In $bossEl "启动")) { Write-Host "start button not found" }
}
Start-Sleep -Seconds 1

# 3) 记事本置前
$np = Start-Process 'C:/Windows/System32/notepad.exe' -PassThru
Start-Sleep -Seconds 2
$np.Refresh()
[W.N]::SetForegroundWindow($np.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 500
$visibleBefore = [W.N]::IsWindowVisible($np.MainWindowHandle)

# 4) Alt+B 隐藏 -> 5) Alt+N 恢复
[System.Windows.Forms.SendKeys]::SendWait("%b")
Start-Sleep -Milliseconds 900
$visibleHidden = [W.N]::IsWindowVisible($np.MainWindowHandle)
[System.Windows.Forms.SendKeys]::SendWait("%n")
Start-Sleep -Milliseconds 900
$visibleRestored = [W.N]::IsWindowVisible($np.MainWindowHandle)
Write-Host ("RESULT notepad before=" + $visibleBefore + " afterAltB=" + $visibleHidden + " afterAltN=" + $visibleRestored)

# 6) 截全屏(主窗口日志可见)
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$g.Dispose()
$bmp.Save($ShotPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "shot: $ShotPath"
Get-Process notepad -ErrorAction SilentlyContinue | Stop-Process -Force
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Host done
