param([string]$OutFile = "C:\Users\ukyan\TVClockModern\ver-check.png")
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Windows.Forms,System.Drawing
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WCv {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
  public struct RECT { public int L, T, R, B; }
};
'@
[WCv]::SetProcessDPIAware() | Out-Null

$root = [System.Windows.Automation.AutomationElement]::RootElement
$nameCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "TVClock Modern - 設定")
$dlg = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $nameCond)
if ($null -eq $dlg) { Write-Host "DIALOG-NOT-FOUND"; exit 1 }
$tabCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "情報")
$tab = $dlg.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $tabCond)
if ($null -ne $tab) {
  $sip = $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
  $sip.Select()
  Write-Host "info tab selected"
} else { Write-Host "tab not found" }
Start-Sleep -Milliseconds 800

$found = [IntPtr]::Zero
$cb = [WCv+EnumProc]{ param($h, $l)
  $sb = New-Object System.Text.StringBuilder 256
  [WCv]::GetWindowText($h, $sb, 256) | Out-Null
  if ($sb.ToString() -like "*TVClock Modern - *" -and [WCv]::IsWindowVisible($h)) { $script:found = $h; return $false }
  return $true
}
[WCv]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($found -eq [IntPtr]::Zero) { Write-Host "WINDOW-NOT-FOUND"; exit 1 }
$r = New-Object 'WCv+RECT'
[WCv]::GetWindowRect($found, [ref]$r) | Out-Null
$w = $r.R - $r.L; $h = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$dc = $g.GetHdc()
[WCv]::PrintWindow($found, $dc, 3) | Out-Null
$g.ReleaseHdc($dc)
$bmp.Save($OutFile)
Write-Host ("saved {0}x{1}" -f $w, $h)
