using System.ComponentModel;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Win32 API を用いたネイティブウィンドウ操作を提供する。</summary>
/// <remarks>
/// オーバーレイウィンドウに必要なネイティブウィンドウ操作（Alt+Tab 非表示・キャプチャ除外・配置・退避）を提供する。<br/>
/// P/Invoke 呼び出しは <c>WinNative</c> に委譲する。<br/>
/// <see cref="IWindowService"/> の実装であり、テスト時のモックに備える。<br/>
/// ウィンドウの特定は <see cref="EsPlatformHandle"/>（UI 非依存の識別子）で行い、<br/>
/// 本クラスは Avalonia オブジェクト（TopLevel 等）に一切触れない（スレッド非依存・ハング時退避に適合）。<br/>
/// <b>ログを出力しない</b>: OS 操作の失敗は <see cref="Win32Exception"/> で呼び出し側へ渡し、記録は呼び出し側が行う。<br/>
/// ホスト（アプリ）が生成して注入する（テストではモックを注入できる）。<br/>
/// 継承不可。<br/>
/// </remarks>
public sealed class WinWindowService : IWindowService
{
    /// <summary>ウィンドウをタスクバー / Alt+Tab に表示しないよう設定する（選択オーバーレイ用、仕様 2.2）。</summary>
    /// <inheritdoc/>
    public void HideFromTaskSwitcher(EsPlatformHandle handle)
    {
        var nativeHwnd = handle.Handle;
        if (nativeHwnd == IntPtr.Zero) return;

        var exStyle = WinNative.GetWindowLongPtr(nativeHwnd, WinNative.GWL_EXSTYLE);
        if (exStyle == IntPtr.Zero)
        {
            // 取得できない状態で書き換えると既存の拡張スタイルのビットを失うため、書き換えずに失敗として通知する
            throw new Win32Exception(
                Marshal.GetLastWin32Error(), "ウィンドウの拡張スタイルを取得できませんでした（Alt+Tab の候補から除外できません）。");
        }

        if (WinNative.SetWindowLongPtr(
                nativeHwnd, WinNative.GWL_EXSTYLE, new IntPtr(exStyle.ToInt64() | WinNative.WS_EX_TOOLWINDOW)) == IntPtr.Zero)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(), "ウィンドウを Alt+Tab の候補から除外できませんでした（拡張スタイルを設定できません）。");
        }
    }

    /// <summary>ウィンドウをキャプチャから除外する/除外を解除する（仕様 6.2.1）。</summary>
    /// <inheritdoc/>
    public bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude)
    {
        var nativeHwnd = handle.Handle;
        if (nativeHwnd == IntPtr.Zero) return false;

        var affinity = exclude ? WinNative.WDA_EXCLUDEFROMCAPTURE : WinNative.WDA_NONE;
        var success = WinNative.SetWindowDisplayAffinity(nativeHwnd, affinity);
        if (success && exclude)
        {
            // 除外設定が DWM に反映されるまで 1 フレーム待つ（設定直後のキャプチャを防ぐ）
            _ = WinNative.DwmFlush();
        }

        return success;
    }

    /// <summary>ウィンドウの位置とサイズを画面物理ピクセルで指定する（仕様 2.3）。</summary>
    /// <inheritdoc/>
    public void SetBounds(EsPlatformHandle handle, EsRectInt bounds)
    {
        var nativeHwnd = handle.Handle;
        if (nativeHwnd == IntPtr.Zero) return;

        if (!WinNative.SetWindowPos(
                nativeHwnd, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                WinNative.SWP_NOZORDER | WinNative.SWP_NOACTIVATE))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"ウィンドウの位置とサイズを設定できませんでした（({bounds.X}, {bounds.Y}, {bounds.Width}, {bounds.Height})）。");
        }
    }

    /// <summary>ウィンドウを最前面解除し、非表示にして閉じる（UI ハング時の退避、仕様 3.6）。</summary>
    /// <inheritdoc/>
    /// <remarks>
    /// <b>ベストエフォート</b>: 各手順は独立に実行し、1 つが失敗しても残りを続行する
    /// （呼び出し元はウォッチドッグであり、代替手段が無いため失敗を返しても回復できない）。
    /// </remarks>
    public void EvacuateWindow(EsPlatformHandle handle)
    {
        var nativeHwnd = handle.Handle;
        if (nativeHwnd == IntPtr.Zero) return;

        // 1. Topmost を解除（タスクマネージャー等が前面に出せるようにする）
        _ = WinNative.SetWindowPos(nativeHwnd, WinNative.HWND_NOTOPMOST, 0, 0, 0, 0,
            WinNative.SWP_NOMOVE | WinNative.SWP_NOSIZE | WinNative.SWP_NOACTIVATE);

        // 2. ウィンドウを非表示にする（戻り値は非表示化の前の可視状態であり、失敗を意味しない）
        _ = WinNative.ShowWindow(nativeHwnd, WinNative.SW_HIDE);

        // 3. WM_CLOSE を送信（UI スレッドが復帰した場合に自動キャンセル処理が走る）
        _ = WinNative.PostMessage(nativeHwnd, WinNative.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }
}
