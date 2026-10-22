using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsRectEdges"/> の単体テスト（UI 非依存・並列実行）。</summary>
/// <remarks>値型の計算のみのため、UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行する。</remarks>
public class EsRectEdgesTests
{
    /// <summary>検証用の 4 辺を生成する。</summary>
    private static EsRectEdges CreateEdges()
        => new(new EsRect(1, 2, 3, 4), new EsRect(5, 6, 7, 8), new EsRect(9, 10, 11, 12), new EsRect(13, 14, 15, 16));

    [Fact]
    internal void Count_は4()
    {
        Assert.Equal(4, EsRectEdges.Count);
    }

    [Fact]
    internal void プロパティは指定した4辺をそのまま返す()
    {
        var edges = CreateEdges();

        Assert.Equal(new EsRect(1, 2, 3, 4), edges.Top);
        Assert.Equal(new EsRect(5, 6, 7, 8), edges.Bottom);
        Assert.Equal(new EsRect(9, 10, 11, 12), edges.Left);
        Assert.Equal(new EsRect(13, 14, 15, 16), edges.Right);
    }

    [Theory]
    [InlineData(0, 1, 2, 3, 4)]
    [InlineData(1, 5, 6, 7, 8)]
    [InlineData(2, 9, 10, 11, 12)]
    [InlineData(3, 13, 14, 15, 16)]
    internal void インデクサは上_下_左_右の順で返す(int index, double x, double y, double width, double height)
    {
        Assert.Equal(new EsRect(x, y, width, height), CreateEdges()[index]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    internal void インデクサは範囲外で例外を投げる(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEdges()[index]);
    }

    [Fact]
    internal void ToArray_上_下_左_右の順の配列を返す()
    {
        var array = CreateEdges().ToArray();

        Assert.Equal(4, array.Length);
        Assert.Equal(new EsRect(1, 2, 3, 4), array[0]);
        Assert.Equal(new EsRect(5, 6, 7, 8), array[1]);
        Assert.Equal(new EsRect(9, 10, 11, 12), array[2]);
        Assert.Equal(new EsRect(13, 14, 15, 16), array[3]);
    }

    [Fact]
    internal void ToArray_呼び出しごとに新しい配列を返す()
    {
        var edges = CreateEdges();

        Assert.NotSame(edges.ToArray(), edges.ToArray());
    }
}
