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
/// 画面/ウィンドウキャプチャ（<c>PrintWindow</c> / <c>GetSystemMetrics</c> 等）、<br/>
/// ネイティブウィンドウ操作（<c>SetWindowPos</c> / <c>ShowWindow</c> / <c>PostMessage</c> 等）、<br/>
/// キャプチャ除外（<c>SetWindowDisplayAffinity</c>）に必要な Win32 API を集約する（Windows 依存の最小実装・DRY）。
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
    // デリゲート
    // ============================================================

    /// <summary>WndProc サブクラス化用のウィンドウプロシージャ。</summary>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>EnumWindows のコールバック。 true を返すと列挙を継続する。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lParam">アプリケーション定義の値。</param>
    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>EnumDisplayMonitors のコールバック。 true を返すと列挙を継続する。</summary>
    /// <param name="hMonitor">モニターハンドル。</param>
    /// <param name="hdcMonitor">モニターのデバイスコンテキスト。</param>
    /// <param name="lprcMonitor">モニターの矩形（画面物理座標）へのポインター。</param>
    /// <param name="dwData">アプリケーション定義の値。</param>
    internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    // ============================================================
    // 定数
    // ============================================================

    // --- カーソル ---
    /// <summary>WM_SETCURSOR: OS がカーソル形状を上書きしようとしたときに送られるメッセージ。</summary>
    internal const uint WM_SETCURSOR = 0x0020;

    /// <summary>GWLP_WNDPROC: ウィンドウプロシージャを設定する <c>SetWindowLongPtr</c> のインデックス。</summary>
    internal const int GWLP_WNDPROC = -4;

    /// <summary>HTCLIENT: クライアント領域内のヒットテスト結果。</summary>
    internal const int HTCLIENT = 1;

    /// <summary>CURSORINFO.flags: カーソルが表示されていることを示すフラグ。</summary>
    internal const int CURSOR_SHOWING = 0x00000001;

    // --- DWM 影除外 ---
    /// <summary>DWM 拡張フレーム境界（影を除外した可視矩形）の属性 ID。</summary>
    internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // --- 仮想スクリーン（全モニタのバウンディングボックス） ---
    /// <summary>仮想スクリーン左端のメトリクス。</summary>
    internal const int SM_XVIRTUALSCREEN = 76;

    /// <summary>仮想スクリーン上端のメトリクス。</summary>
    internal const int SM_YVIRTUALSCREEN = 77;

    /// <summary>仮想スクリーン幅のメトリクス。</summary>
    internal const int SM_CXVIRTUALSCREEN = 78;

    /// <summary>仮想スクリーン高さのメトリクス。</summary>
    internal const int SM_CYVIRTUALSCREEN = 79;

    // --- ウィンドウスタイル（Alt+Tab 非表示用） ---
    /// <summary>拡張ウィンドウスタイルを取得/設定するオフセット。</summary>
    internal const int GWL_EXSTYLE = -20;

    /// <summary>タスクスイッチャー非表示の拡張スタイル。</summary>
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    // --- ウォッチドッグ（UI ハング時の救出用） ---
    /// <summary>ウィンドウクローズメッセージ。</summary>
    internal const uint WM_CLOSE = 0x0010;

    /// <summary>ウィンドウ非表示コマンド。</summary>
    internal const int SW_HIDE = 0;

    /// <summary>最前面解除用の特別なハンドル値。</summary>
    internal static readonly IntPtr HWND_NOTOPMOST = new(-2);

    /// <summary>サイズ変更なしの SetWindowPos フラグ。</summary>
    internal const uint SWP_NOSIZE = 0x0001;

    /// <summary>移動なしの SetWindowPos フラグ。</summary>
    internal const uint SWP_NOMOVE = 0x0002;

    /// <summary>アクティブ化なしの SetWindowPos フラグ。</summary>
    internal const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>Z オーダーを変更しない SetWindowPos フラグ。</summary>
    internal const uint SWP_NOZORDER = 0x0004;

    /// <summary>ウィンドウを所有スレッドのメッセージキューに同期させない SetWindowPos フラグ。</summary>
    internal const uint SWP_ASYNCWINDOWPOS = 0x4000;

    // --- キャプチャ除外（SetWindowDisplayAffinity） ---
    /// <summary>キャプチャ除外なし（通常状態）。</summary>
    internal const int WDA_NONE = 0x00000000;

    /// <summary>ウィンドウを全キャプチャAPI（GDI / DXGI）から除外する（Windows 10 2004+）。</summary>
    internal const int WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    // --- 画面キャプチャ（DIB / BitBlt） ---
    /// <summary>無圧縮（BI_RGB）。 DIB の既定の圧縮方式。</summary>
    internal const uint BI_RGB = 0;

    /// <summary>パレットを RGB の 3 成分で指定する（DIB_RGB_COLORS）。</summary>
    internal const uint DIB_RGB_COLORS = 0;

    /// <summary>コピー元をそのままコピーするラスタ オペレーション（SRCCOPY）。</summary>
    internal const uint SRCCOPY = 0x00CC0020;

    /// <summary>重ね合わせウィンドウ（レイヤード ウィンドウ）も含めてコピーする BitBlt の追加フラグ。</summary>
    internal const uint CAPTUREBLT = 0x40000000;

    // ============================================================
    // user32.dll
    // ============================================================

    /// <summary>現在のカーソル位置を取得する（user32.GetCursorPos）。</summary>
    /// <param name="lpPoint">カーソル位置の出力先。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out POINT lpPoint);

    /// <summary>カーソルを指定位置に移動する（user32.SetCursorPos）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("user32.dll", EntryPoint = "SetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetCursorPos(int x, int y);

    /// <summary>マウスポインターの表示/非表示を切り替える（user32.ShowCursor）。</summary>
    /// <param name="bShow">表示する場合は <see langword="true"/>。</param>
    /// <returns>表示カウンタ。</returns>
    [LibraryImport("user32.dll", EntryPoint = "ShowCursor")]
    internal static partial int ShowCursor([MarshalAs(UnmanagedType.Bool)] bool bShow);

    /// <summary>カーソルの表示状態・位置を取得する（user32.GetCursorInfo）。</summary>
    /// <param name="pci">カーソル情報の出力先。 cbSize を事前に設定する。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 表示状態（<see cref="CURSORINFO.flags"/> の <see cref="CURSOR_SHOWING"/>）を取得して、<br/>
    /// Win32 の表示カウンタと <see cref="WinCursorService"/> の状態追跡を同期するために使用する。<br/>
    /// </remarks>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorInfo(ref CURSORINFO pci);

    /// <summary>指定したウィンドウの情報を取得/設定する（user32.SetWindowLongPtrW）。 WndProc サブクラス化とスタイル設定に使用する。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="nIndex">設定する情報のインデックス（<see cref="GWLP_WNDPROC"/> / <see cref="GWL_EXSTYLE"/> 等）。</param>
    /// <param name="dwNewLong">設定する値。</param>
    /// <returns>設定前の値。</returns>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>指定したウィンドウの情報を取得する（user32.GetWindowLongPtrW）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="nIndex">取得する情報のインデックス（<see cref="GWL_EXSTYLE"/> 等）。</param>
    /// <returns>取得した値。</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    /// <summary>元の WndProc にメッセージを委譲する（user32.CallWindowProcW）。</summary>
    /// <param name="lpPrevWndFunc">元の WndProc アドレス。</param>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="msg">ウィンドウメッセージ。</param>
    /// <param name="wParam">WPARAM。</param>
    /// <param name="lParam">LPARAM。</param>
    /// <returns>ウィンドウプロシージャの処理結果。</returns>
    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW", SetLastError = true)]
    internal static partial IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>全トップレベルウィンドウを列挙する（user32.EnumWindows）。</summary>
    /// <param name="lpEnumFunc">列挙コールバック。</param>
    /// <param name="lParam">アプリケーション定義の値。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    /// <summary>ウィンドウの外接矩形を取得する（user32.GetWindowRect）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lpRect">矩形の出力先。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>ウィンドウが最大化されているかを判定する（user32.IsZoomed）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsZoomed(IntPtr hWnd);

    /// <summary>ウィンドウの可視状態を判定する（user32.IsWindowVisible）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(IntPtr hWnd);

    /// <summary>ウィンドウが入力を受け付け可能かを判定する（user32.IsWindowEnabled）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowEnabled(IntPtr hWnd);

    /// <summary>ウィンドウが最小化されているかを判定する（user32.IsIconic）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(IntPtr hWnd);

    /// <summary>ウィンドウのクラス名を取得する（user32.GetClassNameW）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lpClassName">クラス名の出力先バッファ。</param>
    /// <param name="nMaxCount">バッファサイズ。</param>
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GetClassName(IntPtr hWnd, char[] lpClassName, int nMaxCount);

    /// <summary>ウィンドウタイトルの文字列長を取得する（user32.GetWindowTextLengthW）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    internal static partial int GetWindowTextLengthW(IntPtr hWnd);

    /// <summary>ウィンドウタイトルを取得する（user32.GetWindowTextW）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lpString">タイトルの出力先バッファ。</param>
    /// <param name="nMaxCount">バッファサイズ。</param>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    /// <summary>ウィンドウを所有するプロセス ID を取得する（user32.GetWindowThreadProcessId）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="processId">プロセス ID の出力先。</param>
    /// <returns>ウィンドウを作成したスレッド ID。</returns>
    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>全ディスプレイモニターを列挙する（user32.EnumDisplayMonitors）。</summary>
    /// <param name="hdc">デバイスコンテキスト（NULL = 全モニター）。</param>
    /// <param name="lprcClip">クリップ矩形（NULL = クリップなし）。</param>
    /// <param name="lpfnEnum">列挙コールバック。</param>
    /// <param name="dwData">アプリケーション定義の値。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    /// <summary>指定されたシステムメトリクス（SM_ 定数）を取得する（user32.GetSystemMetrics）。</summary>
    /// <param name="nIndex">取得するシステムメトリクスのインデックス。</param>
    /// <returns>システムメトリクスの値。</returns>
    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int nIndex);

    /// <summary>ウィンドウの位置・サイズ・Z オーダーを変更する（user32.SetWindowPos）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="hWndInsertAfter">Z オーダー位置（<see cref="HWND_NOTOPMOST"/> 等）。</param>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="cx">幅。</param>
    /// <param name="cy">高さ。</param>
    /// <param name="uFlags">フラグ（<see cref="SWP_NOMOVE"/> 等）。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>ウィンドウの表示状態を変更する（user32.ShowWindow）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="nCmdShow">表示コマンド（<see cref="SW_HIDE"/> 等）。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>ウィンドウへメッセージを送信する（user32.PostMessage）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="msg">メッセージ（<see cref="WM_CLOSE"/> 等）。</param>
    /// <param name="wParam">WPARAM。</param>
    /// <param name="lParam">LPARAM。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>ウィンドウのキャプチャ除外設定を変更する（user32.SetWindowDisplayAffinity）。</summary>
    /// <param name="hWnd">対象ウィンドウハンドル。</param>
    /// <param name="dwAffinity">除外設定（<see cref="WDA_NONE"/> / <see cref="WDA_EXCLUDEFROMCAPTURE"/>）。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <see cref="WDA_EXCLUDEFROMCAPTURE"/> を設定すると、そのウィンドウは画面キャプチャ（CopyFromScreen / DXGI 等）に
    /// 一切写らなくなる（フレームタイミングに依存しない決定的な除外）。<br/>
    /// キャプチャ前に設定し、キャプチャ後に <see cref="WDA_NONE"/> へ戻す。<br/>
    /// Windows 10 2004（build 19041）以降で利用可能。 それ以前では失敗（false）を返す。<br/>
    /// </remarks>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowDisplayAffinity(IntPtr hWnd, int dwAffinity);

    // ============================================================
    // kernel32.dll
    // ============================================================

    /// <summary>現在のプロセス ID を取得する（kernel32.GetCurrentProcessId）。</summary>
    [LibraryImport("kernel32.dll")]
    internal static partial uint GetCurrentProcessId();

    // ============================================================
    // user32.dll / gdi32.dll（画面キャプチャ）
    // ============================================================

    /// <summary>デスクトップ全体のデバイス コンテキストを取得する（user32.GetDC）。</summary>
    /// <param name="hWnd">対象ウィンドウ。 <see cref="IntPtr.Zero"/> は画面全体。</param>
    /// <returns>デバイス コンテキスト。 失敗した場合は <see cref="IntPtr.Zero"/>。</returns>
    /// <remarks>取得したコンテキストは <see cref="ReleaseDC"/> で解放する。</remarks>
    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial IntPtr GetDC(IntPtr hWnd);

    /// <summary>デバイス コンテキストを解放する（user32.ReleaseDC）。</summary>
    /// <param name="hWnd">対象ウィンドウ（<see cref="GetDC"/> に渡した値）。</param>
    /// <param name="hDC">解放するデバイス コンテキスト。</param>
    /// <returns>解放した場合は 1。</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    /// <summary>指定されたデバイス コンテキストと互換のメモリ デバイス コンテキストを作成する（gdi32.CreateCompatibleDC）。</summary>
    /// <param name="hdc">互換の基準にするデバイス コンテキスト。</param>
    /// <returns>作成されたメモリ デバイス コンテキスト。 失敗した場合は <see cref="IntPtr.Zero"/>。</returns>
    /// <remarks>不要になった時点で <see cref="DeleteDC"/> で削除する。</remarks>
    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial IntPtr CreateCompatibleDC(IntPtr hdc);

    /// <summary>メモリ デバイス コンテキストを削除する（gdi32.DeleteDC）。</summary>
    /// <param name="hdc">削除するデバイス コンテキスト。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDC(IntPtr hdc);

    /// <summary>DIB セクションを作成し、ピクセルを書き込めるメモリを確保する（gdi32.CreateDIBSection）。</summary>
    /// <param name="hdc">互換の基準にするデバイス コンテキスト。</param>
    /// <param name="pbmi">作成する DIB の形式。</param>
    /// <param name="usage">パレットの指定（<see cref="DIB_RGB_COLORS"/>）。</param>
    /// <param name="ppvBits">ピクセル データへのポインターの出力先。</param>
    /// <param name="hSection">ファイル マッピング オブジェクト（0 でよい）。</param>
    /// <param name="offset">セクション内のオフセット（0 でよい）。</param>
    /// <returns>作成されたビットマップ。 失敗した場合は <see cref="IntPtr.Zero"/>。</returns>
    /// <remarks>
    /// ピクセルは 0 で初期化される。 32bpp（<see cref="BI_RGB"/>）では 1 行のバイト数が <c>幅 × 4</c> になり、
    /// コアの画像と同じ並び（BGRA・上から下・パディングなし）になる。<br/>
    /// <c>biHeight</c> を負値にするとトップダウン DIB になる。
    /// </remarks>
    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial IntPtr CreateDIBSection(
        IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

    /// <summary>デバイス コンテキストへオブジェクトを選択する（gdi32.SelectObject）。</summary>
    /// <param name="hdc">対象のデバイス コンテキスト。</param>
    /// <param name="h">選択するオブジェクト（ビットマップ等）。</param>
    /// <returns>置き換えられた以前のオブジェクト。 失敗した場合は <see cref="IntPtr.Zero"/>。</returns>
    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial IntPtr SelectObject(IntPtr hdc, IntPtr h);

    /// <summary>GDI オブジェクトを削除する（gdi32.DeleteObject）。</summary>
    /// <param name="ho">削除するオブジェクト。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(IntPtr ho);

    /// <summary>ビット ブロック転送でピクセルをコピーする（gdi32.BitBlt）。</summary>
    /// <param name="hdcDest">コピー先のデバイス コンテキスト。</param>
    /// <param name="xDest">コピー先の X 座標。</param>
    /// <param name="yDest">コピー先の Y 座標。</param>
    /// <param name="width">コピーする幅。</param>
    /// <param name="height">コピーする高さ。</param>
    /// <param name="hdcSrc">コピー元のデバイス コンテキスト。</param>
    /// <param name="xSrc">コピー元の X 座標（画面物理座標。 負値は仮想スクリーンの左端）。</param>
    /// <param name="ySrc">コピー元の Y 座標（画面物理座標。 負値は仮想スクリーンの上端）。</param>
    /// <param name="rop">ラスタ オペレーション（<see cref="SRCCOPY"/> 等）。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 同期コールであり、DWM の応答が止まるとブロックする可能性がある（呼び出し側がタイムアウトで保護する）。<br/>
    /// 画面のピクセルは GDI の仕様でアルファ値を持たない（書き込まれない）ため、呼び出し側が不透明へ揃える。<br/>
    /// <b>注意</b>: GDI+ の <c>CopyFromScreen</c> と違い、<c>CAPTUREBLT</c> を付けない限りレイヤード ウィンドウは写らない。
    /// </remarks>
    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BitBlt(
        IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    // ============================================================
    // dwmapi.dll
    // ============================================================

    /// <summary>DWM のウィンドウ属性を取得する（dwmapi.DwmGetWindowAttribute）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="dwAttribute">取得する属性。</param>
    /// <param name="pvAttribute">属性値の出力先。</param>
    /// <param name="cbAttribute">属性値のサイズ。</param>
    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, ref RECT pvAttribute, int cbAttribute);

    /// <summary>DWM（デスクトップ合成マネージャー）が現在のフレームの合成を完了するまでブロックする（dwmapi.DwmFlush）。</summary>
    /// <returns>成功した場合は 0（S_OK）。</returns>
    /// <remarks>
    /// ウィンドウの表示・非表示・サイズ変更後に呼ぶと、その変更が画面へ反映されたことを保証できる。<br/>
    /// キャプチャ前に使用して、非表示状態の残像が写り込むのを防ぐ（DWM の合成完了を待つ）。<br/>
    /// </remarks>
    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmFlush();

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

    // ============================================================
    // 定数（ホットキー）
    // ============================================================

    /// <summary>WM_HOTKEY: グローバルホットキーが押下されたときに送られるメッセージ。</summary>
    internal const uint WM_HOTKEY = 0x0312;

    // ============================================================
    // user32.dll — ホットキー
    // ============================================================

    /// <summary>グローバルホットキーを登録する（user32.RegisterHotKey）。</summary>
    /// <param name="hWnd">ホットキー通知を受け取るウィンドウのハンドル。</param>
    /// <param name="id">ホットキーの識別子。</param>
    /// <param name="fsModifiers">修飾キーのフラグ（MOD_ALT / MOD_CONTROL / MOD_SHIFT / MOD_WIN）。</param>
    /// <param name="vk">仮想キーコード。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, uint vk);

    /// <summary>グローバルホットキーを解除する（user32.UnregisterHotKey）。</summary>
    /// <param name="hWnd">ホットキーを登録したウィンドウのハンドル。</param>
    /// <param name="id">解除するホットキーの識別子。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>UI 非依存の <see cref="EsKey"/> を Win32 仮想キーコードへ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>
    /// 対応する Win32 仮想キーコード。 対応するコードが無いキー（<c>LineFeed</c> / <c>System</c> / <c>DeadCharProcessed</c> /<br/>
    /// <c>Fn*</c> / メディアの一部（<c>MediaHome</c> 等））は <c>0</c>（<c>RegisterHotKey</c> が失敗する値 = 登録できない）。
    /// </returns>
    /// <remarks>
    /// <c>RegisterHotKey</c> の vk パラメータに渡すために使用する。<br/>
    /// <b>対応表に無いキーへ序数（<c>(uint)key</c>）を返さない</b>: 序数は仮想キーコードと別体系であり、<br/>
    /// 無言で<b>別のキーが登録される</b>（実測: 以前は <c>PrintScreen</c> の序数 89 = <c>0x59</c> = <c>Y</c> が登録され、<br/>
    /// <c>Sleep</c> の序数 65 = <c>0x41</c> = <c>A</c> が登録されていた）。 未対応は <c>0</c> として失敗させる（呼び出し側が記録する）。
    /// </remarks>
    internal static uint KeyToWin32VirtualKey(EsKey key) => key switch
    {
        EsKey.None => 0x00,
        EsKey.Cancel => 0x03,
        EsKey.Back => 0x08,
        EsKey.Tab => 0x09,
        EsKey.Clear => 0x0C,
        EsKey.Return => 0x0D,
        EsKey.Enter => 0x0D,
        EsKey.Pause => 0x13,
        EsKey.CapsLock => 0x14,
        EsKey.Capital => 0x14,
        EsKey.HangulMode => 0x15,
        EsKey.KanaMode => 0x15,
        EsKey.JunjaMode => 0x17,
        EsKey.FinalMode => 0x18,
        EsKey.KanjiMode => 0x19,
        EsKey.HanjaMode => 0x19,
        EsKey.Escape => 0x1B,
        EsKey.ImeConvert => 0x1C,
        EsKey.ImeNonConvert => 0x1D,
        EsKey.ImeAccept => 0x1E,
        EsKey.ImeModeChange => 0x1F,
        EsKey.Space => 0x20,
        EsKey.PageUp => 0x21,
        EsKey.Prior => 0x21,
        EsKey.PageDown => 0x22,
        EsKey.Next => 0x22,
        EsKey.End => 0x23,
        EsKey.Home => 0x24,
        EsKey.Left => 0x25,
        EsKey.Up => 0x26,
        EsKey.Right => 0x27,
        EsKey.Down => 0x28,
        EsKey.Select => 0x29,
        EsKey.Print => 0x2A,
        EsKey.Execute => 0x2B,
        EsKey.Snapshot => 0x2C,
        EsKey.PrintScreen => 0x2C,
        EsKey.Insert => 0x2D,
        EsKey.Delete => 0x2E,
        EsKey.Help => 0x2F,
        EsKey.D0 => 0x30,
        EsKey.D1 => 0x31,
        EsKey.D2 => 0x32,
        EsKey.D3 => 0x33,
        EsKey.D4 => 0x34,
        EsKey.D5 => 0x35,
        EsKey.D6 => 0x36,
        EsKey.D7 => 0x37,
        EsKey.D8 => 0x38,
        EsKey.D9 => 0x39,
        EsKey.A => 0x41,
        EsKey.B => 0x42,
        EsKey.C => 0x43,
        EsKey.D => 0x44,
        EsKey.E => 0x45,
        EsKey.F => 0x46,
        EsKey.G => 0x47,
        EsKey.H => 0x48,
        EsKey.I => 0x49,
        EsKey.J => 0x4A,
        EsKey.K => 0x4B,
        EsKey.L => 0x4C,
        EsKey.M => 0x4D,
        EsKey.N => 0x4E,
        EsKey.O => 0x4F,
        EsKey.P => 0x50,
        EsKey.Q => 0x51,
        EsKey.R => 0x52,
        EsKey.S => 0x53,
        EsKey.T => 0x54,
        EsKey.U => 0x55,
        EsKey.V => 0x56,
        EsKey.W => 0x57,
        EsKey.X => 0x58,
        EsKey.Y => 0x59,
        EsKey.Z => 0x5A,
        EsKey.LWin => 0x5B,
        EsKey.RWin => 0x5C,
        EsKey.Apps => 0x5D,
        EsKey.Sleep => 0x5F,
        EsKey.NumPad0 => 0x60,
        EsKey.NumPad1 => 0x61,
        EsKey.NumPad2 => 0x62,
        EsKey.NumPad3 => 0x63,
        EsKey.NumPad4 => 0x64,
        EsKey.NumPad5 => 0x65,
        EsKey.NumPad6 => 0x66,
        EsKey.NumPad7 => 0x67,
        EsKey.NumPad8 => 0x68,
        EsKey.NumPad9 => 0x69,
        EsKey.Multiply => 0x6A,
        EsKey.Add => 0x6B,
        EsKey.Separator => 0x6C,
        EsKey.Subtract => 0x6D,
        EsKey.Decimal => 0x6E,
        EsKey.Divide => 0x6F,
        EsKey.F1 => 0x70,
        EsKey.F2 => 0x71,
        EsKey.F3 => 0x72,
        EsKey.F4 => 0x73,
        EsKey.F5 => 0x74,
        EsKey.F6 => 0x75,
        EsKey.F7 => 0x76,
        EsKey.F8 => 0x77,
        EsKey.F9 => 0x78,
        EsKey.F10 => 0x79,
        EsKey.F11 => 0x7A,
        EsKey.F12 => 0x7B,
        EsKey.F13 => 0x7C,
        EsKey.F14 => 0x7D,
        EsKey.F15 => 0x7E,
        EsKey.F16 => 0x7F,
        EsKey.F17 => 0x80,
        EsKey.F18 => 0x81,
        EsKey.F19 => 0x82,
        EsKey.F20 => 0x83,
        EsKey.F21 => 0x84,
        EsKey.F22 => 0x85,
        EsKey.F23 => 0x86,
        EsKey.F24 => 0x87,
        EsKey.NumLock => 0x90,
        EsKey.Scroll => 0x91,
        EsKey.LeftShift => 0xA0,
        EsKey.RightShift => 0xA1,
        EsKey.LeftCtrl => 0xA2,
        EsKey.RightCtrl => 0xA3,
        EsKey.LeftAlt => 0xA4,
        EsKey.RightAlt => 0xA5,
        EsKey.BrowserBack => 0xA6,
        EsKey.BrowserForward => 0xA7,
        EsKey.BrowserRefresh => 0xA8,
        EsKey.BrowserStop => 0xA9,
        EsKey.BrowserSearch => 0xAA,
        EsKey.BrowserFavorites => 0xAB,
        EsKey.BrowserHome => 0xAC,
        EsKey.VolumeMute => 0xAD,
        EsKey.VolumeDown => 0xAE,
        EsKey.VolumeUp => 0xAF,
        EsKey.MediaNextTrack => 0xB0,
        EsKey.MediaPreviousTrack => 0xB1,
        EsKey.MediaStop => 0xB2,
        EsKey.MediaPlayPause => 0xB3,
        EsKey.LaunchMail => 0xB4,
        EsKey.SelectMedia => 0xB5,
        EsKey.LaunchApplication1 => 0xB6,
        EsKey.LaunchApplication2 => 0xB7,
        EsKey.OemSemicolon => 0xBA,
        EsKey.Oem1 => 0xBA,
        EsKey.OemPlus => 0xBB,
        EsKey.OemComma => 0xBC,
        EsKey.OemMinus => 0xBD,
        EsKey.OemPeriod => 0xBE,
        EsKey.OemQuestion => 0xBF,
        EsKey.Oem2 => 0xBF,
        EsKey.OemTilde => 0xC0,
        EsKey.Oem3 => 0xC0,
        EsKey.AbntC1 => 0xC1,
        EsKey.AbntC2 => 0xC2,
        EsKey.OemOpenBrackets => 0xDB,
        EsKey.Oem4 => 0xDB,
        EsKey.OemPipe => 0xDC,
        EsKey.Oem5 => 0xDC,
        EsKey.OemCloseBrackets => 0xDD,
        EsKey.Oem6 => 0xDD,
        EsKey.OemQuotes => 0xDE,
        EsKey.Oem7 => 0xDE,
        EsKey.Oem8 => 0xDF,
        EsKey.OemBackslash => 0xE2,
        EsKey.Oem102 => 0xE2,
        EsKey.ImeProcessed => 0xE5,
        EsKey.OemAttn => 0xF0,
        EsKey.DbeAlphanumeric => 0xF0,
        EsKey.OemFinish => 0xF1,
        EsKey.DbeKatakana => 0xF1,
        EsKey.DbeHiragana => 0xF2,
        EsKey.OemCopy => 0xF2,
        EsKey.DbeSbcsChar => 0xF3,
        EsKey.OemAuto => 0xF3,
        EsKey.DbeDbcsChar => 0xF4,
        EsKey.OemEnlw => 0xF4,
        EsKey.OemBackTab => 0xF5,
        EsKey.DbeRoman => 0xF5,
        EsKey.DbeNoRoman => 0xF6,
        EsKey.Attn => 0xF6,
        EsKey.CrSel => 0xF7,
        EsKey.DbeEnterWordRegisterMode => 0xF7,
        EsKey.ExSel => 0xF8,
        EsKey.DbeEnterImeConfigureMode => 0xF8,
        EsKey.EraseEof => 0xF9,
        EsKey.DbeFlushString => 0xF9,
        EsKey.Play => 0xFA,
        EsKey.DbeCodeInput => 0xFA,
        EsKey.DbeNoCodeInput => 0xFB,
        EsKey.Zoom => 0xFB,
        EsKey.NoName => 0xFC,
        EsKey.DbeDetermineString => 0xFC,
        EsKey.DbeEnterDialogConversionMode => 0xFD,
        EsKey.Pa1 => 0xFD,
        EsKey.OemClear => 0xFE,
        _ => 0
    };
}
