using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>8 方向のドラッグ ハンドルの矩形を保持する値オブジェクト。</summary>
/// <param name="TopLeft">左上のハンドル。</param>
/// <param name="TopCenter">上のハンドル。</param>
/// <param name="TopRight">右上のハンドル。</param>
/// <param name="MiddleLeft">左のハンドル。</param>
/// <param name="MiddleRight">右のハンドル。</param>
/// <param name="BottomLeft">左下のハンドル。</param>
/// <param name="BottomCenter">下のハンドル。</param>
/// <param name="BottomRight">右下のハンドル。</param>
/// <remarks>
/// ハンドルは常に 8 方向であるため、固定長の値として受け渡す。<br/>
/// <b>列挙順</b>: <see cref="TopLeft"/> → <see cref="TopCenter"/> → <see cref="TopRight"/> →<br/>
/// <see cref="MiddleLeft"/> → <see cref="MiddleRight"/> → <see cref="BottomLeft"/> →<br/>
/// <see cref="BottomCenter"/> → <see cref="BottomRight"/>。
/// </remarks>
public readonly record struct MarqueeHandleRects(
    EsRect TopLeft,
    EsRect TopCenter,
    EsRect TopRight,
    EsRect MiddleLeft,
    EsRect MiddleRight,
    EsRect BottomLeft,
    EsRect BottomCenter,
    EsRect BottomRight)
{
    /// <summary>ハンドルの数（常に 8）。</summary>
    /// <value>8。</value>
    public const int Count = 8;

    /// <summary>列挙順でハンドルの矩形を取得する。</summary>
    /// <param name="index">0〜7 の位置（0 = 左上 / 7 = 右下）。</param>
    /// <returns>指定位置の矩形。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> が 0〜7 の範囲外の場合。</exception>
    public EsRect this[int index] => index switch
    {
        0 => TopLeft,
        1 => TopCenter,
        2 => TopRight,
        3 => MiddleLeft,
        4 => MiddleRight,
        5 => BottomLeft,
        6 => BottomCenter,
        7 => BottomRight,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "ハンドルの位置は 0〜7 である必要があります。"),
    };
}
