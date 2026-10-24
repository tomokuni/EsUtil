namespace EsUtil.Platform.Core.Palette;

/// <summary>色のバリデーション状態。</summary>
/// <param name="HasError">エラーがあるか。</param>
/// <param name="Message">エラー メッセージ（エラーなしの場合は null）。</param>
public readonly record struct ColorValidationState(bool HasError, string? Message);
