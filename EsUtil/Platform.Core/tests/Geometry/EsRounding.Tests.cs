using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsRounding"/> を使う丸め規則（<c>EsMath.RoundToInt</c>）のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 丸め規則は <c>EsMath</c>（internal）が単一所有するため、本テストは <c>InternalsVisibleTo</c> 経由で直接検証する。<br/>
/// 値型の計算のみのため、UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsRoundingTests
{
    [Theory]
    [InlineData(1.0, 1)]
    [InlineData(1.4, 1)]
    [InlineData(1.5, 1)]
    [InlineData(1.9, 1)]
    [InlineData(-1.4, -1)]
    [InlineData(-1.5, -1)]
    [InlineData(-1.9, -1)]
    internal void Truncate_0の方向へ切り捨てる(double value, int expected)
    {
        Assert.Equal(expected, EsMath.RoundToInt(value, EsRounding.Truncate));
    }

    [Theory]
    [InlineData(1.4, 1)]
    [InlineData(1.5, 2)]
    [InlineData(2.5, 3)]
    [InlineData(-1.4, -1)]
    [InlineData(-1.5, -2)]
    [InlineData(-2.5, -3)]
    internal void Round_最も近い整数へ丸める_0_5は0から遠い方へ(double value, int expected)
    {
        Assert.Equal(expected, EsMath.RoundToInt(value, EsRounding.Round));
    }

    [Theory]
    [InlineData(1.0, 1)]
    [InlineData(1.4, 2)]
    [InlineData(-1.0, -1)]
    [InlineData(-1.4, -1)]
    internal void Ceiling_正の無限大の方向へ切り上げる(double value, int expected)
    {
        Assert.Equal(expected, EsMath.RoundToInt(value, EsRounding.Ceiling));
    }

    [Fact]
    internal void RoundToInt_未定義の丸め方法は例外を投げる()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EsMath.RoundToInt(1.0, (EsRounding)999));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1e-12, 0, true)]
    [InlineData(1e-3, 0, false)]
    [InlineData(3.0, 3.0, true)]
    internal void AreClose_許容誤差内なら真(double a, double b, bool expected)
    {
        Assert.Equal(expected, EsMath.AreClose(a, b));
    }

    [Fact]
    internal void AreClose_非数は偽()
    {
        Assert.False(EsMath.AreClose(double.NaN, double.NaN));
        Assert.False(EsMath.AreClose(1.0, double.NaN));
    }

    [Fact]
    internal void AreClose_同じ無限大は真()
    {
        Assert.True(EsMath.AreClose(double.PositiveInfinity, double.PositiveInfinity));
        Assert.True(EsMath.AreClose(double.NegativeInfinity, double.NegativeInfinity));
        Assert.False(EsMath.AreClose(double.PositiveInfinity, double.NegativeInfinity));
    }

    [Fact]
    internal void AreClose_無限大と有限値は偽()
    {
        Assert.False(EsMath.AreClose(double.PositiveInfinity, 1.0));
    }

    [Fact]
    internal void AreClose_巨大な座標でも相対誤差で判定する()
    {
        // 絶対誤差 1e-9 では一致しない大きさでも、相対誤差の範囲内なら等しいと判定する
        Assert.True(EsMath.AreClose(1e12, 1e12 + 1));
    }

    [Fact]
    internal void AreClose_相対誤差を超える差は偽()
    {
        Assert.False(EsMath.AreClose(1e12, 1e12 + 1e6));
    }

    [Fact]
    internal void AreClose_許容誤差を明示できる()
    {
        Assert.True(EsMath.AreClose(1.0, 1.05, tolerance: 0.1));
        Assert.False(EsMath.AreClose(1.0, 1.05, tolerance: 0.01));
    }
}
