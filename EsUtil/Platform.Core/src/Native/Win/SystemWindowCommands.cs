using System.Runtime.Versioning;
using EsUtil.Platform.Core.Window;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>ウィンドウの標準メニュー（左上のアイコンの押下で出す）から実行する操作。</summary>
/// <remarks>
/// <b>移動・サイズ変更は OS に任せる</b>: <c>WM_SYSCOMMAND</c>（<c>SC_MOVE</c> / <c>SC_SIZE</c>）を送ると、<br/>
/// Windows がマウス操作で移動・サイズ変更するモードへ入る（アプリ側で追従を実装しない）。<br/>
/// <b>失敗しても例外にしない</b>: 操作できない状態でもウィンドウは動作し続ける（メニューを閉じるだけ）。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public static class SystemWindowCommands
{
    /// <summary>ウィンドウの移動を開始する（マウスで移動するモードへ入る）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <returns>コマンドを送れた場合は <see langword="true"/>。</returns>
    public static bool BeginMove(EsPlatformHandle handle) => SendCommand(handle, PInvoke.SC_MOVE);

    /// <summary>ウィンドウのサイズ変更を開始する（マウスでサイズを変えるモードへ入る）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <returns>コマンドを送れた場合は <see langword="true"/>。</returns>
    public static bool BeginSize(EsPlatformHandle handle) => SendCommand(handle, PInvoke.SC_SIZE);

    /// <summary>システム コマンドをウィンドウへ送る。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <param name="command">システム コマンド。</param>
    /// <returns>送れた場合は <see langword="true"/>。</returns>
    private static bool SendCommand(EsPlatformHandle handle, uint command)
        => handle.IsValid
            && PInvoke.PostMessage((HWND)handle.Handle, PInvoke.WM_SYSCOMMAND, command, default);
}
