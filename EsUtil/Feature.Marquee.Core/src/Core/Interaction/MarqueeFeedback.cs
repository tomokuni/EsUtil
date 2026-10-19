using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;

namespace EsUtil.Feature.Marquee.Core.Interaction;

/// <summary>1 回の入力通知に対して UI 層が適用する表示上の効果（状態を持たない指示）。</summary>
/// <remarks>
/// コアは状態の更新と通知（<see cref="MarqueeNotice"/>）だけを行い、画面への反映方法（再描画・フォーカス・カーソル）は
/// 本レコードで UI 層へ指示する。 UI 層は指示に従って適用するだけで、判断を持たない。<br/>
/// カーソル操作の適用は UI 非依存（<see cref="ICursorService"/> と画面座標の変換関数のみで完結する）ため、
/// 本レコードの <see cref="ApplyCursor"/> が単一所有する（両 UI で同一の手順・順序になる）。<br/>
/// 濃い既定値（<see cref="IsHandled"/> 以外は false）を持つため、効果が無い通知では <c>default</c> を返せる。<br/>
/// <br/>
/// <b>可視性</b>: 本値の適用はファサード（<see cref="MarqueeCore"/>）が単一所有し、UI 層は値を受け取らないため internal とする。
/// </remarks>
internal readonly record struct MarqueeFeedback
{
    /// <summary>入力が処理されたかどうか（UI 層は自 UI の「処理済み」フラグへ設定する）。</summary>
    internal bool IsHandled { get; init; }

    /// <summary>再描画が必要かどうか。</summary>
    internal bool ShouldRedraw { get; init; }

    /// <summary>フォーカスを取得するかどうか。</summary>
    internal bool ShouldRequestFocus { get; init; }

    /// <summary>ポインターキャプチャの操作（なし・取得・解放）。</summary>
    /// <remarks>
    /// キャプチャを明示的に行う必要がある UI（ポインターがコントロール外へ出てもドラッグを継続したい実装）が使用する。<br/>
    /// ポインターを暗黙にキャプチャする UI では無視してよい（使用しない場合は常に <see cref="MarqueeCaptureAction.None"/> として扱える）。
    /// </remarks>
    internal MarqueeCaptureAction Capture { get; init; }

    /// <summary>カーソル形状を更新するかどうか。</summary>
    internal bool ShouldUpdateCursorShape { get; init; }

    /// <summary><see cref="ShouldUpdateCursorShape"/> が true のときのカーソル形状の種別（UI 層が自 UI のカーソル型へ写像する）。</summary>
    internal MarqueeCursorKind CursorKind { get; init; }

    /// <summary>要求するカーソル操作。</summary>
    internal MarqueeCursorActions Cursor { get; init; }

    /// <summary><see cref="MarqueeCursorActions.MoveTo"/> の移動先（対象座標）。</summary>
    internal EsPoint CursorSourcePosition { get; init; }

    /// <summary>要求されたカーソル操作を適用する。</summary>
    /// <param name="cursor">カーソルサービス。</param>
    /// <param name="effects">画面座標への変換を担う効果（UI 層が実装する）。</param>
    /// <returns>要求された操作をすべて適用できた場合は <see langword="true"/>。 移動に失敗した場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// 適用順は「移動 → 非表示 → 再表示」に固定する（nudge の終了では移動してから再表示する必要がある）。<br/>
    /// 画面座標の変換のみ UI 層に委ね、それ以外（どの操作をどの順で行うか）はコアが単一所有する。<br/>
    /// <b>移動の失敗を値で返す理由</b>: 移動は表示上の追従であり、失敗しても非表示・再表示は続行する必要がある
    /// （例外にすると再表示が行われず、ポインターが非表示のまま残る）。 記録は呼び出し側が行う。
    /// </remarks>
    internal bool ApplyCursor(ICursorService cursor, IMarqueeEffects effects)
    {
        var moved = true;
        if ((Cursor & MarqueeCursorActions.MoveTo) != 0)
        {
            moved = cursor.TrySetCursorScreenPoint(effects.ToScreenPoint(CursorSourcePosition));
        }

        if ((Cursor & MarqueeCursorActions.Hide) != 0)
        {
            cursor.ShowCursor(false);
        }

        if ((Cursor & MarqueeCursorActions.Show) != 0)
        {
            cursor.ShowCursor(true);
        }

        return moved;
    }

    /// <summary>要求された表示上の効果（フォーカス・カーソル形状・再描画）を適用する。</summary>
    /// <param name="effects">効果の適用先（UI 層が実装する）。</param>
    /// <remarks>
    /// 適用順は「フォーカス → カーソル形状 → 再描画」に固定する（再描画を最後にして確定後の状態を 1 回で描く）。<br/>
    /// どの効果をどの順で適用するかはコアが単一所有し、UI 層は自 UI の操作へ接続するだけにする。<br/>
    /// 効果の適用先をデリゲートで受け取らない（入力イベントのたびにデリゲートを生成しない）。
    /// </remarks>
    internal void ApplyVisual(IMarqueeEffects effects)
    {
        if (ShouldRequestFocus)
        {
            effects.RequestFocus();
        }

        if (ShouldUpdateCursorShape)
        {
            effects.SetCursorShape(CursorKind);
        }

        if (ShouldRedraw)
        {
            effects.RequestRedraw();
        }
    }
}
