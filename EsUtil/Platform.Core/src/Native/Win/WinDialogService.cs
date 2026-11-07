namespace EsUtil.Platform.Core.Native.Win;

/// <summary>OS のダイアログ（案内 UI）を Win32 で提供する。</summary>
/// <remarks>
/// <b>タスク ダイアログとメッセージ ボックスを 1 つの入口にまとめる</b>: 案内 UI は「タスク ダイアログを使い、
/// 使えなければメッセージ ボックスへ落ちる」形で対になっており、呼び出し側が両方を直接知る必要はない。<br/>
/// タスク ダイアログの表示は <see cref="WinTaskDialog"/> が、メッセージ ボックスの表示は <see cref="WinMessageBox"/> が担う
/// （本クラスは Win32 を直接呼ばない）。<br/>
/// <see cref="IDialogService"/> の実装であり、テストでは差し替えられる。<br/>
/// 継承不可。
/// </remarks>
public sealed class WinDialogService : IDialogService
{
    /// <summary>タスク ダイアログを表示し、押されたボタンの識別子を返す。</summary>
    /// <inheritdoc/>
    public bool TryShowTaskDialog(
        string windowTitle,
        string mainInstruction,
        string content,
        IReadOnlyList<EsDialogButton> buttons,
        int defaultButtonId,
        Func<string, bool>? hyperlinkHandler,
        out int selectedButtonId)
        => WinTaskDialog.TryShow(
            windowTitle,
            mainInstruction,
            content,
            buttons,
            defaultButtonId,
            hyperlinkHandler,
            out selectedButtonId);

    /// <summary>警告のメッセージ ボックスを表示し、押されたボタンを返す。</summary>
    /// <inheritdoc/>
    public EsDialogResult ShowYesNoCancel(string caption, string text)
        => WinMessageBox.ShowYesNoCancel(caption, text);

    /// <summary>エラーのメッセージ ボックスを表示する。</summary>
    /// <inheritdoc/>
    public void ShowError(string caption, string text) => WinMessageBox.ShowError(caption, text);
}
