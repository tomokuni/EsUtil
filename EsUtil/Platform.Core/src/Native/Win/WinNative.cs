using System.Runtime.InteropServices;

// 本アセンブリの P/Invoke はシステム ディレクトリ（System32）だけを探索する。
// 探索順序を固定しないと、アプリと同じフォルダーへ置かれた同名の偽 DLL を読み込む余地が残る（CA5392）。
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>プラットフォーム機能に必要な Win32 API の P/Invoke 宣言・構造体・デリゲート・定数を集約する。</summary>
/// <remarks>
/// カーソル操作（<c>GetCursorPos</c> / <c>SetCursorPos</c> / <c>ShowCursor</c>）、<br/>
/// カーソル上書き抑止（WndProc サブクラス化: <c>SetWindowLongPtrW</c> / <c>CallWindowProcW</c>）、<br/>
/// 要素（ウィンドウ・スクリーン）検出（<c>EnumWindows</c> / <c>EnumDisplayMonitors</c> 等）、<br/>
/// 画面キャプチャ（<c>GetDC</c> / <c>CreateDIBSection</c> / <c>BitBlt</c> / <c>GetSystemMetrics</c> 等）、<br/>
/// ネイティブウィンドウ操作（<c>SetWindowPos</c> / <c>ShowWindow</c> / <c>PostMessage</c> 等）、<br/>
/// キャプチャ除外（<c>SetWindowDisplayAffinity</c>）、グローバル ホットキー（<c>RegisterHotKey</c>）、<br/>
/// GDI+ による画像のエンコード・デコード（<c>gdiplus.dll</c>）に必要な Win32 API を集約する（Windows 依存の最小実装・DRY）。<br/>
/// <b>宣言は DLL ごとのファイルが持つ</b>: <c>WinNative.User32.cs</c> / <c>WinNative.Gdi32.cs</c> / <c>WinNative.DwmApi.cs</c> /
/// <c>WinNative.Kernel32.cs</c> / <c>WinNative.HotKey.cs</c> / <c>WinNative.GdiPlus.cs</c>
/// （<c>partial</c> のため型は 1 つのまま）。 本ファイルは複数の DLL で共有する構造体とヘルパーを持つ。<br/>
/// <b>本クラスの利用者は本アセンブリの <c>Win*</c> 実装だけである</b>（ホストは <c>Native</c> の抽象と
/// <c>Imaging</c> の画像型だけを使う。 <c>Win*</c> 実装の生成はホストが行う）。
/// </remarks>
internal static partial class WinNative
{
    /// <summary><see cref="RECT"/> 構造体のマーシャリングサイズ（バイト）。</summary>
    internal static readonly int RectSize = Marshal.SizeOf<RECT>();

    // ============================================================
    // 構造体
    // ============================================================

    /// <summary>Win32 RECT 構造体（矩形）。</summary>
    /// <param name="left">左端。</param>
    /// <param name="top">上端。</param>
    /// <param name="right">右端。</param>
    /// <param name="bottom">下端。</param>
    /// <remarks>P/Invoke のマーシャリング用にレイアウトを定義する。</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct RECT(int left, int top, int right, int bottom)
    {
        /// <summary>左端。</summary>
        public int Left { get; } = left;

        /// <summary>上端。</summary>
        public int Top { get; } = top;

        /// <summary>右端。</summary>
        public int Right { get; } = right;

        /// <summary>下端。</summary>
        public int Bottom { get; } = bottom;

        /// <summary>幅（Right - Left）。</summary>
        public int Width => Right - Left;

        /// <summary>高さ（Bottom - Top）。</summary>
        public int Height => Bottom - Top;
    }

    /// <summary>Win32 POINT 構造体（スクリーン座標）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <remarks>P/Invoke のマーシャリング用にレイアウトを定義する。</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct POINT(int x, int y)
    {
        /// <summary>X 座標。</summary>
        public int X { get; } = x;

        /// <summary>Y 座標。</summary>
        public int Y { get; } = y;
    }

    /// <summary>Win32 CURSORINFO 構造体（カーソルの表示状態・位置）。</summary>
    /// <remarks>
    /// <see cref="GetCursorInfo"/> の出力先。 表示状態（<see cref="flags"/> の <see cref="CURSOR_SHOWING"/>）を<br/>
    /// 取得して、Win32 の表示カウンタと <see cref="WinCursorService"/> の状態追跡を同期するために使用する。<br/>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct CURSORINFO
    {
        /// <summary>構造体のサイズ（cbSize）。 呼び出し前に設定する。</summary>
        public int cbSize;

        /// <summary>カーソルの表示状態（<see cref="CURSOR_SHOWING"/> とのビット演算で判定）。</summary>
        public int flags;

        /// <summary>カーソルのハンドル。</summary>
        public IntPtr hCursor;

        /// <summary>カーソルの画面物理座標。</summary>
        public POINT ptScreenPos;
    }

    /// <summary>Win32 BITMAPINFOHEADER 構造体（DIB の形式と寸法）。</summary>
    /// <remarks>
    /// P/Invoke のマーシャリング用にレイアウトを定義する。<br/>
    /// <c>biHeight</c> を負値にするとトップダウン DIB になり、行の並びが上から下（画像の並びと一致）になる。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        /// <summary>構造体のサイズ（biSize）。 呼び出し前に設定する。</summary>
        public uint biSize;

        /// <summary>幅（ピクセル）。</summary>
        public int biWidth;

        /// <summary>高さ（ピクセル）。 負値はトップダウン（上から下の並び）。</summary>
        public int biHeight;

        /// <summary>プレーンの数（常に 1）。</summary>
        public ushort biPlanes;

        /// <summary>1 ピクセルあたりのビット数（32 を指定する）。</summary>
        public ushort biBitCount;

        /// <summary>圧縮方式（<see cref="BI_RGB"/> = 無圧縮）。</summary>
        public uint biCompression;

        /// <summary>画像データのサイズ（<see cref="BI_RGB"/> では 0 でよい）。</summary>
        public uint biSizeImage;

        /// <summary>水平方向の解像度（メートルあたりのピクセル）。</summary>
        public int biXPelsPerMeter;

        /// <summary>垂直方向の解像度（メートルあたりのピクセル）。</summary>
        public int biYPelsPerMeter;

        /// <summary>パレットの色数（<see cref="BI_RGB"/> では 0）。</summary>
        public uint biClrUsed;

        /// <summary>重要な色数（<see cref="BI_RGB"/> では 0）。</summary>
        public uint biClrImportant;
    }

    /// <summary>Win32 BITMAPINFO 構造体（<see cref="BITMAPINFOHEADER"/> と色テーブル）。</summary>
    /// <remarks>
    /// P/Invoke のマーシャリング用にレイアウトを定義する。 32bpp では色テーブルを使わないため宣言しない。<br/>
    /// 色テーブルを持つ形式（8bpp 等）で使う場合は、可変長部分を続けて確保する必要がある。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFO
    {
        /// <summary>画像の形式と寸法。</summary>
        public BITMAPINFOHEADER bmiHeader;
    }

    // ============================================================
    // ヘルパー
    // ============================================================

    /// <summary>仮想スクリーン（全モニタを包含するバウンディングボックス）の境界を取得する。</summary>
    /// <returns>仮想スクリーンの境界（画面物理座標）。 プライマリモニタが (0,0) にない場合は負値になり得る。</returns>
    /// <remarks>
    /// Win32 の仮想スクリーン全体を 1 回のキャプチャ（CopyFromScreen / BitBlt）で取得するための基準境界。<br/>
    /// 複数モニタ環境でも全モニタを包含する矩形を返す。<br/>
    /// </remarks>
    internal static (int X, int Y, int Width, int Height) GetVirtualScreenBounds()
        => (GetSystemMetrics(SM_XVIRTUALSCREEN),
            GetSystemMetrics(SM_YVIRTUALSCREEN),
            GetSystemMetrics(SM_CXVIRTUALSCREEN),
            GetSystemMetrics(SM_CYVIRTUALSCREEN));

    /// <summary>ウィンドウの可視矩形領域を取得する（最大化はワークエリア・通常は DWM 影除外）。</summary>
    /// <param name="hWnd">対象ウィンドウハンドル。</param>
    /// <param name="rect">取得した可視矩形。</param>
    /// <returns>取得できた場合は true。</returns>
    /// <remarks>
    /// 最大化ウィンドウには影がなく、<c>DWMWA_EXTENDED_FRAME_BOUNDS</c> は左右に 8px の内部マージンを返す<br/>
    /// （Windows 10 の既知の挙動）ため、<c>GetWindowRect</c>（ワークエリア = タスクバーを除く最大化領域）を使用する。<br/>
    /// 通常ウィンドウは <c>DWMWA_EXTENDED_FRAME_BOUNDS</c> で影を除外した可視領域を取得し、<br/>
    /// 失敗時は <c>GetWindowRect</c> にフォールバックする。<br/>
    /// <see cref="WinElementFinder"/> / <see cref="WinCaptureService"/> で共用する（単一所有）。
    /// </remarks>
    internal static bool TryGetVisibleRect(IntPtr hWnd, out RECT rect)
    {
        rect = default;
        if (hWnd == IntPtr.Zero) return false;

        // 最大化ウィンドウ: 影がないため GetWindowRect（ワークエリア）を使用する
        // （DWMWA_EXTENDED_FRAME_BOUNDS は最大化時に 8px の内部マージンを返す既知の挙動の回避）
        if (IsZoomed(hWnd))
        {
            return GetWindowRect(hWnd, out rect);
        }

        // 通常ウィンドウ: DWM 影除外（DWMWA_EXTENDED_FRAME_BOUNDS）で可視領域を取得する
        RECT visibleRect = default;
        var hr = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS,
            ref visibleRect, RectSize);
        if (hr >= 0 && visibleRect.Left < visibleRect.Right && visibleRect.Top < visibleRect.Bottom)
        {
            rect = visibleRect;
            return true;
        }

        // フォールバック: GetWindowRect
        return GetWindowRect(hWnd, out rect);
    }
}
