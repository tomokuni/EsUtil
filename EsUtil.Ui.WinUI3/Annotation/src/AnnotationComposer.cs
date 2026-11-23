using System.Drawing.Drawing2D;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈の描画計画を GDI+ で描く（保存する合成画像用）。</summary>
/// <remarks>
/// <b>表示とは別の経路</b>: 画面は XAML の要素で描く。 保存する絵は元画像ピクセルの 1:1 で作る必要があるため、
/// 計画（<see cref="AnnotationShapePlan"/>）を GDI+ へ写す（描く値は計画が解決済みなので、見た目は一致する）。<br/>
/// <b>行の中央寄せは表示と同じ規則</b>を使う（斜体で行の間隔を変えない）。
/// </remarks>
internal static class AnnotationComposer
{
    /// <summary>注釈 1 件を描く（元画像ピクセルの座標系）。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="shape">描画計画。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    internal static void DrawShape(
        System.Drawing.Graphics graphics,
        AnnotationShapePlan shape,
        Func<string, AnnotationTextStyle, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(shape);

        using var fill = shape.HasFill ? new System.Drawing.SolidBrush(shape.Fill) : null;
        using var border = new System.Drawing.Pen(shape.Border, (float)Math.Max(0.5, shape.BorderThickness));

        if (shape.HasTail)
        {
            using var tail = new GraphicsPath();
            var points = new System.Drawing.PointF[shape.Tail.Count];
            for (var i = 0; i < shape.Tail.Count; i++)
            {
                points[i] = new System.Drawing.PointF((float)shape.Tail[i].X, (float)shape.Tail[i].Y);
            }

            tail.AddPolygon(points);
            if (fill is not null)
            {
                graphics.FillPath(fill, tail);
            }

            graphics.DrawPath(border, tail);
        }

        var bounds = new System.Drawing.RectangleF(
            (float)shape.Bounds.X, (float)shape.Bounds.Y, (float)shape.Bounds.Width, (float)shape.Bounds.Height);
        var radius = (float)shape.CornerRadius;
        using var body = CreateRoundedRect(bounds, radius);
        if (fill is not null)
        {
            graphics.FillPath(fill, body);
        }

        graphics.DrawPath(border, body);

        foreach (var line in shape.Lines)
        {
            foreach (var segment in line.Segments)
            {
                DrawSegment(graphics, line, segment, measureText);
            }
        }
    }

    /// <summary>角丸の矩形のパスを作る。</summary>
    /// <param name="bounds">矩形。</param>
    /// <param name="radius">角の半径（0 は角を丸めない）。</param>
    /// <returns>パス。 破棄は呼び出し側。</returns>
    private static GraphicsPath CreateRoundedRect(System.Drawing.RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>本文の区間を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="line">区間が属する行。</param>
    /// <param name="segment">区間。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    private static void DrawSegment(
        System.Drawing.Graphics graphics,
        AnnotationTextLinePlan line,
        AnnotationTextSegmentPlan segment,
        Func<string, AnnotationTextStyle, EsSize> measureText)
    {
        var style = segment.Style;
        var runHeight = measureText("Ag", style with { IsItalic = false }).Height;
        var top = (float)(line.Bounds.Y + ((line.Bounds.Height - runHeight) / 2));

        using var font = new System.Drawing.Font(
            style.FontFamily,
            (float)style.FontSize,
            ToFontStyle(style),
            System.Drawing.GraphicsUnit.Pixel);
        using var brush = new System.Drawing.SolidBrush(style.Color);

        var format = new System.Drawing.StringFormat(System.Drawing.StringFormatFlags.NoWrap)
        {
            Alignment = System.Drawing.StringAlignment.Near,
            LineAlignment = System.Drawing.StringAlignment.Near,
        };
        using var disposableFormat = format;

        graphics.DrawString(
            segment.Text,
            font,
            brush,
            new System.Drawing.PointF((float)segment.Bounds.X, top),
            format);
    }

    /// <summary>書式を GDI+ のフォント スタイルへ変換する。</summary>
    /// <param name="style">書式。</param>
    /// <returns>フォント スタイル。</returns>
    private static System.Drawing.FontStyle ToFontStyle(AnnotationTextStyle style)
    {
        var result = System.Drawing.FontStyle.Regular;
        if (style.IsBold)
        {
            result |= System.Drawing.FontStyle.Bold;
        }

        if (style.IsItalic)
        {
            result |= System.Drawing.FontStyle.Italic;
        }

        if (style.IsUnderline)
        {
            result |= System.Drawing.FontStyle.Underline;
        }

        if (style.IsStrike)
        {
            result |= System.Drawing.FontStyle.Strikeout;
        }

        return result;
    }
}
