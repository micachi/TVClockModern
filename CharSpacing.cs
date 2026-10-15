using System.Windows;

namespace TVClockModern;

/// <summary>
/// 文字ごとの余白計算。実時計(MainWindow.UpdateChars)と
/// 設定プレビュー(SettingsWindow)で共用する。
/// </summary>
internal static class CharSpacing
{
    /// <summary>指定文字の余白。区切り文字の前後に区切り間隔、全文字の後に文字間隔を付ける。</summary>
    public static Thickness GetCharMargin(char c, int index, int length, double letterSpacing, double separatorSpacing)
    {
        var isSep = !char.IsLetterOrDigit(c);
        var left = isSep ? separatorSpacing / 2 : 0;
        var right = (isSep ? separatorSpacing / 2 : 0) + letterSpacing;
        if (index == 0) left = 0;
        if (index == length - 1) right = 0;
        return new Thickness(left, 0, right, 0);
    }
}
