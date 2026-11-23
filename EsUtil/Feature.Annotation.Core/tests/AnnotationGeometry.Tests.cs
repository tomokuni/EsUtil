using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary>
/// <see cref="AnnotationGeometry"/> の規則（注釈を画像の内側へ収める）を検証する。
/// </summary>
public sealed class AnnotationGeometryTests
{
    /// <summary>矩形は大きさを収めたうえで位置を画像の内側へ寄せる。</summary>
    /// <param name="x">元の X。</param>
    /// <param name="y">元の Y。</param>
    /// <param name="width">元の幅。</param>
    /// <param name="height">元の高さ。</param>
    /// <param name="expectedX">期待する X。</param>
    /// <param name="expectedY">期待する Y。</param>
    /// <param name="expectedWidth">期待する幅。</param>
    /// <param name="expectedHeight">期待する高さ。</param>
    [Theory]
    [InlineData(-50, -20, 100, 80, 0, 0, 100, 80)]
    [InlineData(950, 620, 100, 80, 900, 520, 100, 80)]
    [InlineData(10, 10, 5000, 5000, 0, 0, 1000, 600)]
    [InlineData(100, 100, 200, 150, 100, 100, 200, 150)]
    internal void ClampRect_画像の内側へ収める(
        double x, double y, double width, double height,
        double expectedX, double expectedY, double expectedWidth, double expectedHeight)
    {
        var actual = AnnotationGeometry.ClampRect(
            new EsRect(x, y, width, height), new EsSize(1000, 600));

        Assert.Equal(new EsRect(expectedX, expectedY, expectedWidth, expectedHeight), actual);
    }

    /// <summary>しっぽの先端は画像の内側へ収める（辺の上は内側として扱う）。</summary>
    [Fact]
    internal void ClampPoint_画像の外を内側へ収める()
    {
        var image = new EsSize(1000, 600);

        Assert.Equal(new EsPoint(0, 0), AnnotationGeometry.ClampPoint(new EsPoint(-10, -10), image));
        Assert.Equal(new EsPoint(1000, 600), AnnotationGeometry.ClampPoint(new EsPoint(1200, 700), image));
        Assert.Equal(new EsPoint(500, 300), AnnotationGeometry.ClampPoint(new EsPoint(500, 300), image));
    }

    /// <summary>吹き出しのしっぽの先端が画像の外へ出ない（本文が画像の下端にあっても掴める）。</summary>
    [Fact]
    internal void CreateBubble_しっぽの先端を画像の内側へ収める()
    {
        var bubble = AnnotationGeometry.CreateBubble(
            new EsRect(800, 500, 200, 100),
            RichText.Create("test", AnnotationDefaults.TextStyle),
            AnnotationDraftDefaults.Default,
            new EsSize(1000, 600));

        Assert.True(bubble.Bounds.Right <= 1000 && bubble.Bounds.Bottom <= 600);
        Assert.True(bubble.Tail.Tip.X <= 1000 - 10 && bubble.Tail.Tip.Y <= 600 - 10);
    }

    /// <summary>文書経由の変更でも矩形としっぽの先端は画像の外へ出ない。</summary>
    [Fact]
    internal void SetBoundsとSetTailTip_画像の外を内側へ収める()
    {
        var bubble = AnnotationGeometry.CreateBubble(
            new EsRect(10, 10, 100, 60),
            RichText.Create("test", AnnotationDefaults.TextStyle),
            AnnotationDraftDefaults.Default,
            new EsSize(400, 300));
        var document = new AnnotationDocument(400, 300).Add(bubble);

        Assert.True(document.SetBounds(bubble.Id, new EsRect(380, 280, 100, 60)));
        Assert.Equal(new EsRect(300, 240, 100, 60), document.Find(bubble.Id)!.Bounds);

        Assert.True(document.SetTailTip(bubble.Id, new EsPoint(999, 999)));
        var tip = Assert.IsType<BubbleAnnotation>(document.Find(bubble.Id)).Tail.Tip;

        // 端ちょうどではなく、ハンドルを掴める余白を残した内側へ収める
        Assert.True(tip.X <= 400 - 10 && tip.Y <= 300 - 10);
        Assert.True(tip.X >= 10 && tip.Y >= 10);
    }

    /// <summary>同じ値なら変更しない（履歴に同じ手が積まれない）。</summary>
    [Fact]
    internal void SetBounds_同じ値なら変更しない()
    {
        var frame = AnnotationGeometry.CreateFrame(
            new EsRect(10, 10, 100, 60), AnnotationDefaults.FrameAppearance, new EsSize(400, 300));
        var document = new AnnotationDocument(400, 300).Add(frame);

        Assert.False(document.SetBounds(frame.Id, new EsRect(10, 10, 100, 60)));
        Assert.False(document.SetTailTip(frame.Id, new EsPoint(10, 10)));
    }

    /// <summary>本体を動かすとしっぽも同じだけ動く（しっぽが取り残されない）。</summary>
    /// <remarks>
    /// 本体だけ動かすと、しっぽが元の位置に残って本体から離れた細い三角形になる
    /// （実測 2026-11-21: 吹き出しを移動したら、しっぽだけが数百ピクセル離れた場所へ描かれた）。
    /// </remarks>
    [Fact]
    internal void SetBounds_しっぽも本体に付いて動く()
    {
        var bubble = AnnotationGeometry.CreateBubble(
            new EsRect(50, 40, 100, 60),
            RichText.Create("test", AnnotationDefaults.TextStyle),
            AnnotationDraftDefaults.Default,
            new EsSize(400, 300));
        var document = new AnnotationDocument(400, 300).Add(bubble);
        var before = Assert.IsType<BubbleAnnotation>(document.Find(bubble.Id)).Tail.Tip;

        Assert.True(document.SetBounds(bubble.Id, new EsRect(150, 130, 100, 60)));
        var after = Assert.IsType<BubbleAnnotation>(document.Find(bubble.Id)).Tail.Tip;

        Assert.Equal(before.X + 100, after.X, 3);
        Assert.Equal(before.Y + 90, after.Y, 3);
    }
}
