param(
  [string]$ExePath = "$PSScriptRoot\..\bin\JiYuKiller.exe",
  [string]$ShotPath = "$PSScriptRoot\..\shots\battle_page.png"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$BTN = [System.Windows.Automation.ControlType]::Button
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 3
# 若进程已退出说明又崩了
$proc.Refresh()
if ($proc.HasExited) { Write-Host "FAILED: process exited during startup"; exit 1 }
$c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$win = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $c)
$bc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $BTN)
foreach ($b in $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $bc)) {
  if ($b.Current.Name -like '*同意*') { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); break }
}
Start-Sleep -Seconds 3
# 版本确认对话框: 点确定 (自动检测失败时出现)
$win2 = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $c)
foreach ($b in $win2.FindAll([System.Windows.Automation.TreeScope]::Descendants, $bc)) {
  if ($b.Current.Name -eq '确定') { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Host "version confirmed"; break }
}
Start-Sleep -Seconds 2
$win = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $c)
# 点"进程与对抗"导航
foreach ($b in $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $bc)) {
  if ($b.Current.Name -like '*进程与对抗*') { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); break }
}
Start-Sleep -Seconds 2
$proc.Refresh()
if ($proc.HasExited) { Write-Host "FAILED: process exited after nav"; exit 1 }
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$g.Dispose()
New-Item -ItemType Directory -Force -Path (Split-Path $ShotPath) | Out-Null
$bmp.Save($ShotPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "OK: alive, shot saved"
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
