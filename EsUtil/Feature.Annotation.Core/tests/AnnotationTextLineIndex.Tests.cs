using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationTextLineIndex"/> の仕様テスト（キャレット・選択範囲・位置の対応）。</summary>
/// <remarks>
/// 実寸の測定は固定の計測関数（<see cref="FixedTextMetrics"/>。 1 文字 = 文字サイズ × 0.5）で行い、
/// 位置の計算だけを検証する（フォントの実測値に依存させない）。
/// </remarks>
public class AnnotationTextLineIndexTests
{
    /// <summary>明示改行を含む本文で、各行の開始位置が改行を 1 文字として進むことを検証する。</summary>
    [Fact]
    internal void 行の開始位置は改行を1文字として進む()
    {
        var index = CreateIndex("1行目\n2行目");

        Assert.Equal(0, index.LineStart(0));
        Assert.Equal(4, index.LineStart(1));
        Assert.Equal(7, index.TextLength);
    }

    /// <summary>文字位置から行を特定できることを検証する（行末はその行に属する）。</summary>
    [Fact]
    internal void 文字位置から行を特定できる()
    {
        var index = CreateIndex("1行目\n2行目");

        Assert.Equal(0, index.LineIndexOf(0));
        Assert.Equal(0, index.LineIndexOf(3));
        Assert.Equal(1, index.LineIndexOf(4));
        Assert.Equal(1, index.LineIndexOf(7));
    }

    /// <summary>キャレットの位置が行の先頭からの送り幅になることを検証する。</summary>
    [Fact]
    internal void キャレットの位置は行の先頭からの送り幅になる()
    {
        var index = CreateIndex("1行目\n2行目");
        var line = index.Lines[1];

        var caret = index.ResolveCaret(6, FixedTextMetrics.Measure);

        Assert.NotNull(caret);
        Assert.Equal(line.Bounds.X + (2 * 20 * FixedTextMetrics.CharacterWidthRatio), caret.Value.X, 3);
        Assert.Equal(line.Bounds.Y, caret.Value.Y, 3);
        Assert.Equal(line.Bounds.Height, caret.Value.Height, 3);
    }

    /// <summary>位置から最も近い文字位置を求めることを検証する。</summary>
    [Fact]
    internal void 位置から最も近い文字位置を求める()
    {
        var index = CreateIndex("1行目\n2行目");
        var line = index.Lines[1];

        var found = index.IndexAt(new EsPoint(line.Bounds.X + (2 * 10), line.Bounds.Y + 1), FixedTextMetrics.Measure);

        Assert.Equal(6, found);
    }

    /// <summary>選択範囲が行ごとの矩形へ分解されることを検証する。</summary>
    [Fact]
    internal void 選択範囲は行ごとの矩形へ分解される()
    {
        var index = CreateIndex("1行目\n2行目");

        var rects = index.ResolveSelectionRects(1, 6, FixedTextMetrics.Measure);

        Assert.Equal(2, rects.Count);
        Assert.Equal(index.Lines[0].Bounds.Y, rects[0].Y, 3);
        Assert.Equal(index.Lines[1].Bounds.Y, rects[1].Y, 3);
    }

    /// <summary>行が無い計画では対応を解決しないことを検証する。</summary>
    [Fact]
    internal void 行が無い計画では解決しない()
    {
        var shape = new AnnotationShapePlan(
            "a", AnnotationKind.Frame, new EsRect(0, 0, 10, 10), 0,
            System.Drawing.Color.Transparent, System.Drawing.Color.Black, 1, [], []);

        Assert.Null(AnnotationTextLineIndex.Create(shape, string.Empty));
    }

    /// <summary>本文の行を持つ対応を作る。</summary>
    /// <param name="text">本文。</param>
    /// <returns>解決した対応。</returns>
    private static AnnotationTextLineIndex CreateIndex(string text)
    {
        var bubble = new BubbleAnnotation
        {
            Bounds = new EsRect(0, 0, 600, 300),
            Appearance = AnnotationDefaults.BubbleAppearance,
            BodyPadding = 12,
            Text = RichText.Create(text, AnnotationDefaults.TextStyle with { FontSize = 20 }),
        };

        var document = new AnnotationDocument(600, 300, "shot.png").Add(bubble);
        var plan = new AnnotationPresenter().ResolveDrawPlan(document, FixedTextMetrics.Measure);
        return AnnotationTextLineIndex.Create(Assert.Single(plan.Shapes), text)!;
    }
}
