namespace EsUtil.Ui.AvaloniaUI.ColorPicker;

/// <summary>色のバリデーション状態（エラーの有無・メッセージ）を純粋に決定するロジック。</summary>
/// <remarks>
/// <see cref="ColorPickerControl.UpdateValidationStatus"/> の検証状態決定を UI スレッド（StyledProperty）から分離する。<br/>
/// 「空文字または有効な色はエラーなし、無効な色はエラーあり」という判定を純粋関数で提供し、<br/>
/// UI 非依存の <c>[Fact]</c> で並列テストできる（UI ロジック分離・テスト並列性向上）。
/// </remarks>
internal static class ColorValidation
{
    /// <summary>無効な色コードのエラーメッセージ。</summary>
    internal const string InvalidColorMessage = "無効な色コードです。";

    /// <summary>検証結果（エラーの有無とメッセージ）。</summary>
    /// <param name="color">検証対象の色文字列。</param>
    /// <param name="isValid">事前に判定済みの有効性（<see cref="ColorValidator.IsValid"/> の結果）。</param>
    /// <returns>エラーの有無とメッセージ（エラーなしの場合はメッセージ null）。</returns>
    /// <remarks>
    /// 空文字または有効な色はエラーなし（メッセージ null）、それ以外はエラーあり（<see cref="InvalidColorMessage"/>）を返す。
    /// </remarks>
    public static ValidationState Determine(string color, bool isValid)
    {
        if (string.IsNullOrEmpty(color) || isValid)
        {
            return new ValidationState(hasError: false, message: null);
        }

        return new ValidationState(hasError: true, message: InvalidColorMessage);
    }
    /// <summary>無効な色を最終有効値へ復帰すべきか判定する。</summary>
    /// <param name="isValid">事前に判定済みの有効性（<see cref="ColorValidator.IsValid"/> の結果）。</param>
    /// <param name="hasLastValid">最終有効値が存在するかどうか。</param>
    /// <returns>復帰すべき場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 無効な色かつ最終有効値が存在する場合に復帰する（<see cref="ColorPickerControl.OnLostFocus"/> から利用）。<br/>
    /// 「フォーカス喪失時に無効な入力を最終有効値へ戻す」判定を純粋関数で提供する。
    /// </remarks>
    internal static bool ShouldRecover(bool isValid, bool hasLastValid)
        => !isValid && hasLastValid;

    /// <summary>バリデーション状態（エラーの有無とメッセージ）。</summary>
    /// <param name="hasError">バリデーションエラーが有効かどうか。</param>
    /// <param name="message">エラーメッセージ（エラーなしの場合は null）。</param>
    public readonly record struct ValidationState(bool hasError, string? message);
}
