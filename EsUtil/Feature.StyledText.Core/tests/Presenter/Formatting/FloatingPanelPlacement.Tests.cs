using EsUtil.Feature.StyledText.Presenter.Formatting;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.StyledText.Core.Tests.Presenter.Formatting;

/// <summary>フローティング パネルの位置（対象の外への配置・ドラッグ・置ける領域への収め）を検証する。</summary>
public sealed class FloatingPanelPlacementTests
{
    /// <summary>対象の上に十分な場所があれば、対象の上へ余白を空けて置く。</summary>
    [Fact]
    internal void ResolveOutside_上に場所があれば対象の上へ置く()
    {
        var position = FloatingPanelPlacement.ResolveOutside(
            new EsRect(100, 200, 120, 20),
            new EsSize(300, 40),
            new EsRect(0, 0, 800, 600),
            FloatingPanelPlacement.FormatBarGap);

        Assert.Equal(100, position.X);
        Assert.Equal(200 - FloatingPanelPlacement.FormatBarGap - 40, position.Y);
    }

    /// <summary>上に入りきらない場合は対象の下へ置く。</summary>
    [Fact]
    internal void ResolveOutside_上に入りきらない場合は下へ置く()
    {
        var position = FloatingPanelPlacement.ResolveOutside(
            new EsRect(100, 10, 120, 20),
            new EsSize(300, 40),
            new EsRect(0, 0, 800, 600),
            FloatingPanelPlacement.FormatBarGap);

        Assert.Equal(10 + 20 + FloatingPanelPlacement.FormatBarGap, position.Y);
    }

    /// <summary>上下に入りきらない場合は左右へ回り、置ける領域へ収める。</summary>
    [Fact]
    internal void ResolveOutside_上下に入りきらない場合は左右へ回る()
    {
        var position = FloatingPanelPlacement.ResolveOutside(
            new EsRect(100, 90, 200, 20),
            new EsSize(100, 250),
            new EsRect(0, 0, 800, 200),
            FloatingPanelPlacement.FormatBarGap);

        Assert.True(position.X > 100, $"x={position.X}");
        Assert.InRange(position.Y, 0, 1);
    }

    /// <summary>横にはみ出す場合は置ける領域の右端へ寄せる（切れて押せなくなるのを避ける）。</summary>
    [Fact]
    internal void ResolveOutside_横にはみ出す場合は右端へ寄せる()
    {
        var position = FloatingPanelPlacement.ResolveOutside(
            new EsRect(700, 200, 60, 20),
            new EsSize(300, 40),
            new EsRect(0, 0, 800, 600),
            FloatingPanelPlacement.FormatBarGap);

        Assert.Equal(800 - 300, position.X);
    }

    /// <summary>置ける領域が未確定（0）の場合は与えた位置をそのまま返す。</summary>
    [Fact]
    internal void ResolveOutside_置ける領域が未確定の場合はそのまま返す()
    {
        var position = FloatingPanelPlacement.ResolveOutside(
            new EsRect(100, 200, 120, 20),
            new EsSize(300, 40),
            default,
            FloatingPanelPlacement.FormatBarGap);

        Assert.Equal(100, position.X);
        Assert.Equal(200 - FloatingPanelPlacement.FormatBarGap - 40, position.Y);
    }

    /// <summary>ドラッグではポインターの移動量だけパネルを動かす。</summary>
    [Fact]
    internal void ResolveDrag_ポインターの移動量だけ動かす()
    {
        var position = FloatingPanelPlacement.ResolveDrag(
            new EsPoint(100, 100), new EsPoint(30, 40), new EsPoint(55, 20));

        Assert.Equal(125, position.X);
        Assert.Equal(80, position.Y);
    }

    /// <summary>置ける領域からはみ出す位置は内側へ収める。</summary>
    [Fact]
    internal void Clamp_置ける領域の内側へ収める()
    {
        var position = FloatingPanelPlacement.Clamp(
            new EsPoint(-50, 900), 200, 100, new EsRect(0, 0, 800, 600));

        Assert.Equal(0, position.X);
        Assert.Equal(500, position.Y);
    }

    /// <summary>基準の外側へも置けるが、見えている範囲との交わりで切る。</summary>
    [Fact]
    internal void ResolveAvailableArea_外側へ広げて見えている範囲と交わる()
    {
        var area = FloatingPanelPlacement.ResolveAvailableArea(
            new EsRect(100, 100, 200, 100),
            new EsRect(0, 0, 800, 600),
            FloatingPanelPlacement.OutsideAllowance);

        Assert.Equal(0, area.X);
        Assert.Equal(0, area.Y);
        Assert.Equal(540, area.Width);
        Assert.Equal(440, area.Height);
    }
}
