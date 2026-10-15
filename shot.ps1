param(
  [string]$OutFile = "C:\Users\ukyan\TVClockModern\docs\out.png",
  [ValidateSet("clock", "settings")][string]$Target = "clock"
)
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WCshot {
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
[WCshot]::SetProcessDPIAware() | Out-Null

$best = [IntPtr]::Zero
$bestArea = 0
$cb = [WCshot+EnumProc]{ param($h, $l)
  $sb = New-Object System.Text.StringBuilder 256
  [WCshot]::GetWindowText($h, $sb, 256) | Out-Null
  $t = $sb.ToString()
  $match = $false
  if ($Target -eq "clock" -and $t -eq "TVClock Modern") { $match = $true }
  if ($Target -eq "settings" -and $t -like "*TVClock Modern - *") { $match = $true }
  if ($match -and [WCshot]::IsWindowVisible($h)) {
    $r = New-Object 'WCshot+RECT'
    [WCshot]::GetWindowRect($h, [ref]$r) | Out-Null
    $area = ($r.R - $r.L) * ($r.B - $r.T)
    if ($area -gt $bestArea) { $script:best = $h; $script:bestArea = $area }
  }
  return $true
}
[WCshot]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($best -eq [IntPtr]::Zero) { Write-Output "NOT-FOUND"; exit 1 }

$r = New-Object 'WCshot+RECT'
[WCshot]::GetWindowRect($best, [ref]$r) | Out-Null
$w = $r.R - $r.L; $h = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$dc = $g.GetHdc()
[WCshot]::PrintWindow($best, $dc, 3) | Out-Null
$g.ReleaseHdc($dc)
$bmp.Save($OutFile)
Write-Output ("saved {0} ({1}x{2})" -f $OutFile, $w, $h)
