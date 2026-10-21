using EsUtil.Platform.Core;
using MewUIWindowState = Aprillz.MewUI.Controls.WindowState;

namespace EsUtil.Ui.MewUI.Interop;

/// <summary>UI フレームワーク非依存の <see cref="EsWindowState"/> と MewUI の <see cref="MewUIWindowState"/> を相互変換する。</summary>
/// <remarks>
/// <b>対応関係</b>: 両者のメンバー名は完全に一致する（<c>Normal</c> / <c>Minimized</c> / <c>Maximized</c> / <c>FullScreen</c>）。<br/>
/// 設定ファイルの互換性のため、数値の一致には依存せず名前ベースで変換する。<br/>
/// </remarks>
public static class EsWindowStateInterop
{
    /// <summary><see cref="EsWindowState"/> を MewUI のウィンドウ状態へ変換する。</summary>
    /// <param name="state">変換対象のウィンドウ状態。</param>
    /// <returns>対応する MewUI のウィンドウ状態。 未定義値は <see cref="MewUIWindowState.Normal"/>。</returns>
    /// <remarks>
    /// 未定義値（<c>(EsWindowState)999</c> 等）は <see cref="EnumCanonical"/> が検出する
    /// （<c>Enum.TryParse</c> は数値文字列を受け付けてしまうため）。
    /// </remarks>
    public static MewUIWindowState ToMewUIWindowState(this EsWindowState state)
        => EnumCanonical.TryParse<EsWindowState, MewUIWindowState>(state, out var result)
            ? result
            : MewUIWindowState.Normal;

    /// <summary>MewUI のウィンドウ状態を <see cref="EsWindowState"/> へ変換する。</summary>
    /// <param name="state">変換対象のウィンドウ状態。</param>
    /// <returns>対応する <see cref="EsWindowState"/>。 未定義値は <see cref="EsWindowState.Normal"/>。</returns>
    public static EsWindowState ToEsWindowState(this MewUIWindowState state)
        => EnumCanonical.TryParse<MewUIWindowState, EsWindowState>(state, out var result)
            ? result
            : EsWindowState.Normal;
}
