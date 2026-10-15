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
            // タイマーを停止(閉じた窓の監視を続けない)
            _timer?.Stop();
            _proximityTimer?.Stop();
            App.Clocks.Remove(this);
        };
    }

    // ---------- テキスト描画(文字単位: 縁取り+影+間隔+切替効果) ----------

    /// <summary>1レイヤー(本体 or 縁取りの1方向)の状態</summary>
    private sealed class LayerState
    {
        public StackPanel Panel = new();
        public List<Grid> Cells = new();
        public FontFamily Family = new("Yu Gothic UI");
        public double Size = 64;
        public Brush Brush = Brushes.White;
    }

    private sealed class TextLayers
    {
        public Grid Host = new();
        public List<LayerState> Layers = new();
        public string LastText = "";
    }

    private TextLayers _timeLayers = new();
    private TextLayers _dateLayers = new();

    private void RebuildTextVisuals()
    {
        var s = App.Settings;
        var family = new FontFamily(s.FontFamily);
        var textColor = ParseColor(s.TextColor, Colors.White);
        var outlineColor = ParseColor(s.OutlineColor, Colors.Black);

        // XAML のホストに接続(これが無いと画面に出ない)
        _timeLayers.Host = TimeHost;
        _dateLayers.Host = DateHost;

        RebuildLayers(_timeLayers, family, s.FontSize, textColor, outlineColor);
        RebuildLayers(_dateLayers, family, Math.Max(12, s.FontSize * 0.35), textColor, outlineColor);
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static void RebuildLayers(TextLayers layers, FontFamily family, double size,
        Color mainColor, Color outlineColor)
    {
        var s = App.Settings;
        layers.Host.Children.Clear();
        layers.Layers.Clear();
        layers.LastText = "";

        var thickness = s.OutlineEnabled ? Math.Clamp(s.OutlineThickness, 0.5, 20) : 0;

        void AddLayer(Brush brush, double dx, double dy, bool isMain)
        {
            var st = new LayerState { Family = family, Size = size, Brush = brush };
            st.Panel.Orientation = Orientation.Horizontal; // 文字を横に並べる
            st.Panel.RenderTransform = new TranslateTransform(dx, dy);
            layers.Host.Children.Add(st.Panel);
            layers.Layers.Add(st);
            if (isMain)
            {
                st.Panel.Effect = s.TextShadow
                    ? new DropShadowEffect
                    {
                        BlurRadius = s.ShadowBlur,
                        ShadowDepth = Math.Min(8, s.ShadowBlur / 4),
                        Opacity = s.ShadowOpacity,
                        Color = Colors.Black,
                    }
                    : null;
            }
        }

        if (thickness > 0)
        {
            var ob = new SolidColorBrush(outlineColor);
            ob.Freeze();
            double[,] dirs = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 }, { -1, -1 }, { 1, -1 }, { -1, 1 }, { 1, 1 } };
            for (var i = 0; i < dirs.GetLength(0); i++)
                AddLayer(ob, dirs[i, 0] * thickness, dirs[i, 1] * thickness, isMain: false);
        }

        var mb = new SolidColorBrush(mainColor);
        mb.Freeze();
        AddLayer(mb, 0, 0, isMain: true);
    }

    /// <summary>レイヤーの文字セルをテキストに同期(桁数変化で再構築)。</summary>
    private static void UpdateChars(LayerState st, string text, double letterSp, double sepSp)
    {
        if (st.Cells.Count != text.Length)
        {
            st.Cells.Clear();
            st.Panel.Children.Clear();
            for (var i = 0; i < text.Length; i++)
            {
                var cell = new Grid { ClipToBounds = true }; // スロット風にクリップ
                cell.Children.Add(new TextBlock
                {
                    FontFamily = st.Family,
                    FontSize = st.Size,
                    Foreground = st.Brush,
                });
                st.Panel.Children.Add(cell);
                st.Cells.Add(cell);
            }
        }

        for (var i = 0; i < text.Length; i++)
        {
            var tb = (TextBlock)st.Cells[i].Children[0];
            tb.Text = text[i].ToString();

            // 間隔: 区切り文字の前後に区切り間隔、全文字の後に文字間隔
            var isSep = !char.IsLetterOrDigit(text[i]);
            var left = isSep ? sepSp / 2 : 0;
            var right = (isSep ? sepSp / 2 : 0) + letterSp;
            if (i == 0) left = 0;
            if (i == text.Length - 1) right = 0;
            tb.Margin = new Thickness(left, 0, right, 0);
        }
    }

    private void SetText(TextLayers layers, string text, Visibility visibility)
    {
        if (text != layers.LastText)
        {
            var old = layers.LastText;
            layers.LastText = text;
            var s = App.Settings;

            foreach (var st in layers.Layers)
                UpdateChars(st, text, s.LetterSpacing, s.SeparatorSpacing);

            // 切り替え効果(桁数が変わった時はスムーズにできないのでスキップ)
            if (s.TransitionEffect != "none" && old.Length == text.Length
                && layers.Host.Visibility == Visibility.Visible)
            {
                for (var i = 0; i < text.Length; i++)
                    if (old[i] != text[i])
                        AnimateCharChange(layers, i, old[i]);
            }
        }
        layers.Host.Visibility = visibility;
    }

    /// <summary>変化した1文字の切替効果アニメーション(全レイヤー同時)。</summary>
    private static void AnimateCharChange(TextLayers layers, int index, char oldChar)
    {
        var s = App.Settings;
        var dur = TimeSpan.FromMilliseconds(Math.Clamp(s.TransitionDurationMs, 50, 500));
        var half = TimeSpan.FromMilliseconds(dur.TotalMilliseconds / 2);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        foreach (var st in layers.Layers)
        {
            if (index >= st.Cells.Count) continue;
            var cell = st.Cells[index];
            var tb = (TextBlock)cell.Children[0];

            // 古い残像があれば除去
            for (var i = cell.Children.Count - 1; i >= 1; i--)
                cell.Children.RemoveAt(i);

            var ghost = new TextBlock
            {
                Text = oldChar.ToString(),
                FontFamily = st.Family,
                FontSize = st.Size,
                Foreground = st.Brush,
                Margin = tb.Margin,
            };

            DoubleAnimation? mainAnim = null;
            DoubleAnimation? ghostAnim = null;

            switch (s.TransitionEffect)
            {
                case "flipx":
                case "flipy":
                {
                    // 文字の中心を軸に反転(横回転=縦方向の中心、縦回転=横方向の中心)
                    tb.RenderTransformOrigin = new Point(0.5, 0.5);
                    ghost.RenderTransformOrigin = new Point(0.5, 0.5);
                    var axis = s.TransitionEffect == "flipx"
                        ? ScaleTransform.ScaleXProperty : ScaleTransform.ScaleYProperty;
                    var mainT = new ScaleTransform();
                    // BeginTime 遅延中はベース値が見えるため、先に 0 (潰れた状態) へ
                    if (s.TransitionEffect == "flipx") mainT.ScaleX = 0; else mainT.ScaleY = 0;
                    tb.RenderTransform = mainT;
                    mainAnim = new DoubleAnimation(0, 1, dur)
                    {
                        BeginTime = half, EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd,
                    };
                    mainT.BeginAnimation(axis, mainAnim);

                    var ghostT = new ScaleTransform();
                    ghost.RenderTransform = ghostT;
                    ghostAnim = new DoubleAnimation(1, 0, half)
                    {
                        EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd,
                    };
                    ghostT.BeginAnimation(axis, ghostAnim);
                    break;
                }
                case "slideup":
                case "slidedown":
                {
                    // スロット式: 旧数字が抜けると同時に新数字が流れ込む(同時進行)
                    var dir = s.TransitionEffect == "slideup" ? -1 : 1; // 上へ抜ける=負方向
                    var lineH = st.Size * 1.25;

                    var mainT = new TranslateTransform(0, -dir * lineH);
                    tb.RenderTransform = mainT;
                    mainAnim = new DoubleAnimation(-dir * lineH, 0, dur)
                    {
                        EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd,
                    };
                    mainT.BeginAnimation(TranslateTransform.YProperty, mainAnim);

                    var ghostT = new TranslateTransform(0, 0);
                    ghost.RenderTransform = ghostT;
                    ghostAnim = new DoubleAnimation(0, dir * lineH, dur)
                    {
                        EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd,
                    };
                    ghostT.BeginAnimation(TranslateTransform.YProperty, ghostAnim);
                    break;
                }
                case "fade":
                {
                    // フェードイン・アウト: 古い文字が徐々に消え(等速)、その後新しい文字が出る
                    // (古い文字のフェードアウトに EaseOut を使うと即座に消えて見えない)
                    tb.Opacity = 0;
                    mainAnim = new DoubleAnimation(0, 1, half)
                    {
                        BeginTime = half, EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd,
                    };
                    tb.BeginAnimation(OpacityProperty, mainAnim);
                    ghostAnim = new DoubleAnimation(1, 0, half)
                    {
                        FillBehavior = FillBehavior.HoldEnd,
                    };
                    ghost.BeginAnimation(OpacityProperty, ghostAnim);
                    break;
                }
                case "crossfade":
                {
                    // クロスフェード: 同時に交差
                    tb.Opacity = 0;
                    mainAnim = new DoubleAnimation(0, 1, dur)
                    {
                        EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd,
                    };
                    tb.BeginAnimation(OpacityProperty, mainAnim);
                    ghostAnim = new DoubleAnimation(1, 0, dur)
                    {
                        EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd,
                    };
                    ghost.BeginAnimation(OpacityProperty, ghostAnim);
                    break;
                }
                default:
                    return;
            }

            cell.Children.Add(ghost);

            var g = ghostAnim;
            g.Completed += (_, _) => cell.Children.Remove(ghost);
            var m = mainAnim;
            if (m is not null)
                m.Completed += (_, _) =>
                {
                    tb.BeginAnimation(OpacityProperty, null);
                    tb.Opacity = 1;
                    tb.RenderTransform = null; // 残存オフセットによる位置ズレを防ぐ
                };
        }
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

    private static bool _formatErrorLogged;

    /// <summary>書式例外でもタイマーを止めず、最後の正常表示を維持する。</summary>
    private static string SafeTimeText(DateTime now, string format, string? fallback)
    {
        try
        {
            var r = now.ToString(format);
            _formatErrorLogged = false; // 書式が治ったら再度ログできるように
            return r;
        }
        catch (Exception ex)
        {
            if (!_formatErrorLogged)
            {
                _formatErrorLogged = true;
                Logger.Error($"時刻書式が不正のため前回表示を維持: {format}", ex);
            }
            return fallback ?? now.ToString("HH:mm");
        }
    }

    private void UpdateClock()
    {
        try
        {
            var now = DateTime.Now;

            var s = App.Settings;
            SetText(_timeLayers, SafeTimeText(now, s.TimeFormat, _timeLayers.LastText), Visibility.Visible);

            var dateText = string.IsNullOrWhiteSpace(s.DateFormat) || !s.ShowDate
                ? ""
                : SafeTimeText(now, s.DateFormat, _dateLayers.LastText);
            SetText(_dateLayers, dateText,
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
            // 描画例外でタイマーを停止しない(次の秒で自動復帰する)
            Logger.Error("時計描画中の例外", ex);
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
                // 担当モニタ原点からの相対位置で記憶(モニタ構成変化に強い)。
                // 時計が担当モニタ以外にドラッグされている間は保存しない
                // (他モニタ基準の座標で担当モニタの記憶を汚染しない)。
                if (TryGetWorkingArea(out var wa, out var curDevice) && curDevice == _monitorDevice)
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
            if (!_exStyleApplied || !IsLoaded) return; // 閉じた窓に対しては何もしない
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
        if (!IsLoaded) return; // 閉じた窓の監視は停止
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
        if (!IsLoaded) return; // 閉じた窓には反映しない
        try
        {
            Logger.Info($"設定反映: monitor={_monitorDevice}");
            RebuildTextVisuals();
            ApplyTopmost();
            ApplyClickThrough();

            BeginAnimation(OpacityProperty, null); // アニメーションを解放してから
            Opacity = App.Settings.WindowOpacity;

            StartProximityWatch();

            if (string.IsNullOrEmpty(_monitorDevice))
            {
                // 単一モード: 対象モニターへ移動してから、そのモニター内でプリセット整列
                // (#6: プリセットありでも対象モニター変更が無視されない順序)
                ApplyTargetMonitor();
                if (!string.IsNullOrWhiteSpace(App.Settings.PositionPreset))
                    ApplyPreset(App.Settings.PositionPreset);
            }
            // マルチモニタ同時表示時は担当モニタ内の現在位置を維持

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

        var miTopmost = new MenuItem { Header = "常に手前に表示(_T)", IsCheckable = true };
        miTopmost.Click += (_, _) =>
        {
            // 開いた時点で同期済みの最新状態を反転(#7)
            App.Settings.Topmost = !App.Settings.Topmost;
            App.ApplyToAllClocks();
            App.Settings.Save();
        };

        var miClickThrough = new MenuItem { Header = "クリックスルー(_C)", IsCheckable = true };
        miClickThrough.Click += (_, _) =>
        {
            App.Settings.ClickThrough = !App.Settings.ClickThrough;
            App.ApplyToAllClocks();
            App.Settings.Save();
        };

        // 位置プリセット サブメニュー(位置のみ即時反映)
        var presetMenu = new MenuItem { Header = "位置プリセット(_P)" };
        foreach (var (label, tag) in new[]
        {
            ("左上", "topleft"), ("右上", "topright"),
            ("左下", "bottomleft"), ("右下", "bottomright"), ("中央", "center"),
        })
        {
            var tag2 = tag;
            var mi = new MenuItem { Header = label };
            mi.Click += (_, _) => App.ApplyPositionPreset(tag2);
            presetMenu.Items.Add(mi);
        }

        // Windows 自動起動(チェックは開くたびに同期)
        var miAutoStart = new MenuItem { Header = "Windows 起動時に自動で開始", IsCheckable = true };
        miAutoStart.Click += (_, _) =>
        {
            App.Settings.AutoStart = miAutoStart.IsChecked;
            App.SetAutoStart(miAutoStart.IsChecked);
            App.Settings.Save();
        };

        // このアプリについて(設定画面の情報タブへ)
        var miAbout = new MenuItem { Header = "このアプリについて(_A)" };
        miAbout.Click += (_, _) => ShowSettings("情報");

        var miExit = new MenuItem { Header = "終了(_X)" };
        // アプリ全体を終了(時計1つの Close ではない)(#8)
        miExit.Click += (_, _) => Application.Current.Shutdown();

        // メニュー表示中はクリックスルー解除(閉じたら復帰)+ 最新状態へ同期(#7)
        ClockMenu.Opened += (_, _) =>
        {
            _menuOpen = true;
            ApplyClickThrough();
            miTopmost.IsChecked = App.Settings.Topmost;
            miClickThrough.IsChecked = App.Settings.ClickThrough;
            miAutoStart.IsChecked = App.Settings.AutoStart;
        };
        ClockMenu.Closed += (_, _) => { _menuOpen = false; ApplyClickThrough(); };

        ClockMenu.Items.Clear();
        ClockMenu.Items.Add(miSettings);
        ClockMenu.Items.Add(presetMenu);
        ClockMenu.Items.Add(new Separator());
        ClockMenu.Items.Add(miTopmost);
        ClockMenu.Items.Add(miClickThrough);
        ClockMenu.Items.Add(miAutoStart);
        ClockMenu.Items.Add(new Separator());
        ClockMenu.Items.Add(miAbout);
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
                // ユーザーの手動移動でプリセットを解除(#6: 再起動後も手動位置を維持)
                App.Settings.PositionPreset = "";
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

    private void OnContextMenuRequest(object sender, MouseButtonEventArgs e)
        => ClockMenu.IsOpen = true;

    public void ShowSettings(string initialTab = "")
    {
        try
        {
            if (App.SettingsWindowOpen)
            {
                // 既に開いている場合は前面へ出してタブだけ切り替える
                App.BringSettingsToFront();
                if (initialTab.Length > 0) App.SelectSettingsTab(initialTab);
                return;
            }

            // モードレスで開く(ShowDialogは全ウィンドウを無効化し時計が反応しなくなるため)
            App.SettingsWindowOpen = true;
            App.PendingSettingsTab = initialTab;
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
