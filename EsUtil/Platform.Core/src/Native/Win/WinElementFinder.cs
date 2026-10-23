using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Window;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Win32 API を利用した要素（ウィンドウ・スクリーン）検出実装。</summary>
/// <remarks>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。 本クラスは <c>PInvoke</c> を直接呼ぶ。<br/>
/// 自己完結型: 自ウィンドウの除外（自プロセスのウィンドウ）とスクリーン境界は外部からの設定なしに解決する。<br/>
/// ホスト（アプリ）が生成して注入する（テストではモックを注入できる）。<br/>
/// <b>ログを出力しない</b>: 見つからないこと（該当なし）は空の結果で表し、記録は呼び出し側が行う。<br/>
/// 継承不可。<br/>
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public sealed class WinElementFinder : IElementFinder
{
    /// <summary>クラス名取得バッファのサイズ（文字数）。</summary>
    private const int ClassNameBufferSize = 256;

    /// <summary><c>EnumWindows</c> の現在のコールバック（スレッドごと。 入れ子の呼び出しは LIFO で戻す）。</summary>
    /// <remarks>
    /// <b>スレッド ローカルにする理由</b>: アンマネージド境界を越えるデリゲートは、呼び出しのたびに作ると
    /// GC の負荷になる（要素検出は撮影のたびに走る）。 コールバックは<b>静的などラムを 1 回だけ生成</b>し、
    /// 呼び出しの文脈（どの検出処理か）だけをここで渡す。
    /// </remarks>
    [ThreadStatic]
    private static Func<IntPtr, bool>? s_windowCallback;

    /// <summary><c>EnumDisplayMonitors</c> が受け取るモニター矩形の集約先（スレッドごと）。</summary>
    [ThreadStatic]
    private static List<EsRectInt>? s_monitorBounds;

    /// <summary><c>EnumWindows</c> のコールバック（1 回だけ生成して使い回す）。</summary>
    private static readonly WNDENUMPROC s_windowThunk = WindowThunk;

    /// <summary><c>EnumDisplayMonitors</c> のコールバック（1 回だけ生成して使い回す）。</summary>
    /// <remarks>
    /// <b>生成をメソッドへ切り出す理由</b>: 生成されたデリゲートの引数にポインター（<c>RECT*</c>）があるため、
    /// フィールドの初期化子では unsafe コンテキストにならない。 生成は 1 回だけであり、呼び出しごとの負荷は無い。
    /// </remarks>
    private static readonly MONITORENUMPROC s_monitorThunk = CreateMonitorThunk();

    private IReadOnlyList<EsRectInt>? _screenBounds;

    /// <summary>全ディスプレイモニターの矩形（画面物理座標）を列挙する（自己完結）。</summary>
    /// <returns>各モニターの矩形リスト。</returns>
    /// <remarks>
    /// Win32 の <c>EnumDisplayMonitors</c> で全モニターを列挙する（Avalonia の TopLevel 非依存）。<br/>
    /// 列挙に失敗した場合は空リストを返す（モニターが無い状態として扱う）。
    /// </remarks>
    private IReadOnlyList<EsRectInt> EnumerateMonitorBounds()
        => _screenBounds ??= EnumerateMonitorBoundsCore();

    /// <summary><see cref="EnumerateMonitorBounds"/> の実体。</summary>
    /// <returns>各モニターの矩形リスト。 列挙できなかった場合は空リスト。</returns>
    private static IReadOnlyList<EsRectInt> EnumerateMonitorBoundsCore()
    {
        var bounds = new List<EsRectInt>();
        var previous = s_monitorBounds;
        s_monitorBounds = bounds;
        try
        {
            _ = PInvoke.EnumDisplayMonitors(default, null, s_monitorThunk, default);
        }
        finally
        {
            s_monitorBounds = previous;
        }

        return bounds;
    }

    /// <summary><c>EnumDisplayMonitors</c> のコールバックを生成する。</summary>
    /// <returns>生成したデリゲート。</returns>
    private static unsafe MONITORENUMPROC CreateMonitorThunk() => MonitorThunk;

    /// <summary><c>EnumDisplayMonitors</c> のコールバックの実体。</summary>
    /// <param name="hMonitor">モニターハンドル（未使用）。</param>
    /// <param name="hdcMonitor">モニターのデバイス コンテキスト（未使用）。</param>
    /// <param name="lprcMonitor">モニターの矩形へのポインター。</param>
    /// <param name="dwData">アプリケーション定義の値（未使用）。</param>
    /// <returns>列挙を継続する場合は true。</returns>
    private static unsafe BOOL MonitorThunk(HMONITOR hMonitor, HDC hdcMonitor, RECT* lprcMonitor, LPARAM dwData)
    {
        if (lprcMonitor is not null && s_monitorBounds is { } bounds)
        {
            bounds.Add(new EsRectInt(
                lprcMonitor->left, lprcMonitor->top,
                lprcMonitor->right - lprcMonitor->left, lprcMonitor->bottom - lprcMonitor->top));
        }

        return true;
    }

    /// <summary>トップレベル ウィンドウを列挙し、コールバックが false を返すまで続ける。</summary>
    /// <param name="callback">各ウィンドウの判定（false を返すと列挙を停止する）。</param>
    private static void EnumerateWindows(Func<IntPtr, bool> callback)
    {
        var previous = s_windowCallback;
        s_windowCallback = callback;
        try
        {
            _ = PInvoke.EnumWindows(s_windowThunk, default);
        }
        finally
        {
            s_windowCallback = previous;
        }
    }

    /// <summary><c>EnumWindows</c> のコールバックの実体。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lParam">アプリケーション定義の値（未使用）。</param>
    /// <returns>列挙を継続する場合は true。</returns>
    private static BOOL WindowThunk(HWND hWnd, LPARAM lParam)
        => s_windowCallback is { } callback && callback((IntPtr)hWnd);

    /// <summary>指定位置の最前面ウィンドウを検出する。</summary>
    /// <param name="screenPoint">判定する位置（画面物理座標）。</param>
    /// <returns>ウィンドウの検出結果（矩形・タイトル・識別子）。 未検出時は <see cref="ElementFinderResults.IsDetected"/> = false。</returns>
    /// <remarks>
    /// <c>EnumWindows</c> の列挙順（最前面順）で、<paramref name="screenPoint"/> を含む最初のウィンドウを採用する。<br/>
    /// 除外条件（自プロセス・不可視・無効・最小化・0x0・タスクバー/デスクトップ）は <see cref="IsEligibleWindow"/> で適用し、<br/>
    /// ホットパスで毎回全列挙しないよう、最初の一致で列挙を早期終了する。
    /// </remarks>
    public ElementFinderResults FindWindowUnderCursor(EsPointInt screenPoint)
    {
        var selfPid = PInvoke.GetCurrentProcessId();
        ElementFinderResults result = default;

        EnumerateWindows(hWnd =>
        {
            if (IsEligibleWindow(hWnd, selfPid, out var candidate) && candidate.Bounds.Contains(screenPoint))
            {
                result = candidate;
                return false; // 最初の一致で列挙を停止（ホットパス最適化）
            }
            return true;
        });

        return result;
    }

    /// <summary>ウィンドウを選択対象とするか判定し、対象なら検出結果を返す（<see cref="ListWindows"/> と共用）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="selfPid">自プロセスの PID（自ウィンドウの除外用）。</param>
    /// <param name="candidate">選択対象の検出結果。 対象外なら default。</param>
    /// <returns>選択対象なら true。</returns>
    private static bool IsEligibleWindow(IntPtr hWnd, uint selfPid, out ElementFinderResults candidate)
    {
        candidate = default;
        _ = PInvoke.GetWindowThreadProcessId((HWND)hWnd, out var pid);
        if (pid == selfPid) return false;
        if (!PInvoke.IsWindowVisible((HWND)hWnd)) return false;
        if (!PInvoke.IsWindowEnabled((HWND)hWnd)) return false;
        if (PInvoke.IsIconic((HWND)hWnd)) return false;
        if (IsExcludedClass(hWnd)) return false;

        if (!PInvoke.GetWindowRect((HWND)hWnd, out var rect))
        {
            // 矩形を取得できないウィンドウは選択対象にできない（列挙の内部で起こりうるため、対象外として除外する）
            return false;
        }

        if (rect.right <= rect.left || rect.bottom <= rect.top) return false;

        var handle = EsWindow.FromHandle(hWnd, "HWND");
        if (!TryGetWindowVisibleRect(handle, out var visibleRect))
        {
            return false;
        }
        var title = GetWindowTitle(handle);
        var className = GetWindowClassName(hWnd);
        if (IsExcludedFrame(className, title)) return false;

        candidate = new ElementFinderResults(visibleRect, true, Title: title, Handle: handle, ClassName: className);
        return true;
    }

    /// <summary>指定位置が含まれるスクリーンを検出する。</summary>
    /// <param name="screenPoint">判定する位置（画面物理座標）。</param>
    /// <returns>スクリーンの検出結果（矩形・「Screen {インデックス}」）。 未検出時は <see cref="ElementFinderResults.IsDetected"/> = false。</returns>
    /// <remarks>
    /// <see cref="ListScreens"/>（全スクリーン一覧）と同じ境界を使い、<paramref name="screenPoint"/> を含む最初の<br/>
    /// スクリーンを採用する。 スクリーン境界は Win32 の <c>EnumDisplayMonitors</c> から自己解決する。
    /// </remarks>
    public ElementFinderResults FindScreenUnderCursor(EsPointInt screenPoint)
        => FindScreen(screenPoint);

    /// <summary>全スクリーンの一覧を返す（仮想スクリーンの1枚画像に対するスナップショット管理用）。</summary>
    /// <returns>全スクリーンの検出結果一覧（矩形・「Screen {インデックス}」）。 列挙できない場合は空リスト。</returns>
    /// <remarks>
    /// 全スクリーンを1枚の画像（仮想スクリーン）として取得した際に、その画像内のスクリーン境界を<br/>
    /// 座標系（画像ローカル）で特定するための一覧を返す。<br/>
    /// モニター境界は Win32 の <c>EnumDisplayMonitors</c> から自己解決する（外部設定不要）。
    /// </remarks>
    public IReadOnlyList<ElementFinderResults> ListScreens()
    {
        var bounds = EnumerateMonitorBounds();
        var results = new List<ElementFinderResults>(bounds.Count);
        for (int i = 0; i < bounds.Count; i++)
        {
            results.Add(new ElementFinderResults(bounds[i], true, Title: $"Screen {i}"));
        }
        return results;
    }

    /// <summary>指定位置を含むスクリーンだけを探す（一覧を作らない）。</summary>
    /// <param name="screenPoint">判定する位置（画面物理座標）。</param>
    /// <returns>見つかったスクリーンの検出結果。 無い場合は <see cref="ElementFinderResults.IsDetected"/> = false。</returns>
    private ElementFinderResults FindScreen(EsPointInt screenPoint)
    {
        var bounds = EnumerateMonitorBounds();
        for (var i = 0; i < bounds.Count; i++)
        {
            if (bounds[i].Contains(screenPoint))
            {
                return new ElementFinderResults(bounds[i], true, Title: $"Screen {i}");
            }
        }

        return default;
    }

    /// <summary>全ウィンドウの一覧を返す（仮想スクリーンの1枚画像に対するスナップショット管理用）。</summary>
    /// <returns>全ウィンドウの検出結果一覧（矩形・タイトル・識別子）。 列挙できない場合は空リスト。</returns>
    /// <remarks>
    /// 全スクリーンを1枚の画像（仮想スクリーン）として取得した際に、その画像内のウィンドウ境界を<br/>
    /// 座標系（画像ローカル）で特定するための一覧を返す。<br/>
    /// <see cref="FindWindowUnderCursor(EsPointInt)"/> と同じ除外条件（自プロセス・不可視・無効・最小化・<br/>
    /// 0x0サイズ・タスクバー/デスクトップ）を適用する。
    /// </remarks>
    public IReadOnlyList<ElementFinderResults> ListWindows()
    {
        var results = new List<ElementFinderResults>();
        var selfPid = PInvoke.GetCurrentProcessId();

        // EnumWindows: 全トップレベルウィンドウを列挙し、除外条件を適用して一覧化する
        // 除外判定は IsEligibleWindow（FindWindowUnderCursor と共用・単一所有）に委譲する。
        EnumerateWindows(hWnd =>
        {
            if (IsEligibleWindow(hWnd, selfPid, out var candidate))
                results.Add(candidate);
            return true;
        });

        return results;
    }

    /// <summary>ウィンドウのクラス名を取得する。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <returns>クラス名。 取得できない場合は空文字。</returns>
    /// <remarks><see cref="ListWindows"/> の結果（<see cref="ElementFinderResults.ClassName"/>）と除外判定（<see cref="IsExcludedClass"/>）で使用する。</remarks>
    private static string GetWindowClassName(IntPtr hWnd)
    {
        var className = new char[ClassNameBufferSize];
        var len = PInvoke.GetClassName((HWND)hWnd, className);
        return len > 0 ? className.AsSpan(0, len).ToString() : "";
    }

    /// <summary>タスクバー / デスクトップウィンドウ（仕様 2.4.2 の除外対象）かどうかを判定する。</summary>
    /// <remarks>
    /// クラス名で判定する: Shell_TrayWnd（タスクバー）、Shell_SecondaryTrayWnd（セカンダリタスクバー・
    /// マルチモニタ）、Shell_ImmersiveTrayWnd（Windows 8/10 のタスクバー）、Progman / WorkerW（デスクトップ）。<br/>
    /// デスクトップ全体のキャプチャは ScreenSelect を使用するため、これらは選択対象外とする。
    /// </remarks>
    private static bool IsExcludedClass(IntPtr hWnd)
    {
        // クラス名の取得は GetWindowClassName に委譲する（重複排除）
        var className = GetWindowClassName(hWnd);
        return className.Length > 0 && IsExcludedClassName(className);
    }

    /// <summary>タイトルが空の UWP フレーム（スタートメニュー等のオーバーレイ）かどうかを判定する（純粋関数・テスト容易）。</summary>
    /// <param name="className">クラス名。</param>
    /// <param name="title">ウィンドウタイトル。</param>
    /// <returns>除外対象（タイトル空の <c>ApplicationFrameWindow</c>）なら true。</returns>
    /// <remarks>
    /// Windows 10 の UWP アプリは <c>ApplicationFrameWindow</c>（フレーム・タイトルバー）をトップレベルに持ち、<br/>
    /// 実際のコンテンツは子ウィンドウ（Windows.UI.Core.CoreWindow）に表示される。<br/>
    /// スタートメニュー / タスクビュー等のシステムオーバーレイも <c>ApplicationFrameWindow</c> だがタイトルが空のため、<br/>
    /// 「タイトル空」を条件に除外する（タイトル付きの実際の UWP アプリは選択対象に残す）。
    /// </remarks>
    internal static bool IsExcludedFrame(string className, string? title)
        => string.Equals(className, "ApplicationFrameWindow", StringComparison.Ordinal) && string.IsNullOrEmpty(title);

    /// <summary>タスクバー / デスクトップウィンドウのクラス名かどうかを判定する（純粋関数・テスト容易）。</summary>
    /// <param name="className">クラス名。</param>
    /// <returns>除外対象のクラス名なら true。</returns>
    /// <remarks>
    /// 文字列ベースの純粋判定のため、ヘッドレス不要のテストで検証できる。<br/>
    /// 除外対象: <c>Shell_TrayWnd</c> / <c>Shell_SecondaryTrayWnd</c> / <c>Shell_ImmersiveTrayWnd</c> / <c>Progman</c> / <c>WorkerW</c>。
    /// </remarks>
    internal static bool IsExcludedClassName(string className)
        => className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Shell_ImmersiveTrayWnd"
            or "Progman" or "WorkerW";

    /// <summary>ウィンドウの可視矩形領域を取得する（最大化はワークエリア・通常は DWM 影除外）。</summary>
    /// <param name="handle">ウィンドウの識別子。</param>
    /// <param name="rect">取得した可視矩形（画面物理座標）。</param>
    /// <returns>取得できた場合は true。</returns>
    /// <remarks>
    /// 最大化ウィンドウには影がなく、<c>DWMWA_EXTENDED_FRAME_BOUNDS</c> は左右に 8px の内部マージンを返す<br/>
    /// （Windows 10 の既知の挙動）ため、<c>GetWindowRect</c>（ワークエリア = タスクバーを除く最大化領域）を使用する。<br/>
    /// 通常ウィンドウは <c>DWMWA_EXTENDED_FRAME_BOUNDS</c> で影を除外した可視領域を取得し、<br/>
    /// 失敗時は <c>GetWindowRect</c> にフォールバックする。<br/>
    /// インターフェイスからは公開せず、<see cref="FindWindowUnderCursor"/> の内部実装として使用する。
    /// </remarks>
    internal static bool TryGetWindowVisibleRect(EsPlatformHandle handle, out EsRectInt rect)
    {
        var hWnd = (HWND)handle.Handle;
        rect = default;
        if (hWnd.IsNull) return false;

        RECT windowRect;
        if (!PInvoke.IsZoomed(hWnd))
        {
            // 通常ウィンドウ: DWM 影除外（DWMWA_EXTENDED_FRAME_BOUNDS）で可視領域を取得する
            var visibleRect = default(RECT);
            var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref visibleRect, 1));
            var hr = PInvoke.DwmGetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, bytes);
            if (hr.Succeeded && visibleRect.left < visibleRect.right && visibleRect.top < visibleRect.bottom)
            {
                rect = ToRectInt(visibleRect);
                return true;
            }
        }

        // フォールバック: GetWindowRect（最大化時はワークエリア）
        if (!PInvoke.GetWindowRect(hWnd, out windowRect))
        {
            return false;
        }

        rect = ToRectInt(windowRect);
        return true;
    }

    /// <summary>Win32 の矩形をコアの整数矩形へ変換する。</summary>
    /// <param name="rect">変換する矩形。</param>
    /// <returns>コアの整数矩形。</returns>
    private static EsRectInt ToRectInt(in RECT rect)
        => new(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);

    /// <summary>ウィンドウのタイトルを取得する。</summary>
    /// <param name="handle">タイトルを取得するウィンドウのハンドル。</param>
    /// <returns>ウィンドウタイトル。 取得できない場合は空文字。</returns>
    /// <remarks>インターフェイスからは公開せず、<see cref="FindWindowUnderCursor"/> の内部実装として使用する。</remarks>
    internal static string GetWindowTitle(EsPlatformHandle handle)
    {
        var hWnd = handle.Handle;
        var len = PInvoke.GetWindowTextLength((HWND)hWnd);
        if (len <= 0) return "";
        var titleChars = new char[len + 1];
        var written = PInvoke.GetWindowText((HWND)hWnd, titleChars);
        return new string(titleChars, 0, Math.Min(written, len));
    }
}


