using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Coord;

/// <summary><see cref="CoordTransform"/> のファクトリ（<see cref="CoordTransform.ClientToSource"/>）と、座標の純粋計算（原点基準の等方スケーリング）の単体テスト。</summary>
/// <remarks>
/// コア幾何型（<see cref="EsPoint"/> / <see cref="EsSize"/>）のみを使用するため、<br/>
/// UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行できる。
/// </remarks>
public class CoordTransformTests
{
    // ====================================================================
    // 倍率の向き（対象座標 / ローカル DIP）
    // ====================================================================

    [Fact]
    internal void ClientToSource_倍率2はDIPを2倍して対象にする()
    {
        var port = CoordTransform.ClientToSource(() => 2.0);

        // 対象 = ローカル DIP × 倍率（125% DPI なら対象は物理ピクセルで DIP の 1.25 倍）
        Assert.Equal(new EsPoint(200, 300), port.ToSource(new EsPoint(100, 150)));
    }

    [Fact]
    internal void ClientToSource_対象を倍率で除算してDIPにする()
    {
        var port = CoordTransform.ClientToSource(() => 2.0);

        Assert.Equal(new EsPoint(100, 150), port.ToDip(new EsPoint(200, 300)));
    }

    [Fact]
    internal void ClientToSource_往復で元の座標に戻る()
    {
        var port = CoordTransform.ClientToSource(() => 1.5);
        var p = new EsPoint(123.4, 456.7);

        var roundTrip = port.ToDip(port.ToSource(p));

        Assert.Equal(p.X, roundTrip.X, 6);
        Assert.Equal(p.Y, roundTrip.Y, 6);
    }

    [Fact]
    internal void ClientToSource_プロバイダは呼び出し毎に評価される()
    {
        var factor = 1.0;
        var port = CoordTransform.ClientToSource(() => factor);
        Assert.Equal(new EsPoint(100, 100), port.ToSource(new EsPoint(100, 100)));

        factor = 2.0;
        Assert.Equal(new EsPoint(200, 200), port.ToSource(new EsPoint(100, 100)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    internal void ClientToSource_倍率が0以下なら等倍として扱う(double sourcePerDip)
    {
        var port = CoordTransform.ClientToSource(() => sourcePerDip);

        Assert.Equal(new EsPoint(10, 20), port.ToSource(new EsPoint(10, 20)));
        Assert.Equal(new EsPoint(10, 20), port.ToDip(new EsPoint(10, 20)));
    }

    [Fact]
    internal void ClientToSource_境界を省略すると無制限になる()
    {
        var port = CoordTransform.ClientToSource(() => 2.0);

        Assert.Null(port.SourceArea);
    }

    [Fact]
    internal void ClientToSource_境界を指定すると対象の境界を返す()
    {
        var port = CoordTransform.ClientToSource(() => 2.0, () => new EsSize(1920, 1080));

        Assert.Equal(new EsSize(1920, 1080), port.SourceArea);
    }

    [Fact]
    internal void ClientToSource_境界は呼び出し毎に評価される()
    {
        var bounds = new EsSize(100, 100);
        var port = CoordTransform.ClientToSource(() => 1.0, () => bounds);

        Assert.Equal(new EsSize(100, 100), port.SourceArea);

        bounds = new EsSize(200, 200);
        Assert.Equal(new EsSize(200, 200), port.SourceArea);
    }

    // ====================================================================
    // 座標の純粋計算（原点基準の等方スケーリング）
    // ====================================================================

    [Fact]
    internal void MultiplyPoint_倍率を乗算する()
    {
        var (x, y) = CoordTransform.MultiplyPoint(100, 150, 2.0);

        Assert.Equal(200, x);
        Assert.Equal(300, y);
    }

    [Fact]
    internal void DividePoint_倍率で除算する()
    {
        var (x, y) = CoordTransform.DividePoint(200, 300, 2.0);

        Assert.Equal(100, x);
        Assert.Equal(150, y);
    }
}
