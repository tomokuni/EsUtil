using Aprillz.MewUI;

namespace EsUtil.Ui.MewUI.Interop;

/// <summary>コアのネイティブウィンドウ識別子と MewUI のウィンドウを相互変換する。</summary>
/// <remarks>
/// <see cref="Window.Handle"/>（<c>System.IntPtr</c>）を <see cref="EsPlatformHandle"/> として扱う。<br/>
/// Win32 の HWND に相当するため、<c>IWindowService</c> / <c>ICursorOverrideGuard</c> などの<br/>
/// ハンドルを要求するサービスへ MewUI のウィンドウを渡せる。<br/>
/// </remarks>
public static class EsPlatformHandleInterop
{
    /// <summary>MewUI のウィンドウをコアのネイティブウィンドウ識別子へ変換する。</summary>
    /// <param name="window">変換元のウィンドウ。 null の場合はハンドル値 0。</param>
    /// <returns>コアの識別子（Win32: HWND 相当）。</returns>
    public static EsPlatformHandle ToEsPlatformHandle(this Window? window)
        => new(window?.Handle ?? 0);
}
