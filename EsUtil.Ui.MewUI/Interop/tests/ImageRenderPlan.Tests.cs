using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.MewUI.Interop.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.MewUI.Interop.Tests;

/// <summary><see cref="ImageRenderPlan"/> の描画可否と配置のテスト。</summary>
/// <remarks>
/// MewUI の描画 API は実際のウィンドウを必要とし画素を読み戻せないため、<b>判断（描くか / どこへ置くか）</b>を
/// 検証する。 配置の計算そのものは <see cref="EsImageDrawMath"/> のテストが担う。
/// </remarks>
public class ImageRenderPlanTests
{
    [Fact]
    internal void Create_Fillは縦横比を無視して領域全体を配置にする()
    {
        using var source = TestHelper.CreateBitmap(4, 2, DrawingColor.Red);

        var plan = ImageRenderPlan.Create(source, 4, 4, EsImageDrawMode.Fill);

        Assert.True(plan.ShouldDraw);
        Assert.Equal(new EsRect(0, 0, 4, 2), plan.Source);
        Assert.Equal(new EsRect(0, 0, 4, 4), plan.Destination);
    }

    [Fact]
    internal void Create_Uniformは縦横比を保ち上下を余白にする()
    {
        using var source = TestHelper.CreateBitmap(4, 2, DrawingColor.Red);

        var plan = ImageRenderPlan.Create(source, 8, 8, EsImageDrawMode.Uniform);

        // 8x8 の領域へ 8x4 で収まり、上下に 2 ずつ余白が残る
        Assert.True(plan.ShouldDraw);
        Assert.Equal(new EsRect(0, 2, 8, 4), plan.Destination);
    }

    [Fact]
    internal void Create_画像が無い場合は描画しない()
    {
        var plan = ImageRenderPlan.Create(null, 4, 4, EsImageDrawMode.Fill);
        Assert.False(plan.ShouldDraw);
    }

    [Fact]
    internal void Create_破棄済みの画像は描画しない()
    {
        var source = TestHelper.CreateBitmap(4, 4, DrawingColor.Red);
        source.Dispose();

        var plan = ImageRenderPlan.Create(source, 4, 4, EsImageDrawMode.Fill);
        Assert.False(plan.ShouldDraw);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(0, 0)]
    internal void Create_描画先が空の場合は描画しない(int targetWidth, int targetHeight)
    {
        using var source = TestHelper.CreateBitmap(4, 4, DrawingColor.Red);

        var plan = ImageRenderPlan.Create(source, targetWidth, targetHeight, EsImageDrawMode.Fill);
        Assert.False(plan.ShouldDraw);
    }
}
