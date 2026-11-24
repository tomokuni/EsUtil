using System.Globalization;
using Avalonia;
using Avalonia.Media;
using EsUtil.Feature.StyledText.Core.Defaults;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.StyledText;

/// <summary>書式付き文字列の描画と実寸の計測（Avalonia の描画 API への写し）。</summary>
/// <remarks>
/// <b>位置は決めない</b>: 折り返しと行の配置は <see cref="TextLayout"/> が、行の中の文字位置は
/// <see cref="TextLineLayout"/> が単一所有する。 本クラスは解決済みの行を Avalonia の描画 API へ写すだけである
/// （写しを 1 か所に置くことで、編集コントロールと注釈の描画が同じ見た目になる）。<br/>
/// <b>斜体を外して行の高さを測る</b>: 斜体は字を傾けるだけで行の高さを変えない（合成斜体では実寸が変わる
/// 環境がある）。 幅は指定どおりの書式で測る（折り返しは実際の送り幅に従う）。
/// </remarks>
internal static class RichTextPainter
{
    /// <summary>下線・取り消し線の太さの比率（文字の大きさに対する比）。</summary>
    private const double DecorationThicknessRatio = 0.06;

    /// <summary>行の高さを測るための文字列（上端と下端が揃った字）。</summary>
    /// <remarks>行の高さは文字によらず書式で決まるため、代表の 2 文字で測る（全文字を測る必要はない）。</remarks>
    private const string LineMetricsText = "Ag";

    /// <summary>文字列を描画できる形へ変換する。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>整形済みの文字列（元画像ピクセル）。</returns>
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
    internal static EsUtil.Platform.Core.Geometry.EsSize Measure(string text, TextStyle style)
    {
        var formatted = CreateText(text, style);

        // **行の高さは斜体で変えない**: 斜体は字を傾けるだけであり、実寸の高さを使うと行の間隔が変わる
        return new EsUtil.Platform.Core.Geometry.EsSize(
            formatted.WidthIncludingTrailingWhitespace, MeasureLineMetrics(style).Height);
    }

    /// <summary>行の高さとベースラインを測るための整形（斜体を外した書式）を作る。</summary>
    /// <param name="style">書式。</param>
    /// <returns>整形済みの文字列（<c>Ag</c>）。</returns>
    /// <remarks>
    /// <b>斜体を外す</b>: 斜体は字を傾けるだけで行の高さを変えない（合成斜体では実寸が変わる環境がある）。
    /// </remarks>
    internal static FormattedText CreateLineMetrics(TextStyle style)
        => CreateText(LineMetricsText, style with { IsItalic = false });

    /// <summary>行の実寸（高さとベースライン）を測る（斜体を外した書式で測る）。</summary>
    /// <param name="style">書式。</param>
    /// <returns>行の実寸。</returns>
    internal static TextLineMetrics MeasureLineMetrics(TextStyle style)
    {
        var formatted = CreateLineMetrics(style);
        return new TextLineMetrics(formatted.Height, formatted.Baseline);
    }

    /// <summary>行を描く（ベースラインを 1 本に決める）。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="line">行。</param>
    /// <remarks>
    /// <b>行のベースラインは 1 本に決める</b>: 区間ごとに縦位置を決めると、斜体で実寸が変わったときに
    /// 文字が上下へずれる（規則は <see cref="TextLineLayout.ResolveBaseline"/> が単一所有する）。
    /// </remarks>
    internal static void DrawLine(DrawingContext context, TextLinePlan line)
    {
        var baseline = TextLineLayout.ResolveBaseline(line, MeasureLineMetrics);
        foreach (var segment in line.Segments)
        {
            var formatted = CreateText(segment.Text, segment.Style);
            var top = baseline - formatted.Baseline;
            context.DrawText(formatted, new Point(segment.Bounds.X, top));
            DrawDecorations(context, segment, formatted, top);
        }
    }

    /// <summary>色をブラシへ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>単色のブラシ。</returns>
    internal static IBrush ToBrush(DrawingColor color)
        => new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

    /// <summary>使うフォント ファミリを解決する（無い場合は優先順の次のフォント）。</summary>
    /// <param name="fontFamily">指定されたフォント ファミリ名。</param>
    /// <returns>実際に使うフォント ファミリ。</returns>
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
}
