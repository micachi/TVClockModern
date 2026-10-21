using System;
using System.Drawing;

namespace TVClockModern;

/// <summary>
/// 画面外ガードの純粋な計算(UI・マウス・キーボードに依存しない)。
/// MainWindow から呼ばれるほか、--self-test で単体検証できる。
/// </summary>
internal static class ScreenGuard
{
    /// <summary>物理px のウィンドウ矩形(WM_MOVING の RECT と同じ並び)。</summary>
    public struct MoveRect
    {
        public int Left, Top, Right, Bottom;
    }

    /// <summary>
    /// DIP のウィンドウ位置を作業領域内へ収める。
    /// 収めきれない次元(ウィンドウが作業領域より大きい)は左/上端へ寄せる。
    /// </summary>
    public static (double Left, double Top) Clamp(
        Rectangle wa, double dpi, double wDip, double hDip, double left, double top)
    {
        if (dpi <= 0 || wa.Width <= 0 || wa.Height <= 0) return (left, top);

        var wpx = Math.Max(0, (int)Math.Round(wDip * dpi));
        var hpx = Math.Max(0, (int)Math.Round(hDip * dpi));

        var minLeft = wa.Left / dpi;
        var minTop = wa.Top / dpi;
        var maxLeft = Math.Max(wa.Left, wa.Right - wpx) / dpi;
        var maxTop = Math.Max(wa.Top, wa.Bottom - hpx) / dpi;

        return (Math.Min(Math.Max(left, minLeft), maxLeft),
                Math.Min(Math.Max(top, minTop), maxTop));
    }

    /// <summary>
    /// 物理px の移動先矩形を作業領域内へ補正する。補正したら true。
    /// (WM_MOVING 用。DIP を経由しないので DPI 変換誤差が出ない)
    /// </summary>
    public static bool Constrain(Rectangle wa, ref MoveRect r)
    {
        var w = r.Right - r.Left;
        var h = r.Bottom - r.Top;
        if (w <= 0 || h <= 0 || wa.Width <= 0 || wa.Height <= 0) return false;

        var maxLeft = Math.Max(wa.Left, wa.Right - w);
        var maxTop = Math.Max(wa.Top, wa.Bottom - h);
        var nl = Math.Min(Math.Max(r.Left, wa.Left), maxLeft);
        var nt = Math.Min(Math.Max(r.Top, wa.Top), maxTop);
        if (nl == r.Left && nt == r.Top) return false;

        r.Left = nl;
        r.Top = nt;
        r.Right = nl + w;
        r.Bottom = nt + h;
        return true;
    }

    /// <summary>
    /// 自己テスト。ユーザー入力を一切使わずにガード計算だけを検証する。
    /// 結果は %LOCALAPPDATA%\TVClockModern\logs\self-test.txt に書き、0=全PASS / 1=失敗あり を返す。
    /// </summary>
    public static int RunSelfTest(out string reportPath)
    {
        var lines = new System.Collections.Generic.List<string>();
        var failures = 0;

        void Check(string name, bool ok, string detail = "")
        {
            if (!ok) failures++;
            lines.Add($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  [" + detail + "]" : "")}");
        }

        bool Near(double a, double b) => Math.Abs(a - b) < 0.02;

        // --- Clamp(DIP) ---
        var waMain = new Rectangle(0, 0, 3840, 2076);   // 3840x2160 / 175% / 下端にタスクバー
        const double dpi175 = 1.75;
        const double wDip = 1277, hDip = 693;           // 2235x1213 px

        var inside = Clamp(waMain, dpi175, wDip, hDip, 100, 100);
        Check("clamp: inside is unchanged", Near(inside.Left, 100) && Near(inside.Top, 100),
            $"{inside.Left:0.##},{inside.Top:0.##}");

        var above = Clamp(waMain, dpi175, wDip, hDip, 100, -500);
        Check("clamp: top pulled to work-area top", Near(above.Top, 0), $"{above.Top:0.##}");

        var below = Clamp(waMain, dpi175, wDip, hDip, 100, 5000);
        Check("clamp: bottom pulled to work-area bottom", Near(below.Top, 863 / dpi175), $"{below.Top:0.##}");

        var leftEdge = Clamp(waMain, dpi175, wDip, hDip, -800, 100);
        Check("clamp: left pulled to work-area left", Near(leftEdge.Left, 0), $"{leftEdge.Left:0.##}");

        var rightEdge = Clamp(waMain, dpi175, wDip, hDip, 5000, 100);
        Check("clamp: right pulled to work-area right", Near(rightEdge.Left, 1605 / dpi175), $"{rightEdge.Left:0.##}");

        // タスクバーを除いた下端で止まること(画面下端 2160 ではなく 2076)
        var taskbar = Clamp(waMain, dpi175, wDip, hDip, 100, 5000);
        Check("clamp: uses working area, not full screen", taskbar.Top + hDip <= 2076 / dpi175 + 0.02,
            $"top+h={taskbar.Top + hDip:0.##}");

        // 左側にサブモニタ(負の原点)
        var waLeftMonitor = new Rectangle(-1920, 0, 1920, 1040);
        var leftMon = Clamp(waLeftMonitor, 1.0, 200, 100, -2500, -100);
        Check("clamp: secondary monitor at negative origin", Near(leftMon.Left, -1920) && Near(leftMon.Top, 0),
            $"{leftMon.Left:0.##},{leftMon.Top:0.##}");

        // ウィンドウが作業領域より大きい → 左上寄せ
        var waSmall = new Rectangle(0, 0, 1000, 800);
        var oversized = Clamp(waSmall, 1.0, 1200, 100, 50, 50);
        Check("clamp: oversized window pinned to left/top", Near(oversized.Left, 0) && Near(oversized.Top, 50),
            $"{oversized.Left:0.##},{oversized.Top:0.##}");

        // 不正 DPI でも例外にせず素通し
        var badDpi = Clamp(waMain, 0, wDip, hDip, 123, 456);
        Check("clamp: dpi<=0 passes through", Near(badDpi.Left, 123) && Near(badDpi.Top, 456));

        // --- Constrain(物理px / WM_MOVING) ---
        var r1 = new MoveRect { Left = 3700, Top = 1900, Right = 4000, Bottom = 2100 };
        var changed1 = Constrain(waMain, ref r1);
        Check("constrain: bottom-right overflow corrected",
            changed1 && r1.Left == 3540 && r1.Top == 1876 && r1.Right == 3840 && r1.Bottom == 2076,
            $"{r1.Left},{r1.Top},{r1.Right},{r1.Bottom}");

        var r2 = new MoveRect { Left = 10, Top = 10, Right = 300, Bottom = 200 };
        var changed2 = Constrain(waMain, ref r2);
        Check("constrain: inside is unchanged", !changed2 && r2.Left == 10 && r2.Top == 10);

        var r3 = new MoveRect { Left = -50, Top = -50, Right = 1150, Bottom = 50 };
        var changed3 = Constrain(waSmall, ref r3);
        Check("constrain: oversized window pinned to left/top",
            changed3 && r3.Left == 0 && r3.Top == 0 && r3.Right == 1200 && r3.Bottom == 100,
            $"{r3.Left},{r3.Top},{r3.Right},{r3.Bottom}");

        var r4 = new MoveRect { Left = 10, Top = 10, Right = 10, Bottom = 10 };
        var changed4 = Constrain(waMain, ref r4);
        Check("constrain: zero-size rect ignored", !changed4);

        // --- レポート ---
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TVClockModern", "logs");
        System.IO.Directory.CreateDirectory(dir);
        reportPath = System.IO.Path.Combine(dir, "self-test.txt");
        var header = $"ScreenGuard self-test  {DateTime.Now:yyyy-MM-dd HH:mm:ss}  "
                   + $"{lines.Count - failures}/{lines.Count} passed";
        var body = header + Environment.NewLine + string.Join(Environment.NewLine, lines) + Environment.NewLine;
        try { System.IO.File.WriteAllText(reportPath, body); } catch { }
        try { Console.WriteLine(body); } catch { }

        return failures == 0 ? 0 : 1;
    }
}
