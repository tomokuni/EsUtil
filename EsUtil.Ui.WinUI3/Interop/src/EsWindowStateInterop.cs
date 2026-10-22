using Microsoft.UI.Windowing;

namespace EsUtil.Ui.WinUI3.Interop;

/// <summary>UI フレームワーク非依存の <see cref="EsWindowState"/> と WinUI 3 のウィンドウ状態を相互変換する。</summary>
/// <remarks>
/// <b>WinUI 3 は 1 つの列挙型でウィンドウ状態を表さない</b>: 全画面は表示方法（<see cref="AppWindowPresenterKind"/>）、<br/>
/// 通常・最小化・最大化は重ね表示の状態（<see cref="OverlappedPresenterState"/>）が担う。<br/>
/// そのため本クラスは 2 つの列挙型への写像を提供する（<see cref="EsWindowState.FullScreen"/> は全画面提示で表す）。<br/>
/// <br/>
/// <b>対応先がない値</b>: <see cref="EsWindowState.FullScreen"/> を <see cref="OverlappedPresenterState"/> へ写す場合は<br/>
/// 対応するメンバーがないため <see cref="OverlappedPresenterState.Restored"/> とする（全画面の解除として扱う）。<br/>
/// 逆に <see cref="AppWindowPresenterKind.CompactOverlay"/> は<br/>
/// <see cref="EsWindowState"/> にないため <see cref="EsWindowState.Normal"/> とする（1 対 1 でないことは本 remarks が単一所有する）。<br/>
/// <br/>
/// <b>設定ファイルの互換性</b>: 変換は数値の一致に依存せず、名前ではなく規則（全画面 / それ以外）で行う。
/// </remarks>
public static class EsWindowStateInterop
{
    /// <summary><see cref="EsWindowState"/> を WinUI 3 の表示方法へ変換する。</summary>
    /// <param name="state">変換対象のウィンドウ状態。</param>
    /// <returns>
    /// <see cref="EsWindowState.FullScreen"/> の場合は <see cref="AppWindowPresenterKind.FullScreen"/>、<br/>
    /// それ以外（通常・最小化・最大化）は <see cref="AppWindowPresenterKind.Default"/>。
    /// </returns>
    /// <remarks>最小化・最大化は提示の種類ではなく重ね表示の状態のため、<see cref="ToOverlappedPresenterState"/> が担う。</remarks>
    public static AppWindowPresenterKind ToAppWindowPresenterKind(this EsWindowState state)
        => state == EsWindowState.FullScreen ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Default;

    /// <summary>WinUI 3 の表示方法を <see cref="EsWindowState"/> へ変換する。</summary>
    /// <param name="kind">変換対象の表示方法。</param>
    /// <returns><see cref="AppWindowPresenterKind.FullScreen"/> の場合は <see cref="EsWindowState.FullScreen"/>、それ以外は <see cref="EsWindowState.Normal"/>。</returns>
    public static EsWindowState ToEsWindowState(this AppWindowPresenterKind kind)
        => kind == AppWindowPresenterKind.FullScreen ? EsWindowState.FullScreen : EsWindowState.Normal;

    /// <summary><see cref="EsWindowState"/> を WinUI 3 の重ね表示の状態へ変換する。</summary>
    /// <param name="state">変換対象のウィンドウ状態。</param>
    /// <returns>
    /// <see cref="EsWindowState.Minimized"/> は <see cref="OverlappedPresenterState.Minimized"/>、<br/>
    /// <see cref="EsWindowState.Maximized"/> は <see cref="OverlappedPresenterState.Maximized"/>、<br/>
    /// それ以外（通常・全画面）は <see cref="OverlappedPresenterState.Restored"/>。
    /// </returns>
    public static OverlappedPresenterState ToOverlappedPresenterState(this EsWindowState state) => state switch
    {
        EsWindowState.Minimized => OverlappedPresenterState.Minimized,
        EsWindowState.Maximized => OverlappedPresenterState.Maximized,
        _ => OverlappedPresenterState.Restored,
    };

    /// <summary>WinUI 3 の重ね表示の状態を <see cref="EsWindowState"/> へ変換する。</summary>
    /// <param name="state">変換対象の重ね表示の状態。</param>
    /// <returns>
    /// <see cref="OverlappedPresenterState.Minimized"/> は <see cref="EsWindowState.Minimized"/>、<br/>
    /// <see cref="OverlappedPresenterState.Maximized"/> は <see cref="EsWindowState.Maximized"/>、<br/>
    /// <see cref="OverlappedPresenterState.Restored"/> は <see cref="EsWindowState.Normal"/>。
    /// </returns>
    public static EsWindowState ToEsWindowState(this OverlappedPresenterState state) => state switch
    {
        OverlappedPresenterState.Minimized => EsWindowState.Minimized,
        OverlappedPresenterState.Maximized => EsWindowState.Maximized,
        _ => EsWindowState.Normal,
    };
}
