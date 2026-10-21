using EsUtil.Platform.Core;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace EsUtil.Ui.WinUI3.Interop;

/// <summary>コアのネイティブウィンドウ識別子と WinUI 3 のウィンドウを相互変換する。</summary>
/// <remarks>
/// <see cref="WindowNative"/>.<c>GetWindowHandle</c> が返す Win32 の <c>HWND</c> を <see cref="EsPlatformHandle"/> として扱う。<br/>
/// Win32 の HWND そのものであるため、<c>IWindowService</c> / <c>ICursorOverrideGuard</c> などの<br/>
/// ハンドルを要求するサービスへ WinUI 3 のウィンドウを渡せる。<br/>
/// <b>呼び出し条件</b>: ウィンドウが生成済みであること（生成前はハンドルが 0 になる）。
/// </remarks>
public static class EsPlatformHandleInterop
{
    /// <summary>WinUI 3 のウィンドウをコアのネイティブウィンドウ識別子へ変換する。</summary>
    /// <param name="window">変換元のウィンドウ。 null の場合はハンドル値 0。</param>
    /// <returns>コアの識別子（Win32: HWND 相当）。</returns>
    public static EsPlatformHandle ToEsPlatformHandle(this Window? window)
        => new(window is null ? 0 : WindowNative.GetWindowHandle(window));
}
