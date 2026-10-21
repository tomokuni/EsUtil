using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace EsUtil.Ui.WinUI3.Interop;

/// <summary>撮影オーバーレイのウィンドウを、クライアント領域が画面と一致するように配置する。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: WinUI 3 のウィンドウは DWM の枠（数ピクセル）を内側に持つため、<br/>
/// <see cref="AppWindow.MoveAndResize(RectInt32)"/> で画面と同じ大きさを指定すると、<br/>
/// <b>クライアント領域は枠の分だけ小さくなる</b>（実測 2026-10-17: 3840 × 3360 を指定して 3834 × 3354）。<br/>
/// 撮影した画像はクライアント領域いっぱいへ描くため、そのままでは画像が数ピクセル縮小され、<br/>
/// 画面の座標と一致しなくなる（枠も画面内に見える）。<br/>
/// <br/>
/// 本クラスは枠の太さを実測し、その分だけウィンドウを外側へ広げて<b>クライアント領域を指定の矩形に一致</b>させる。
/// </remarks>
public static class OverlayWindowPlacer
{
    /// <summary>ウィンドウのクライアント領域が指定の矩形と一致するよう、ウィンドウを配置する。</summary>
    /// <param name="appWindow">配置するウィンドウ。</param>
    /// <param name="target">クライアント領域に一致させたい矩形（画面物理ピクセル）。</param>
    /// <remarks>
    /// 枠の太さは環境（テーマ・DPI）で変わるため定数にせず、実際に配置して測る。<br/>
    /// 枠が無い場合は指定の矩形をそのまま使う。
    /// </remarks>
    public static void MoveClientArea(AppWindow appWindow, EsRectInt target)
    {
        ArgumentNullException.ThrowIfNull(appWindow);

        // まず素直に置いて、ウィンドウとクライアント領域の差（＝枠の太さ）を測る
        appWindow.MoveAndResize(new RectInt32(target.X, target.Y, target.Width, target.Height));

        var hwnd = Win32Interop.GetWindowFromWindowId(appWindow.Id);
        if (hwnd == IntPtr.Zero ||
            !GetWindowRect(hwnd, out var window) ||
            !GetClientRect(hwnd, out var client))
        {
            return;
        }

        var frameWidth = (window.Right - window.Left) - (client.Right - client.Left);
        var frameHeight = (window.Bottom - window.Top) - (client.Bottom - client.Top);
        if (frameWidth <= 0 && frameHeight <= 0)
        {
            return;
        }

        // 枠は左右・上下に均等に付くため、半分ずつ外側へ広げる
        appWindow.MoveAndResize(new RectInt32(
            target.X - (frameWidth / 2),
            target.Y - (frameHeight / 2),
            target.Width + frameWidth,
            target.Height + frameHeight));
    }

    /// <summary>ウィンドウの矩形（画面座標・画面物理ピクセル）を取得する。</summary>
    /// <param name="hwnd">対象のウィンドウ ハンドル。</param>
    /// <param name="rect">取得した矩形。</param>
    /// <returns>取得できた場合は true。</returns>
    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    /// <summary>ウィンドウのクライアント領域の大きさを取得する。</summary>
    /// <param name="hwnd">対象のウィンドウ ハンドル。</param>
    /// <param name="rect">取得した矩形（左上は常に 0）。</param>
    /// <returns>取得できた場合は true。</returns>
    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);

    /// <summary>Win32 の矩形（<c>RECT</c>）。</summary>
    private struct NativeRect
    {
        /// <summary>左端の座標。</summary>
        public int Left;

        /// <summary>上端の座標。</summary>
        public int Top;

        /// <summary>右端の座標。</summary>
        public int Right;

        /// <summary>下端の座標。</summary>
        public int Bottom;
    }
}
