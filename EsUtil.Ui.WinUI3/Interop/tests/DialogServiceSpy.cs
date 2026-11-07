using EsUtil.Platform.Core.Native;

namespace EsUtil.Ui.WinUI3.Interop.Tests;

/// <summary>テストで使用するダイアログのスタブ（表示せずに選択を返す）。</summary>
/// <remarks>
/// <see cref="IDialogService"/> を実装し、ダイアログを表示せずに結果だけを返す。<br/>
/// タスク ダイアログを使えるかどうか（<see cref="IsTaskDialogAvailable"/>）と、利用者の選択をテストが決められる。
/// </remarks>
internal sealed class DialogServiceSpy : IDialogService
{
    /// <summary>タスク ダイアログを使えるかどうか（<see langword="false"/> は ComCtl32 v6 が無い環境を表す）。</summary>
    public bool IsTaskDialogAvailable { get; set; } = true;

    /// <summary>タスク ダイアログで押されたボタンの識別子。</summary>
    public int TaskDialogResult { get; set; }

    /// <summary>メッセージ ボックスで押されたボタン。</summary>
    public EsDialogResult MessageBoxResult { get; set; }

    /// <summary>タスク ダイアログを表示しようとした回数。</summary>
    public int TaskDialogCalls { get; private set; }

    /// <summary>メッセージ ボックスを表示しようとした回数。</summary>
    public int MessageBoxCalls { get; private set; }

    /// <summary>最後に渡されたタスク ダイアログの本文。</summary>
    public string LastTaskDialogContent { get; private set; } = string.Empty;

    /// <summary>最後に渡されたタスク ダイアログの主要な指示（太字の 1 行）。</summary>
    public string LastMainInstruction { get; private set; } = string.Empty;

    /// <summary>最後に渡されたタスク ダイアログのボタン数。</summary>
    public int LastButtonCount { get; private set; }

    /// <inheritdoc/>
    public bool TryShowTaskDialog(
        string windowTitle,
        string mainInstruction,
        string content,
        IReadOnlyList<EsDialogButton> buttons,
        int defaultButtonId,
        Func<string, bool>? hyperlinkHandler,
        out int selectedButtonId)
    {
        TaskDialogCalls++;
        LastTaskDialogContent = content;
        LastMainInstruction = mainInstruction;
        LastButtonCount = buttons.Count;
        selectedButtonId = IsTaskDialogAvailable ? TaskDialogResult : defaultButtonId;
        return IsTaskDialogAvailable;
    }

    /// <inheritdoc/>
    public EsDialogResult ShowYesNoCancel(string caption, string text)
    {
        MessageBoxCalls++;
        return MessageBoxResult;
    }

    /// <inheritdoc/>
    public void ShowError(string caption, string text) => ShowErrorCalls++;

    /// <summary>エラーのメッセージ ボックスを表示しようとした回数。</summary>
    public int ShowErrorCalls { get; private set; }
}
