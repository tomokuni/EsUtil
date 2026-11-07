namespace EsUtil.Platform.Core.Native;

/// <summary>メッセージ ボックスで押されたボタン。</summary>
/// <remarks><see cref="IDialogService.ShowYesNoCancel"/> の戻り値。</remarks>
public enum EsDialogResult
{
    /// <summary>「はい」。</summary>
    Yes,

    /// <summary>「いいえ」。</summary>
    No,

    /// <summary>「キャンセル」。 閉じられた場合（×・Esc）もこの値になる。</summary>
    Cancel,
}
