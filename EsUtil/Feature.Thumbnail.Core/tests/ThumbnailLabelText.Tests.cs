using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Thumbnail.Tests;

/// <summary>名前行へ収まるよう文言を整える計算の単体テスト。</summary>
public class ThumbnailLabelTextTests
{
    /// <summary>2 行に収まる名前はそのまま返すことを検証する。</summary>
    [Fact]
    internal void Fit_収まる名前は変えない()
    {
        Assert.Equal(
            "capture-0001.png",
            ThumbnailLabelText.Fit("capture-0001.png", 160, ThumbnailLabelText.ResolveHeight(11, ThumbnailLabelText.MaxLines), 11));
    }

    /// <summary>2 行に収まらない名前は拡張子を残して省略することを検証する。</summary>
    [Fact]
    internal void Fit_収まらない名前は拡張子を残して省略する()
    {
        var fitted = ThumbnailLabelText.Fit(
            "capture-0123456789-abcdefghij.png", 60, ThumbnailLabelText.ResolveHeight(11, ThumbnailLabelText.MaxLines), 11);

        Assert.EndsWith(".png", fitted, StringComparison.Ordinal);
        Assert.Contains(ThumbnailLabelText.Ellipsis, fitted, StringComparison.Ordinal);
        Assert.True(fitted.Length < "capture-0123456789-abcdefghij.png".Length);
    }

    /// <summary>幅が未確定の場合は切り詰めないことを検証する。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    internal void Fit_幅が未確定の場合は切り詰めない(double width)
    {
        var height = ThumbnailLabelText.ResolveHeight(11, ThumbnailLabelText.MaxLines);

        Assert.Equal("capture-0001.png", ThumbnailLabelText.Fit("capture-0001.png", width, height, 11));
    }

    /// <summary>null と空文字はそのまま扱うことを検証する。</summary>
    [Fact]
    internal void Fit_nullと空文字はそのまま返す()
    {
        var height = ThumbnailLabelText.ResolveHeight(11, ThumbnailLabelText.MaxLines);

        Assert.Equal(string.Empty, ThumbnailLabelText.Fit(null, 160, height, 11));
        Assert.Equal(string.Empty, ThumbnailLabelText.Fit(string.Empty, 160, height, 11));
    }

    /// <summary>名前行の高さが行送り × 行数になることを検証する。</summary>
    [Fact]
    internal void ResolveHeight_行送りと行数から高さを求める()
    {
        var expected = Math.Ceiling(11 * ThumbnailLabelText.LineSpacing) * ThumbnailLabelText.MaxLines;

        Assert.Equal(expected, ThumbnailLabelText.ResolveHeight(11, ThumbnailLabelText.MaxLines), 6);
    }

    /// <summary>文字サイズが正でない場合は高さ 0 になることを検証する。</summary>
    [Fact]
    internal void ResolveHeight_文字サイズが正でない場合は0になる()
    {
        Assert.Equal(0, ThumbnailLabelText.ResolveHeight(0, ThumbnailLabelText.MaxLines), 6);
        Assert.Equal(0, ThumbnailLabelText.ResolveHeight(11, 0), 6);
    }

    /// <summary>高さに収まる行数を求められることを検証する。</summary>
    [Fact]
    internal void ResolveMaxLines_高さから行数を求める()
    {
        var lineHeight = ThumbnailLabelText.ResolveLineHeight(11);

        Assert.Equal(2, ThumbnailLabelText.ResolveMaxLines(lineHeight * 2, 11));
        Assert.Equal(1, ThumbnailLabelText.ResolveMaxLines(lineHeight * 1.5, 11));
        Assert.Equal(2, ThumbnailLabelText.ResolveMaxLines(lineHeight * 2.9, 11));
    }

    /// <summary>実寸を測る場合は 1 行に収まる名前を省略しないことを検証する。</summary>
    [Fact]
    internal void FitMeasured_1行に収まる名前は省略しない()
    {
        var fitted = ThumbnailLabelText.Fit(
            "capture-0001.png", 200, ThumbnailLabelText.ResolveLineHeight(11), 11, false, FixedWidthMeasure);

        Assert.Equal("capture-0001.png", fitted);
    }

    /// <summary>実寸を測る場合も 1 行に収まらない名前は拡張子を残して省略することを検証する。</summary>
    [Fact]
    internal void FitMeasured_1行に収まらない名前は拡張子を残して省略する()
    {
        var height = ThumbnailLabelText.ResolveLineHeight(11);
        var fitted = ThumbnailLabelText.Fit(
            "capture-0123456789-abcd.png", 60, height, 11, false, FixedWidthMeasure);

        Assert.EndsWith(".png", fitted, StringComparison.Ordinal);
        Assert.Contains(ThumbnailLabelText.Ellipsis, fitted, StringComparison.Ordinal);
        Assert.True(FixedWidthMeasure(fitted, 60, 11, false).Height <= height + 0.5);
    }

    /// <summary>太字では 1 文字が広いぶん早く省略されることを検証する。</summary>
    [Fact]
    internal void FitMeasured_太字は早く省略する()
    {
        const string name = "capture-0001-long-name.png";
        var height = ThumbnailLabelText.ResolveHeight(11, ThumbnailLabelText.MaxLines);

        var normal = ThumbnailLabelText.Fit(name, 120, height, 11, false, FixedWidthMeasure);
        var bold = ThumbnailLabelText.Fit(name, 120, height, 11, true, FixedWidthMeasure);

        Assert.True(bold.Length <= normal.Length);
    }

    /// <summary>高さが 1 行分も無い場合は省略記号と拡張子だけを返すことを検証する。</summary>
    [Fact]
    internal void FitMeasured_高さが足りない場合は省略記号だけを返す()
    {
        var fitted = ThumbnailLabelText.Fit(
            "capture-0001.png", 60, ThumbnailLabelText.ResolveLineHeight(11) / 2, 11, false, FixedWidthMeasure);

        Assert.Equal(string.Concat(ThumbnailLabelText.Ellipsis, ".png"), fitted);
    }

    /// <summary>1 文字を文字サイズの 0.6 倍の幅として測る固定の測定（描画 API を使わない検証用）。</summary>
    /// <param name="text">測る文言。</param>
    /// <param name="maxWidth">折り返す幅（DIP）。</param>
    /// <param name="fontSize">文字サイズ（DIP）。</param>
    /// <param name="isBold">太字で測るかどうか（幅を 1 割増しにする）。</param>
    /// <returns>折り返した後の実寸（DIP）。</returns>
    private static EsSize FixedWidthMeasure(string text, double maxWidth, double fontSize, bool isBold)
    {
        var charWidth = fontSize * 0.6 * (isBold ? 1.1 : 1.0);
        var charsPerLine = Math.Max(1, (int)(maxWidth / charWidth));
        var lines = Math.Max(1, (int)Math.Ceiling((double)text.Length / charsPerLine));

        return new EsSize(Math.Min(maxWidth, text.Length * charWidth), lines * ThumbnailLabelText.ResolveLineHeight(fontSize));
    }
}
