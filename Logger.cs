using System.IO;

namespace TVClockModern;

/// <summary>
/// 例外・重要イベントの記録。%LOCALAPPDATA%\TVClockModern\logs\app-YYYYMMDD.log
/// ログ書き込み自体が失敗しても絶対に例外を投げない(クラッシュ耐性の最後の砦)。
/// </summary>
internal static class Logger
{
    private static readonly object _lock = new();

    public static string Dir
    {
        get
        {
            try
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TVClockModern", "logs");
            }
            catch { return Path.GetTempPath(); }
        }
    }

    public static void Info(string message) => Write("INFO ", message);

    public static void Error(Exception ex) => Write("ERROR", ex.ToString());

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message}\n{ex}");

    private static void Write(string level, string message)
    {
        try
        {
            var dir = Dir;
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"app-{DateTime.Now:yyyyMMdd}.log");
            lock (_lock)
            {
                File.AppendAllText(file,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");

                // 古いログを削除(7日分だけ保持)
                foreach (var old in Directory.GetFiles(dir, "app-*.log"))
                {
                    if (DateTime.Today - File.GetLastWriteTime(old).Date > TimeSpan.FromDays(7))
                    {
                        try { File.Delete(old); } catch { /* 削除失敗は無視 */ }
                    }
                }
            }
        }
        catch
        {
            // ログ不可の環境でもアプリは落とさない
        }
    }
}
