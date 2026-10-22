using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>指定位置のモニターの実効 DPI を照会する（Windows 実装）。</summary>
/// <remarks>
/// <b>なぜコアが持つか</b>: UI フレームワークの公開 API は「モニター単位の実効 DPI」を公開しない
/// （WinForms の <c>Screen</c> は矩形とデバイス名のみ、<c>Control.DeviceDpi</c> はコントロールが属する
/// モニターの 1 値のみ。 WinUI 3 / Avalonia / MewUI も同じ）。 撮影セッションは「選択矩形が属する
/// モニターの表示倍率」を位置から解決する必要があるため、<b>Win32 の照会を 1 箇所に集約する</b>
/// （UI 版ごとに複製すると解決結果が食い違う）。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。<br/>
/// <b>DPI 非対応プロセスでの値</b>: プロセスが DPI 非対応の場合、OS はシステム DPI を返す
/// （モニターごとの実効 DPI にはならない。 DPI 対応の有効化はアプリの実行ファイルが行う）。<br/>
/// <b>失敗を例外にしない</b>: 照会できない場合（モニターが無い・API が失敗した）は <see langword="null"/> を返す。
/// 呼び出し側が代替値で解決する（撮影は続けられる）。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public static class WinMonitorDpi
{
    /// <summary>標準の DPI（96 DPI = スケール 1.0）。</summary>
    public const uint StandardDpi = 96;

    /// <summary>指定位置を含むモニターの実効 DPI を取得する。</summary>
    /// <param name="screenPoint">照会する位置（画面物理座標）。</param>
    /// <returns>実効 DPI（96 = 100%）。 照会できない場合は <see langword="null"/>。</returns>
    /// <remarks>最も近いモニターを対象にする（ウィンドウが複数のモニターに跨っていても座標から一意に決まる）。</remarks>
    public static uint? GetEffectiveDpi(EsPointInt screenPoint)
    {
        var monitor = PInvoke.MonitorFromPoint(
            new System.Drawing.Point(screenPoint.X, screenPoint.Y), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (monitor.IsNull)
        {
            return null;
        }

        var hr = PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _);
        return hr.Succeeded && dpiX > 0 ? dpiX : null;
    }
}
