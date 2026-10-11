using System.Runtime.InteropServices;

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
}
