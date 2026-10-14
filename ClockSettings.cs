using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TVClockModern;

/// <summary>
/// アプリ設定。JSONで %APPDATA%\TVClockModern\settings.json に保存。
/// - 書き込みは「バックアップ → 一時ファイル → 原子的置換」で破損を防ぐ
/// - 読み込みは「本体 → バックアップ → 既定値」の3段階フォールバック
/// </summary>
public sealed class ClockSettings
{
    // --- 表示 ---
    public string TimeFormat { get; set; } = "HH:mm";
    public string DateFormat { get; set; } = "yyyy/MM/dd (ddd)";
    public string FontFamily { get; set; } = "Yu Gothic UI";
    public double FontSize { get; set; } = 64;
    public string TextColor { get; set; } = "#FFFFFFFF";
    public double LetterSpacing { get; set; } = 0;     // 文字間隔 (DIP)
    public double SeparatorSpacing { get; set; } = 0;  // 区切り文字の前後間隔 (DIP)
    /// <summary>時刻切り替え効果: none/flipx/flipy/slideup/slidedown/fade/crossfade</summary>
    public string TransitionEffect { get; set; } = "crossfade";
    public int TransitionDurationMs { get; set; } = 200;
    public bool TextShadow { get; set; } = true;
    public double ShadowBlur { get; set; } = 6;      // 影の大きさ(ぼかし半径)
    public double ShadowOpacity { get; set; } = 0.85; // 影の濃さ
    public bool OutlineEnabled { get; set; } = true; // 文字縁取り(既定オン・2px)
    public string OutlineColor { get; set; } = "#FF000000";
    public double OutlineThickness { get; set; } = 2;
    public double WindowOpacity { get; set; } = 1.0;
    public bool ShowDate { get; set; } = false;

    // --- マウス接近時のフェード ---
    public bool ProximityEnabled { get; set; } = false;
    public int ProximityDistance { get; set; } = 100;   // 感度(判定距離 px)
    public double ProximityOpacity { get; set; } = 1.0; // 接近時の不透明度
    public int FadeDurationMs { get; set; } = 300;      // フェード時間(短い=速い)
    /// <summary>フェードのイージング: linear / easeout / easein</summary>
    public string FadeEasing { get; set; } = "easeout";

    // --- 動作 ---
    public bool Topmost { get; set; } = true;
    public bool ClickThrough { get; set; } = false;
    public bool AutoStart { get; set; } = false;
    public string TargetMonitor { get; set; } = ""; // ""=自動 / Screen.DeviceName
    /// <summary>クリックスルー中の透明解除キー: Ctrl/Alt/Shift/None</summary>
    public string ModifierKey { get; set; } = "Ctrl";
    public bool ShowOnAllMonitors { get; set; } = false;
    /// <summary>最後に使った位置プリセット(桁数変化でサイズが変わった時に再整列するのに使う)</summary>
    public string PositionPreset { get; set; } = "";
    /// <summary>マルチモニタ同時表示時のデバイス別位置 [left, top]</summary>
    public Dictionary<string, double[]> MonitorPositions { get; set; } = new();

    // --- 位置(最終表示位置) ---
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;

    [JsonIgnore]
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TVClockModern");

    [JsonIgnore]
    private static string MainPath => Path.Combine(Dir, "settings.json");
    [JsonIgnore]
    private static string BackupPath => Path.Combine(Dir, "settings.bak");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static ClockSettings Load()
    {
        // 1) 本体
        var s = TryRead(MainPath);
        if (s is not null) return s;

        // 2) バックアップ
        s = TryRead(BackupPath);
        if (s is not null)
        {
            Logger.Error($"設定ファイルが破損していたためバックアップから復元: {MainPath}");
            return s;
        }

        // 3) 既定値(破損ファイルは .corrupt として退避)
        try
        {
            if (File.Exists(MainPath))
            {
                var corrupt = MainPath + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}";
                File.Move(MainPath, corrupt);
                Logger.Error($"設定ファイルを退避: {corrupt}");
            }
        }
        catch (Exception ex) { Logger.Error("破損設定の退避に失敗", ex); }

        Logger.Info("既定設定で起動");
        return new ClockSettings();
    }

    private static ClockSettings? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var s = JsonSerializer.Deserialize<ClockSettings>(json, JsonOpts);
            if (s is null) return null;
            s.Sanitize();
            return s;
        }
        catch (Exception ex)
        {
            Logger.Error($"設定の読み込み失敗: {path}", ex);
            return null;
        }
    }

    /// <summary>異常値の丸め。壊れた設定値で UI/座標計算が爆発しないようにする。</summary>
    /// <summary>異常値の丸め。壊れた設定値で UI/座標計算が爆発しないようにする。インポートした設定にも使用。</summary>
    public void Sanitize()
    {
        TimeFormat = string.IsNullOrWhiteSpace(TimeFormat) ? "HH:mm" : TimeFormat;
        DateFormat ??= "";
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "Yu Gothic UI" : FontFamily;
        FontSize = Math.Clamp(FontSize, 8, 400);
        WindowOpacity = Math.Clamp(WindowOpacity, 0.1, 1.0);
        ShadowBlur = Math.Clamp(ShadowBlur, 0, 60);
        ShadowOpacity = Math.Clamp(ShadowOpacity, 0, 1);
        OutlineThickness = Math.Clamp(OutlineThickness, 0.5, 20);
        ProximityDistance = Math.Clamp(ProximityDistance, 0, 2000);
        ProximityOpacity = Math.Clamp(ProximityOpacity, 0.05, 1.0);
        FadeDurationMs = Math.Clamp(FadeDurationMs, 30, 3000);
        var validEasings = new[] { "linear", "easeout", "easein" };
        if (!validEasings.Contains(FadeEasing)) FadeEasing = "easeout";
        var validMods = new[] { "Ctrl", "Alt", "Shift", "None" };
        if (!validMods.Contains(ModifierKey)) ModifierKey = "Ctrl";
        var validEffects = new[] { "none", "flipx", "flipy", "slideup", "slidedown", "fade", "crossfade" };
        if (!validEffects.Contains(TransitionEffect)) TransitionEffect = "crossfade";
        LetterSpacing = Math.Clamp(LetterSpacing, 0, 100);
        SeparatorSpacing = Math.Clamp(SeparatorSpacing, 0, 200);
        TransitionDurationMs = Math.Clamp(TransitionDurationMs, 50, 500);
        Left = Math.Clamp(Left, -20000, 20000);
        Top = Math.Clamp(Top, -20000, 20000);
        var validPositions = new Dictionary<string, double[]>();
        foreach (var kv in MonitorPositions)
            if (kv.Value is { Length: 2 })
                validPositions[kv.Key] = new[]
                {
                    Math.Clamp(kv.Value[0], -20000, 20000),
                    Math.Clamp(kv.Value[1], -20000, 20000),
                };
        MonitorPositions = validPositions;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);

            // 直前の正常ファイルをバックアップへ
            if (File.Exists(MainPath))
                File.Copy(MainPath, BackupPath, overwrite: true);

            var tmp = MainPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOpts));

            if (File.Exists(MainPath))
                File.Replace(tmp, MainPath, null);
            else
                File.Move(tmp, MainPath);
        }
        catch (Exception ex)
        {
            Logger.Error("設定の保存に失敗", ex);
        }
    }
}
