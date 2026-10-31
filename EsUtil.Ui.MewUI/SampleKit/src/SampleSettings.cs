using System.Globalization;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace EsUtil.Ui.MewUI.SampleKit;

/// <summary>設定パネルの行を組み立てる（サンプル間で見た目と並びを揃える）。</summary>
/// <remarks>
/// <b>並びの規則</b>: 設定パネルは「プロパティ設定」（対象コントロールの公開プロパティ）と
/// 「サンプル設定」（サンプル固有の操作）の 2 つの領域に分ける。 同じ機能のサンプル（Loupe / Marquee 等）は
/// <b>同じ項目を同じ順で並べる</b>（UI 層が違っても同じ）。<br/>
/// <b>項目名の幅</b>: ラベルは左の固定幅へ置き、編集 UI を右へ並べる。
/// </remarks>
public static class SampleSettings
{
    /// <summary>項目名の幅（DIP）。</summary>
    public const double LabelWidth = 120;

    /// <summary>編集 UI の幅（DIP）。</summary>
    public const double EditorWidth = 180;

    /// <summary>設定パネルの文字サイズ（DIP。 テーマの既定値と同じ値を明示して UI 層をまたいで揃える）。</summary>
    public const double FontSize = 12;

    /// <summary>領域の見出しと中身を縦に並べる。</summary>
    /// <param name="title">領域の見出し。</param>
    /// <param name="rows">中身の行。</param>
    /// <returns>見出しと中身を並べた要素。</returns>
    public static StackPanel Area(string title, params Element[] rows)
        => Section(title, rows);

    /// <summary>見出しと行を縦に並べる。</summary>
    /// <param name="title">見出し。</param>
    /// <param name="rows">行。</param>
    /// <returns>見出しと行を並べた要素。</returns>
    public static StackPanel Section(string title, params Element[] rows)
    {
        var body = new StackPanel().Vertical().Spacing(6);
        body.Children(rows);

        var section = new StackPanel().Vertical().Spacing(6);
        section.Children(new Label().Text(title).Bold().FontSize(FontSize), body);
        return section;
    }

    /// <summary>区切り線を作る。</summary>
    /// <returns>横線。</returns>
    public static Border Separator()
        => new() { Height = 1, Background = Color.FromRgb(0x50, 0x50, 0x50) };

    /// <summary>チェック ボックスの行を作る。</summary>
    /// <param name="label">表示する文言。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>チェック ボックス。</returns>
    public static FrameworkElement Check(string label, bool initial, Action<bool> apply)
    {
        var check = new CheckBox().IsChecked(initial).Content(label).FontSize(FontSize);
        check.OnCheckedChanged(apply);

        // 初期値を対象へ反映する（対象の既定値とチェックの初期値が違う場合に表示と状態がずれないようにする）
        apply(initial);
        return check;
    }

    /// <summary>スライダーの行を作る（現在値を数値で表示する）。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="step">増減の単位。</param>
    /// <param name="isInteger">整数のみを受け付けるかどうか。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>項目名・スライダー・現在値を並べた行。</returns>
    public static FrameworkElement Slider(
        string label, double initial, double minimum, double maximum, double step, bool isInteger, Action<double> apply)
    {
        var value = new TextBlock().Width(52).VerticalAlignment(VerticalAlignment.Center).FontSize(FontSize);
        value.Text = Format(initial, isInteger);

        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = initial,
            Width = EditorWidth,
            VerticalAlignment = VerticalAlignment.Center,
        };
        slider.SmallChange(step);
        slider.FontSize(FontSize);
        slider.ValueChanged += changed =>
        {
            value.Text = Format(changed, isInteger);
            apply(changed);
        };

        return Row(label, new StackPanel().Horizontal().Spacing(8).Children(slider, value));
    }

    /// <summary>数値入力の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="step">増減の単位。</param>
    /// <param name="isInteger">整数のみを受け付けるかどうか。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>項目名と数値入力を並べた行。</returns>
    public static FrameworkElement Number(
        string label, double initial, double minimum, double maximum, double step, bool isInteger, Action<double> apply)
    {
        var input = new NumericUpDown()
            .Minimum(minimum)
            .Maximum(maximum)
            .Step(step)
            .IsInteger(isInteger)
            .Width(EditorWidth);
        input.Value = initial;
        input.FontSize(FontSize);
        input.OnValueChanged(apply);
        return Row(label, input);
    }

    /// <summary>選択肢の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="choices">選択肢の名前。</param>
    /// <param name="selectedIndex">初期の選択インデックス。</param>
    /// <param name="apply">変更の反映先（選択インデックス）。</param>
    /// <returns>項目名とコンボ ボックスを並べた行。</returns>
    public static FrameworkElement Choice(string label, string[] choices, int selectedIndex, Action<int> apply)
    {
        var combo = new ComboBox().Items(choices).FontSize(FontSize);
        combo.SelectedIndex = selectedIndex;
        combo.OnSelectionChanged(_ => apply(combo.SelectedIndex));
        return Row(label, combo);
    }

    /// <summary>文字入力の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>項目名と入力欄を並べた行。</returns>
    public static FrameworkElement Text(string label, string initial, Action<string> apply)
    {
        var input = new TextBox { Text = initial, Width = EditorWidth };
        input.FontSize(FontSize);
        input.TextChanged += text => apply(text);
        return Row(label, input);
    }

    /// <summary>ボタンを縦に並べた行を作る。</summary>
    /// <param name="buttons">ボタンの文言と処理。</param>
    /// <returns>ボタンを並べた要素。</returns>
    public static FrameworkElement Buttons(params (string Text, Action Action)[] buttons)
    {
        var panel = new StackPanel().Vertical().Spacing(6);
        foreach (var (text, action) in buttons)
        {
            var button = new Button().Content(text).OnClick(action);
            button.Cursor(CursorType.Hand);
            button.FontSize(FontSize);
            panel.Children(button);
        }

        return panel;
    }

    /// <summary>項目名と編集 UI を横に並べる。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="control">編集 UI。</param>
    /// <returns>並べた行。</returns>
    public static FrameworkElement Row(string label, Element control)
        => new StackPanel().Horizontal().Spacing(8).Children(
            new Label().Text(label).Width(LabelWidth).VerticalTextAlignment(TextAlignment.Center).FontSize(FontSize),
            control);

    /// <summary>値を表示用の文字列へ書式化する。</summary>
    /// <param name="value">値。</param>
    /// <param name="isInteger">整数のみを受け付けるかどうか。</param>
    /// <returns>表示用の文字列。</returns>
    private static string Format(double value, bool isInteger)
        => isInteger
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);
}
