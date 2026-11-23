using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Ui.AvaloniaUI.ColorSelector;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Sample;

/// <summary>既定値（新規作成時の外観・書式）と全体設定を編集する左側のペイン。</summary>
/// <remarks>
/// <b>既定値はコントロールの <c>DraftDefaults</c> へ書き戻す</b>: 既存の注釈は変わらず、これから作る注釈にだけ効く。
/// 個々の注釈の編集は、選択して右クリック（<c>AnnotationLayerControl</c> のフライアウト）で行う。<br/>
/// <b>色の選択は UI の色選択コントロール</b>（<c>ColorSelectorControl</c>）を使う。 行の組み立ては
/// <c>AnnotationEditorControls</c> の部品を共有する（同じ見た目・同じ解釈にするため）。
/// </remarks>
internal sealed class SettingsPane : StackPanel
{
    /// <summary>本文の表示と書式バーをまとめたコントロール。</summary>
    private readonly AnnotationTextEditor _editor;

    /// <summary>編集対象のコントロール。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>現在の既定値（編集のたびに新しい値へ置き換える）。</summary>
    private AnnotationDraftDefaults _defaults = AnnotationDraftDefaults.Default;

    /// <summary>この設定ペインが作った色の選択（並べ方の変更をまとめて反映する）。</summary>
    private readonly List<ColorSelectorControl> _selectors = [];

    /// <summary>代表色（■ の格子）の列数（設定ペインのすべての色の選択が使う）。</summary>
    private int _columns = AnnotationPalette.DefaultPaletteColumns;

    /// <summary>ペインを初期化する。</summary>
    /// <param name="editor">既定値と書式バーの設定を適用するコントロール。</param>
    public SettingsPane(AnnotationTextEditor editor)
    {
        _editor = editor;
        _layer = editor.Layer;
        Width = 320;
        Spacing = 6;
        Margin = new Thickness(8);
        Build();
    }

    /// <summary>ペインの中身を組み立てる。</summary>
    private void Build()
    {
        Children.Add(AnnotationEditorControls.Heading("既定値（新規作成時に使う）"));

        Children.Add(SubHeading("枠線"));
        Children.Add(ColorRow("枠線の色", _defaults.FrameAppearance.Border,
            color => SetFrame(_defaults.FrameAppearance with { Border = color })));
        Children.Add(AnnotationEditorControls.NumberRow("枠線の太さ", _defaults.FrameAppearance.BorderThickness,
            AnnotationLimits.BorderThicknessMin, AnnotationLimits.BorderThicknessMax,
            value => SetFrame(_defaults.FrameAppearance with { BorderThickness = value })));
        Children.Add(ColorRow("内側の色", _defaults.FrameAppearance.Fill,
            color => SetFrame(_defaults.FrameAppearance with { Fill = color })));
        Children.Add(AnnotationEditorControls.NumberRow("角丸", _defaults.FrameAppearance.CornerRadius,
            0, AnnotationLimits.CornerRadiusMax,
            value => SetFrame(_defaults.FrameAppearance with { CornerRadius = value })));

        Children.Add(SubHeading("吹き出し"));
        Children.Add(ColorRow("枠線の色", _defaults.BubbleAppearance.Border,
            color => SetBubble(_defaults.BubbleAppearance with { Border = color })));
        Children.Add(AnnotationEditorControls.NumberRow("枠線の太さ", _defaults.BubbleAppearance.BorderThickness,
            AnnotationLimits.BorderThicknessMin, AnnotationLimits.BorderThicknessMax,
            value => SetBubble(_defaults.BubbleAppearance with { BorderThickness = value })));
        Children.Add(ColorRow("内側の色", _defaults.BubbleAppearance.Fill,
            color => SetBubble(_defaults.BubbleAppearance with { Fill = color })));
        Children.Add(AnnotationEditorControls.NumberRow("角丸", _defaults.BubbleAppearance.CornerRadius,
            0, AnnotationLimits.CornerRadiusMax,
            value => SetBubble(_defaults.BubbleAppearance with { CornerRadius = value })));
        Children.Add(AnnotationEditorControls.NumberRow("本文の余白", _defaults.BodyPadding,
            0, AnnotationLimits.BodyPaddingMax,
            value => SetDefaults(_defaults with { BodyPadding = value })));
        Children.Add(AnnotationEditorControls.NumberRow("しっぽの幅", _defaults.TailWidth,
            AnnotationLimits.TailWidthMin, AnnotationLimits.TailWidthMax,
            value => SetDefaults(_defaults with { TailWidth = value })));

        Children.Add(SubHeading("本文の書式"));
        Children.Add(AnnotationEditorControls.ChoiceRow(
            "フォント",
            [.. AnnotationFonts.All],
            Math.Max(0, AnnotationFonts.All.ToList().IndexOf(_defaults.TextStyle.FontFamily)),
            index => SetText(_defaults.TextStyle with { FontFamily = AnnotationFonts.All[index] })));
        Children.Add(ColorRow("文字色", _defaults.TextStyle.Color,
            color => SetText(_defaults.TextStyle with { Color = color })));
        Children.Add(AnnotationEditorControls.NumberRow("大きさ", _defaults.TextStyle.FontSize,
            AnnotationLimits.FontSizeMin, AnnotationLimits.FontSizeMax,
            value => SetText(_defaults.TextStyle with { FontSize = value })));
        Children.Add(Decorations());

        Children.Add(SubHeading("全体"));
        Children.Add(AnnotationEditorControls.NumberRow("行間の倍率", _layer.LineHeightFactor, 0.8, 3.0,
            value => _layer.LineHeightFactor = value, increment: 0.05m));
        Children.Add(AnnotationEditorControls.NumberRow("ハンドルの掴む半径", _layer.HandleHitRadius, 2, 64,
            value => _layer.HandleHitRadius = value));

        Children.Add(SubHeading("書式バー"));
        Children.Add(AnnotationEditorControls.NumberRow(
            "代表色の列数", _columns, 1, 16, value => SetColumns((int)Math.Round(value))));

        var edit = new Button { Content = "選択中の注釈を編集（右クリックと同じ）" };
        edit.Click += (_, _) => _layer.ShowStyleEditor();
        Children.Add(edit);
    }

    /// <summary>枠線の既定値を差し替える。</summary>
    /// <param name="appearance">新しい外観。</param>
    private void SetFrame(AnnotationAppearance appearance)
        => SetDefaults(_defaults with { FrameAppearance = appearance });

    /// <summary>色の行を作る（このペインの色の選択は代表色と標準のタブを出す）。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">現在の色。</param>
    /// <param name="apply">適用する処理。</param>
    /// <returns>項目名と色の選択を並べた行。</returns>
    /// <remarks>
    /// <b>設定ペインの色はすべて同じ構成</b>にする（代表色 + テーマカラー・色名・カラーピッカー）。
    /// 列数は「書式バー」の設定に合わせ、変更したら全行へ反映する（<see cref="SetColumns"/>）。
    /// </remarks>
    private Control ColorRow(string label, DrawingColor initial, Action<DrawingColor> apply)
    {
        var selector = AnnotationEditorControls.CreateSelector(initial, _columns, allPalettes: true);
        _selectors.Add(selector);
        return AnnotationEditorControls.ColorRow(label, selector, apply);
    }

    /// <summary>代表色の列数を変え、このペインの色の選択と書式バーへ反映する。</summary>
    /// <param name="columns">新しい列数（1 以上）。</param>
    private void SetColumns(int columns)
    {
        _columns = columns;
        _editor.PaletteColumns = columns;
        foreach (var selector in _selectors)
        {
            AnnotationPalette.ConfigureAll(selector, columns);
        }
    }

    /// <summary>吹き出しの既定値を差し替える。</summary>
    /// <param name="appearance">新しい外観。</param>
    private void SetBubble(AnnotationAppearance appearance)
        => SetDefaults(_defaults with { BubbleAppearance = appearance });

    /// <summary>本文の既定の書式を差し替える。</summary>
    /// <param name="style">新しい書式。</param>
    private void SetText(AnnotationTextStyle style)
        => SetDefaults(_defaults with { TextStyle = style });

    /// <summary>既定値を差し替えてコントロールへ反映する。</summary>
    /// <param name="defaults">新しい既定値。</param>
    private void SetDefaults(AnnotationDraftDefaults defaults)
    {
        _defaults = defaults;
        _layer.DraftDefaults = defaults;
    }

    /// <summary>本文の装飾（太字・斜体・下線・取り消し線）の切り替えを作る。</summary>
    /// <returns>切り替えの行。</returns>
    private Control Decorations()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(AnnotationEditorControls.Label("装飾"));
        row.Children.Add(AnnotationEditorControls.Toggle(
            "B", _defaults.TextStyle.IsBold, () => SetText(_defaults.TextStyle with { IsBold = !_defaults.TextStyle.IsBold }), bold: true));
        row.Children.Add(AnnotationEditorControls.Toggle(
            "I", _defaults.TextStyle.IsItalic, () => SetText(_defaults.TextStyle with { IsItalic = !_defaults.TextStyle.IsItalic }), italic: true));
        row.Children.Add(AnnotationEditorControls.Toggle(
            "U", _defaults.TextStyle.IsUnderline, () => SetText(_defaults.TextStyle with { IsUnderline = !_defaults.TextStyle.IsUnderline }), underline: true));
        row.Children.Add(AnnotationEditorControls.Toggle(
            "S", _defaults.TextStyle.IsStrike, () => SetText(_defaults.TextStyle with { IsStrike = !_defaults.TextStyle.IsStrike }), strike: true));
        return row;
    }

    /// <summary>小見出しを作る。</summary>
    /// <param name="text">文言。</param>
    /// <returns>小見出し。</returns>
    private static Control SubHeading(string text) => new TextBlock
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 8, 0, 0),
    };
}
