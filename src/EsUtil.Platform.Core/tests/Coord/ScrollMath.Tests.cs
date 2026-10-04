using EsUtil.Platform.Core.Coord;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Coord;

/// <summary>
/// <see cref="ScrollMath"/>（スクロールバーの幾何計算・純粋関数）の回帰テスト。
/// </summary>
/// <remarks>
/// スクロールバーの範囲・ビューポートサイズの計算を検証する。<br/>
/// <c>double</c> のみに依存し UI フレームワーク非依存のため <c>[Fact]</c> で並列実行する。<br/>
/// バグ修正（スクロールバーが表示されない・範囲が正しくない）の回帰防止を目的とする。<br/>
/// スクロールバーの計算で共用する。
/// </remarks>
public class ScrollMathTests
{
    // ====================================================================
    // MaxOffset（スクロール範囲）
    // ====================================================================

    [Fact]
    internal void MaxOffset_コンテンツがビューポートより大きい場合は差分を返す()
    {
        // コンテンツ 1000、ビューポート 400、scale 1 → 1000 - 400 = 600
        var result = ScrollMath.MaxOffset(1000, 400, 1.0);
        Assert.Equal(600.0, result);
    }

    [Fact]
    internal void MaxOffset_ズーム時はビューポートをスケールで除する()
    {
        // コンテンツ 1000、ビューポート 400、scale 2 → 1000 - 400/2 = 800
        var result = ScrollMath.MaxOffset(1000, 400, 2.0);
        Assert.Equal(800.0, result);
    }

    [Fact]
    internal void MaxOffset_コンテンツがビューポート以下の場合は0を返す()
    {
        // コンテンツ 300、ビューポート 400 → 0（スクロール不要）
        var result = ScrollMath.MaxOffset(300, 400, 1.0);
        Assert.Equal(0.0, result);
    }

    [Fact]
    internal void MaxOffset_スケールが0でもゼロ除算しない()
    {
        // scale 0 → 1 として扱う
        var result = ScrollMath.MaxOffset(1000, 400, 0.0);
        Assert.Equal(600.0, result);
    }

    // ====================================================================
    // ViewportSize（コンテンツ座標での表示範囲）
    // ====================================================================

    [Fact]
    internal void ViewportSize_ビューポートをスケールで除する()
    {
        // ビューポート 400、scale 2 → 200
        var result = ScrollMath.ViewportSize(400, 2.0);
        Assert.Equal(200.0, result);
    }

    [Fact]
    internal void ViewportSize_スケールが0でもゼロ除算しない()
    {
        var result = ScrollMath.ViewportSize(400, 0.0);
        Assert.Equal(400.0, result);
    }
}
