using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationPanelPlacement"/> の規則を検証する。</summary>
public sealed class AnnotationPanelPlacementTests
{
    /// <summary>アプリの画面のうちパネルを置ける領域（テストで使う既定値）。</summary>
    private static readonly EsRect s_available = new(0, 0, 1280, 800);

    /// <summary>アンカーをそのままパネルの左上にする。</summary>
    [Fact]
    internal void ResolveInitial_アンカーをそのまま左上にする()
    {
        var position = AnnotationPanelPlacement.ResolveInitial(new EsPoint(123.5, 45.25));

        Assert.Equal(123.5, position.X);
        Assert.Equal(45.25, position.Y);
    }

    /// <summary>本文の上へ余白を空けて置く（本文に被せない）。</summary>
    [Fact]
    internal void ResolveOutside_本文の上へ置く()
    {
        var position = AnnotationPanelPlacement.ResolveOutside(
            target: new EsRect(200, 400, 300, 200),
            panel: new EsSize(400, 40),
            available: s_available,
            gap: 8);

        Assert.Equal(200, position.X);
        Assert.Equal(400 - 8 - 40, position.Y);
    }

    /// <summary>上に入りきらないときは本文の下へ置く。</summary>
    [Fact]
    internal void ResolveOutside_上に入らなければ下へ置く()
    {
        var position = AnnotationPanelPlacement.ResolveOutside(
            target: new EsRect(200, 10, 300, 200),
            panel: new EsSize(400, 40),
            available: s_available,
            gap: 8);

        Assert.Equal(10 + 200 + 8, position.Y);
    }

    /// <summary>上下とも入りきらないときは本文の右へ置く。</summary>
    [Fact]
    internal void ResolveOutside_上下に入らなければ右へ置く()
    {
        var position = AnnotationPanelPlacement.ResolveOutside(
            target: new EsRect(200, 10, 300, 760),
            panel: new EsSize(400, 40),
            available: s_available,
            gap: 8);

        Assert.Equal(200 + 300 + 8, position.X);
    }

    /// <summary>置ける領域からはみ出さない（見えなくならない）。</summary>
    [Fact]
    internal void ResolveOutside_置ける領域からはみ出さない()
    {
        var position = AnnotationPanelPlacement.ResolveOutside(
            target: new EsRect(1200, 400, 70, 100),
            panel: new EsSize(400, 40),
            available: s_available,
            gap: 8);

        Assert.InRange(position.X, s_available.X, s_available.X + s_available.Width - 400);
        Assert.InRange(position.Y, s_available.Y, s_available.Y + s_available.Height - 40);
    }

    /// <summary>ドラッグではポインターの移動量だけを足す。</summary>
    [Fact]
    internal void ResolveDrag_ポインターの移動量だけを足す()
    {
        var position = AnnotationPanelPlacement.ResolveDrag(
            origin: new EsPoint(500, 400),
            cursorOrigin: new EsPoint(520, 410),
            cursorCurrent: new EsPoint(480, 470));

        Assert.Equal(460, position.X);
        Assert.Equal(460, position.Y);
    }

    /// <summary>移動していないときは位置が変わらない。</summary>
    [Fact]
    internal void ResolveDrag_移動していないときは変わらない()
    {
        var origin = new EsPoint(700, 300);

        var position = AnnotationPanelPlacement.ResolveDrag(origin, origin, origin);

        Assert.Equal(origin, position);
    }
}
