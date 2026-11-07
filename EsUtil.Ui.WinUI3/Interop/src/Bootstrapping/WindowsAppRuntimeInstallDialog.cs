using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;

namespace EsUtil.Ui.WinUI3.Interop.Bootstrapping;

/// <summary>「Windows App Runtime が必要」という案内を Win32 のダイアログで表示する（既定の案内 UI）。</summary>
/// <remarks>
/// <b>なぜ XAML を使わないか</b>: 案内は WinUI の初期化より前（<c>Application</c> の生成前）に出るため、
/// XAML の要素を作れない。 Win32 のタスク ダイアログ（<c>TaskDialogIndirect</c>）とメッセージ ボックスだけで組む。<br/>
/// <br/>
/// <b>選択肢</b>: 「自動インストールする」「ダウンロードページを表示する」「インストールせずにアプリを終了する」の 3 つを出し、
/// 内容にはダウンロード ページの URL を表示する（<b>URL はクリックするとブラウザーで開く</b>。 ダイアログは閉じない）。<br/>
/// <br/>
/// <b>タスク ダイアログを使えない場合</b>: <c>TaskDialogIndirect</c> は ComCtl32 の v6（アプリのマニフェストが
/// <c>Microsoft.Windows.Common-Controls</c> を宣言している状態）でしか動かない。 動かない環境では
/// <b>メッセージ ボックスへ落ちる</b>（はい＝自動インストール / いいえ＝ダウンロードページを表示 / キャンセル＝終了。 本文に同じ 3 択を書く）。<br/>
/// <br/>
/// <b>Win32 の呼び出しはコアが単一所有する</b>: ダイアログは <see cref="IDialogService"/> 越しに表示する
/// （既定実装は <see cref="WinDialogService"/>。 本 UI 層は P/Invoke を持たない）。<br/>
/// <b>UI スレッド</b>: ダイアログは同期で表示する（呼び出し元のスレッドを止める）。 起動処理の途中で呼ぶこと。
/// </remarks>
internal static class WindowsAppRuntimeInstallDialog
{
    /// <summary>「自動インストールする」ボタンの ID。</summary>
    private const int InstallButtonId = 100;

    /// <summary>「ダウンロードページを表示する」ボタンの ID。</summary>
    private const int OpenPageButtonId = 101;

    /// <summary>「インストールせずにアプリを終了する」ボタンの ID。</summary>
    private const int ExitButtonId = 102;

    /// <summary>ダイアログのタイトル。</summary>
    private const string WindowTitle = "Windows App Runtime が必要です";

    /// <summary>利用者へ選択を尋ねる。</summary>
    /// <param name="request">案内に出す情報。 null 不可。</param>
    /// <param name="dialogs">ダイアログの表示先。 null の場合は Windows の既定実装を使う。</param>
    /// <returns>利用者が選んだ結果。 閉じられた（×・Esc）場合は <see cref="WindowsAppRuntimeInstallChoice.Exit"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> が null の場合。</exception>
    public static WindowsAppRuntimeInstallChoice Ask(
        WindowsAppRuntimeInstallPromptRequest request,
        IDialogService? dialogs = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        dialogs ??= new WinDialogService();
        return TryShowTaskDialog(request, dialogs) ?? ShowMessageBox(request, dialogs);
    }

    /// <summary>タスク ダイアログで尋ねる。</summary>
    /// <param name="request">案内に出す情報。</param>
    /// <param name="dialogs">ダイアログの表示先。</param>
    /// <returns>選ばれた結果。 タスク ダイアログを使えない場合は null（呼び出し側がメッセージ ボックスへ落ちる）。</returns>
    private static WindowsAppRuntimeInstallChoice? TryShowTaskDialog(
        WindowsAppRuntimeInstallPromptRequest request,
        IDialogService dialogs)
    {
        EsDialogButton[] buttons =
        [
            new(InstallButtonId, "自動インストールする"),
            new(OpenPageButtonId, "ダウンロードページを表示する"),
            new(ExitButtonId, "インストールせずにアプリを終了する"),
        ];

        // 本文の URL をクリックできるようにする（リンクの処理はコアのタスク ダイアログが担う）
        var shown = dialogs.TryShowTaskDialog(
            WindowTitle,
            $"Windows App Runtime {request.RequiredVersion} 以降が必要です。",
            BuildContent(request, hyperlink: true),
            buttons,
            InstallButtonId,
            static _ => WindowsAppRuntimeDownloadPage.Open(),
            out var selectedButtonId);

        if (!shown)
        {
            return null;
        }

        return selectedButtonId switch
        {
            InstallButtonId => WindowsAppRuntimeInstallChoice.Install,
            OpenPageButtonId => WindowsAppRuntimeInstallChoice.OpenDownloadPage,
            _ => WindowsAppRuntimeInstallChoice.Exit,
        };
    }

    /// <summary>メッセージ ボックスで尋ねる（タスク ダイアログを使えない環境向け）。</summary>
    /// <param name="request">案内に出す情報。</param>
    /// <param name="dialogs">ダイアログの表示先。</param>
    /// <returns>選ばれた結果。 閉じられた場合は <see cref="WindowsAppRuntimeInstallChoice.Exit"/>。</returns>
    /// <remarks>
    /// ボタンの表題は OS が決める（はい / いいえ / キャンセル）ため、<b>本文へ選択肢との対応を書く</b>。
    /// </remarks>
    private static WindowsAppRuntimeInstallChoice ShowMessageBox(
        WindowsAppRuntimeInstallPromptRequest request,
        IDialogService dialogs)
    {
        var text = BuildContent(request, hyperlink: false)
            + Environment.NewLine
            + Environment.NewLine
            + "[はい] 自動インストールする"
            + Environment.NewLine
            + "[いいえ] ダウンロードページを表示する"
            + Environment.NewLine
            + "[キャンセル] インストールせずにアプリを終了する";

        return dialogs.ShowYesNoCancel(WindowTitle, text) switch
        {
            EsDialogResult.Yes => WindowsAppRuntimeInstallChoice.Install,
            EsDialogResult.No => WindowsAppRuntimeInstallChoice.OpenDownloadPage,
            _ => WindowsAppRuntimeInstallChoice.Exit,
        };
    }

    /// <summary>案内の本文を組み立てる（ダウンロード ページの URL をクリックできるリンクとして含める）。</summary>
    /// <param name="request">案内に出す情報。</param>
    /// <param name="hyperlink">URL を <c>&lt;a&gt;</c> で囲むかどうか。 タスク ダイアログでは true、メッセージ ボックスでは false。</param>
    /// <returns>本文。</returns>
    /// <remarks>
    /// URL は <c>&lt;a&gt;</c> で囲む（タスク ダイアログの <c>TDF_ENABLE_HYPERLINKS</c> がリンクとして扱う）。
    /// メッセージ ボックスはリンクを扱えないため、タグを付けない URL だけの行にする。
    /// </remarks>
    private static string BuildContent(WindowsAppRuntimeInstallPromptRequest request, bool hyperlink)
    {
        var installed = request.InstalledVersion is { } version
            ? $"インストール済みの版: {version}"
            : "インストール済みの版: なし";

        var url = request.DownloadPageUri.AbsoluteUri;
        var urlLine = hyperlink ? $"<a href=\"{url}\">{url}</a>" : url;

        return "このアプリは WinUI 3 を使うため、Windows App Runtime が必要です。"
            + Environment.NewLine
            + installed
            + Environment.NewLine
            + Environment.NewLine
            + "ダウンロード ページ（インストーラー）:"
            + Environment.NewLine
            + urlLine;
    }
}
