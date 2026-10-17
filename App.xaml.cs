using System.Windows;
using Application = System.Windows.Application;

namespace TVClockModern;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private static bool _ownsMutex;

    /// <summary>稼働中の時計ウィンドウ(マルチモニタ同時表示時は複数)</summary>
    public static List<MainWindow> Clocks { get; } = new();

    /// <summary>設定をファイルからロード済みか(#1: 未ロード状態での保存を防ぐ)</summary>
    private static bool _settingsLoaded;

    /// <summary>設定画面が開いているか(時計クリック時の前面再登場判定用)</summary>
    public static bool SettingsWindowOpen;
    private System.Windows.Forms.NotifyIcon? _tray;
    private System.Windows.Forms.ToolStripMenuItem? _miSettings, _miTopmost, _miClickThrough, _miRestart, _miExit;
    private int _dispatchedExceptions;
    private DateTime _lastExceptionUtc = DateTime.MinValue;

    /// <summary>確認済みの新バージョンタグ(未確認/null)。情報タブのバナーとバルーンで使用。</summary>
    public static string? NewVersion;
    /// <summary>リリースページを既定ブラウザで開く。</summary>
    public static void OpenReleasePage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = UpdateChecker.ReleaseUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Error("リリースページを開けませんでした", ex);
        }
    }

    /// <summary>起動時の更新確認(24h に 1 回)。失敗しても黙って継続する。</summary>
    private async Task CheckForUpdatesInBackground()
    {
        string? tag = null;
        try
        {
            tag = await UpdateChecker.GetLatestVersionAsync();
        }
        catch (Exception ex)
        {
            Logger.Info("更新確認に失敗(無視): " + ex.Message);
            return;
        }

        // 設定への記録(記録時刻・検出タグ)は UI スレッドで行う
        await Dispatcher.InvokeAsync(() =>
        {
            UpdateChecker.MarkChecked();
            if (tag is null) return;
            App.Settings.PendingUpdate = tag;
            NewVersion = tag;
            _tray?.ShowBalloonTip(10000, L.T("balloon.updateTitle"),
                L.T("balloon.updateBody", tag), System.Windows.Forms.ToolTipIcon.Info);
        });
    }

    public static ClockSettings Settings { get; set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        // --- タスク全体を保護: ここで落としたら時計が消える ---
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("Dispatcher 未処理例外", args.Exception);
            // 累積ではなく「10分以内の連発」でカウント(まばらな珍例外で
            // 長期常駐中に意図せず再起動しないようにする)
            var nowUtc = DateTime.UtcNow;
            if (nowUtc - _lastExceptionUtc > TimeSpan.FromMinutes(10))
                _dispatchedExceptions = 0;
            _lastExceptionUtc = nowUtc;
            _dispatchedExceptions++;
            args.Handled = true; // まずは生かす
            if (_dispatchedExceptions > 20)
            {
                // 連続異常時は素直に自己再起動
                TryRestart("例外多発のため再起動します");
            }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Logger.Error("AppDomain 未処理例外",
                args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
            if (args.IsTerminating) TryRestart(null); // 致命的終了時は復帰を試みる
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error("未観測タスク例外", args.Exception);
            args.SetObserved();
        };

        // --- 設定ロード(失敗時もフォールバック済み) ---
        // 二重起動拒否メッセージの言語判定にも使うため、Mutex 待機の前に読む。
        // 保存は OnExit/TryRestart で _ownsMutex && _settingsLoaded を確認してから。
        Settings = ClockSettings.Load();
        _settingsLoaded = true;
        L.Init(Settings.Language); // UI 言語(トレイ・メニュー・設定画面の文字列)

        // --- 単一インスタンス(再起動競合に備えて最大2秒待機) ---
        var isNew = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (true)
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\TVClockModern.SingleInstance", out isNew);
            if (isNew) break;
            _singleInstanceMutex.Dispose();
            if (DateTime.UtcNow > deadline)
            {
                Logger.Info("既存インスタンスがあるため終了");
                System.Windows.MessageBox.Show(L.T("msg.alreadyRunning"),
                    "TVClock Modern", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            Thread.Sleep(250); // 前インスタンスの終了を待つ
        }
        _ownsMutex = true;

        base.OnStartup(e);

        try { InitTray(); }
        catch (Exception ex) { Logger.Error("タスクトレイ初期化失敗", ex); }

        // 更新確認: ①設定に未対応の新バージョンタグが残っていれば再通知(再起動後も維持)
        // ②なければ 24h に 1 回、GitHub Releases を確認(設定 OFF ならスキップ)。失敗は無視。
        var pending = UpdateChecker.GetPendingUpdate();
        if (pending is not null)
        {
            NewVersion = pending;
            _tray?.ShowBalloonTip(10000, L.T("balloon.updateTitle"),
                L.T("balloon.updateBody", pending), System.Windows.Forms.ToolTipIcon.Info);
        }
        else if (UpdateChecker.ShouldCheck())
            _ = CheckForUpdatesInBackground();

        // 起動時にウィンドウセットを構築(単一 or 全モニタ)
        Dispatcher.BeginInvoke(() =>
        {
            Clocks.Clear();
            if (Settings.ShowOnAllMonitors)
            {
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    var w = new MainWindow(screen.DeviceName);
                    w.Show();
                    Clocks.Add(w);
                }
                if (Clocks.Count > 0) Application.Current.MainWindow = Clocks[0];
            }
            else
            {
                var single = new MainWindow();
                single.Show();
                Clocks.Add(single);
                MainWindow = single;
            }
        });

        // --settings 付き起動時は設定画面を開く(デバッグ/自動化用)
        if (e.Args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
            Dispatcher.BeginInvoke(() => Clocks.FirstOrDefault()?.ShowSettings());
    }

    /// <summary>Windows 自動起動(HKCU Run)の登録/解除。UI からも共通で使用する。</summary>
    public static void SetAutoStart(bool enable)
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

    /// <summary>設定画面が次に開くときに選択するタブ(例: "情報")。</summary>
    public static string PendingSettingsTab { get; set; } = "";

    /// <summary>開いている設定画面のタブを切り替える。</summary>
    public static void SelectSettingsTab(string header)
    {
        try
        {
            var dlg = Application.Current?.Windows.OfType<SettingsWindow>().FirstOrDefault();
            dlg?.SelectTab(header);
        }
        catch (Exception ex)
        {
            Logger.Error("タブ切替に失敗", ex);
        }
    }

    /// <summary>設定画面(開いている場合)を1回だけ前面に上げてから最前面属性を解除する。
    /// 「一瞬全面に出るが、常時最前面にはならない」挙動。</summary>
    public static void BringSettingsToFront()
    {
        try
        {
            var dlg = Application.Current?.Windows.OfType<SettingsWindow>().FirstOrDefault();
            if (dlg is null) return;
            var hwnd = new System.Windows.Interop.WindowInteropHelper(dlg).EnsureHandle();
            const uint flags = NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE;
            // 1) 一旦最前面に上げて
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, flags);
            // 2) すぐ最前面属性を解除(通常ウィンドウの最上位に落ち着く)
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0, flags);
            dlg.Activate();
        }
        catch (Exception ex)
        {
            Logger.Error("設定画面の前面化に失敗", ex);
        }
    }

    /// <summary>「すべてのモニターに表示」設定に応じてウィンドウセットを同期する。
    /// モニタの増減にも追従。必ずUIスレッドで呼ぶ。</summary>
    public static void EnsureClocks()
    {
        try
        {
            Logger.Info($"EnsureClocks開始: 全モニタ={Settings.ShowOnAllMonitors} 現在{Clocks.Count}窗");
            if (!Settings.ShowOnAllMonitors)
            {
                // 単一モード(#2): MonitorDevice が空の新しい単一時計を生成し、
                // 既存の全時計(担当モニタ付き含む)は表示後に閉じる。
                // Clocks からの除去を先に行い、Close() 中の Closed ハンドラとの
                // 競合(二重削除・誤削除)を防ぐ。
                if (Clocks.All(c => c.MonitorDevice != ""))
                {
                    Logger.Info("単一モード: 新しい単一時計を生成");
                    var newWin = new MainWindow();
                    newWin.Show();
                    Clocks.Add(newWin);
                    Application.Current.MainWindow = newWin;
                }
                var obsoleteSingle = Clocks.Where(c => c.MonitorDevice != "").ToList();
                foreach (var w in obsoleteSingle)
                {
                    Logger.Info("単一モード: 担当モニタ付き時計を閉じる");
                    Clocks.Remove(w);
                    w.Close();
                }
                return;
            }

            var screens = System.Windows.Forms.Screen.AllScreens;
            Logger.Info("モニタ: " + string.Join(", ", screens.Select(s => s.DeviceName)));
            // 無いモニタ用を追加
            foreach (var screen in screens)
            {
                if (Clocks.Any(c => c.MonitorDevice == screen.DeviceName)) continue;
                Logger.Info("時計を追加: " + screen.DeviceName);
                var win = new MainWindow(screen.DeviceName);
                win.Show();
                Clocks.Add(win);
            }
            // 存在しないモニタの分を削除
            // 存在しないモニタ・単一モード時計の分を削除
            // (Clocks からの除去を先に行い、Closed ハンドラとの競合を防ぐ)
            var obsolete = Clocks.Where(c => c.MonitorDevice == "" ||
                screens.All(s => s.DeviceName != c.MonitorDevice)).ToList();
            foreach (var w in obsolete)
            {
                Logger.Info("不要な時計を閉じる: " + w.MonitorDevice);
                Clocks.Remove(w);
                w.Close();
            }
            if (Clocks.Count > 0) Application.Current.MainWindow = Clocks[0];
        }
        catch (Exception ex)
        {
            Logger.Error("時計ウィンドウの同期に失敗", ex);
        }
    }

    /// <summary>位置プリセット(左上/右上/左下/右下/中央)を全時計に適用。
    /// 各時計はそれぞれが表示されているモニタ基準で動く。</summary>
    public static void ApplyPositionPreset(string preset)
    {
        foreach (var c in Clocks.ToList())
            c.ApplyPreset(preset);
        Settings.Save();
    }

    /// <summary>全時計ウィンドウに設定を反映する。</summary>
    public static void ApplyToAllClocks()
    {
        Logger.Info($"ApplyToAllClocks: {Clocks.Count}窗に反映");
        foreach (var c in Clocks.ToList())
            c.ApplySettingsFromDialog();
    }

    /// <summary>トレイメニューの文字列を現在の言語で更新(言語切替時に呼ぶ)。</summary>
    private void UpdateTrayStrings()
    {
        try
        {
            if (_miSettings is null) return; // InitTray 前は何もしない
            _miSettings.Text = L.T("tray.settings");
            _miTopmost!.Text = L.T("tray.topmost");
            _miClickThrough!.Text = L.T("tray.clickthrough");
            _miRestart!.Text = L.T("tray.restart");
            _miExit!.Text = L.T("tray.exit");
        }
        catch (Exception ex)
        {
            Logger.Error("トレイ文字列の更新に失敗", ex);
        }
    }

    /// <summary>言語設定を再読み込みし、トレイ・全時計ウィンドウに即時反映する(UI スレッドで呼ぶ)。</summary>
    public static void ApplyLanguage()
    {
        L.Init(Settings.Language);
        UpdateTrayStringsStatic();
        RebuildClocks();
    }

    private static void UpdateTrayStringsStatic()
    {
        if (Application.Current is App app) app.UpdateTrayStrings();
    }

    /// <summary>
    /// 全時計ウィンドウを建て直す(言語変更などコンストラクタで決まる文字列の再構築)。
    /// UI スレッドで呼ぶ。位置は Closing の SavePosition → 新ウィンドウの復元で引き継ぐ。
    /// </summary>
    public static void RebuildClocks()
    {
        try
        {
            Logger.Info("時計ウィンドウを再構築");
            foreach (var w in Clocks.ToList())
            {
                Clocks.Remove(w);
                w.Close();
            }

            if (Settings.ShowOnAllMonitors)
            {
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    var win = new MainWindow(screen.DeviceName);
                    win.Show();
                    Clocks.Add(win);
                }
                if (Clocks.Count > 0) Application.Current.MainWindow = Clocks[0];
            }
            else
            {
                var single = new MainWindow();
                single.Show();
                Clocks.Add(single);
                Application.Current.MainWindow = single;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("時計ウィンドウの再構築に失敗", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // #1: 設定をロード済みで自分が Mutex を所有している場合だけ保存する。
        // 二重起動で拒否されたプロセス(未ロード・非所有)が既定値で上書きしないようにする。
        if (_ownsMutex && _settingsLoaded)
        {
            try { Settings.Save(); } catch { /* 保存失敗は無視 */ }
        }
        try { _tray?.Dispose(); } catch { }
        try { if (_ownsMutex) _singleInstanceMutex?.ReleaseMutex(); } catch { }
        try { _singleInstanceMutex?.Dispose(); } catch { }
        base.OnExit(e);
    }

    /// <summary>アプリアイコンの読み込み(埋め込みリソース → 実行ファイル → 既定)。</summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            // exe 自身に埋め込まれたアイコン(ApplicationIcon 設定により存在する)
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                return System.Drawing.Icon.ExtractAssociatedIcon(exe) ?? System.Drawing.SystemIcons.Application;
        }
        catch (Exception ex)
        {
            Logger.Error("アイコン読み込みに失敗", ex);
        }
        return System.Drawing.SystemIcons.Application;
    }

    // ---------- タスクトレイ(クリックスルー時の生命線) ----------
    private void InitTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();

        var miSettings = new System.Windows.Forms.ToolStripMenuItem(L.T("tray.settings"));
        miSettings.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            if (Clocks.Count > 0) Clocks[0].ShowSettings();
        });

        var miTopmost = new System.Windows.Forms.ToolStripMenuItem(L.T("tray.topmost"));
        miTopmost.CheckOnClick = true;
        miTopmost.Checked = Settings.Topmost;
        miTopmost.Click += (_, _) =>
        {
            // 開いた時点で同期済みの最新状態を反転(#7)
            Settings.Topmost = !Settings.Topmost;
            Dispatcher.Invoke(ApplyToAllClocks);
            Settings.Save();
        };

        var miClickThrough = new System.Windows.Forms.ToolStripMenuItem(L.T("tray.clickthrough"));
        miClickThrough.CheckOnClick = true;
        miClickThrough.Checked = Settings.ClickThrough;
        miClickThrough.Click += (_, _) =>
        {
            Settings.ClickThrough = !Settings.ClickThrough;
            Dispatcher.Invoke(ApplyToAllClocks);
            Settings.Save();
        };

        // 開くたびに最新の設定へ同期(#7)
        menu.Opening += (_, _) =>
        {
            miTopmost.Checked = Settings.Topmost;
            miClickThrough.Checked = Settings.ClickThrough;
        };

        var miRestart = new System.Windows.Forms.ToolStripMenuItem(L.T("tray.restart"));
        miRestart.Click += (_, _) => TryRestart("ユーザー操作による再起動");

        var miExit = new System.Windows.Forms.ToolStripMenuItem(L.T("tray.exit"));
        miExit.Click += (_, _) => Shutdown();

        menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
        {
            miSettings, new System.Windows.Forms.ToolStripSeparator(),
            miTopmost, miClickThrough, new System.Windows.Forms.ToolStripSeparator(),
            miRestart, miExit
        });

        _miSettings = miSettings;
        _miTopmost = miTopmost;
        _miClickThrough = miClickThrough;
        _miRestart = miRestart;
        _miExit = miExit;

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "TVClock Modern",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(() =>
        {
            if (Clocks.Count > 0) Clocks[0].ShowSettings();
        });

        // バルーン通知クリックでリリースページを開く(更新確認済みのときのみ)
        _tray.BalloonTipClicked += (_, _) =>
        {
            if (NewVersion is not null) OpenReleasePage();
        };

        // モニタ構成変化でウィンドウセットを同期
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) =>
            Dispatcher.BeginInvoke(() => EnsureClocks());
    }

    /// <summary>可能なら自分自身を再起動する。失敗しても例外は投げない。</summary>
    private void TryRestart(string? reason)
    {
        try
        {
            if (reason is not null) Logger.Info(reason);
            if (_ownsMutex && _settingsLoaded)
            {
                try { Settings.Save(); } catch { } // Environment.Exit は OnExit を通らないため明示保存
            }
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Error("自己再起動に失敗", ex);
            return;
        }
        Environment.Exit(0);
    }
}
