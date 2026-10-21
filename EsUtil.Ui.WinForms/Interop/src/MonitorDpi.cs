using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.Interop;

/// <summary>指定位置のモニターの DPI を照会する。</summary>
/// <remarks>
/// <b>なぜ本プロジェクトが持つか</b>: WinForms の公開 API は「モニター単位の DPI」を公開しない<br/>
/// （<see cref="Screen"/> は矩形とデバイス名のみ、<see cref="Control.DeviceDpi"/> はコントロールが属する<br/>
/// モニターの 1 値のみ）。 仮想スクリーン全体を 1 枚の画像として扱う撮影セッションは、<br/>
/// 選択矩形が属するモニターの表示倍率を位置から解決する必要がある（サイズラベルの表示倍率）。<br/>
/// <br/>
/// <b>DPI 非対応プロセスでの値</b>: プロセスが DPI 非対応の場合、OS はシステム DPI を返す<br/>
/// （モニターごとの実効 DPI にはならない。 DPI 対応の有効化はアプリの実行ファイルが行う）。<br/>
/// <b>照会できない場合</b>: 0 を返す（呼び出し側が代替値で解決する。 例外にしない）。
/// </remarks>
public static partial class MonitorDpi
{
    /// <summary>最も近いモニターを選ぶ指定（<c>MONITOR_DEFAULTTONEAREST</c>）。</summary>
    private const uint MonitorDefaultToNearest = 2;

    /// <summary>実効 DPI を求める指定（<c>MDT_EFFECTIVE_DPI</c>）。</summary>
    private const int MdtEffectiveDpi = 0;

    /// <summary>指定位置にあるモニターの DPI スケール係数を照会する。</summary>
    /// <param name="point">照会する位置（画面物理ピクセル）。</param>
    /// <returns>スケール係数（1.0 = 96 DPI）。 照会できない場合は 0（無効値。<see cref="DpiScaler.IsValidScaling"/> が false）。</returns>
    /// <remarks>
    /// <see cref="DpiScaler.ToScaling(double)"/> で DPI を係数へ換算する（換算規則はコアが単一所有する）。<br/>
    /// 戻り値を 0 とするのは「照会できなかった」を呼び出し側が判別できるようにするためである<br/>
    /// （1.0 を返すと 96 DPI のモニターと区別できない）。
    /// </remarks>
    public static double GetScaleForPoint(EsPointInt point)
    {
        var monitor = MonitorFromPoint(new NativePoint(point.X, point.Y), MonitorDefaultToNearest);
        if (monitor == 0)
        {
            return 0;
        }

        // 失敗時は 0 のままにする（dpiX を初期化して返す）
        return GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) == 0 && dpiX > 0
            ? DpiScaler.ToScaling(dpiX)
            : 0;
    }

    /// <summary>指定位置にあるモニターのハンドルを取得する（<c>MonitorFromPoint</c>）。</summary>
    /// <param name="point">照会する位置（画面物理ピクセル）。</param>
    /// <param name="flags">モニターの選択方法。</param>
    /// <returns>モニターのハンドル。 取得できない場合は 0。</returns>
    [LibraryImport("user32.dll", EntryPoint = "MonitorFromPoint")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint MonitorFromPoint(NativePoint point, uint flags);

    /// <summary>モニターの DPI を取得する（<c>GetDpiForMonitor</c>）。</summary>
    /// <param name="monitor">モニターのハンドル。</param>
    /// <param name="dpiType">取得する DPI の種類。</param>
    /// <param name="dpiX">横方向の DPI。</param>
    /// <param name="dpiY">縦方向の DPI。</param>
    /// <returns>成功した場合は 0（<c>S_OK</c>）。 それ以外は Win32 のエラー コード。</returns>
    [LibraryImport("shcore.dll", EntryPoint = "GetDpiForMonitor")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>Win32 の <c>POINT</c>（画面物理ピクセルの座標）。</summary>
    /// <remarks>
    /// P/Invoke が受け取れるのは<b>公開フィールドのみの構造体</b>であるため、<br/>
    /// コアの座標型（プロパティで公開する読み取り専用の値型）とは別に本型を用意する。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        /// <summary>X 座標（画面物理ピクセル）。</summary>
        public int X = x;

        /// <summary>Y 座標（画面物理ピクセル）。</summary>
        public int Y = y;
    }
}
