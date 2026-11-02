using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe;

/// <summary>ルーペの十字線（縦 1 本・横 1 本）の矩形を保持する値オブジェクト。</summary>
/// <param name="Vertical">縦線の矩形。</param>
/// <param name="Horizontal">横線の矩形。</param>
/// <remarks>
/// 十字線は常に縦 1 本・横 1 本であるため、固定長の値として受け渡す。<br/>
/// <b>列挙順</b>: <see cref="Vertical"/> → <see cref="Horizontal"/>。
/// </remarks>
public readonly record struct LoupeCrosshairRects(EsRect Vertical, EsRect Horizontal)
{
    /// <summary>十字線の本数（常に 2）。</summary>
    /// <value>2。</value>
    public const int Count = 2;

    /// <summary>列挙順（縦・横）で矩形を取得する。</summary>
    /// <param name="index">0（縦）または 1（横）。</param>
    /// <returns>指定位置の矩形。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> が 0〜1 の範囲外の場合。</exception>
    public EsRect this[int index] => index switch
    {
        0 => Vertical,
        1 => Horizontal,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "十字線の位置は 0〜1 である必要があります。"),
    };
}
