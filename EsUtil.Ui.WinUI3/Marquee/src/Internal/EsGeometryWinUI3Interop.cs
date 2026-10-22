using WinUIPoint = Windows.Foundation.Point;

namespace EsUtil.Ui.WinUI3.Marquee.Internal;

/// <summary>コア幾何型と WinUI（WinRT）の座標型を相互変換する。</summary>
/// <remarks>
/// コアロジック（<see cref="EsPoint"/> 等）は UI フレームワークに依存せず、UI 境界でのみ本クラスで変換する。<br/>
/// 変換は拡張メソッドとして提供し、呼び出し側は <c>using</c> 1 行のみで利用できる。<br/>
/// WinUI の幾何型（<see cref="WinUIPoint"/> 等）は <c>float</c> ベースの値型のため、値の変換で丸めが入る<br/>
/// （<see cref="EsPoint"/> は <c>double</c> を保持する）。<br/>
/// <b>色は変換しない</b>: 本 UI の色は要素へ渡すときにだけ XAML の色へ変換する（<see cref="MarqueeShapePlan.ToArgb"/> が担う）。
/// </remarks>
internal static class EsGeometryWinUI3Interop
{
    /// <summary><see cref="EsPoint"/> を WinUI の座標型へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>WinUI の座標。</returns>
    public static WinUIPoint ToWinUIPoint(this EsPoint point) => new(point.X, point.Y);

    /// <summary>WinUI の座標型を <see cref="EsPoint"/> へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>コアの座標。</returns>
    public static EsPoint ToEsPoint(this WinUIPoint point) => new(point.X, point.Y);
}
