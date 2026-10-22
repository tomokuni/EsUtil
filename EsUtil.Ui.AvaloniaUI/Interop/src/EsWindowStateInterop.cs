using AvaloniaWindowState = Avalonia.Controls.WindowState;

namespace EsUtil.Ui.AvaloniaUI.Interop;

/// <summary>UI フレームワーク非依存の <see cref="EsWindowState"/> と Avalonia の <see cref="AvaloniaWindowState"/> を相互変換する。</summary>
/// <remarks>
/// 両者はメンバー名が一致するように設計されているため、数値の一致には依存せず名前ベースで変換する<br/>
/// （将来 Avalonia 側に別名メンバーが追加されても、名前で変換すればずれない）。<br/>
/// メンバー数が 4 と少ないため、未定義値（<c>(EsWindowState)999</c> 等）は <see cref="EnumCanonical"/> で検出し、<br/>
/// 既定値（<c>Normal</c>）へフォールバックする（<c>Enum.TryParse</c> は数値文字列を許容するため、名前解決の成否だけでは未定義値を検出できない）。
/// </remarks>
public static class EsWindowStateInterop
{
    /// <summary><see cref="EsWindowState"/> を Avalonia の <see cref="AvaloniaWindowState"/> へ変換する。</summary>
    /// <param name="state">変換対象のウィンドウ状態。</param>
    /// <returns>対応する Avalonia のウィンドウ状態。 未定義値・名前が一致しない場合は <see cref="AvaloniaWindowState.Normal"/>。</returns>
    public static AvaloniaWindowState ToAvaloniaWindowState(this EsWindowState state) =>
        EnumCanonical.TryParse<EsWindowState, AvaloniaWindowState>(state, out var result)
            ? result
            : AvaloniaWindowState.Normal;

    /// <summary>Avalonia の <see cref="AvaloniaWindowState"/> を <see cref="EsWindowState"/> へ変換する。</summary>
    /// <param name="state">変換対象のウィンドウ状態。</param>
    /// <returns>対応する <see cref="EsWindowState"/>。 未定義値・名前が一致しない場合は <see cref="EsWindowState.Normal"/>。</returns>
    public static EsWindowState ToEsWindowState(this AvaloniaWindowState state) =>
        EnumCanonical.TryParse<AvaloniaWindowState, EsWindowState>(state, out var result)
            ? result
            : EsWindowState.Normal;
}
