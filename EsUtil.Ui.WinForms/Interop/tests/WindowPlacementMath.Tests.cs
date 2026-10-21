using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary><see cref="WindowPlacementMath"/> のテスト（DIP ⇔ 画面物理ピクセルの換算）。</summary>
/// <remarks>純粋関数のみのため UI 初期化なしで並列検証できる。</remarks>
public sealed class WindowPlacementMathTests
{
    /// <summary>DIP の座標がスケール係数で物理ピクセルへ換算されることを検証する。</summary>
    /// <param name="x">DIP の X 座標。</param>
    /// <param name="y">DIP の Y 座標。</param>
    /// <param name="scale">スケール係数。</param>
    /// <param name="expectedX">期待する X 座標。</param>
    /// <param name="expectedY">期待する Y 座標。</param>
    [Theory]
    [InlineData(100, 200, 1.5, 150, 300)]
    [InlineData(100, 200, 2.0, 200, 400)]
    [InlineData(-100, -51, 1.25, -125, -64)]
    [InlineData(100, 200, 1.0, 100, 200)]
    internal void ToPhysicalPixelPoint_スケールを掛けて四捨五入する(
        double x, double y, double scale, int expectedX, int expectedY)
    {
        var point = WindowPlacementMath.ToPhysicalPixelPoint(new EsPoint(x, y), scale);

        Assert.Equal(expectedX, point.X);
        Assert.Equal(expectedY, point.Y);
    }

    /// <summary>スケールが無効な値の場合は基準値（等倍）として扱うことを検証する。</summary>
    /// <param name="scale">スケール係数。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    internal void ToPhysicalPixelPoint_無効なスケールは等倍として扱う(double scale)
    {
        var point = WindowPlacementMath.ToPhysicalPixelPoint(new EsPoint(10, 20), scale);

        Assert.Equal(10, point.X);
        Assert.Equal(20, point.Y);
    }

    /// <summary>DIP のサイズが物理ピクセルのサイズへ換算され、最小 1 にクランプされることを検証する。</summary>
    [Fact]
    internal void ToPhysicalPixelSize_最小1にクランプする()
    {
        Assert.Equal(new Size(200, 300), WindowPlacementMath.ToPhysicalPixelSize(new EsSize(100, 150), 2.0));
        Assert.Equal(new Size(1, 1), WindowPlacementMath.ToPhysicalPixelSize(new EsSize(0.1, 0.1), 1.0));
        Assert.Equal(new Size(1, 1), WindowPlacementMath.ToPhysicalPixelSize(new EsSize(0, 0), 1.0));
    }

    /// <summary>物理ピクセルの座標が DIP へ戻ることを検証する。</summary>
    [Fact]
    internal void ToDipPoint_往復で値が保たれる()
    {
        var original = new EsPoint(120, -40);
        var physical = WindowPlacementMath.ToPhysicalPixelPoint(original, 1.5);
        var roundTripped = WindowPlacementMath.ToDipPoint(physical, 1.5);

        Assert.Equal(original.X, roundTripped.X);
        Assert.Equal(original.Y, roundTripped.Y);
    }

    /// <summary>物理ピクセルの長さが DIP へ換算されることを検証する。</summary>
    [Fact]
    internal void ToDipLength_スケールで割る()
    {
        Assert.Equal(100.0, WindowPlacementMath.ToDipLength(150, 1.5), 6);
        // スケールが無効な場合は基準値（等倍）で割る
        Assert.Equal(150.0, WindowPlacementMath.ToDipLength(150, 0.0), 6);
    }

    /// <summary>復元に使える矩形かどうかの判定を検証する。</summary>
    [Fact]
    internal void IsUsable_0以下の辺は復元しない()
    {
        Assert.True(WindowPlacementMath.IsUsable(new EsRectInt(0, 0, 100, 100)));
        Assert.False(WindowPlacementMath.IsUsable(new EsRectInt(0, 0, 0, 100)));
        Assert.False(WindowPlacementMath.IsUsable(new EsRectInt(0, 0, 100, 0)));
        Assert.False(WindowPlacementMath.IsUsable(null));
    }
}
