using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsSize"/> の領域判定とクランプのテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「領域が未確定ならクランプしない」の判定と、軸ごとのクランプを固定する。<br/>
/// レイアウト前の領域（幅・高さが 0）で値を勝手に動かさないことが要件である。
/// </remarks>
public class EsSizeTests
{
    // ====================================================================
    // HasArea（領域の確定判定）
    // ====================================================================

    [Theory]
    [InlineData(200, 100, true)]
    [InlineData(1, 1, true)]
    [InlineData(0, 100, false)]
    [InlineData(200, 0, false)]
    [InlineData(-1, -1, false)]
    internal void HasArea_幅と高さがともに正の場合のみ確定とする(double width, double height, bool expected)
        => Assert.Equal(expected, new EsSize(width, height).HasArea);

    // ====================================================================
    // ClampToArea（サイズのクランプ）
    // ====================================================================

    [Theory]
    [InlineData(160, 200, 150, 150)]
    [InlineData(160, 100, 100, 100)]
    [InlineData(100, 200, 150, 100)]
    internal void ClampToArea_領域の最小辺を上限とする(double size, double areaWidth, double areaHeight, double expected)
        => Assert.Equal(expected, new EsSize(areaWidth, areaHeight).ClampToArea(size));

    [Theory]
    [InlineData(0, 100)]
    [InlineData(0, 0)]
    [InlineData(200, 0)]
    internal void ClampToArea_領域が未確定ならクランプしない(double areaWidth, double areaHeight)
        => Assert.Equal(160, new EsSize(areaWidth, areaHeight).ClampToArea(160));

    // ====================================================================
    // ClampOffset（位置のクランプ）
    // ====================================================================

    [Theory]
    [InlineData(100, 40)]
    [InlineData(-50, 0)]
    [InlineData(0, 0)]
    internal void ClampOffset_領域内へ収まる範囲にクランプする(double offset, double expected)
        => Assert.Equal(expected, new EsSize(200, 100).ClampOffset(offset, size: 160, EsAxis.X));

    [Fact]
    internal void ClampOffset_軸を指定して幅と高さを使い分ける()
    {
        var area = new EsSize(200, 100);

        // 幅 200 では上限 40、高さ 100 では上限 0（対象 160 が収まらない）
        Assert.Equal(40, area.ClampOffset(100, size: 160, EsAxis.X));
        Assert.Equal(0, area.ClampOffset(100, size: 160, EsAxis.Y));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(0, 0)]
    [InlineData(-20, 100)]
    internal void ClampOffset_対象の軸の長さが0以下ならクランプしない(double areaWidth, double areaHeight)
        => Assert.Equal(100, new EsSize(areaWidth, areaHeight).ClampOffset(100, size: 160, EsAxis.X));

    [Fact]
    internal void ClampOffset_軸ごとに確定を判定する()
    {
        // 幅だけが確定している領域では、高さのクランプは行わない（軸ごとに判定する）
        var area = new EsSize(200, 0);

        Assert.Equal(40, area.ClampOffset(100, size: 160, EsAxis.X));
        Assert.Equal(100, area.ClampOffset(100, size: 160, EsAxis.Y));
    }

    [Fact]
    internal void ClampOffset_対象が領域より大きい場合は0へ寄せる()
        => Assert.Equal(0, new EsSize(100, 100).ClampOffset(50, size: 200, EsAxis.X));

    [Fact]
    internal void ClampOffset_未定義の軸は例外()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new EsSize(200, 100).ClampOffset(10, size: 5, (EsAxis)99));

    // ====================================================================
    // Zero / Infinity / AspectRatio
    // ====================================================================

    [Fact]
    internal void Zero_は幅と高さとも0()
    {
        Assert.Equal(new EsSize(0, 0), EsSize.Zero);
    }

    [Fact]
    internal void Infinity_は幅と高さとも正の無限大()
    {
        Assert.Equal(double.PositiveInfinity, EsSize.Infinity.Width);
        Assert.Equal(double.PositiveInfinity, EsSize.Infinity.Height);
    }

    [Fact]
    internal void AspectRatio_幅を高さで割った値を返す()
    {
        Assert.Equal(2, new EsSize(200, 100).AspectRatio);
    }

    // ====================================================================
    // WithWidth / WithHeight / Constrain
    // ====================================================================

    [Fact]
    internal void WithWidth_WithHeight_指定した成分だけを差し替える()
    {
        var size = new EsSize(1, 2);

        Assert.Equal(new EsSize(9, 2), size.WithWidth(9));
        Assert.Equal(new EsSize(1, 9), size.WithHeight(9));
    }

    [Fact]
    internal void Constrain_成分ごとに小さい方を採る()
    {
        Assert.Equal(new EsSize(3, 4), new EsSize(10, 4).Constrain(new EsSize(3, 4)));
    }

    // ====================================================================
    // Inflate / Deflate（EsThickness）
    // ====================================================================

    [Fact]
    internal void Inflate_左右と上下の合計を加える()
    {
        Assert.Equal(new EsSize(110, 120), new EsSize(100, 100).Inflate(new EsThickness(3, 5, 7, 15)));
    }

    [Fact]
    internal void Deflate_左右と上下の合計を減らす()
    {
        Assert.Equal(new EsSize(90, 80), new EsSize(100, 100).Deflate(new EsThickness(3, 5, 7, 15)));
    }

    [Fact]
    internal void Deflate_負のサイズにはならない()
    {
        Assert.Equal(new EsSize(0, 0), new EsSize(10, 10).Deflate(new EsThickness(20, 20, 20, 20)));
    }

    // ====================================================================
    // 演算子
    // ====================================================================

    [Fact]
    internal void 加減算は成分ごとに行う()
    {
        Assert.Equal(new EsSize(4, 6), new EsSize(1, 2) + new EsSize(3, 4));
        Assert.Equal(new EsSize(-2, -2), new EsSize(1, 2) - new EsSize(3, 4));
    }

    [Fact]
    internal void 係数倍と除算は成分ごとに行う()
    {
        Assert.Equal(new EsSize(2, 4), new EsSize(1, 2) * 2);
        Assert.Equal(new EsSize(4, 8), new EsSize(4, 8) / 1);
    }

    [Fact]
    internal void ベクターとの乗算は成分ごとに行う()
    {
        Assert.Equal(new EsSize(2, 12), new EsSize(1, 4) * new EsVector(2, 3));
    }

    [Fact]
    internal void サイズ同士の除算は比のベクターを返す()
    {
        Assert.Equal(new EsVector(2, 4), new EsSize(4, 8) / new EsSize(2, 2));
    }

    // ====================================================================
    // NearlyEquals / タプル
    // ====================================================================

    [Fact]
    internal void NearlyEquals_許容誤差内なら真()
    {
        Assert.True(new EsSize(100, 100).NearlyEquals(new EsSize(100 + 1e-12, 100)));
        Assert.False(new EsSize(100, 100).NearlyEquals(new EsSize(100.001, 100)));
    }

    [Fact]
    internal void FromTuple_ToTuple_タプルと往復する()
    {
        Assert.Equal(new EsSize(3, 4), EsSize.FromTuple((3, 4)));
        Assert.Equal((3, 4), new EsSize(3, 4).ToTuple());
    }

    [Fact]
    internal void Deconstruct_タプルへ分解できる()
    {
        var (width, height) = new EsSize(3, 4);

        Assert.Equal(3, width);
        Assert.Equal(4, height);
    }
}
