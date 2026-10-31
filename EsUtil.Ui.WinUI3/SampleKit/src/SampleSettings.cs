using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace EsUtil.Ui.WinUI3.SampleKit;

/// <summary>設定パネルの行を組み立てる（サンプル間で見た目と並びを揃える）。</summary>
/// <remarks>
/// <b>並びの規則</b>: 設定パネルは「プロパティ設定」（対象コントロールの公開プロパティ）と
/// 「サンプル設定」（サンプル固有の操作）の 2 つの領域に分ける。 同じ機能のサンプル（Loupe / Marquee 等）は
/// <b>同じ項目を同じ順で並べる</b>（UI 層が違っても同じ）。
/// </remarks>
public static class SampleSettings
{
    /// <summary>項目名の幅（DIP）。</summary>
    public const double LabelWidth = 120;

    /// <summary>編集 UI の幅（DIP）。</summary>
    public const double EditorWidth = 180;

    /// <summary>設定パネルの文字サイズ（DIP。 MewUI のテーマ既定値と同じにして UI 層をまたいで揃える）。</summary>
    public const double FontSize = 12;

    /// <summary>領域の見出しと中身を縦に並べる。</summary>
    /// <param name="title">領域の見出し。</param>
    /// <param name="rows">中身の行。</param>
    /// <returns>見出しと中身を並べた要素。</returns>
    public static UIElement Area(string title, params UIElement[] rows) => Section(title, rows);

    /// <summary>見出しと行を縦に並べる。</summary>
    /// <param name="title">見出し。</param>
    /// <param name="rows">行。</param>
    /// <returns>見出しと行を並べた要素。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> が null の場合。</exception>
    public static UIElement Section(string title, params UIElement[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var body = new StackPanel { Spacing = 6 };
        foreach (var row in rows)
        {
            body.Children.Add(row);
        }

        var section = new StackPanel { Spacing = 6 };
        section.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = FontSize });
        section.Children.Add(body);
        return section;
    }

    /// <summary>区切り線を作る。</summary>
    /// <returns>横線。</returns>
    public static UIElement Separator()
        => new Border { Height = 1, Background = new SolidColorBrush(Microsoft.UI.Colors.DimGray) };

    /// <summary>チェック ボックスの行を作る。</summary>
    /// <param name="label">表示する文言。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>チェック ボックス。</returns>
    public static UIElement Check(string label, bool initial, Action<bool> apply)
    {
        var check = new CheckBox { Content = label, IsChecked = initial, FontSize = FontSize };
        check.Checked += (_, _) => apply(true);
        check.Unchecked += (_, _) => apply(false);

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
    public static UIElement Slider(
        string label, double initial, double minimum, double maximum, double step, bool isInteger, Action<double> apply)
    {
        var value = new TextBlock
        {
            Width = 52,
            VerticalAlignment = VerticalAlignment.Center,
            Text = Format(initial, isInteger),
            FontSize = FontSize,
        };

        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = initial,
            Width = EditorWidth,
            FontSize = FontSize,
            StepFrequency = step,
            VerticalAlignment = VerticalAlignment.Center,
        };
        slider.ValueChanged += (_, e) =>
        {
            value.Text = Format(e.NewValue, isInteger);
            apply(e.NewValue);
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(slider);
        row.Children.Add(value);
        return Row(label, row);
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
    public static UIElement Number(
        string label, double initial, double minimum, double maximum, double step, bool isInteger, Action<double> apply)
        => NumberRow(label, initial, minimum, maximum, step, isInteger, apply).Element;

    /// <summary>数値入力の行を作る（外から値を更新できる）。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="step">増減の単位。</param>
    /// <param name="isInteger">整数のみを受け付けるかどうか。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>行の要素と、値を外から更新する関数。</returns>
    /// <remarks>
    /// 対象コントロールの状態が利用者の操作で変わるとき（一覧の選択など）は、本メソッドの戻り値で表示を追随させる。<br/>
    /// 更新は入力の変更イベントを発火しない（対象を再設定しない）。
    /// </remarks>
    public static SampleField NumberRow(
        string label, double initial, double minimum, double maximum, double step, bool isInteger, Action<double> apply)
    {
        var input = new NumberBox
        {
            Minimum = minimum,
            Maximum = maximum,
            SmallChange = step,
            Value = initial,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = EditorWidth,
            FontSize = FontSize,
        };
        var updating = false;
        input.ValueChanged += (_, e) =>
        {
            if (!updating && !double.IsNaN(e.NewValue))
            {
                apply(isInteger ? Math.Round(e.NewValue) : e.NewValue);
            }
        };

        return new SampleField
        {
            Element = Row(label, input),
            SetValue = value =>
            {
                updating = true;
                try
                {
                    input.Value = value;
                }
                finally
                {
                    updating = false;
                }
            },
        };
    }

    /// <summary>選択肢の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="choices">選択肢の名前。</param>
    /// <param name="selectedIndex">初期の選択インデックス。</param>
    /// <param name="apply">変更の反映先（選択インデックス）。</param>
    /// <returns>項目名とコンボ ボックスを並べた行。</returns>
    public static UIElement Choice(string label, string[] choices, int selectedIndex, Action<int> apply)
    {
        var combo = new ComboBox { Width = EditorWidth, FontSize = FontSize };
        foreach (var choice in choices)
        {
            combo.Items.Add(choice);
        }

        combo.SelectedIndex = selectedIndex;
        combo.SelectionChanged += (_, _) => apply(combo.SelectedIndex);
        return Row(label, combo);
    }

    /// <summary>文字入力の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>項目名と入力欄を並べた行。</returns>
    public static UIElement Text(string label, string initial, Action<string> apply)
    {
        var input = new TextBox { Text = initial, Width = EditorWidth, FontSize = FontSize };
        input.TextChanged += (_, _) => apply(input.Text);
        return Row(label, input);
    }

    /// <summary>ボタンを縦に並べた行を作る。</summary>
    /// <param name="buttons">ボタンの文言と処理。</param>
    /// <returns>ボタンを並べた要素。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buttons"/> が null の場合。</exception>
    public static UIElement Buttons(params (string Text, Action Action)[] buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);

        var panel = new StackPanel { Spacing = 6 };
        foreach (var (text, action) in buttons)
        {
            var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = FontSize };
            button.Click += (_, _) => action();
            panel.Children.Add(button);
        }

        return panel;
    }

    /// <summary>項目名と編集 UI を横に並べる。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="control">編集 UI。</param>
    /// <returns>並べた行。</returns>
    public static UIElement Row(string label, UIElement control)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Width = LabelWidth,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontSize = FontSize,
        });
        row.Children.Add(control);
        return row;
    }

    /// <summary>値を表示用の文字列へ書式化する。</summary>
    /// <param name="value">値。</param>
    /// <param name="isInteger">整数のみを受け付けるかどうか。</param>
    /// <returns>表示用の文字列。</returns>
    private static string Format(double value, bool isInteger)
        => isInteger
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);
}
