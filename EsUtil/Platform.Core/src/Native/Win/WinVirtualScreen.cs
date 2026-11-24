using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>全モニターを包含する仮想スクリーンの矩形（画面物理ピクセル）の照会。</summary>
/// <remarks>
/// <b>なぜ <c>Platform.Core.Window</c> ではなく本層か</b>: ウィンドウの配置規則（純粋計算）は
/// <see cref="EsUtil.Platform.Core.Window.EsWindowPlacementMath"/> が持ち、<b>OS を呼ぶ照会だけ</b>を本層へ置く
/// （<c>Coord</c> / <c>Window</c> の型が Win32 を呼ぶと「OS 依存は <c>Native</c>」の規則から外れる）。<br/>
/// <b><c>DisplayArea.FindAll()</c> を使わない理由</b>: ウィンドウの表示前に列挙すると、WinRT の射影で
/// <c>InvalidCastException</c> になり起動できない。 <c>GetSystemMetrics</c> は
/// ウィンドウに依存せず、画面キャプチャと同じ値の出所になる。<br/>
/// <b>プライマリ モニタが (0,0) にない場合は負値</b>になる（仮想スクリーンの座標そのものである）。
/// </remarks>
public static class WinVirtualScreen
{
    /// <summary>全モニターを包含する仮想スクリーンの矩形（画面物理ピクセル）を取得する。</summary>
    /// <returns>仮想スクリーンの矩形。 取得できない場合は空の矩形。</returns>
    public static EsRectInt Bounds
    {
        get
        {
            var x = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
            var y = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
            var width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
            var height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
            return width <= 0 || height <= 0
                ? new EsRectInt(0, 0, 0, 0)
                : new EsRectInt(x, y, width, height);
        }
    }

    /// <summary>全モニターの矩形（画面物理ピクセル）を取得する。</summary>
    /// <returns>モニターごとの矩形。 列挙できない場合は空のリスト。</returns>
    /// <remarks>
    /// <b>モニター単位で撮影するために使う</b>: Windows.Graphics.Capture はモニター単位でしか撮れないため、
    /// 撮影の実装はモニターごとに撮って 1 枚へ合成する（[`docs/SPEC_API.md`](../../../docs/SPEC_API.md) の
    /// <c>VirtualScreenComposition</c>）。 矩形は <c>EnumDisplayMonitors</c> から取得する
    /// （<see cref="Bounds"/> の <c>GetSystemMetrics</c> と違い、モニターの区切りが分かる）。<br/>
    /// 並び順は OS の列挙順であり、座標順ではない。
    /// </remarks>
    public static IReadOnlyList<EsRectInt> Monitors
    {
        get
        {
            var bounds = new List<EsRectInt>();
            var previous = s_monitors;
            s_monitors = bounds;
            try
            {
                unsafe
                {
                    _ = PInvoke.EnumDisplayMonitors(default, null, &MonitorThunk, default);
                }
            }
            finally
            {
                s_monitors = previous;
            }

            return bounds;
        }
    }

    /// <summary>列挙中のモニター矩形の受け皿（コールバックは静的メソッドから呼ばれるため静的フィールドを使う）。</summary>
    /// <remarks>
    /// 入れ子の列挙に備えて、呼び出しごとに以前の値を保存して戻す（スレッド単位で競合しないとは限らないため、
    /// <see cref="Monitors"/> の読み取り中は他スレッドから列挙しない前提にしない。 収集先は取得のたびに作り直す）。
    /// </remarks>
    [ThreadStatic]
    private static List<EsRectInt>? s_monitors;

    /// <summary><c>EnumDisplayMonitors</c> のコールバックの実体。</summary>
    /// <param name="monitor">モニター ハンドル（未使用）。</param>
    /// <param name="hdc">モニターのデバイス コンテキスト（未使用）。</param>
    /// <param name="bounds">モニターの矩形へのポインター。</param>
    /// <param name="data">アプリケーション定義の値（未使用）。</param>
    /// <returns>列挙を継続する場合は true。</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe BOOL MonitorThunk(HMONITOR monitor, HDC hdc, RECT* bounds, LPARAM data)
    {
        _ = monitor;
        _ = hdc;
        _ = data;
        s_monitors?.Add(new EsRectInt(
            bounds->left,
            bounds->top,
            bounds->right - bounds->left,
            bounds->bottom - bounds->top));
        return true;
    }
}
