using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Thumbnail;

/// <summary>画像の周囲へ落とす影の計画（描く範囲・濃さ・帯）。</summary>
/// <remarks>
/// <b>影の算法の差し替え点である</b>: 既定の実装は <see cref="ThumbnailShadowPlan"/>（実測値）。
/// 別の算法（ぼかしを使う・影をずらす・描かない等）は本インターフェースを実装して
/// <see cref="ThumbnailAppearance.Shadow"/> へ渡す。<br/>
/// <br/>
/// <b>UI が使うもの</b>: 帯を塗る UI（MewUI / WinUI3 / WinForms）は <see cref="BuildDeviceBands"/> の結果を<br/>
/// 単色の矩形として塗る。 要素で描く UI（AvaloniaUI）は <see cref="BlurRadius"/> と <see cref="ColorArgb"/> を使えるが、<br/>
/// それは既定の実装（<see cref="ThumbnailShadowPlan"/>）に限る（他の実装は帯を描く）。<br/>
/// <br/>
/// <b>座標系</b>: <see cref="BuildDeviceBands"/> の入出力は DIP（入力）と物理ピクセル（出力）である。<br/>
/// <b>余白</b>: <see cref="Insets"/> は画像の外側へ確保する余白であり、レイアウトの項目の大きさに反映される<br/>
/// （<see cref="ThumbnailOptions.SurroundExtent"/> より広い場合はレイアウト側が広がる）。
/// </remarks>
public interface IThumbnailShadowPlan
{
    /// <summary>影のぼかしの広がり（DIP）を取得する。</summary>
    /// <value>画像の端からこの距離まで影を落とす。</value>
    double BlurRadius { get; }

    /// <summary>影の色（<c>0xAARRGGBB</c>）を取得する。</summary>
    /// <value>透過度は各停止点が上書きする。</value>
    uint ColorArgb { get; }

    /// <summary>画像の周囲へ確保する影の余白を取得する。</summary>
    /// <value>4 辺の余白（DIP）。</value>
    ThumbnailShadowInsets Insets { get; }

    /// <summary>4 辺の計画（描画順は上辺・右辺・下辺・左辺）を取得する。</summary>
    /// <value>要素として辺を描く UI が使う。 帯を塗る UI は <see cref="BuildDeviceBands"/> を使う。</value>
    IReadOnlyList<ThumbnailShadowEdge> Edges { get; }

    /// <summary>4 隅の放射グラデーションの停止点を取得する。</summary>
    /// <value>隅を 1 つの要素で塗る UI が使う。 帯を塗る UI は <see cref="BuildDeviceBands"/> を使う。</value>
    IReadOnlyList<ThumbnailShadowStop> CornerStops { get; }

    /// <summary>画像の矩形から、影を塗る 1 画素幅の帯（4 辺と 4 隅）を作る。</summary>
    /// <param name="x">画像の左端（DIP）。</param>
    /// <param name="y">画像の上端（DIP）。</param>
    /// <param name="width">画像の幅（DIP）。</param>
    /// <param name="height">画像の高さ（DIP）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率（DPI の倍率）。</param>
    /// <returns>帯の一覧（物理ピクセルの矩形と色）。</returns>
    /// <remarks>
    /// <b>これは「描画計画」の画素版である</b>: 濃さを 1 画素ごとの単色へ落とすため、どの UI 版も<br/>
    /// 「単色の矩形を塗る」だけで同じ結果になる（グラデーション ブラシの端の扱いに依存しない）。<br/>
    /// 画像が空・倍率が 0 以下の場合は空の一覧を返す。
    /// </remarks>
    IReadOnlyList<ThumbnailShadowBand> BuildDeviceBands(double x, double y, double width, double height, double scaling);

    /// <summary>影を含む矩形（画像 + 余白）を求める。</summary>
    /// <param name="image">画像の矩形（DIP）。</param>
    /// <returns>影を含む矩形（DIP）。</returns>
    /// <remarks>描画計画の <see cref="ThumbnailDrawPlan.ShadowArea"/> を組み立てるために使う。</remarks>
    EsRect Expand(EsRect image) => new(
        image.X - Insets.Left,
        image.Y - Insets.Top,
        image.Width + Insets.Left + Insets.Right,
        image.Height + Insets.Top + Insets.Bottom);
}
