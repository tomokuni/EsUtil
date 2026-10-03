using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Coord;

/// <summary><see cref="CoordTransformExtensions"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「座標変換ポートが未設定（null）の場合は等価変換として扱う」規則を単一所有するためのヘルパーの検証。<br/>
/// 各コントロール・コアのジオメトリは本規則を自前で書かない（書くとポート未設定時の扱いが UI ごとにずれる）。
/// </remarks>
public class CoordTransformExtensionsTests
{
    /// <summary>ポートが指定されている場合はそのポートを返すことを検証する。</summary>
    [Fact]
    public void OrIdentity_ポート指定時はそのポートを返す()
    {
        var port = CoordTransform.ClientToSource(() => 2.0);

        Assert.Same(port, port.OrIdentity());
    }

    /// <summary>ポート未設定（null）の場合は等価変換を返すことを検証する。</summary>
    [Fact]
    public void OrIdentity_ポート未設定なら等価変換を返す()
    {
        ICoordTransform? port = null;

        Assert.Same(CoordTransform.Identity, port.OrIdentity());
    }

    /// <summary>返された等価変換が座標をそのまま通すことを検証する。</summary>
    [Fact]
    public void OrIdentity_等価変換は座標をそのまま通す()
    {
        ICoordTransform? port = null;

        var point = port.OrIdentity().ToDip(new EsPoint(10, 20));

        Assert.Equal(new EsPoint(10, 20), point);
    }
}
