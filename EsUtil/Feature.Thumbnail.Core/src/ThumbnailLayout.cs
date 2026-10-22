
namespace EsUtil.Feature.Thumbnail;

/// <summary>サムネイル一覧の配置（折り返し・行の高さ・下端揃え）を解決する純粋計算。</summary>
/// <remarks>
/// <b>並べ方</b>: 一定の幅の項目を利用可能な幅で折り返し、各行の高さを「その行の最大アイテム高さ」に決め、<br/>
/// 項目を行内で下端揃えにする（名前の 1 文字目の高さ位置が行内で揃う）。 標準の折り返しパネルは<br/>
/// 行内で上端揃えのため、高さが異なる項目では名前の位置が揃わない（画像の縦横比によって高さが変わる）。<br/>
/// <br/>
/// <b>項目の外枠</b>: 画像 + 影の余地 + 名前の行を、<see cref="ThumbnailOptions.SlotInset"/> と<br/>
/// <see cref="ThumbnailOptions.LabelHeight"/> で包んだ矩形である。 ホバー帯はこの矩形へ描く。<br/>
/// <br/>
/// <b>この計算は UI フレームワークに依存しない</b>: 入力は長さだけで、出力は矩形と添字だけである。
/// </remarks>
public static class ThumbnailLayout
{
    /// <summary>一覧の配置を解決する。</summary>
    /// <param name="availableWidth">一覧の幅（DIP）。</param>
    /// <param name="imageWidth">画像の幅（DIP）。 <see cref="ThumbnailOptions.MinimumImageWidth"/> 未満は引き上げる。</param>
    /// <param name="imageHeights">項目ごとの画像の高さ（DIP）。 画像が無い項目は幅と同じ値を渡す（正方形）。</param>
    /// <param name="options">設定。 null の場合は <see cref="ThumbnailOptions.Default"/>。</param>
    /// <returns>配置の結果。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="imageHeights"/> に負の値が含まれる場合。</exception>
    /// <remarks>
    /// <b>利用可能な幅が未確定（0 以下）の場合</b>: 1 列で並べる（幅の確定後に呼び直す）。<br/>
    /// <b>項目が 1 件も無い場合</b>: 列数 1・内容の寸法に余白だけを持つ結果を返す。
    /// </remarks>
    public static ThumbnailLayoutResult Resolve(
        double availableWidth,
        double imageWidth,
        ReadOnlySpan<double> imageHeights,
        ThumbnailOptions? options = null)
    {
        var settings = options ?? ThumbnailOptions.Default;
        var width = Math.Max(settings.MinimumImageWidth, imageWidth);
        var inset = settings.SlotInset;
        var slotWidth = width + (inset * 2);
        var stride = slotWidth + settings.ItemSpacing;
        var padding = settings.ContentPadding;

        var count = imageHeights.Length;
        if (count == 0)
        {
            return new ThumbnailLayoutResult(
                width, slotWidth, 1, new EsSize(padding * 2, padding * 2), [], [], []);
        }

        // 折り返しの列数。 先頭の項目は必ず 1 列目へ置く（幅が項目より狭くても 1 列は並べる）
        var columns = 1;
        var usable = Math.Max(0, availableWidth - (padding * 2));
        if (usable > 0 && stride > 0)
        {
            columns = Math.Max(1, (int)Math.Floor((usable + settings.ItemSpacing) / stride));
        }

        if (settings.MaximumColumns > 0)
        {
            columns = Math.Min(columns, settings.MaximumColumns);
        }

        columns = Math.Min(columns, count);

        var rows = ((count - 1) / columns) + 1;
        var rowHeights = new double[rows];
        var imageHeightCopy = new double[count];
        for (var i = 0; i < count; i++)
        {
            var height = imageHeights[i];
            if (height < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(imageHeights), height, "画像の高さは 0 以上である必要があります。");
            }

            imageHeightCopy[i] = height;
            var row = i / columns;
            rowHeights[row] = Math.Max(rowHeights[row], height);
        }

        for (var row = 0; row < rows; row++)
        {
            rowHeights[row] += (inset * 2) + settings.LabelHeight;
        }

        var slots = new EsRect[count];
        var images = new EsRect[count];
        var labels = new EsRect[count];

        var rowTops = new double[rows];
        var y = padding;
        for (var row = 0; row < rows; row++)
        {
            rowTops[row] = y;
            y += rowHeights[row];
        }

        for (var i = 0; i < count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            var imageHeight = imageHeightCopy[i];
            var slotHeight = (inset * 2) + imageHeight + settings.LabelHeight;
            var slotX = padding + (column * stride);

            // 行内で下端を揃える（名前の 1 文字目の高さが行内で揃う）
            var slotY = rowTops[row] + (rowHeights[row] - slotHeight);
            slots[i] = new EsRect(slotX, slotY, slotWidth, slotHeight);

            var imageY = slotY + inset;
            images[i] = new EsRect(slotX + inset, imageY, width, imageHeight);
            labels[i] = new EsRect(slotX + inset, imageY + imageHeight, width, settings.LabelHeight);
        }

        var contentWidth = (padding * 2) + (columns * slotWidth) + ((columns - 1) * settings.ItemSpacing);
        var contentHeight = y + padding;

        return new ThumbnailLayoutResult(
            width, slotWidth, columns, new EsSize(contentWidth, contentHeight), slots, images, labels);
    }

    /// <summary>画像の表示高さを求める（幅を固定し、縦横比を保つ）。</summary>
    /// <param name="width">表示幅（DIP）。</param>
    /// <param name="pixelWidth">元画像の幅（ピクセル）。</param>
    /// <param name="pixelHeight">元画像の高さ（ピクセル）。</param>
    /// <returns>表示高さ（DIP）。 画像の寸法が不正な場合は幅と同じ値（正方形）。</returns>
    /// <remarks>
    /// 幅だけを与えて縦横比のまま縮小する（横長の画像は低く、縦長の画像は高い項目になる）。
    /// </remarks>
    public static double ImageHeight(double width, int pixelWidth, int pixelHeight)
    {
        if (width <= 0 || pixelWidth <= 0 || pixelHeight <= 0)
        {
            return Math.Max(0, width);
        }

        return width * pixelHeight / pixelWidth;
    }
}
