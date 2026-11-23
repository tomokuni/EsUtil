
namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈の外観（枠線・内側・角丸・しっぽ・重なり順）を編集するパネル（WinForms 版）。</summary>
/// <remarks>
/// <b>選択中の注釈だけに効く</b>（設定画面の既定値は新しく追加する注釈へ効く）。<br/>
/// <b>本文の書式は書式バーが担う</b>（このパネルは外観と重なり順だけを扱う）。
/// </remarks>
internal sealed class AnnotationStylePanel : Panel
{
    /// <summary>対象のレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>パネルを閉じる処理。</summary>
    private readonly Action _close;

    /// <summary>外観パネルを初期化する。</summary>
    /// <param name="layer">対象のレイヤー。</param>
    /// <param name="close">パネルを閉じる処理。</param>
    /// <param name="appearance">パネルの外観（下地の色。 コアが単一所有する）。</param>
    internal AnnotationStylePanel(
        AnnotationLayerControl layer, Action close, AnnotationPanelAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        _layer = layer;
        _close = close;
        Width = 236;
        Height = 320;

        // 下地の色はコアの外観に合わせる（他の UI 版と同じ白）。
        // WinForms は枠を 1 本しか描けないため、枠は FixedSingle のままにする
        BackColor = Color.FromArgb(
            appearance.Background.A, appearance.Background.R, appearance.Background.G, appearance.Background.B);
        BorderStyle = BorderStyle.FixedSingle;
        AutoScroll = true;

        Build();
    }

    /// <summary>パネルの中身を組み立てる。</summary>
    private void Build()
    {
        var y = 6;
        Add(AnnotationEditorControls.Heading(AnnotationEditorText.AppearanceHeading), ref y);

        if (_layer.Selected is not { } item)
        {
            Add(AnnotationEditorControls.Label(AnnotationEditorText.NoSelection), ref y);
            return;
        }

        var appearance = item.Appearance;
        AddColor(AnnotationEditorText.BorderColor, appearance.Border, ref y,
            value => _layer.UpdateSelectedAppearance(current => current with { Border = value }));
        AddNumber(AnnotationEditorText.BorderThickness, appearance.BorderThickness, AnnotationLimits.BorderThicknessMin, AnnotationLimits.BorderThicknessMax, ref y,
            value => _layer.UpdateSelectedAppearance(current => current with { BorderThickness = value }));
        AddColor(AnnotationEditorText.FillColor, appearance.Fill, ref y,
            value => _layer.UpdateSelectedAppearance(current => current with { Fill = value }));
        AddNumber(AnnotationEditorText.CornerRadius, appearance.CornerRadius, 0, AnnotationLimits.CornerRadiusMax, ref y,
            value => _layer.UpdateSelectedAppearance(current => current with { CornerRadius = value }));

        if (item is BubbleAnnotation bubble)
        {
            Add(AnnotationEditorControls.Heading(AnnotationEditorText.BubbleHeading), ref y);
            AddNumber(AnnotationEditorText.BodyPadding, bubble.BodyPadding, 0, AnnotationLimits.BodyPaddingMax, ref y,
                value => _layer.SetSelectedBodyPadding(value));
            AddNumber(AnnotationEditorText.TailWidth, bubble.Tail.Width, AnnotationLimits.TailWidthMin, AnnotationLimits.TailWidthMax, ref y,
                value => _layer.UpdateSelectedTail(tail => tail with { Width = value }));
            AddChoice(AnnotationBubbleEdgeCatalog.Label, [.. AnnotationBubbleEdgeCatalog.Options], (int)bubble.Tail.BaseEdge, ref y,
                value => _layer.UpdateSelectedTail(tail => tail with { BaseEdge = (BubbleEdge)value }));
        }

        var front = new Button
        {
            Text = AnnotationEditorText.MoveToFront,
            Width = (int)Math.Round(AnnotationEditorLayout.FieldWidth),
            Height = 24,
            Location = new Point(6, y),
            Font = new Font(AnnotationEditorControls.UiFontFamily, AnnotationEditorControls.FontSize),
        };
        front.Click += (_, _) => _layer.MoveSelectedToFront();
        Controls.Add(front);

        var back = new Button
        {
            Text = AnnotationEditorText.MoveToBack,
            Width = (int)Math.Round(AnnotationEditorLayout.FieldWidth),
            Height = 24,
            Location = new Point(110, y),
            Font = new Font(AnnotationEditorControls.UiFontFamily, AnnotationEditorControls.FontSize),
        };
        back.Click += (_, _) => _layer.MoveSelectedToBack();
        Controls.Add(back);
        y += 28;

        var close = new Button
        {
            Text = "閉じる",
            Width = (int)Math.Round(AnnotationEditorLayout.FieldWidth),
            Height = 24,
            Location = new Point(6, y),
            Font = new Font(AnnotationEditorControls.UiFontFamily, AnnotationEditorControls.FontSize),
        };
        close.Click += (_, _) => _close();
        Controls.Add(close);
    }

    /// <summary>入力欄の左端（ピクセル。 項目名の幅と行の間隔から決める）。</summary>
    private static readonly int FieldLeft =
        (int)Math.Round(AnnotationEditorLayout.LabelWidth + AnnotationEditorLayout.RowSpacing);
    /// <summary>色の行を追加する。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="color">現在の色。</param>
    /// <param name="y">追加する Y 座標（追加後に進める）。</param>
    /// <param name="apply">色が選ばれたときに呼ぶ処理。</param>
    private void AddColor(string label, Color color, ref int y, Action<Color> apply)
    {
        Add(AnnotationEditorControls.Label(label), ref y, new Point(6, y));
        var button = AnnotationEditorControls.ColorButton(color, apply);
        button.Location = new Point(FieldLeft, y - 22);
        Controls.Add(button);
    }

    /// <summary>数値の行を追加する。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="value">現在の値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="y">追加する Y 座標（追加後に進める）。</param>
    /// <param name="apply">値が変わったときに呼ぶ処理。</param>
    private void AddNumber(string label, double value, double minimum, double maximum, ref int y, Action<double> apply)
    {
        Add(AnnotationEditorControls.Label(label), ref y, new Point(6, y));
        var box = AnnotationEditorControls.NumberBox(value, minimum, maximum, apply);
        box.Location = new Point(FieldLeft, y - 22);
        Controls.Add(box);
    }

    /// <summary>選択肢の行を追加する。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="options">選択肢。</param>
    /// <param name="selectedIndex">選ぶ位置。</param>
    /// <param name="y">追加する Y 座標（追加後に進める）。</param>
    /// <param name="apply">選ばれたときに呼ぶ処理。</param>
    private void AddChoice(string label, string[] options, int selectedIndex, ref int y, Action<int> apply)
    {
        Add(AnnotationEditorControls.Label(label), ref y, new Point(6, y));
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = (int)Math.Round(AnnotationEditorLayout.FieldWidth),
            Location = new Point(FieldLeft, y - 22),
            Font = new Font(AnnotationEditorControls.UiFontFamily, AnnotationEditorControls.FontSize),
        };

        foreach (var option in options)
        {
            combo.Items.Add(option);
        }

        combo.SelectedIndex = Math.Clamp(selectedIndex, 0, options.Length - 1);
        combo.SelectedIndexChanged += (_, _) => apply(combo.SelectedIndex);
        Controls.Add(combo);
    }

    /// <summary>要素を追加して Y 座標を進める。</summary>
    /// <param name="control">追加する要素。</param>
    /// <param name="y">現在の Y 座標（追加後に進める）。</param>
    private void Add(Control control, ref int y)
    {
        control.Location = new Point(6, y);
        Controls.Add(control);
        y += control.Height + 2;
    }

    /// <summary>要素を指定位置へ追加して Y 座標を進める。</summary>
    /// <param name="control">追加する要素。</param>
    /// <param name="y">現在の Y 座標（追加後に進める）。</param>
    /// <param name="location">追加する位置。</param>
    private void Add(Control control, ref int y, Point location)
    {
        control.Location = location;
        Controls.Add(control);
        y += 24;
    }
}
