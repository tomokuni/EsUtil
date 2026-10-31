using EsUtil.Platform.Core.Common;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary>自然な数値表記（<see cref="NumberText"/>）の単体テスト。</summary>
/// <remarks>
/// 目的は「2 進小数の誤差をログへ出さない」ことである。 丸めの桁数を固定しないため、
/// 値ごとに最短の自然な表記になることを確かめる。
/// </remarks>
public class NumberTextTests
{
    [Fact]
    internal void 実数の誤差は丸めて自然な表記にする()
    {
        Assert.Equal("1.6", NumberText.Format(1.5999999999999996));
        Assert.Equal("0.3", NumberText.Format(0.30000000000000004));
        Assert.Equal("1.56", NumberText.Format(1.5599999999999996));
    }

    [Fact]
    internal void 整数は小数点を付けない()
    {
        Assert.Equal("4", NumberText.Format(4.0));
        Assert.Equal("240", NumberText.Format(240.0));
        Assert.Equal("0", NumberText.Format(0.0));
    }

    [Fact]
    internal void 小数は末尾の0を付けない()
    {
        Assert.Equal("0.5", NumberText.Format(0.5));
        Assert.Equal("1.25", NumberText.Format(1.25));
    }

    [Fact]
    internal void 指数表記にしない()
    {
        Assert.Equal("0.000001", NumberText.Format(0.000001));
        Assert.Equal("1000000000000", NumberText.Format(1_000_000_000_000.0));
    }

    [Fact]
    internal void カルチャに依存しない()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            Assert.Equal("1.6", NumberText.Format(1.5999999999999996));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    internal void 整数型はそのままの表記にする()
    {
        Assert.Equal("8", NumberText.Format(8));
        Assert.Equal("-1", NumberText.Format(-1));
    }

    [Fact]
    internal void 有限でない値は型の標準表記にする()
    {
        Assert.Equal("NaN", NumberText.Format(double.NaN));
        Assert.Equal("∞", NumberText.Format(double.PositiveInfinity).Replace("Infinity", "∞", StringComparison.Ordinal));
    }
}
