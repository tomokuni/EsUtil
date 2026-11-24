using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.StyledText.Core.Tests;

/// <summary>
/// <see cref="TextLineLayout"/> の規則（描画と同じ区間の原点から位置を決める）を検証する。
/// </summary>
/// <remarks>
/// <b>キャレット・選択範囲・当たり判定は描画と一致しなければならない</b>。 行頭から幅を積み上げる
/// 計算と、区間ごとに描く描画とで基準が違うと、文字とキャレットがずれる。
/// </remarks>
public sealed class TextLineLayoutTests
{
    /// <summary>文字幅（元画像ピクセル）。 区間ごとに違えて、積み上げの誤りを見つけられるようにする。</summary>
    private const double Wide = 10;

    /// <summary>行の先頭からの文字位置は区間の原点からの距離で求める。</summary>
    /// <remarks>
    /// 区間の 2 つ目以降は <see cref="TextSegmentPlan.Bounds"/> の位置から測る。
    /// 区間の幅を積み上げる計算（行頭 + 合計）とは、端数の丸めで差が出る。
    /// </remarks>
    [Fact]
    internal void ResolveX_区間の原点から測る()
    {
        var line = CreateLine(new EsRect(100, 20, 60, 20));

        // 2 文字目まで（区間 1 の内部）
        Assert.Equal(120, TextLineLayout.ResolveX(line, 2, Prefix), 3);

        // 区間の境界（区間 1 の末尾 = 区間 2 の先頭）は次の区間の原点
        Assert.Equal(130, TextLineLayout.ResolveX(line, 3, Prefix), 3);

        // 区間 2 の内部
        Assert.Equal(140, TextLineLayout.ResolveX(line, 4, Prefix), 3);

        // 行の末尾
        Assert.Equal(160, TextLineLayout.ResolveX(line, 6, Prefix), 3);
    }

    /// <summary>X 座標から文字位置を求めると、位置から X を求めた結果と一致する。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    internal void ResolveOffset_ResolveXと往復する(int offset)
    {
        var line = CreateLine(new EsRect(100, 20, 60, 20));

        var x = TextLineLayout.ResolveX(line, offset, Prefix);

        Assert.Equal(offset, TextLineLayout.ResolveOffset(line, x, Prefix));
    }

    /// <summary>文字の無い区間が混ざっても位置が崩れない。</summary>
    [Fact]
    internal void ResolveX_空の区間を飛ばす()
    {
        var line = new TextLinePlan(
            new EsRect(0, 0, 30, 20),
            [
                new TextSegmentPlan("ab", Style, new EsRect(0, 0, 20, 20)),
                new TextSegmentPlan(string.Empty, Style, new EsRect(20, 0, 0, 20)),
                new TextSegmentPlan("cd", Style, new EsRect(20, 0, 20, 20)),
            ]);

        Assert.Equal(20, TextLineLayout.ResolveX(line, 2, Prefix), 3);
        Assert.Equal(30, TextLineLayout.ResolveX(line, 3, Prefix), 3);
        Assert.Equal(40, TextLineLayout.ResolveX(line, 4, Prefix), 3);
    }

    /// <summary>行のベースラインは行の中で最も大きい文字を基準に決める。</summary>
    /// <remarks>
    /// 区間ごとに縦位置を決めると、書式が混ざった行で文字が上下へずれる。 描画もキャレットも同じ
    /// ベースラインを使う。
    /// </remarks>
    [Fact]
    internal void ResolveBaseline_最も大きい文字を基準にする()
    {
        var small = Style;
        var large = Style with { FontSize = 40 };
        var line = new TextLinePlan(
            new EsRect(0, 100, 100, 40),
            [
                new TextSegmentPlan("a", small, new EsRect(0, 100, 10, 40)),
                new TextSegmentPlan("B", large, new EsRect(10, 100, 20, 40)),
            ]);

        var baseline = TextLineLayout.ResolveBaseline(
            line, style => new TextLineMetrics(style.FontSize, style.FontSize * 0.8));

        // 上端 100 + (40 - 40) / 2 + 40 × 0.8
        Assert.Equal(132, baseline, 3);
    }

    /// <summary>行の文字数は区間の文字数の合計になる。</summary>
    [Fact]
    internal void Length_区間の文字数を合計する()
    {
        Assert.Equal(6, TextLineLayout.Length(CreateLine(new EsRect(0, 0, 60, 20))));
    }

    /// <summary>本文の行から当たり判定とキャレットの位置が同じ規則で求まることを検証する。</summary>
    /// <remarks>
    /// <see cref="TextLineIndex"/>（当たり判定とキャレットの入口）が
    /// <see cref="TextLineLayout"/> と同じ結果を返す（2 つの計算経路を作らない）。
    /// </remarks>
    [Fact]
    internal void LineIndex_当たり判定とキャレットが一致する()
    {
        var line = CreateLine(new EsRect(100, 20, 60, 20));
        var index = TextLineIndex.Create([line], "abc def");

        var caret = index!.ResolveCaret(4, FixedTextMetrics.Measure);

        Assert.True(caret.HasValue);
        Assert.Equal(TextLineLayout.ResolveX(line, 4, SegmentPrefix), caret.Value.X, 3);
        Assert.Equal(4, index.IndexAt(new EsPoint(caret.Value.X, caret.Value.Y + 1), FixedTextMetrics.Measure));
    }

    /// <summary>区間の幅を測る（テスト用。 1 文字 = <see cref="Wide"/> ピクセル）。</summary>
    private static double Prefix(TextSegmentPlan segment, int count)
    {
        _ = segment;
        return count * Wide;
    }

    /// <summary>区間の幅を測る（<see cref="FixedTextMetrics"/> と同じ比率）。</summary>
    private static double SegmentPrefix(TextSegmentPlan segment, int count)
        => FixedTextMetrics.Measure(segment.Text[..count], segment.Style).Width;

    /// <summary>テスト用の行を作る（"ab" と "cdef" の 2 区間）。</summary>
    private static TextLinePlan CreateLine(EsRect bounds) => new(
        bounds,
        [
            new TextSegmentPlan("ab", Style, new EsRect(bounds.X, bounds.Y, 20, bounds.Height)),
            new TextSegmentPlan("cdef", Style, new EsRect(bounds.X + 20, bounds.Y, 40, bounds.Height)),
        ]);

    /// <summary>テスト用の書式。</summary>
    private static TextStyle Style => TextDefaults.DefaultStyle;
}
