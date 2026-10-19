namespace EsUtil.Feature.Marquee.Core.Interaction;

/// <summary>コア（状態層）が UI 層へ要求するカーソル操作（フラグの組み合わせ）。</summary>
/// <remarks>
/// 1 回の入力通知で複数の操作を要求できる（例: nudge の終了では「nudge 位置へ移動」してから「再表示」する）。<br/>
/// 適用順は <see cref="MarqueeFeedback.ApplyCursor"/> が固定する（移動 → 非表示 → 再表示）。<br/>
/// カーソルの表示残留・原点飛びを防ぐためこの順序を守る必要がある。
/// </remarks>
[Flags]
internal enum MarqueeCursorActions
{
    /// <summary>操作なし。</summary>
    None = 0,

    /// <summary><see cref="MarqueeFeedback.CursorSourcePosition"/> へカーソルを移動する。</summary>
    MoveTo = 1,

    /// <summary>カーソルを非表示にする（nudge 中）。</summary>
    Hide = 2,

    /// <summary>カーソルを再表示する（nudge の終了・外部復元）。</summary>
    Show = 4,
}
