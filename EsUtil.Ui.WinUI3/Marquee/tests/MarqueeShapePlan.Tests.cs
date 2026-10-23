using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinUI3.Interop.Drawing;
using EsUtil.Ui.WinUI3.Marquee.Internal;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Marquee.Tests;

/// <summary><see cref="MarqueeShapePlan"/> のテスト（描画計画から矩形の列への変換）。</summary>
/// <remarks>
/// 描画計画は幾何・色・寸法を解決済みであるため、本テストは「計画の値がそのとおり矩形になるか」と<br/>
/// 「表示可否（<c>Show*</c>）と層の順序で置くかどうかが決まるか」を検証する。<br/>
/// ウィンドウを必要としないため、XAML の初期化なしで並列実行できる。
/// </remarks>
public class MarqueeShapePlanTests
{
    /// <summary>暗転の色（黒 50%）。</summary>
    private static readonly DrawingColor s_overlay = DrawingColor.FromArgb(128, 0, 0, 0);

    /// <summary>ハンドルの塗り（白）。</summary>
    private static readonly DrawingColor s_handle = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>始点マーカーの色（赤）。</summary>
    private static readonly DrawingColor s_marker = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>検証に使う外観（暗転・ハンドル・マーカーを表示する）。</summary>
    private static readonly MarqueeAppearance s_appearance = new()
    {
        ShowOverlay = true,
        ShowHandles = true,
        ShowMarker = true,
        OverlayColor = s_overlay,
        HandleColor = s_handle,
        HandleBorderColor = DrawingColor.Black,
        MarkerColor = s_marker,
        MarkerThickness = 1,
    };

    /// <summary>装飾・ハンドル・マーカーの矩形が計画の順に並ぶことを検証する。</summary>
    [Fact]
    internal void Build_計画の層の順に矩形を追加する()
    {
        var plan = CreatePlan(
            s_appearance,
            [MarqueeDrawLayer.Decoration, MarqueeDrawLayer.Handles, MarqueeDrawLayer.Marker]);
        var rects = new DrawList();
        var labels = new List<MarqueeLabelPlacement>();

        MarqueeShapePlan.Build(plan, rects, labels);

        // 暗転 1 + ハンドル（塗り 1 + 枠 4）+ マーカー（十字 2）
        Assert.Equal(1 + 5 + 2, rects.RectCount);
        Assert.Equal(s_overlay, rects.Rects[0].Color);
        Assert.Equal(s_handle, rects.Rects[1].Color);
        Assert.Equal(s_marker, rects.Rects[6].Color);
    }

    /// <summary>暗転を表示しない外観では暗転の矩形を追加しないことを検証する。</summary>
    [Fact]
    internal void Build_暗転を表示しない外観では暗転を追加しない()
    {
        var plan = CreatePlan(s_appearance with { ShowOverlay = false }, [MarqueeDrawLayer.Decoration]);
        var rects = new DrawList();

        MarqueeShapePlan.Build(plan, rects, []);

        Assert.Equal(0, rects.RectCount);
    }

    /// <summary>ハンドルを表示しない外観ではハンドルを追加しないことを検証する。</summary>
    [Fact]
    internal void Build_ハンドルを表示しない外観ではハンドルを追加しない()
    {
        var plan = CreatePlan(s_appearance with { ShowHandles = false }, [MarqueeDrawLayer.Handles]);
        var rects = new DrawList();

        MarqueeShapePlan.Build(plan, rects, []);

        Assert.Equal(0, rects.RectCount);
    }

    /// <summary>表示が有効なラベルだけを取り出すことを検証する。</summary>
    [Fact]
    internal void Build_表示が有効なラベルだけを取り出す()
    {
        var plan = CreatePlan(s_appearance, [MarqueeDrawLayer.Label]);
        var labels = new List<MarqueeLabelPlacement>();

        MarqueeShapePlan.Build(plan, new DrawList(), labels);

        Assert.Equal(2, labels.Count);
        Assert.All(labels, label => Assert.True(label.IsVisible));
    }

    /// <summary>色を <c>0xAARRGGBB</c> へ変換することを検証する。</summary>
    [Fact]
    internal void ToArgb_色をARGBへ変換する()
        => Assert.Equal(0xFF102030u, MarqueeShapePlan.ToArgb(DrawingColor.FromArgb(255, 0x10, 0x20, 0x30)));

    /// <summary>検証に使う描画計画を生成する。</summary>
    /// <param name="appearance">外観。</param>
    /// <param name="layers">層の順序。</param>
    /// <returns>描画計画。</returns>
    /// <remarks>矩形とラベルは固定値で与える（幾何の計算は提示層のテストが担う）。</remarks>
    private static MarqueeDrawPlan CreatePlan(MarqueeAppearance appearance, MarqueeDrawLayer[] layers)
        => new(
            appearance,
            new EsRect(0, 0, 200, 100),
            new EsRect(10, 10, 80, 60),
            new EsPoint(90, 70),
            [new EsRect(0, 0, 0, 10), new EsRect(0, 90, 200, 10)],
            [],
            null,
            [],
            new MarqueeHandles([new EsRect(10, 10, 8, 8)], 1),
            new MarqueeMarker(new EsPoint(50, 40), 6),
            layers,
            [
                new MarqueeLabelPlacement(new EsRect(10, 70, 40, 16), 2, 2, "80x60", 11, DrawingColor.White, s_overlay, true),
                new MarqueeLabelPlacement(new EsRect(60, 70, 30, 16), 2, 2, "50,40", 11, DrawingColor.White, s_overlay, true),
            ]);
}
