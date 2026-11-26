using EsUtil.Feature.StyledText.Presenter.Formatting;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.StyledText.Core.Tests.Presenter.Formatting;

/// <summary>ポップアップで出す書式バーの位置（上・下の選択と置ける領域への収め）を検証する。</summary>
public sealed class TextFormatBarPlacementTests
{
    /// <summary>選択の上に十分な場所があれば、選択の上へ余白を空けて置く。</summary>
    [Fact]
    internal void ResolvePopupPosition_上に場所があれば選択の上へ置く()
    {
        var position = TextFormatBarPlacement.ResolvePopupPosition(
            new EsRect(100, 200, 120, 20),
            new EsSize(300, 40),
            new EsRect(0, 0, 800, 600),
            TextFormatBarPlacement.Gap);

        Assert.Equal(100, position.X);
        Assert.Equal(200 - TextFormatBarPlacement.Gap - 40, position.Y);
    }

    /// <summary>上に入りきらない場合は選択の下へ置く。</summary>
    [Fact]
    internal void ResolvePopupPosition_上に入りきらない場合は下へ置く()
    {
        var position = TextFormatBarPlacement.ResolvePopupPosition(
            new EsRect(100, 10, 120, 20),
            new EsSize(300, 40),
            new EsRect(0, 0, 800, 600),
            TextFormatBarPlacement.Gap);

        Assert.Equal(10 + 20 + TextFormatBarPlacement.Gap, position.Y);
    }

    /// <summary>どちらにも入りきらない場合は上へ置き、置ける領域へ収める。</summary>
    [Fact]
    internal void ResolvePopupPosition_どちらにも入りきらない場合は置ける領域へ収める()
    {
        var position = TextFormatBarPlacement.ResolvePopupPosition(
            new EsRect(100, 60, 120, 20),
            new EsSize(300, 200),
            new EsRect(0, 0, 800, 200),
            TextFormatBarPlacement.Gap);

        Assert.InRange(position.Y, 0, 0);
    }

    /// <summary>横にはみ出す場合は置ける領域の右端へ寄せる（切れて押せなくなるのを避ける）。</summary>
    [Fact]
    internal void ResolvePopupPosition_横にはみ出す場合は右端へ寄せる()
    {
        var position = TextFormatBarPlacement.ResolvePopupPosition(
            new EsRect(700, 200, 60, 20),
            new EsSize(300, 40),
            new EsRect(0, 0, 800, 600),
            TextFormatBarPlacement.Gap);

        Assert.Equal(800 - 300, position.X);
    }

    /// <summary>置ける領域が未確定（0）の場合は与えた位置をそのまま返す。</summary>
    [Fact]
    internal void ResolvePopupPosition_置ける領域が未確定の場合はそのまま返す()
    {
        var position = TextFormatBarPlacement.ResolvePopupPosition(
            new EsRect(100, 200, 120, 20),
            new EsSize(300, 40),
            default,
            TextFormatBarPlacement.Gap);

        Assert.Equal(100, position.X);
        Assert.Equal(200 - TextFormatBarPlacement.Gap - 40, position.Y);
    }
}
