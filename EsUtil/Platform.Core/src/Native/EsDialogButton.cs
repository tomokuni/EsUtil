namespace EsUtil.Platform.Core.Native;

/// <summary>タスク ダイアログのボタン。</summary>
/// <param name="Id">押されたときに返す識別子（呼び出し側が決める）。</param>
/// <param name="Text">ボタンの表題。</param>
/// <remarks>
/// <see cref="IDialogService.TryShowTaskDialog"/> へ渡す。 <b>識別子は 0 以外にする</b>（0 は「押されなかった」の意味に使う）。
/// </remarks>
public readonly record struct EsDialogButton(int Id, string Text);
