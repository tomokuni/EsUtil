using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.AvaloniaUI.ColorSelector;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の編集 UI で使う行の部品（色・数値・選択肢・見出し・装飾の切り替え）。</summary>
/// <remarks>
/// <b>編集 UI の見た目を 1 か所にする</b>: 右クリックのフライアウトと、ホストの設定ペイン（既定値の編集）が
/// 同じ部品を使う（項目名の幅・色の解釈・増減の幅が 2 か所でずれない）。<br/>
/// <b>色の選択は <see cref="ColorSelectorControl"/> を使う</b>: 色名・16 進・ピッカーの入力を UI の色選択コントロールが
/// 単一所有する（自前の解析を書かない。 保存値は <c>#AARRGGBB</c>）。
/// </remarks>
public static class AnnotationEditorControls
{
    /// <summary>項目名のラベルの幅（DIP）。</summary>
    public const double LabelWidth = AnnotationEditorLayout.LabelWidth;

    /// <summary>行の入力欄の幅（DIP。 値はコアが単一所有する）。</summary>
    /// <remarks>
    /// <b>数値・選択肢・色を同じ幅にする</b>: 幅が違うと行の右端が揃わず、入力部がばらばらに見える
    /// （実測 2026-11-22）。 フライアウトの幅もこの値から決まる（<see cref="AnnotationPanelAppearance.StyleEditor"/>）。
    /// </remarks>
    public const double FieldWidth = AnnotationEditorLayout.FieldWidth;

    /// <summary>色の選択の幅（DIP。 行の入力欄と同じ幅に揃える）。</summary>
    public static double ColorSelectorWidth => FieldWidth;

    /// <summary>見出しを作る。</summary>
    /// <param name="text">見出しの文言。</param>
    /// <returns>見出し。</returns>
    public static Control Heading(string text) => new TextBlock
    {
        Text = text,
        FontWeight = FontWeight.Bold,
    };

    /// <summary>項目名のラベルを作る。</summary>
    /// <param name="text">項目名。</param>
    /// <returns>ラベル。</returns>
    public static Control Label(string text) => new TextBlock
    {
        Text = text,
        Width = LabelWidth,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>色の選択を作る（項目名とコントロールを横に並べる）。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">現在の色。</param>
    /// <param name="apply">適用する処理。</param>
    /// <param name="textFormat">入力欄へ出す表記（色名 + 不透明度 / カラーコード）。</param>
    /// <returns>色の選択。</returns>
    /// <remarks>
    /// <b>他の項目と同じ横並びにする</b>: 項目名を上へ置くと 1 行を使い、選択の列が縦に間延びする（実測で指摘）。<br/>
    /// <b>注釈向けの構成にする</b>: テーマの色・色名のタブと ColorPicker タブを隠し、代表色 40 色のタブだけを出す
    /// （<see cref="AnnotationPalette.Configure"/>）。
    /// </remarks>
    public static Control ColorRow(
        string label,
        DrawingColor initial,
        Action<DrawingColor> apply,
        ColorTextFormat textFormat = ColorTextFormat.NameWithAlpha)
        => ColorRow(label, CreateSelector(initial, textFormat: textFormat), apply);

    /// <summary>色の選択を作る（項目名とコントロールを横に並べる）。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="selector">色の選択（構成は呼び出し側が決める）。</param>
    /// <param name="apply">適用する処理。</param>
    /// <returns>項目名と色の選択を並べた行。</returns>
    /// <remarks>
    /// <b>構成済みの色の選択を渡せる</b>: 設定画面は「代表色 + 標準のタブ」を出し、
    /// 書式バーは代表色だけを出す（画面ごとにタブの構成が異なる）。 並べ方やタブを差し替えたい呼び出し側は
    /// 先に構成した <paramref name="selector"/> を渡す。
    /// </remarks>
    public static Control ColorRow(string label, ColorSelectorControl selector, Action<DrawingColor> apply)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(apply);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(Label(label));
        selector.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorSelectorControl.SelectedColorProperty
                && EsUtil.Platform.Core.Imaging.DrawingColorExtensions.TryParseHexText(selector.SelectedColor, out var color))
            {
                apply(color);
            }
        };

        row.Children.Add(selector);
        return row;
    }

    /// <summary>注釈向けの構成にした色の選択を作る（「代表色」と「ColorPicker」のタブを出す）。</summary>
    /// <param name="initial">現在の色。</param>
    /// <param name="columns">代表色（■ の格子）の列数。</param>
    /// <param name="allPalettes">代表色に加えて標準のタブ（テーマカラー・色名）も出すかどうか（設定画面向け）。</param>
    /// <param name="textFormat">入力欄へ出す表記（色名 + 不透明度 / カラーコード）。</param>
    /// <returns>構成した色の選択。</returns>
    /// <remarks>
    /// <b>注釈向けの構成にする</b>: フライアウトは代表色（■ の格子）と ColorPicker のタブだけにし（テーマの色・色名を
    /// 隠す）、設定画面は標準のタブも出す（<see cref="AnnotationPalette.Configure"/> /
    /// <see cref="AnnotationPalette.ConfigureAll"/>）。
    /// </remarks>
    public static ColorSelectorControl CreateSelector(
        DrawingColor initial,
        int columns = AnnotationPalette.DefaultPaletteColumns,
        bool allPalettes = false,
        ColorTextFormat textFormat = ColorTextFormat.NameWithAlpha)
    {
        var selector = new ColorSelectorControl
        {
            SelectedColor = ToHex(initial),
            HighlightBorderOnError = true,
            TextFontSize = 12,
            TextFormat = textFormat,
            Width = FieldWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (allPalettes)
        {
            AnnotationPalette.ConfigureAll(selector, columns);
        }
        else
        {
            AnnotationPalette.Configure(selector, columns, showPicker: true);
        }

        return selector;
    }

    /// <summary>数値の入力を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">現在の値。</param>
    /// <param name="minimum">下限。</param>
    /// <param name="maximum">上限。</param>
    /// <param name="apply">適用する処理。</param>
    /// <param name="increment">増減の幅。</param>
    /// <returns>数値の入力。</returns>
    public static Control NumberRow(
        string label, double initial, double minimum, double maximum, Action<double> apply, decimal increment = 1m)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(Label(label));

        var box = new NumericUpDown
        {
            Value = (decimal)initial,
            Minimum = (decimal)minimum,
            Maximum = (decimal)maximum,
            Increment = increment,
            FormatString = "0.##",
            Width = FieldWidth,
        };
        box.ValueChanged += (_, _) =>
        {
            if (box.Value is { } value)
            {
                apply((double)value);
            }
        };
        row.Children.Add(box);
        return row;
    }

    /// <summary>選択肢の入力を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="options">選択肢。</param>
    /// <param name="selectedIndex">初期の選択位置。</param>
    /// <param name="apply">適用する処理。</param>
    /// <returns>選択肢の入力。</returns>
    public static Control ChoiceRow(string label, string[] options, int selectedIndex, Action<int> apply)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(Label(label));

        var combo = new ComboBox
        {
            Width = FieldWidth,
            ItemsSource = options,
            SelectedIndex = Math.Max(0, selectedIndex),
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
            {
                apply(combo.SelectedIndex);
            }
        };
        row.Children.Add(combo);
        return row;
    }

    /// <summary>装飾の切り替えボタンを作る（本文の書式の B / I / U / S）。</summary>
    /// <param name="text">ボタンの文言。</param>
    /// <param name="isChecked">現在の状態。</param>
    /// <param name="apply">切り替えの処理。</param>
    /// <param name="bold">太字で表示するかどうか。</param>
    /// <param name="italic">斜体で表示するかどうか。</param>
    /// <param name="underline">下線を引いて表示するかどうか。</param>
    /// <param name="strike">取り消し線を引いて表示するかどうか。</param>
    /// <returns>切り替えボタン。</returns>
    public static Control Toggle(
        string text, bool isChecked, Action apply,
        bool bold = false, bool italic = false, bool underline = false, bool strike = false)
    {
        var content = new TextBlock
        {
            Text = text,

            // **記号は行の文字より一回り大きくする**（値はコアの AnnotationEditorLayout が単一所有する）
            FontSize = AnnotationEditorLayout.ToggleFontSize,
            FontFamily = new FontFamily(AnnotationEditorLayout.ToggleFontFamily),
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
            RenderTransform = new TranslateTransform(0, 2),
        };

        if (underline)
        {
            content.TextDecorations = TextDecorations.Underline;
        }

        if (strike)
        {
            content.TextDecorations = TextDecorations.Strikethrough;
        }

        var button = new ToggleButton
        {
            Content = content,
            IsChecked = isChecked,
            Width = 36,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        button.IsCheckedChanged += (_, _) => apply();
        return button;
    }

    /// <summary>色を <c>#AARRGGBB</c> の文字列へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns><c>#AARRGGBB</c> 形式の文字列。</returns>
    public static string ToHex(DrawingColor color)
        => EsUtil.Platform.Core.Imaging.DrawingColorExtensions.ToHexText(color);
}
