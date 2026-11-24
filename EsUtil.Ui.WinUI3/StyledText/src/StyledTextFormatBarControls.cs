using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace EsUtil.Ui.WinUI3.StyledText;

/// <summary>書式バーの部品（装飾の切り替え）。</summary>
/// <remarks>
/// <b>見た目を 1 か所へ集める</b>: 余白や文字サイズがバーと外観パネルでずれるのを防ぐ。
/// 値の所有者は計算層（<see cref="TextEditorLayout"/>）である。
/// </remarks>
internal static class StyledTextFormatBarControls
{
    /// <summary>行の文字サイズ（DIP）。</summary>
    internal const double FontSize = TextEditorLayout.FontSizeDip;

    /// <summary>装飾の切り替え（B / I / U / S）を作る。</summary>
    /// <param name="text">ボタンの文字。</param>
    /// <param name="isChecked">押された状態にする場合は <see langword="true"/>。</param>
    /// <param name="apply">押された状態が変わったときに呼ぶ処理。</param>
    /// <param name="bold">太字で表示する場合は <see langword="true"/>。</param>
    /// <param name="italic">斜体で表示する場合は <see langword="true"/>。</param>
    /// <param name="underline">下線を引く場合は <see langword="true"/>。</param>
    /// <param name="strike">取り消し線を引く場合は <see langword="true"/>。</param>
    /// <returns>切り替えボタン。</returns>
    internal static ToggleButton Toggle(
        string text,
        bool isChecked,
        Action<bool> apply,
        bool bold = false,
        bool italic = false,
        bool underline = false,
        bool strike = false)
    {
        var decorations = Windows.UI.Text.TextDecorations.None;
        if (underline)
        {
            decorations |= Windows.UI.Text.TextDecorations.Underline;
        }

        if (strike)
        {
            decorations |= Windows.UI.Text.TextDecorations.Strikethrough;
        }

        var content = new TextBlock
        {
            Text = text,
            FontSize = TextEditorLayout.ToggleFontSize,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(TextEditorLayout.ToggleFontFamily),
            FontWeight = bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            FontStyle = italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform { Y = -1 },
        };

        if (decorations != Windows.UI.Text.TextDecorations.None)
        {
            content.TextDecorations = decorations;
        }

        var button = new ToggleButton
        {
            Content = content,
            IsChecked = isChecked,
            MinWidth = TextEditorLayout.ToggleMinWidth,
            MinHeight = TextFormatBarLayout.ItemHeight,
            Height = TextFormatBarLayout.ItemHeight,
            Padding = new Thickness(4, 0, 4, 0),
            FontSize = FontSize,
            VerticalContentAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
            HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
        };

        button.Checked += (_, _) => apply(true);
        button.Unchecked += (_, _) => apply(false);
        return button;
    }
}