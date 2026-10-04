using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsPointInt"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 小数を扱う座標型への変換は本型が単一所有するため、成分が欠落・入れ替わりなく引き継がれることを検証する。
/// </remarks>
public class EsPointIntTests
{
    /// <summary>整数の成分がそのまま小数の座標型へ引き継がれることを検証する。</summary>
    [Fact]
    internal void ToEsPoint_成分をそのまま引き継ぐ()
    {
        var result = new EsPointInt(120, 340).ToEsPoint();

        Assert.Equal(120, result.X);
        Assert.Equal(340, result.Y);
    }

    /// <summary>負の座標（セカンダリモニタの原点）も引き継ぐことを検証する。</summary>
    [Fact]
    internal void ToEsPoint_負の座標も引き継ぐ()
    {
        var result = new EsPointInt(-1920, -200).ToEsPoint();

        Assert.Equal(-1920, result.X);
        Assert.Equal(-200, result.Y);
    }

    /// <summary>原点は原点のまま変換されることを検証する。</summary>
    [Fact]
    internal void ToEsPoint_原点は原点のまま()
    {
        Assert.Equal(default(EsPoint), new EsPointInt(0, 0).ToEsPoint());
    }

    // ====================================================================
    // IsEmpty / Zero
    // ====================================================================

    [Fact]
    internal void Zero_は原点()
    {
        Assert.Equal(new EsPointInt(0, 0), EsPointInt.Zero);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    internal void IsEmpty_XとYがともに0の場合のみ真(int x, int y, bool expected)
    {
        Assert.Equal(expected, new EsPointInt(x, y).IsEmpty);
    }

    // ====================================================================
    // WithX / WithY / Offset
    // ====================================================================

    [Fact]
    internal void WithX_WithY_指定した成分だけを差し替える()
    {
        var point = new EsPointInt(1, 2);

        Assert.Equal(new EsPointInt(9, 2), point.WithX(9));
        Assert.Equal(new EsPointInt(1, 9), point.WithY(9));
    }

    [Fact]
    internal void Offset_指定量だけ平行移動する()
    {
        Assert.Equal(new EsPointInt(11, 22), new EsPointInt(1, 2).Offset(10, 20));
    }

    [Fact]
    internal void 演算子は成分ごとに加減算する()
    {
        Assert.Equal(new EsPointInt(4, 6), new EsPointInt(1, 2) + new EsPointInt(3, 4));
        Assert.Equal(new EsPointInt(-2, -2), new EsPointInt(1, 2) - new EsPointInt(3, 4));
    }

    // ====================================================================
    // FromEsPoint（丸め規則の集約）
    // ====================================================================

    [Theory]
    [InlineData(1.4, 1)]
    [InlineData(1.5, 2)]
    [InlineData(-1.5, -2)]
    internal void FromEsPoint_既定は四捨五入(double value, int expected)
    {
        Assert.Equal(expected, EsPointInt.FromEsPoint(new EsPoint(value, value)).X);
    }

    [Theory]
    [InlineData(1.9, 1)]
    [InlineData(-1.9, -1)]
    internal void FromEsPoint_Truncateは0の方向へ切り捨てる(double value, int expected)
    {
        Assert.Equal(expected, EsPointInt.FromEsPoint(new EsPoint(value, value), EsRounding.Truncate).X);
    }

    [Theory]
    [InlineData(1.1, 2)]
    [InlineData(-1.1, -1)]
    internal void FromEsPoint_Ceilingは正の方向へ切り上げる(double value, int expected)
    {
        Assert.Equal(expected, EsPointInt.FromEsPoint(new EsPoint(value, value), EsRounding.Ceiling).X);
    }

    [Fact]
    internal void FromEsPoint_XとYを個別に丸める()
    {
        Assert.Equal(new EsPointInt(2, 3), EsPointInt.FromEsPoint(new EsPoint(1.5, 2.5)));
    }

    // ====================================================================
    // タプル
    // ====================================================================

    [Fact]
    internal void FromTuple_ToTuple_タプルと往復する()
    {
        Assert.Equal(new EsPointInt(3, 4), EsPointInt.FromTuple((3, 4)));
        Assert.Equal((3, 4), new EsPointInt(3, 4).ToTuple());
    }

    [Fact]
    internal void Deconstruct_タプルへ分解できる()
    {
        var (x, y) = new EsPointInt(3, 4);

        Assert.Equal(3, x);
        Assert.Equal(4, y);
    }
}
