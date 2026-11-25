using EsUtil.Platform.Core.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Presenter.Editor;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈の外観（枠線・内側・角丸・しっぽ・重なり順）を編集するパネル（WinUI 3 版）。</summary>
/// <remarks>
/// <b>選択中の注釈だけに効く</b>（設定画面の既定値は新しく追加する注釈へ効く）。<br/>
/// <b>本文の書式は書式バーが担う</b>（このパネルは外観と重なり順だけを扱う）。
/// </remarks>
internal sealed partial class AnnotationStylePanel : ContentControl
{
    /// <summary>対象のレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>外観パネルを初期化する。</summary>
    /// <param name="layer">対象のレイヤー。</param>
    internal AnnotationStylePanel(AnnotationLayerControl layer)
    {
        _layer = layer;

        // 枠・余白・幅はコアの外観（AnnotationPanelAppearance）が単一所有する。
        // ここで持つと、ウィンドウの枠と二重になって「余計な辺枠」に見える。
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;

        Content = Build();
    }

    /// <summary>パネルの中身を組み立てる。</summary>
    /// <returns>縦に積んだ要素。</returns>
    private FrameworkElement Build()
    {
        var panel = new StackPanel { Spacing = 2 };

        panel.Children.Add(AnnotationEditorControls.Heading(AnnotationEditorText.AppearanceHeading));
        panel.Children.Add(AnnotationEditorControls.Divider());

        if (_layer.Selected is not { } item)
        {
            panel.Children.Add(AnnotationEditorControls.Label(AnnotationEditorText.NoSelection));
            return panel;
        }

        var appearance = item.Appearance;
        panel.Children.Add(AnnotationEditorControls.ColorRow(
            AnnotationEditorText.BorderColor, AnnotationEditorControls.ToHex(appearance.Border), _layer.ColorTextFormat,
            value => _layer.UpdateSelectedAppearance(current => current with { Border = ParseColor(value, current.Border) })));
        panel.Children.Add(AnnotationEditorControls.NumberRow(
            AnnotationEditorText.BorderThickness, appearance.BorderThickness, AnnotationLimits.BorderThicknessMin, AnnotationLimits.BorderThicknessMax,
            value => _layer.UpdateSelectedAppearance(current => current with { BorderThickness = value })));
        panel.Children.Add(AnnotationEditorControls.ColorRow(
            AnnotationEditorText.FillColor, AnnotationEditorControls.ToHex(appearance.Fill), _layer.ColorTextFormat,
            value => _layer.UpdateSelectedAppearance(current => current with { Fill = ParseColor(value, current.Fill) })));
        panel.Children.Add(AnnotationEditorControls.NumberRow(
            AnnotationEditorText.CornerRadius, appearance.CornerRadius, 0, AnnotationLimits.CornerRadiusMax,
            value => _layer.UpdateSelectedAppearance(current => current with { CornerRadius = value })));

        if (item is BubbleAnnotation bubble)
        {
            panel.Children.Add(AnnotationEditorControls.Divider());
            panel.Children.Add(AnnotationEditorControls.Heading(AnnotationEditorText.BubbleHeading));
            panel.Children.Add(AnnotationEditorControls.NumberRow(
                AnnotationEditorText.BodyPadding, bubble.BodyPadding, 0, AnnotationLimits.BodyPaddingMax, value => _layer.SetSelectedBodyPadding(value)));
            panel.Children.Add(AnnotationEditorControls.NumberRow(
                AnnotationEditorText.TailWidth, bubble.Tail.Width, AnnotationLimits.TailWidthMin, AnnotationLimits.TailWidthMax,
                value => _layer.UpdateSelectedTail(tail => tail with { Width = value })));
            panel.Children.Add(AnnotationEditorControls.ChoiceRow(
                AnnotationBubbleEdgeCatalog.Label,
                AnnotationBubbleEdgeCatalog.Options,
                (int)bubble.Tail.BaseEdge,
                value => _layer.UpdateSelectedTail(tail => tail with { BaseEdge = (BubbleEdge)value })));
        }

        panel.Children.Add(AnnotationEditorControls.Divider());
        var order = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var front = new Button { Content = AnnotationEditorText.MoveToFront, FontSize = AnnotationEditorControls.FontSize };
        front.Click += (_, _) => _layer.BringSelectedToFront();
        var back = new Button { Content = AnnotationEditorText.MoveToBack, FontSize = AnnotationEditorControls.FontSize };
        back.Click += (_, _) => _layer.SendSelectedToBack();
        order.Children.Add(front);
        order.Children.Add(back);
        panel.Children.Add(order);

        return panel;
    }

    /// <summary>保存値（<c>#AARRGGBB</c>）を色へ変換する。</summary>
    /// <param name="text">保存値。</param>
    /// <param name="fallback">解釈できない場合の色。</param>
    /// <returns>色。</returns>
    /// <remarks>文字列の解釈は <see cref="DrawingColorExtensions.TryParseHexText"/> が単一所有する（自前で桁数を数えない）。</remarks>
    private static DrawingColor ParseColor(string? text, DrawingColor fallback)
        => DrawingColorExtensions.TryParseHexText(text, out var color) ? color : fallback;
}
