namespace EsUtil.Feature.Marquee;

/// <summary>コア（状態層）から UI 層へ伝える通知の種別。</summary>
/// <remarks>
/// UI 層は本種別を自 UI の公開イベントへ転送するだけである（判断はコアが済ませている）。<br/>
/// 種別と公開イベントの対応は各 UI 実装の転送処理が持つ。<br/>
/// <br/>
/// <b>生のポインター入力を通知しない</b>: 押下・解放・クリック・移動は入力そのものであり、<br/>
/// 通知すると「UI 層が入力したものをコアがそのまま返す」往復になる（単体の選択矩形コントロールの概念ではない）。<br/>
/// ホストが入力そのものを必要とする場合は自 UI の入力イベントを使う（押下状態は <see cref="MarqueeSnapshot.IsMouseDown"/> でも取得できる）。
/// </remarks>
public enum MarqueeNoticeKind
{
    /// <summary>新規選択のドラッグ開始（始点の決定）。</summary>
    SelectionStarted,

    /// <summary>選択矩形の変更。</summary>
    SelectionChanged,

    /// <summary>選択矩形の確定。</summary>
    SelectionCompleted,

    /// <summary>領域のキャンセル（小さすぎる矩形・枠外ダブルクリック等）。</summary>
    SelectionCancelled,

    /// <summary>追従位置（十字マーカーが指す位置）の変更。</summary>
    MarkerPositionMoved,

    /// <summary>確定要求（Enter・ダブルクリック・API 呼び出し）。</summary>
    ConfirmRequested,

    /// <summary>キャンセル要求（Escape・枠外ダブルクリック・API 呼び出し）。</summary>
    CancelRequested,
}
