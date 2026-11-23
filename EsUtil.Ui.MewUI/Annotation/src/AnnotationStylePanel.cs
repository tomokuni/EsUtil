using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈の外観・吹き出し・重なり順を編集するパネル（右クリック）。</summary>
/// <remarks>
/// <b>選択中の注釈だけを編集する</b>: 既存の注釈の外観・書式は注釈ごとに持つ（既定値を変えても既存は変わらない）。<br/>
/// <b>行の部品は <see cref="AnnotationEditorControls"/> を共有する</b>（ホストの設定ペインと同じ見た目・同じ色の解釈）。
/// </remarks>
public sealed class AnnotationStylePanel : ContentControl
{
    /// <summary>編集パネルを初期化する。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    /// <param name="appearance">パネルの外観（中身の幅と高さの上限を使う）。</param>
    public AnnotationStylePanel(AnnotationLayerControl layer, AnnotationPanelAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(layer);

        ArgumentNullException.ThrowIfNull(appearance);

        var content = new StackPanel { Spacing = 8, Width = appearance.ContentWidth };

        // 見出しはタイトル バーのような帯にする（この面と余白を押したまま動かすとウィンドウが移動する）
        content.Add(new Border
        {
            Padding = new Thickness(4, 3, 4, 4),
            Background = new Color(255, 240, 240, 240),
            Child = AnnotationEditorControls.Heading(AnnotationEditorText.AppearanceHeading),
        });

        content.Add(BuildAppearanceGroup(layer));

        if (layer.Selected is BubbleAnnotation)
        {
            content.Add(Separator());
            content.Add(BuildBubbleGroup(layer));
        }

        content.Add(Separator());
        content.Add(BuildOrderGroup(layer));

        // 枠・余白・幅はコアの外観（AnnotationPanelAppearance）が単一所有する。
        // ここで持つと、ウィンドウの枠と二重になって「余計な辺枠」に見える。
        // **スクロールさせない**: `ScrollViewer` で包むと、中身が収まっていてもバーが出る
        // （MewUI の既定は `ScrollMode.Visible`。 `Auto` にしてもパネルの領域が窓の残り高さに
        // 切り取られてバーが残った。 実測 2026-11-23）。 置く位置はホストが
        // `AnnotationPanelPlacement`（コア）で窓の内側へ収めるため、ここでは中身をそのまま置く。
        Content = content;
    }

    /// <summary>区切り線を作る。</summary>
    /// <returns>区切り線。</returns>
    private static Element Separator() => new Border
    {
        Height = 1,
        Background = new Color(64, 128, 128, 128),
    };

    /// <summary>文字のボタンを作る（MewUI のボタンは文字列を内容に持てない）。</summary>
    /// <param name="text">文言。</param>
    /// <returns>ボタン。</returns>
    private static Button CreateButton(string text) => new() { Content = new TextBlock { Text = text } };

    /// <summary>外観（枠線・内側・角丸）の編集を作る。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    /// <returns>外観の編集。</returns>
    private static Element BuildAppearanceGroup(AnnotationLayerControl layer)
    {
        var appearance = layer.Selected?.Appearance ?? AnnotationAppearance.Default;
        var panel = new StackPanel { Spacing = 6 };

        panel.Add(AnnotationEditorControls.ColorRow(
            AnnotationEditorText.BorderColor,
            appearance.Border,
            color => layer.UpdateSelectedAppearance(a => a with { Border = color }),
            layer.ColorTextFormat));
        panel.Add(AnnotationEditorControls.NumberRow(
            AnnotationEditorText.BorderThickness,
            appearance.BorderThickness,
            AnnotationLimits.BorderThicknessMin,
            AnnotationLimits.BorderThicknessMax,
            value => layer.UpdateSelectedAppearance(a => a with { BorderThickness = value })));
        panel.Add(AnnotationEditorControls.ColorRow(
            AnnotationEditorText.FillColor,
            appearance.Fill,
            color => layer.UpdateSelectedAppearance(a => a with { Fill = color }),
            layer.ColorTextFormat));
        panel.Add(AnnotationEditorControls.NumberRow(
            AnnotationEditorText.CornerRadius,
            appearance.CornerRadius,
            0,
            AnnotationLimits.CornerRadiusMax,
            value => layer.UpdateSelectedAppearance(a => a with { CornerRadius = value })));

        return panel;
    }

    /// <summary>吹き出し（本文の余白・しっぽ）の編集を作る。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    /// <returns>吹き出しの編集。</returns>
    private static Element BuildBubbleGroup(AnnotationLayerControl layer)
    {
        var bubble = (BubbleAnnotation)layer.Selected!;
        var panel = new StackPanel { Spacing = 6 };

        panel.Add(AnnotationEditorControls.Heading(AnnotationEditorText.BubbleHeading));
        panel.Add(AnnotationEditorControls.NumberRow(
            AnnotationEditorText.BodyPadding,
            bubble.BodyPadding,
            0,
            AnnotationLimits.BodyPaddingMax,
            value => layer.SetSelectedBodyPadding(value)));
        panel.Add(AnnotationEditorControls.NumberRow(
            AnnotationEditorText.TailWidth,
            bubble.Tail.Width,
            AnnotationLimits.TailWidthMin,
            AnnotationLimits.TailWidthMax,
            value => layer.UpdateSelectedTail(tail => tail with { Width = value })));
        panel.Add(AnnotationEditorControls.ChoiceRow(
            AnnotationBubbleEdgeCatalog.Label,
            [.. AnnotationBubbleEdgeCatalog.Options],
            (int)bubble.Tail.BaseEdge,
            index => layer.UpdateSelectedTail(tail => tail with { BaseEdge = (BubbleEdge)index })));

        return panel;
    }

    /// <summary>重なり順の編集を作る。</summary>
    /// <param name="layer">編集対象のコントロール。</param>
    /// <returns>重なり順の編集。</returns>
    private static Element BuildOrderGroup(AnnotationLayerControl layer)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var front = CreateButton(AnnotationEditorText.MoveToFront);
        front.OnClick(() => layer.MoveSelectedToFront());
        var back = CreateButton(AnnotationEditorText.MoveToBack);
        back.OnClick(() => layer.MoveSelectedToBack());

        row.Add(front);
        row.Add(back);
        return row;
    }
}
