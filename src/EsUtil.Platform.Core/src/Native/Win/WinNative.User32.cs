using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

// 本ファイルは user32.dll の宣言（カーソル・ウィンドウ・要素検出・画面キャプチャのデバイス コンテキスト）を集約する。 型の説明は WinNative.cs を参照する。
internal static partial class WinNative
{

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
    // user32.dll — 画面キャプチャ（デバイス コンテキスト）
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
}
