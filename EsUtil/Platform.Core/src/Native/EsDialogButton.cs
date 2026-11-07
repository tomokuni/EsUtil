namespace EsUtil.Platform.Core.Native;

/// <summary>タスク ダイアログのボタン。</summary>
/// <param name="Id">押されたときに返す識別子（呼び出し側が決める）。</param>
/// <param name="Text">ボタンの表題。</param>
/// <remarks>
/// <see cref="IDialogService.TryShowTaskDialog"/> へ渡す。 <b>識別子は 0 以外にする</b>
/// （閉じられた場合は識別子ではなく OS のキャンセル相当の値が返るため、呼び出し側は一覧に無い値を「閉じられた」として扱う）。
/// </remarks>
public readonly record struct EsDialogButton(int Id, string Text);
