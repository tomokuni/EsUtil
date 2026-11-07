using System.ComponentModel;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
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
/// 本クラスは枠の太さを実測し、その分だけウィンドウを外側へ広げて<b>クライアント領域を指定の矩形に一致</b>させる。<br/>
/// <b>枠の計測はコアが単一所有する</b>（<see cref="IWindowFrameMetrics.GetSize"/> の既定実装は <see cref="WinWindowFrame"/>。 UI 層は Win32 を直接呼ばない）。
/// </remarks>
public static class OverlayWindowPlacer
{
    /// <summary>ウィンドウのクライアント領域が指定の矩形と一致するよう、ウィンドウを配置する。</summary>
    /// <param name="appWindow">配置するウィンドウ。</param>
    /// <param name="target">クライアント領域に一致させたい矩形（画面物理ピクセル）。</param>
    /// <param name="windowFrame">枠の計測先。 null の場合は Windows の既定実装を使う。</param>
    /// <remarks>
    /// 枠の太さは環境（テーマ・DPI）で変わるため定数にせず、実際に配置して測る。<br/>
    /// 枠が無い場合と、枠を測れない場合は指定の矩形をそのまま使う（例外にしない。配置は続行できる）。
    /// </remarks>
    public static void MoveClientArea(AppWindow appWindow, EsRectInt target, IWindowFrameMetrics? windowFrame = null)
    {
        ArgumentNullException.ThrowIfNull(appWindow);

        // まず素直に置いて、ウィンドウとクライアント領域の差（＝枠の太さ）を測る
        appWindow.MoveAndResize(new RectInt32(target.X, target.Y, target.Width, target.Height));

        var hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(appWindow.Id);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        (int Width, int Height) frame;
        try
        {
            frame = (windowFrame ?? new WinWindowFrame()).GetSize(EsWindow.FromHandle(hwnd));
        }
        catch (Win32Exception)
        {
            // 測れない場合は指定の矩形のままにする（枠の分だけ小さくなるが、配置そのものは成立する）
            return;
        }

        if (frame.Width <= 0 && frame.Height <= 0)
        {
            return;
        }

        // 枠は左右・上下に均等に付くため、半分ずつ外側へ広げる
        appWindow.MoveAndResize(new RectInt32(
            target.X - (frame.Width / 2),
            target.Y - (frame.Height / 2),
            target.Width + frame.Width,
            target.Height + frame.Height));
    }
}
