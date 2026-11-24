using System.Globalization;
using Avalonia;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Text;
using EsUtil.Feature.Text.Core.Defaults;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の描画計画を Avalonia の描画 API へ写す。</summary>
/// <remarks>
/// <b>計算を持たない</b>: 角丸・塗り・枠線・しっぽの多角形・行の位置は計画が解決済みである
/// （4 つの UI 版で見た目がずれないよう、判断は Core / Presenter が単一所有する）。<br/>
/// <b>文字の実寸だけは UI が測る</b>（<see cref="CreateText"/>）。 折り返しは計画側が行う。
/// </remarks>
internal static class AnnotationPainter
{
    /// <summary>下線・取り消し線の太さの比率（文字の大きさに対する比）。</summary>
    private const double DecorationThicknessRatio = 0.06;

    /// <summary>文字列を描画できる形へ変換する。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>整形済みの文字列（元画像ピクセルの大きさ）。</returns>
    internal static FormattedText CreateText(string text, TextStyle style)
        => new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                ResolveFontFamily(style.FontFamily),
                style.IsItalic ? FontStyle.Italic : FontStyle.Normal,
                style.IsBold ? FontWeight.Bold : FontWeight.Normal),
            style.FontSize,
            ToBrush(style.Color));

    /// <summary>文字列の実寸を測る（幅と行の高さ）。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。</returns>
    internal static EsSize Measure(string text, TextStyle style)
    {
        var formatted = CreateText(text, style);
        return new EsSize(formatted.WidthIncludingTrailingWhitespace, formatted.Height);
    }

    /// <summary>注釈 1 件を描く（元画像ピクセルの座標系で呼ぶ）。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="shape">描画計画。</param>
    internal static void DrawShape(DrawingContext context, AnnotationShapePlan shape)
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

        foreach (var line in shape.Lines)
        {
            // 行のベースラインは 1 本に決める（区間ごとに縦位置を決めると、斜体で実寸が変わったときに
            // 文字が上下へずれる）
            var baseline = ResolveLineBaseline(line);

            foreach (var segment in line.Segments)
            {
                var formatted = CreateText(segment.Text, segment.Style);
                var top = baseline - formatted.Baseline;
                context.DrawText(formatted, new Point(segment.Bounds.X, top));
                DrawDecorations(context, segment, formatted, top);
            }
        }
    }

    /// <summary>行のベースライン（元画像ピクセル）を求める。</summary>
    /// <param name="line">行の描画計画。</param>
    /// <returns>ベースラインの Y 座標（行の上端から、いちばん背の高い区間の書式で決める）。</returns>
    /// <remarks>
    /// <b>斜体・太字を外した実寸で決める</b>: 装飾によって実寸（<c>FormattedText.Height</c> /
    /// <c>Baseline</c>）が変わっても、行のベースラインは動かさない（斜体にすると文字が上へずれ、
    /// 行の高さが増える）
    /// </remarks>
    private static double ResolveLineBaseline(TextLinePlan line)
    {
        var height = 0.0;
        double referenceHeight = 0;
        double referenceBaseline = 0;
        foreach (var segment in line.Segments)
        {
            var metrics = CreateLineMetrics(segment.Style);
            if (metrics.Height <= height)
            {
                continue;
            }

            height = metrics.Height;
            referenceHeight = metrics.Height;
            referenceBaseline = metrics.Baseline;
        }

        return ResolveBaseline(line.Bounds.Y, line.Bounds.Height, referenceHeight, referenceBaseline);
    }

    /// <summary>行の高さいっぱいへ中央寄せしたときのベースラインを求める。</summary>
    /// <param name="lineTop">行の上端（元画像ピクセル）。</param>
    /// <param name="lineHeight">行の高さ。</param>
    /// <param name="referenceHeight">基準の書式の実寸の高さ。</param>
    /// <param name="referenceBaseline">基準の書式のベースライン（上端から）。</param>
    /// <returns>ベースラインの Y 座標。</returns>
    /// <remarks>計算だけを切り出す（斜体で実寸が変わっても位置が動かないことを単体テストで固定するため）。</remarks>
    internal static double ResolveBaseline(
        double lineTop, double lineHeight, double referenceHeight, double referenceBaseline)
        => lineTop + ((lineHeight - referenceHeight) / 2) + referenceBaseline;

    /// <summary>行の高さとベースラインを測るための整形（斜体を外した書式）を作る。</summary>
    /// <param name="style">書式。</param>
    /// <returns>整形済みの文字列（<c>Ag</c>）。</returns>
    /// <remarks>
    /// <b>斜体を外す</b>: 斜体は字を傾けるだけで行の高さを変えない（合成斜体では実寸が変わる環境がある）。
    /// 幅は <see cref="CreateText"/>（指定どおりの書式）で測る（折り返しは実際の送り幅に従う）。
    /// </remarks>
    internal static FormattedText CreateLineMetrics(TextStyle style)
        => CreateText("Ag", style with { IsItalic = false });

    /// <summary>色をブラシへ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>単色のブラシ。</returns>
    internal static IBrush ToBrush(DrawingColor color)
        => new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

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

    /// <summary>下線・取り消し線を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="segment">区間。</param>
    /// <param name="formatted">整形済みの文字列（文字の実寸を取るため）。</param>
    /// <param name="top">文字の上端（ベースラインから求めた描画位置）。</param>
    private static void DrawDecorations(
        DrawingContext context, TextSegmentPlan segment, FormattedText formatted, double top)
    {
        if (!segment.Style.IsUnderline && !segment.Style.IsStrike)
        {
            return;
        }

        var thickness = Math.Max(1.0, segment.Style.FontSize * DecorationThicknessRatio);
        var pen = new Pen(ToBrush(segment.Style.Color), thickness);
        var left = segment.Bounds.X;
        var right = segment.Bounds.X + formatted.Width;

        if (segment.Style.IsUnderline)
        {
            var y = top + formatted.Height - (thickness * 2);
            context.DrawLine(pen, new Point(left, y), new Point(right, y));
        }

        if (segment.Style.IsStrike)
        {
            var y = top + (formatted.Height / 2);
            context.DrawLine(pen, new Point(left, y), new Point(right, y));
        }
    }

    /// <summary>使うフォント ファミリを解決する（無い場合は優先順の次のフォント）。</summary>
    /// <param name="fontFamily">指定されたフォント ファミリ名。</param>
    /// <returns>実際に使うフォント ファミリ。</returns>
    /// <remarks>
    /// 既定は <c>Yu Gothic UI</c> であり、無い環境では <c>Segoe UI</c> を使う
    /// （優先順は <see cref="TextDefaults.PreferredFontFamilies"/> が単一所有する）。
    /// </remarks>
    private static FontFamily ResolveFontFamily(string fontFamily)
    {
        var installed = FontManager.Current.SystemFonts;
        if (installed.Any(font => string.Equals(font.Name, fontFamily, StringComparison.OrdinalIgnoreCase)))
        {
            return new FontFamily(fontFamily);
        }

        foreach (var fallback in TextDefaults.PreferredFontFamilies)
        {
            if (installed.Any(font => string.Equals(font.Name, fallback, StringComparison.OrdinalIgnoreCase)))
            {
                return new FontFamily(fallback);
            }
        }

        return FontFamily.Default;
    }
}
