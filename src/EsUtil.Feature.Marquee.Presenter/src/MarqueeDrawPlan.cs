using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>選択矩形コントロールの描画 1 フレーム分の計画（描画層が解決した値を UI 層へ渡す唯一の契約）。</summary>
/// <param name="Appearance">外観（色・ラベルの体裁・枠線の太さ・表示可否）。</param>
/// <param name="ClientRect">描画面の全体（左上を原点とする矩形・DIP）。</param>
/// <param name="FocusDipRect">注目矩形（DIP。 閉区間は解決済み）。 選択もハイライトも無い場合は null。</param>
/// <param name="LabelAnchor">ラベルの基準点（注目矩形の内側へ寄せた左上・DIP）。 注目矩形が無い場合は null。</param>
/// <param name="OverlayDipRects">暗転して塗る領域（DIP）。 暗転を描かない設定の場合は空。</param>
/// <param name="OuterBorderDipRects">外枠の矩形（DIP の 4 辺）。 枠線を描かない設定または注目矩形が無い場合は空。</param>
/// <param name="InnerFillDipRect">注目矩形の内側の塗り（DIP）。 塗らない設定または注目矩形が無い場合は null。</param>
/// <param name="InnerBorderDipRects">注目矩形の内側の枠線（DIP の 4 辺）。 描かない設定または注目矩形が無い場合は空。</param>
/// <param name="Handles">ドラッグハンドルの幾何（DIP の矩形列と枠線の太さ）。 選択が無い場合は null。</param>
/// <param name="Marker">始点マーカー（DIP）。 nudge 中でない場合は null。</param>
/// <param name="Layers">描く層とその順序（<b>描画順は描画層が単一所有する</b>）。 先頭から順に処理する。</param>
/// <param name="Labels">テキスト（サイズラベル・カーソル座標ラベル）の配置指示。 表示しない場合は空。</param>
/// <remarks>
/// <b>描く値はすべて本値に解決済み</b>である（描画実装は計算を持たず、<paramref name="Layers"/> の順に写すだけ）。<br/>
/// したがって描画実装が幾何計算（矩形の導出・ハイライトの優先順・閉区間の解決）を行うことはない。<br/>
/// <b>描くかどうかも本値が単一所有する</b>: 非表示の項目は空として返るため、描画実装は層ごとの可否を判定しない<br/>
/// （色のアルファを「描かない」の代用にしない）。<br/>
/// <b>文字は <paramref name="Labels"/> として値を渡す</b>: UI 層が要素として置くか画像として描くかを決める。<br/>
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
    /// <value>配置するラベルを持つ場合は true。</value>
    public bool HasText => Labels.Count > 0;

    /// <summary>装飾（外側・内側）を描く必要があるかどうかを取得する。</summary>
    /// <value>暗転領域・外枠・内側の塗り・内枠のいずれかを持つ場合は true。</value>
    public bool HasDecoration
        => OverlayDipRects.Count > 0 || OuterBorderDipRects.Count > 0
        || InnerFillDipRect is not null || InnerBorderDipRects.Count > 0;
}
