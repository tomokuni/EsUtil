using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EsUtil.Platform.Core.Native.Win;

// 本ファイルは user32.dll の宣言（カーソル・ウィンドウ・要素検出・画面キャプチャのデバイス コンテキスト）を集約する。
// 型の説明は WinNative.cs を参照する。
// 宣言は CsWin32 が生成する（src/NativeMethods.txt）。 本ファイルは内部の値型（POINT / RECT / CURSORINFO）と
// IntPtr を保つ薄い包みであり、呼び出し側（Win* 実装）の型を変えない。
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

    /// <summary>CURSORINFO.flags: カーソルが表示されていることを示すフラグ。</summary>
    internal const int CURSOR_SHOWING = (int)CURSORINFO_FLAGS.CURSOR_SHOWING;

    /// <summary>HTCLIENT: クライアント領域内のヒットテスト結果。</summary>
    internal const int HTCLIENT = 1;

    // --- 仮想スクリーン（全モニタのバウンディングボックス） ---
    /// <summary>仮想スクリーン左端のメトリクス。</summary>
    internal const SYSTEM_METRICS_INDEX SM_XVIRTUALSCREEN = SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN;

    /// <summary>仮想スクリーン上端のメトリクス。</summary>
    internal const SYSTEM_METRICS_INDEX SM_YVIRTUALSCREEN = SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN;

    /// <summary>仮想スクリーン幅のメトリクス。</summary>
    internal const SYSTEM_METRICS_INDEX SM_CXVIRTUALSCREEN = SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN;

    /// <summary>仮想スクリーン高さのメトリクス。</summary>
    internal const SYSTEM_METRICS_INDEX SM_CYVIRTUALSCREEN = SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN;

    // --- ウィンドウスタイル（Alt+Tab 非表示用） ---
    /// <summary>拡張ウィンドウスタイルを取得/設定するインデックス。</summary>
    /// <remarks>
    /// <b><c>int</c> として持つ理由</b>: <c>SetWindowLongPtr</c> は手書きの <c>LibraryImport</c>
    /// （64 ビットのポインター長 API）であり、<c>LibraryImport</c> は CsWin32 の列挙型を引数に取れない
    /// （<c>SYSLIB1051</c>）。 値は列挙型から取る（数値を書かない）。
    /// </remarks>
    internal const int GWL_EXSTYLE = (int)WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE;

    /// <summary>ウィンドウプロシージャを取得/設定するインデックス。</summary>
    /// <remarks><c>int</c> として持つ理由は <see cref="GWL_EXSTYLE"/> を参照する。</remarks>
    internal const int GWLP_WNDPROC = (int)WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC;

    /// <summary>タスクスイッチャー非表示の拡張スタイル。</summary>
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    // --- ウォッチドッグ（UI ハング時の救出用） ---
    /// <summary>ウィンドウクローズメッセージ。</summary>
    internal const uint WM_CLOSE = 0x0010;

    /// <summary>ウィンドウ非表示コマンド。</summary>
    internal const SHOW_WINDOW_CMD SW_HIDE = SHOW_WINDOW_CMD.SW_HIDE;

    /// <summary>最前面解除用の特別なハンドル値。</summary>
    internal static readonly IntPtr HWND_NOTOPMOST = new(-2);

    /// <summary>サイズ変更なしの SetWindowPos フラグ。</summary>
    internal const SET_WINDOW_POS_FLAGS SWP_NOSIZE = SET_WINDOW_POS_FLAGS.SWP_NOSIZE;

    /// <summary>移動なしの SetWindowPos フラグ。</summary>
    internal const SET_WINDOW_POS_FLAGS SWP_NOMOVE = SET_WINDOW_POS_FLAGS.SWP_NOMOVE;

    /// <summary>アクティブ化なしの SetWindowPos フラグ。</summary>
    internal const SET_WINDOW_POS_FLAGS SWP_NOACTIVATE = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;

    /// <summary>Z オーダーを変更しない SetWindowPos フラグ。</summary>
    internal const SET_WINDOW_POS_FLAGS SWP_NOZORDER = SET_WINDOW_POS_FLAGS.SWP_NOZORDER;

    // --- キャプチャ除外（SetWindowDisplayAffinity） ---
    /// <summary>キャプチャ除外なし（通常状態）。</summary>
    internal const WINDOW_DISPLAY_AFFINITY WDA_NONE = WINDOW_DISPLAY_AFFINITY.WDA_NONE;

    /// <summary>ウィンドウを全キャプチャAPI（GDI / DXGI）から除外する（Windows 10 2004+）。</summary>
    internal const WINDOW_DISPLAY_AFFINITY WDA_EXCLUDEFROMCAPTURE = WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE;

    // ============================================================
    // user32.dll
    // ============================================================

    /// <summary>現在のカーソル位置を取得する（user32.GetCursorPos）。</summary>
    /// <param name="lpPoint">カーソル位置の出力先。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static unsafe bool GetCursorPos(out POINT lpPoint)
    {
        // 生成された宣言は System.Drawing.Point を使う（本ライブラリの POINT は幅・高さの計算を持つ値型）
        System.Drawing.Point point;
        var succeeded = PInvoke.GetCursorPos(&point);
        lpPoint = new POINT(point.X, point.Y);
        return succeeded;
    }
#pragma warning restore CA1416

    /// <summary>カーソルを指定位置に移動する（user32.SetCursorPos）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool SetCursorPos(int x, int y) => PInvoke.SetCursorPos(x, y);
#pragma warning restore CA1416

    /// <summary>マウスポインターの表示/非表示を切り替える（user32.ShowCursor）。</summary>
    /// <param name="bShow">表示する場合は <see langword="true"/>。</param>
    /// <returns>表示カウンタ。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static int ShowCursor(bool bShow) => PInvoke.ShowCursor(bShow);
#pragma warning restore CA1416

    /// <summary>カーソルの表示状態・位置を取得する（user32.GetCursorInfo）。</summary>
    /// <param name="pci">カーソル情報の出力先。 cbSize は本メソッドが設定する。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 表示状態（<see cref="CURSORINFO.flags"/> の <see cref="CURSOR_SHOWING"/>）を取得して、<br/>
    /// Win32 の表示カウンタと <see cref="WinCursorService"/> の状態追跡を同期するために使用する。<br/>
    /// 生成された宣言は幅・高さの計算を持たない <c>CURSORINFO</c> を使うため、本メソッドで写す。
    /// </remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static unsafe bool GetCursorInfo(ref CURSORINFO pci)
    {
        var info = new Windows.Win32.UI.WindowsAndMessaging.CURSORINFO
        {
            cbSize = (uint)sizeof(Windows.Win32.UI.WindowsAndMessaging.CURSORINFO),
        };
        var succeeded = PInvoke.GetCursorInfo(&info);
        pci.cbSize = (int)info.cbSize;
        pci.flags = (int)info.flags;
        pci.hCursor = info.hCursor;
        pci.ptScreenPos = new POINT(info.ptScreenPos.X, info.ptScreenPos.Y);
        return succeeded;
    }
#pragma warning restore CA1416

    /// <summary>指定したウィンドウの情報を取得/設定する（user32.SetWindowLongPtrW）。 WndProc サブクラス化とスタイル設定に使用する。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="nIndex">設定する情報のインデックス（<see cref="GWLP_WNDPROC"/> / <see cref="GWL_EXSTYLE"/> 等）。</param>
    /// <param name="dwNewLong">設定する値。</param>
    /// <returns>設定前の値。</returns>
    /// <remarks>
    /// <b>手書きの宣言を残す理由</b>: CsWin32 が生成するのは 32 ビットの <c>SetWindowLong</c> であり、
    /// <b>64 ビットのポインター長 API は生成されない</b>。 ウィンドウ プロシージャ（<c>GWLP_WNDPROC</c>）は
    /// ポインター長であり、32 ビット版ではアドレスが切り詰められて実行時に落ちる。
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>指定したウィンドウの情報を取得する（user32.GetWindowLongPtrW）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="nIndex">取得する情報のインデックス（<see cref="GWL_EXSTYLE"/> 等）。</param>
    /// <returns>取得した値。</returns>
    /// <remarks>手書きの宣言を残す理由は <see cref="SetWindowLongPtr"/> を参照する。</remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    /// <summary>元の WndProc にメッセージを委譲する（user32.CallWindowProcW）。</summary>
    /// <param name="lpPrevWndFunc">元の WndProc アドレス。</param>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="msg">ウィンドウメッセージ。</param>
    /// <param name="wParam">WPARAM。</param>
    /// <param name="lParam">LPARAM。</param>
    /// <returns>ウィンドウプロシージャの処理結果。</returns>
    /// <remarks>
    /// <b>手書きの宣言を残す理由</b>: 生成された宣言の第 1 引数はデリゲート（<c>WNDPROC</c>）であり、
    /// <c>IntPtr</c> から渡すにはメッセージごとにデリゲートを作る（<c>WM_SETCURSOR</c> はマウス移動のたびに届く）。
    /// 本ライブラリはサブクラス化で得たアドレスをそのまま渡すため、<c>IntPtr</c> を受ける宣言を持つ。
    /// </remarks>
    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW", SetLastError = true)]
    internal static partial IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>全トップレベルウィンドウを列挙する（user32.EnumWindows）。</summary>
    /// <param name="lpEnumFunc">列挙コールバック。</param>
    /// <param name="lParam">アプリケーション定義の値。</param>
    /// <remarks>手書きの宣言を残す理由は <see cref="CallWindowProc"/> と同じ（コールバックをデリゲートで受けない）。</remarks>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    /// <summary>ウィンドウの外接矩形を取得する（user32.GetWindowRect）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lpRect">矩形の出力先。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>生成された宣言の <c>RECT</c> はフィールドのみであり、本ライブラリの <see cref="RECT"/>（幅・高さを持つ）へ写す。</remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static unsafe bool GetWindowRect(IntPtr hWnd, out RECT lpRect)
    {
        Windows.Win32.Foundation.RECT rect;
        var succeeded = PInvoke.GetWindowRect((HWND)hWnd, &rect);
        lpRect = new RECT(rect.left, rect.top, rect.right, rect.bottom);
        return succeeded;
    }
#pragma warning restore CA1416

    /// <summary>ウィンドウが最大化されているかを判定する（user32.IsZoomed）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <returns>最大化されている場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool IsZoomed(IntPtr hWnd) => PInvoke.IsZoomed((HWND)hWnd);
#pragma warning restore CA1416

    /// <summary>ウィンドウの可視状態を判定する（user32.IsWindowVisible）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <returns>可視の場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool IsWindowVisible(IntPtr hWnd) => PInvoke.IsWindowVisible((HWND)hWnd);
#pragma warning restore CA1416

    /// <summary>ウィンドウが入力を受け付け可能かを判定する（user32.IsWindowEnabled）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <returns>操作可能な場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool IsWindowEnabled(IntPtr hWnd) => PInvoke.IsWindowEnabled((HWND)hWnd);
#pragma warning restore CA1416

    /// <summary>ウィンドウが最小化されているかを判定する（user32.IsIconic）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <returns>最小化されている場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool IsIconic(IntPtr hWnd) => PInvoke.IsIconic((HWND)hWnd);
#pragma warning restore CA1416

    /// <summary>ウィンドウのクラス名を取得する（user32.GetClassNameW）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lpClassName">クラス名の出力先バッファ。</param>
    /// <param name="nMaxCount">バッファサイズ。</param>
    /// <returns>書き込まれた文字数。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static unsafe int GetClassName(IntPtr hWnd, char[] lpClassName, int nMaxCount)
    {
        fixed (char* buffer = lpClassName)
        {
            return PInvoke.GetClassName((HWND)hWnd, new PWSTR(buffer), nMaxCount);
        }
    }
#pragma warning restore CA1416

    /// <summary>ウィンドウタイトルの文字列長を取得する（user32.GetWindowTextLengthW）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <returns>タイトルの文字数。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static int GetWindowTextLength(IntPtr hWnd) => PInvoke.GetWindowTextLength((HWND)hWnd);
#pragma warning restore CA1416

    /// <summary>ウィンドウタイトルを取得する（user32.GetWindowTextW）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lpString">タイトルの出力先バッファ。</param>
    /// <param name="nMaxCount">バッファサイズ。</param>
    /// <returns>書き込まれた文字数。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static unsafe int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount)
    {
        fixed (char* buffer = lpString)
        {
            return PInvoke.GetWindowText((HWND)hWnd, new PWSTR(buffer), nMaxCount);
        }
    }
#pragma warning restore CA1416

    /// <summary>ウィンドウを所有するプロセス ID を取得する（user32.GetWindowThreadProcessId）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="processId">プロセス ID の出力先。</param>
    /// <returns>ウィンドウを作成したスレッド ID。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static unsafe uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId)
    {
        uint id;
        var threadId = PInvoke.GetWindowThreadProcessId((HWND)hWnd, &id);
        processId = id;
        return threadId;
    }
#pragma warning restore CA1416

    /// <summary>全ディスプレイモニターを列挙する（user32.EnumDisplayMonitors）。</summary>
    /// <param name="hdc">デバイスコンテキスト（NULL = 全モニター）。</param>
    /// <param name="lprcClip">クリップ矩形（NULL = クリップなし）。</param>
    /// <param name="lpfnEnum">列挙コールバック。</param>
    /// <param name="dwData">アプリケーション定義の値。</param>
    /// <remarks>手書きの宣言を残す理由は <see cref="CallWindowProc"/> と同じ（コールバックをデリゲートで受けない）。</remarks>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    /// <summary>指定されたシステムメトリクス（SM_ 定数）を取得する（user32.GetSystemMetrics）。</summary>
    /// <param name="nIndex">取得するシステムメトリクスのインデックス。</param>
    /// <returns>システムメトリクスの値。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static int GetSystemMetrics(SYSTEM_METRICS_INDEX nIndex) => PInvoke.GetSystemMetrics(nIndex);
#pragma warning restore CA1416

    /// <summary>ウィンドウの位置・サイズ・Z オーダーを変更する（user32.SetWindowPos）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="hWndInsertAfter">Z オーダー位置（<see cref="HWND_NOTOPMOST"/> 等）。</param>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="cx">幅。</param>
    /// <param name="cy">高さ。</param>
    /// <param name="uFlags">フラグ（<see cref="SWP_NOMOVE"/> 等）。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags)
        => PInvoke.SetWindowPos((HWND)hWnd, (HWND)hWndInsertAfter, x, y, cx, cy, uFlags);
#pragma warning restore CA1416

    /// <summary>ウィンドウの表示状態を変更する（user32.ShowWindow）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="nCmdShow">表示コマンド（<see cref="SW_HIDE"/> 等）。</param>
    /// <returns>以前に可視だった場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool ShowWindow(IntPtr hWnd, SHOW_WINDOW_CMD nCmdShow) => PInvoke.ShowWindow((HWND)hWnd, nCmdShow);
#pragma warning restore CA1416

    /// <summary>ウィンドウへメッセージを送信する（user32.PostMessage）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="msg">メッセージ（<see cref="WM_CLOSE"/> 等）。</param>
    /// <param name="wParam">WPARAM。</param>
    /// <param name="lParam">LPARAM。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        => PInvoke.PostMessage((HWND)hWnd, msg, (nuint)wParam, lParam);
#pragma warning restore CA1416

    /// <summary>システム メニューのコマンド（ウィンドウ メニュー）を送るメッセージ。</summary>
    internal const uint WM_SYSCOMMAND = 0x0112;

    /// <summary>システム コマンド: ウィンドウの移動を開始する。</summary>
    internal const int SC_MOVE = 0xF010;

    /// <summary>システム コマンド: ウィンドウのサイズ変更を開始する。</summary>
    internal const int SC_SIZE = 0xF000;

    /// <summary>ウィンドウのキャプチャ除外設定を変更する（user32.SetWindowDisplayAffinity）。</summary>
    /// <param name="hWnd">対象ウィンドウハンドル。</param>
    /// <param name="dwAffinity">除外設定（<see cref="WDA_NONE"/> / <see cref="WDA_EXCLUDEFROMCAPTURE"/>）。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <see cref="WDA_EXCLUDEFROMCAPTURE"/> を設定すると、そのウィンドウは画面キャプチャ（BitBlt / DXGI 等）に
    /// 一切写らなくなる（フレームタイミングに依存しない決定的な除外）。<br/>
    /// キャプチャ前に設定し、キャプチャ後に <see cref="WDA_NONE"/> へ戻す。<br/>
    /// Windows 10 2004（build 19041）以降で利用可能。 それ以前では失敗（false）を返す。
    /// </remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool SetWindowDisplayAffinity(IntPtr hWnd, WINDOW_DISPLAY_AFFINITY dwAffinity)
        => PInvoke.SetWindowDisplayAffinity((HWND)hWnd, dwAffinity);
#pragma warning restore CA1416

    // ============================================================
    // user32.dll — 画面キャプチャ（デバイス コンテキスト）
    // ============================================================
    /// <summary>デスクトップ全体のデバイス コンテキストを取得する（user32.GetDC）。</summary>
    /// <param name="hWnd">対象ウィンドウ。 <see cref="IntPtr.Zero"/> は画面全体。</param>
    /// <returns>デバイス コンテキスト。 失敗した場合は <see cref="IntPtr.Zero"/>。</returns>
    /// <remarks>取得したコンテキストは <see cref="ReleaseDC"/> で解放する。</remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static IntPtr GetDC(IntPtr hWnd) => PInvoke.GetDC((HWND)hWnd);
#pragma warning restore CA1416

    /// <summary>デバイス コンテキストを解放する（user32.ReleaseDC）。</summary>
    /// <param name="hWnd">対象ウィンドウ（<see cref="GetDC"/> に渡した値）。</param>
    /// <param name="hDC">解放するデバイス コンテキスト。</param>
    /// <returns>解放した場合は 1。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static int ReleaseDC(IntPtr hWnd, IntPtr hDC) => PInvoke.ReleaseDC((HWND)hWnd, (HDC)hDC);
#pragma warning restore CA1416
}