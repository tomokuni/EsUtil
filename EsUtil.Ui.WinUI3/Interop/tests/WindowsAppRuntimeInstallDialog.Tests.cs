using EsUtil.Platform.Core.Native;
using EsUtil.Ui.WinUI3.Interop.Bootstrapping;
using Xunit;

namespace EsUtil.Ui.WinUI3.Interop.Tests;

/// <summary><see cref="WindowsAppRuntimeInstallDialog"/> の単体テスト（選択の写像とフォールバック）。</summary>
/// <remarks>
/// <see cref="IDialogService"/> を差し替えるため<b>ダイアログを表示しない</b>（ヘッドレスのセッションでも実行できる）。<br/>
/// タスク ダイアログの 3 択と、タスク ダイアログを使えない環境でのメッセージ ボックスの 3 択を分けて検証する。
/// </remarks>
public sealed class WindowsAppRuntimeInstallDialogTests
{
    /// <summary>案内に渡す情報を作る。</summary>
    /// <returns>必要とする版とダウンロード ページを持つ情報。</returns>
    private static WindowsAppRuntimeInstallPromptRequest CreateRequest()
        => new()
        {
            RequiredVersion = new Version(2, 6, 4, 0),
            DownloadPageUri = new Uri("https://aka.ms/windowsappsdk/example"),
        };

    /// <summary>タスク ダイアログの選択が結果へ写像されることを検証する。</summary>
    /// <param name="buttonId">押されたボタンの識別子。</param>
    /// <param name="expected">期待する結果。</param>
    /// <remarks>想定外の識別子（閉じられた場合を含む）は「インストールせずに終了する」として扱う。</remarks>
    [Theory]
    [InlineData(100, WindowsAppRuntimeInstallChoice.Install)]
    [InlineData(101, WindowsAppRuntimeInstallChoice.OpenDownloadPage)]
    [InlineData(102, WindowsAppRuntimeInstallChoice.Exit)]
    [InlineData(999, WindowsAppRuntimeInstallChoice.Exit)]
    internal void Ask_タスクダイアログの選択を結果へ写像する(int buttonId, WindowsAppRuntimeInstallChoice expected)
    {
        var dialogs = new DialogServiceSpy { TaskDialogResult = buttonId };

        var choice = WindowsAppRuntimeInstallDialog.Ask(CreateRequest(), dialogs);

        Assert.Equal(expected, choice);
        Assert.Equal(1, dialogs.TaskDialogCalls);
        Assert.Equal(0, dialogs.MessageBoxCalls);
    }

    /// <summary>タスク ダイアログを使えない場合はメッセージ ボックスの選択へ落ちることを検証する。</summary>
    /// <param name="result">メッセージ ボックスで押されたボタン。</param>
    /// <param name="expected">期待する結果。</param>
    [Theory]
    [InlineData(EsDialogResult.Yes, WindowsAppRuntimeInstallChoice.Install)]
    [InlineData(EsDialogResult.No, WindowsAppRuntimeInstallChoice.OpenDownloadPage)]
    [InlineData(EsDialogResult.Cancel, WindowsAppRuntimeInstallChoice.Exit)]
    internal void Ask_タスクダイアログを使えない場合はメッセージボックスへ落ちる(
        EsDialogResult result,
        WindowsAppRuntimeInstallChoice expected)
    {
        var dialogs = new DialogServiceSpy { IsTaskDialogAvailable = false, MessageBoxResult = result };

        var choice = WindowsAppRuntimeInstallDialog.Ask(CreateRequest(), dialogs);

        Assert.Equal(expected, choice);
        Assert.Equal(1, dialogs.TaskDialogCalls);
        Assert.Equal(1, dialogs.MessageBoxCalls);
    }

    /// <summary>タスク ダイアログへ 3 択とリンク付きの本文を渡すことを検証する。</summary>
    /// <remarks>本文の URL が <c>&lt;a&gt;</c> で囲まれていないと、ダイアログ上でクリックできない。</remarks>
    [Fact]
    internal void Ask_本文へリンクと3択を渡す()
    {
        var dialogs = new DialogServiceSpy { TaskDialogResult = 100 };

        _ = WindowsAppRuntimeInstallDialog.Ask(CreateRequest(), dialogs);

        Assert.Equal(3, dialogs.LastButtonCount);
        Assert.Contains("<a href=\"https://aka.ms/windowsappsdk/example\">", dialogs.LastTaskDialogContent, StringComparison.Ordinal);
        Assert.Contains("2.6.4.0", dialogs.LastMainInstruction, StringComparison.Ordinal);
    }

    /// <summary>案内に出す情報が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void Ask_情報がnullなら例外を投げる()
    {
        Assert.Throws<ArgumentNullException>(() => WindowsAppRuntimeInstallDialog.Ask(null!, new DialogServiceSpy()));
    }
}
