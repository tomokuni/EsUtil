using System.Drawing.Drawing2D;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.StyledText;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈の描画計画を GDI+ へ写す（WinForms 版）。</summary>
/// <remarks>
/// <b>計算を持たない</b>: 角丸・塗り・枠線・しっぽの多角形・行の位置は計画（<see cref="AnnotationShapePlan"/>）が
/// 解決済みである（4 つの UI 版で見た目がずれない）。<br/>
/// <b>画面と保存で同じ手順を使う</b>: 表示は表示倍率と位置を渡し、保存は 1:1 で呼ぶ（見た目が一致する）。
/// </remarks>
internal static class AnnotationPainter
{
    /// <summary>注釈を描く（元画像ピクセルの座標系）。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="plan">描画計画。</param>
    /// <param name="scale">表示倍率。</param>
    /// <param name="offsetX">画像の左上 X 座標（DIP）。</param>
    /// <param name="offsetY">画像の左上 Y 座標（DIP）。</param>
    /// <param name="externalTextId">本文を描かない吹き出しの識別子（別のコントロールが描く）。</param>
    /// <remarks>
    /// <b>編集中も本文を描く</b>: 本 UI は本文を別の入力欄で表示しないため、編集中に本文を描かないと
    /// 文字が消える。 キャレットと選択範囲は後から重ねる。
    /// </remarks>
    internal static void Draw(
        Graphics graphics,
        AnnotationDrawPlan plan,
        double scale,
        double offsetX,
        double offsetY,
        string? externalTextId = null)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(plan);

        var state = graphics.Save();
        try
        {
            graphics.TranslateTransform((float)offsetX, (float)offsetY);
            graphics.ScaleTransform((float)scale, (float)scale);
            foreach (var shape in plan.Shapes)
            {
                // **本文を別のコントロールが描く場合は描かない**（同じ文字を二重に描かない）
                DrawShape(graphics, shape, string.Equals(shape.Id, externalTextId, StringComparison.Ordinal));
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>注釈 1 件を描く。</summary>
    /// <param name="graphics">描画先（元画像ピクセルの座標系）。</param>
    /// <param name="shape">描画計画。</param>
    /// <param name="suppressText">本文を描かない場合は <see langword="true"/>（本文を別のコントロールが描く）。</param>
    internal static void DrawShape(Graphics graphics, AnnotationShapePlan shape, bool suppressText = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(shape);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var fill = shape.HasFill ? new SolidBrush(shape.Fill) : null;
        using var border = new Pen(shape.Border, (float)Math.Max(0.5, shape.BorderThickness));

        if (shape.HasTail)
        {
            using var tail = new GraphicsPath();
            var points = new PointF[shape.Tail.Count];
            for (var i = 0; i < shape.Tail.Count; i++)
            {
                points[i] = new PointF((float)shape.Tail[i].X, (float)shape.Tail[i].Y);
            }

            tail.AddPolygon(points);
            if (fill is not null)
            {
                graphics.FillPath(fill, tail);
            }

            graphics.DrawPath(border, tail);
        }

        var bounds = new RectangleF(
            (float)shape.Bounds.X, (float)shape.Bounds.Y, (float)shape.Bounds.Width, (float)shape.Bounds.Height);
        using var body = CreateRoundedRect(bounds, (float)shape.CornerRadius);
        if (fill is not null)
        {
            graphics.FillPath(fill, body);
        }

        graphics.DrawPath(border, body);

        if (suppressText)
        {
            return;
        }

        foreach (var line in shape.Lines)
        {
            foreach (var segment in line.Segments)
            {
                DrawSegment(graphics, line, segment);
            }
        }
    }

    /// <summary>角丸の矩形のパスを作る。</summary>
    /// <param name="bounds">矩形。</param>
    /// <param name="radius">角の半径（0 は角を丸めない）。</param>
    /// <returns>パス。 破棄は呼び出し側。</returns>
    private static GraphicsPath CreateRoundedRect(RectangleF bounds, float radius)
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
    private static void DrawSegment(Graphics graphics, TextLinePlan line, TextSegmentPlan segment)
    {
        var style = segment.Style;
        var runHeight = AnnotationTextMetrics.ResolveLineHeight(style);
        var top = (float)(line.Bounds.Y + ((line.Bounds.Height - runHeight) / 2));

        using var font = AnnotationTextMetrics.CreateFont(style with { IsUnderline = false, IsStrike = false });
        using var brush = new SolidBrush(style.Color);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoWrap,
        };

        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(
            segment.Text,
            font,
            brush,
            new PointF((float)segment.Bounds.X, top),
            format);

        if (!style.IsUnderline && !style.IsStrike)
        {
            return;
        }

        // 下線・取り消し線は区間の送り幅に合わせて引く（フォントの装飾では行の高さに依らず位置が揃わない）
        var width = (float)(segment.Bounds.Width > 0
            ? segment.Bounds.Width
            : AnnotationTextMetrics.Measure(segment.Text, style).Width);
        var thickness = (float)Math.Max(1, style.FontSize * 0.06);
        using var pen = new Pen(style.Color, thickness);
        if (style.IsUnderline)
        {
            var y = top + (float)runHeight - (thickness * 2);
            graphics.DrawLine(pen, (float)segment.Bounds.X, y, (float)segment.Bounds.X + width, y);
        }

        if (style.IsStrike)
        {
            var y = top + ((float)runHeight / 2);
            graphics.DrawLine(pen, (float)segment.Bounds.X, y, (float)segment.Bounds.X + width, y);
        }
    }

    /// <summary>選択枠（枠とハンドル）を描く。</summary>
    /// <param name="graphics">描画先（DIP）。</param>
    /// <param name="borderRects">枠の矩形。</param>
    /// <param name="borderColor">枠の色。</param>
    /// <param name="handleRects">ハンドルの矩形（無い場合は <see langword="null"/>）。</param>
    /// <param name="handleColor">ハンドルの塗り。</param>
    /// <param name="handleBorderColor">ハンドルの枠線の色。</param>
    internal static void DrawSelection(
        Graphics graphics,
        IReadOnlyList<EsRect> borderRects,
        Color borderColor,
        IReadOnlyList<EsRect>? handleRects,
        Color handleColor,
        Color handleBorderColor)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(borderRects);

        using var border = new SolidBrush(borderColor);
        foreach (var rect in borderRects)
        {
            graphics.FillRectangle(
                border, (float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
        }

        if (handleRects is null)
        {
            return;
        }

        using var fill = new SolidBrush(handleColor);
        using var stroke = new Pen(handleBorderColor, 1);
        foreach (var rect in handleRects)
        {
            graphics.FillRectangle(
                fill, (float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
            graphics.DrawRectangle(
                stroke, (float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
        }
    }

    /// <summary>しっぽのハンドル（円）を描く。</summary>
    /// <param name="graphics">描画先（DIP）。</param>
    /// <param name="center">中心の座標。</param>
    /// <param name="size">一辺の大きさ。</param>
    /// <param name="fill">塗り。</param>
    /// <param name="stroke">枠線。</param>
    internal static void DrawTailHandle(Graphics graphics, PointF center, double size, Color fill, Color stroke)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        using var brush = new SolidBrush(fill);
        using var pen = new Pen(stroke, 1);
        var rect = new RectangleF(
            (float)(center.X - (size / 2)),
            (float)(center.Y - (size / 2)),
            (float)size,
            (float)size);
        graphics.FillRectangle(brush, rect);
        graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
    }
}
