using EsUtil.Feature.Marquee.Presenter.Drawing;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;


namespace EsUtil.Feature.Marquee;

/// <summary>描画層のテキスト配置から、ラベル要素の配置指示を解決する（実寸 → 配置矩形の写像）。</summary>
/// <remarks>
/// <b>位置付け</b>: 「文字を UI 要素として配置する」UI のための変換である。 描画層（<see cref="MarqueePresenter"/>）は<br/>
/// 文言・基準点・領域を決めるが、実際の配置矩形はテキストの実寸に依存するため、<br/>
/// 実寸測定（描画 API 依存）を UI 層から受け取って要素の矩形へ変換する。<br/>
/// 体裁（内側余白・フォントサイズ・色）は外観（<see cref="MarqueeAppearance"/>）が単一所有する。<br/>
/// <br/>
/// <b>順序は本クラスが所有する</b>: サイズラベル → カーソル座標ラベルの順に並べる<br/>
/// （文字を要素として置く場合、要素は親より後に描かれるため常に最上位になる）。<br/>
/// UI フレームワークに依存しない（測定関数をデリゲートで受け取る）ため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public partial class MarqueePresenter
{
    /// <summary>ラベル要素の配置指示を解決する（描画順に並ぶ）。</summary>
    /// <param name="sizeLabel">サイズラベルのテキスト配置。 表示しない場合は null。</param>
    /// <param name="coordLabel">カーソル座標ラベルのテキスト配置。 表示しない場合は null。</param>
    /// <param name="appearance">外観（色・フォントサイズ・内側余白。 本層が単一所有する）。 null 不可。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。 null 不可。</param>
    /// <returns>配置するラベル要素の一覧（サイズラベル → カーソル座標ラベルの順。 どちらも無い場合は空）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> または <paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// 文言・位置は描画層が、色・フォントサイズ・内側余白は外観が単一所有する（本クラスは実寸の反映のみ）。<br/>
    /// <b>表示可否でも配置指示を消さない</b>: 表示可否は各指示の <c>IsVisible</c> が持ち、<br/>
    /// UI は要素として置くかどうかを本値で判断する（止めた項目の文言・位置も値として残る）。
    /// </remarks>
    internal static IReadOnlyList<MarqueeLabelPlacement> ResolveLabelPlacements(
        MarqueeTextPlacement? sizeLabel,
        MarqueeTextPlacement? coordLabel,
        MarqueeAppearance appearance,
        Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(measureText);

        if (sizeLabel is null && coordLabel is null)
        {
            // 描画のたびに呼ばれるため、要素が無い場合は一覧を生成しない
            return [];
        }

        var elements = new List<MarqueeLabelPlacement>(2);

        if (sizeLabel is { } size)
        {
            elements.Add(ResolveElement(
                size, appearance.SizeLabelFontSize, appearance.SizeLabelColor, appearance.SizeLabelBackgroundColor,
                appearance, isVisible: appearance.ShowSizeLabel, measureText));
        }

        if (coordLabel is { } coord)
        {
            elements.Add(ResolveElement(
                coord, appearance.CoordLabelFontSize, appearance.CoordLabelColor, appearance.CoordLabelBackgroundColor,
                appearance, isVisible: appearance.ShowCoordLabel, measureText));
        }

        return elements;
    }

    /// <summary>テキスト配置と体裁から、要素の配置指示を組み立てる。</summary>
    /// <param name="placement">描画層が決めた文言と基準点（クランプ領域を含む）。</param>
    /// <param name="fontSize">フォントサイズ（DIP。 外観が所有する）。</param>
    /// <param name="textColor">文字色（外観が所有する）。</param>
    /// <param name="backgroundColor">背景色（外観が所有する）。</param>
    /// <param name="appearance">外観（内側余白の所有者）。</param>
    /// <param name="isVisible">外観が指定する表示可否（UI が要素として置くかどうかの判断に使う）。</param>
    /// <param name="measureText">テキストの実寸を測る関数。</param>
    /// <returns>要素の配置指示。</returns>
    /// <remarks>
    /// 背景矩形は「文字の実寸 + 内側余白 × 2」であり、基準点を左上として置く。 描画領域をはみ出す場合は領域内へクランプする<br/>
    /// （クランプは <see cref="EsSize.ClampOffset"/> が単一所有する。 領域が未確定の軸はクランプしない）。<br/>
    /// 内側余白はサイズラベルとホストの付加情報ラベルで共通の値を共有する（積み上げの間隔を揃えるため）。
    /// </remarks>
    private static MarqueeLabelPlacement ResolveElement(
        in MarqueeTextPlacement placement,
        double fontSize,
        DrawingColor textColor,
        DrawingColor backgroundColor,
        MarqueeAppearance appearance,
        bool isVisible,
        Func<string, double, EsSize> measureText)
    {
        var padding = appearance.LabelPadding;
        var textSize = measureText(placement.Text, fontSize);
        var width = textSize.Width + padding * 2;
        var height = textSize.Height + padding * 2;

        // 基準点は文字の左上である（水平・垂直の揃えは持たない）
        var dipRect = new EsRect(
            placement.ClampArea.ClampOffset(placement.Point.X, width, EsAxis.X),
            placement.ClampArea.ClampOffset(placement.Point.Y, height, EsAxis.Y),
            width,
            height);

        return new MarqueeLabelPlacement(
            dipRect, padding, padding, placement.Text, fontSize, textColor, backgroundColor, isVisible);
    }
}
