using CoreValidation = EsUtil.Platform.Core.Palette.ColorValidation;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>色文字列の妥当性を検証する（規則の所有者はコアの <see cref="CoreValidation"/>）。</summary>
/// <remarks>
/// 判定は <see cref="CoreValidation.IsValid(string?)"/> が単一所有する（解析は
/// <see cref="EsUtil.Platform.Core.Palette.ColorString"/>）。 本クラスは本プロジェクトの内部名を保つための薄い入口である。
/// </remarks>
internal static class ColorValidator
{
    /// <summary>指定された色文字列が有効かを判定する。</summary>
    /// <param name="color">検証対象の色文字列。 null は無効。</param>
    /// <returns>有効な場合は <see langword="true"/>。</returns>
    internal static bool IsValid(string? color) => CoreValidation.IsValid(color);
}