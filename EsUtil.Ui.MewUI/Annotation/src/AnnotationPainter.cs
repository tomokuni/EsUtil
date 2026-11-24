using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Text;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.StyledText;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈の描画計画を MewUI の描画 API へ写す。</summary>
/// <remarks>
/// <b>計算を持たない</b>: 角丸・塗り・枠線・しっぽの多角形・行の位置は計画が解決済みである
/// （4 つの UI 版で見た目がずれないよう、判断は Core / Presenter が単一所有する）。<br/>
/// <b>文字の実寸だけは UI が測る</b>（<see cref="AnnotationTextMetrics"/>）。 折り返しは計画側が行う。
/// </remarks>
internal static class AnnotationPainter
{
    /// <summary>注釈 1 件を描く（元画像ピクセルの座標系で呼ぶ）。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="shape">描画計画。</param>
    /// <param name="layouts">文字のレイアウトを取得する関数（同じフレームでは同じ実体を返す）。</param>
    internal static void DrawShape(
        IGraphicsContext context, AnnotationShapePlan shape, Func<string, TextStyle, ITextLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        var fill = shape.HasFill ? AnnotationTextMetrics.ToMewColor(shape.Fill) : (Color?)null;
        var border = AnnotationTextMetrics.ToMewColor(shape.Border);

        if (shape.HasTail)
        {
            DrawTail(context, shape, fill, border);
        }

        var bounds = shape.Bounds;
        var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);

        if (fill is { } fillColor)
        {
            context.FillRoundedRectangle(rect, shape.CornerRadius, shape.CornerRadius, fillColor);
        }

        if (shape.BorderThickness > 0)
        {
            context.DrawRoundedRectangle(rect, shape.CornerRadius, shape.CornerRadius, border, shape.BorderThickness);
        }

        // **文字の描画は本文コントロールと同じ実装を使う**: 注釈に写しを持つと、行のベースラインや
        // 下線の位置が片方だけ直したときに食い違う。
        foreach (var line in shape.Lines)
        {
            StyledText.RichTextPainter.DrawLine(context, line, layouts);
        }
    }

    /// <summary>色を MewUI の色へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>MewUI の色。</returns>
    internal static Color ToColor(DrawingColor color) => AnnotationTextMetrics.ToMewColor(color);

    /// <summary>しっぽ（三角形）を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="shape">描画計画。</param>
    /// <param name="fill">塗り（無い場合は枠線だけ）。</param>
    /// <param name="border">枠線の色。</param>
    /// <remarks>
    /// 本体の後に描くと本体の枠線が三角形の上へ重なるため、<b>本体より先に描く</b>。 付け根の辺は本体の内側へ
    /// 隠れる（本体の塗りが後から上書きする）。
    /// </remarks>
    private static void DrawTail(IGraphicsContext context, AnnotationShapePlan shape, Color? fill, Color border)
    {
        var path = new PathGeometry();
        path.MoveTo(
            new Point(shape.Tail[0].X, shape.Tail[0].Y));
        for (var i = 1; i < shape.Tail.Count; i++)
        {
            path.LineTo(new Point(shape.Tail[i].X, shape.Tail[i].Y));
        }

        path.Close();
        path.Freeze();

        if (fill is { } fillColor)
        {
            context.FillPath(path, fillColor);
        }

        if (shape.BorderThickness > 0)
        {
            context.DrawPath(path, border, shape.BorderThickness);
        }
    }
}