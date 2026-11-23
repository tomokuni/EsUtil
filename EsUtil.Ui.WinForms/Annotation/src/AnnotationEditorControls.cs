namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>編集 UI の部品（書式バーと外観パネルが共有する）。</summary>
/// <remarks><b>見た目を 1 か所へ集める</b>（余白や文字サイズが panel ごとにずれるのを防ぐ）。</remarks>
internal static class AnnotationEditorControls
{
    /// <summary>行の文字サイズ（ポイント）。</summary>
    internal const float FontSize = 9F;

    /// <summary>装飾の切り替え（B / I / U / S）の文字サイズ（ポイント）。</summary>
    /// <remarks>
    /// <b>行の文字より一回り大きくする</b>: 記号 1 文字だけのボタンは、行の文字と同じ大きさだと
    /// 線（下線・取り消し線）と字面が小さく、押せる場所としても見えにくい（実測 2026-11-22）。
    /// コアの値（14 DIP）をポイントへ換算します（1 DIP = 0.75 pt）。
    /// </remarks>
    internal const float ToggleFontSize = (float)(AnnotationEditorLayout.ToggleFontSize * 0.75);

    /// <summary>編集 UI の文字に使うフォント名を取得する。</summary>
    /// <remarks>本文の既定フォント（<see cref="AnnotationDefaults.PreferredFontFamilies"/> の先頭）と揃える。</remarks>
    internal static string UiFontFamily => AnnotationDefaults.PreferredFontFamilies[0];

    /// <summary>項目間の余白（ピクセル）。</summary>
    internal const int Gap = (int)AnnotationEditorLayout.RowSpacing;

    /// <summary>項目名のラベルを作る。</summary>
    /// <param name="text">項目名。</param>
    /// <returns>ラベル。</returns>
    internal static Label Label(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font(UiFontFamily, FontSize),
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, Gap, Gap, 0),
    };

    /// <summary>見出しのラベルを作る。</summary>
    /// <param name="text">見出し。</param>
    /// <returns>ラベル。</returns>
    internal static Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font(UiFontFamily, FontSize, FontStyle.Bold),
        Margin = new Padding(0, Gap, Gap, 0),
    };

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
                AnnotationEditorLayout.ToggleFontFamily,
                ToggleFontSize,
                bold ? FontStyle.Bold : italic ? FontStyle.Italic : FontStyle.Regular),
            Margin = new Padding(1),
        };

        button.CheckedChanged += (_, _) => apply(button.Checked);
        return button;
    }

    /// <summary>色のボタンを作る（押すと代表色のメニューが開く）。</summary>
    /// <param name="color">現在の色。</param>
    /// <param name="apply">色が選ばれたときに呼ぶ処理。</param>
    /// <returns>色のボタン。</returns>
    internal static Button ColorButton(Color color, Action<Color> apply)
    {
        var button = new Button
        {
            Width = (int)Math.Round(AnnotationEditorLayout.FieldWidth),
            Height = 24,
            BackColor = color,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(1),
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
        button.Click += (_, _) => ShowColorMenu(button, color, apply);
        return button;
    }

    /// <summary>代表色のメニューを開く（任意の色は色ダイアログで選ぶ）。</summary>
    /// <param name="owner">メニューを開くボタン。</param>
    /// <param name="current">現在の色。</param>
    /// <param name="apply">色が選ばれたときに呼ぶ処理。</param>
    private static void ShowColorMenu(Button owner, Color current, Action<Color> apply)
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        foreach (var (name, value) in AnnotationPalette.RepresentativeColors)
        {
            var item = new ToolStripMenuItem(name) { BackColor = value };
            item.Click += (_, _) =>
            {
                owner.BackColor = value;
                apply(value);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        var more = new ToolStripMenuItem("その他の色...");
        more.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = current, FullOpen = true };
            if (dialog.ShowDialog(owner) != DialogResult.OK)
            {
                return;
            }

            owner.BackColor = dialog.Color;
            apply(dialog.Color);
        };
        menu.Items.Add(more);
        menu.Show(owner, new Point(0, owner.Height));
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
            Width = (int)Math.Round(AnnotationEditorLayout.FieldWidth),
            Font = new Font(UiFontFamily, FontSize),
            Margin = new Padding(1),
        };

        box.ValueChanged += (_, _) => apply((double)box.Value);
        return box;
    }
}
