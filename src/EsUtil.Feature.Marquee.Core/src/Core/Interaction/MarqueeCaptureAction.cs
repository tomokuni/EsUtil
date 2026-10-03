namespace EsUtil.Feature.Marquee.Core.Interaction;

/// <summary>コア（入力の解釈）が UI 層へ要求するポインターキャプチャの操作。</summary>
/// <remarks>
/// 取得（<see cref="Capture"/>）と解放（<see cref="Release"/>）は同じ経路（<see cref="MarqueeFeedback"/>）で要求する<br/>
/// （片方だけを入力ハンドラへ直接書くと、取得の条件と解放の条件が別々の場所になる）。
/// </remarks>
internal enum MarqueeCaptureAction
{
    /// <summary>操作なし（キャプチャの状態を変えない）。</summary>
    None = 0,

    /// <summary>ポインターをキャプチャする（ドラッグ開始）。</summary>
    Capture = 1,

    /// <summary>ポインターのキャプチャを解放する（ドラッグ終了）。</summary>
    Release = 2,
}
