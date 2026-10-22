using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinUI3.Marquee.Internal;

/// <summary>連続するポインター押下がダブルクリック（2 回目の押下）かどうかを判定する純粋関数。</summary>
/// <remarks>
/// <b>位置付け</b>: WinUI 3 のポインター イベントはクリック数を報告しないため、本 UI が判定する。<br/>
/// コントロールは「直前の押下位置と時刻」を保持し、押下のたびに本クラスへ問い合わせてクリック数を決める<br/>
/// （コアは 2 以上をダブルクリックとして扱い、選択矩形の内側なら確定・外側ならキャンセルを要求する）。<br/>
/// <br/>
/// <b>時間の上限は OS の設定値</b>を使う（<see cref="Win32Interop.DoubleClickIntervalMilliseconds"/>）。<br/>
/// <b>距離の上限</b>は本クラスが単一所有する（同じ場所を 2 回押した場合だけダブルクリックと見なす）。<br/>
/// 時刻と座標を引数で受け取る純粋関数であるため、UI スレッドなしの <c>[Fact]</c> で検証できる。
/// </remarks>
internal static class DoubleClickDetector
{
    /// <summary>ダブルクリックと見なす 2 回の押下の距離の上限（DIP）。</summary>
    /// <remarks>指のぶれ・マウスの 1 画素のずれを許容しつつ、離れた 2 点を別の操作として扱う値とする。</remarks>
    public const double MaxMoveDip = 4.0;

    /// <summary>単独の押下を表すクリック数。</summary>
    /// <remarks>コアは 1 を「新規選択またはハンドルの操作」として扱う。</remarks>
    public const int SingleClickCount = 1;

    /// <summary>ダブルクリックを表すクリック数（コアは 2 以上をダブルクリックとして扱う）。</summary>
    public const int DoubleClickCount = 2;

    /// <summary>2 回目の押下がダブルクリックかどうかを判定する。</summary>
    /// <param name="previous">直前の押下位置（ローカル DIP）。</param>
    /// <param name="current">今回の押下位置（ローカル DIP）。</param>
    /// <param name="elapsedMilliseconds">直前の押下からの経過時間（ミリ秒）。 負値は判定不能として扱う。</param>
    /// <param name="intervalMilliseconds">ダブルクリックと見なす時間の上限（ミリ秒。 OS の設定値）。</param>
    /// <returns>時間と距離の両方が上限以内の場合は true。</returns>
    /// <remarks>
    /// 上限ちょうどは「以内」（<c>&lt;=</c>）として扱う（OS の設定値は境界を含む上限である）。<br/>
    /// 負の経過時間（時計の巻き戻り・未初期化）は連続した押下と見なさない。
    /// </remarks>
    public static bool IsDoubleClick(
        EsPoint previous, EsPoint current, long elapsedMilliseconds, long intervalMilliseconds)
        => elapsedMilliseconds >= 0
        && elapsedMilliseconds <= intervalMilliseconds
        && Math.Abs(current.X - previous.X) <= MaxMoveDip
        && Math.Abs(current.Y - previous.Y) <= MaxMoveDip;
}
