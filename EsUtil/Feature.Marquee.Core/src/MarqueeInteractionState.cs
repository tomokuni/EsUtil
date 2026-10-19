namespace EsUtil.Feature.Marquee;

/// <summary>Marquee の対話状態（State パターン: 上位への通知状態）。</summary>
/// <remarks>
/// 内部状態（選択矩形・ドラッグ・nudge）から合成した公開ビュー。<br/>
/// 上位（ホスト）はこの状態で「描画モード（枠線 / 十字線 / ドラッグ中）」を判断できる。<br/>
/// - <see cref="NoSelection"/>: 領域なし（初期状態）<br/>
/// - <see cref="StartPointNudging"/>: 始点 Nudge 中（未選択 + nudge 中）<br/>
/// - <see cref="Dragging"/>: ドラッグ中（新規選択・ハンドル変形）<br/>
/// - <see cref="DraggingNudging"/>: ドラッグ中の Nudge 中<br/>
/// - <see cref="Selected"/>: 領域あり（選択確定後）<br/>
/// - <see cref="SelectedNudging"/>: 領域の Nudge 中（選択確定後のキーボード微調整）
/// </remarks>
public enum MarqueeInteractionState
{
    /// <summary>領域なし（初期状態）。</summary>
    NoSelection,

    /// <summary>始点 Nudge 中（キーボードで始点が設定済み）。</summary>
    StartPointNudging,

    /// <summary>ドラッグ中（新規選択・ハンドル変形）。</summary>
    Dragging,

    /// <summary>ドラッグ中の Nudge 中。</summary>
    DraggingNudging,

    /// <summary>領域あり（選択確定後）。</summary>
    Selected,

    /// <summary>領域の Nudge 中（選択確定後のキーボード微調整）。</summary>
    SelectedNudging,
}
