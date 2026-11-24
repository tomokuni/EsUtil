using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Ui.WinForms.StyledText;

/// <summary>書式バーの部品（装飾の切り替え・数値の入力）。</summary>
/// <remarks>
/// <b>見た目を 1 か所へ集める</b>: 余白や文字サイズがバーと外観パネルでずれるのを防ぐ。
/// 値の所有者は計算層（<see cref="TextEditorLayout"/>）である。
/// </remarks>
internal static class StyledTextFormatBarControls
{
    /// <summary>行の文字サイズ（ポイント）。</summary>
    internal const float FontSize = 9F;

    /// <summary>装飾の切り替え（B / I / U / S）の文字サイズ（ポイント）。</summary>
    /// <remarks>コアの値（14 DIP）をポイントへ換算する（1 DIP = 0.75 pt）。</remarks>
    internal const float ToggleFontSize = (float)(TextEditorLayout.ToggleFontSize * 0.75);

    /// <summary>編集 UI の文字に使うフォント名を取得する。</summary>
    /// <remarks>本文の既定フォント（<see cref="TextDefaults.PreferredFontFamilies"/> の先頭）と揃える。</remarks>
    internal static string UiFontFamily => TextDefaults.PreferredFontFamilies[0];

    /// <summary>項目間の余白（ピクセル）。</summary>
    internal const int Gap = (int)TextEditorLayout.ControlSpacing;

    /// <summary>装飾の切り替え（B / I / U / S）を作る。</summary>
    /// <param name="text">ボタンの文字。</param>
    /// <param name="isChecked">押された状態にする場合は <see langword="true"/>。</param>
    /// <param name="apply">押された状態が変わったときに呼ぶ処理。</param>
    /// <param name="bold">太字で表示する場合は <see langword="true"/>。</param>
    /// <param name="italic">斜体で表示する場合は <see langword="true"/>。</param>
    /// <returns>切り替えボタン。</returns>
    internal static CheckBox Toggle(string text, bool isChecked, Action<bool> apply, bool bold = false, bool italic = false)
    {
        var button = new CheckBox
        {
            Text = text,
            Appearance = Appearance.Button,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Size = new Size(28, 24),
            Checked = isChecked,
            Font = new Font(
                TextEditorLayout.ToggleFontFamily,
                ToggleFontSize,
                bold ? FontStyle.Bold : italic ? FontStyle.Italic : FontStyle.Regular),
            Margin = new Padding(1),
        };

        button.CheckedChanged += (_, _) => apply(button.Checked);
        return button;
    }

    /// <summary>数値の入力を作る。</summary>
    /// <param name="value">現在の値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="apply">値が変わったときに呼ぶ処理。</param>
    /// <returns>数値の入力。</returns>
    internal static NumericUpDown NumberBox(double value, double minimum, double maximum, Action<double> apply)
    {
        var box = new NumericUpDown
        {
            Minimum = (decimal)minimum,
            Maximum = (decimal)maximum,
            DecimalPlaces = 1,
            Increment = 1,
            Value = (decimal)Math.Clamp(value, minimum, maximum),
            Width = 80,
            Font = new Font(UiFontFamily, FontSize),
            Margin = new Padding(1),
        };

        box.ValueChanged += (_, _) => apply((double)box.Value);
        return box;
    }
}