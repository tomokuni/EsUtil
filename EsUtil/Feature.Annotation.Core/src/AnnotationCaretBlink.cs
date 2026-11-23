namespace EsUtil.Feature.Annotation;

/// <summary>本文の編集中に出すキャレットの点滅（規則は単一所有）。</summary>
/// <remarks>
/// <b>点滅させる</b>: 点灯したままの縦線は「選択枠の線」に見え、入力位置を示すキャレットだと分かりません。
/// 一般的なアプリと同じ速さで点滅させます（Windows の既定は 530 ms。 <c>GetCaretBlinkTime</c> と同じ）。<br/>
/// <b>止めない場面を作らない</b>: 文字入力・キャレット移動の直後は<b>点灯から</b>始めます（消えた瞬間に
/// 操作すると、入力位置を見失うため）。<br/>
/// <b>判断はコア・時計は UI</b>: 経過時間から「いま点灯か」を決める規則をここへ置き、UI はタイマーで
/// 再描画するだけにします（4 つの UI 版で速さと位相がずれない）。
/// </remarks>
public static class AnnotationCaretBlink
{
    /// <summary>点滅の間隔（ミリ秒。 点灯・消灯それぞれの長さ）。</summary>
    /// <value>530（Windows の既定のカーソル点滅速度）。</value>
    public const int IntervalMilliseconds = 530;

    /// <summary>点滅の間隔を取得する。</summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromMilliseconds(IntervalMilliseconds);

    /// <summary>経過時間から、キャレットを点灯して描くかを決める。</summary>
    /// <param name="elapsed">点滅を始めてからの経過時間（負の値は 0 として扱う）。</param>
    /// <returns>点灯している場合は <see langword="true"/>。</returns>
    public static bool IsVisible(TimeSpan elapsed)
    {
        var milliseconds = elapsed <= TimeSpan.Zero ? 0 : elapsed.TotalMilliseconds;
        return (int)(milliseconds / IntervalMilliseconds) % 2 == 0;
    }
}
