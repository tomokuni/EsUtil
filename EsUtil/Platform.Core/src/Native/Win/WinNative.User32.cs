using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EsUtil.Platform.Core.Native.Win;

// 本ファイルは user32.dll の宣言（カーソル・ウィンドウ・要素検出・画面キャプチャのデバイス コンテキスト）を集約する。
// 型の説明は WinNative.cs を参照する。
// 宣言は CsWin32 が生成する（src/NativeMethods.txt。 アーキテクチャ固有の SetWindowLongPtr のため
// csproj が CsWin32PlatformTarget=x64 を指定する）。 本ファイルは内部の値型（POINT / RECT / CURSORINFO）と
// IntPtr を保つ薄い包みであり、呼び出し側（Win* 実装）の型を変えない。
internal static partial class WinNative
{

    // ============================================================
    // デリゲート
    // ============================================================

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
    internal const WINDOW_LONG_PTR_INDEX GWL_EXSTYLE = WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE;

    /// <summary>ウィンドウ破棄時に最後に届くメッセージ（サブクラス化の解除に使う）。</summary>
    internal const uint WM_NCDESTROY = 0x0082;

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
    // コールバックの橋渡し（アンマネージド境界を越えるため、呼び出しごとにデリゲートを作らない）
    // ============================================================
    /// <summary>EnumWindows の現在のコールバック（スレッドごと。 入れ子の呼び出しは LIFO で戻す）。</summary>
    [ThreadStatic]
    private static EnumWindowsProc? s_enumWindowsCallback;

    /// <summary>EnumDisplayMonitors の現在のコールバック（スレッドごと。 入れ子の呼び出しは LIFO で戻す）。</summary>
    [ThreadStatic]
    private static MonitorEnumProc? s_monitorEnumCallback;

    /// <summary>EnumWindows のコールバック（1 回だけ生成して使い回す）。</summary>
    /// <remarks>
    /// <b>静的にする理由</b>: アンマネージド境界を越えるデリゲートは、呼び出しのたびに作ると GC の負荷になる
    /// （要素検出は撮影のたびに走る）。 呼び出しの文脈は <see cref="s_enumWindowsCallback"/> で渡す。
    /// </remarks>
    private static readonly WNDENUMPROC s_enumWindowsThunk = EnumWindowsThunk;

    /// <summary>EnumDisplayMonitors のコールバック（1 回だけ生成して使い回す）。</summary>
    /// <remarks>
    /// <b>生成をメソッドへ切り出す理由</b>: 生成されたデリゲートの引数にポインター（<c>RECT*</c>）があるため、
    /// フィールドの初期化子では unsafe コンテキストにならない。 生成は 1 回だけであり、呼び出しごとの負荷は無い。
    /// </remarks>
    private static readonly MONITORENUMPROC s_monitorEnumThunk = CreateMonitorEnumThunk();

    /// <summary>EnumDisplayMonitors のコールバックを生成する。</summary>
    /// <returns>生成したデリゲート。</returns>
    private static unsafe MONITORENUMPROC CreateMonitorEnumThunk() => MonitorEnumThunk;

    /// <summary>EnumWindows のコールバックの実体。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lParam">アプリケーション定義の値。</param>
    /// <returns>列挙を継続する場合は true。</returns>
    private static BOOL EnumWindowsThunk(HWND hWnd, LPARAM lParam)
        => s_enumWindowsCallback is { } callback && callback((IntPtr)hWnd, (IntPtr)lParam);

    /// <summary>EnumDisplayMonitors のコールバックの実体。</summary>
    /// <param name="hMonitor">モニターハンドル。</param>
    /// <param name="hdcMonitor">モニターのデバイスコンテキスト。</param>
    /// <param name="lprcMonitor">モニターの矩形へのポインター。</param>
    /// <param name="dwData">アプリケーション定義の値。</param>
    /// <returns>列挙を継続する場合は true。</returns>
    private static unsafe BOOL MonitorEnumThunk(HMONITOR hMonitor, HDC hdcMonitor, Windows.Win32.Foundation.RECT* lprcMonitor, LPARAM dwData)
        => s_monitorEnumCallback is { } callback
            && callback((IntPtr)hMonitor, hdcMonitor, (IntPtr)lprcMonitor, (IntPtr)dwData);

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
    /// 生成された宣言は <c>System.Drawing.Point</c> を使うため、本メソッドで自前の値型へ写す。
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

    /// <summary>指定したウィンドウの情報を設定する（user32.SetWindowLongPtrW）。 拡張スタイルの設定に使用する。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="nIndex">設定する情報のインデックス（<see cref="GWL_EXSTYLE"/> 等）。</param>
    /// <param name="dwNewLong">設定する値。</param>
    /// <returns>設定前の値。</returns>
    /// <remarks>
    /// <b>64 ビットのポインター長 API を使う</b>: この API はポインター長の値を扱うため、
    /// 32 ビットの <c>SetWindowLong</c> では値が切り詰められる。
    /// 生成には <c>CsWin32PlatformTarget</c> の指定が要る（csproj を参照する）。
    /// </remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static nint SetWindowLongPtr(IntPtr hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong)
        => PInvoke.SetWindowLongPtr((HWND)hWnd, nIndex, dwNewLong);
#pragma warning restore CA1416

    /// <summary>指定したウィンドウの情報を取得する（user32.GetWindowLongPtrW）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="nIndex">取得する情報のインデックス（<see cref="GWL_EXSTYLE"/> 等）。</param>
    /// <returns>取得した値。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static nint GetWindowLongPtr(IntPtr hWnd, WINDOW_LONG_PTR_INDEX nIndex)
        => PInvoke.GetWindowLongPtr((HWND)hWnd, nIndex);
#pragma warning restore CA1416

    /// <summary>ウィンドウ プロシージャをサブクラス化する（comctl32.SetWindowSubclass）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="pfnSubclass">サブクラス プロシージャ。 呼び出し側がフィールドに保持する（GC 対策）。</param>
    /// <param name="uIdSubclass">サブクラスを識別する値。 同一ウィンドウ内で一意にする。</param>
    /// <param name="dwRefData">サブクラス プロシージャへ渡すアプリケーション定義の値。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b><c>SetWindowLongPtr(GWLP_WNDPROC)</c> を使わない理由</b>: 従来の差し替えは
    /// 元のプロシージャを自前で保存・復元する必要があり、他のサブクラス化（ビジュアル スタイル等）と
    /// 共存できない。 <c>SetWindowSubclass</c> は comctl32 が連鎖を管理し、
    /// <see cref="DefSubclassProc"/> で元のプロシージャへ委譲できる（Windows の推奨手順）。<br/>
    /// <b>ウィンドウを作成したスレッドから呼ぶ</b>（スレッド アフィン）。<br/>
    /// ウィンドウ破棄時は <see cref="WM_NCDESTROY"/> で <see cref="RemoveWindowSubclass"/> を呼び、
    /// サブクラス プロシージャへの参照を解放する（呼ばないと comctl32 が参照を保持し続ける）。
    /// </remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, nuint uIdSubclass, nuint dwRefData)
        => PInvoke.SetWindowSubclass((HWND)hWnd, pfnSubclass, uIdSubclass, dwRefData);
#pragma warning restore CA1416

    /// <summary>サブクラス化を解除する（comctl32.RemoveWindowSubclass）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="pfnSubclass"><see cref="SetWindowSubclass"/> へ渡したサブクラス プロシージャ。</param>
    /// <param name="uIdSubclass"><see cref="SetWindowSubclass"/> へ渡した識別値。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, nuint uIdSubclass)
        => PInvoke.RemoveWindowSubclass((HWND)hWnd, pfnSubclass, uIdSubclass);
#pragma warning restore CA1416

    /// <summary>元のウィンドウ プロシージャへメッセージを委譲する（comctl32.DefSubclassProc）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="msg">ウィンドウメッセージ。</param>
    /// <param name="wParam">WPARAM。</param>
    /// <param name="lParam">LPARAM。</param>
    /// <returns>ウィンドウ プロシージャの処理結果。</returns>
    /// <remarks>
    /// <b>サブクラス プロシージャの中から呼ぶ</b>: comctl32 が次のプロシージャ（本来の WndProc を含む）へ
    /// 連鎖させる。 元のプロシージャのアドレスを自前で保持する必要はない。
    /// </remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static LRESULT DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        => PInvoke.DefSubclassProc((HWND)hWnd, msg, (nuint)wParam, lParam);
#pragma warning restore CA1416

    /// <summary>全トップレベルウィンドウを列挙する（user32.EnumWindows）。</summary>
    /// <param name="lpEnumFunc">列挙コールバック。</param>
    /// <param name="lParam">アプリケーション定義の値。</param>
    /// <returns>最後のコールバックが true を返した場合は true。</returns>
    /// <remarks>
    /// <b>コールバックを毎回作らない</b>: アンマネージド境界を越えるデリゲートは GC の負荷になるため、
    /// 静的などラムを使い、呼び出しの文脈はスレッド ローカルで渡す（入れ子の呼び出しも LIFO で戻す）。
    /// </remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam)
    {
        var previous = s_enumWindowsCallback;
        s_enumWindowsCallback = lpEnumFunc;
        try
        {
            return PInvoke.EnumWindows(s_enumWindowsThunk, lParam);
        }
        finally
        {
            s_enumWindowsCallback = previous;
        }
    }
#pragma warning restore CA1416

    /// <summary>全ディスプレイモニターを列挙する（user32.EnumDisplayMonitors）。</summary>
    /// <param name="hdc">デバイスコンテキスト（NULL = 全モニター）。</param>
    /// <param name="lpfnEnum">列挙コールバック。</param>
    /// <param name="dwData">アプリケーション定義の値。</param>
    /// <returns>最後のコールバックが true を返した場合は true。</returns>
    /// <remarks>
    /// コールバックを毎回作らない理由は <see cref="EnumWindows"/> を参照する。<br/>
    /// <b>クリップ矩形は取らない</b>: 全モニターを列挙する用途しかなく、常に NULL を渡すため
    /// （引数を持たせると「渡したのに効かない」と誤解される）。
    /// </remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool EnumDisplayMonitors(IntPtr hdc, MonitorEnumProc lpfnEnum, IntPtr dwData)
    {
        var previous = s_monitorEnumCallback;
        s_monitorEnumCallback = lpfnEnum;
        try
        {
            return PInvoke.EnumDisplayMonitors((HDC)hdc, null, s_monitorEnumThunk, dwData);
        }
        finally
        {
            s_monitorEnumCallback = previous;
        }
    }
#pragma warning restore CA1416

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