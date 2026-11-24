using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Text;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>本文の実寸の測定（GDI+ のテキスト エンジンで測る）。</summary>
/// <remarks>
/// <b>描くエンジンで測る</b>: 本文は GDI+ で描くため、実寸も GDI+ で測る（文字数からの見積もりでは折り返しの位置が
/// 実際の描画とずれる）。<br/>
/// <b>行の高さは斜体で変えない</b>: 行の高さはフォントの行送りから求め、斜体は字を傾けるだけにする
/// （斜体にすると行の間隔が変わる問題を避ける。 4 つの UI 版で同じ規則）。<br/>
/// <b>測定用の面は使い回す</b>: 画面に依存しない測面（<see cref="Graphics.FromHwnd"/> の 0 番）を 1 つだけ持つ。
/// </remarks>
internal static class AnnotationTextMetrics
{
    /// <summary>測定に使う書式（文字の送りだけを測る）。</summary>
    private static readonly StringFormat s_format = new(StringFormat.GenericTypographic)
    {
        FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap,
    };

    /// <summary>測定に使う面（初回に生成して使い回す）。</summary>
    private static readonly Graphics s_graphics = Graphics.FromHwnd(IntPtr.Zero);

    /// <summary>文字列と書式から実寸を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。 高さは斜体を外した書式で測る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> または <paramref name="style"/> が null の場合。</exception>
    internal static EsSize Measure(string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);

        var width = text.Length == 0 ? 0 : MeasureWidth(text, style);
        return new EsSize(width, ResolveLineHeight(style));
    }

    /// <summary>行の高さを求める（斜体を外した書式で測る）。</summary>
    /// <param name="style">書式。</param>
    /// <returns>行の高さ（元画像ピクセル）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> が null の場合。</exception>
    internal static double ResolveLineHeight(TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        using var font = CreateFont(style with { IsItalic = false });
        var family = font.FontFamily;
        var lineSpacing = family.GetLineSpacing(font.Style);
        var emHeight = family.GetEmHeight(font.Style);
        if (lineSpacing <= 0 || emHeight <= 0)
        {
            return style.FontSize * 1.2;
        }

        return style.FontSize * lineSpacing / emHeight;
    }

    /// <summary>行の高さとベースライン（上端から）を求める。</summary>
    /// <param name="style">書式。</param>
    /// <returns>行の高さとベースライン。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> が null の場合。</exception>
    internal static (double Height, double Baseline) ResolveLineMetrics(TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        using var font = CreateFont(style with { IsItalic = false });
        var family = font.FontFamily;
        var lineSpacing = family.GetLineSpacing(font.Style);
        var emHeight = family.GetEmHeight(font.Style);
        var ascent = family.GetCellAscent(font.Style);
        if (lineSpacing <= 0 || emHeight <= 0)
        {
            return (style.FontSize * 1.2, style.FontSize);
        }

        var height = style.FontSize * lineSpacing / emHeight;
        return (height, style.FontSize * ascent / emHeight);
    }

    /// <summary>文字列の送り幅を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>送り幅（元画像ピクセル）。</returns>
    private static double MeasureWidth(string text, TextStyle style)
    {
        using var font = CreateFont(style);
        return s_graphics.MeasureString(text, font, int.MaxValue, s_format).Width;
    }

    /// <summary>書式から GDI+ のフォントを作る。</summary>
    /// <param name="style">書式。</param>
    /// <returns>フォント。 破棄は呼び出し側。</returns>
    internal static Font CreateFont(TextStyle style)
    {
        var fontStyle = FontStyle.Regular;
        if (style.IsBold)
        {
            fontStyle |= FontStyle.Bold;
        }

        if (style.IsItalic)
        {
            fontStyle |= FontStyle.Italic;
        }

        if (style.IsUnderline)
        {
            fontStyle |= FontStyle.Underline;
        }

        if (style.IsStrike)
        {
            fontStyle |= FontStyle.Strikeout;
        }

        return new Font(style.FontFamily, (float)style.FontSize, fontStyle, GraphicsUnit.Pixel);
    }
}
