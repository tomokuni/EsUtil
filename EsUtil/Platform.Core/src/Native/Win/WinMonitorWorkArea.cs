using EsUtil.Platform.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>ウィンドウが載っているモニターの作業領域（タスク バーを除いた矩形）を照会する。</summary>
/// <remarks>
/// <b>なぜコアが持つか</b>: フローティング パネルを画面の外へ出さないためには、モニターごとの作業領域が要る。
/// UI フレームワークの公開 API はこれを統一した形で公開しない（WinForms の <c>Screen</c>、Avalonia の
/// <c>Screens</c>、WinUI 3 の <c>DisplayArea</c> はそれぞれ別の型であり、UI フレームワークを持たない版では
/// 自前の P/Invoke を書くことになる）。 <b>Win32 の照会を 1 箇所に集約する</b>。<br/>
/// <b>座標は画面物理ピクセル</b>である（DIP ではない。 表示倍率を掛けるのは呼び出し側である）。<br/>
/// <b>失敗を例外にしない</b>: 照会できない場合は <see langword="null"/> を返し、呼び出し側が位置をそのまま使う。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。
/// </remarks>
public static class WinMonitorWorkArea
{
    /// <summary>ウィンドウが載っているモニターの作業領域を取得する。</summary>
    /// <param name="windowHandle">ウィンドウのハンドル（0 は無効）。</param>
    /// <returns>作業領域（画面物理ピクセル）。 照会できない場合は <see langword="null"/>。</returns>
    /// <remarks>ウィンドウがどのモニターにも載っていない場合は最も近いモニターを使う（Windows の既定の解決）。</remarks>
    public static EsRectInt? GetWorkArea(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return null;
        }

        var monitor = PInvoke.MonitorFromWindow(
            new HWND(windowHandle), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (monitor.IsNull)
        {
            return null;
        }

        var info = default(MONITORINFO);
        info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>();
        if (!PInvoke.GetMonitorInfo(monitor, ref info))
        {
            return null;
        }

        var work = info.rcWork;
        return new EsRectInt(work.left, work.top, work.right - work.left, work.bottom - work.top);
    }
}
