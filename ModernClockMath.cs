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
}
