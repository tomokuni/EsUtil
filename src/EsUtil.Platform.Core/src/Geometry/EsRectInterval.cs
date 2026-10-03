namespace EsUtil.Platform.Core.Geometry;

/// <summary>矩形の幅・高さが終点を含むかどうか（区間の流儀）。</summary>
/// <remarks>
/// 同じ「幅 10」でも、始点のピクセルから 10 個を含むのか（半開区間）、終点のピクセルを含めて 11 個になるのか（閉区間）で<br/>
/// 実際のピクセル数が変わる。 <see langword="bool"/> で受け渡すと意味が読めないため、列挙型で指定する。<br/>
/// <br/>
/// <b>用途</b>: ドラッグで作る矩形は「始点から終点まで（両端を含む）」を意図するため <see cref="Closed"/>、<br/>
/// OS の検出結果（<c>RECT</c> 等）は終点を含まないピクセル数のため <see cref="HalfOpen"/> として扱う。<br/>
/// ピクセル数へ変換するときの <c>+1</c> の判断は本値が単一所有し、下流の層は再判定しない。<br/>
/// <b>既定</b>: <see cref="HalfOpen"/>（幅 = ピクセル数。 従来の挙動を変えない）。
/// </remarks>
public enum EsRectInterval
{
    /// <summary>始点を含み終点を含まない（幅 = ピクセル数）。</summary>
    HalfOpen,

    /// <summary>始点と終点の両方を含む（幅 = ピクセル数 − 1）。</summary>
    Closed,
}
