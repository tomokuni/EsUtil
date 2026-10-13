using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Win32 API を利用した要素（ウィンドウ・スクリーン）検出実装。</summary>
/// <remarks>
/// P/Invoke 呼び出しは <c>WinNative</c> に委譲する。<br/>
/// 自己完結型: 自ウィンドウの除外（自プロセスのウィンドウ）とスクリーン境界は外部からの設定なしに解決する。<br/>
/// ホスト（アプリ）が生成して注入する（テストではモックを注入できる）。<br/>
/// <b>ログを出力しない</b>: 見つからないこと（該当なし）は空の結果で表し、記録は呼び出し側が行う。<br/>
/// 継承不可。<br/>
/// </remarks>
public sealed class WinElementFinder : IElementFinder
{
    /// <summary>クラス名取得バッファのサイズ（文字数）。</summary>
    private const int ClassNameBufferSize = 256;

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
        _ = WinNative.EnumDisplayMonitors(
            IntPtr.Zero, IntPtr.Zero, (_, _, lprcMonitor, _) =>
            {
                if (lprcMonitor != IntPtr.Zero)
                {
                    var rect = Marshal.PtrToStructure<WinNative.RECT>(lprcMonitor);
                    bounds.Add(new EsRectInt(rect.Left, rect.Top, rect.Width, rect.Height));
                }
                return true;
            }, IntPtr.Zero);

        return bounds;
    }

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
        var selfPid = WinNative.GetCurrentProcessId();
        ElementFinderResults result = default;

        _ = WinNative.EnumWindows((hWnd, _) =>
        {
            if (IsEligibleWindow(hWnd, selfPid, out var candidate) && candidate.Bounds.Contains(screenPoint))
            {
                result = candidate;
                return false; // 最初の一致で列挙を停止（ホットパス最適化）
            }
            return true;
        }, IntPtr.Zero);

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
        WinNative.GetWindowThreadProcessId(hWnd, out var pid);
        if (pid == selfPid) return false;
        if (!WinNative.IsWindowVisible(hWnd)) return false;
        if (!WinNative.IsWindowEnabled(hWnd)) return false;
        if (WinNative.IsIconic(hWnd)) return false;
        if (IsExcludedClass(hWnd)) return false;

        if (!WinNative.GetWindowRect(hWnd, out var rect))
        {
            // 矩形を取得できないウィンドウは選択対象にできない（列挙の内部で起こりうるため、対象外として除外する）
            return false;
        }

        if (rect.Width <= 0 || rect.Height <= 0) return false;

        var handle = new EsPlatformHandle(hWnd, "HWND");
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
        var selfPid = WinNative.GetCurrentProcessId();

        // EnumWindows: 全トップレベルウィンドウを列挙し、除外条件を適用して一覧化する
        // 除外判定は IsEligibleWindow（FindWindowUnderCursor と共用・単一所有）に委譲する。
        _ = WinNative.EnumWindows((hWnd, _) =>
        {
            if (IsEligibleWindow(hWnd, selfPid, out var candidate))
                results.Add(candidate);
            return true;
        }, IntPtr.Zero);

        return results;
    }

    /// <summary>ウィンドウのクラス名を取得する。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <returns>クラス名。 取得できない場合は空文字。</returns>
    /// <remarks><see cref="ListWindows"/> の結果（<see cref="ElementFinderResults.ClassName"/>）と除外判定（<see cref="IsExcludedClass"/>）で使用する。</remarks>
    private static string GetWindowClassName(IntPtr hWnd)
    {
        var className = new char[ClassNameBufferSize];
        var len = WinNative.GetClassName(hWnd, className, className.Length);
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
    /// 可視矩形の取得（最大化のワークエリア / DWM 影除外 / GetWindowRect フォールバック）は
    /// <see cref="WinNative.TryGetVisibleRect"/> に委譲する（<see cref="WinCaptureService"/> と共用・単一所有）。<br/>
    /// インターフェイスからは公開せず、<see cref="FindWindowUnderCursor"/> の内部実装として使用する。
    /// </remarks>
    internal static bool TryGetWindowVisibleRect(EsPlatformHandle handle, out EsRectInt rect)
    {
        if (!WinNative.TryGetVisibleRect(handle.Handle, out var nativeRect))
        {
            rect = default;
            return false;
        }

        rect = new EsRectInt(nativeRect.Left, nativeRect.Top, nativeRect.Width, nativeRect.Height);
        return true;
    }

    /// <summary>ウィンドウのタイトルを取得する。</summary>
    /// <param name="handle">タイトルを取得するウィンドウのハンドル。</param>
    /// <returns>ウィンドウタイトル。 取得できない場合は空文字。</returns>
    /// <remarks>インターフェイスからは公開せず、<see cref="FindWindowUnderCursor"/> の内部実装として使用する。</remarks>
    internal static string GetWindowTitle(EsPlatformHandle handle)
    {
        var hWnd = handle.Handle;
        var len = WinNative.GetWindowTextLengthW(hWnd);
        if (len <= 0) return "";
        var titleChars = new char[len + 1];
        WinNative.GetWindowText(hWnd, titleChars, titleChars.Length);
        return new string(titleChars, 0, len);
    }
}


