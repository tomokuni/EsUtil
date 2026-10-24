using CoreValidation = EsUtil.Platform.Core.Palette.ColorValidation;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>色のバリデーション状態を決める（規則の所有者はコアの <see cref="CoreValidation"/>）。</summary>
/// <remarks>
/// 「空または有効な色はエラーなし」という規則と、フォーカスを失ったときに最後の有効値へ戻す判定は
/// コアが単一所有する。 本クラスは本プロジェクトの内部名を保つための薄い入口である。
/// </remarks>
internal static class ColorValidation
{
    /// <summary>無効な色コードのエラー メッセージ。</summary>
    internal const string InvalidColorMessage = CoreValidation.InvalidColorMessage;

    /// <summary>検証結果を決める。</summary>
    /// <param name="color">検証対象の色文字列。</param>
    /// <param name="isValid">事前に判定済みの有効性（<see cref="ColorValidator.IsValid(string?)"/> の結果）。</param>
    /// <returns>エラーの有無とメッセージ。</returns>
    public static ValidationState Determine(string? color, bool isValid)
    {
        var state = CoreValidation.Determine(color, isValid);
        return new ValidationState(state.HasError, state.Message);
    }

    /// <summary>無効な色を最後の有効値へ戻すべきかを判定する。</summary>
    /// <param name="isValid">事前に判定済みの有効性。</param>
    /// <param name="hasLastValid">最後の有効値があるか。</param>
    /// <returns>戻すべき場合は <see langword="true"/>。</returns>
    internal static bool ShouldRecover(bool isValid, bool hasLastValid)
        => CoreValidation.ShouldRecover(isValid, hasLastValid);

    /// <summary>バリデーション状態（エラーの有無とメッセージ）。</summary>
    /// <param name="hasError">エラーがあるか。</param>
    /// <param name="message">エラー メッセージ（エラーなしの場合は null）。</param>
    public readonly record struct ValidationState(bool hasError, string? message);
}