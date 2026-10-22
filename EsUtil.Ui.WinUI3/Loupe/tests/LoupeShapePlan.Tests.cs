using EsUtil.Ui.WinUI3.Loupe.Internal;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Loupe.Tests;

/// <summary><see cref="LoupeShapePlan"/> のテスト（描画計画から矩形の列への変換）。</summary>
/// <remarks>
/// 描画計画は幾何・色・寸法を解決済みであるため、本テストは「計画の値がそのとおり矩形になるか」と<br/>
/// 「表示可否（<c>Show*</c>）で置くかどうかが決まるか」を検証する。<br/>
/// ウィンドウを必要としないため、XAML の初期化なしで並列実行できる。
/// </remarks>
public class LoupeShapePlanTests
{
    /// <summary>十字線の色（赤）。</summary>
    private static readonly DrawingColor s_cross = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>枠線の色（青）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>検証に使う外観（十字線と枠線を表示する）。</summary>
    private static readonly LoupeAppearance s_appearance = new()
    {
        ShowCrosshair = true,
        ShowBorder = true,
        CrossColor = s_cross,
        BorderColor = s_border,
    };

    /// <summary>十字線の矩形を計画のとおりに組み立てることを検証する。</summary>
    [Fact]
    internal void BuildCrosshair_計画の矩形と色を写す()
    {
        var plan = CreatePlan(s_appearance);

        var list = LoupeShapePlan.BuildCrosshair(plan);

        Assert.Equal(plan.CrosshairPixelRects.Count, list.Count);
        Assert.Equal(plan.CrosshairPixelRects[0].X, list.Rects[0].X);
        Assert.Equal(plan.CrosshairPixelRects[0].Width, list.Rects[0].Width);
        Assert.Equal(LoupeShapePlan.ToArgb(s_cross), list.Rects[0].ColorArgb);
    }

    /// <summary>枠線の矩形を計画のとおりに組み立てることを検証する。</summary>
    [Fact]
    internal void BuildBorder_計画の矩形と色を写す()
    {
        var plan = CreatePlan(s_appearance);

        var list = LoupeShapePlan.BuildBorder(plan);

        Assert.Equal(plan.BorderPixelRects.Count, list.Count);
        Assert.Equal(LoupeShapePlan.ToArgb(s_border), list.Rects[0].ColorArgb);
    }

    /// <summary>十字線を表示しない外観では矩形を組み立てないことを検証する。</summary>
    [Fact]
    internal void BuildCrosshair_表示しない外観では空になる()
    {
        var plan = CreatePlan(s_appearance with { ShowCrosshair = false });

        Assert.Equal(0, LoupeShapePlan.BuildCrosshair(plan).Count);
    }

    /// <summary>枠線を表示しない外観では矩形を組み立てないことを検証する。</summary>
    [Fact]
    internal void BuildBorder_表示しない外観では空になる()
    {
        var plan = CreatePlan(s_appearance with { ShowBorder = false });

        Assert.Equal(0, LoupeShapePlan.BuildBorder(plan).Count);
    }

    /// <summary>表示が有効なラベルだけを取り出すことを検証する。</summary>
    [Fact]
    internal void VisibleLabels_表示が有効な指示だけを返す()
    {
        var plan = CreatePlan(s_appearance);

        var labels = LoupeShapePlan.VisibleLabels(plan);

        Assert.All(labels, label => Assert.True(label.IsVisible));
        Assert.True(labels.Count <= plan.Labels.Count);
    }

    /// <summary>色を <c>0xAARRGGBB</c> へ変換することを検証する。</summary>
    [Fact]
    internal void ToArgb_色をARGBへ変換する()
        => Assert.Equal(0xFF102030u, LoupeShapePlan.ToArgb(DrawingColor.FromArgb(255, 0x10, 0x20, 0x30)));

    /// <summary>検証に使う描画計画を生成する。</summary>
    /// <param name="appearance">外観。</param>
    /// <returns>描画計画。</returns>
    /// <remarks>矩形とラベルは固定値で与える（幾何の計算は提示層のテストが担う）。</remarks>
    private static LoupeDrawPlan CreatePlan(LoupeAppearance appearance)
        => new(
            appearance,
            new EsRect(0, 0, 64, 64),
            null,
            [new EsRect(31, 0, 2, 64)],
            [new EsRect(0, 31, 64, 2)],
            [],
            [new LoupeLabelPlacement(new EsRect(2, 2, 20, 12), 2, 2, "16,16", 12, DrawingColor.White, DrawingColor.Black, 0, true)],
            new EsPoint(8, 8),
            2,
            64,
            1);
}
