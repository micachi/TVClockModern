param(
  [string]$OutFile = "C:\Users\ukyan\TVClockModern\docs\gif",
  [int]$Frames = 12,
  [int]$IntervalMs = 70
)
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WCgif {
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
[WCgif]::SetProcessDPIAware() | Out-Null

$best = [IntPtr]::Zero
$bestArea = 0
$cb = [WCgif+EnumProc]{ param($h, $l)
  $sb = New-Object System.Text.StringBuilder 256
  [WCgif]::GetWindowText($h, $sb, 256) | Out-Null
  if ($sb.ToString() -eq "TVClock Modern" -and [WCgif]::IsWindowVisible($h)) {
    $r = New-Object 'WCgif+RECT'
    [WCgif]::GetWindowRect($h, [ref]$r) | Out-Null
    $area = ($r.R - $r.L) * ($r.B - $r.T)
    if ($area -gt $bestArea) { $script:best = $h; $script:bestArea = $area }
  }
  return $true
}
[WCgif]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($best -eq [IntPtr]::Zero) { Write-Output "NOT-FOUND"; exit 1 }

$r = New-Object 'WCgif+RECT'
[WCgif]::GetWindowRect($best, [ref]$r) | Out-Null
$w = $r.R - $r.L; $h = $r.B - $r.T

# 秒の境目の直前まで待機
while ([DateTime]::Now.Millisecond -lt 700) { Start-Sleep -Milliseconds 20 }

for ($i = 0; $i -lt $Frames; $i++) {
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $dc = $g.GetHdc()
  [WCgif]::PrintWindow($best, $dc, 3) | Out-Null
  $g.ReleaseHdc($dc)
  $bmp.Save(("{0}\f{1:d2}.png" -f $OutFile, $i))
  $bmp.Dispose()
  Start-Sleep -Milliseconds $IntervalMs
}
Write-Output "captured $Frames frames"
