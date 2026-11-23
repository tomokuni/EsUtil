using Avalonia.Headless.XUnit;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Tests;

/// <summary>本文の行の高さとベースラインが装飾（斜体）で変わらないことを検証する。</summary>
/// <remarks>
/// 斜体は字を傾けるだけであり、行の高さ・ベースラインを変えてはいけない。
/// 区間ごとの実寸で縦位置を決めると、斜体にした区間だけが上下へずれ、行の高さも増える（実測 2026-11-18）。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class AnnotationPainterTests
{
    /// <summary>行の高さを測る書式から斜体が外れることを検証する。</summary>
    /// <remarks>
    /// 幅（折り返しの判定）は指定どおりの書式で測るが、行の高さは斜体を外した書式で測る
    /// （<see cref="AnnotationPainter.CreateLineMetrics"/>）。
    /// </remarks>
    [AvaloniaFact]
    internal void 行の高さを測る書式から斜体が外れる()
    {
        var normal = AnnotationDefaults.TextStyle;
        var italic = normal with { IsItalic = true };

        var normalMetrics = AnnotationPainter.CreateLineMetrics(normal);
        var italicMetrics = AnnotationPainter.CreateLineMetrics(italic);

        Assert.Equal(normalMetrics.Height, italicMetrics.Height, 3);
        Assert.Equal(normalMetrics.Baseline, italicMetrics.Baseline, 3);
    }

    /// <summary>行のベースラインが、区間ごとの実寸ではなく行の基準で決まることを検証する。</summary>
    /// <remarks>
    /// 斜体の区間で実寸（高さ・ベースライン）が変わっても、描画位置は
    /// <see cref="AnnotationPainter.ResolveBaseline"/>（行の高さと基準の書式から求める）で決まる。
    /// 同じ行の別の区間と同じベースラインになることを確かめる。
    /// </remarks>
    [AvaloniaFact]
    internal void 行のベースラインは行の基準で決まる()
    {
        const double lineTop = 100;
        const double lineHeight = 30;
        const double referenceHeight = 24;
        const double referenceBaseline = 19;

        var baseline = AnnotationPainter.ResolveBaseline(
            lineTop, lineHeight, referenceHeight, referenceBaseline);

        // 行の高さいっぱいへ中央寄せした位置（上端 + (行の高さ − 実寸) / 2 + ベースライン）
        Assert.Equal(100 + 3 + 19, baseline, 3);

        // 斜体の区間も、このベースラインから自分のベースラインぶん戻した位置に描かれる
        var italic = AnnotationPainter.CreateText("文字", AnnotationDefaults.TextStyle with { IsItalic = true });
        var top = baseline - italic.Baseline;
        Assert.Equal(baseline, top + italic.Baseline, 3);
    }

    /// <summary>文字を描く位置がベースラインで揃うことを検証する。</summary>
    /// <remarks>
    /// 斜体と通常の区間で <c>FormattedText.Baseline</c> が違っても、同じベースラインへ載せれば
    /// 文字の下端（ベースライン）が一致する。 上端はベースラインから測るため、実寸の高さに依存しない。
    /// </remarks>
    [AvaloniaFact]
    internal void 斜体と通常の区間でベースラインが揃う()
    {
        var normal = AnnotationPainter.CreateText("文字", AnnotationDefaults.TextStyle);
        var italic = AnnotationPainter.CreateText("文字", AnnotationDefaults.TextStyle with { IsItalic = true });

        var baseline = AnnotationPainter.ResolveBaseline(0, 30, 24, 19);

        var normalTop = baseline - normal.Baseline;
        var italicTop = baseline - italic.Baseline;

        Assert.Equal(normalTop + normal.Baseline, italicTop + italic.Baseline, 3);
    }
}
