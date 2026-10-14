param([string]$OutFile = "C:\Users\ukyan\TVClockModern\clock-check.png")
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WCapt {
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
[WCapt]::SetProcessDPIAware() | Out-Null

$best = [IntPtr]::Zero
$bestArea = 0
$cb = [WCapt+EnumProc]{ param($h, $l)
  $sb = New-Object System.Text.StringBuilder 256
  [WCapt]::GetWindowText($h, $sb, 256) | Out-Null
  if ($sb.ToString() -eq "TVClock Modern" -and [WCapt]::IsWindowVisible($h)) {
    $r = New-Object 'WCapt+RECT'
    [WCapt]::GetWindowRect($h, [ref]$r) | Out-Null
    $area = ($r.R - $r.L) * ($r.B - $r.T)
    if ($area -gt $bestArea) { $script:best = $h; $script:bestArea = $area }
  }
  return $true
}
[WCapt]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($best -eq [IntPtr]::Zero) { Write-Output "NOT_FOUND"; exit 1 }

$r = New-Object 'WCapt+RECT'
[WCapt]::GetWindowRect($best, [ref]$r) | Out-Null
$w = $r.R - $r.L; $h = $r.B - $r.T
Write-Output ("capturing {0}x{1}" -f $w, $h)
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$dc = $g.GetHdc()
[WCapt]::PrintWindow($best, $dc, 3) | Out-Null
$g.ReleaseHdc($dc)
$bmp.Save($OutFile)
Write-Output "saved"
