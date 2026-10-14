param([string]$OutFile = "C:\Users\ukyan\TVClockModern\settings-screenshot.png")
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class WC {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  public struct RECT { public int L, T, R, B; }
};
'@
[WC]::SetProcessDPIAware() | Out-Null
$p = Get-Process TVClockModern -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output "NO_WINDOW"; exit 1 }
$hwnd = $p.MainWindowHandle
# 最前面化
[WC]::SetWindowPos($hwnd, [IntPtr](-1), 0,0,0,0, 0x0003) | Out-Null  # NOSIZE|NOMOVE
Start-Sleep -Milliseconds 600
$r = New-Object 'WC+RECT'
[WC]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.R - $r.L; $h = $r.B - $r.T
Write-Output ("rect: {0},{1} {2}x{3}" -f $r.L, $r.T, $w, $h)
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
$bmp.Save($OutFile)
Write-Output "saved: $OutFile"
