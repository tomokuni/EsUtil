using WinFormsWindowState = System.Windows.Forms.FormWindowState;

namespace EsUtil.Ui.WinForms.Interop;

/// <summary>UI フレームワーク非依存の <see cref="EsWindowState"/> と WinForms の <see cref="WinFormsWindowState"/> を相互変換する。</summary>
/// <remarks>
/// <b>対応関係</b>: 両者のメンバー名は一致する（<c>Normal</c> / <c>Minimized</c> / <c>Maximized</c>）。<br/>
/// 設定ファイルの互換性のため、数値の一致には依存せず名前ベースで変換する。<br/>
/// <b>非対称性</b>: <see cref="EsWindowState.FullScreen"/> に対応する WinForms のウィンドウ状態は無い<br/>
/// （WinForms の全画面は <c>FormBorderStyle.None</c> + <c>WindowState.Maximized</c> の組み合わせで表す）。<br/>
/// そのため <see cref="IsRepresentable"/> で表現可否を問い合わせ、写像できない値は <c>Normal</c> へ落とす<br/>
/// （全画面を「通常」として復元すると見た目が変わるため、呼び出し側は先に表現可否を判定できる）。
/// </remarks>
public static class EsWindowStateInterop
{
    /// <summary><see cref="EsWindowState"/> を WinForms のウィンドウ状態へ変換する。</summary>
    /// <param name="state">変換対象のウィンドウ状態。</param>
    /// <returns>対応する WinForms のウィンドウ状態。 対応先がない場合（全画面・未定義値）は <see cref="WinFormsWindowState.Normal"/>。</returns>
    /// <remarks>
    /// 未定義値（<c>(EsWindowState)999</c> 等）は <see cref="EnumCanonical"/> が検出する<br/>
    /// （<c>Enum.TryParse</c> は数値文字列を受け付けてしまうため）。
    /// </remarks>
    public static WinFormsWindowState ToFormWindowState(this EsWindowState state)
        => EnumCanonical.TryParse<EsWindowState, WinFormsWindowState>(state, out var result)
            ? result
            : WinFormsWindowState.Normal;

    /// <summary>WinForms のウィンドウ状態を <see cref="EsWindowState"/> へ変換する。</summary>
    /// <param name="state">変換対象のウィンドウ状態。</param>
    /// <returns>対応する <see cref="EsWindowState"/>。 未定義値は <see cref="EsWindowState.Normal"/>。</returns>
    public static EsWindowState ToEsWindowState(this WinFormsWindowState state)
        => EnumCanonical.TryParse<WinFormsWindowState, EsWindowState>(state, out var result)
            ? result
            : EsWindowState.Normal;

    /// <summary>WinForms が表現できる <see cref="EsWindowState"/> かどうかを判定する。</summary>
    /// <param name="state">判定対象のウィンドウ状態。</param>
    /// <returns>対応するウィンドウ状態がある場合（全画面・未定義値以外）は true。</returns>
    public static bool IsRepresentable(this EsWindowState state)
        => EnumCanonical.TryParse<EsWindowState, WinFormsWindowState>(state, out _);
}
