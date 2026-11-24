using Avalonia;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.StyledText;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の描画計画を Avalonia の描画 API へ写す。</summary>
/// <remarks>
/// <b>計算を持たない</b>: 角丸・塗り・枠線・しっぽの多角形・行の位置は計画が解決済みである
/// （4 つの UI 版で見た目がずれないよう、判断は Core / Presenter が単一所有する）。<br/>
/// <b>文字の描画と実寸は <see cref="StyledText.RichTextPainter"/> が単一所有する</b>: 本文の描画は注釈に
/// 固有ではなく、書式付き文字列の編集コントロールと同じ見た目でなければならない（写しを持つと、片方だけ
/// 直したときに文字の位置や行の高さが食い違う）。
/// </remarks>
internal static class AnnotationPainter
{
    /// <summary>文字列を描画できる形へ変換する。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>整形済みの文字列（元画像ピクセルの大きさ）。</returns>
    internal static FormattedText CreateText(string text, TextStyle style)
        => StyledText.RichTextPainter.CreateText(text, style);

    /// <summary>行の高さを測るための整形（斜体を外した書式）を作る。</summary>
    /// <param name="style">書式。</param>
    /// <returns>整形済みの文字列（<c>Ag</c>）。</returns>
    internal static FormattedText CreateLineMetrics(TextStyle style)
        => StyledText.RichTextPainter.CreateLineMetrics(style);

    /// <summary>文字列の実寸を測る（幅と行の高さ）。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。</returns>
    internal static EsSize Measure(string text, TextStyle style)
        => StyledText.RichTextPainter.Measure(text, style);

    /// <summary>注釈 1 件を描く（元画像ピクセルの座標系で呼ぶ）。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="shape">描画計画。</param>
    /// <param name="suppressText">本文を描かない場合は <see langword="true"/>（本文を別のコントロールが描く）。</param>
    internal static void DrawShape(DrawingContext context, AnnotationShapePlan shape, bool suppressText = false)
    {
        var brush = shape.HasFill ? ToBrush(shape.Fill) : null;
        var pen = new Pen(ToBrush(shape.Border), shape.BorderThickness);

        if (shape.HasTail)
        {
            DrawTail(context, shape, brush, pen);
        }

        context.DrawRectangle(
            brush,
            pen,
            new Rect(shape.Bounds.X, shape.Bounds.Y, shape.Bounds.Width, shape.Bounds.Height),
            shape.CornerRadius,
            shape.CornerRadius);

        if (suppressText)
        {
            return;
        }

        foreach (var line in shape.Lines)
        {
            StyledText.RichTextPainter.DrawLine(context, line);
        }
    }

    /// <summary>色をブラシへ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>単色のブラシ。</returns>
    internal static IBrush ToBrush(DrawingColor color)
        => StyledText.RichTextPainter.ToBrush(color);

    /// <summary>しっぽ（三角形）を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="shape">描画計画。</param>
    /// <param name="brush">塗り。</param>
    /// <param name="pen">枠線。</param>
    /// <remarks>
    /// 本体の後に描くと本体の枠線が三角形の上へ重なるため、<b>本体より先に描く</b>。
    /// 付け根の辺は本体の内側へ隠れる（本体の塗りが後から上書きする）。
    /// </remarks>
    private static void DrawTail(DrawingContext context, AnnotationShapePlan shape, IBrush? brush, IPen pen)
    {
        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(new Point(shape.Tail[0].X, shape.Tail[0].Y), isFilled: true);
            for (var i = 1; i < shape.Tail.Count; i++)
            {
                stream.LineTo(new Point(shape.Tail[i].X, shape.Tail[i].Y));
            }

            stream.EndFigure(isClosed: true);
        }

        context.DrawGeometry(brush, pen, geometry);
    }
}