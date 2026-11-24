using Avalonia.Headless.XUnit;
using Xunit;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Text;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Tests;

/// <summary>本文の行の高さとベースラインが装飾（斜体）で変わらないことを検証する。</summary>
/// <remarks>
/// 斜体は字を傾けるだけであり、行の高さ・ベースラインを変えてはいけない。 区間ごとの実寸で縦位置を
/// 決めると、斜体にした区間だけが上下へずれ、行の高さも増える。 文字の描画と実寸は
/// <see cref="StyledText.RichTextPainter"/> が単一所有する（注釈は写しを持たない）。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class AnnotationPainterTests
{
    /// <summary>行の高さを測る書式から斜体が外れることを検証する。</summary>
    /// <remarks>
    /// 幅（折り返しの判定）は指定どおりの書式で測るが、行の高さは斜体を外した書式で測る
    /// （<see cref="StyledText.RichTextPainter.CreateLineMetrics"/>）。
    /// </remarks>
    [AvaloniaFact]
    internal void 行の高さを測る書式から斜体が外れる()
    {
        var normal = TextDefaults.DefaultStyle;
        var italic = normal with { IsItalic = true };

        var normalMetrics = AnnotationPainter.CreateLineMetrics(normal);
        var italicMetrics = AnnotationPainter.CreateLineMetrics(italic);

        Assert.Equal(normalMetrics.Height, italicMetrics.Height, 3);
        Assert.Equal(normalMetrics.Baseline, italicMetrics.Baseline, 3);
    }

    /// <summary>行のベースラインが、斜体の区間があっても動かないことを検証する。</summary>
    /// <remarks>
    /// ベースラインは行の中で最も大きい文字（斜体を外した実寸）から決まる。 斜体の区間を足しても
    /// 値が変わらなければ、斜体にした文字だけが上下へずれない。
    /// </remarks>
    [AvaloniaFact]
    internal void 行のベースラインは斜体の区間があっても動かない()
    {
        var normal = TextDefaults.DefaultStyle;
        var italic = normal with { IsItalic = true };
        var line = new TextLinePlan(
            new EsRect(0, 100, 200, 30),
            [
                new TextSegmentPlan("文字", normal, new EsRect(0, 100, 100, 30)),
                new TextSegmentPlan("斜体", italic, new EsRect(100, 100, 100, 30)),
            ]);

        var normalOnly = line with
        {
            Segments = [new TextSegmentPlan("文字", normal, new EsRect(0, 100, 100, 30))],
        };

        Assert.Equal(
            TextLineLayout.ResolveBaseline(normalOnly, StyledText.RichTextPainter.MeasureLineMetrics),
            TextLineLayout.ResolveBaseline(line, StyledText.RichTextPainter.MeasureLineMetrics),
            3);
    }

    /// <summary>文字を描く位置がベースラインで揃うことを検証する。</summary>
    /// <remarks>
    /// 斜体と通常の区間で <c>FormattedText.Baseline</c> が違っても、同じベースラインへ載せれば
    /// 文字の下端（ベースライン）が一致する。 上端はベースラインから測るため、実寸の高さに依存しない。
    /// </remarks>
    [AvaloniaFact]
    internal void 斜体と通常の区間でベースラインが揃う()
    {
        var normal = AnnotationPainter.CreateText("文字", TextDefaults.DefaultStyle);
        var italic = AnnotationPainter.CreateText("文字", TextDefaults.DefaultStyle with { IsItalic = true });
        var line = new TextLinePlan(
            new EsRect(0, 0, 200, 30),
            [new TextSegmentPlan("文字", TextDefaults.DefaultStyle, new EsRect(0, 0, 100, 30))]);

        var baseline = TextLineLayout.ResolveBaseline(line, StyledText.RichTextPainter.MeasureLineMetrics);

        var normalTop = baseline - normal.Baseline;
        var italicTop = baseline - italic.Baseline;

        Assert.Equal(normalTop + normal.Baseline, italicTop + italic.Baseline, 3);
    }

    /// <summary>斜体にしても行の高さ（実寸の高さ）が変わらないことを検証する。</summary>
    /// <remarks>
    /// 斜体は字を傾けるだけであり、行の間隔を変えてはいけない。 実寸の高さに斜体の字の高さを使うと、
    /// 斜体を設定した行だけ背が高くなり、行の間隔が変わる。
    /// </remarks>
    [AvaloniaFact]
    internal void 斜体にしても行の高さが変わらない()
    {
        var normal = TextDefaults.DefaultStyle;
        var italic = normal with { IsItalic = true };

        Assert.Equal(
            AnnotationPainter.Measure("文字", normal).Height,
            AnnotationPainter.Measure("文字", italic).Height,
            3);
    }
}