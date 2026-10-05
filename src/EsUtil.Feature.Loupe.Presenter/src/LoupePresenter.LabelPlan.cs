using EsUtil.Feature.Loupe.Presenter.Drawing;
using EsUtil.Platform.Core.Geometry;


namespace EsUtil.Feature.Loupe;

/// <summary>ルーペの中心座標ラベルの配置指示を解決する（実寸 → 配置矩形の写像）。</summary>
/// <remarks>
/// <b>位置付け</b>: 「文字を UI 要素として配置する」UI のための変換である。 描画層（<see cref="LoupePresenter"/>）は<br/>
/// 文言・基準点を決めるが、実際の配置矩形はテキストの実寸に依存するため、<br/>
/// 実寸測定（描画 API 依存）を UI 層から受け取って要素の矩形へ変換する。<br/>
/// 体裁（内側余白・フォントサイズ・色・角丸）は外観（<see cref="LoupeAppearance"/>）が単一所有する。<br/>
/// <br/>
/// <b>中心座標ラベルは常に最上位</b>である（UI の要素は親より後に描かれるため構造で成立する）。<br/>
/// UI フレームワークに依存しない（測定関数をデリゲートで受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public partial class LoupePresenter
{
    /// <summary>中心座標ラベル背景の左右の内側余白（DIP）。</summary>
    /// <remarks>上下（<see cref="PaddingY"/>）より広くする（文字の左右に余白を確保するため）。</remarks>
    private const double PaddingX = 4.0;

    /// <summary>中心座標ラベル背景の上下の内側余白（DIP）。</summary>
    /// <remarks>左右（<see cref="PaddingX"/>）より狭くする（ルーペ上端の帯へ収めるため）。</remarks>
    private const double PaddingY = 1.0;

    /// <summary>中心座標ラベルの配置指示を解決する。</summary>
    /// <param name="coordLabel">描画層が解決したテキスト配置。 表示しない場合は null。</param>
    /// <param name="appearance">外観（色・フォントサイズ・角丸。 本層が単一所有する）。 null 不可。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。 null 不可。</param>
    /// <returns>配置するラベル要素（1 要素）。 中心未設定の場合は空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> または <paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// 文言・位置は描画層が、色・フォントサイズ・角丸は外観が単一所有する（本クラスは実寸の反映のみ）。<br/>
    /// <b>表示可否でも配置指示を消さない</b>: 表示可否は指示の <c>IsVisible</c> が持ち、<br/>
    /// UI は要素として置くかどうかを本値で判断する（止めた項目の文言・位置も値として残る）。
    /// </remarks>
    internal static IReadOnlyList<LoupeLabelPlacement> ResolveLabelPlacements(
        LoupeTextPlacement? coordLabel, LoupeAppearance appearance, Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(measureText);

        return coordLabel is { } label
            ? [ResolveElement(label, appearance, isVisible: appearance.ShowCoordLabel, measureText)]
            : [];
    }

    /// <summary>テキスト配置と体裁から、要素の配置指示を組み立てる。</summary>
    /// <param name="placement">描画層が決めた文言と基準点。</param>
    /// <param name="appearance">外観（色・フォントサイズ・角丸）。</param>
    /// <param name="isVisible">外観が指定する表示可否（UI が要素として置くかどうかの判断に使う）。</param>
    /// <param name="measureText">テキストの実寸を測る関数。</param>
    /// <returns>要素の配置指示。</returns>
    /// <remarks>
    /// 背景矩形は「文字の実寸 + 内側余白 × 2」であり、基準点を左上として置く。 描画領域をはみ出す場合は領域内へクランプする<br/>
    /// （クランプは <see cref="EsSize.ClampOffset"/> が単一所有する。 領域が未確定の軸はクランプしない）。<br/>
    /// 上下の余白はルーペ固有の値であり本クラスが単一所有する。
    /// </remarks>
    private static LoupeLabelPlacement ResolveElement(
        in LoupeTextPlacement placement, LoupeAppearance appearance, bool isVisible, Func<string, double, EsSize> measureText)
    {
        var fontSize = appearance.CoordLabelFontSize;
        var textSize = measureText(placement.Text, fontSize);
        var width = textSize.Width + PaddingX * 2;
        var height = textSize.Height + PaddingY * 2;

        // 基準点は文字の左上である（水平・垂直の揃えは持たない）
        var bounds = new EsRect(
            placement.ClampArea.ClampOffset(placement.Point.X, width, EsAxis.X),
            placement.ClampArea.ClampOffset(placement.Point.Y, height, EsAxis.Y),
            width,
            height);

        return new LoupeLabelPlacement(
            bounds, PaddingX, PaddingY, placement.Text, fontSize,
            appearance.CoordLabelColor, appearance.CoordLabelBackgroundColor, appearance.CoordLabelCornerRadius, isVisible);
    }
}
