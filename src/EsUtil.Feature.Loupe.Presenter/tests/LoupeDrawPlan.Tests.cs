using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Loupe.Tests;

/// <summary><see cref="LoupePresenter.ResolveDrawPlan"/> が返す描画計画のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 描画計画は「UI が見る唯一の描画値」であり、幾何（コア）と外観・層の順序・ラベル配置を解決済みで持つ。<br/>
/// 画素への反映は MewUI の描画実装テストが担うため、本テストは値（層・矩形・配置）だけを検証する。
/// </remarks>
public class LoupeDrawPlanTests
{
    /// <summary>描画計画の一辺（DIP/物理ピクセル。 スケーリング 1.0）。</summary>
    private const int LoupeSize = 32;

    [Fact]
    internal void ResolveDrawPlan_外観をそのまま保持する()
    {
        var appearance = LoupeAppearance.Default with { ShowBorder = false, BorderThickness = 3 };

        var plan = ResolvePlan(appearance, new EsPoint(16, 16));

        Assert.Equal(appearance, plan.Appearance);
    }

    [Fact]
    internal void ResolveDrawPlan_描画面と切り出し領域を解決する()
    {
        var plan = ResolvePlan(LoupeAppearance.Default, new EsPoint(16, 16));

        Assert.Equal(new EsRect(0, 0, LoupeSize, LoupeSize), plan.ClientPixelRect);
        Assert.Equal(LoupeSize, plan.ClientPixelSize);
        Assert.Equal(4, plan.ZoomLevel);
        // 拡大率 4・一辺 32 → 切り出し一辺 8、左上は中心から 4 引いた位置
        Assert.Equal(new EsPoint(12, 12), plan.CaptureTopLeft);
        Assert.NotNull(plan.MagnifiedImage);
    }

    [Fact]
    internal void ResolveDrawPlan_拡大中心が未設定なら拡大画像と座標ラベルを持たない()
    {
        var plan = ResolvePlan(LoupeAppearance.Default, center: null);

        Assert.Null(plan.CaptureTopLeft);
        Assert.Null(plan.MagnifiedImage);
        Assert.Empty(plan.Labels);
        Assert.False(plan.HasText);
    }

    [Fact]
    internal void ResolveDrawPlan_中心があれば座標ラベルを配置する()
    {
        var plan = ResolvePlan(LoupeAppearance.Default with { ShowCoordLabel = true }, new EsPoint(123, 456));

        var label = Assert.Single(plan.Labels);
        Assert.Equal("(123, 456)", label.Text);
        Assert.True(plan.HasText);
    }

    [Fact]
    internal void ResolveDrawPlan_座標ラベルを非表示にしても配置指示は残る()
    {
        var plan = ResolvePlan(LoupeAppearance.Default with { ShowCoordLabel = false }, new EsPoint(123, 456));

        // 計画は表示可否で指示を消さない（文言・位置は残り、表示可否は指示が持つ）
        var label = Assert.Single(plan.Labels);
        Assert.Equal("(123, 456)", label.Text);
        Assert.False(label.IsVisible);
        Assert.False(plan.HasText);
    }

    [Fact]
    internal void ResolveDrawPlan_ペーストボードは常に先頭に含む()
    {
        var plan = ResolvePlan(LoupeAppearance.Default with { ShowCrosshair = false, ShowBorder = false }, center: null);

        // 中心が無くても層の先頭は必ずペーストボード（表示可否では層を消さない）
        Assert.Equal(LoupeDrawLayer.Pasteboard, plan.Layers[0]);
    }

    [Fact]
    internal void ResolveDrawPlan_層の順序は下地からテキストの順になる()
    {
        var plan = ResolvePlan(LoupeAppearance.Default, new EsPoint(16, 16));

        Assert.Equal(
            [
                LoupeDrawLayer.Pasteboard,
                LoupeDrawLayer.Image,
                LoupeDrawLayer.Crosshair,
                LoupeDrawLayer.Border,
                LoupeDrawLayer.Label,
            ],
            plan.Layers);
    }

    [Fact]
    internal void ResolveDrawPlan_十字線が非表示でも矩形と層は残る()
    {
        var plan = ResolvePlan(LoupeAppearance.Default with { ShowCrosshair = false }, new EsPoint(16, 16));

        // 値は幾何だけで決まるため、表示を止めても計画は矩形と層を保つ（描くかどうかは描画側が決める）
        Assert.Equal(2, plan.CrosshairPixelRects.Count);
        Assert.Contains(LoupeDrawLayer.Crosshair, plan.Layers);
    }

    [Fact]
    internal void ResolveDrawPlan_枠線が非表示でも矩形と層は残る()
    {
        var plan = ResolvePlan(LoupeAppearance.Default with { ShowBorder = false }, new EsPoint(16, 16));

        Assert.Equal(4, plan.BorderPixelRects.Count);
        Assert.Contains(LoupeDrawLayer.Border, plan.Layers);
    }

    [Fact]
    internal void ResolveDrawPlan_表示中の十字線と枠線の矩形を解決する()
    {
        var plan = ResolvePlan(LoupeAppearance.Default with { BorderThickness = 2 }, new EsPoint(16, 16));

        // 十字線は縦横 2 本、枠線は 4 辺
        Assert.Equal(2, plan.CrosshairPixelRects.Count);
        Assert.Equal(4, plan.BorderPixelRects.Count);
    }

    /// <summary>計画がスケーリング係数を持ち、DIP の長さを物理ピクセルへ換算できることを検証する。</summary>
    /// <remarks>
    /// ホストが矩形を差し替えるとき、計画が倍率を持たないと（計画に無い入力を外から参照することになり）<br/>
    /// resolver の契約（同じ入力に同じ出力）が成立しない。 換算の規則は提示層と同じ（切り上げ・最低 1）。
    /// </remarks>
    [Theory]
    [InlineData(1.0, 3.0, 3)]
    [InlineData(1.25, 3.0, 4)]
    [InlineData(2.0, 18.0, 36)]
    internal void ResolveDrawPlan_倍率を持ちDIPを物理ピクセルへ換算できる(double scaling, double dip, int expected)
    {
        var presenter = new LoupePresenter
        {
            Options = LoupeOptions.Default with { RequestedSize = LoupeSize, ZoomLevel = 4 },
            Appearance = LoupeAppearance.Default,
            ClientToPixelScaling = scaling,
            CenterPoint = new EsPoint(16, 16),
        };

        var plan = presenter.ResolveDrawPlan(LoupeSize, static (_, _) => default);

        Assert.Equal(scaling, plan.ClientToPixelScaling, 3);
        Assert.Equal(expected, plan.ScaleDipToPixel(dip));
    }

    /// <summary>テスト用の描画計画を解決する。</summary>
    /// <param name="appearance">外観。</param>
    /// <param name="center">拡大中心。</param>
    /// <returns>描画計画。</returns>
    /// <remarks>実寸測定はダミーを渡す（配置の写像は `LoupePresenter.ResolveLabelPlacements` のテストが担う）。</remarks>
    private static LoupeDrawPlan ResolvePlan(LoupeAppearance appearance, EsPoint? center)
    {
        var presenter = new LoupePresenter
        {
            Options = LoupeOptions.Default with { RequestedSize = LoupeSize, ZoomLevel = 4 },
            Appearance = appearance,
            CenterPoint = center,
        };

        return presenter.ResolveDrawPlan(LoupeSize, static (_, _) => default);
    }
}
