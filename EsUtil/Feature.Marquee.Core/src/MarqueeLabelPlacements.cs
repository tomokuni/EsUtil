namespace EsUtil.Feature.Marquee;

/// <summary>マーキーのラベル（サイズ・カーソル座標）の配置指示を保持する値オブジェクト。</summary>
/// <param name="SizeLabel">サイズラベルの配置指示。 無い場合は null。</param>
/// <param name="CoordLabel">カーソル座標ラベルの配置指示。 無い場合は null。</param>
/// <remarks>
/// ラベルはサイズとカーソル座標の 2 種類だけであるため、固定長の値として受け渡す。<br/>
/// <b>列挙順</b>: <see cref="SizeLabel"/> → <see cref="CoordLabel"/>（描画順でもある）。<br/>
/// 配置指示を持たないラベルは <c>null</c> であり、列挙には含めない。
/// </remarks>
public readonly record struct MarqueeLabelPlacements(
    MarqueeLabelPlacement? SizeLabel,
    MarqueeLabelPlacement? CoordLabel)
{
    /// <summary>保持しているラベルの数（0〜2）。</summary>
    /// <value>配置指示を持つラベルの数。</value>
    public int Count => (SizeLabel is null ? 0 : 1) + (CoordLabel is null ? 0 : 1);

    /// <summary>列挙順（サイズラベル → カーソル座標ラベル）で配置指示を取得する。</summary>
    /// <param name="index">0〜<see cref="Count"/> - 1 の位置。</param>
    /// <returns>指定位置の配置指示。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> が範囲外の場合。</exception>
    /// <remarks>保持していないラベルは飛ばす（<c>null</c> を返さない）。</remarks>
    public MarqueeLabelPlacement this[int index] => index switch
    {
        0 when SizeLabel is { } size => size,
        0 when CoordLabel is { } coord => coord,
        1 when SizeLabel is not null && CoordLabel is { } coord => coord,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "ラベルの位置が範囲外です。"),
    };
}
