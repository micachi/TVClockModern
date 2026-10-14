using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WinForms = System.Windows.Forms;

namespace TVClockModern;

public partial class SettingsWindow : Window
{
    private static readonly string[] TimePresets =
    {
        "HH:mm", "HH:mm:ss", "hh:mm tt", "hh:mm:ss tt", "H時m分", "HH:mm:ss tt",
    };
    private static readonly string[] DatePresets =
    {
        "yyyy/MM/dd (ddd)", "yyyy年M月d日(ddd)", "M/d ddd", "yyyy-MM-dd", "ddd M月d日", "",
    };

    private bool _ready;

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => { LoadFromSettings(); _ready = true; UpdatePreview(); };
    }

    private sealed record MonitorItem(string Name, string Device)
    {
        public override string ToString() => Name;
    }

    private void LoadMonitorList()
    {
        var items = new List<MonitorItem> { new("自動 (現在の位置)", "") };
        try
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            for (var i = 0; i < screens.Length; i++)
            {
                var sc = screens[i];
                var primary = sc.Primary ? " [メイン]" : "";
                items.Add(new MonitorItem($"{i + 1}: {sc.Bounds.Width}x{sc.Bounds.Height}{primary}", sc.DeviceName));
            }
        }
        catch (Exception ex)
        {
            Logger.Error("モニタ列挙に失敗", ex);
        }

        MonitorBox.ItemsSource = items;
        var sel = items.FirstOrDefault(m => m.Device == App.Settings.TargetMonitor) ?? items[0];
        MonitorBox.SelectedItem = sel;
    }

    private void LoadFromSettings()
    {
        var s = App.Settings;

        LoadMonitorList();

        TimeFormatBox.ItemsSource = TimePresets;
        TimeFormatBox.Text = s.TimeFormat;

        var dateItems = new List<string>(DatePresets);
        if (!dateItems.Contains(s.DateFormat)) dateItems.Insert(0, s.DateFormat);
        DateFormatBox.ItemsSource = dateItems;
        DateFormatBox.Text = s.DateFormat;

        FontBox.ItemsSource = System.Windows.Media.Fonts.SystemFontFamilies
            .Select(f => f.Source).OrderBy(x => x, StringComparer.CurrentCulture).ToList();
        FontBox.SelectedItem = s.FontFamily;
        if (FontBox.SelectedItem is null) FontBox.Text = s.FontFamily;

        SizeSlider.Value = Math.Clamp(s.FontSize, 12, 200);
        LetterSpacingSlider.Value = s.LetterSpacing;
        SeparatorSpacingSlider.Value = s.SeparatorSpacing;
        TransitionBox.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("none", "なし"),
            new("flipx", "横に回転"),
            new("flipy", "縦に回転"),
            new("slideup", "せり上げ"),
            new("slidedown", "せり下げ"),
            new("fade", "フェードイン・アウト"),
            new("crossfade", "クロスフェード"),
        };
        TransitionBox.SelectedValue = s.TransitionEffect;
        TransitionDurationSlider.Value = Math.Clamp(s.TransitionDurationMs, 50, 1000);
        OpacitySlider.Value = s.WindowOpacity;
        ShadowCheck.IsChecked = s.TextShadow;
        ShadowBlurSlider.Value = s.ShadowBlur;
        ShadowOpacitySlider.Value = s.ShadowOpacity;
        OutlineCheck.IsChecked = s.OutlineEnabled;
        OutlineThicknessSlider.Value = Math.Clamp(s.OutlineThickness, 0.5, 10);
        ShowDateCheck.IsChecked = s.ShowDate;
        TopmostCheck.IsChecked = s.Topmost;
        ClickThroughCheck.IsChecked = s.ClickThrough;
        ModifierBox.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("Ctrl", "Ctrl"),
            new("Alt", "Alt"),
            new("Shift", "Shift"),
            new("None", "なし"),
        };
        ModifierBox.SelectedValue = s.ModifierKey;
        AutoStartCheck.IsChecked = s.AutoStart;

        ProximityCheck.IsChecked = s.ProximityEnabled;
        AllMonitorsCheck.IsChecked = s.ShowOnAllMonitors;
        ProximitySlider.Value = Math.Clamp(s.ProximityDistance, 10, 400);
        ProximityOpacitySlider.Value = s.ProximityOpacity;
        // 速度1(遅い/1000ms)〜10(速い/30ms)をミリ秒に逆マップ
        FadeSpeedSlider.Value = Math.Clamp((int)Math.Round((1110 - s.FadeDurationMs) / 108.0), 1, 10);
        FadeEasingBox.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("linear", "リニア (等速)"),
            new("easeout", "減速 (ぬるっと着地)"),
            new("easein", "加速 (素早く始まる)"),
        };
        FadeEasingBox.SelectedValue = s.FadeEasing;

        UpdateColorPreview(ColorPreview, s.TextColor);
        UpdateColorPreview(OutlineColorPreview, s.OutlineColor);
        UpdatePreview();
    }

    // ---------- カラーダイアログ ----------
    private void OnPickColor(object sender, RoutedEventArgs e)
    {
        if (PickColor(App.Settings.TextColor) is { } hex)
        {
            UpdateColorPreview(ColorPreview, hex);
            UpdatePreview();
        }
    }

    private void OnPickOutlineColor(object sender, RoutedEventArgs e)
    {
        if (PickColor(App.Settings.OutlineColor) is { } hex)
        {
            UpdateColorPreview(OutlineColorPreview, hex);
            UpdatePreview();
        }
    }

    private static string? PickColor(string current)
    {
        using var dlg = new WinForms.ColorDialog
        {
            FullOpen = true,
            Color = ToWinFormsColor(current),
        };
        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return null;
        var c = dlg.Color;
        return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private static System.Drawing.Color ToWinFormsColor(string hex)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch { return System.Drawing.Color.White; }
    }

    private string CurrentColorHex(Rectangle preview)
    {
        if (preview.Fill is SolidColorBrush b) return b.Color.ToString();
        return "#FFFFFF";
    }

    private static void UpdateColorPreview(Rectangle preview, string hex)
    {
        try { preview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { preview.Fill = Brushes.White; }
    }

    // ---------- プレビュー ----------
    private void UpdatePreview()
    {
        // 初期化中(XAMLのイベント発火時)は何もしない
        if (!_ready || PreviewTimeHost is null || PreviewDateHost is null) return;

        var now = DateTime.Now;
        var family = FontBox.Text is { Length: > 0 } f ? new FontFamily(f) : new FontFamily("Yu Gothic UI");
        var size = SizeSlider.Value;
        var mainColor = ParseColor(CurrentColorHex(ColorPreview), Colors.White);
        var outlineColor = ParseColor(CurrentColorHex(OutlineColorPreview), Colors.Black);
        var outlineThickness = OutlineCheck.IsChecked == true ? OutlineThicknessSlider.Value : 0;
        var shadow = ShadowCheck.IsChecked == true;

        var timeStr = SafeFormat(now, TimeFormatBox.Text, "HH:mm");
        var df = DateFormatBox.Text ?? "";
        var dateStr = string.IsNullOrWhiteSpace(df) || ShowDateCheck.IsChecked != true ? "" : SafeFormat(now, df, "yyyy/MM/dd (ddd)");

        BuildPreviewText(PreviewTimeHost, timeStr, family, size, mainColor, outlineColor, outlineThickness, shadow);
        BuildPreviewText(PreviewDateHost, dateStr, family, Math.Max(12, size * 0.35), mainColor, outlineColor, outlineThickness, shadow);
        PreviewDateHost.Visibility = dateStr.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        // 数値ラベル
        SizeValue.Text = ((int)SizeSlider.Value).ToString();
        LetterSpacingValue.Text = LetterSpacingSlider.Value.ToString("0");
        SeparatorSpacingValue.Text = SeparatorSpacingSlider.Value.ToString("0");
        TransitionDurationValue.Text = TransitionDurationSlider.Value.ToString("0ms");
        OpacityValue.Text = OpacitySlider.Value.ToString("0.00");
        OutlineThicknessValue.Text = OutlineThicknessSlider.Value.ToString("0.0");
        ShadowBlurValue.Text = ShadowBlurSlider.Value.ToString("0");
        ShadowOpacityValue.Text = ShadowOpacitySlider.Value.ToString("0.00");
        ProximityValue.Text = ((int)ProximitySlider.Value).ToString();
        ProximityOpacityValue.Text = ProximityOpacitySlider.Value.ToString("0.00");
        FadeSpeedValue.Text = ((int)FadeSpeedSlider.Value).ToString();
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private void BuildPreviewText(Grid host, string text, FontFamily family, double size,
        Color mainColor, Color outlineColor, double outlineThickness, bool shadow)
    {
        host.Children.Clear();
        if (outlineThickness > 0)
        {
            var ob = new SolidColorBrush(outlineColor);
            ob.Freeze();
            double[,] dirs = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 }, { -1, -1 }, { 1, -1 }, { -1, 1 }, { 1, 1 } };
            for (var i = 0; i < dirs.GetLength(0); i++)
            {
                var tb = new TextBlock { Text = text, FontFamily = family, FontSize = size, Foreground = ob };
                tb.RenderTransform = new TranslateTransform(dirs[i, 0] * outlineThickness, dirs[i, 1] * outlineThickness);
                host.Children.Add(tb);
            }
        }
        var mb = new SolidColorBrush(mainColor);
        mb.Freeze();
        var main = new TextBlock { Text = text, FontFamily = family, FontSize = size, Foreground = mb };
        if (shadow)
        {
            main.Effect = new DropShadowEffect
            {
                BlurRadius = ShadowBlurSlider?.Value ?? 6,
                ShadowDepth = Math.Min(8, (ShadowBlurSlider?.Value ?? 6) / 4),
                Opacity = ShadowOpacitySlider?.Value ?? 0.85,
                Color = Colors.Black,
            };
        }
        host.Children.Add(main);
    }

    private static string SafeFormat(DateTime dt, string format, string fallback)
    {
        try { return dt.ToString(format ?? fallback); }
        catch { return dt.ToString(fallback); }
    }

    // ---------- 適用 / OK / キャンセル ----------
    private void SaveToSettings()
    {
        var s = App.Settings;
        s.TimeFormat = string.IsNullOrWhiteSpace(TimeFormatBox.Text) ? "HH:mm" : TimeFormatBox.Text;
        s.DateFormat = DateFormatBox.Text ?? "";
        s.FontFamily = string.IsNullOrWhiteSpace(FontBox.Text) ? "Yu Gothic UI" : FontBox.Text;
        s.FontSize = SizeSlider.Value;
        s.LetterSpacing = LetterSpacingSlider.Value;
        s.SeparatorSpacing = SeparatorSpacingSlider.Value;
        s.TransitionEffect = TransitionBox.SelectedValue as string ?? "crossfade";
        s.TransitionDurationMs = (int)TransitionDurationSlider.Value;
        s.WindowOpacity = OpacitySlider.Value;
        s.TextShadow = ShadowCheck.IsChecked == true;
        s.ShadowBlur = ShadowBlurSlider.Value;
        s.ShadowOpacity = ShadowOpacitySlider.Value;
        s.OutlineEnabled = OutlineCheck.IsChecked == true;
        s.OutlineColor = CurrentColorHex(OutlineColorPreview);
        s.OutlineThickness = OutlineThicknessSlider.Value;
        s.ShowDate = ShowDateCheck.IsChecked == true;
        s.Topmost = TopmostCheck.IsChecked == true;
        s.ClickThrough = ClickThroughCheck.IsChecked == true;
        s.ModifierKey = ModifierBox.SelectedValue as string ?? "Ctrl";
        s.TargetMonitor = (MonitorBox.SelectedItem as MonitorItem)?.Device ?? "";
        s.ShowOnAllMonitors = AllMonitorsCheck.IsChecked == true;
        s.TextColor = CurrentColorHex(ColorPreview);
        s.AutoStart = AutoStartCheck.IsChecked == true;

        s.ProximityEnabled = ProximityCheck.IsChecked == true;
        s.ProximityDistance = (int)ProximitySlider.Value;
        s.ProximityOpacity = ProximityOpacitySlider.Value;
        s.FadeDurationMs = Math.Clamp(1110 - (int)FadeSpeedSlider.Value * 108, 30, 1000);
        s.FadeEasing = FadeEasingBox.SelectedValue as string ?? "easeout";
    }

    private void ApplyToClock()
    {
        try
        {
            App.EnsureClocks();      // モニタ同時表示の ON/OFF・増減を同期
            App.ApplyToAllClocks();
        }
        catch { }
    }

    private void OnPreset(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string preset)
        {
            SaveToSettings();
            App.ApplyPositionPreset(preset);
        }
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        SaveToSettings();
        ApplyAutoStart(App.Settings.AutoStart);
        ApplyToClock();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        SaveToSettings();
        ApplyAutoStart(App.Settings.AutoStart);
        ApplyToClock();
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    /// <summary>HKCU\...\Run への登録。失敗してもクラッシュしない。</summary>
    private static void ApplyAutoStart(bool enable)
    {
        try
        {
            const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string valueName = "TVClockModern";
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, writable: true)
                ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath);
            if (enable)
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                    key.SetValue(valueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("自動起動設定の変更に失敗", ex);
        }
    }

    private void OnPreviewChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdatePreview();

    private void OnPreviewChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => UpdatePreview();

    private void OnPreviewChanged(object sender, RoutedEventArgs e)
        => UpdatePreview();
}
