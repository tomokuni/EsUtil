using System.Drawing.Text;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.StyledText;

/// <summary>書式付き文字列の描画と実寸の計測（GDI+ への写し）。</summary>
/// <remarks>
/// <b>位置は決めない</b>: 折り返しと行の配置は <see cref="TextLayout"/> が、行の中の文字位置は
/// <see cref="TextLineLayout"/> が単一所有する。 本クラスは解決済みの行を GDI+ へ写すだけである。<br/>
/// <b>行の高さは斜体で変えない</b>: 斜体は字を傾けるだけであり、行の高さ・ベースラインを変えると 1 行ごとに
/// 間隔が変わる（UI 版で同じ規則）。<br/>
/// <b>測る面は使い回す</b>: 画面に依存しない測面を 1 つだけ持つ。
/// </remarks>
internal static class RichTextPainter
{
    /// <summary>測定に使う書式（文字の送りだけを測る）。</summary>
    private static readonly StringFormat s_measureFormat = new(StringFormat.GenericTypographic)
    {
        FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap,
    };

    /// <summary>測定に使う面（初回に生成して使い回す）。</summary>
    private static readonly Graphics s_measureGraphics = Graphics.FromHwnd(IntPtr.Zero);

    /// <summary>文字列と書式から実寸を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。 高さは斜体を外した書式で測る。</returns>
    internal static EsSize Measure(string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);

        var width = text.Length == 0 ? 0 : MeasureWidth(text, style);
        return new EsSize(width, MeasureLineMetrics(style).Height);
    }

    /// <summary>行の実寸（高さとベースライン）を測る（斜体を外した書式で測る）。</summary>
    /// <param name="style">書式。</param>
    /// <returns>行の実寸。</returns>
    internal static TextLineMetrics MeasureLineMetrics(TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        using var font = CreateFont(style with { IsItalic = false });
        var family = font.FontFamily;
        var lineSpacing = family.GetLineSpacing(font.Style);
        var emHeight = family.GetEmHeight(font.Style);
        var ascent = family.GetCellAscent(font.Style);
        if (lineSpacing <= 0 || emHeight <= 0)
        {
            return new TextLineMetrics(style.FontSize * 1.2, style.FontSize);
        }

        return new TextLineMetrics(
            style.FontSize * lineSpacing / emHeight,
            style.FontSize * ascent / emHeight);
    }

    /// <summary>行を描く（ベースラインを 1 本に決める）。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="line">行。</param>
    internal static void DrawLine(Graphics graphics, TextLinePlan line)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(line);

        var baseline = TextLineLayout.ResolveBaseline(line, MeasureLineMetrics);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        foreach (var segment in line.Segments)
        {
            DrawSegment(graphics, segment, baseline);
        }
    }

    /// <summary>書式から GDI+ のフォントを作る（下線・取り消し線は自前で引く）。</summary>
    /// <param name="style">書式。</param>
    /// <returns>フォント。 破棄は呼び出し側。</returns>
    internal static Font CreateFont(TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        var fontStyle = FontStyle.Regular;
        if (style.IsBold)
        {
            fontStyle |= FontStyle.Bold;
        }

        if (style.IsItalic)
        {
            fontStyle |= FontStyle.Italic;
        }

        return new Font(style.FontFamily, (float)style.FontSize, fontStyle, GraphicsUnit.Pixel);
    }

    /// <summary>区間を描く（下線・取り消し線を含む）。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="segment">区間。</param>
    /// <param name="baseline">行のベースライン。</param>
    private static void DrawSegment(Graphics graphics, TextSegmentPlan segment, double baseline)
    {
        var style = segment.Style;
        using var font = CreateFont(style);
        using var brush = new SolidBrush(style.Color);
        using var format = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap };

        // 文字の上端はベースラインから自分のベースラインを引いて決める（区間ごとに縦位置を決めない）
        var metrics = MeasureLineMetrics(style);
        var top = (float)(baseline - metrics.Baseline);
        graphics.DrawString(segment.Text, font, brush, new PointF((float)segment.Bounds.X, top), format);

        if (!style.IsUnderline && !style.IsStrike)
        {
            return;
        }

        var width = (float)(segment.Bounds.Width > 0 ? segment.Bounds.Width : Measure(segment.Text, style).Width);
        var thickness = (float)Math.Max(1.0, style.FontSize * TextDrawingAppearance.DecorationThicknessRatio);
        var height = (float)metrics.Height;
        using var pen = new Pen(style.Color, thickness);
        if (style.IsUnderline)
        {
            var y = top + height - (thickness * 2);
            graphics.DrawLine(pen, (float)segment.Bounds.X, y, (float)segment.Bounds.X + width, y);
        }

        if (style.IsStrike)
        {
            var y = top + (height / 2);
            graphics.DrawLine(pen, (float)segment.Bounds.X, y, (float)segment.Bounds.X + width, y);
        }
    }

    /// <summary>文字列の送り幅を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>送り幅（元画像ピクセル）。</returns>
    private static double MeasureWidth(string text, TextStyle style)
    {
        using var font = CreateFont(style);
        return s_measureGraphics.MeasureString(text, font, int.MaxValue, s_measureFormat).Width;
    }
}
