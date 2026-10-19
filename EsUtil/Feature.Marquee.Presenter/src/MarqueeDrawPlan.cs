using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>選択矩形コントロールの描画 1 フレーム分の計画（描画層が解決した値を UI 層へ渡す唯一の契約）。</summary>
/// <param name="Appearance">外観（色・ラベルの体裁・枠線の太さ・表示可否）。</param>
/// <param name="ClientRect">描画面の全体（左上を原点とする矩形・DIP）。</param>
/// <param name="FocusDipRect">注目矩形（DIP。 閉区間は解決済み）。 選択もハイライトも無い場合は null。</param>
/// <param name="LabelAnchor">ラベルの基準点（注目矩形の内側へ寄せた左上・DIP）。 注目矩形が無い場合は null。</param>
/// <param name="OverlayDipRects">暗転して塗る領域（DIP）。 注目矩形が無い場合は描画面の全体（1 領域）。</param>
/// <param name="OuterBorderDipRects">外枠の矩形（DIP の 4 辺）。 注目矩形が無い場合は空。</param>
/// <param name="InnerFillDipRect">注目矩形の内側の塗り（DIP）。 注目矩形が無い場合は null。</param>
/// <param name="InnerBorderDipRects">注目矩形の内側の枠線（DIP の 4 辺）。 注目矩形が無い場合は空。</param>
/// <param name="Handles">ドラッグハンドルの幾何（DIP の矩形列と枠線の太さ）。 選択が無い場合は null。</param>
/// <param name="Marker">始点マーカー（DIP）。 nudge 中でない場合は null。</param>
/// <param name="Layers">描く値のある層とその順序（<b>描画順は描画層が単一所有する</b>）。 先頭から順に処理する。</param>
/// <param name="Labels">テキスト（サイズラベル・カーソル座標ラベル）の配置指示。 位置を持たない場合は空。</param>
/// <remarks>
/// <b>描く値はすべて本値に解決済み</b>である（描画実装は計算を持たず、<paramref name="Layers"/> の順に写すだけ）。<br/>
/// したがって描画実装が幾何計算（矩形の導出・ハイライトの優先順・閉区間の解決）を行うことはない。<br/>
/// <b>表示可否（<see cref="MarqueeAppearance"/> の <c>Show*</c>）は値も層も消さない</b>:<br/>
/// 値の有無は幾何だけで決まるため、表示を止めても計算値はそのまま残る（描画側がフラグを見て描くかどうかを決める）。<br/>
/// したがって「描く必要があるか」は <see cref="HasText"/> / <see cref="HasDecoration"/> のように<br/>
/// 表示可否を適用した述語で判定する（描画実装は層ごとに外観のフラグを見る）。<br/>
/// <b>文字は <paramref name="Labels"/> として値を渡す</b>: UI 層が要素として置くか画像として描くかを決める<br/>
/// （表示可否は各配置指示が持つ）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public readonly record struct MarqueeDrawPlan(
    MarqueeAppearance Appearance,
    EsRect ClientRect,
    EsRect? FocusDipRect,
    EsPoint? LabelAnchor,
    IReadOnlyList<EsRect> OverlayDipRects,
    IReadOnlyList<EsRect> OuterBorderDipRects,
    EsRect? InnerFillDipRect,
    IReadOnlyList<EsRect> InnerBorderDipRects,
    MarqueeHandles? Handles,
    MarqueeMarker? Marker,
    IReadOnlyList<MarqueeDrawLayer> Layers,
    IReadOnlyList<MarqueeLabelPlacement> Labels)
{
    /// <summary>テキスト（サイズラベル・カーソル座標ラベル）を描く必要があるかどうかを取得する。</summary>
    /// <value>表示が有効なラベルを持つ場合は true。</value>
    /// <remarks>表示可否（<see cref="MarqueeAppearance"/> の <c>Show*</c>）まで適用した結果を返す（計画は可否で値を消さないため、本値で判定できる）。</remarks>
    public bool HasText => HasVisibleLabel(Labels);

    /// <summary>装飾（外側・内側）を描く必要があるかどうかを取得する。</summary>
    /// <value>表示が有効な装飾を 1 つ以上持つ場合は true。</value>
    /// <remarks>表示可否（<see cref="MarqueeAppearance"/> の <c>Show*</c>）まで適用した結果を返す（計画は可否で値を消さないため、本値で判定できる）。</remarks>
    public bool HasDecoration
        => (Appearance.ShowOverlay && Appearance.ShouldDrawOverlay(FocusDipRect is not null) && OverlayDipRects.Count > 0)
        || (Appearance.ShowOuterBorder && OuterBorderDipRects.Count > 0)
        || (Appearance.ShowInnerFill && InnerFillDipRect is not null)
        || (Appearance.ShowInnerBorder && InnerBorderDipRects.Count > 0);

    /// <summary>表示が有効なラベルを持つかを判定する。</summary>
    /// <param name="labels">配置指示の一覧。</param>
    /// <returns>表示が有効なラベルを持つ場合は true。</returns>
    /// <remarks>LINQ を避ける（インデックス可能なコレクションのため、規約どおりインデクサーを使う）。</remarks>
    private static bool HasVisibleLabel(IReadOnlyList<MarqueeLabelPlacement> labels)
    {
        for (var index = 0; index < labels.Count; index++)
        {
            if (labels[index].IsVisible)
            {
                return true;
            }
        }

        return false;
    }
}
