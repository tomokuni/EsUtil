namespace EsUtil.Platform.Core.Geometry;

/// <summary>小数の値を整数へ丸める方法。</summary>
/// <remarks>
/// <see langword="bool"/> で「切り捨てか四捨五入か」を指定すると呼び出し側で意味が読めないため、列挙型で指定する。<br/>
/// 整数系の値型（<see cref="EsPointInt"/> / <see cref="EsSizeInt"/> / <see cref="EsRectInt"/>）の変換は本列挙型を受け取り、<br/>
/// 丸め規則を <c>EsMath.RoundToInt</c> が単一所有する。<br/>
/// <b>各フレームワークの対応</b>: .NET 標準の <c>Point.Truncate</c> / <c>Point.Round</c> / <c>Point.Ceiling</c> に対応する。
/// </remarks>
public enum EsRounding
{
    /// <summary>0 の方向へ切り捨てる（<c>Math.Truncate</c> と同じ）。</summary>
    Truncate,

    /// <summary>最も近い整数へ丸める（既定。 0.5 は 0 から遠い方へ）。</summary>
    Round,

    /// <summary>正の無限大の方向へ切り上げる（<c>Math.Ceiling</c> と同じ）。</summary>
    Ceiling,
}
