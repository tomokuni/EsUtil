using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationHitTest"/> の仕様テスト（選択としっぽの優先順）。</summary>
/// <remarks>どの注釈が選ばれるかは UI 版で変えない（同じ操作で違う注釈が選ばれるのを防ぐ）。</remarks>
public class AnnotationHitTestTests
{
    /// <summary>重なっている場合は最前面の注釈を選ぶことを検証する。</summary>
    [Fact]
    internal void 当たり判定_重なりは最前面を選ぶ()
    {
        var back = new FrameAnnotation { Id = "back", Bounds = new EsRect(0, 0, 200, 200) };
        var front = new FrameAnnotation { Id = "front", Bounds = new EsRect(50, 50, 100, 100) };
        var document = new AnnotationDocument(400, 400) { Items = [back, front] };

        var hit = AnnotationHitTest.HitTest(document, new EsPoint(100, 100));

        Assert.NotNull(hit);
        Assert.Equal("front", hit.Id);
    }

    /// <summary>注釈が無い位置では何も選ばないことを検証する。</summary>
    [Fact]
    internal void 当たり判定_外側は選ばない()
    {
        var document = new AnnotationDocument(400, 400) { Items = [new FrameAnnotation { Id = "a", Bounds = new EsRect(0, 0, 50, 50) }] };

        Assert.Null(AnnotationHitTest.HitTest(document, new EsPoint(300, 300)));
    }

    /// <summary>しっぽの先端は本体より優先して掴めることを検証する。</summary>
    [Fact]
    internal void しっぽ_先端は本体の上でも掴める()
    {
        var bubble = new BubbleAnnotation
        {
            Id = "bubble",
            Bounds = new EsRect(0, 0, 200, 200),
            Tail = new BubbleTail(new EsPoint(100, 100), BubbleEdge.Bottom, 0.5, 20),
        };
        var document = new AnnotationDocument(400, 400) { Items = [bubble] };

        Assert.NotNull(AnnotationHitTest.HitTestTail(document, new EsPoint(103, 98)));
        Assert.Null(AnnotationHitTest.HitTestTail(document, new EsPoint(300, 300)));
    }

    /// <summary>しっぽの先端の掴む半径を広げられることを検証する。</summary>
    [Fact]
    internal void しっぽ_半径を広げると遠くても掴める()
    {
        var bubble = new BubbleAnnotation
        {
            Id = "bubble",
            Bounds = new EsRect(0, 0, 100, 100),
            Tail = new BubbleTail(new EsPoint(300, 300), BubbleEdge.Bottom, 0.5, 20),
        };
        var document = new AnnotationDocument(400, 400) { Items = [bubble] };

        Assert.Null(AnnotationHitTest.HitTestTail(document, new EsPoint(320, 300), radius: 10));
        Assert.NotNull(AnnotationHitTest.HitTestTail(document, new EsPoint(320, 300), radius: 30));
    }
}
