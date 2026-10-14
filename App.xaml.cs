using System.Windows;
using Application = System.Windows.Application;

namespace TVClockModern;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private static bool _ownsMutex;

    /// <summary>稼働中の時計ウィンドウ(マルチモニタ同時表示時は複数)</summary>
    public static List<MainWindow> Clocks { get; } = new();

    /// <summary>設定画面が開いているか(時計クリック時の前面再登場判定用)</summary>
    public static bool SettingsWindowOpen;
    private System.Windows.Forms.NotifyIcon? _tray;
    private int _dispatchedExceptions;

    public static ClockSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        // --- タスク全体を保護: ここで落としたら時計が消える ---
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("Dispatcher 未処理例外", args.Exception);
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

        // --- 単一インスタンス(再起動競合に備えて最大5秒待機) ---
        var isNew = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\TVClockModern.SingleInstance", out isNew);
            if (isNew) break;
            _singleInstanceMutex.Dispose();
            if (DateTime.UtcNow > deadline)
            {
                Logger.Info("既存インスタンスがあるため終了");
                Shutdown();
                return;
            }
            Thread.Sleep(250); // 前インスタンスの終了を待つ
        }
        _ownsMutex = true;

        // --- 設定ロード(失敗時もフォールバック済み) ---
        Settings = ClockSettings.Load();

        base.OnStartup(e);

        try { InitTray(); }
        catch (Exception ex) { Logger.Error("タスクトレイ初期化失敗", ex); }

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
                // 単一モード: 1つだけ残す
                for (var i = Clocks.Count - 1; i >= 1; i--)
                {
                    Logger.Info("単一モード: 余分な時計を閉じる");
                    Clocks[i].Close();
                    Clocks.RemoveAt(i);
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
            for (var i = Clocks.Count - 1; i >= 0; i--)
            {
                if (Clocks[i].MonitorDevice == "")
                {
                    Logger.Info("単一モード時計を閉じる(全モニタモードへ切替)");
                    Clocks[i].Close(); Clocks.RemoveAt(i); continue;
                }
                if (screens.All(s => s.DeviceName != Clocks[i].MonitorDevice))
                {
                    Logger.Info("切断モニタの時計を閉じる: " + Clocks[i].MonitorDevice);
                    Clocks[i].Close();
                    Clocks.RemoveAt(i);
                }
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

    protected override void OnExit(ExitEventArgs e)
    {
        try { Settings.Save(); } catch { /* 保存失敗は無視 */ }
        try { _tray?.Dispose(); } catch { }
        try { if (_ownsMutex) _singleInstanceMutex?.ReleaseMutex(); } catch { }
        try { _singleInstanceMutex?.Dispose(); } catch { }
        base.OnExit(e);
    }

    // ---------- タスクトレイ(クリックスルー時の生命線) ----------
    private void InitTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();

        var miSettings = new System.Windows.Forms.ToolStripMenuItem("設定(&S)");
        miSettings.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            if (Clocks.Count > 0) Clocks[0].ShowSettings();
        });

        var miTopmost = new System.Windows.Forms.ToolStripMenuItem("常に手前に表示(&T)");
        miTopmost.CheckOnClick = true;
        miTopmost.Checked = Settings.Topmost;
        miTopmost.Click += (_, _) =>
        {
            Settings.Topmost = miTopmost.Checked;
            Dispatcher.Invoke(ApplyToAllClocks);
            Settings.Save();
        };

        var miClickThrough = new System.Windows.Forms.ToolStripMenuItem("クリックスルー(&C)");
        miClickThrough.CheckOnClick = true;
        miClickThrough.Checked = Settings.ClickThrough;
        miClickThrough.Click += (_, _) =>
        {
            Settings.ClickThrough = miClickThrough.Checked;
            Dispatcher.Invoke(ApplyToAllClocks);
            Settings.Save();
        };

        var miRestart = new System.Windows.Forms.ToolStripMenuItem("再起動(&R)");
        miRestart.Click += (_, _) => TryRestart("ユーザー操作による再起動");

        var miExit = new System.Windows.Forms.ToolStripMenuItem("終了(&X)");
        miExit.Click += (_, _) => Shutdown();

        menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
        {
            miSettings, new System.Windows.Forms.ToolStripSeparator(),
            miTopmost, miClickThrough, new System.Windows.Forms.ToolStripSeparator(),
            miRestart, miExit
        });

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "TVClock Modern",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(() =>
        {
            if (Clocks.Count > 0) Clocks[0].ShowSettings();
        });

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
