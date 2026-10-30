using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TVClockModern;

/// <summary>
/// モダンモードの時計表示。秒は数字で出さず、色の変化で「それとなく」表す。
///  - sweep : 二重円。0秒から濃い色の扇形が広がり、分の頭で全体が薄れて繰り返す
///  - liquid: 背景なし。時刻の文字だけが暗い色から明るい色へ、液面が上がるように満ちていく
/// </summary>
public sealed class ModernClockView : FrameworkElement
{
    public static readonly DependencyProperty TimeTextProperty = DependencyProperty.Register(
        nameof(TimeText), typeof(string), typeof(ModernClockView),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DateTextProperty = DependencyProperty.Register(
        nameof(DateText), typeof(string), typeof(ModernClockView),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>sweep / liquid</summary>
    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
        nameof(Theme), typeof(string), typeof(ModernClockView),
        new FrameworkPropertyMetadata("sweep",
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(
        nameof(FontFamily), typeof(FontFamily), typeof(ModernClockView),
        new FrameworkPropertyMetadata(new FontFamily("Yu Gothic UI"),
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WeightProperty = DependencyProperty.Register(
        nameof(Weight), typeof(FontWeight), typeof(ModernClockView),
        new FrameworkPropertyMetadata(FontWeights.Normal,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextSizeProperty = DependencyProperty.Register(
        nameof(TextSize), typeof(double), typeof(ModernClockView),
        new FrameworkPropertyMetadata(64.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>分の中の進み具合 0..1</summary>
    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        nameof(Fraction), typeof(double), typeof(ModernClockView),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>分の変わり目の残り 0..1(1=分の頭、0=通常)</summary>
    public static readonly DependencyProperty RolloverProperty = DependencyProperty.Register(
        nameof(Rollover), typeof(double), typeof(ModernClockView),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public string TimeText { get => (string)GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }
    public string DateText { get => (string)GetValue(DateTextProperty); set => SetValue(DateTextProperty, value); }
    public string Theme { get => (string)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public FontWeight Weight { get => (FontWeight)GetValue(WeightProperty); set => SetValue(WeightProperty, value); }
    public double TextSize { get => (double)GetValue(TextSizeProperty); set => SetValue(TextSizeProperty, value); }
    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public double Rollover { get => (double)GetValue(RolloverProperty); set => SetValue(RolloverProperty, value); }

    private bool IsLiquid => string.Equals(Theme, "liquid", StringComparison.OrdinalIgnoreCase);

    /// <summary>sweep の円の直径(文字サイズから決める)</summary>
    public static double DiameterFor(double textSize) => Math.Max(140, textSize * 3.9);

    private double Pad => Math.Max(6, TextSize * 0.12);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsLiquid)
        {
            var size = MeasureBlock();
            return new Size(size.Width + Pad * 2, size.Height + Pad * 2);
        }
        var d = DiameterFor(TextSize);
        return new Size(d, d);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 1 || h <= 1) return;

        if (IsLiquid)
        {
            DrawLiquidText(dc);
            return;
        }

        var r = Math.Min(w, h) / 2;
        var c = new Point(w / 2, h / 2);
        DrawSweep(dc, c, r);
        DrawSweepTexts(dc, c);
    }

    // ---------- 文字寸法(共通) ----------
    private Typeface Typeface => new(FontFamily, FontStyles.Normal, Weight, FontStretches.Normal);

    private FormattedText MakeText(string text, double size, Brush brush, double dpi) =>
        new(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface, size, brush, dpi);

    private (double Width, double Height) MeasureBlock()
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var time = MakeText(TimeText, Math.Max(8, TextSize), Brushes.White, dpi);
        var w = time.Width;
        var h = time.Height;
        if (!string.IsNullOrEmpty(DateText))
        {
            var date = MakeText(DateText, Math.Max(8, TextSize * 0.34), Brushes.White, dpi);
            w = Math.Max(w, date.Width);
            h += Math.Max(2, TextSize * 0.08) + date.Height;
        }
        return (w, h);
    }

    // ---------- sweep(二重円 + 扇形の広がり) ----------
    private void DrawSweep(DrawingContext dc, Point c, double r)
    {
        var deep = Color.FromRgb(0x7C, 0x2D, 0x12);
        var amber = Color.FromRgb(0xF5, 0x9E, 0x0B);

        var outer = new SolidColorBrush(deep);
        outer.Freeze();
        dc.DrawEllipse(outer, null, c, r, r);

        var rIn = r * 0.84;
        var inner = new SolidColorBrush(amber);
        inner.Freeze();
        dc.DrawEllipse(inner, null, c, rIn, rIn);

        var sweep = 360.0 * Math.Clamp(Fraction, 0.0, 1.0);
        if (sweep > 0.05)
        {
            var wedge = new SolidColorBrush(deep);
            wedge.Freeze();
            dc.DrawGeometry(wedge, null, PieGeometry(c, rIn, sweep));
        }

        // 分の変わり目: 満タンの濃い色を短時間で薄くする(シームレスな繰り返し)
        var fade = Math.Clamp(Rollover, 0.0, 1.0);
        if (fade > 0.001)
        {
            var b = new SolidColorBrush(Color.FromArgb((byte)(0xE6 * fade), deep.R, deep.G, deep.B));
            b.Freeze();
            dc.DrawEllipse(b, null, c, rIn, rIn);
        }
    }

    private static Geometry PieGeometry(Point c, double r, double sweepDeg)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            var top = new Point(c.X, c.Y - r);
            g.BeginFigure(c, true, true);
            g.LineTo(top, true, false);
            if (sweepDeg >= 359.9)
            {
                var bottom = new Point(c.X, c.Y + r);
                g.ArcTo(bottom, new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                g.ArcTo(top, new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
            }
            else
            {
                var a = (sweepDeg - 90.0) * Math.PI / 180.0;
                var end = new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
                g.ArcTo(end, new Size(r, r), 0, sweepDeg > 180.0, SweepDirection.Clockwise, true, false);
            }
            g.LineTo(c, true, false);
        }
        geo.Freeze();
        return geo;
    }

    private void DrawSweepTexts(DrawingContext dc, Point c)
    {
        if (string.IsNullOrEmpty(TimeText)) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var shadowBrush = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0));
        var time = MakeText(TimeText, Math.Max(8, TextSize), Brushes.White, dpi);
        var timeShadow = MakeText(TimeText, Math.Max(8, TextSize), shadowBrush, dpi);

        FormattedText? date = null;
        FormattedText? dateShadow = null;
        if (!string.IsNullOrEmpty(DateText))
        {
            var dateSize = Math.Max(8, TextSize * 0.34);
            date = MakeText(DateText, dateSize, Brushes.White, dpi);
            dateShadow = MakeText(DateText, dateSize, shadowBrush, dpi);
        }

        var gap = Math.Max(2, TextSize * 0.08);
        var totalH = time.Height + (date is null ? 0 : gap + date.Height);
        var y = c.Y - totalH / 2;
        var off = Math.Max(1, TextSize * 0.035);

        dc.DrawText(timeShadow, new Point(c.X - time.Width / 2 + off, y + off));
        dc.DrawText(time, new Point(c.X - time.Width / 2, y));

        if (date is not null && dateShadow is not null)
        {
            var dy = y + time.Height + gap;
            dc.DrawText(dateShadow, new Point(c.X - date.Width / 2 + off, dy + off));
            dc.DrawText(date, new Point(c.X - date.Width / 2, dy));
        }
    }

    // ---------- liquid(背景なし・文字だけが液面のように満ちる) ----------
    private void DrawLiquidText(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(TimeText)) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var timeSize = Math.Max(8, TextSize);
        var dateSize = Math.Max(8, TextSize * 0.34);
        var gap = Math.Max(2, TextSize * 0.08);
        var off = Math.Max(1, TextSize * 0.035);

        var dark = new SolidColorBrush(Color.FromRgb(0x8C, 0x2B, 0x2B));
        dark.Freeze();
        var shadow = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0));

        var timeDark = MakeText(TimeText, timeSize, dark, dpi);
        var timeBright = MakeText(TimeText, timeSize, Brushes.White, dpi);
        var timeShadow = MakeText(TimeText, timeSize, shadow, dpi);

        FormattedText? dateDark = null, dateBright = null, dateShadow = null;
        if (!string.IsNullOrEmpty(DateText))
        {
            dateDark = MakeText(DateText, dateSize, dark, dpi);
            dateBright = MakeText(DateText, dateSize, Brushes.White, dpi);
            dateShadow = MakeText(DateText, dateSize, shadow, dpi);
        }

        var totalW = Math.Max(timeDark.Width, dateDark?.Width ?? 0);
        var totalH = timeDark.Height + (dateDark is null ? 0 : gap + dateDark.Height);
        var left = (ActualWidth - totalW) / 2;
        var top = (ActualHeight - totalH) / 2;

        // 影(背景が透明なので輪郭を確保する)
        dc.DrawText(timeShadow, new Point(left + (totalW - timeDark.Width) / 2 + off, top + off));
        if (dateShadow is not null)
            dc.DrawText(dateShadow, new Point(left + (totalW - dateShadow.Width) / 2 + off, top + timeDark.Height + gap + off));

        // 暗い色で全体を描く
        dc.DrawText(timeDark, new Point(left + (totalW - timeDark.Width) / 2, top));
        if (dateDark is not null)
            dc.DrawText(dateDark, new Point(left + (totalW - dateDark.Width) / 2, top + timeDark.Height + gap));

        // 液面より下だけ明るい色で上書きする(0秒=暗い / 59秒=全体が明るい)
        var level = ModernClockMath.LiquidLevel(Fraction, Rollover);
        if (level <= 0.001) return;

        var waterY = top + totalH * (1.0 - level);
        var clip = new RectangleGeometry(new Rect(left - 2, waterY, totalW + 4, totalH - (waterY - top) + off + 2));
        clip.Freeze();
        dc.PushClip(clip);
        dc.DrawText(timeBright, new Point(left + (totalW - timeBright.Width) / 2, top));
        if (dateBright is not null)
            dc.DrawText(dateBright, new Point(left + (totalW - dateBright.Width) / 2, top + timeDark.Height + gap));
        dc.Pop();
    }
}
