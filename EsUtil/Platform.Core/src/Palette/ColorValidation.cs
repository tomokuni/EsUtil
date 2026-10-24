namespace EsUtil.Platform.Core.Palette;

/// <summary>色文字列の妥当性（エラーの有無とメッセージ）を決める純粋な判定。</summary>
/// <remarks>
/// 「空または有効な色はエラーなし、無効な色はエラーあり」という規則の<b>単一所有者</b>である。
/// UI スレッドの外（UI 初期化を要しない <c>[Fact]</c>）で検証できるよう、判定だけを置く。<br/>
/// 有効性の判定は <see cref="ColorString.TryParse(string, out System.Drawing.Color, out double)"/> が単一所有する
/// （本クラスは条件分岐だけを持つ）。
/// </remarks>
public static class ColorValidation
{
    /// <summary>無効な色のときのエラー メッセージ。</summary>
    public const string InvalidColorMessage = "無効な色コードです。";

    /// <summary>色文字列が有効かを調べる。</summary>
    /// <param name="color">対象の文字列。 null 可。</param>
    /// <returns>有効な場合は <see langword="true"/>。</returns>
    public static bool IsValid(string? color) => ColorString.TryParse(color, out _, out _);

    /// <summary>バリデーション状態を決める。</summary>
    /// <param name="color">対象の文字列。 null 可。</param>
    /// <param name="isValid">事前に判定した有効性（<see cref="IsValid"/> の結果）。</param>
    /// <returns>エラーの有無とメッセージ。</returns>
    /// <remarks>空文字または有効な色はエラーなし（メッセージは null）、それ以外はエラーあり。</remarks>
    public static ColorValidationState Determine(string? color, bool isValid)
        => string.IsNullOrEmpty(color) || isValid
            ? new ColorValidationState(HasError: false, Message: null)
            : new ColorValidationState(HasError: true, Message: InvalidColorMessage);

    /// <summary>無効な入力を最後の有効値へ戻すべきかを決める。</summary>
    /// <param name="isValid">事前に判定した有効性。</param>
    /// <param name="hasLastValid">最後の有効値があるか。</param>
    /// <returns>戻すべき場合は <see langword="true"/>。</returns>
    /// <remarks>フォーカスを失ったときの復帰（無効な入力のままにしない）に使う。</remarks>
    public static bool ShouldRecover(bool isValid, bool hasLastValid) => !isValid && hasLastValid;
}
