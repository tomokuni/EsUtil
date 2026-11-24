using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.MewUI.ColorSelector;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Annotation.Presenter.Editor;
using EsUtil.Feature.Annotation.Presenter.Panel;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈の編集 UI で使う行の部品（色・数値・選択肢・見出し・装飾の切り替え）。</summary>
/// <remarks>
/// <b>編集 UI の見た目を 1 か所にする</b>: 書式バーと編集パネル、ホストの設定ペインが同じ部品を使う
/// （項目名の幅・色の解釈・増減の幅が 2 か所でずれない）。<br/>
/// <b>色の選択は <see cref="ColorSelectorControl"/> を使う</b>: 色名・16 進・ピッカーの入力を UI の色選択
/// コントロールが単一所有する（自前の解析を書かない。 保存値は <c>#AARRGGBB</c>）。
/// </remarks>
public static class AnnotationEditorControls
{
    /// <summary>項目名のラベルの幅（DIP）。</summary>
    public const double LabelWidth = AnnotationEditorLayout.LabelWidth;

    /// <summary>行の入力欄の幅（DIP。 値はコアが単一所有する）。</summary>
    /// <remarks>
    /// <b>数値・選択肢・色を同じ幅にする</b>: 幅が違うと行の右端が揃わず、入力部がばらばらに見える。
    /// フライアウトの幅もこの値から決まる（<see cref="AnnotationPanelAppearance.StyleEditor"/>）。
    /// </remarks>
    public const double FieldWidth = AnnotationEditorLayout.FieldWidth;

    /// <summary>色の選択の幅（DIP。 行の入力欄と同じ幅に揃える）。</summary>
    public static double ColorSelectorWidth => FieldWidth;

    /// <summary>見出しを作る。</summary>
    /// <param name="text">見出しの文言。</param>
    /// <returns>見出し。</returns>
    public static UIElement Heading(string text) => new TextBlock
    {
        Text = text,
        FontWeight = FontWeight.Bold,
    };

    /// <summary>項目名のラベルを作る。</summary>
    /// <param name="text">項目名。</param>
    /// <returns>ラベル。</returns>
    public static Element Label(string text) => new TextBlock
    {
        Text = text,
        Width = LabelWidth,
        VerticalTextAlignment = TextAlignment.Center,
    };

    /// <summary>色の選択を作る（項目名とコントロールを横に並べる）。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">現在の色。</param>
    /// <param name="apply">適用する処理。</param>
    /// <param name="textFormat">入力欄へ出す表記（色名 + 不透明度 / カラーコード）。</param>
    /// <returns>色の選択。</returns>
    public static Element ColorRow(
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
    public static Element ColorRow(string label, ColorSelectorControl selector, Action<DrawingColor> apply)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(apply);

        selector.SelectedColorChanged += (_, _) =>
        {
            if (DrawingColorExtensions.TryParseHexText(selector.SelectedColor, out var color))
            {
                apply(color);
            }
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Add(Label(label));
        row.Add(selector);
        return row;
    }

    /// <summary>注釈向けの構成にした色の選択を作る（「代表色」と「ColorPicker」のタブを出す）。</summary>
    /// <param name="initial">現在の色。</param>
    /// <param name="columns">代表色（■ の格子）の列数。</param>
    /// <param name="allPalettes">標準のタブも出すかどうか（設定画面向け）。</param>
    /// <param name="textFormat">入力欄へ出す表記。</param>
    /// <returns>構成した色の選択。</returns>
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

        // 色の入力欄は MewUI の ComboBox が描く（色の選択は内部の ComboBox を公開していない）。
        PaintInputs(selector);
        return selector;
    }

    /// <summary>入力欄の背景（有効時の色）を取得する。</summary>
    /// <value>テーマの入力欄の色。 テーマを解決できない場合（<c>Application.Run</c> 前）は白。</value>
    /// <remarks>
    /// <b>MewUI の ComboBox は既定で灰色</b>である。 白い数値の入力と
    /// 並ぶと無効に見えるため、有効時の入力欄の色（数値の入力と同じ色）で塗る。
    /// </remarks>
    private static Color InputBackground
        => Application.IsRunning ? Application.Current.Theme.Palette.ControlBackground : Color.White;

    /// <summary>含まれる入力欄（<see cref="ComboBox"/>）を有効時の背景で塗る。</summary>
    /// <param name="element">対象の要素。</param>
    private static void PaintInputs(Element element)
    {
        foreach (var combo in FindInputs(element))
        {
            combo.Background = InputBackground;
        }
    }

    /// <summary>数値の入力を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">現在の値。</param>
    /// <param name="minimum">下限。</param>
    /// <param name="maximum">上限。</param>
    /// <param name="apply">適用する処理。</param>
    /// <param name="increment">増減の幅。</param>
    /// <returns>数値の入力。</returns>
    public static Element NumberRow(
        string label, double initial, double minimum, double maximum, Action<double> apply, double increment = 1)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Add(Label(label));

        var box = new NumericUpDown()
            .Minimum(minimum)
            .Maximum(maximum)
            .Step(increment);
        box.Value = initial;
        box.Format = "0.##";
        box.Width = FieldWidth;
        box.OnValueChanged(apply);
        row.Add(box);
        return row;
    }

    /// <summary>選択肢の入力を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="options">選択肢。</param>
    /// <param name="selectedIndex">初期の選択位置。</param>
    /// <param name="apply">適用する処理。</param>
    /// <returns>選択肢の入力。</returns>
    public static Element ChoiceRow(string label, string[] options, int selectedIndex, Action<int> apply)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Add(Label(label));

        var combo = new ComboBox().Items(options);
        combo.Width = FieldWidth;
        combo.Background = InputBackground;
        combo.SelectedIndex = Math.Max(0, selectedIndex);
        combo.OnSelectionChanged(_ =>
        {
            if (combo.SelectedIndex >= 0)
            {
                apply(combo.SelectedIndex);
            }
        });
        row.Add(combo);
        return row;
    }

    /// <summary>装飾の切り替えボタンを作る（本文の書式の B / I / U / S）。</summary>
    /// <param name="text">ボタンの文言。</param>
    /// <param name="isChecked">現在の状態。</param>
    /// <param name="apply">切り替えの処理。</param>
    /// <param name="bold">太字で表示するかどうか。</param>
    /// <param name="italic">斜体で表示するかどうか。</param>
    /// <returns>切り替えボタン。</returns>
    public static Element Toggle(string text, bool isChecked, Action apply, bool bold = false, bool italic = false)
    {
        var content = new TextBlock
        {
            Text = text,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
        };

        var button = new ToggleButton
        {
            Content = content,
            IsChecked = isChecked,
            Width = 36,
        };
        button.OnCheckedChanged(_ => apply());
        return button;
    }

    /// <summary>色を <c>#AARRGGBB</c> の文字列へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns><c>#AARRGGBB</c> 形式の文字列。</returns>
    public static string ToHex(DrawingColor color) => DrawingColorExtensions.ToHexText(color);

    /// <summary>色の選択の中の入力欄の高さを揃える。</summary>
    /// <param name="selector">色の選択。</param>
    /// <param name="height">高さ（DIP）。</param>
    /// <remarks>
    /// <b>外側の高さだけでは中の入力欄が伸びない</b>: MewUI の <see cref="ComboBox"/> は自分の高さを持ち、
    /// 親の高さへ合わせて伸びません。 数値の入力と並べると<b>色の入力欄だけが低く</b>見えます。
    /// 高さの規則（書式バーの項目の高さ）は
    /// コアが単一所有するため、ここでは受け取った値を中の入力欄へも与えます。
    /// </remarks>
    public static void AlignInputHeight(ColorSelectorControl selector, double height)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (height <= 0)
        {
            return;
        }

        foreach (var combo in FindInputs(selector))
        {
            combo.Height = height;
        }
    }

    /// <summary>含まれる入力欄（<see cref="ComboBox"/>）を列挙する。</summary>
    /// <param name="element">対象の要素。</param>
    /// <returns>入力欄。</returns>
    private static IEnumerable<ComboBox> FindInputs(Element element)
    {
        if (element is ComboBox combo)
        {
            yield return combo;
        }

        if (element is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                foreach (var found in FindInputs(child))
                {
                    yield return found;
                }
            }
        }
    }
}
