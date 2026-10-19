using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>Marquee が UI 層へ要求する効果（フォーカス・再描画・カーソル形状・ポインターキャプチャ・座標変換）を適用する窓口。</summary>
/// <remarks>
/// コア（<see cref="MarqueeCore"/>）は状態の更新と通知だけを行い、画面への反映は本インターフェース経由で UI 層へ委ねる。<br/>
/// 各 UI 実装が自 UI の操作へ接続する唯一の境界であり、コアは UI フレームワークに依存しない。<br/>
/// 効果を適用する順序（フォーカス → カーソル形状 → 再描画）はコアが単一所有するため、実装は呼ばれた順に適用するだけでよい。<br/>
/// ポインターキャプチャのように UI によって不要な効果（暗黙にキャプチャする UI）は、何もしない実装で応答してよい。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> でファサードを検証できる。
/// </remarks>
public interface IMarqueeEffects
{
    /// <summary>コントロールへフォーカスを設定する（矢印キー nudge の受け付け開始）。</summary>
    void RequestFocus();

    /// <summary>再描画を要求する。</summary>
    void RequestRedraw();

    /// <summary>カーソル形状を設定する。</summary>
    /// <param name="kind">カーソル形状の種別（UI 層が自 UI のカーソル型へ写像する）。</param>
    void SetCursorShape(MarqueeCursorKind kind);

    /// <summary>ポインターキャプチャを取得または解放する。</summary>
    /// <param name="capture">true = 取得 / false = 解放。</param>
    /// <remarks>ドラッグ中にポインターがコントロール外へ出ても移動イベントを受け取りたい UI が使用する。</remarks>
    void SetPointerCapture(bool capture);

    /// <summary>対象座標を画面物理座標へ変換する（カーソル移動用）。</summary>
    /// <param name="sourcePoint">対象座標。</param>
    /// <returns>画面物理座標。 変換できない場合は既定値。</returns>
    EsPointInt ToScreenPoint(EsPoint sourcePoint);
}
