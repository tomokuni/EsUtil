using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsThickness"/> の単体テスト（UI 非依存・並列実行）。</summary>
/// <remarks>値型の計算のみのため、UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行する。</remarks>
public class EsThicknessTests
{
    [Fact]
    public void 成分をそのまま保持する()
    {
        var thickness = new EsThickness(1, 2, 3, 4);

        Assert.Equal(1, thickness.Left);
        Assert.Equal(2, thickness.Top);
        Assert.Equal(3, thickness.Right);
        Assert.Equal(4, thickness.Bottom);
    }

    [Fact]
    public void 単一値のコンストラクタは4辺へ同じ値を設定する()
    {
        var thickness = new EsThickness(5);

        Assert.Equal(new EsThickness(5, 5, 5, 5), thickness);
    }

    [Fact]
    public void Zero_は全辺0()
    {
        Assert.Equal(new EsThickness(0, 0, 0, 0), EsThickness.Zero);
    }

    [Fact]
    public void Horizontal_左右の合計を返す()
    {
        Assert.Equal(7, new EsThickness(3, 10, 4, 20).Horizontal);
    }

    [Fact]
    public void Vertical_上下の合計を返す()
    {
        Assert.Equal(30, new EsThickness(3, 10, 4, 20).Vertical);
    }

    [Fact]
    public void 負の厚さもそのまま保持する()
    {
        var thickness = new EsThickness(-1, -2, -3, -4);

        Assert.Equal(-4, thickness.Horizontal);
        Assert.Equal(-6, thickness.Vertical);
    }

    [Fact]
    public void FromTuple_ToTuple_タプルと往復する()
    {
        Assert.Equal(new EsThickness(1, 2, 3, 4), EsThickness.FromTuple((1, 2, 3, 4)));
        Assert.Equal((1, 2, 3, 4), new EsThickness(1, 2, 3, 4).ToTuple());
    }

    [Fact]
    public void Deconstruct_タプルへ分解できる()
    {
        var (left, top, right, bottom) = new EsThickness(1, 2, 3, 4);

        Assert.Equal(1, left);
        Assert.Equal(2, top);
        Assert.Equal(3, right);
        Assert.Equal(4, bottom);
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        Assert.Equal(new EsThickness(1, 2, 3, 4), new EsThickness(1, 2, 3, 4));
        Assert.NotEqual(new EsThickness(1, 2, 3, 4), new EsThickness(4, 3, 2, 1));
    }
}
