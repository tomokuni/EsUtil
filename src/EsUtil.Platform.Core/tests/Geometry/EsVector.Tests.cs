using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsVector"/> の計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ベクターの演算はパン・ズームの各所で使われるため、長さ・正規化・内積・外積の結果を固定する。<br/>
/// UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsVectorTests
{
    // ====================================================================
    // 定数
    // ====================================================================

    [Fact]
    internal void Zero_One_UnitX_UnitY_は既定のベクター()
    {
        Assert.Equal(new EsVector(0, 0), EsVector.Zero);
        Assert.Equal(new EsVector(1, 1), EsVector.One);
        Assert.Equal(new EsVector(1, 0), EsVector.UnitX);
        Assert.Equal(new EsVector(0, 1), EsVector.UnitY);
    }

    // ====================================================================
    // Length / LengthSquared
    // ====================================================================

    [Fact]
    internal void Length_3と4のベクターは5になる()
    {
        Assert.Equal(5, new EsVector(3, 4).Length);
    }

    [Fact]
    internal void LengthSquared_平方根を取らない値を返す()
    {
        Assert.Equal(25, new EsVector(3, 4).LengthSquared);
    }

    [Fact]
    internal void LengthSquared_巨大な成分は無限大になるが非数にはならない()
    {
        // 600 乗を超える積は double の範囲を外れるため無限大になるが、非数（比較不能）にはしない
        Assert.True(double.IsPositiveInfinity(new EsVector(1e200, 1e200).Length));
    }

    // ====================================================================
    // Normalize
    // ====================================================================

    [Fact]
    internal void Normalize_長さ1の同じ向きのベクターを返す()
    {
        var normalized = new EsVector(3, 4).Normalize();

        Assert.Equal(0.6, normalized.X, 10);
        Assert.Equal(0.8, normalized.Y, 10);
        Assert.Equal(1, normalized.Length, 10);
    }

    [Fact]
    internal void Normalize_ゼロベクターはゼロベクターを返す()
    {
        Assert.Equal(EsVector.Zero, EsVector.Zero.Normalize());
    }

    [Fact]
    internal void Normalize_負のベクターも非数にならない()
    {
        var normalized = new EsVector(-3, 0).Normalize();

        Assert.Equal(new EsVector(-1, 0), normalized);
    }

    // ====================================================================
    // Dot / Cross
    // ====================================================================

    [Theory]
    [InlineData(1, 0, 0, 1, 0)]
    [InlineData(1, 2, 3, 4, 11)]
    [InlineData(0, 0, 5, 5, 0)]
    [InlineData(-1, -2, 3, 4, -11)]
    internal void Dot_内積を返す(double ax, double ay, double bx, double by, double expected)
    {
        Assert.Equal(expected, EsVector.Dot(new EsVector(ax, ay), new EsVector(bx, by)));
    }

    [Theory]
    [InlineData(1, 0, 0, 1, 1)]
    [InlineData(1, 0, 0, -1, -1)]
    [InlineData(0, 0, 5, 5, 0)]
    internal void Cross_外積を返す(double ax, double ay, double bx, double by, double expected)
    {
        Assert.Equal(expected, EsVector.Cross(new EsVector(ax, ay), new EsVector(bx, by)));
    }

    // ====================================================================
    // Min / Max / Abs
    // ====================================================================

    [Fact]
    internal void Min_Max_成分ごとの最小最大を返す()
    {
        Assert.Equal(new EsVector(1, 2), EsVector.Min(new EsVector(1, 5), new EsVector(3, 2)));
        Assert.Equal(new EsVector(3, 5), EsVector.Max(new EsVector(1, 5), new EsVector(3, 2)));
    }

    [Fact]
    internal void Abs_成分の絶対値を返す()
    {
        Assert.Equal(new EsVector(3, 4), new EsVector(-3, -4).Abs());
    }

    // ====================================================================
    // WithX / WithY
    // ====================================================================

    [Fact]
    internal void WithX_WithY_指定した成分だけを差し替える()
    {
        var vector = new EsVector(1, 2);

        Assert.Equal(new EsVector(9, 2), vector.WithX(9));
        Assert.Equal(new EsVector(1, 9), vector.WithY(9));
    }

    // ====================================================================
    // 演算子
    // ====================================================================

    [Fact]
    internal void Add_成分ごとに加算する()
    {
        Assert.Equal(new EsVector(4, 6), new EsVector(1, 2) + new EsVector(3, 4));
    }

    [Fact]
    internal void Subtract_成分ごとに減算する()
    {
        Assert.Equal(new EsVector(-2, -2), new EsVector(1, 2) - new EsVector(3, 4));
    }

    [Fact]
    internal void UnaryNegate_成分の符号を反転する()
    {
        Assert.Equal(new EsVector(-1, -2), -new EsVector(1, 2));
    }

    [Fact]
    internal void Multiply_係数倍を両側で許容する()
    {
        Assert.Equal(new EsVector(2, 4), new EsVector(1, 2) * 2);
        Assert.Equal(new EsVector(2, 4), 2 * new EsVector(1, 2));
    }

    [Fact]
    internal void Divide_成分ごとに除算する()
    {
        Assert.Equal(new EsVector(1, 2), new EsVector(4, 8) / 4);
    }

    // ====================================================================
    // 型変換・タプル
    // ====================================================================

    [Fact]
    internal void ToEsPoint_座標へ変換する()
    {
        Assert.Equal(new EsPoint(3, 4), new EsVector(3, 4).ToEsPoint());
    }

    [Fact]
    internal void FromEsPoint_座標から生成する()
    {
        Assert.Equal(new EsVector(3, 4), EsVector.FromEsPoint(new EsPoint(3, 4)));
    }

    [Fact]
    internal void FromTuple_ToTuple_タプルと往復する()
    {
        Assert.Equal(new EsVector(3, 4), EsVector.FromTuple((3, 4)));
        Assert.Equal((3, 4), new EsVector(3, 4).ToTuple());
    }

    [Fact]
    internal void Deconstruct_タプルへ分解できる()
    {
        var (x, y) = new EsVector(3, 4);

        Assert.Equal(3, x);
        Assert.Equal(4, y);
    }

    [Fact]
    internal void 等価性は値で判定される()
    {
        Assert.Equal(new EsVector(3, 4), new EsVector(3, 4));
        Assert.NotEqual(new EsVector(3, 4), new EsVector(4, 3));
    }
}
