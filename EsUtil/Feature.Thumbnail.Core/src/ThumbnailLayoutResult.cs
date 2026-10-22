
namespace EsUtil.Feature.Thumbnail;

/// <summary>サムネイル一覧の配置の結果（項目の矩形・内容の寸法・可視範囲）。</summary>
/// <remarks>
/// <b>座標系</b>: 一覧の内容の左上を原点とする DIP 座標である（スクロール位置は含まない）。<br/>
/// 各項目の矩形は<b>外枠（ホバー帯）</b>であり、画像と名前の矩形はその内側にある。<br/>
/// <br/>
/// <b>行の高さ</b>: 行ごとに「その行の最大の画像の高さ」で決め、項目は行内で下端を揃える<br/>
/// （名前の 1 文字目の高さ位置が行内で揃う）。<br/>
/// <br/>
/// <b>不変</b>: 生成後は変化しない（寸法が変わったら <see cref="ThumbnailLayout.Resolve"/> で作り直す）。
/// </remarks>
public sealed class ThumbnailLayoutResult
{
    /// <summary>項目が無い配置。</summary>
    /// <value>列数 1・項目 0 件の配置。</value>
    public static ThumbnailLayoutResult Empty { get; } = new(0, 0, 0, new EsSize(0, 0), [], [], []);

    /// <summary>項目の矩形（外枠。 並び順）。</summary>
    private readonly EsRect[] _slots;

    /// <summary>画像の矩形（並び順）。</summary>
    private readonly EsRect[] _images;

    /// <summary>名前の矩形（並び順）。</summary>
    private readonly EsRect[] _labels;

    /// <summary>配置の結果を生成する。</summary>
    /// <param name="imageWidth">画像の幅（DIP）。</param>
    /// <param name="slotWidth">項目の外枠の幅（DIP）。</param>
    /// <param name="columns">列数。</param>
    /// <param name="contentSize">内容の寸法（DIP）。</param>
    /// <param name="slots">項目の外枠の矩形。</param>
    /// <param name="images">画像の矩形。</param>
    /// <param name="labels">名前の矩形。</param>
    internal ThumbnailLayoutResult(
        double imageWidth,
        double slotWidth,
        int columns,
        EsSize contentSize,
        EsRect[] slots,
        EsRect[] images,
        EsRect[] labels)
    {
        ImageWidth = imageWidth;
        SlotWidth = slotWidth;
        Columns = Math.Max(1, columns);
        ContentSize = contentSize;
        _slots = slots;
        _images = images;
        _labels = labels;
    }

    /// <summary>画像の幅（DIP）を取得する。</summary>
    public double ImageWidth { get; }

    /// <summary>項目の外枠の幅（DIP）を取得する。</summary>
    public double SlotWidth { get; }

    /// <summary>項目数を取得する。</summary>
    public int Count => _slots.Length;

    /// <summary>列数を取得する。</summary>
    /// <value>1 以上。</value>
    public int Columns { get; }

    /// <summary>行数を取得する。</summary>
    /// <value>項目が無い場合は 0。</value>
    public int Rows => Count == 0 ? 0 : ((Count - 1) / Columns) + 1;

    /// <summary>内容の寸法（DIP）を取得する。</summary>
    /// <value>余白を含む一覧全体の寸法（スクロール領域の大きさ）。</value>
    public EsSize ContentSize { get; }

    /// <summary>項目の外枠の矩形（並び順）を取得する。</summary>
    public IReadOnlyList<EsRect> Slots => _slots;

    /// <summary>画像の矩形（並び順）を取得する。</summary>
    public IReadOnlyList<EsRect> Images => _images;

    /// <summary>名前の矩形（並び順）を取得する。</summary>
    public IReadOnlyList<EsRect> Labels => _labels;

    /// <summary>指定した項目の外枠の矩形を取得する。</summary>
    /// <param name="index">項目の添字。</param>
    /// <returns>外枠の矩形。 範囲外の場合は空の矩形。</returns>
    public EsRect Slot(int index) => index >= 0 && index < _slots.Length ? _slots[index] : EsRect.Empty;

    /// <summary>指定した項目の画像の矩形を取得する。</summary>
    /// <param name="index">項目の添字。</param>
    /// <returns>画像の矩形。 範囲外の場合は空の矩形。</returns>
    public EsRect Image(int index) => index >= 0 && index < _images.Length ? _images[index] : EsRect.Empty;

    /// <summary>指定した項目の名前の矩形を取得する。</summary>
    /// <param name="index">項目の添字。</param>
    /// <returns>名前の矩形。 範囲外の場合は空の矩形。</returns>
    public EsRect Label(int index) => index >= 0 && index < _labels.Length ? _labels[index] : EsRect.Empty;

    /// <summary>指定した位置にある項目の添字を求める。</summary>
    /// <param name="position">一覧の内容の座標（スクロール位置を加えた座標）。</param>
    /// <returns>項目の添字。 どの項目にも当たらない場合は -1。</returns>
    /// <remarks>
    /// 外枠（ホバー帯）で判定する。 項目の外枠は隙間なく並ぶため、隣り合う項目の間で<br/>
    /// 「どこにも当たらない」帯は生じない（<see cref="ThumbnailOptions.ItemSpacing"/> を空けた場合を除く）。
    /// </remarks>
    public int IndexAt(EsPoint position)
    {
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].Contains(position))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>見えている項目の範囲を求める。</summary>
    /// <param name="offsetY">スクロール位置（DIP）。</param>
    /// <param name="viewportHeight">見えている高さ（DIP）。</param>
    /// <returns>見えている項目の範囲。 項目が無い場合は <see cref="ThumbnailRange.Empty"/>。</returns>
    /// <remarks>
    /// ビューポートと交差する項目を返す（一部だけ見えている項目を含む）。<br/>
    /// 高さが 0 以下の場合はスクロール位置にある行だけを返す。
    /// </remarks>
    public ThumbnailRange VisibleRange(double offsetY, double viewportHeight)
    {
        if (_slots.Length == 0)
        {
            return ThumbnailRange.Empty;
        }

        var top = offsetY;
        var bottom = offsetY + Math.Max(0, viewportHeight);
        var first = -1;
        var last = -1;
        for (var i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot.Bottom <= top || slot.Y >= bottom)
            {
                continue;
            }

            if (first < 0)
            {
                first = i;
            }

            last = i;
        }

        if (first < 0)
        {
            // 高さ 0 のビューポートでも「その位置の項目」を返す（生成の優先度に使う）
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Y <= top && top < _slots[i].Bottom)
                {
                    return new ThumbnailRange(i, i);
                }
            }

            return ThumbnailRange.Empty;
        }

        return new ThumbnailRange(first, last);
    }
}
