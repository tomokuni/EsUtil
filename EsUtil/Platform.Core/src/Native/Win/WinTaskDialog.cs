using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>ComCtl32 v6 のタスク ダイアログを表示する（XAML などの初期化前でも使える案内 UI）。</summary>
/// <remarks>
/// <b>メッセージ ボックスとの違い</b>: ボタンの表題を指定でき、本文の URL をリンクとして扱える。 起動処理の途中
/// （UI フレームワークの初期化前）に、利用者へ選択を求める用途で使う。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。<br/>
/// <b>使えない環境</b>: <c>TaskDialogIndirect</c> は ComCtl32 の v6（アプリのマニフェストが
/// <c>Microsoft.Windows.Common-Controls</c> を宣言している状態）でしか動かない。 <b>使えない場合は例外にせず
/// <see langword="false"/> を返す</b>（呼び出し側がメッセージ ボックスへ落ちる）。<br/>
/// <b>UI スレッド</b>: ダイアログは同期で表示する（呼び出し元のスレッドを止める）。
/// </remarks>
public static class WinTaskDialog
{
    /// <summary>タスク ダイアログを表示し、押されたボタンの識別子を返す。</summary>
    /// <param name="windowTitle">ダイアログの表題。 null 不可。</param>
    /// <param name="mainInstruction">主要な指示（太字で表示される 1 行）。 null 不可。</param>
    /// <param name="content">本文。 改行は <see cref="Environment.NewLine"/>。 null 不可。</param>
    /// <param name="buttons">ボタン（1 つ以上。 識別子は 0 以外）。 null 不可。</param>
    /// <param name="defaultButtonId">既定のボタンの識別子（<paramref name="buttons"/> のいずれか）。</param>
    /// <param name="hyperlinkHandler">本文のリンクが押されたときに URL を受け取る処理。<see langword="null"/> の場合はリンクを有効にしない。</param>
    /// <param name="selectedButtonId">押されたボタンの識別子。 表示できなかった場合・閉じられた場合は <paramref name="defaultButtonId"/>。</param>
    /// <returns>表示できた場合は <see langword="true"/>。 タスク ダイアログを使えない環境では <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException">必須の引数が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="buttons"/> が空の場合。</exception>
    /// <remarks>
    /// <b>表示できたかどうかだけを返す</b>: 選択の解釈（識別子から意味への写像）は呼び出し側が行う（ダイアログは
    /// 特定の業務を知らない）。<br/>
    /// <b>リンクの処理</b>: 本文の <c>&lt;a href="..."&gt;</c> が押されると <paramref name="hyperlinkHandler"/> へ
    /// URL を渡す。<b>外部から渡された文字列を実行しない</b>（処理は呼び出し側が自分の URL だけを開く）。
    /// </remarks>
    public static unsafe bool TryShow(
        string windowTitle,
        string mainInstruction,
        string content,
        IReadOnlyList<EsDialogButton> buttons,
        int defaultButtonId,
        Func<string, bool>? hyperlinkHandler,
        out int selectedButtonId)
    {
        ArgumentNullException.ThrowIfNull(windowTitle);
        ArgumentNullException.ThrowIfNull(mainInstruction);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(buttons);
        if (buttons.Count == 0)
        {
            throw new ArgumentException("ボタンが 1 つもありません。", nameof(buttons));
        }

        selectedButtonId = defaultButtonId;

        // リンクの処理はダイアログの表示が終わるまで参照を保持する（GC に回収されるとダイアログから呼べなくなる）
        var handlerHandle = hyperlinkHandler is null ? (GCHandle?)null : GCHandle.Alloc(hyperlinkHandler);
        var titlePointer = Marshal.StringToHGlobalUni(windowTitle);
        var mainPointer = Marshal.StringToHGlobalUni(mainInstruction);
        var contentPointer = Marshal.StringToHGlobalUni(content);
        var buttonPointers = new nint[buttons.Count];
        try
        {
            for (var i = 0; i < buttons.Count; i++)
            {
                buttonPointers[i] = Marshal.StringToHGlobalUni(buttons[i].Text);
            }

            var flags = TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION | TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT;
            if (handlerHandle is not null)
            {
                flags |= TASKDIALOG_FLAGS.TDF_ENABLE_HYPERLINKS;
            }

            var buttonArray = new TASKDIALOG_BUTTON[buttons.Count];
            for (var i = 0; i < buttons.Count; i++)
            {
                buttonArray[i].nButtonID = buttons[i].Id;
                buttonArray[i].pszButtonText = (char*)buttonPointers[i];
            }

            fixed (TASKDIALOG_BUTTON* buttonPointer = buttonArray)
            {
                var config = new TASKDIALOGCONFIG
                {
                    // 構造体は関数ポインターを含むため sizeof を使えない（Marshal の大きさを使う）
                    cbSize = (uint)Marshal.SizeOf<TASKDIALOGCONFIG>(),
                    dwFlags = flags,
                    pszWindowTitle = (char*)titlePointer,
                    pszMainInstruction = (char*)mainPointer,
                    pszContent = (char*)contentPointer,
                    cButtons = (uint)buttonArray.Length,
                    pButtons = buttonPointer,
                    nDefaultButton = defaultButtonId,
                    pfCallback = &OnTaskDialogNotification,
                    lpCallbackData = handlerHandle is { } handler ? GCHandle.ToIntPtr(handler) : 0,
                };

                try
                {
                    var result = (int)PInvoke.TaskDialogIndirect(config, out var button, out _, out _);
                    if (result < 0)
                    {
                        return false;
                    }

                    selectedButtonId = button;
                    return true;
                }
                catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException or NotSupportedException)
                {
                    // ComCtl32 の v5（Common-Controls を宣言していないアプリ）には TaskDialogIndirect が無い（実測 2026-11-05）
                    return false;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(titlePointer);
            Marshal.FreeHGlobal(mainPointer);
            Marshal.FreeHGlobal(contentPointer);
            foreach (var pointer in buttonPointers)
            {
                if (pointer != 0)
                {
                    Marshal.FreeHGlobal(pointer);
                }
            }

            handlerHandle?.Free();
        }
    }

    /// <summary>タスク ダイアログからの通知を受け取る（本文のリンクが押されたら呼び出し側の処理へ渡す）。</summary>
    /// <param name="hwnd">ダイアログのウィンドウ ハンドル（未使用）。</param>
    /// <param name="msg">通知の種類。</param>
    /// <param name="wParam">通知ごとの値（未使用）。</param>
    /// <param name="lParam">通知ごとの値（リンクの場合は URL へのポインター）。</param>
    /// <param name="refData">ダイアログへ渡した任意の値（<c>GCHandle</c> のポインター）。</param>
    /// <returns>常に成功（S_OK）。</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static HRESULT OnTaskDialogNotification(
        HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam, nint refData)
    {
        if (msg == (uint)TASKDIALOG_NOTIFICATIONS.TDN_HYPERLINK_CLICKED && refData != 0)
        {
            var handler = GCHandle.FromIntPtr(refData).Target as Func<string, bool>;
            var url = Marshal.PtrToStringUni((nint)lParam);
            if (handler is not null && !string.IsNullOrEmpty(url))
            {
                _ = handler(url);
            }
        }

        return default;
    }
}
