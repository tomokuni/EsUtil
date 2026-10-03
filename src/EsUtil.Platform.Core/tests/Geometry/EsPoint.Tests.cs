using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsPoint"/> の表示整形テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 座標ラベルの文言のみを検証するため、UI フレームワークのヘッドレス初期化に依存せず<br/>
/// <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsPointTests
{
    // ====================================================================
    // Format（表示用の整形）
    // ====================================================================

    [Fact]
    public void Format_既定は整数へ丸める()
    {
        // 小数点以下は四捨五入される（既定 0 桁）
        Assert.Equal("(123, 457)", new EsPoint(123.4, 456.7).Format());
    }

    [Fact]
    public void Format_桁数を指定すると指定桁で丸める()
    {
        Assert.Equal("(123.40, 456.70)", new EsPoint(123.4, 456.7).Format(2));
    }

    [Fact]
    public void Format_ゼロは括弧付きの0と表示する()
    {
        Assert.Equal("(0, 0)", new EsPoint(0, 0).Format());
    }

    [Fact]
    public void Format_負の座標も括弧付きで整形する()
    {
        Assert.Equal("(-5, -10)", new EsPoint(-5, -10).Format());
    }

    [Fact]
    public void Format_0桁の指定は既定と同じ結果になる()
    {
        var point = new EsPoint(12.6, -3.4);

        Assert.Equal(point.Format(), point.Format(0));
    }

    [Fact]
    public void Format_最大桁数を指定できる()
    {
        var text = new EsPoint(1.5, 2.5).Format(15);

        Assert.StartsWith("(1.5", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(16)]
    [InlineData(int.MaxValue)]
    public void Format_表示できない桁数は例外(int decimals)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new EsPoint(1, 2).Format(decimals));

    // ====================================================================
    // IsEmpty / Zero
    // ====================================================================

    [Fact]
    public void Zero_は原点()
    {
        Assert.Equal(new EsPoint(0, 0), EsPoint.Zero);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(-0.0, 0, true)]
    public void IsEmpty_XとYがともに0の場合のみ真(double x, double y, bool expected)
    {
        Assert.Equal(expected, new EsPoint(x, y).IsEmpty);
    }

    // ====================================================================
    // WithX / WithY / Offset
    // ====================================================================

    [Fact]
    public void WithX_WithY_指定した成分だけを差し替える()
    {
        var point = new EsPoint(1, 2);

        Assert.Equal(new EsPoint(9, 2), point.WithX(9));
        Assert.Equal(new EsPoint(1, 9), point.WithY(9));
    }

    [Fact]
    public void Offset_指定量だけ平行移動する()
    {
        Assert.Equal(new EsPoint(11, 22), new EsPoint(1, 2).Offset(10, 20));
    }

    // ====================================================================
    // DistanceTo
    // ====================================================================

    [Fact]
    public void DistanceTo_3と4の座標は5になる()
    {
        Assert.Equal(5, new EsPoint(0, 0).DistanceTo(new EsPoint(3, 4)));
    }

    [Fact]
    public void DistanceTo_自分自身とは0になる()
    {
        var point = new EsPoint(7, 8);

        Assert.Equal(0, point.DistanceTo(point));
    }

    // ====================================================================
    // NearlyEquals
    // ====================================================================

    [Fact]
    public void NearlyEquals_許容誤差内なら真()
    {
        Assert.True(new EsPoint(1, 2).NearlyEquals(new EsPoint(1 + 1e-12, 2)));
    }

    [Fact]
    public void NearlyEquals_差が大きければ偽()
    {
        Assert.False(new EsPoint(1, 2).NearlyEquals(new EsPoint(1.001, 2)));
    }

    // ====================================================================
    // 演算子
    // ====================================================================

    [Fact]
    public void Add_座標同士は成分ごとに加算する()
    {
        Assert.Equal(new EsPoint(4, 6), new EsPoint(1, 2) + new EsPoint(3, 4));
    }

    [Fact]
    public void Add_ベクターは平行移動になる()
    {
        Assert.Equal(new EsPoint(11, 22), new EsPoint(1, 2) + new EsVector(10, 20));
    }

    [Fact]
    public void Subtract_座標同士はベクターを返す()
    {
        Assert.Equal(new EsVector(2, 2), new EsPoint(3, 4) - new EsPoint(1, 2));
    }

    [Fact]
    public void Subtract_ベクターは逆方向へ平行移動する()
    {
        Assert.Equal(new EsPoint(-9, -18), new EsPoint(1, 2) - new EsVector(10, 20));
    }

    [Fact]
    public void UnaryNegate_成分の符号を反転する()
    {
        Assert.Equal(new EsPoint(-1, -2), -new EsPoint(1, 2));
    }

    [Fact]
    public void Multiply_係数倍を両側で許容する()
    {
        Assert.Equal(new EsPoint(2, 4), new EsPoint(1, 2) * 2);
        Assert.Equal(new EsPoint(2, 4), 2 * new EsPoint(1, 2));
    }

    [Fact]
    public void Divide_成分ごとに除算する()
    {
        Assert.Equal(new EsPoint(1, 2), new EsPoint(4, 8) / 4);
    }

    // ====================================================================
    // 型変換・タプル
    // ====================================================================

    [Fact]
    public void ToEsVector_成分をそのまま引き継ぐ()
    {
        Assert.Equal(new EsVector(3, 4), new EsPoint(3, 4).ToEsVector());
    }

    [Fact]
    public void FromTuple_ToTuple_タプルと往復する()
    {
        Assert.Equal(new EsPoint(3, 4), EsPoint.FromTuple((3, 4)));
        Assert.Equal((3, 4), new EsPoint(3, 4).ToTuple());
    }

    [Fact]
    public void Deconstruct_タプルへ分解できる()
    {
        var (x, y) = new EsPoint(3, 4);

        Assert.Equal(3, x);
        Assert.Equal(4, y);
    }
}
