namespace EsUtil.Platform.Core.Native.Win;

// 本ファイルは dwmapi.dll の宣言（影を除いた可視矩形と合成の完了待ち）を集約する。 型の説明は WinNative.cs を参照する。
internal static partial class WinNative
{

    // ============================================================
    // 定数
    // ============================================================
    // --- DWM 影除外 ---
    /// <summary>DWM 拡張フレーム境界（影を除外した可視矩形）の属性 ID。</summary>
    internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // ============================================================
    // dwmapi.dll
    // ============================================================

    /// <summary>DWM のウィンドウ属性を取得する（dwmapi.DwmGetWindowAttribute）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="dwAttribute">取得する属性。</param>
    /// <param name="pvAttribute">属性値の出力先。</param>
    /// <param name="cbAttribute">属性値のサイズ。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>宣言は CsWin32 が生成する（<c>src/NativeMethods.txt</c>）。 本メソッドは<b>生ポインターと数値の引数</b>を
    /// 保つための薄い包みであり、呼び出し側のコードを変えない。</remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static unsafe int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, ref RECT pvAttribute, int cbAttribute)
    {
        fixed (RECT* value = &pvAttribute)
        {
            return Windows.Win32.PInvoke.DwmGetWindowAttribute(
                (Windows.Win32.Foundation.HWND)hWnd,
                (Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE)dwAttribute,
                value,
                (uint)cbAttribute).Value;
        }
    }
#pragma warning restore CA1416

    /// <summary>DWM（デスクトップ合成マネージャー）が現在のフレームの合成を完了するまでブロックする（dwmapi.DwmFlush）。</summary>
    /// <returns>成功した場合は 0（S_OK）。</returns>
    /// <remarks>
    /// ウィンドウの表示・非表示・サイズ変更後に呼ぶと、その変更が画面へ反映されたことを保証できる。<br/>
    /// キャプチャ前に使用して、非表示状態の残像が写り込むのを防ぐ（DWM の合成完了を待つ）。<br/>
    /// 宣言は CsWin32 が生成する（<c>src/NativeMethods.txt</c>）。
    /// </remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static int DwmFlush() => Windows.Win32.PInvoke.DwmFlush().Value;
#pragma warning restore CA1416
}
