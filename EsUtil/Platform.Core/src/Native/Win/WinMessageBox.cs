using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>XAML などの初期化前に使えるエラー表示（Win32 のメッセージ ボックス）。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: UI フレームワークの初期化に失敗する状況（例: アンパッケージの WinUI 3 アプリを
/// .NET ホストから起動した）では、ログだけでは利用者が気づけない。 <c>MessageBox</c> はウィンドウも XAML も
/// 必要としないため、<b>起動処理の最初</b>から使える。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。<br/>
/// <b>失敗しても例外にしない</b>: 表示できない状況（対話セッションでない等）でも起動処理を妨げない。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public static class WinMessageBox
{
    /// <summary>エラーのメッセージ ボックスを表示する。</summary>
    /// <param name="caption">表題（アプリ名）。</param>
    /// <param name="text">本文。</param>
    /// <remarks>利用者が閉じるまで待つが、選択されたボタンでは分岐しない（戻り値の型も呼び出し側へ出さない）。</remarks>
    public static void ShowError(string caption, string text)
        => _ = PInvoke.MessageBox(default, text, caption, MESSAGEBOX_STYLE.MB_ICONERROR);
}
