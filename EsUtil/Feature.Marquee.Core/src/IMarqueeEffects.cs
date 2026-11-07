using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>Marquee が UI 層へ要求する効果（フォーカス・再描画・カーソル形状・カーソルの移動と表示・ポインターキャプチャ・座標変換）を適用する窓口。</summary>
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

    /// <summary>実カーソルを画面物理座標へ移動する（矢印キーの nudge とハンドル押下で使う）。</summary>
    /// <param name="screenPoint">移動先（画面物理座標）。</param>
    /// <returns>
    /// 移動できた場合は <see langword="true"/>、移動を試みて失敗した場合は <see langword="false"/>、
    /// <b>カーソルを操作しない実装</b>は <see langword="null"/>（未対応。 失敗ではない）。
    /// </returns>
    /// <remarks>
    /// <b>失敗しても選択操作は続行する</b>: 移動は表示上の追従であり、失敗は値で返して呼び出し側が記録する。<br/>
    /// <b>「操作しない」を <see langword="false"/> にしない理由</b>: 未対応は正常な状態であり、失敗として記録すると
    /// 既定実装のまま矢印キーを押すたびに警告が出る。<br/>
    /// 既定実装（<see cref="Core.Contract.MarqueeEffectsBase"/>）は移動しない（null）。
    /// </remarks>
    bool? TryMoveCursor(EsPointInt screenPoint);

    /// <summary>実カーソルの表示/非表示を切り替える（nudge 中は隠す）。</summary>
    /// <param name="show">表示する場合は <see langword="true"/>、隠す場合は <see langword="false"/>。</param>
    /// <remarks>
    /// <b>非表示と表示は対で呼ばれる</b>（OS の表示カウンタを増減する実装では、片方だけだとカウンタがずれる）。<br/>
    /// 既定実装（<see cref="Core.Contract.MarqueeEffectsBase"/>）は何もしない。
    /// </remarks>
    void ShowCursor(bool show);
}
