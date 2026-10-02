param(
  [string]$ExePath = "$PSScriptRoot/../bin/JiYuKiller.exe"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -Namespace W -Name N -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
'@
$BTN = [System.Windows.Automation.ControlType]::Button
function Get-MainWin($proc) {
  $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
  return [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $c)
}
function Click-In($win, [string]$like) {
  $bc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $BTN)
  foreach ($b in $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $bc)) {
    if ($b.Current.Name -like ("*" + $like + "*")) {
      try { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); return $true }
      catch {
        try { $b.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); return $true }
        catch { try { $b.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); return $true } catch {} }
      }
    }
  }
  return $false
}
function Shot([string]$name) {
  Start-Sleep -Milliseconds 600
  $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
  $g.Dispose()
  $bmp.Save("$PSScriptRoot/../shots/" + $name, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host "shot: $name"
}
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 3
$win = Get-MainWin $proc
Start-Sleep -Milliseconds 500
Shot "19_sidebar_pwd.png"
# 点侧栏关于(精确名)
foreach ($b in $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $bc)) {
  if ($b.Current.Name -eq "关于") { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); break }
}
Start-Sleep -Milliseconds 900
Shot "20_about_silent.png"
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 500
# 切到工具页
Click-In $win "IP.txt" $BTN | Out-Null
Start-Sleep -Milliseconds 900
Shot "21_tools.png"
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Host done
