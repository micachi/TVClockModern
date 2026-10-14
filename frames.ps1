param([string]$Dir = "C:\Users\ukyan\TVClockModern\frames")
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WCf {
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
[WCf]::SetProcessDPIAware() | Out-Null

$best = [IntPtr]::Zero
$bestArea = 0
$cb = [WCf+EnumProc]{ param($h, $l)
  $sb = New-Object System.Text.StringBuilder 256
  [WCf]::GetWindowText($h, $sb, 256) | Out-Null
  if ($sb.ToString() -eq "TVClock Modern" -and [WCf]::IsWindowVisible($h)) {
    $r = New-Object 'WCf+RECT'
    [WCf]::GetWindowRect($h, [ref]$r) | Out-Null
    $area = ($r.R - $r.L) * ($r.B - $r.T)
    if ($area -gt $bestArea) { $script:best = $h; $script:bestArea = $area }
  }
  return $true
}
[WCf]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($best -eq [IntPtr]::Zero) { Write-Output "NOT_FOUND"; exit 1 }

$r = New-Object 'WCf+RECT'
[WCf]::GetWindowRect($best, [ref]$r) | Out-Null
$w = $r.R - $r.L; $h = $r.B - $r.T

# 秒の境目を待つ
$nowMs = [DateTime]::Now.Millisecond
Start-Sleep -Milliseconds (1000 - $nowMs + 50)

for ($i = 0; $i -lt 8; $i++) {
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $dc = $g.GetHdc()
  [WCf]::PrintWindow($best, $dc, 3) | Out-Null
  $g.ReleaseHdc($dc)
  $bmp.Save(("{0}\f{1}.png" -f $Dir, $i))
  $bmp.Dispose()
  Start-Sleep -Milliseconds 90
}
Write-Output "captured 8 frames"
