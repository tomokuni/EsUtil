using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;

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
    /// <returns>利用者が選んだ結果。 閉じられた（×・Esc）場合は <see cref="WindowsAppRuntimeInstallChoice.Exit"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> が null の場合。</exception>
    public static WindowsAppRuntimeInstallChoice Ask(WindowsAppRuntimeInstallPromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TryShowTaskDialog(request) ?? ShowMessageBox(request);
    }

    /// <summary>タスク ダイアログで尋ねる。</summary>
    /// <param name="request">案内に出す情報。</param>
    /// <returns>選ばれた結果。 タスク ダイアログを使えない場合は null（呼び出し側がメッセージ ボックスへ落ちる）。</returns>
    private static unsafe WindowsAppRuntimeInstallChoice? TryShowTaskDialog(
        WindowsAppRuntimeInstallPromptRequest request)
    {
        var mainInstruction = $"Windows App Runtime {request.RequiredVersion} 以降が必要です。";
        var content = BuildContent(request, hyperlink: true);
        const string InstallLabel = "自動インストールする";
        const string OpenPageLabel = "ダウンロードページを表示する";
        const string ExitLabel = "インストールせずにアプリを終了する";

        // 本文の URL をクリックできるようにする（TDF_ENABLE_HYPERLINKS + 通知の処理）。
        // デリゲートは呼び出しが終わるまで参照を保持する（GC に回収されるとダイアログから呼べなくなる）
        PFTASKDIALOGCALLBACK? callback = OnTaskDialogNotification;
        try
        {
            fixed (char* title = WindowTitle)
            fixed (char* main = mainInstruction)
            fixed (char* body = content)
            fixed (char* install = InstallLabel)
            fixed (char* openPage = OpenPageLabel)
            fixed (char* exit = ExitLabel)
            {
                Span<TASKDIALOG_BUTTON> buttons =
                [
                    new() { nButtonID = InstallButtonId, pszButtonText = install },
                    new() { nButtonID = OpenPageButtonId, pszButtonText = openPage },
                    new() { nButtonID = ExitButtonId, pszButtonText = exit },
                ];

                fixed (TASKDIALOG_BUTTON* buttonPointer = buttons)
                {
                    var config = new TASKDIALOGCONFIG
                    {
                        // 構造体は関数ポインター（デリゲート）を含むため sizeof を使えない（Marshal の大きさを使う）
                        cbSize = (uint)Marshal.SizeOf<TASKDIALOGCONFIG>(),
                        dwFlags = TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION
                            | TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT
                            | TASKDIALOG_FLAGS.TDF_ENABLE_HYPERLINKS,
                        pszWindowTitle = title,
                        pszMainInstruction = main,
                        pszContent = body,
                        cButtons = (uint)buttons.Length,
                        pButtons = buttonPointer,
                        nDefaultButton = InstallButtonId,
                        pfCallback = callback,
                    };

                    var result = TryInvokeTaskDialog(config, out var button);
                    if (result is null)
                    {
                        // ComCtl32 v6 が有効でない（マニフェストが Common-Controls を宣言していない）環境など。
                        // 呼び出し側がメッセージ ボックスへ落ちる
                        return null;
                    }

                    return button switch
                    {
                        InstallButtonId => WindowsAppRuntimeInstallChoice.Install,
                        OpenPageButtonId => WindowsAppRuntimeInstallChoice.OpenDownloadPage,
                        _ => WindowsAppRuntimeInstallChoice.Exit,
                    };
                }
            }
        }
        finally
        {
            GC.KeepAlive(callback);
        }
    }

    /// <summary>タスク ダイアログからの通知を受け取る（本文の URL がクリックされたらブラウザーで開く）。</summary>
    /// <param name="hwnd">ダイアログのウィンドウ ハンドル。</param>
    /// <param name="msg">通知の種類。</param>
    /// <param name="wParam">通知ごとの値（未使用）。</param>
    /// <param name="lParam">通知ごとの値（ハイパーリンクの場合は URL へのポインター）。</param>
    /// <param name="refData">ダイアログへ渡した任意の値（未使用）。</param>
    /// <returns>常に成功（S_OK）。</returns>
    /// <remarks>
    /// URL は本文に埋め込んだ当ライブラリのダウンロード ページだけであるため、<paramref name="lParam"/> の文字列は読まず、
    /// <see cref="WindowsAppRuntimeDownloadPage.Uri"/> を開く（外部から渡された文字列を実行しない）。
    /// </remarks>
    private static HRESULT OnTaskDialogNotification(
        HWND hwnd, TASKDIALOG_NOTIFICATIONS msg, WPARAM wParam, LPARAM lParam, nint refData)
    {
        if (msg == TASKDIALOG_NOTIFICATIONS.TDN_HYPERLINK_CLICKED)
        {
            // 開けなくてもダイアログは閉じない（利用者は URL を手で開ける）
            WindowsAppRuntimeDownloadPage.Open();
        }

        return default;
    }

    /// <summary>タスク ダイアログを呼ぶ（失敗した場合は null）。</summary>
    /// <param name="config">ダイアログの設定。</param>
    /// <param name="button">押されたボタンの ID。</param>
    /// <returns>成功した場合は HRESULT。 使えない場合は null。</returns>
    private static int? TryInvokeTaskDialog(in TASKDIALOGCONFIG config, out int button)
    {
        button = ExitButtonId;
        try
        {
            var result = (int)PInvoke.TaskDialogIndirect(config, out button, out _, out _);
            return result < 0 ? null : result;
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException or NotSupportedException)
        {
            // ComCtl32 の v5（Common-Controls を宣言していないアプリ）には TaskDialogIndirect が無い（実測 2026-11-05）。
            // メッセージ ボックスへ落とす（案内そのものは続けられる）
            return null;
        }
    }

    /// <summary>メッセージ ボックスで尋ねる（タスク ダイアログを使えない環境向け）。</summary>
    /// <param name="request">案内に出す情報。</param>
    /// <returns>選ばれた結果。 閉じられた場合は <see cref="WindowsAppRuntimeInstallChoice.Exit"/>。</returns>
    /// <remarks>
    /// ボタンの表題は OS が決める（はい / いいえ / キャンセル）ため、<b>本文へ選択肢との対応を書く</b>。
    /// </remarks>
    private static WindowsAppRuntimeInstallChoice ShowMessageBox(WindowsAppRuntimeInstallPromptRequest request)
    {
        var text = BuildContent(request, hyperlink: false)
            + Environment.NewLine
            + Environment.NewLine
            + "[はい] 自動インストールする"
            + Environment.NewLine
            + "[いいえ] ダウンロードページを表示する"
            + Environment.NewLine
            + "[キャンセル] インストールせずにアプリを終了する";

        var result = PInvoke.MessageBox(
            default,
            text,
            WindowTitle,
            MESSAGEBOX_STYLE.MB_YESNOCANCEL | MESSAGEBOX_STYLE.MB_ICONWARNING
                | MESSAGEBOX_STYLE.MB_DEFBUTTON1 | MESSAGEBOX_STYLE.MB_TOPMOST);

        return result switch
        {
            MESSAGEBOX_RESULT.IDYES => WindowsAppRuntimeInstallChoice.Install,
            MESSAGEBOX_RESULT.IDNO => WindowsAppRuntimeInstallChoice.OpenDownloadPage,
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
