using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.Marquee.Presenter.Drawing;

namespace EsUtil.Feature.Marquee.Tests;

/// <summary><see cref="MarqueePresenter.ResolveCoordLabelText"/> の単体テスト（カーソル座標ラベルの文言と基準点）。</summary>
/// <remarks>
/// 文言（対象座標の文字列化）と基準点（DIP 座標 + オフセット）の出所が異なることを検証する。<br/>
/// 座標変換はコア（<see cref="MarqueeGeometry"/> の解決）が済ませているため、本層は座標変換ポートを受け取らない。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class MarqueePresenterLabelTextsTests
{
    /// <summary>検証に使う幾何を生成する（マウス位置の 2 表現だけを指定する）。</summary>
    /// <param name="mouseSource">マウス位置（対象座標）。</param>
    /// <param name="mouseDip">マウス位置（DIP。 座標変換後）。</param>
    /// <returns>カーソル座標ラベルの検証に必要な値だけを持つ幾何。</returns>
    private static MarqueeGeometry CreateGeometry(EsPoint mouseSource, EsPoint mouseDip)
        => new(new EsSize(200, 100), null, null, null, null, mouseSource, mouseDip);

    /// <summary>表示しない設定では座標ラベルを返さないことを検証する。</summary>
    [Fact]
    public void ResolveCoordLabelText_表示しない設定ではnullになる()
    {
        var appearance = MarqueeAppearance.Default with { ShowCoordLabel = false };

        var result = MarqueePresenter.ResolveCoordLabelText(
            CreateGeometry(new EsPoint(10, 20), new EsPoint(10, 20)), appearance);

        Assert.Null(result);
    }

    /// <summary>文言が対象座標から作られることを検証する（DIP ではない）。</summary>
    [Fact]
    public void ResolveCoordLabelText_文言は対象座標から作る()
    {
        // 表示スケール 2 のポートを模した幾何（対象 (30,40) は DIP (15,20)）
        var geometry = CreateGeometry(new EsPoint(30, 40), new EsPoint(15, 20));

        var result = MarqueePresenter.ResolveCoordLabelText(geometry, MarqueeAppearance.Default);

        Assert.Equal(new EsPoint(30, 40).Format(), result!.Value.Text);
    }

    /// <summary>基準点が DIP 座標とオフセットから求められることを検証する。</summary>
    [Fact]
    public void ResolveCoordLabelText_基準点はDIPとオフセットから求める()
    {
        var appearance = MarqueeAppearance.Default with { CoordLabelOffset = 8.0 };
        var geometry = CreateGeometry(new EsPoint(30, 40), new EsPoint(15, 20));

        var result = MarqueePresenter.ResolveCoordLabelText(geometry, appearance);

        Assert.Equal(new EsPoint(23, 28), result!.Value.Point);
    }

    /// <summary>クランプ領域が描画領域のサイズになることを検証する。</summary>
    [Fact]
    public void ResolveCoordLabelText_クランプ領域は描画領域のサイズになる()
    {
        var geometry = CreateGeometry(new EsPoint(30, 40), new EsPoint(30, 40));

        var result = MarqueePresenter.ResolveCoordLabelText(geometry, MarqueeAppearance.Default);

        Assert.Equal(new EsSize(200, 100), result!.Value.ClampArea);
    }
}
