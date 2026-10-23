using EsUtil.Platform.Core.Window;

namespace EsUtil.Ui.WinForms.Interop;

/// <summary>コアのネイティブウィンドウ識別子と WinForms のコントロールを相互変換する。</summary>
/// <remarks>
/// <see cref="Control.Handle"/>（<c>System.IntPtr</c>）を <see cref="EsPlatformHandle"/> として扱う。<br/>
/// Win32 の HWND そのものであるため、<c>IWindowService</c> / <c>ICursorOverrideGuard</c> などの<br/>
/// ハンドルを要求するサービスへ WinForms のコントロール（フォーム）を渡せる。<br/>
/// <b>ハンドルを生成しない</b>: <see cref="Control.Handle"/> は取得した時点でウィンドウを生成するため、<br/>
/// 未生成の場合はハンドル値 0 を返す（ウィンドウを必要としない文脈で副作用を起こさない）。
/// </remarks>
public static class EsPlatformHandleInterop
{
    /// <summary>WinForms のコントロールをコアのネイティブウィンドウ識別子へ変換する。</summary>
    /// <param name="control">変換元のコントロール。 null またはハンドル未生成の場合はハンドル値 0。</param>
    /// <returns>コアの識別子（Win32: HWND 相当）。</returns>
    public static EsPlatformHandle ToEsPlatformHandle(this Control? control)
        => new(control is null || !control.IsHandleCreated ? 0 : control.Handle);
}
