using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Coord;

/// <summary><see cref="EsWindowPlacementMath"/> のテスト（DIP ⇔ 画面物理ピクセルの変換）。</summary>
/// <remarks>
/// 設定ファイルは画面物理ピクセル、MewUI のウィンドウは DIP のため、この変換がずれると<br/>
/// ウィンドウが画面外へ復元される。 純粋関数のみのため UI 初期化なしで並列検証できる。
/// </remarks>
public sealed class EsWindowPlacementMathTests
{
    // ====================================================================
    // DIP → 物理ピクセル
    // ====================================================================

    /// <summary>DPI スケールに応じて座標が物理ピクセルへ変換されることを検証する。</summary>
    /// <param name="x">DIP の X 座標。</param>
    /// <param name="y">DIP の Y 座標。</param>
    /// <param name="scale">DPI スケール係数。</param>
    /// <param name="expectedX">期待する物理 X 座標。</param>
    /// <param name="expectedY">期待する物理 Y 座標。</param>
    [Theory]
    [InlineData(100, 200, 1.0, 100, 200)]
    [InlineData(100, 200, 1.25, 125, 250)]
    [InlineData(100, 200, 1.5, 150, 300)]
    [InlineData(100, 200, 2.0, 200, 400)]
    [InlineData(-40, -20, 1.25, -50, -25)]
    internal void ToPhysicalPixelPoint_スケールを掛けて四捨五入する(
        double x, double y, double scale, int expectedX, int expectedY)
    {
        var result = EsWindowPlacementMath.ToPhysicalPixelPoint(new EsPoint(x, y), scale);

        Assert.Equal(expectedX, result.X);
        Assert.Equal(expectedY, result.Y);
    }

    [Fact]
    internal void ToPhysicalPixelPoint_端数は四捨五入する()
    {
        // 101.6 * 1.25 = 127.0 / 100.4 * 1.25 = 125.5 → 126（MidpointRounding.AwayFromZero ではなく ToEven だが 125.5 は偶数へ）
        var result = EsWindowPlacementMath.ToPhysicalPixelPoint(new EsPoint(101.6, 100.4), 1.25);

        Assert.Equal(127, result.X);
        Assert.Equal(126, result.Y);
    }

    /// <summary>無効なスケール（0 以下・非数）は 1.0 として扱われることを検証する。</summary>
    /// <param name="scale">DPI スケール係数。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    internal void ToPhysicalPixelPoint_無効なスケールは1として扱う(double scale)
    {
        var result = EsWindowPlacementMath.ToPhysicalPixelPoint(new EsPoint(100, 200), scale);

        Assert.Equal(100, result.X);
        Assert.Equal(200, result.Y);
    }

    /// <summary>DPI スケールに応じてサイズが物理ピクセルへ変換されることを検証する。</summary>
    /// <param name="width">DIP の幅。</param>
    /// <param name="height">DIP の高さ。</param>
    /// <param name="scale">DPI スケール係数。</param>
    /// <param name="expectedWidth">期待する物理幅。</param>
    /// <param name="expectedHeight">期待する物理高さ。</param>
    [Theory]
    [InlineData(1200, 800, 1.0, 1200, 800)]
    [InlineData(1200, 800, 1.25, 1500, 1000)]
    [InlineData(1200, 800, 1.5, 1800, 1200)]
    internal void ToPhysicalPixelSize_スケールを掛けて四捨五入する(
        double width, double height, double scale, int expectedWidth, int expectedHeight)
    {
        var result = EsWindowPlacementMath.ToPhysicalPixelSize(new EsSize(width, height), scale);

        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }
    /// <summary>変換後のサイズが 0 にならないことを検証する（0 サイズのウィンドウで復元不能になるのを防ぐ）。</summary>
    /// <param name="width">DIP の幅。</param>
    /// <param name="height">DIP の高さ。</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.1, 0.1)]
    [InlineData(-100, -50)]
    internal void ToPhysicalPixelSize_最小1へクランプする(double width, double height)
    {
        var result = EsWindowPlacementMath.ToPhysicalPixelSize(new EsSize(width, height), 1.0);

        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    // ====================================================================
    // 物理ピクセル → DIP
    // ====================================================================

    /// <summary>物理ピクセルの座標が DIP へ戻ることを検証する。</summary>
    /// <param name="x">物理 X 座標。</param>
    /// <param name="y">物理 Y 座標。</param>
    /// <param name="scale">DPI スケール係数。</param>
    /// <param name="expectedX">期待する DIP X 座標。</param>
    /// <param name="expectedY">期待する DIP Y 座標。</param>
    [Theory]
    [InlineData(100, 200, 1.0, 100, 200)]
    [InlineData(125, 250, 1.25, 100, 200)]
    [InlineData(-50, -25, 1.25, -40, -20)]
    internal void ToDipPoint_スケールで割る(
        int x, int y, double scale, double expectedX, double expectedY)
    {
        var result = EsWindowPlacementMath.ToDipPoint(new EsPointInt(x, y), scale);

        Assert.Equal(expectedX, result.X, 6);
        Assert.Equal(expectedY, result.Y, 6);
    }

    [Fact]
    internal void ToDipPoint_無効なスケールは1として扱う()
    {
        var result = EsWindowPlacementMath.ToDipPoint(new EsPointInt(100, 200), 0);

        Assert.Equal(100, result.X);
        Assert.Equal(200, result.Y);
    }

    /// <summary>DIP → 物理 → DIP の往復で値が保たれることを検証する。</summary>
    /// <param name="scale">DPI スケール係数。</param>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    internal void 往復_DIPから物理を経て元へ戻る(double scale)
    {
        var original = new EsPoint(1234, 567);

        var physical = EsWindowPlacementMath.ToPhysicalPixelPoint(original, scale);
        var restored = EsWindowPlacementMath.ToDipPoint(physical, scale);

        // 物理ピクセルは整数のため、往復では 1 スケール単位（= 1/scale DIP）以内の誤差を許容する
        Assert.Equal(original.X, restored.X, 1.0 / scale);
        Assert.Equal(original.Y, restored.Y, 1.0 / scale);
    }

    // ====================================================================
    // 復元可否の判定
    // ====================================================================

    [Fact]
    internal void IsUsable_幅と高さが正ならtrueを返す()
    {
        Assert.True(EsWindowPlacementMath.IsUsable(new EsRectInt(10, 20, 1200, 800)));
    }

    /// <summary>幅または高さが 0 以下の場合は復元しないことを検証する。</summary>
    [Fact]
    internal void IsUsable_幅か高さが0以下ならfalseを返す()
    {
        Assert.False(EsWindowPlacementMath.IsUsable(new EsRectInt(10, 20, 0, 800)));
        Assert.False(EsWindowPlacementMath.IsUsable(new EsRectInt(10, 20, 1200, 0)));
        Assert.False(EsWindowPlacementMath.IsUsable(new EsRectInt(10, 20, -100, -50)));
    }

    [Fact]
    internal void IsUsable_nullはfalseを返す()
    {
        Assert.False(EsWindowPlacementMath.IsUsable(null));
    }
}
