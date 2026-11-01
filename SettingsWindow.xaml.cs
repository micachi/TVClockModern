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
    private System.Windows.Threading.DispatcherTimer? _previewTimer;

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
        lblDisplayMode.Text = L.T("label.displayMode");
        lblModernTheme.Text = L.T("label.modernTheme");
        lblModernColors.Text = L.T("label.modernColors");
        ModernDarkButton.Content = L.T("btn.pick");
        ModernLightButton.Content = L.T("btn.pick");
        lblDateFormat.Text = L.T("label.dateFormat");
        ShowDateCheck.Content = L.T("check.showDate");
        lblTransitionEffect.Text = L.T("label.transitionEffect");
        lblTransitionDuration.Text = L.T("label.transitionDuration");

        lblFont.Text = L.T("label.font");
        lblFontSize.Text = L.T("label.fontSize");
        BoldCheck.Content = L.T("check.bold");
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
        KeepOnScreenCheck.Content = L.T("check.keepOnScreen");
        KeepOnScreenCheck.ToolTip = L.T("tooltip.keepOnScreen");
        lblModifierKey.Text = L.T("label.modifierKey");
        lblModifierKey.ToolTip = L.T("tooltip.modifierKey");
        AutoStartCheck.Content = L.T("check.autostart");
        CheckUpdatesCheck.Content = L.T("check.updates");
        lblLanguage.Text = L.T("label.language");
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
        lblUpdateSection.Text = L.T("label.updateSection");
        CheckUpdateButton.Content = L.T("btn.checkUpdate");
        OpenReleaseButton.Content = L.T("btn.openRelease");
        ExportButton.Content = L.T("btn.export");
        ImportButton.Content = L.T("btn.import");
        lblSettingsSavedTo.Text = L.T("label.settingsSavedTo");
        lblDisclaimer.Text = L.T("label.disclaimer");
        lblDisclaimer1.Text = L.T("text.disclaimer1");
        lblDisclaimer2.Text = L.T("text.disclaimer2");
        lblPreview.Text = L.T("label.preview");
        lblPreviewBg.Text = L.T("label.previewBg");
        PreviewBgButton.Content = L.T("btn.pick");
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
        // プレビューの時刻を毎秒更新(実時計と同じ表示に保つ)
        _previewTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _previewTimer.Tick += (_, _) => UpdatePreview();
        _previewTimer.Start();
        Closed += (_, _) => _previewTimer.Stop();
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

        SizeSlider.Value = Math.Clamp(s.FontSize, 12, 500);
        BoldCheck.IsChecked = s.FontWeight == "bold";
        LetterSpacingSlider.Value = s.LetterSpacing;
        SeparatorSpacingSlider.Value = s.SeparatorSpacing;
        ModeBox.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("classic", L.T("mode.classic")),
            new("modern", L.T("mode.modern")),
        };
        ModeBox.SelectedValue = s.DisplayMode;
        ThemeBox.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("sweep", L.T("theme.sweep")),
            new("liquid", L.T("theme.liquid")),
        };
        ThemeBox.SelectedValue = s.ModernTheme;
        UpdateModernVisibility();

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
        KeepOnScreenCheck.IsChecked = s.KeepOnScreen;
        ModifierBox.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("Ctrl", "Ctrl"),
            new("Alt", "Alt"),
            new("Shift", "Shift"),
            new("None", L.T("modifier.none")),
        };
        ModifierBox.SelectedValue = s.ModifierKey;
        AutoStartCheck.IsChecked = s.AutoStart;
        CheckUpdatesCheck.IsChecked = s.CheckUpdates;

        LanguageBox.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("auto", L.T("combo.languageAuto")),
            new("ja", "日本語"),
            new("en", "English"),
            new("zh-Hans", "简体中文"),
            new("ko", "한국어"),
        };
        LanguageBox.SelectedValue = s.Language;
        UpdateUpdateStatus(App.NewVersion is not null ? "known" : "idle");

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
        UpdateColorPreview(PreviewBgPreview, s.PreviewBackground);
        UpdateColorPreview(ModernDarkPreview, s.ModernDark);
        UpdateColorPreview(ModernLightPreview, s.ModernLight);
        UpdatePreviewBackground(s.PreviewBackground);
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

    // ---------- モダンの色(両テーマ共通) ----------
    private void OnPickModernDark(object sender, RoutedEventArgs e) => PickInto(ModernDarkPreview);
    private void OnPickModernLight(object sender, RoutedEventArgs e) => PickInto(ModernLightPreview);

    private void PickInto(Rectangle swatch)
    {
        if (PickColor(CurrentColorHex(swatch)) is { } hex)
        {
            UpdateColorPreview(swatch, hex);
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

    /// <summary>プレビュー黒枠の背景を設定する(不正値は既定の中間グレーへフォールバック)。</summary>
    private void UpdatePreviewBackground(string hex)
    {
        try { PreviewBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { PreviewBorder.Background = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)); }
    }

    private void OnPickPreviewBg(object sender, RoutedEventArgs e)
    {
        if (PickColor(App.Settings.PreviewBackground) is { } hex)
        {
            UpdateColorPreview(PreviewBgPreview, hex);
            UpdatePreviewBackground(hex);
        }
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

        // モダンモード: 円形の専用ビューでプレビューする
        if (string.Equals(ModeBox.SelectedValue as string, "modern", StringComparison.OrdinalIgnoreCase))
        {
            var theme = ThemeBox.SelectedValue as string ?? "sweep";
            PreviewTimeHost.Children.Clear();
            PreviewDateHost.Visibility = Visibility.Collapsed;

            var modernFmt = (TimeFormatBox.Text ?? "").IndexOf('s') >= 0 ? "HH:mm" : (TimeFormatBox.Text ?? "HH:mm");
            var (pfx, min, sfx) = ModernClockMath.SplitMinute(now, modernFmt);
            PreviewTimeHost.Children.Add(new ModernClockView
            {
                Theme = theme,
                FontFamily = family,
                Weight = BoldCheck.IsChecked == true ? FontWeights.Bold : FontWeights.Normal,
                TextSize = size,
                TimeText = SafeFormat(now, modernFmt, "HH:mm"),
                TimePrefix = pfx,
                TimeMinute = min,
                TimeSuffix = sfx,
                DateText = dateStr,
                ModernDarkColor = CurrentColorHex(ModernDarkPreview),
                ModernLightColor = CurrentColorHex(ModernLightPreview),
                Fraction = ModernClockMath.Fraction(now),
                Rollover = ModernClockMath.Rollover(now.Second + now.Millisecond / 1000.0,
                    string.Equals(theme, "liquid", StringComparison.OrdinalIgnoreCase) ? 0.45 : 0.7),
            });
        }
        else
        {
            BuildPreviewText(PreviewTimeHost, timeStr, family, size, mainColor, outlineColor, outlineThickness, shadow);
            BuildPreviewText(PreviewDateHost, dateStr, family, Math.Max(12, size * 0.35), mainColor, outlineColor, outlineThickness, shadow);
            PreviewDateHost.Visibility = dateStr.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // 数値欄(スライダーと双方向。入力中の欄は上書きしない)
        SetValueText(SizeValue, SizeSlider.Value);
        SetValueText(LetterSpacingValue, LetterSpacingSlider.Value);
        SetValueText(SeparatorSpacingValue, SeparatorSpacingSlider.Value);
        SetValueText(TransitionDurationValue, TransitionDurationSlider.Value);
        SetValueText(OpacityValue, OpacitySlider.Value);
        SetValueText(OutlineThicknessValue, OutlineThicknessSlider.Value);
        SetValueText(ShadowBlurValue, ShadowBlurSlider.Value);
        SetValueText(ShadowOpacityValue, ShadowOpacitySlider.Value);
        SetValueText(ProximityValue, ProximitySlider.Value);
        SetValueText(ProximityOpacityValue, ProximityOpacitySlider.Value);
        SetValueText(FadeSpeedValue, FadeSpeedSlider.Value);
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private void BuildPreviewText(StackPanel host, string text, FontFamily family, double size,
        Color mainColor, Color outlineColor, double outlineThickness, bool shadow)
    {
        var weight = BoldCheck.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
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
                Weight = weight,
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

    // ---------- 更新確認 ----------
    private bool _updateChecking;

    /// <summary>情報タブの更新ステータス表示を更新する。</summary>
    private void UpdateUpdateStatus(string state)
    {
        var v = App.NewVersion;
        if (v is not null)
        {
            lblUpdateStatus.Text = L.T("msg.updateAvailable", v);
            OpenReleaseButton.Visibility = Visibility.Visible;
        }
        else if (state == "done")
        {
            lblUpdateStatus.Text = L.T("msg.upToDate");
            OpenReleaseButton.Visibility = Visibility.Collapsed;
        }
        else if (state == "failed")
        {
            lblUpdateStatus.Text = L.T("msg.updateCheckFailed");
            OpenReleaseButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            lblUpdateStatus.Text = "";
            OpenReleaseButton.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        if (_updateChecking) return;
        _updateChecking = true;
        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = L.T("btn.checking");
        lblUpdateStatus.Text = "";

        string? newTag = null;
        var failed = false;
        try
        {
            newTag = await UpdateChecker.GetLatestVersionAsync();
            if (newTag is not null) App.NewVersion = newTag;
        }
        catch (Exception ex)
        {
            failed = true;
            Logger.Error("更新確認に失敗", ex);
        }

        // 記録は UI スレッドで(settings.json の保存競合を避ける)
        UpdateChecker.MarkChecked();
        if (newTag is not null) App.Settings.PendingUpdate = newTag;

        _updateChecking = false;
        CheckUpdateButton.IsEnabled = true;
        CheckUpdateButton.Content = L.T("btn.checkUpdate");
        UpdateUpdateStatus(failed ? "failed" : "done");
    }

    private void OnOpenRelease(object sender, RoutedEventArgs e) => App.OpenReleasePage();

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
        s.FontWeight = BoldCheck.IsChecked == true ? "bold" : "normal";
        s.LetterSpacing = LetterSpacingSlider.Value;
        s.SeparatorSpacing = SeparatorSpacingSlider.Value;
        s.TransitionEffect = TransitionBox.SelectedValue as string ?? "crossfade";
        s.DisplayMode = ModeBox.SelectedValue as string ?? "classic";
        s.ModernTheme = ThemeBox.SelectedValue as string ?? "sweep";
        s.TransitionDurationMs = (int)TransitionDurationSlider.Value;
        s.WindowOpacity = OpacitySlider.Value;
        s.TextShadow = ShadowCheck.IsChecked == true;
        s.ShadowBlur = ShadowBlurSlider.Value;
        s.ShadowOpacity = ShadowOpacitySlider.Value;
        s.OutlineEnabled = OutlineCheck.IsChecked == true;
        s.OutlineColor = CurrentColorHex(OutlineColorPreview);
        s.ModernDark = CurrentColorHex(ModernDarkPreview);
        s.ModernLight = CurrentColorHex(ModernLightPreview);
        s.OutlineThickness = OutlineThicknessSlider.Value;
        s.ShowDate = ShowDateCheck.IsChecked == true;
        s.Topmost = TopmostCheck.IsChecked == true;
        s.ClickThrough = ClickThroughCheck.IsChecked == true;
        s.KeepOnScreen = KeepOnScreenCheck.IsChecked == true;
        s.ModifierKey = ModifierBox.SelectedValue as string ?? "Ctrl";
        s.TargetMonitor = (MonitorBox.SelectedItem as MonitorItem)?.Device ?? "";
        s.ShowOnAllMonitors = AllMonitorsCheck.IsChecked == true;
        s.TextColor = CurrentColorHex(ColorPreview);
        s.AutoStart = AutoStartCheck.IsChecked == true;
        s.CheckUpdates = CheckUpdatesCheck.IsChecked == true;
        s.PreviewBackground = CurrentColorHex(PreviewBgPreview);

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
            // インポートで言語が変わっていた場合はここでも即時反映
            if (L.CurrentLanguage != App.Settings.Language)
                App.ApplyLanguage();
            LoadFromSettings(); // ダイアログUIも新しい設定に同期
            Logger.Info($"設定をインポート: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            Logger.Error("設定インポートに失敗", ex);
            MessageBox.Show(this, L.T("msg.importFailed", ex.Message), "TVClock Modern");
        }
    }

    private void ApplyAll()
    {
        var langBefore = L.CurrentLanguage;
        SaveToSettings();
        ApplyAutoStart(App.Settings.AutoStart);
        if (App.Settings.Language != langBefore)
        {
            // 言語変更: 時計ウィンドウを建て直し、トレイ・自分自身も即時再ローカライズ
            App.ApplyLanguage();
        }
        else
        {
            ApplyToClock();
        }
        ApplyLocalization();
        LoadFromSettings();
    }

    private void OnApply(object sender, RoutedEventArgs e) => ApplyAll();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        ApplyAll();
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    /// <summary>HKCU Run への登録/解除。実体は App.SetAutoStart。</summary>
    private static void ApplyAutoStart(bool enable) => App.SetAutoStart(enable);

    private void OnPreviewChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdatePreview();

    /// <summary>表示モードが変わったとき: モダン専用の設定の表示/非表示を切り替える。</summary>
    private void OnModeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateModernVisibility();
        UpdatePreview();
    }

    /// <summary>モダン専用の設定(テーマ・色)は、クラシック選択時には隠す。</summary>
    private void UpdateModernVisibility()
    {
        var modern = string.Equals(ModeBox.SelectedValue as string, "modern", StringComparison.OrdinalIgnoreCase);
        var v = modern ? Visibility.Visible : Visibility.Collapsed;

        lblModernTheme.Visibility = v;
        ThemeBox.Visibility = v;
        lblModernColors.Visibility = v;
        ModernColorRow.Visibility = v;
    }

    private void OnPreviewChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => UpdatePreview();

    private void OnPreviewChanged(object sender, RoutedEventArgs e)
        => UpdatePreview();

    // ---------- 数値入力欄 ----------
    // スライダー横の数値表示を直接入力できる欄にしたもの。
    // 欄の名前は「<スライダー名>Value」(例: SizeValue ⇔ SizeSlider)で対応付ける。

    private Slider? SliderFor(TextBox box) =>
        FindName(box.Name.Replace("Value", "Slider")) as Slider;

    private static string FormatValue(string boxName, double value) => boxName switch
    {
        "OutlineThicknessValue" => value.ToString("0.0"),
        "OpacityValue" or "ShadowOpacityValue" or "ProximityOpacityValue" => value.ToString("0.00"),
        _ => value.ToString("0"),
    };

    /// <summary>スライダー値を数値欄へ反映する。入力中の欄は書き換えない(毎秒のプレビュー更新で入力を壊さない)。</summary>
    private static void SetValueText(TextBox? box, double value)
    {
        if (box is null || box.IsKeyboardFocused) return;
        box.Text = FormatValue(box.Name, value);
    }

    private void OnValueBoxCommit(object sender, RoutedEventArgs e) => CommitValueBox(sender as TextBox);

    private void OnValueBoxFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox box) box.SelectAll();
    }

    private void OnValueBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not TextBox box) return;
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            CommitValueBox(box);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (SliderFor(box) is { } s) box.Text = FormatValue(box.Name, s.Value);
            box.SelectAll();
            e.Handled = true;
        }
    }

    /// <summary>入力された数値をスライダーへ反映する(範囲外はスライダーの範囲へクランプ、不正な文字列は元の値へ戻す)。</summary>
    private void CommitValueBox(TextBox? box)
    {
        if (box is null || SliderFor(box) is not { } slider) return;

        var text = (box.Text ?? "").Trim();
        if (double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture, out var v) ||
            double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v))
        {
            // 整数表示の欄は整数へ丸める(表示と実際の値がずれないように)
            var isInteger = box.Name is not ("OutlineThicknessValue" or "OpacityValue"
                or "ShadowOpacityValue" or "ProximityOpacityValue");
            if (isInteger) v = Math.Round(v, MidpointRounding.AwayFromZero);
            slider.Value = Math.Clamp(v, slider.Minimum, slider.Maximum);
        }

        box.Text = FormatValue(box.Name, slider.Value);
        UpdatePreview();
    }
}
