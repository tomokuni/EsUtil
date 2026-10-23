namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>色文字列（色名 / #RRGGBB / #AARRGGBB / +{n}% 形式）の妥当性を検証するユーティリティ。</summary>
/// <remarks>
/// 検証は <see cref="ColorString.TryParse(string, out Avalonia.Media.Color, out double)"/> に一元委譲する<br/>
/// （色文字列の解析ロジックの二重所有を排除し、<see cref="ColorString"/> を単一の権威とする）。<br/>
/// <see cref="ColorComboBoxControl"/> のバリデーション（最終有効値の追跡・エラー状態の判定）から利用する。<br/>
/// UI 非依存（Avalonia.Media を参照しない）のため、他のプロジェクトでも流用可能である。
/// </remarks>
internal static class ColorValidator
{
    /// <summary>指定された色文字列が有効か判定する。</summary>
    /// <param name="color">検証対象の色文字列。 null は無効。</param>
    /// <returns>有効な色の場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// null は <see cref="ColorString.TryParse(string, out Avalonia.Media.Color, out double)"/> が false を返すため、
    /// 明示的な null チェックは行わない（分岐の重複を排除）。
    /// </remarks>
    internal static bool IsValid(string? color)
        => ColorString.TryParse(color, out _, out _);
}
