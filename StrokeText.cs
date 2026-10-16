using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TVClockModern;

/// <summary>
/// 縁取り付きテキストを 1 要素で描画するコントロール。
/// 旧実装(本体+8方向コピーの最大9レイヤーの TextBlock)を Geometry+Pen の
/// 一筆描きに置き換え、視覚結果を維持したまま視覚要素数を
/// 文字数×9 から文字数×1 へ削減する。
/// ペンはグリフ輪郭上に中心合わせされるため、幅は縁取り太さの 2 倍で
/// 塗ることで「外側に太さ分」のはみ出しを旧実装と同じにする。
/// </summary>
internal sealed class StrokeText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(StrokeText),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(
        nameof(Font), typeof(FontFamily), typeof(StrokeText),
        new FrameworkPropertyMetadata(new FontFamily("Yu Gothic UI"), FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize), typeof(double), typeof(StrokeText),
        new FrameworkPropertyMetadata(64.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure),
        v => (double)v > 0);

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(StrokeText),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OutlineBrushProperty = DependencyProperty.Register(
        nameof(OutlineBrush), typeof(Brush), typeof(StrokeText),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OutlineThicknessProperty = DependencyProperty.Register(
        nameof(OutlineThickness), typeof(double), typeof(StrokeText),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender), v => (double)v >= 0);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FontFamily Font
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush? OutlineBrush
    {
        get => (Brush?)GetValue(OutlineBrushProperty);
        set => SetValue(OutlineBrushProperty, value);
    }

    public double OutlineThickness
    {
        get => (double)GetValue(OutlineThicknessProperty);
        set => SetValue(OutlineThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;

        // 縁取り: 輪郭ペンは中心合わせなので幅2倍で外側に太さ分だけはみ出させる。
        // Round join で角を丸く(旧8方向コピーの継ぎ目より滑らか)。
        if (OutlineThickness > 0 && OutlineBrush is not null)
        {
            var geometry = CreateFormattedText().BuildGeometry(new Point(0, 0));
            if (geometry is not null)
            {
                var pen = new Pen(OutlineBrush, OutlineThickness * 2) { LineJoin = PenLineJoin.Round };
                dc.DrawGeometry(null, pen, geometry);
            }
        }

        // 塗りはテキストラスタライザで描く(TextBlock と同じAA/ガンマ。ジオメトリ塗りは
        // エッジの AA 性格が変わるため避ける)
        dc.DrawText(CreateFormattedText(), new Point(0, 0));
    }

    private FormattedText CreateFormattedText()
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        return new FormattedText(
            Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            FontSize,
            Fill ?? Brushes.Transparent,
            dpi);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (string.IsNullOrEmpty(Text)) return new Size(0, 0);
        var ft = CreateFormattedText();
        // 整数化しない(TextBlock と同じ分数幅。Ceil すると累積位置がずれ、
        // 後続文字のサブピクセル配置が変わって AA が変わる)
        return new Size(ft.WidthIncludingTrailingWhitespace, ft.Height);
    }
}
