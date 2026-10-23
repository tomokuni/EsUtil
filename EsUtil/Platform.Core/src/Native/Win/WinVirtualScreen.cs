using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>全モニターを包含する仮想スクリーンの矩形（画面物理ピクセル）の照会。</summary>
/// <remarks>
/// <b>なぜ <c>Platform.Core.Window</c> ではなく本層か</b>: ウィンドウの配置規則（純粋計算）は
/// <see cref="EsUtil.Platform.Core.Window.EsWindowPlacementMath"/> が持ち、<b>OS を呼ぶ照会だけ</b>を本層へ置く
/// （<c>Coord</c> / <c>Window</c> の型が Win32 を呼ぶと「OS 依存は <c>Native</c>」の規則から外れる）。<br/>
/// <b><c>DisplayArea.FindAll()</c> を使わない理由</b>: ウィンドウの表示前に列挙すると、WinRT の射影で
/// <c>InvalidCastException</c> になり起動できない（実測 2026-10-17）。 <c>GetSystemMetrics</c> は
/// ウィンドウに依存せず、画面キャプチャと同じ値の出所になる。<br/>
/// <b>プライマリ モニタが (0,0) にない場合は負値</b>になる（仮想スクリーンの座標そのものである）。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
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
}
