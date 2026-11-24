using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace EsUtil.Ui.WinUI3.StyledText;

/// <summary>書式付き文字列の実寸の計測（XAML のテキスト エンジンで測る）。</summary>
/// <remarks>
/// <b>描くエンジンで測る</b>: 本文は <see cref="TextBlock"/> として置くため、実寸も <see cref="TextBlock"/> で測る
/// （文字数からの見積もりでは折り返しの位置が実際の描画とずれる）。<br/>
/// <b>UI スレッドから呼ぶ</b>: 測定はツリー外の <see cref="TextBlock"/> を作るため、XAML の UI スレッドで行う。<br/>
/// <b>行の高さは斜体で変えない</b>: 行の高さは斜体を外した書式で測る（UI 版で同じ規則）。
/// </remarks>
public static class RichTextMetrics
{
    /// <summary>行の高さを測るための文字列。</summary>
    private const string LineMetricsText = "Ag";

    /// <summary>測定に使う要素（初回に生成して使い回す）。</summary>
    private static TextBlock? s_probe;

    /// <summary>文字列と書式から実寸を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。 高さは斜体を外した書式で測る。</returns>
    /// <remarks>
    /// <b>ホストが自分のコントロールの <c>MeasureText</c> へ渡せる</b>: 注釈などのホストは、本文と同じ
    /// テキスト エンジンで測る必要がある（測り方が違うと折り返しと文字の位置が食い違う）。
    /// </remarks>
    public static EsSize Measure(string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);

        var height = MeasureSize(LineMetricsText, style with { IsItalic = false }).Height;
        var width = text.Length == 0 ? 0 : MeasureSize(text, style).Width;
        return new EsSize(width, height);
    }

    /// <summary>色を XAML のブラシへ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>単色のブラシ。</returns>
    internal static SolidColorBrush ToBrush(System.Drawing.Color color)
        => new(Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B));

    /// <summary>区間の文字を置く要素を作る。</summary>
    /// <param name="segment">区間。</param>
    /// <param name="scale">表示倍率。</param>
    /// <returns>文字の要素。</returns>
    internal static TextBlock CreateText(TextSegmentPlan segment, double scale)
    {
        var style = segment.Style;
        var text = new TextBlock
        {
            Text = segment.Text,
            FontFamily = new FontFamily(style.FontFamily),
            FontSize = style.FontSize * scale,
            FontWeight = style.IsBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = style.IsItalic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            Foreground = ToBrush(style.Color),
            TextWrapping = TextWrapping.NoWrap,
            IsHitTestVisible = false,
        };

        var decorations = Windows.UI.Text.TextDecorations.None;
        if (style.IsUnderline)
        {
            decorations |= Windows.UI.Text.TextDecorations.Underline;
        }

        if (style.IsStrike)
        {
            decorations |= Windows.UI.Text.TextDecorations.Strikethrough;
        }

        if (decorations != Windows.UI.Text.TextDecorations.None)
        {
            text.TextDecorations = decorations;
        }

        return text;
    }

    /// <summary>行の中で区間の文字を置く上端（元画像ピクセル）を求める。</summary>
    /// <param name="line">行。</param>
    /// <param name="segment">区間。</param>
    /// <returns>上端の Y 座標（行の上端からの中央寄せ）。</returns>
    internal static double ResolveTextTop(TextLinePlan line, TextSegmentPlan segment)
    {
        var runHeight = Measure("Ag", segment.Style with { IsItalic = false }).Height;
        return line.Bounds.Y + ((line.Bounds.Height - runHeight) / 2);
    }

    /// <summary>文字列の実寸を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。</returns>
    private static EsSize MeasureSize(string text, TextStyle style)
    {
        var probe = s_probe ??= new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            IsTextScaleFactorEnabled = false,
            UseLayoutRounding = false,
        };

        probe.FontFamily = new FontFamily(style.FontFamily);
        probe.FontSize = style.FontSize;
        probe.FontWeight = style.IsBold ? FontWeights.Bold : FontWeights.Normal;
        probe.FontStyle = style.IsItalic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
        probe.Text = text;
        probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

        var desired = probe.DesiredSize;
        probe.Text = string.Empty;
        return new EsSize(desired.Width, desired.Height);
    }
}
