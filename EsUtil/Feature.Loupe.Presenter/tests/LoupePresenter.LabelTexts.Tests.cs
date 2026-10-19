using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Loupe.Tests;

/// <summary><see cref="LoupePresenter.ResolveCoordLabelText"/> の単体テスト（中心座標ラベルの文言と基準点）。</summary>
/// <remarks>
/// 文言（座標の書式）と基準点（外観のオフセット）の導出を検証する。<br/>
/// 実寸の反映と配置指示への写像は <see cref="LoupePresenterLabelPlanTests"/> が検証する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class LoupePresenterLabelTextsTests
{
    /// <summary>検証に使う描画の幾何を生成する。</summary>
    /// <param name="center">拡大中心（未設定は null）。</param>
    /// <returns>描画の幾何。</returns>
    private static LoupeGeometry CreateGeometry(EsPoint? center)
        => new(center, ClientSize: 160.0, ClientPixelSize: 160, ClientToPixelScaling: 1.0, ZoomLevel: 4);

    /// <summary>拡大中心が未設定なら配置しないことを検証する。</summary>
    [Fact]
    internal void 中心未設定なら配置しない()
    {
        var placement = LoupePresenter.ResolveCoordLabelText(CreateGeometry(null), LoupeAppearance.Default);

        Assert.Null(placement);
    }

    /// <summary>表示の設定が無効でも文言（値）を解決することを検証する。</summary>
    /// <remarks>
    /// 計画は表示可否で値を消さない（表示可否は配置指示が持ち、描くかどうかは描画側が決める）。<br/>
    /// したがって本メソッドの門は「拡大中心の有無（幾何）」だけである。
    /// </remarks>
    [Fact]
    internal void 表示の設定が無効でも文言を解決する()
    {
        var appearance = LoupeAppearance.Default with { ShowCoordLabel = false };

        var placement = LoupePresenter.ResolveCoordLabelText(CreateGeometry(new EsPoint(10, 20)), appearance);

        Assert.NotNull(placement);
        Assert.Equal("(10, 20)", placement.Value.Text);
    }

    /// <summary>文言が拡大中心の座標を整形した文字列になることを検証する。</summary>
    [Fact]
    internal void 文言は拡大中心の座標を整形する()
    {
        var placement = LoupePresenter.ResolveCoordLabelText(CreateGeometry(new EsPoint(10, 20)), LoupeAppearance.Default);

        Assert.NotNull(placement);
        Assert.Equal("(10, 20)", placement.Value.Text);
    }

    /// <summary>基準点が外観のオフセットだけ内側になることを検証する。</summary>
    /// <remarks>余白は体裁（描画にだけ必要な設定）であり、外観が単一所有する。</remarks>
    [Theory]
    [InlineData(0.0)]
    [InlineData(2.0)]
    [InlineData(6.5)]
    internal void 基準点は外観のオフセットだけ内側になる(double offset)
    {
        var appearance = LoupeAppearance.Default with { CoordLabelOffset = offset };

        var placement = LoupePresenter.ResolveCoordLabelText(CreateGeometry(new EsPoint(10, 20)), appearance);

        Assert.NotNull(placement);
        Assert.Equal(new EsPoint(offset, offset), placement.Value.Point);
    }

    /// <summary>外観の既定のオフセットを使うことを検証する。</summary>
    [Fact]
    internal void 基準点は既定では外観の既定オフセットを使う()
    {
        var placement = LoupePresenter.ResolveCoordLabelText(CreateGeometry(new EsPoint(10, 20)), LoupeAppearance.Default);

        Assert.NotNull(placement);
        Assert.Equal(new EsPoint(LoupeAppearance.CoordLabelOffsetDefault, LoupeAppearance.CoordLabelOffsetDefault), placement.Value.Point);
    }
}
