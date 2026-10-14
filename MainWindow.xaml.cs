using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace TVClockModern;

public partial class MainWindow : Window
{
    private readonly string _monitorDevice; // マルチモニタ同時表示時の担当デバイス(""=単一モード)

    /// <summary>担当モニタのデバイス名(単一モードは "")</summary>
    public string MonitorDevice => _monitorDevice;
    private readonly ContextMenu ClockMenu = new();

    private DispatcherTimer? _timer;
    private bool _exStyleApplied;

    // 縁取り用のコピー(本体 + 8方向)
    private readonly List<TextBlock> _timeOutline = new();
    private TextBlock? _timeMain;
    private readonly List<TextBlock> _dateOutline = new();
    private TextBlock? _dateMain;

    public MainWindow(string monitorDevice = "")
    {
        _monitorDevice = monitorDevice;
        InitializeComponent();

        Topmost = App.Settings.Topmost;
        Left = App.Settings.Left;
        Top = App.Settings.Top;
        Opacity = App.Settings.WindowOpacity;

        RebuildTextVisuals();
        ApplyClickThrough();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            // Alt-Tab / タスク一覧に出さない(ガジェット的挙動)
            var ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, ex | NativeMethods.WS_EX_TOOLWINDOW);
            _exStyleApplied = true;
        };

        BuildContextMenu();

        Loaded += OnLoaded;
        SizeChanged += OnSizeChangedForPreset;
        Closing += (_, _) => SavePosition();
        Closed += (_, _) =>
        {
            Logger.Info($"時計ウィンドウClosed: monitor={_monitorDevice}");
            App.Clocks.Remove(this);
        };
    }

    // ---------- テキスト描画(縁取り+影) ----------
    private void RebuildTextVisuals()
    {
        var s = App.Settings;
        var family = new FontFamily(s.FontFamily);
        var textColor = ParseColor(s.TextColor, Colors.White);
        var outlineColor = ParseColor(s.OutlineColor, Colors.Black);

        BuildHost(TimeHost, _timeOutline, ref _timeMain, family, s.FontSize, textColor, outlineColor);
        var dateSize = Math.Max(12, s.FontSize * 0.35);
        BuildHost(DateHost, _dateOutline, ref _dateMain, family, dateSize, textColor, outlineColor);
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static void BuildHost(Grid host, List<TextBlock> outlines, ref TextBlock? main,
        FontFamily family, double size, Color mainColor, Color outlineColor)
    {
        host.Children.Clear();
        outlines.Clear();

        var s = App.Settings;
        var thickness = s.OutlineEnabled ? Math.Clamp(s.OutlineThickness, 0.5, 20) : 0;

        if (thickness > 0)
        {
            var ob = new SolidColorBrush(outlineColor);
            ob.Freeze();
            double[,] dirs = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 }, { -1, -1 }, { 1, -1 }, { -1, 1 }, { 1, 1 } };
            for (var i = 0; i < dirs.GetLength(0); i++)
            {
                var tb = new TextBlock { FontFamily = family, FontSize = size, Foreground = ob };
                tb.RenderTransform = new TranslateTransform(dirs[i, 0] * thickness, dirs[i, 1] * thickness);
                host.Children.Add(tb);
                outlines.Add(tb);
            }
        }

        var mb = new SolidColorBrush(mainColor);
        mb.Freeze();
        main = new TextBlock { FontFamily = family, FontSize = size, Foreground = mb };
        host.Children.Add(main);

        ApplyShadowTo(main, s);
    }

    private static void ApplyShadowTo(TextBlock? tb, ClockSettings s)
    {
        if (tb is null) return;
        tb.Effect = s.TextShadow
            ? new DropShadowEffect
            {
                BlurRadius = s.ShadowBlur,
                ShadowDepth = Math.Min(8, s.ShadowBlur / 4),
                Opacity = s.ShadowOpacity,
                Color = Colors.Black,
            }
            : null;
    }

    private void SetText(TextBlock? main, List<TextBlock> outlines, string text, Visibility visibility)
    {
        if (main is null) return;
        main.Text = text;
        foreach (var o in outlines) o.Text = text;
        main.Visibility = visibility;
        foreach (var o in outlines) o.Visibility = visibility;
    }

    // ---------- 時刻表示(秒同期: 毎秒1回の起床でCPU負荷最小化) ----------
    private void StartTimer()
    {
        _timer?.Stop();
        _timer = new DispatcherTimer(DispatcherPriority.Background);
        _timer.Tick += TimerTick;
        ScheduleNextTick();
        _timer.Start();
        UpdateClock();
    }

    private void TimerTick(object? sender, EventArgs e)
    {
        UpdateClock();
        ScheduleNextTick();
    }

    private void ScheduleNextTick()
    {
        // 次の「秒の境目 + 15ms」まで寝る(5Hzポーリングをやめて1Hzに)
        var ms = 1000 - DateTime.Now.Millisecond + 15;
        _timer.Interval = TimeSpan.FromMilliseconds(ms);
    }

    private void UpdateClock()
    {
        try
        {
            var now = DateTime.Now;

            var s = App.Settings;
            SetText(_timeMain, _timeOutline, now.ToString(s.TimeFormat), Visibility.Visible);

            var dateText = string.IsNullOrWhiteSpace(s.DateFormat) || !s.ShowDate ? "" : now.ToString(s.DateFormat);
            SetText(_dateMain, _dateOutline, dateText,
                dateText.Length == 0 ? Visibility.Collapsed : Visibility.Visible);

            // 位置の自動保存は1分ごと(ディスクにも書くのでクラッシュ耐性あり)
            if (now.Second == 0)
            {
                SavePosition();
                App.Settings.Save();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("時計描画中の例外", ex);
            try { _timer?.Stop(); } catch { }
        }
    }

    // ---------- 位置・モニタ追従 ----------
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_monitorDevice))
            RestoreOrCenterOnMonitor();
        else
        {
            ApplyTargetMonitor();
            // 位置プリセット記憶時はそれを優先(桁数変化に追従)
            if (!string.IsNullOrWhiteSpace(App.Settings.PositionPreset))
                ApplyPreset(App.Settings.PositionPreset);
        }
        ClampToScreen();
        ApplyTopmost();
        ApplyClickThrough();
        StartProximityWatch();
        StartTimer();

        // モニタ構成・DPI 変化への追従
        void OnDisplayChanged(object? s, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ApplyTargetMonitor();
                ClampToScreen();
            });
        }
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
    }

    private double Dpi => VisualTreeHelper.GetDpi(this).PixelsPerDip;

    private bool TryGetWorkingArea(out System.Drawing.Rectangle wa, out string deviceName)
    {
        try
        {
            var center = new System.Drawing.Point(
                (int)((Left + ActualWidth / 2) * Dpi),
                (int)((Top + ActualHeight / 2) * Dpi));
            var screen = System.Windows.Forms.Screen.FromPoint(center);
            wa = screen.WorkingArea;
            deviceName = screen.DeviceName;
            return true;
        }
        catch
        {
            wa = default;
            deviceName = "";
            return false;
        }
    }

    private void SavePosition()
    {
        try
        {
            var s = App.Settings;
            if (string.IsNullOrEmpty(_monitorDevice))
            {
                s.Left = Left;
                s.Top = Top;

                // モニタ指定時は、そのモニタ原点からの相対位置も記憶
                if (!string.IsNullOrWhiteSpace(s.TargetMonitor)
                    && TryGetWorkingArea(out var wa, out var device)
                    && device == s.TargetMonitor)
                {
                    s.MonitorPositions[s.TargetMonitor] = new[]
                    {
                        Math.Round(Left - wa.Left / Dpi, 1),
                        Math.Round(Top - wa.Top / Dpi, 1),
                    };
                }
            }
            else
            {
                // 担当モニタ原点からの相対位置で記憶(モニタ構成変化に強い)
                if (TryGetWorkingArea(out var wa, out _))
                    s.MonitorPositions[_monitorDevice] = new[]
                    {
                        Math.Round(Left - wa.Left / Dpi, 1),
                        Math.Round(Top - wa.Top / Dpi, 1),
                    };
            }
        }
        catch (Exception ex)
        {
            Logger.Error("位置保存に失敗", ex);
        }
    }

    /// <summary>担当モニタに復帰/中央配置(マルチモニタ同時表示時)。</summary>
    private void RestoreOrCenterOnMonitor()
    {
        try
        {
            var screen = System.Windows.Forms.Screen.AllScreens
                .FirstOrDefault(sc => sc.DeviceName == _monitorDevice);
            if (screen is null)
            {
                Logger.Info($"担当モニタが見つからない(切断?): {_monitorDevice}");
                return;
            }

            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var wa = screen.WorkingArea;
            if (App.Settings.MonitorPositions.TryGetValue(_monitorDevice, out var pos) && pos is { Length: 2 })
            {
                // 作業領域原点からの相対位置を復元
                Left = wa.Left / dpi + pos[0];
                Top = wa.Top / dpi + pos[1];
            }
            else
            {
                Left = (wa.Left + (wa.Width - ActualWidth * dpi) / 2) / dpi;
                Top = (wa.Top + (wa.Height - ActualHeight * dpi) / 2) / dpi;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("モニタへの復帰に失敗", ex);
        }
    }

    /// <summary>桁数変化などで窓サイズが変わった時、位置プリセットを再適用する。</summary>
    private void OnSizeChangedForPreset(object sender, SizeChangedEventArgs e)
    {
        try
        {
            // 起動初期のサイズ決定中は無視、プリセット未使用時も何もしない
            if (!IsLoaded || e.NewSize.Equals(e.PreviousSize)) return;
            var preset = App.Settings.PositionPreset;
            if (string.IsNullOrWhiteSpace(preset)) return;
            ApplyPreset(preset); // SavePosition は行うが Settings.Save() はしない(軽量)
        }
        catch { }
    }

    /// <summary>位置プリセット適用(自分のモニタ基準)。</summary>
    public void ApplyPreset(string preset)
    {
        try
        {
            if (!TryGetWorkingArea(out var wa, out _)) return;
            var dpi = Dpi;
            var m = 8 * dpi; // 余白 8 DIP
            var wpx = ActualWidth * dpi;
            var hpx = ActualHeight * dpi;

            double xPx, yPx;
            switch (preset)
            {
                case "topleft": xPx = wa.Left + m; yPx = wa.Top + m; break;
                case "topright": xPx = wa.Right - m - wpx; yPx = wa.Top + m; break;
                case "bottomleft": xPx = wa.Left + m; yPx = wa.Bottom - m - hpx; break;
                case "bottomright": xPx = wa.Right - m - wpx; yPx = wa.Bottom - m - hpx; break;
                case "center": xPx = wa.Left + (wa.Width - wpx) / 2; yPx = wa.Top + (wa.Height - hpx) / 2; break;
                default: return;
            }
            Left = xPx / dpi;
            Top = yPx / dpi;
            App.Settings.PositionPreset = preset; // サイズ変化時に再整列できるよう記憶
            SavePosition();
            ClampToScreen();
        }
        catch (Exception ex)
        {
            Logger.Error("位置プリセット適用に失敗", ex);
        }
    }

    /// <summary>指定モニタが有効なら、そのモニタの作業領域中央へ移動する。</summary>
    private void ApplyTargetMonitor()
    {
        try
        {
            var s = App.Settings;
            if (string.IsNullOrWhiteSpace(s.TargetMonitor)) return;

            var target = System.Windows.Forms.Screen.AllScreens
                .FirstOrDefault(sc => sc.DeviceName == s.TargetMonitor);
            if (target is null)
            {
                Logger.Info($"指定モニタが見つからないため自動にフォールバック: {s.TargetMonitor}");
                return;
            }

            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var center = new System.Drawing.Point(
                (int)((Left + ActualWidth / 2) * dpi),
                (int)((Top + ActualHeight / 2) * dpi));

            var current = System.Windows.Forms.Screen.FromPoint(center);
            if (current.DeviceName == target.DeviceName) return; // 既に正しい

            var wa = target.WorkingArea;
            if (App.Settings.MonitorPositions.TryGetValue(s.TargetMonitor, out var rel) && rel is { Length: 2 })
            {
                // 記憶した相対位置へ復元
                Left = wa.Left / dpi + rel[0];
                Top = wa.Top / dpi + rel[1];
            }
            else
            {
                // 初回は中央へ
                Left = (wa.Left + (wa.Width - ActualWidth * dpi) / 2) / dpi;
                Top = (wa.Top + (wa.Height - ActualHeight * dpi) / 2) / dpi;
                s.MonitorPositions[s.TargetMonitor] = new[]
                {
                    Math.Round(Left - wa.Left / dpi, 1),
                    Math.Round(Top - wa.Top / dpi, 1),
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Error("モニタへの配置に失敗", ex);
        }
    }

    /// <summary>画面の外に飛んだ位置・切断モニタ上の位置を、作業領域内へ補正する。</summary>
    private void ClampToScreen()
    {
        try
        {
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var winPt = new System.Drawing.Point(
                (int)(Left * dpi), (int)(Top * dpi));
            var screen = System.Windows.Forms.Screen.FromPoint(winPt);
            var wa = screen.WorkingArea;

            var w = Math.Max(0, ActualWidth) * dpi;
            var h = Math.Max(0, ActualHeight) * dpi;

            var maxLeft = Math.Max(wa.Left, wa.Right - (int)w);
            var maxTop = Math.Max(wa.Top, wa.Bottom - (int)h);

            var newLeft = Math.Min(Math.Max(Left, wa.Left / dpi), maxLeft / dpi);
            var newTop = Math.Min(Math.Max(Top, wa.Top / dpi), maxTop / dpi);

            if (Math.Abs(newLeft - Left) > 0.5 || Math.Abs(newTop - Top) > 0.5)
            {
                Left = newLeft;
                Top = newTop;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("位置クランプ失敗", ex);
        }
    }

    // ---------- 常に手前 / クリックスルー ----------
    private IntPtr Hwnd => _hwnd != IntPtr.Zero ? _hwnd : (_hwnd = new WindowInteropHelper(this).EnsureHandle());
    private IntPtr _hwnd;

    public void ApplyTopmost()
    {
        try { Topmost = App.Settings.Topmost; }
        catch (Exception ex) { Logger.Error("Topmost適用失敗", ex); }
    }

    public void ApplyClickThrough()
    {
        try
        {
            if (!_exStyleApplied) return;
            var hwnd = Hwnd;
            var ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            var transparent = App.Settings.ClickThrough && !_modActive && !_menuOpen;
            if (transparent)
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
                    ex | NativeMethods.WS_EX_TRANSPARENT);
            else
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
                    ex & ~NativeMethods.WS_EX_TRANSPARENT);
        }
        catch (Exception ex)
        {
            Logger.Error("クリックスルー適用失敗", ex);
        }
    }

    // ---------- マウス接近フェード + モディファイアキー ----------
    private DispatcherTimer? _proximityTimer;
    private bool _near;
    private bool _modActive;   // 透明解除キー押下中
    private bool _menuOpen;    // コンテキストメニュー表示中
    private double _lastFadeTarget = -1; // 不可能値で初期化(NANは比較が常にfalseになるため不可)

    private static bool ModifierPressed()
    {
        var vk = App.Settings.ModifierKey switch
        {
            "Ctrl" => NativeMethods.VK_CONTROL,
            "Alt" => NativeMethods.VK_MENU,
            "Shift" => NativeMethods.VK_SHIFT,
            _ => 0,
        };
        return vk != 0 && NativeMethods.GetAsyncKeyState(vk) < 0;
    }

    private bool InteractionWatchNeeded =>
        App.Settings.ProximityEnabled ||
        (App.Settings.ClickThrough && App.Settings.ModifierKey != "None");

    private void StartProximityWatch()
    {
        _proximityTimer?.Stop();
        _proximityTimer = null;
        _near = false;
        _modActive = false;
        _lastFadeTarget = -1;

        if (!InteractionWatchNeeded) return;

        _proximityTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(16), // 60Hz ポーリング(反応遅れ最大16ms)
        };
        _proximityTimer.Tick += (_, _) => UpdateInteractionState();
        _proximityTimer.Start();
    }

    private void UpdateInteractionState()
    {
        try
        {
            // モディファイアキー → クリックスルー一時解除
            var mod = ModifierPressed();
            if (mod != _modActive)
            {
                Logger.Info($"透明解除キー: {_modActive} → {mod}");
                _modActive = mod;
                ApplyClickThrough();
            }

            // 接近判定
            var near = false;
            var prevNear = _near;
            if (App.Settings.ProximityEnabled)
            {
                var cur = System.Windows.Forms.Cursor.Position;
                var dpi = Dpi;
                var x = Left * dpi; var y = Top * dpi;
                var w = ActualWidth * dpi; var h = ActualHeight * dpi;

                // ウィンドウ矩形の外側からの距離(内側なら 0)
                var dx = Math.Max(Math.Max(x - cur.X, cur.X - (x + w)), 0);
                var dy = Math.Max(Math.Max(y - cur.Y, cur.Y - (y + h)), 0);
                near = Math.Sqrt(dx * dx + dy * dy) <= App.Settings.ProximityDistance;
            }
            _near = near;
            if (near != prevNear)
                Logger.Info($"接近判定: {prevNear} → {near}");

            // 目標不透明度: 解除キー中/メニュー中は完全表示 → 接近時 → 通常
            var target = (_modActive || _menuOpen) ? 1.0
                : _near ? App.Settings.ProximityOpacity
                : App.Settings.WindowOpacity;

            if (Math.Abs(target - _lastFadeTarget) > 0.001)
            {
                Logger.Info($"フェード: {Math.Round(_lastFadeTarget, 3)} → {Math.Round(target, 3)} (near={_near}, mod={_modActive}, menu={_menuOpen})");
                _lastFadeTarget = target;
                FadeTo(target);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("インタラクション監視に失敗", ex);
            try { _proximityTimer?.Stop(); } catch { }
        }
    }

    private void FadeTo(double opacity)
    {
        try
        {
            var anim = new DoubleAnimation(
                opacity,
                TimeSpan.FromMilliseconds(App.Settings.FadeDurationMs))
            {
                FillBehavior = FillBehavior.HoldEnd, // GPU コンポジションで滑らかに
            };
            anim.EasingFunction = App.Settings.FadeEasing switch
            {
                "linear" => null,
                "easein" => new QuadraticEase { EasingMode = EasingMode.EaseIn },
                _ => new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            BeginAnimation(OpacityProperty, anim);
        }
        catch (Exception ex)
        {
            Logger.Error("フェードに失敗", ex);
            Opacity = opacity;
        }
    }

    // ---------- 設定反映(適用ボタン/設定画面を閉じた後) ----------
    public void ApplySettingsFromDialog()
    {
        try
        {
            Logger.Info($"設定反映: monitor={_monitorDevice}");
            RebuildTextVisuals();
            ApplyTopmost();
            ApplyClickThrough();

            BeginAnimation(OpacityProperty, null); // アニメーションを解放してから
            Opacity = App.Settings.WindowOpacity;

            StartProximityWatch();
            ApplyTargetMonitor();
            ClampToScreen();
            UpdateClock();
            App.Settings.Save();
        }
        catch (Exception ex)
        {
            Logger.Error("設定反映に失敗", ex);
        }
    }

    // ---------- 右クリックメニュー ----------
    private void BuildContextMenu()
    {
        var miSettings = new MenuItem { Header = "設定(_S)" };
        miSettings.Click += (_, _) => ShowSettings();

        var miTopmost = new MenuItem { Header = "常に手前に表示(_T)", IsCheckable = true, IsChecked = App.Settings.Topmost };
        miTopmost.Click += (_, _) =>
        {
            App.Settings.Topmost = miTopmost.IsChecked;
            App.ApplyToAllClocks();
            App.Settings.Save();
        };

        var miClickThrough = new MenuItem { Header = "クリックスルー(_C)", IsCheckable = true, IsChecked = App.Settings.ClickThrough };
        miClickThrough.Click += (_, _) =>
        {
            App.Settings.ClickThrough = miClickThrough.IsChecked;
            App.ApplyToAllClocks();
            App.Settings.Save();
        };

        var miExit = new MenuItem { Header = "終了(_X)" };
        miExit.Click += (_, _) => Close();

        // メニュー表示中はクリックスルー解除(閉じたら復帰)
        ClockMenu.Opened += (_, _) => { _menuOpen = true; ApplyClickThrough(); };
        ClockMenu.Closed += (_, _) => { _menuOpen = false; ApplyClickThrough(); };

        ClockMenu.Items.Clear();
        ClockMenu.Items.Add(miSettings);
        ClockMenu.Items.Add(miTopmost);
        ClockMenu.Items.Add(miClickThrough);
        ClockMenu.Items.Add(new Separator());
        ClockMenu.Items.Add(miExit);
    }

    // ---------- 入力 ----------
    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.ClickCount == 2)
            {
                ShowSettings();
                return;
            }
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                // 設定画面表示中に時計がクリックされたら、設定画面を1回だけ前面へ
                if (App.SettingsWindowOpen)
                    App.BringSettingsToFront();

                DragMove();
                // ドラッグ終了時に即保存(強制終了されても位置が残る)
                SavePosition();
                App.Settings.Save();
            }
        }
        catch (InvalidOperationException)
        {
            // マウスボタン解放後に DragMove が呼ばれた場合の benign 例外
        }
    }

    private static bool _settingsOpen;

    private void OnContextMenuRequest(object sender, MouseButtonEventArgs e)
        => ClockMenu.IsOpen = true;

    public void ShowSettings()
    {
        try
        {
            if (App.SettingsWindowOpen) return; // 既に開いている

            // モードレスで開く(ShowDialogは全ウィンドウを無効化し時計が反応しなくなるため)
            App.SettingsWindowOpen = true;
            var dlg = new SettingsWindow();
            dlg.Closed += (_, _) =>
            {
                App.SettingsWindowOpen = false;
                App.ApplyToAllClocks();
            };
            dlg.Show();
            // 開く時は通常表示。時計がクリックされた時だけ前面に上げる(ShowSettingsのクリック処理)
        }
        catch (Exception ex)
        {
            Logger.Error("設定画面オープン失敗", ex);
        }
    }
}
