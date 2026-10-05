using EsUtil.Feature.Loupe.Presenter.Drawing;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe;

/// <summary>ルーペのラベルの文言と基準点の導出を担う部分。</summary>
/// <remarks>
/// <b>文言の書式は描画層が単一所有する</b>: コアは幾何（拡大中心・表示サイズ）だけを返し、文言と基準点は描画の都合として本クラスが作る。<br/>
/// 基準点はルーペの左上から <see cref="LoupeAppearance.CoordLabelOffset"/> だけ内側の点である（ラベルは左上起点で置く。 中央寄せは持たない）。<br/>
/// フォントサイズ・角丸・色は <see cref="LoupeAppearance"/> が単一所有する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public partial class LoupePresenter
{
    /// <summary>中心座標ラベルの文言と基準点を解決する。</summary>
    /// <param name="geometry">描画の幾何（拡大中心・表示サイズを持つ）。</param>
    /// <param name="appearance">外観（表示可否とオフセットを持つ）。</param>
    /// <returns>中心座標ラベルのテキスト配置。 中心未設定の場合は null。</returns>
    /// <remarks>
    /// 文言は <see cref="EsPoint.Format(int)"/> が文字列化する（座標の書式はコアの値型が単一所有する）。<br/>
    /// <b>表示可否（<paramref name="appearance"/> の <c>ShowCoordLabel</c>）では本値を消さない</b><br/>
    /// （文言は値として残し、描くかどうかは描画側が判断する。 配置指示の <c>IsVisible</c> が表示可否を持つ）。
    /// </remarks>
    internal static LoupeTextPlacement? ResolveCoordLabelText(in LoupeGeometry geometry, LoupeAppearance appearance)
    {
        if (geometry.Center is not { } center)
        {
            return null;
        }

        var offset = appearance.CoordLabelOffset;

        return new LoupeTextPlacement(
            center.Format(),
            new EsPoint(offset, offset),
            default);
    }
}
