using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TVClockModern;

/// <summary>
/// モダンモードの時計表示。秒は数字で出さず、色の変化で「それとなく」表す。
///  - sweep : 二重円。0秒から濃い色の扇形が広がり、分の頭で全体が薄れて繰り返す
///  - liquid: 背景なし。**分の文字だけ**が暗い色から明るい色へ、液面が上がるように満ちる
/// </summary>
public sealed class ModernClockView : FrameworkElement
{
    public static readonly DependencyProperty TimeTextProperty = DependencyProperty.Register(
        nameof(TimeText), typeof(string), typeof(ModernClockView),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    // リキッド用: 時刻を「分の前 / 分 / 後」に分割したもの(分だけを変化させるため)
    public static readonly DependencyProperty TimePrefixProperty = DependencyProperty.Register(
        nameof(TimePrefix), typeof(string), typeof(ModernClockView),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TimeMinuteProperty = DependencyProperty.Register(
        nameof(TimeMinute), typeof(string), typeof(ModernClockView),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TimeSuffixProperty = DependencyProperty.Register(
        nameof(TimeSuffix), typeof(string), typeof(ModernClockView),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

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

    // --- モダンの色(両テーマ共通) ---
    public static readonly DependencyProperty ModernDarkColorProperty = DependencyProperty.Register(
        nameof(ModernDarkColor), typeof(string), typeof(ModernClockView),
        new FrameworkPropertyMetadata("#FF7C2D12", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ModernLightColorProperty = DependencyProperty.Register(
        nameof(ModernLightColor), typeof(string), typeof(ModernClockView),
        new FrameworkPropertyMetadata("#FFF59E0B", FrameworkPropertyMetadataOptions.AffectsRender));

    public string TimeText { get => (string)GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }
    public string TimePrefix { get => (string)GetValue(TimePrefixProperty); set => SetValue(TimePrefixProperty, value); }
    public string TimeMinute { get => (string)GetValue(TimeMinuteProperty); set => SetValue(TimeMinuteProperty, value); }
    public string TimeSuffix { get => (string)GetValue(TimeSuffixProperty); set => SetValue(TimeSuffixProperty, value); }
    public string DateText { get => (string)GetValue(DateTextProperty); set => SetValue(DateTextProperty, value); }
    public string Theme { get => (string)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public FontWeight Weight { get => (FontWeight)GetValue(WeightProperty); set => SetValue(WeightProperty, value); }
    public double TextSize { get => (double)GetValue(TextSizeProperty); set => SetValue(TextSizeProperty, value); }
    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public double Rollover { get => (double)GetValue(RolloverProperty); set => SetValue(RolloverProperty, value); }
    public string ModernDarkColor { get => (string)GetValue(ModernDarkColorProperty); set => SetValue(ModernDarkColorProperty, value); }
    public string ModernLightColor { get => (string)GetValue(ModernLightColorProperty); set => SetValue(ModernLightColorProperty, value); }

    private bool IsLiquid => string.Equals(Theme, "liquid", StringComparison.OrdinalIgnoreCase);

    /// <summary>sweep の円の直径(文字サイズから決める)</summary>
    public static double DiameterFor(double textSize) => Math.Max(140, textSize * 3.9);

    private double Pad => Math.Max(6, TextSize * 0.12);

    private static Color ParseColor(string? hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex ?? ""); }
        catch { return fallback; }
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

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
        var white = Brushes.White;

        double lineW = 0, lineH = 0;
        foreach (var part in new[] { TimePrefix, TimeMinute, TimeSuffix })
        {
            var t = MakeText(part, Math.Max(8, TextSize), white, dpi);
            lineW += t.Width;
            lineH = Math.Max(lineH, t.Height);
        }
        if (lineW <= 0)
        {
            var t = MakeText(TimeText, Math.Max(8, TextSize), white, dpi);
            lineW = t.Width;
            lineH = t.Height;
        }

        var w = lineW;
        var h = lineH;
        if (!string.IsNullOrEmpty(DateText))
        {
            var date = MakeText(DateText, Math.Max(8, TextSize * 0.34), white, dpi);
            w = Math.Max(w, date.Width);
            h += Math.Max(2, TextSize * 0.08) + date.Height;
        }
        return (w, h);
    }

    // ---------- sweep(二重円 + 扇形の広がり) ----------
    private void DrawSweep(DrawingContext dc, Point c, double r)
    {
        var deep = ParseColor(ModernDarkColor, Color.FromRgb(0x7C, 0x2D, 0x12));
        var light = ParseColor(ModernLightColor, Color.FromRgb(0xF5, 0x9E, 0x0B));

        dc.DrawEllipse(Frozen(deep), null, c, r, r);

        var rIn = r * 0.84;
        dc.DrawEllipse(Frozen(light), null, c, rIn, rIn);

        var sweep = 360.0 * Math.Clamp(Fraction, 0.0, 1.0);
        if (sweep > 0.05)
            dc.DrawGeometry(Frozen(deep), null, PieGeometry(c, rIn, sweep));

        // 分の変わり目: 満タンの濃い色を短時間で薄くする(シームレスな繰り返し)
        var fade = Math.Clamp(Rollover, 0.0, 1.0);
        if (fade > 0.001)
            dc.DrawEllipse(Frozen(Color.FromArgb((byte)(0xE6 * fade), deep.R, deep.G, deep.B)), null, c, rIn, rIn);
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

    // ---------- liquid(背景なし・分の文字だけが液面のように満ちる) ----------
    private void DrawLiquidText(DrawingContext dc)
    {
        var dark = ParseColor(ModernDarkColor, Color.FromRgb(0x7C, 0x2D, 0x12));
        var bright = ParseColor(ModernLightColor, Color.FromRgb(0xF5, 0x9E, 0x0B));

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var timeSize = Math.Max(8, TextSize);
        var dateSize = Math.Max(8, TextSize * 0.34);
        var gap = Math.Max(2, TextSize * 0.08);
        var off = Math.Max(1, TextSize * 0.035);

        var darkBrush = Frozen(dark);
        var brightBrush = Frozen(bright);
        var shadow = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0));

        var prefix = MakeText(TimePrefix, timeSize, brightBrush, dpi);
        var minuteDark = MakeText(TimeMinute, timeSize, darkBrush, dpi);
        var minuteBright = MakeText(TimeMinute, timeSize, brightBrush, dpi);
        var suffix = MakeText(TimeSuffix, timeSize, brightBrush, dpi);

        // 分の指定子が無い書式なら、時刻全体を固定色で出す(変化させる対象が無い)
        if (string.IsNullOrEmpty(TimeMinute))
        {
            prefix = MakeText(TimeText, timeSize, brightBrush, dpi);
            minuteDark = MakeText("", timeSize, darkBrush, dpi);
            minuteBright = MakeText("", timeSize, brightBrush, dpi);
            suffix = MakeText("", timeSize, brightBrush, dpi);
        }

        var lineW = prefix.Width + minuteDark.Width + suffix.Width;
        var lineH = Math.Max(prefix.Height, Math.Max(minuteDark.Height, suffix.Height));
        var date = string.IsNullOrEmpty(DateText) ? null : MakeText(DateText, dateSize, brightBrush, dpi);
        var dateShadow = date is null ? null : MakeText(DateText, dateSize, shadow, dpi);

        var totalW = Math.Max(lineW, date?.Width ?? 0);
        var totalH = lineH + (date is null ? 0 : gap + date.Height);
        var left = (ActualWidth - totalW) / 2;
        var top = (ActualHeight - totalH) / 2;
        var x = left + (totalW - lineW) / 2;

        // 影(背景が透明なので輪郭を確保する)
        var shadowParts = new[] { MakeText(TimePrefix, timeSize, shadow, dpi), MakeText(TimeMinute, timeSize, shadow, dpi), MakeText(TimeSuffix, timeSize, shadow, dpi) };
        if (string.IsNullOrEmpty(TimeMinute))
            shadowParts[0] = MakeText(TimeText, timeSize, shadow, dpi);
        var sx = x;
        foreach (var sp in shadowParts)
        {
            dc.DrawText(sp, new Point(sx + off, top + off));
            sx += sp.Width;
        }
        if (dateShadow is not null)
            dc.DrawText(dateShadow, new Point(left + (totalW - dateShadow.Width) / 2 + off, top + lineH + gap + off));

        // 分以外(時・区切り・午前/午後・日付)は固定色
        dc.DrawText(prefix, new Point(x, top));
        var minX = x + prefix.Width;
        var minW = minuteDark.Width;
        dc.DrawText(suffix, new Point(minX + minW, top));
        if (date is not null)
            dc.DrawText(date, new Point(left + (totalW - date.Width) / 2, top + lineH + gap));

        if (minW <= 0) return;

        // 分の文字: 暗い色で描いてから、液面より下だけ明るい色で上書きする
        dc.DrawText(minuteDark, new Point(minX, top));

        var level = ModernClockMath.LiquidLevel(Fraction, Rollover);
        if (level <= 0.001) return;

        var waterY = top + minuteDark.Height * (1.0 - level);
        var clip = new RectangleGeometry(new Rect(minX - 1, waterY, minW + 2, minuteDark.Height - (waterY - top) + off + 2));
        clip.Freeze();
        dc.PushClip(clip);
        dc.DrawText(minuteBright, new Point(minX, top));
        dc.Pop();
    }
}
