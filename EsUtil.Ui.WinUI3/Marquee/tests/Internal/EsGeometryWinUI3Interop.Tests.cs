using EsUtil.Ui.WinUI3.Marquee.Internal;
using Windows.Foundation;
using Xunit;

namespace EsUtil.Ui.WinUI3.Marquee.Tests.Internal;

/// <summary><see cref="EsGeometryWinUI3Interop"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ポインター イベントの座標（WinUI の <see cref="Point"/>）とコアの座標型の変換は入力の境界で必ず通るため、<br/>
/// 「成分がそのまま写ること」「往復で値が変わらないこと」を検証する。
/// </remarks>
public class EsGeometryWinUI3InteropTests
{
    /// <summary>コアの座標が WinUI の座標型へ写ることを検証する。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(12.5, -3.25)]
    [InlineData(-100.0, 250.75)]
    internal void ToWinUIPoint_成分をそのまま写す(double x, double y)
    {
        var converted = new EsPoint(x, y).ToWinUIPoint();

        Assert.Equal(x, converted.X);
        Assert.Equal(y, converted.Y);
    }

    /// <summary>WinUI の座標がコアの座標へ写ることを検証する。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(12.5, -3.25)]
    internal void ToEsPoint_成分をそのまま写す(double x, double y)
    {
        var converted = new Point(x, y).ToEsPoint();

        Assert.Equal(x, converted.X);
        Assert.Equal(y, converted.Y);
    }

    /// <summary>往復変換で値が変わらないことを検証する（WinUI の座標型は単精度のため整数値で確かめる）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(120.0, 240.0)]
    [InlineData(-8.0, 16.0)]
    internal void 変換_往復で値が変わらない(double x, double y)
        => Assert.Equal(new EsPoint(x, y), new EsPoint(x, y).ToWinUIPoint().ToEsPoint());
}
