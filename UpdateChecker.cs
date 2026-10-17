using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace TVClockModern;

/// <summary>
/// GitHub Releases からの更新確認。
/// - API: releases/latest (プレリリースは除外される)
/// - 起動時は 24h に 1 回まで(LastUpdateCheck でスロットル)、情報タブから手動確認も可
/// - 失敗時は呼び出し側で黙ってログに記録する(オフライン起動で通知しない)
/// - テレメトリなし。GitHub に対しては GET のみ
/// </summary>
internal static class UpdateChecker
{
    public const string ReleaseUrl = "https://github.com/micachi/TVClockModern/releases/latest";
    private const string ApiUrl = "https://api.github.com/repos/micachi/TVClockModern/releases/latest";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    public static Version CurrentVersion
        => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>起動時の自動確認を行うべきか(設定 ON かつ 24h 以上未確認)。</summary>
    public static bool ShouldCheck()
    {
        if (!App.Settings.CheckUpdates) return false;
        if (!DateTime.TryParse(App.Settings.LastUpdateCheck, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var last))
            return true; // 未確認・不正値
        return DateTime.UtcNow - last > CheckInterval;
    }

    /// <summary>確認時刻を記録する(値のみ設定。永続化は通常の自動保存に任せる)。UI スレッドで呼ぶ。</summary>
    public static void MarkChecked()
        => App.Settings.LastUpdateCheck = DateTime.UtcNow.ToString("o");

    /// <summary>
    /// 設定に保存された未対応の新バージョンタグを返す(あれば UI スレッドで呼ぶ)。
    /// 現在より新しくなければ(インストール済み等)保存値をクリアして null を返す。
    /// </summary>
    public static string? GetPendingUpdate()
    {
        var t = App.Settings.PendingUpdate;
        if (string.IsNullOrEmpty(t)) return null;
        if (IsNewer(t)) return t;
        App.Settings.PendingUpdate = "";
        return null;
    }

    /// <summary>タグ(vX.Y.Z)が現在より新しいか。</summary>
    public static bool IsNewer(string tagName)
    {
        var v = tagName.TrimStart('v', 'V');
        return Version.TryParse(v, out var parsed) && parsed > CurrentVersion;
    }

    /// <summary>
    /// GitHub API で最新リリースを取得し、現在より新しい場合はタグを返す
    /// (新しいものでなければ null、通信エラーは例外)。
    /// LastUpdateCheck/PendingUpdate の記録は呼び出し側(UI スレッド)で行うこと。
    /// </summary>
    public static async Task<string?> GetLatestVersionAsync()
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        // GitHub API は User-Agent 必須
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"TVClockModern/{CurrentVersion}");

        var json = await http.GetStringAsync(ApiUrl);
        using var doc = JsonDocument.Parse(json);
        var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        return IsNewer(tag) ? tag : null;
    }
}
