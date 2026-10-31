using System;

namespace TVClockModern;

/// <summary>
/// モダンモードの時刻進行の計算(純粋関数・UI非依存)。
/// 秒は数字で出さず、この割合を色の広がり・液面として描く。
/// </summary>
internal static class ModernClockMath
{
    /// <summary>分の中での進み具合 0..1(ミリ秒まで含む)。0秒=0、30秒=0.5、59.999秒≈1。</summary>
    public static double Fraction(DateTime now) =>
        Math.Clamp((now.Second + now.Millisecond / 1000.0) / 60.0, 0.0, 1.0);

    /// <summary>
    /// 分の頭で「満タン状態」をどれだけ残すか 0..1。
    /// secondsIntoMinute が duration を超えると 0。分の変わり目をシームレスに見せるための減衰値。
    /// </summary>
    public static double Rollover(double secondsIntoMinute, double duration) =>
        duration <= 0 ? 0 : Math.Clamp(1.0 - secondsIntoMinute / duration, 0.0, 1.0);

    /// <summary>液面の高さ 0..1(リキッドテーマ)。分の頭は満タンから素早く落ちる。</summary>
    public static double LiquidLevel(double fraction, double rollover) =>
        Math.Clamp(Math.Max(fraction, rollover), 0.0, 1.0);

    /// <summary>
    /// 時刻書式を「分の指定子より前 / 分 / 後」へ分割する。
    /// リキッドテーマは分の文字だけを変化させるため、どの部分が分かを知る必要がある。
    /// 例: "h:mm" → ("h:", "mm", "") / "H時m分" → ("H時", "m", "分")
    /// 分の指定子(m の連続)が無ければ、全体を前半として返す。
    /// </summary>
    public static (string PrefixFormat, string MinuteFormat, string SuffixFormat) SplitMinuteFormat(string format)
    {
        if (string.IsNullOrEmpty(format)) return ("", "", "");

        var end = format.LastIndexOf('m');
        if (end < 0) return (format, "", "");

        var start = end;
        while (start > 0 && format[start - 1] == 'm') start--;

        return (format.Substring(0, start), format.Substring(start, end - start + 1), format.Substring(end + 1));
    }

    /// <summary>SplitMinuteFormat の結果を実際の時刻で文字列にする。</summary>
    public static (string Prefix, string Minute, string Suffix) SplitMinute(DateTime now, string format)
    {
        var (pf, mf, sf) = SplitMinuteFormat(format);
        string F(string f)
        {
            if (f.Length == 0) return "";
            try { return now.ToString(EscapeSingle(f)); } catch { return ""; }
        }
        return (F(pf), F(mf), F(sf));
    }

    /// <summary>
    /// 1文字だけの書式は .NET では「標準書式」と解釈されるため、そのまま渡すと
    /// "m" が月日、"分" のようなリテラル1文字は例外になる。指定子なら % を付け、
    /// リテラルなら引用符で囲んで意図どおりに解釈させる。
    /// </summary>
    private static string EscapeSingle(string f)
    {
        if (f.Length != 1) return f;
        return "mMhHsstdFfgyKz".IndexOf(f[0]) >= 0 ? "%" + f : "'" + f + "'";
    }
}
