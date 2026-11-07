namespace EsUtil.Platform.Core.Native;

/// <summary>メッセージ ボックスで押されたボタン。</summary>
/// <remarks><see cref="IDialogService.ShowYesNoCancel"/> の戻り値。</remarks>
public enum EsDialogResult
{
    /// <summary>「はい」。</summary>
    Yes,

    /// <summary>「いいえ」。</summary>
    No,

    /// <summary>「キャンセル」。</summary>
    Cancel,

    /// <summary>閉じられた（×・Esc）。 ボタンは押されていない。</summary>
    Close,
}
