using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Thumbnail;

/// <summary>サムネイル 1 件分の描画計画（矩形・層・状態・影・名前）。</summary>
/// <param name="Index">項目の添字。</param>
/// <param name="Slot">項目の外枠（ホバー帯）の矩形。</param>
/// <param name="Image">画像の矩形。</param>
/// <param name="ShadowArea">影を含む矩形（画像 + 影の余白）。</param>
/// <param name="Label">名前の計画。</param>
/// <param name="Layers">描く層の順序（状態に応じて間引く）。</param>
/// <param name="Highlight">強調の種類（<see cref="ThumbnailHighlightKind.None"/> は帯を描かない）。</param>
/// <param name="Shadow">影の計画。 null の場合は影を描かない。</param>
/// <param name="IsHovered">マウスが重なっているかどうか。</param>
/// <param name="IsSelected">選択中かどうか。</param>
/// <param name="IsCurrent">現在表示中の画像かどうか。</param>
/// <remarks>
/// <b>UI は本計画のとおりに描く</b>: どの矩形へ何を描くか（背景・影・画像・名前）とその順序は<br/>
/// 本計画が単一所有する。 UI が持つのは描画 API の呼び出しと入力の受付だけである。<br/>
/// <br/>
/// <b>座標系</b>: 一覧の内容の左上を原点とする DIP 座標（スクロール位置は UI が加える）。<br/>
/// <br/>
/// <b>差し替え点</b>: 本型は公開の契約であり、ホストは <see cref="ThumbnailPresenter.ResolveDrawPlan"/> を<br/>
/// 上書きして自前の計画を返せる（背景・層の順序・名前の体裁・影の有無を変える）。 影の算法は<br/>
/// <see cref="IThumbnailShadowPlan"/> の実装を <see cref="Shadow"/> へ渡して差し替える。 帯を塗る手順は<br/>
/// UI が持つ（描画 API は UI フレームワーク固有のため、計画は値を渡すに留める）。<br/>
/// <br/>
/// <b>層の順序</b>: <see cref="Layers"/> は「描く層」を <c>Background</c> → <c>Shadow</c> → <c>Image</c> → <c>Label</c><br/>
/// の順で並べる（状態に応じて間引く。 順序は入れ替えない）。 名前（<c>Label</c>）を要素として置く UI でも<br/>
/// 同じ順序になる（要素は描画面の上に重ねる）。 影を枠のブラシで描く UI は影と画像が同じ要素に載るが、<br/>
/// どちらも既定の順序（影 → 画像）で描かれる。
/// </remarks>
public readonly record struct ThumbnailDrawPlan(
    int Index,
    EsRect Slot,
    EsRect Image,
    EsRect ShadowArea,
    ThumbnailLabelPlan Label,
    IReadOnlyList<ThumbnailDrawLayer> Layers,
    ThumbnailHighlightKind Highlight,
    IThumbnailShadowPlan? Shadow,
    bool IsHovered,
    bool IsSelected,
    bool IsCurrent)
{
    /// <summary>強調（ホバーまたは選択）されているかどうかを取得する。</summary>
    /// <value>ホバー中または選択中の場合は true。</value>
    public bool IsHighlighted => IsHovered || IsSelected;
}
