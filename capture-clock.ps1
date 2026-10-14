param([string]$OutFile = "C:\Users\ukyan\TVClockModern\clock-check.png")
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WC7 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
  public struct RECT { public int L, T, R, B; }
};
'@

$found = [IntPtr]::Zero
$cb = [WC7+EnumProc]{ param($h, $l)
  if ([WC7]::IsWindowVisible($h)) {
    $sb = New-Object System.Text.StringBuilder 256
    [WC7]::GetWindowText($h, $sb, 256) | Out-Null
    if ($sb.ToString() -eq "TVClock Modern") { $script:found = $h; return $false }
  }
  return $true
}
[WC7]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($found -eq [IntPtr]::Zero) { Write-Output "NOT_FOUND"; exit 1 }

$r = New-Object 'WC7+RECT'
[WC7]::GetWindowRect($found, [ref]$r) | Out-Null
$w = $r.R - $r.L; $h = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$dc = $g.GetHdc()
[WC7]::PrintWindow($found, $dc, 3) | Out-Null
$g.ReleaseHdc($dc)
$bmp.Save($OutFile)
Write-Output ("saved {0}x{1}" -f $w, $h)
