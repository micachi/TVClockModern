using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TVClockModern;

/// <summary>
/// MonitorPositions の寛容な読み書き。null 値・要素数不一致・非有限数を含む
/// エントリーは読み飛ばす(#5: 破損 JSON でもデシリアライズ例外を起こさない)。
/// </summary>
public sealed class MonitorPositionsConverter : JsonConverter<Dictionary<string, double[]>>
{
    public override Dictionary<string, double[]>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

        var dict = new Dictionary<string, double[]>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return dict;
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();
            var key = reader.GetString() ?? "";
            reader.Read();
            if (reader.TokenType != JsonTokenType.StartArray) { reader.Skip(); continue; }

            var values = new List<double>();
            var valid = true;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var v) && double.IsFinite(v))
                    values.Add(v);
                else
                    valid = false;
            }
            if (valid && values.Count == 2)
                dict[key] = values.ToArray();
        }
        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, double[]> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var kv in value)
        {
            // 異常エントリー(要素数不一致・非有限数)は読み飛ばす(Read の寛容さと対称に)
            if (kv.Key is null || kv.Value is not { Length: 2 }
                || !double.IsFinite(kv.Value[0]) || !double.IsFinite(kv.Value[1]))
                continue;
            writer.WritePropertyName(kv.Key);
            writer.WriteStartArray();
            writer.WriteNumberValue(kv.Value[0]);
            writer.WriteNumberValue(kv.Value[1]);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}

/// <summary>
/// アプリ設定。JSONで %APPDATA%\TVClockModern\settings.json に保存。
/// - 書き込みは「バックアップ → 一時ファイル → 原子的置換」で破損を防ぐ
/// - 読み込みは「本体 → バックアップ → 既定値」の3段階フォールバック
/// - 本体が破損してバックアップから復旧した場合、本体を退避してから復帰する
///   (次の Save が正常なバックアップを破損データで上書きしないように)
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
    public double ProximityOpacity { get; set; } = 0.2; // 接近時の不透明度
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
    /// <summary>UI 言語: auto (システム UI カルチャに従う) / ja / en / zh-Hans / ko</summary>
    public string Language { get; set; } = "auto";
    /// <summary>起動時に更新を確認する</summary>
    public bool CheckUpdates { get; set; } = true;
    /// <summary>最終更新確認時刻 (UTC, ISO 8601)。空または不正値=未確認</summary>
    public string LastUpdateCheck { get; set; } = "";
    /// <summary>最後に使った位置プリセット(桁数変化でサイズが変わった時に再整列するのに使う)</summary>
    public string PositionPreset { get; set; } = "";
    /// <summary>マルチモニタ同時表示時のデバイス別位置 [left, top]</summary>
    [JsonConverter(typeof(MonitorPositionsConverter))]
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

    /// <summary>書式が .NET のカスタム日時書式として有効か(プレビュー/保存/ロード/インポート共通)。</summary>
    public static bool IsValidFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return false;
        try { DateTime.Now.ToString(format); return true; }
        catch { return false; }
    }

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
            // 本体を退避する(残すと次の Save が正常なバックアップを破損データで上書きする)
            MoveMainAside();
            return s;
        }

        // 3) 既定値(破損ファイルは .corrupt として退避)
        MoveMainAside();
        Logger.Info("既定設定で起動");
        return new ClockSettings();
    }

    /// <summary>本体ファイルを .corrupt-日時 として退避する(失敗しても無視)。</summary>
    private static void MoveMainAside()
    {
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

    /// <summary>非有限値を既定値へ戻し、有限値は範囲内に丸める。</summary>
    private static double ClampFinite(double v, double min, double max, double def)
        => double.IsFinite(v) ? Math.Clamp(v, min, max) : def;

    /// <summary>異常値の丸め。壊れた設定値・NaN・null 辞書で UI/座標計算が爆発しないようにする。
    /// インポートした設定にも使用する。何度適用しても結果が変わらない(冪等)。</summary>
    public void Sanitize()
    {
        // 書式: 不正なものは安全な既定へ正規化(空の日付書式=非表示は有効)
        if (!IsValidFormat(TimeFormat)) TimeFormat = "HH:mm";
        if (DateFormat is null || (DateFormat.Length > 0 && !IsValidFormat(DateFormat)))
            DateFormat = "";

        var validEasings = new[] { "linear", "easeout", "easein" };
        if (!validEasings.Contains(FadeEasing)) FadeEasing = "easeout";
        var validMods = new[] { "Ctrl", "Alt", "Shift", "None" };
        if (!validMods.Contains(ModifierKey)) ModifierKey = "Ctrl";
        var validLanguages = new[] { "auto", "ja", "en", "zh-Hans", "ko" };
        if (!validLanguages.Contains(Language)) Language = "auto";
        var validEffects = new[] { "none", "flipx", "flipy", "slideup", "slidedown", "fade", "crossfade" };
        if (!validEffects.Contains(TransitionEffect)) TransitionEffect = "crossfade";

        FontSize = ClampFinite(FontSize, 12, 200, 64);
        WindowOpacity = ClampFinite(WindowOpacity, 0.1, 1.0, 1.0);
        ShadowBlur = ClampFinite(ShadowBlur, 0, 60, 6);
        ShadowOpacity = ClampFinite(ShadowOpacity, 0, 1, 0.85);
        OutlineThickness = ClampFinite(OutlineThickness, 0.5, 20, 2);
        LetterSpacing = ClampFinite(LetterSpacing, 0, 100, 0);
        SeparatorSpacing = ClampFinite(SeparatorSpacing, 0, 200, 0);
        ProximityDistance = (int)ClampFinite(ProximityDistance, 0, 2000, 100);
        ProximityOpacity = ClampFinite(ProximityOpacity, 0.05, 1.0, 0.2);
        FadeDurationMs = (int)ClampFinite(FadeDurationMs, 50, 500, 200);
        Left = ClampFinite(Left, -20000, 20000, 100);
        Top = ClampFinite(Top, -20000, 20000, 100);

        // モニター位置辞書: null / 不正エントリーを除去し、正常なものだけ残す
        var validPositions = new Dictionary<string, double[]>();
        if (MonitorPositions is not null)
        {
            foreach (var kv in MonitorPositions)
            {
                if (kv.Key is null || kv.Value is not { Length: 2 }) continue;
                var x = kv.Value[0]; var y = kv.Value[1];
                if (!double.IsFinite(x) || !double.IsFinite(y)) continue;
                validPositions[kv.Key] = new[]
                {
                    Math.Clamp(x, -20000, 20000),
                    Math.Clamp(y, -20000, 20000),
                };
            }
        }
        MonitorPositions = validPositions;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(this, JsonOpts);

            // 変更がなければ何も書かない(毎分の自動保存がディスクに触れない)
            if (File.Exists(MainPath) && File.ReadAllText(MainPath) == json)
                return;

            // 直前の正常ファイルをバックアップへ(本体と同じく原子的置換。
            // File.Copy の途中で落ちてもバックアップが半端にならないように)
            if (File.Exists(MainPath))
            {
                var bakTmp = BackupPath + ".tmp";
                File.Copy(MainPath, bakTmp, overwrite: true);
                if (File.Exists(BackupPath))
                    File.Replace(bakTmp, BackupPath, null);
                else
                    File.Move(bakTmp, BackupPath);
            }

            var tmp = MainPath + ".tmp";
            File.WriteAllText(tmp, json);

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
