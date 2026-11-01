using Xunit;

namespace EsUtil.Feature.Thumbnail.Tests;

/// <summary>外観から決まる寸法（<see cref="ThumbnailMetrics"/>）の単体テスト。</summary>
public class ThumbnailMetricsTests
{
    /// <summary>負の値と非数が 0 へ補正されることを検証する。</summary>
    /// <param name="value">指定する値。</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    internal void 負の値と非数は0になる(double value)
    {
        var metrics = new ThumbnailMetrics(value, value);

        Assert.Equal(0, metrics.SlotInset, 6);
        Assert.Equal(0, metrics.LabelHeight, 6);
    }

    /// <summary>指定した値をそのまま保持することを検証する。</summary>
    [Fact]
    internal void 指定した値を保持する()
    {
        var metrics = new ThumbnailMetrics(10, 30);

        Assert.Equal(10, metrics.SlotInset, 6);
        Assert.Equal(30, metrics.LabelHeight, 6);
    }

    /// <summary>0 の寸法が余白も名前の行も確保しないことを検証する。</summary>
    [Fact]
    internal void Zero_は余白も名前の行も持たない()
    {
        Assert.Equal(0, ThumbnailMetrics.Zero.SlotInset, 6);
        Assert.Equal(0, ThumbnailMetrics.Zero.LabelHeight, 6);
    }
}
