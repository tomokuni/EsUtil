using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.WinUI3.Internal;

/// <summary>ウィンドウ（<c>HWND</c>）の解決と、画面物理座標 ↔ クライアント座標の変換を行う Win32 の入口。</summary>
/// <remarks>
/// <b>位置付け</b>: WinUI 3 の公開 API だけでは「画面物理座標」と「ウィンドウのクライアント座標」を相互変換できない<br/>
/// （XAML のルートは表示倍率とコンテンツ内の相対座標までしか与えない）ため、必要な Win32 API だけを本クラスが単一所有する。<br/>
/// <br/>
/// <b>宣言の仕方</b>: 安全でないコードの許可（<c>AllowUnsafeBlocks</c>）を必要としない <c>DllImport</c> を使い、<br/>
/// 読み込み先を System32 に固定する（<c>DefaultDllImportSearchPaths</c>。 探索順の脆弱性を作らない）。<br/>
/// <b>対応 OS</b>: 本プロジェクトの <c>SupportedOSPlatformVersion</c>（10.0.19041.0）で利用できる API だけを使う。<br/>
/// <b>呼び出し</b>: ウィンドウの生成後（XAML のルートが確定した後）に、UI スレッドから呼ぶ。
/// </remarks>
internal static class Win32Interop
{
    /// <summary>Win32 の <c>POINT</c> 構造体（クライアント座標または画面座標）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        /// <summary>X 座標。</summary>
        public int X;

        /// <summary>Y 座標。</summary>
        public int Y;
    }

    /// <summary>ウィンドウ ハンドルを解決する。</summary>
    /// <param name="windowId">XAML のルートが属するウィンドウの ID。</param>
    /// <param name="windowHandle">解決したウィンドウ ハンドル。 解決できない場合は <see cref="IntPtr.Zero"/>。</param>
    /// <returns>解決できた場合は true。</returns>
    /// <remarks>
    /// XAML のルート（<c>XamlRoot.ContentIslandEnvironment.AppWindowId</c>）から得た ID を使うため、<br/>
    /// ホストへウィンドウを渡してもらう必要がない（コントロールだけで現在のウィンドウを特定できる）。
    /// </remarks>
    public static bool TryGetWindowHandle(Microsoft.UI.WindowId windowId, out IntPtr windowHandle)
    {
        windowHandle = Microsoft.UI.Win32Interop.GetWindowFromWindowId(windowId);
        return windowHandle != IntPtr.Zero;
    }

    /// <summary>画面物理座標をクライアント座標（ウィンドウのクライアント領域の左上を原点とする物理ピクセル）へ変換する。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。</param>
    /// <param name="screenPoint">画面物理座標。</param>
    /// <returns>クライアント座標。 変換できない場合は <paramref name="screenPoint"/> をそのまま返す。</returns>
    /// <remarks>
    /// 変換に失敗した場合は入力をそのまま返す（失敗を原点で表さない。 位置がずれるだけで操作は続けられる）。
    /// </remarks>
    public static EsPointInt ScreenToClient(IntPtr windowHandle, EsPointInt screenPoint)
    {
        var point = new NativePoint { X = screenPoint.X, Y = screenPoint.Y };
        return ScreenToClientCore(windowHandle, ref point)
            ? new EsPointInt(point.X, point.Y)
            : screenPoint;
    }

    /// <summary>クライアント座標を画面物理座標へ変換する。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。</param>
    /// <param name="clientPoint">クライアント座標（物理ピクセル）。</param>
    /// <returns>画面物理座標。 変換できない場合は <paramref name="clientPoint"/> をそのまま返す。</returns>
    /// <remarks>変換に失敗した場合は入力をそのまま返す（<see cref="ScreenToClient"/> と同じ規則）。</remarks>
    public static EsPointInt ClientToScreen(IntPtr windowHandle, EsPointInt clientPoint)
    {
        var point = new NativePoint { X = clientPoint.X, Y = clientPoint.Y };
        return ClientToScreenCore(windowHandle, ref point)
            ? new EsPointInt(point.X, point.Y)
            : clientPoint;
    }

    /// <summary>ダブルクリックと見なす時間の上限（OS の設定値）を取得する。</summary>
    /// <returns>上限（ミリ秒）。</returns>
    /// <remarks>
    /// WinUI 3 のポインター イベントはクリック数を報告しないため、OS の設定値を閾値として使う<br/>
    /// （コントロールが固定値を決めると、OS の設定を変えた利用者と感覚が食い違う）。
    /// </remarks>
    public static long DoubleClickIntervalMilliseconds() => GetDoubleClickTime();

    /// <summary>画面物理座標をクライアント座標へ変換する（Win32 の <c>ScreenToClient</c>）。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。</param>
    /// <param name="point">変換する座標（変換後にクライアント座標へ書き換わる）。</param>
    /// <returns>成功した場合は true。</returns>
    [DllImport("user32.dll", EntryPoint = "ScreenToClient", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClientCore(IntPtr windowHandle, ref NativePoint point);

    /// <summary>クライアント座標を画面物理座標へ変換する（Win32 の <c>ClientToScreen</c>）。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。</param>
    /// <param name="point">変換する座標（変換後に画面物理座標へ書き換わる）。</param>
    /// <returns>成功した場合は true。</returns>
    [DllImport("user32.dll", EntryPoint = "ClientToScreen", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreenCore(IntPtr windowHandle, ref NativePoint point);

    /// <summary>ダブルクリックと見なす時間の上限を取得する（Win32 の <c>GetDoubleClickTime</c>）。</summary>
    /// <returns>上限（ミリ秒）。</returns>
    [DllImport("user32.dll", EntryPoint = "GetDoubleClickTime")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetDoubleClickTime();
}
