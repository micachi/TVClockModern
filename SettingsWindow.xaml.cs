using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
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

    /// <summary>指定タブキー(format/chars/deco/behavior/info)のタブを選択する(情報タブへのリンク等)。</summary>
    public void SelectTab(string tabKey)
    {
        foreach (var item in SettingsTabControl.Items.OfType<TabItem>())
        {
            if ((string)item.Tag == tabKey)
            {
                item.IsSelected = true;
                return;
            }
        }
    }

    /// <summary>全 UI 文字列を現在の言語で設定(App.OnStartup で L.Init 済み)。</summary>
    private void ApplyLocalization()
    {
        Title = "TVClock Modern - " + L.T("settings.title");
        FormatTab.Header = L.T("tab.format");
        CharsTab.Header = L.T("tab.chars");
        DecoTab.Header = L.T("tab.deco");
        BehaviorTab.Header = L.T("tab.behavior");
        InfoTab.Header = L.T("tab.info");

        lblTimeFormat.Text = L.T("label.timeFormat");
        lblDateFormat.Text = L.T("label.dateFormat");
        ShowDateCheck.Content = L.T("check.showDate");
        lblTransitionEffect.Text = L.T("label.transitionEffect");
        lblTransitionDuration.Text = L.T("label.transitionDuration");

        lblFont.Text = L.T("label.font");
        lblFontSize.Text = L.T("label.fontSize");
        lblTextColor.Text = L.T("label.textColor");
        ColorButton.Content = L.T("btn.pick");
        lblLetterSpacing.Text = L.T("label.letterSpacing");
        lblSepSpacing.Text = L.T("label.sepSpacing");

        lblOutline.Text = L.T("label.outline");
        OutlineCheck.Content = L.T("check.outline");
        lblOutlineColor.Text = L.T("label.color");
        OutlineColorButton.Content = L.T("btn.pick");
        lblOutlineThickness.Text = L.T("label.thickness");
        lblShadow.Text = L.T("label.shadow");
        ShadowCheck.Content = L.T("check.shadow");
        lblShadowBlur.Text = L.T("label.size");
        lblShadowDensity.Text = L.T("label.density");

        lblMonitor.Text = L.T("label.monitor");
        AllMonitorsCheck.Content = L.T("check.allMonitors");
        AllMonitorsCheck.ToolTip = L.T("tooltip.allMonitors");
        lblPositionPreset.Text = L.T("label.positionPreset");
        PresetTl.Content = L.T("preset.topleft");
        PresetTr.Content = L.T("preset.topright");
        PresetBl.Content = L.T("preset.bottomleft");
        PresetBr.Content = L.T("preset.bottomright");
        PresetC.Content = L.T("preset.center");
        notePreset.Text = L.T("note.preset");
        lblWindowOpacity.Text = L.T("label.windowOpacity");
        TopmostCheck.Content = L.T("check.topmost");
        ClickThroughCheck.Content = L.T("check.clickthrough");
        ClickThroughCheck.ToolTip = L.T("tooltip.clickthrough");
        lblModifierKey.Text = L.T("label.modifierKey");
        lblModifierKey.ToolTip = L.T("tooltip.modifierKey");
        AutoStartCheck.Content = L.T("check.autostart");
        lblLanguage.Text = L.T("label.language");
        noteLanguage.Text = L.T("note.language");
        lblProximity.Text = L.T("label.proximity");
        ProximityCheck.Content = L.T("check.proximity");
        lblProximityDistance.Text = L.T("label.proximityDistance");
        lblProximityOpacity.Text = L.T("label.proximityOpacity");
        lblFadeSpeed.Text = L.T("label.fadeSpeed");
        lblFadeCurve.Text = L.T("label.fadeCurve");

        lblAppDesc.Text = L.T("label.appDesc");
        lblVersion.Text = L.T("label.version");
        lblAuthor.Text = L.T("label.author");
        lblLicense.Text = L.T("label.license");
        lblSettingsFile.Text = L.T("label.settingsFile");
        lblLogs.Text = L.T("label.logs");
        lblSettingsTransfer.Text = L.T("label.settingsTransfer");
        ExportButton.Content = L.T("btn.export");
        ImportButton.Content = L.T("btn.import");
        lblSettingsSavedTo.Text = L.T("label.settingsSavedTo");
        lblDisclaimer.Text = L.T("label.disclaimer");
        lblDisclaimer1.Text = L.T("text.disclaimer1");
        lblDisclaimer2.Text = L.T("text.disclaimer2");
        lblPreview.Text = L.T("label.preview");
        OkButton.Content = L.T("btn.ok");
        CancelButton.Content = L.T("btn.cancel");
        ApplyButton.Content = L.T("btn.apply");
    }

    /// <summary>ウィンドウ サイズを表示先モニターの作業領域に収める(#10)。</summary>
    private void ClampToWorkArea()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // 物理px → DIP へ変換して上限にする
            var maxW = screen.WorkingArea.Width / dpi;
            var maxH = screen.WorkingArea.Height / dpi;

            MaxWidth = Math.Max(MinWidth, maxW - 16);
            MaxHeight = Math.Max(MinHeight, maxH - 16);
            if (ActualHeight > MaxHeight) Height = MaxHeight;
            if (ActualWidth > MaxWidth) Width = MaxWidth;
        }
        catch (Exception ex)
        {
            Logger.Error("作業領域へのクランプに失敗", ex);
        }
    }

    /// <summary>編集可能な書式コンボの TextBox に TextChanged を接続する(#9)。</summary>
    private void HookEditableFormatBoxes()
    {
        foreach (var combo in new[] { TimeFormatBox, DateFormatBox })
        {
            if (combo.Template?.FindName("PART_EditableTextBox", combo) is TextBox tb)
                tb.TextChanged += (_, _) => UpdatePreview();
        }
    }

    public SettingsWindow()
    {
        InitializeComponent();
        ApplyLocalization();
        SourceInitialized += (_, _) => ClampToWorkArea();

        // 情報タブ: バージョン表示
        try
        {
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = ver is null ? "v1.0.0" : $"v{ver.Major}.{ver.Minor}.{ver.Build}";
        }
        catch { VersionText.Text = "v1.0.0"; }
        Loaded += (_, _) =>
        {
            ClampToWorkArea();
            HookEditableFormatBoxes();
            LoadFromSettings();
            _ready = true;
            UpdatePreview();
            if (App.PendingSettingsTab.Length > 0)
            {
                SelectTab(App.PendingSettingsTab);
                App.PendingSettingsTab = "";
            }
        };
        LocationChanged += (_, _) => ClampToWorkArea();
    }

    private sealed record MonitorItem(string Name, string Device)
    {
        public override string ToString() => Name;
    }

    private void LoadMonitorList()
    {
        var items = new List<MonitorItem> { new(L.T("monitor.auto"), "") };
        try
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            for (var i = 0; i < screens.Length; i++)
            {
                var sc = screens[i];
                var primary = sc.Primary ? L.T("monitor.main") : "";
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
            new("none", L.T("effect.none")),
            new("flipx", L.T("effect.flipx")),
            new("flipy", L.T("effect.flipy")),
            new("slideup", L.T("effect.slideup")),
            new("slidedown", L.T("effect.slidedown")),
            new("fade", L.T("effect.fade")),
            new("crossfade", L.T("effect.crossfade")),
        };
        TransitionBox.SelectedValue = s.TransitionEffect;
        TransitionDurationSlider.Value = Math.Clamp(s.TransitionDurationMs, 50, 500);
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
            new("None", L.T("modifier.none")),
        };
        ModifierBox.SelectedValue = s.ModifierKey;
        AutoStartCheck.IsChecked = s.AutoStart;

        LanguageBox.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("auto", L.T("combo.languageAuto")),
            new("ja", "日本語"),
            new("en", "English"),
            new("zh-Hans", "简体中文"),
            new("ko", "한국어"),
        };
        LanguageBox.SelectedValue = s.Language;

        ProximityCheck.IsChecked = s.ProximityEnabled;
        AllMonitorsCheck.IsChecked = s.ShowOnAllMonitors;
        ProximitySlider.Value = Math.Clamp(s.ProximityDistance, 10, 400);
        ProximityOpacitySlider.Value = s.ProximityOpacity;
        // 速度1(遅い/1000ms)〜10(速い/30ms)をミリ秒に逆マップ
        FadeSpeedSlider.Value = Math.Clamp((int)Math.Round((1110 - s.FadeDurationMs) / 108.0), 1, 10);
        FadeEasingBox.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("linear", L.T("easing.linear")),
            new("easeout", L.T("easing.easeout")),
            new("easein", L.T("easing.easein")),
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

    private void BuildPreviewText(StackPanel host, string text, FontFamily family, double size,
        Color mainColor, Color outlineColor, double outlineThickness, bool shadow)
    {
        host.Children.Clear();
        var letterSp = LetterSpacingSlider?.Value ?? 0;
        var sepSp = SeparatorSpacingSlider?.Value ?? 0;

        // 縁取り込みの StrokeText を並べる(時計本体と同一の描画方式)
        var layerHost = new Grid();
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        if (shadow)
        {
            panel.Effect = new DropShadowEffect
            {
                BlurRadius = ShadowBlurSlider?.Value ?? 6,
                ShadowDepth = Math.Min(8, (ShadowBlurSlider?.Value ?? 6) / 4),
                Opacity = ShadowOpacitySlider?.Value ?? 0.85,
                Color = Colors.Black,
            };
        }

        var mb = new SolidColorBrush(mainColor);
        mb.Freeze();
        Brush? ob = null;
        if (outlineThickness > 0)
        {
            var obSolid = new SolidColorBrush(outlineColor);
            obSolid.Freeze();
            ob = obSolid;
        }

        for (var i = 0; i < text.Length; i++)
        {
            panel.Children.Add(new StrokeText
            {
                Text = text[i].ToString(),
                Font = family,
                FontSize = size,
                Fill = mb,
                OutlineBrush = ob,
                OutlineThickness = outlineThickness,
                Margin = CharSpacing.GetCharMargin(text[i], i, text.Length, letterSp, sepSp),
            });
        }

        layerHost.Children.Add(panel);
        host.Children.Add(layerHost);
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
        var invalid = new List<string>();

        // 書式検証(#3): 不正な書式は既存設定を保持し、その旨を通知する
        var tf = string.IsNullOrWhiteSpace(TimeFormatBox.Text) ? "HH:mm" : TimeFormatBox.Text;
        if (ClockSettings.IsValidFormat(tf)) s.TimeFormat = tf;
        else invalid.Add(L.T("invalid.timeFormat", TimeFormatBox.Text));

        var df = DateFormatBox.Text ?? "";
        if (df.Length == 0 || ClockSettings.IsValidFormat(df)) s.DateFormat = df;
        else invalid.Add(L.T("invalid.dateFormat", df));

        if (invalid.Count > 0)
        {
            var detail = string.Join("\n", invalid);
            MessageBox.Show(this, L.T("msg.invalidFormats", detail),
                "TVClock Modern", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

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
        s.Language = LanguageBox.SelectedValue as string ?? "auto";
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

    private void OnNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Error("リンクを開けませんでした", ex);
        }
    }

    private void OnPreset(object sender, RoutedEventArgs e)
    {
        // #11: 位置プリセットは「位置のみ」を即時反映する。
        // 他の未適用の編集(色・自動起動・全モニタ表示など)は SaveToSettings しない。
        if (sender is Button b && b.Tag is string preset)
            App.ApplyPositionPreset(preset);
    }

    // ---------- エクスポート / インポート ----------
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private void OnExport(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = L.T("export.defaultName"),
                Filter = L.T("filter.files"),
                DefaultExt = ".json",
            };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(App.Settings, JsonOpts));
            Logger.Info($"設定をエクスポート: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            Logger.Error("設定エクスポートに失敗", ex);
            MessageBox.Show(this, L.T("msg.exportFailed", ex.Message), "TVClock Modern");
        }
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = L.T("filter.files"),
            };
            if (dlg.ShowDialog() != true) return;

            var imported = JsonSerializer.Deserialize<ClockSettings>(File.ReadAllText(dlg.FileName), JsonOpts);
            if (imported is null) throw new InvalidDataException(L.T("msg.importEmpty"));
            imported.Sanitize();

            App.Settings = imported;
            App.Settings.Save();
            ApplyAutoStart(App.Settings.AutoStart);
            ApplyToClock();
            LoadFromSettings(); // ダイアログUIも新しい設定に同期
            Logger.Info($"設定をインポート: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            Logger.Error("設定インポートに失敗", ex);
            MessageBox.Show(this, L.T("msg.importFailed", ex.Message), "TVClock Modern");
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

    /// <summary>HKCU Run への登録/解除。実体は App.SetAutoStart。</summary>
    private static void ApplyAutoStart(bool enable) => App.SetAutoStart(enable);

    private void OnPreviewChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdatePreview();

    private void OnPreviewChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => UpdatePreview();

    private void OnPreviewChanged(object sender, RoutedEventArgs e)
        => UpdatePreview();
}
