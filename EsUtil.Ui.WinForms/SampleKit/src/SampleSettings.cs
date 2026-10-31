using System.Globalization;

namespace EsUtil.Ui.WinForms.SampleKit;

/// <summary>設定パネルの行を組み立てる（サンプル間で見た目と並びを揃える）。</summary>
/// <remarks>
/// <b>並びの規則</b>: 設定パネルは「プロパティ設定」（対象コントロールの公開プロパティ）と
/// 「サンプル設定」（サンプル固有の操作）の 2 つの領域に分ける。 同じ機能のサンプル（Loupe / Marquee 等）は
/// <b>同じ項目を同じ順で並べる</b>（UI 層が違っても同じ）。
/// </remarks>
public static class SampleSettings
{
    /// <summary>項目名の幅（ピクセル）。</summary>
    public const int LabelWidth = 120;

    /// <summary>編集 UI の幅（ピクセル）。</summary>
    public const int EditorWidth = 180;

    /// <summary>設定パネルの文字サイズ（DIP。 他の UI 層と同じ 12 DIP）。</summary>
    /// <remarks>WinForms の既定フォントは 9pt（= 12 DIP）であるため、明示の指定は行わず本値で意図を残す。</remarks>
    public const double FontSize = 12;

    /// <summary>領域の見出しと中身を縦に並べる。</summary>
    /// <param name="title">領域の見出し。</param>
    /// <param name="rows">中身の行。</param>
    /// <returns>見出しと中身を並べた要素。</returns>
    public static Control Area(string title, params Control[] rows) => Section(title, rows);

    /// <summary>見出しと行を縦に並べる。</summary>
    /// <param name="title">見出し。</param>
    /// <param name="rows">行。</param>
    /// <returns>見出しと行を並べた要素。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> が null の場合。</exception>
    public static Control Section(string title, params Control[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var body = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = new Point(10, 22),
        };
        body.Controls.AddRange(rows);

        return new GroupBox
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(SampleShell.SettingsWidth - 56, 0),
            Controls = { body },
        };
    }

    /// <summary>区切り線を作る。</summary>
    /// <returns>横線。</returns>
    public static Control Separator() => new Panel { Height = 1, Width = SampleShell.SettingsWidth - 70, BackColor = Color.DimGray };

    /// <summary>チェック ボックスの行を作る。</summary>
    /// <param name="label">表示する文言。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>チェック ボックス。</returns>
    public static Control Check(string label, bool initial, Action<bool> apply)
    {
        var check = new CheckBox { Text = label, Checked = initial, AutoSize = true };
        check.CheckedChanged += (_, _) => apply(check.Checked);

        // 初期値を対象へ反映する（対象の既定値とチェックの初期値が違う場合に表示と状態がずれないようにする）
        apply(initial);

        // 行の入れ物で包まずチェック ボックス自身を返す（呼び出し側が状態を操作できるようにする）
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
    /// <param name="scale">
    /// スライダーの刻みに掛ける倍率（既定は 1）。 <see cref="TrackBar"/> は整数しか扱えないため、
    /// 小数の項目は 10 を指定して内部値を 10 倍で持ち、表示と反映は元の単位で行う。
    /// </param>
    /// <returns>項目名・スライダー・現在値を並べた行。</returns>
    public static Control Slider(
        string label, double initial, double minimum, double maximum, double step, bool isInteger, Action<double> apply, double scale = 1.0)
    {
        var value = new Label
        {
            Width = 46,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = Format(initial, isInteger),
        };

        var slider = new TrackBar
        {
            Minimum = (int)Math.Round(minimum * scale),
            Maximum = (int)Math.Round(maximum * scale),
            Value = (int)Math.Round(initial * scale),
            SmallChange = Math.Max(1, (int)Math.Round(step * scale)),
            LargeChange = Math.Max(1, (int)Math.Round((maximum - minimum) * scale / 10)),
            TickFrequency = Math.Max(1, (int)Math.Round(step * scale)),
            Width = EditorWidth,
            Height = 28,
        };
        slider.ValueChanged += (_, _) =>
        {
            var current = slider.Value / scale;
            value.Text = Format(current, isInteger);
            apply(current);
        };

        return Row(label, slider, value);
    }

    /// <summary>数値入力の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="decimalPlaces">小数の桁数（0 は整数のみ）。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <param name="step">増減の単位（0 は桁数から決める。 他 UI の数値入力と同じ刻みにする）。</param>
    /// <returns>項目名と数値入力を並べた行。</returns>
    public static Control Number(
        string label, double initial, double minimum, double maximum, int decimalPlaces, Action<double> apply, double step = 0)
        => NumberRow(label, initial, minimum, maximum, decimalPlaces, apply, step).Element;

    /// <summary>数値入力の行を作る（外から値を更新できる）。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="decimalPlaces">小数の桁数（0 は整数のみ）。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <param name="step">増減の単位（0 は桁数から決める。 他 UI の数値入力と同じ刻みにする）。</param>
    /// <returns>行の要素と、値を外から更新する関数。</returns>
    /// <remarks>
    /// 対象コントロールの状態が利用者の操作で変わるとき（一覧の選択など）は、本メソッドの戻り値で表示を追随させる。<br/>
    /// 更新は入力の変更イベントを発火しない（対象を再設定しない）。
    /// </remarks>
    public static SampleField NumberRow(
        string label, double initial, double minimum, double maximum, int decimalPlaces, Action<double> apply, double step = 0)
    {
        var input = new NumericUpDown
        {
            Minimum = (decimal)minimum,
            Maximum = (decimal)maximum,
            DecimalPlaces = decimalPlaces,
            Increment = step > 0
                ? (decimal)step
                : decimalPlaces == 0 ? 1m : (decimal)Math.Pow(10, -decimalPlaces),
            Value = (decimal)initial,
            Width = EditorWidth,
        };
        var updating = false;
        input.ValueChanged += (_, _) =>
        {
            if (!updating)
            {
                apply((double)input.Value);
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
                    input.Value = (decimal)value;
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
    public static Control Choice(string label, string[] choices, int selectedIndex, Action<int> apply)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = EditorWidth };
        combo.Items.AddRange([.. choices.Cast<object>()]);
        combo.SelectedIndex = selectedIndex;
        combo.SelectedIndexChanged += (_, _) => apply(combo.SelectedIndex);
        return Row(label, combo);
    }

    /// <summary>文字入力の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>項目名と入力欄を並べた行。</returns>
    public static Control Text(string label, string initial, Action<string> apply)
    {
        var input = new TextBox { Text = initial, Width = EditorWidth };
        input.TextChanged += (_, _) => apply(input.Text);
        return Row(label, input);
    }

    /// <summary>ボタンを縦に並べた行を作る。</summary>
    /// <param name="buttons">ボタンの文言と処理。</param>
    /// <returns>ボタンを並べた要素。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buttons"/> が null の場合。</exception>
    public static Control Buttons(params (string Text, Action Action)[] buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);

        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        foreach (var (text, action) in buttons)
        {
            var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            button.Click += (_, _) => action();
            panel.Controls.Add(button);
        }

        return panel;
    }

    /// <summary>項目名と編集 UI を横に並べる。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="controls">編集 UI。</param>
    /// <returns>並べた行。</returns>
    public static Control Row(string label, params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
        };

        row.Controls.Add(new Label
        {
            Text = label,
            Width = LabelWidth,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
        });
        row.Controls.AddRange(controls);
        return row;
    }

    /// <summary>項目名を持たない行（チェック ボックス・ボタン）を作る。</summary>
    /// <param name="controls">並べるコントロール。</param>
    /// <returns>横に並べた要素。</returns>
    /// <remarks>項目名の幅を空けると、長い文言が設定パネルの幅を超えて横スクロールになる。</remarks>
    public static Control BareRow(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
        };

        row.Controls.AddRange(controls);
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
