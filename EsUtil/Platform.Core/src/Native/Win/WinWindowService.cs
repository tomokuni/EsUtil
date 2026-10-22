using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Win32 API を用いたネイティブウィンドウ操作を提供する。</summary>
/// <remarks>
/// オーバーレイウィンドウに必要なネイティブウィンドウ操作（Alt+Tab 非表示・キャプチャ除外・配置・退避）を提供する。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。 本クラスは <c>PInvoke</c> を直接呼ぶ。<br/>
/// <see cref="IWindowService"/> の実装であり、テスト時のモックに備える。<br/>
/// ウィンドウの特定は <see cref="EsPlatformHandle"/>（UI 非依存の識別子）で行い、<br/>
/// 本クラスは Avalonia オブジェクト（TopLevel 等）に一切触れない（スレッド非依存・ハング時退避に適合）。<br/>
/// <b>ログを出力しない</b>: OS 操作の失敗は <see cref="Win32Exception"/> で呼び出し側へ渡し、記録は呼び出し側が行う。<br/>
/// ホスト（アプリ）が生成して注入する（テストではモックを注入できる）。<br/>
/// 継承不可。<br/>
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public sealed class WinWindowService : IWindowService
{
    /// <summary>ウィンドウをタスクバー / Alt+Tab に表示しないよう設定する（選択オーバーレイ用、仕様 2.2）。</summary>
    /// <inheritdoc/>
    public void HideFromTaskSwitcher(EsPlatformHandle handle)
    {
        var nativeHwnd = (HWND)handle.Handle;
        if (nativeHwnd.IsNull) return;

        var exStyle = PInvoke.GetWindowLongPtr(nativeHwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if (exStyle == 0)
        {
            // 取得できない状態で書き換えると既存の拡張スタイルのビットを失うため、書き換えずに失敗として通知する
            throw new Win32Exception(
                Marshal.GetLastWin32Error(), "ウィンドウの拡張スタイルを取得できませんでした（Alt+Tab の候補から除外できません）。");
        }

        if (PInvoke.SetWindowLongPtr(
                nativeHwnd,
                WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                exStyle | (nint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(), "ウィンドウを Alt+Tab の候補から除外できませんでした（拡張スタイルを設定できません）。");
        }
    }

    /// <summary>ウィンドウをキャプチャから除外する/除外を解除する（仕様 6.2.1）。</summary>
    /// <inheritdoc/>
    public bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude)
    {
        var nativeHwnd = (HWND)handle.Handle;
        if (nativeHwnd.IsNull) return false;

        var affinity = exclude ? WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE : WINDOW_DISPLAY_AFFINITY.WDA_NONE;
        var success = PInvoke.SetWindowDisplayAffinity(nativeHwnd, affinity);
        if (success && exclude)
        {
            // 除外設定が DWM に反映されるまで 1 フレーム待つ（設定直後のキャプチャを防ぐ）
            _ = PInvoke.DwmFlush();
        }

        return success;
    }

    /// <summary>ウィンドウの位置とサイズを画面物理ピクセルで指定する（仕様 2.3）。</summary>
    /// <inheritdoc/>
    public void SetBounds(EsPlatformHandle handle, EsRectInt bounds)
    {
        var nativeHwnd = (HWND)handle.Handle;
        if (nativeHwnd.IsNull) return;

        if (!PInvoke.SetWindowPos(
                nativeHwnd, default, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE))
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
        var nativeHwnd = (HWND)handle.Handle;
        if (nativeHwnd.IsNull) return;

        // 1. Topmost を解除（タスクマネージャー等が前面に出せるようにする）
        _ = PInvoke.SetWindowPos(nativeHwnd, HWND.HWND_NOTOPMOST, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        // 2. ウィンドウを非表示にする（戻り値は非表示化の前の可視状態であり、失敗を意味しない）
        _ = PInvoke.ShowWindow(nativeHwnd, SHOW_WINDOW_CMD.SW_HIDE);

        // 3. WM_CLOSE を送信（UI スレッドが復帰した場合に自動キャンセル処理が走る）
        _ = PInvoke.PostMessage(nativeHwnd, PInvoke.WM_CLOSE, default, default);
    }
}
