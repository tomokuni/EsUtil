using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Win32 API を用いたネイティブウィンドウ操作を提供する。</summary>
/// <remarks>
/// オーバーレイウィンドウに必要なネイティブウィンドウ操作（Alt+Tab 非表示・UI ハング時の退避）を提供する。<br/>
/// P/Invoke 呼び出しは <c>WinNative</c> に委譲する。<br/>
/// <see cref="IWindowService"/> の実装であり、テスト時のモックに備える。<br/>
/// ウィンドウの特定は <see cref="EsPlatformHandle"/>（UI 非依存の識別子）で行い、<br/>
/// 本クラスは Avalonia オブジェクト（TopLevel 等）に一切触れない（スレッド非依存・ハング時退避に適合）。<br/>
/// ホスト（アプリ）が生成して注入する（テストではモックを注入できる）。<br/>
/// 継承不可。<br/>
/// </remarks>
/// <remarks><see cref="WinWindowService"/> の新しいインスタンスを初期化します。</remarks>
/// <param name="logger">ロガー。 null の場合は出力なし。</param>
public sealed class WinWindowService(ILogger? logger = null) : IWindowService
{
    /// <summary>ロガー（null の場合は出力なし）。</summary>
    private readonly ILogger _log = logger.OrNullLogger();

    /// <summary>ウィンドウをタスクバー / Alt+Tab に表示しないよう設定する（選択オーバーレイ用、仕様 2.2）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <remarks>
    /// WS_EX_TOOLWINDOW を拡張スタイルに追加し、Alt+Tab の切り替え候補から除外する。<br/>
    /// 無効なハンドルの場合は何もしない。<br/>
    /// <b>現在のスタイルを取得できない場合は書き換えない</b>（取得できない状態で書き換えると、既存の<br/>
    /// 拡張スタイルのビットを失いウィンドウの振る舞いを壊すため。 失敗は記録する）。
    /// </remarks>
    public void HideFromTaskSwitcher(EsPlatformHandle handle)
    {
        var nativeHwnd = handle.Handle;
        if (nativeHwnd == IntPtr.Zero) return;

        var exStyle = WinNative.GetWindowLongPtr(nativeHwnd, WinNative.GWL_EXSTYLE);
        if (exStyle == IntPtr.Zero)
        {
            _log.LogWindowStyleQueryFailed(nativeHwnd.ToString("X"));
            return;
        }

        var success = WinNative.SetWindowLongPtr(nativeHwnd, WinNative.GWL_EXSTYLE,
            new IntPtr(exStyle.ToInt64() | WinNative.WS_EX_TOOLWINDOW)) != IntPtr.Zero;
        _log.LogWindowHiddenFromTaskSwitcher(nativeHwnd.ToString("X"), success);
    }

    /// <summary>ウィンドウをキャプチャから除外する/除外を解除する（仕様 6.2.1）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <param name="exclude">キャプチャ除外する場合は <see langword="true"/>。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// WDA_EXCLUDEFROMCAPTURE により、そのウィンドウは画面キャプチャ（CopyFromScreen / DXGI 等）に
    /// 一切写らなくなる（フレームタイミングに依存しない決定的な除外）。<br/>
    /// 除外設定時のみ DWM の再合成待ち（<c>DwmFlush</c>）を行い、設定直後のキャプチャを防ぐ。<br/>
    /// Windows 10 2004（build 19041）以降で利用可能。 それ以前では失敗（false）を返す。<br/>
    /// </remarks>
    public bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude)
    {
        var nativeHwnd = handle.Handle;
        if (nativeHwnd == IntPtr.Zero) return false;

        var affinity = exclude ? WinNative.WDA_EXCLUDEFROMCAPTURE : WinNative.WDA_NONE;
        var success = WinNative.SetWindowDisplayAffinity(nativeHwnd, affinity);
        _log.LogCaptureExclusionChanged(nativeHwnd.ToString("X"), exclude, success);
        if (success && exclude)
        {
            // 除外設定が DWM に反映されるまで 1 フレーム待つ（設定直後のキャプチャを防ぐ）
            WinNative.DwmFlush();
        }
        return success;
    }

    /// <summary>ウィンドウの位置とサイズを画面物理ピクセルで指定する（仕様 2.3）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <param name="bounds">設定する矩形（画面物理ピクセル。 負の座標可）。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// SetWindowPos により、OS の作業領域補正を受けずに物理ピクセルで配置する。<br/>
    /// Z オーダーとアクティブ状態は変更しない（<c>SWP_NOZORDER</c> / <c>SWP_NOACTIVATE</c>）。<br/>
    /// 無効なハンドルの場合は何もしない（false）。<br/>
    /// </remarks>
    public bool SetBounds(EsPlatformHandle handle, EsRectInt bounds)
    {
        var nativeHwnd = handle.Handle;
        if (nativeHwnd == IntPtr.Zero) return false;

        var success = WinNative.SetWindowPos(nativeHwnd, IntPtr.Zero,
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            WinNative.SWP_NOZORDER | WinNative.SWP_NOACTIVATE);
        _log.LogWindowBoundsChanged(nativeHwnd.ToString("X"), bounds.X, bounds.Y, bounds.Width, bounds.Height, success);
        return success;
    }

    /// <summary>ウィンドウを最前面解除し、非表示にして閉じる（UI ハング時の退避、仕様 3.6）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <remarks>
    /// UI スレッドがハングした場合にバックグラウンドスレッドから呼び出され、<br/>
    /// タスクマネージャー等でプロセスを終了できるようにする。<br/>
    /// 1. Topmost を解除（タスクマネージャー等が前面に出せるようにする）<br/>
    /// 2. ウィンドウを非表示にする（全画面オーバーレイが邪魔しないようにする）<br/>
    /// 3. WM_CLOSE を送信（UI スレッドが復帰した場合に自動キャンセル処理が走る）<br/>
    /// Win32 API はスレッド非依存のため、バックグラウンドスレッドから呼び出し可能。<br/>
    /// 本メソッドは <see cref="EsPlatformHandle"/>（数値）のみを使用し、UI オブジェクトに触れない。<br/>
    /// </remarks>
    public void EvacuateWindow(EsPlatformHandle handle)
    {
        var nativeHwnd = handle.Handle;
        if (nativeHwnd == IntPtr.Zero) return;

        // 1. Topmost を解除
        var unsetTopmost = WinNative.SetWindowPos(nativeHwnd, WinNative.HWND_NOTOPMOST, 0, 0, 0, 0,
            WinNative.SWP_NOMOVE | WinNative.SWP_NOSIZE | WinNative.SWP_NOACTIVATE);

        // 2. ウィンドウを非表示にする（戻り値は非表示化の前の可視状態であり、失敗を意味しない）
        var wasVisible = WinNative.ShowWindow(nativeHwnd, WinNative.SW_HIDE);

        // 3. WM_CLOSE を送信
        var posted = WinNative.PostMessage(nativeHwnd, WinNative.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

        // OS 操作の結果を記録する（いずれかが失敗しても処理は続行する。 退避はベストエフォート）
        _log.LogWindowEvacuated(nativeHwnd.ToString("X"), unsetTopmost, wasVisible, posted);
    }
}
