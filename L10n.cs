using System.Threading;

namespace TVClockModern;

/// <summary>
/// UI 多言語。埋め込み JSON 辞書(TVClockModern.Strings.{code}.json)から読み込む。
/// - L.T(key) で現在言語の文字列を取得(欠落時は日本語、それも無ければ key)
/// - Language 設定: "auto"(システム UI カルチャから自動) / "ja" / "en" / "zh-Hans" / "ko"
/// - 日時の表示カルチャはシステムに従う(本クラスは UI 文字列のみ担当)
/// </summary>
internal static class L
{
    private const string Fallback = "ja";
    private static Dictionary<string, string> _current = new();
    private static Dictionary<string, string> _fallback = new();

    /// <summary>現在の言語コード(ja/en/zh-Hans/ko)。</summary>
    public static string CurrentLanguage { get; private set; } = Fallback;

    /// <summary>App.OnStartup で設定ロード後に一度だけ呼ぶ。</summary>
    public static void Init(string setting)
    {
        var code = setting switch
        {
            "ja" => "ja",
            "en" => "en",
            "zh-Hans" => "zh-Hans",
            "ko" => "ko",
            _ => AutoDetect(),
        };
        CurrentLanguage = code;
        _fallback = Load(Fallback) ?? new Dictionary<string, string>();
        _current = code == Fallback ? _fallback : Load(code) ?? _fallback;
    }

    private static string AutoDetect()
    {
        var ui = Thread.CurrentThread.CurrentUICulture.Name;
        if (ui.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja";
        if (ui.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-Hans"; // 繁体も簡体辞書で代替
        if (ui.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return "ko";
        return "en";
    }

    public static string T(string key)
        => _current.TryGetValue(key, out var v) ? v
         : _fallback.TryGetValue(key, out var f) ? f
         : key;

    public static string T(string key, string arg) => string.Format(T(key), arg);

    private static Dictionary<string, string>? Load(string code) => code switch
    {
        "ja" => StringsData.StringsJa,
        "en" => StringsData.StringsEn,
        "zh-Hans" => StringsData.StringsZhHans,
        "ko" => StringsData.StringsKo,
        _ => null,
    };
}
