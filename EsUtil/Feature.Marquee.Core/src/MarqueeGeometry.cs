using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>選択矩形コントロールの描画 1 フレーム分の幾何（コアが責任を持つ値の全量）。</summary>
/// <param name="ClientSize">描画領域のサイズ（DIP。 原点は常に 0）。</param>
/// <param name="FocusDipRect">注目矩形（DIP。 終点の加算は解決済み）。 選択もハイライトも無い場合は null。</param>
/// <param name="FocusSourceRect">注目矩形（対象座標）。 表示スケールの解決（ホスト結線）に使う。</param>
/// <param name="Handles">ドラッグハンドル（DIP の矩形列と枠線の太さ）。 選択が無い場合は null。</param>
/// <param name="Marker">始点マーカー（DIP。 nudge 中のみ）。 nudge 中でない場合は null。</param>
/// <param name="MouseSourcePosition">直近のマウス位置（対象座標。 カーソル座標ラベルの文言の基準）。</param>
/// <param name="MouseDipPosition">直近のマウス位置（DIP。 カーソル座標ラベルの配置の基準）。</param>
/// <remarks>
/// <b>本値がコアの公開する描画の入口である</b>: コアは選択矩形・注目矩形・ハンドル・マーカーの幾何だけを返し、<br/>
/// 装飾（暗転マスク・枠線・内側の塗り）・文言・表示可否・描画順は描画の都合として描画層（本コアを参照する上位の層）が解決する。<br/>
/// <b>注目矩形はコアに残す</b>: 座標変換と終点の加算の解決は選択の対象と同じ規則を共有するため、<br/>
/// 分離すると矩形がずれる（暗転領域・枠線・内側の塗り・サイズラベルが同じ矩形を使う）。<br/>
/// <b>マウス位置は対象座標と DIP の両方を渡す</b>: カーソル座標ラベルは文言が対象座標・配置が DIP を必要とするため、<br/>
/// 描画層が座標変換ポートを持つ必要をなくす（変換はコアが 1 回だけ行う）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public readonly record struct MarqueeGeometry(
    EsSize ClientSize,
    EsRect? FocusDipRect,
    EsRect? FocusSourceRect,
    MarqueeHandles? Handles,
    MarqueeMarker? Marker,
    EsPoint MouseSourcePosition,
    EsPoint MouseDipPosition)
{
    /// <summary>描画面の全体（左上を原点（0, 0）とする矩形）を取得する。</summary>
    /// <value>幅・高さが <see cref="ClientSize"/> の矩形（DIP）。</value>
    /// <remarks>描画 API のコンテキスト原点は常にコントロールの左上であるため、原点を持たない型で受ける（取り違えを防ぐ）。</remarks>
    public EsRect ClientRect => new(0, 0, ClientSize.Width, ClientSize.Height);
}
