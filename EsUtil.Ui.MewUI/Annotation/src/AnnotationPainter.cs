using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Text;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Text;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈の描画計画を MewUI の描画 API へ写す。</summary>
/// <remarks>
/// <b>計算を持たない</b>: 角丸・塗り・枠線・しっぽの多角形・行の位置は計画が解決済みである
/// （4 つの UI 版で見た目がずれないよう、判断は Core / Presenter が単一所有する）。<br/>
/// <b>文字の実寸だけは UI が測る</b>（<see cref="AnnotationTextMetrics"/>）。 折り返しは計画側が行う。
/// </remarks>
internal static class AnnotationPainter
{
    /// <summary>下線・取り消し線の太さの比率（文字の大きさに対する比）。</summary>
    private const double DecorationThicknessRatio = 0.06;

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

        foreach (var line in shape.Lines)
        {
            // 行のベースラインは 1 本に決める（区間ごとに縦位置を決めると、斜体で実寸が変わったときに
            // 文字が上下へずれる）
            var baseline = ResolveLineBaseline(line);

            foreach (var segment in line.Segments)
            {
                var layout = layouts(segment.Text, segment.Style);
                var top = baseline - layout.Lines[0].Baseline;
                DrawText(context, layout, segment.Bounds.X, top, segment.Style.Color);
                DrawDecorations(context, segment, layout, top);
            }
        }
    }

    /// <summary>色を MewUI の色へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>MewUI の色。</returns>
    internal static Color ToColor(DrawingColor color) => AnnotationTextMetrics.ToMewColor(color);

    /// <summary>行のベースライン（元画像ピクセル）を求める。</summary>
    /// <param name="line">行の描画計画。</param>
    /// <returns>ベースラインの Y 座標。</returns>
    /// <remarks>
    /// <b>決め方はコアが単一所有する</b>（<see cref="TextLineLayout.ResolveBaseline"/>）:
    /// キャレット・選択範囲も同じベースラインを使うため、ここで独自に計算してはいけない。
    /// </remarks>
    private static double ResolveLineBaseline(TextLinePlan line)
        => TextLineLayout.ResolveBaseline(line, AnnotationTextMetrics.MeasureLineMetrics);

    /// <summary>文字列を描画する。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="layout">レイアウト。</param>
    /// <param name="left">左端（元画像ピクセル）。</param>
    /// <param name="top">上端（元画像ピクセル）。</param>
    /// <param name="color">文字色。</param>
    private static void DrawText(IGraphicsContext context, ITextLayout layout, double left, double top, DrawingColor color)
    {
        if (layout.MeasuredSize.Width <= 0)
        {
            return;
        }

        var options = new TextDrawOptions { Foreground = AnnotationTextMetrics.ToMewColor(color) };
        context.Text.Draw(layout, new Point(left, top), in options);
    }

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

    /// <summary>下線・取り消し線を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="segment">区間。</param>
    /// <param name="layout">レイアウト（文字の実寸を取るため）。</param>
    /// <param name="top">文字の上端（ベースラインから求めた描画位置）。</param>
    private static void DrawDecorations(
        IGraphicsContext context, TextSegmentPlan segment, ITextLayout layout, double top)
    {
        if (!segment.Style.IsUnderline && !segment.Style.IsStrike)
        {
            return;
        }

        var thickness = Math.Max(1.0, segment.Style.FontSize * DecorationThicknessRatio);
        var color = AnnotationTextMetrics.ToMewColor(segment.Style.Color);
        var left = segment.Bounds.X;
        var right = segment.Bounds.X + layout.MeasuredSize.Width;
        var height = layout.MeasuredSize.Height;

        if (segment.Style.IsUnderline)
        {
            var y = top + height - (thickness * 2);
            context.DrawLine(new Point(left, y), new Point(right, y), color, thickness);
        }

        if (segment.Style.IsStrike)
        {
            var y = top + (height / 2);
            context.DrawLine(new Point(left, y), new Point(right, y), color, thickness);
        }
    }
}
