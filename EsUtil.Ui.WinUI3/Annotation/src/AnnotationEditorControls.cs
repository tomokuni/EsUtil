using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.WinUI3.ColorSelector;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Annotation.Presenter.Editor;
using EsUtil.Feature.StyledText.Presenter.Formatting;
using EsUtil.Feature.Annotation.Presenter.Panel;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>編集 UI の行の部品（書式バーと外観パネルが共有する）。</summary>
/// <remarks>
/// <b>見た目を 1 か所へ集める</b>: 行の組み立てを各パネルへ書き写すと、余白や文字サイズが panel ごとにずれる。
/// </remarks>
internal static class AnnotationEditorControls
{
    /// <summary>行の入力欄の幅（DIP。 値はコアが単一所有する）。</summary>
    /// <remarks>
    /// <b>数値・選択肢・色を同じ幅にする</b>: 幅が違うと行の右端が揃わず、入力部がばらばらに見える。
    /// フライアウトの幅もこの値から決まる（<see cref="AnnotationPanelAppearance.StyleEditor"/>）。
    /// </remarks>
    internal const double FieldWidth = AnnotationEditorLayout.FieldWidth;
    /// <summary>行の文字サイズ（DIP）。</summary>
    internal const double FontSize = AnnotationEditorLayout.FontSizeDip;

    /// <summary>項目間の余白（DIP）。</summary>
    internal const double Gap = AnnotationEditorLayout.GroupSpacing;

    /// <summary>見出しを作る。</summary>
    /// <param name="text">見出しの文字列。</param>
    /// <returns>見出しの要素。</returns>
    internal static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = FontSize,
        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
        Margin = new Thickness(0, 0, 0, 2),
    };

    /// <summary>項目名を作る。</summary>
    /// <param name="text">項目名。</param>
    /// <returns>項目名の要素。</returns>
    internal static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = FontSize,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, Gap, 0),
    };

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

            // **記号は行の文字より一回り大きくする**（値は提示層の AnnotationEditorLayout が単一所有する）
            FontSize = AnnotationEditorLayout.ToggleFontSize,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(AnnotationEditorLayout.ToggleFontFamily),
            FontWeight = bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            FontStyle = italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,

            // **記号自体にも装飾を描く**: 「U」「S」の文字だけでは意味が伝わらない
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,

            // **上下中央へ寄せる**: Consolas の記号は行の上寄りに描かれるため、そのままでは
            // 下へ 2 DIP ずれて見える。 描画だけを動かす（レイアウトは変えない）。
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
            MinWidth = AnnotationEditorLayout.ToggleMinWidth,

            // **高さを明示する**: 既定の高さでは 12 DIP の文字の上が欠ける。
            // 高さは計算層（TextFormatBarLayout）が単一所有する
            MinHeight = TextFormatBarLayout.ItemHeight,
            Height = TextFormatBarLayout.ItemHeight,
            Padding = new Thickness(4, 0, 4, 0),
            FontSize = FontSize,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        button.Checked += (_, _) => apply(true);
        button.Unchecked += (_, _) => apply(false);
        return button;
    }

    /// <summary>色の行（見出し + 色の入力）を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="color">現在の色（<c>#AARRGGBB</c>）。</param>
    /// <param name="textFormat">入力欄の表記。</param>
    /// <param name="apply">色が変わったときに呼ぶ処理。</param>
    /// <returns>行の要素。</returns>
    internal static FrameworkElement ColorRow(
        string label, string color, ColorTextFormat textFormat, Action<string> apply)
    {
        var selector = CreateSelector(color, textFormat);
        selector.SelectedColorChanged += (_, _) => apply(selector.SelectedColor);
        return BuildRow(label, selector);
    }

    /// <summary>色の入力を作る（代表色のタブを出す）。</summary>
    /// <param name="color">現在の色（<c>#AARRGGBB</c>）。</param>
    /// <param name="textFormat">入力欄の表記。</param>
    /// <returns>色の入力。</returns>
    internal static ColorSelectorControl CreateSelector(string color, ColorTextFormat textFormat)
    {
        var selector = new ColorSelectorControl
        {
            SelectedColor = color,
            TextFormat = textFormat,

            // **幅はカラーコードから決める（規則はコアが単一所有する）**: 固定の 240 DIP では、はっきり余る幅が
            // 空いて「不必要に長い」見た目になる。 行の他の入力欄と同じ幅に揃える。
            Width = FieldWidth,
            FontSize = FontSize,
        };

        AnnotationPalette.Configure(selector, showPicker: true);
        return selector;
    }

    /// <summary>数値の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="value">現在の値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="apply">値が変わったときに呼ぶ処理。</param>
    /// <returns>行の要素。</returns>
    internal static FrameworkElement NumberRow(
        string label, double value, double minimum, double maximum, Action<double> apply)
    {
        var box = new NumberBox
        {
            Value = value,
            Minimum = minimum,
            Maximum = maximum,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = FieldWidth,
            FontSize = FontSize,
        };

        box.ValueChanged += (_, e) => apply(double.IsNaN(e.NewValue) ? value : e.NewValue);
        return BuildRow(label, box);
    }

    /// <summary>選択肢の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="options">選択肢。</param>
    /// <param name="selectedIndex">選ぶ位置。</param>
    /// <param name="apply">選ばれたときに呼ぶ処理。</param>
    /// <returns>行の要素。</returns>
    internal static FrameworkElement ChoiceRow(
        string label, IReadOnlyList<string> options, int selectedIndex, Action<int> apply)
    {
        var combo = new ComboBox
        {
            ItemsSource = options,
            SelectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, options.Count - 1)),
            Width = FieldWidth,
            FontSize = FontSize,
        };

        combo.SelectionChanged += (_, _) => apply(combo.SelectedIndex);
        return BuildRow(label, combo);
    }

    /// <summary>項目名と入力欄を 1 行に並べる。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="control">入力欄。</param>
    /// <returns>行の要素。</returns>
    private static FrameworkElement BuildRow(string label, FrameworkElement control)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(0, 0, 0, 2),
        };

        if (label.Length > 0)
        {
            // 項目名の幅を固定する（揃えないと入力欄の横位置が項目ごとにずれる。 幅はコアが単一所有する）
            var caption = Label(label);
            caption.Width = AnnotationEditorLayout.LabelWidth;
            row.Children.Add(caption);
        }

        row.Children.Add(control);
        return row;
    }

    /// <summary>色を保存値（<c>#AARRGGBB</c>）へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>保存値の文字列。</returns>
    internal static string ToHex(DrawingColor color)
        => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>パネルの背景を作る。</summary>
    /// <returns>背景のブラシ。</returns>
    internal static Brush PanelBackground()
        => new SolidColorBrush(Windows.UI.Color.FromArgb(242, 250, 250, 250));

    /// <summary>区切り線を作る。</summary>
    /// <returns>区切り線の要素。</returns>
    internal static Rectangle Divider() => new()
    {
        Height = 1,
        Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 0, 0, 0)),
        Margin = new Thickness(0, 3, 0, 3),
    };
}
