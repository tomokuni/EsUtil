using Xunit;

namespace EsUtil.Feature.Thumbnail.Tests;

/// <summary>名前行へ収まるよう文言を整える計算の単体テスト。</summary>
public class ThumbnailLabelTextTests
{
    /// <summary>2 行に収まる名前はそのまま返すことを検証する。</summary>
    [Fact]
    internal void Fit_収まる名前は変えない()
    {
        Assert.Equal("capture-0001.png", ThumbnailLabelText.Fit("capture-0001.png", 160, 11, ThumbnailLabelText.MaxLines));
    }

    /// <summary>2 行に収まらない名前は拡張子を残して省略することを検証する。</summary>
    [Fact]
    internal void Fit_収まらない名前は拡張子を残して省略する()
    {
        var fitted = ThumbnailLabelText.Fit("capture-0123456789-abcdefghij.png", 60, 11, ThumbnailLabelText.MaxLines);

        Assert.EndsWith(".png", fitted, StringComparison.Ordinal);
        Assert.Contains(ThumbnailLabelText.Ellipsis, fitted, StringComparison.Ordinal);
        Assert.True(fitted.Length < "capture-0123456789-abcdefghij.png".Length);
    }

    /// <summary>寸法が未確定の場合は切り詰めないことを検証する。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    internal void Fit_幅が未確定の場合は切り詰めない(double width)
        => Assert.Equal("capture-0001.png", ThumbnailLabelText.Fit("capture-0001.png", width, 11, ThumbnailLabelText.MaxLines));

    /// <summary>null と空文字はそのまま扱うことを検証する。</summary>
    [Fact]
    internal void Fit_nullと空文字はそのまま返す()
    {
        Assert.Equal(string.Empty, ThumbnailLabelText.Fit(null, 160, 11, ThumbnailLabelText.MaxLines));
        Assert.Equal(string.Empty, ThumbnailLabelText.Fit(string.Empty, 160, 11, ThumbnailLabelText.MaxLines));
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
}
