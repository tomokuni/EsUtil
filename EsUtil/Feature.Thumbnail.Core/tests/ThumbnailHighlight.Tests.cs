using Xunit;

namespace EsUtil.Feature.Thumbnail.Tests;

/// <summary>強調（ホバー・選択）の帯の色を解決する処理のテスト。</summary>
/// <remarks>
/// 帯の色そのものは UI のテーマが決めるため、本クラスは「どの色をどの濃さで敷くか」だけを検証する<br/>
/// （明暗 2 色を持たないことが要点である）。
/// </remarks>
public class ThumbnailHighlightTests
{
    private const uint Black = 0xFF000000;
    private const uint White = 0xFFFFFFFF;
    private const uint Accent = 0xFF0078D4;

    [Fact]
    internal void 強調しない場合は帯を描かない()
        => Assert.Equal(0u, ThumbnailAppearance.Default.ResolveHighlightArgb(ThumbnailHighlightKind.None, Black, Accent));

    [Fact]
    internal void ホバーはテーマの文字色を薄く敷く()
    {
        var argb = ThumbnailAppearance.Default.ResolveHighlightArgb(ThumbnailHighlightKind.Hover, Black, Accent);

        // 既定の不透明度は 0.10（0xFF × 0.10 ≒ 0x1A）。 色味は文字色のまま（アクセントは使わない）
        Assert.Equal(0x1A000000u, argb);
    }

    [Fact]
    internal void 選択はテーマのアクセント色を薄く敷く()
    {
        var argb = ThumbnailAppearance.Default.ResolveHighlightArgb(ThumbnailHighlightKind.Selected, Black, Accent);

        // 既定の不透明度は 0.20（0xFF × 0.20 ≒ 0x33）
        Assert.Equal(0x330078D4u, argb);
    }

    [Fact]
    internal void 明暗のどちらでもテーマの色をそのまま使う()
    {
        var light = ThumbnailAppearance.Default.ResolveHighlightArgb(ThumbnailHighlightKind.Hover, Black, Accent);
        var dark = ThumbnailAppearance.Default.ResolveHighlightArgb(ThumbnailHighlightKind.Hover, White, Accent);

        // 明暗で固定の 2 色を持たず、渡された色（テーマの値）から作る
        Assert.Equal(0x1A000000u, light);
        Assert.Equal(0x1AFFFFFFu, dark);
    }

    [Fact]
    internal void アクセント色が取れないUIでは文字色で代用する()
    {
        var argb = ThumbnailAppearance.Default.ResolveHighlightArgb(ThumbnailHighlightKind.Selected, White, 0);

        Assert.Equal(0x33FFFFFFu, argb);
    }

    [Fact]
    internal void 濃さは外観で変えられる()
    {
        var appearance = ThumbnailAppearance.Default with { HoverHighlightOpacity = 0.5, SelectionHighlightOpacity = 1.0 };

        Assert.Equal(0x80000000u, appearance.ResolveHighlightArgb(ThumbnailHighlightKind.Hover, Black, Accent));
        Assert.Equal(0xFF0078D4u, appearance.ResolveHighlightArgb(ThumbnailHighlightKind.Selected, Black, Accent));
    }

    [Fact]
    internal void 濃さは0から1へ補正される()
    {
        var appearance = ThumbnailAppearance.Default with { HoverHighlightOpacity = 2.0, SelectionHighlightOpacity = -1.0 };

        Assert.Equal(1.0, appearance.HoverHighlightOpacity);
        Assert.Equal(0.0, appearance.SelectionHighlightOpacity);
    }

    [Fact]
    internal void 濃さが0のときは帯を描かない()
    {
        var appearance = ThumbnailAppearance.Default with { HoverHighlightOpacity = 0 };

        Assert.Equal(0u, appearance.ResolveHighlightArgb(ThumbnailHighlightKind.Hover, Black, Accent));
    }
}
