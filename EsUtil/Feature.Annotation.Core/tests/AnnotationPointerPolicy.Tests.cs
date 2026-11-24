using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.Annotation.Core.Contract;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Interaction;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary>ポインターが示す操作（<see cref="AnnotationPointerPolicy"/>）の規則を検証する。</summary>
/// <remarks>
/// 優先順（しっぽ → ハンドル → 本体）を 4 つの UI 版で共有します。 形への写像だけを UI が行います。
/// </remarks>
public sealed class AnnotationPointerPolicyTests
{
    /// <summary>注釈の上では移動、外では何もしない。</summary>
    [Fact]
    internal void Resolve_注釈の上は移動()
    {
        var document = CreateDocument();
        var inside = new EsPoint(20, 20);
        var outside = new EsPoint(300, 250);

        Assert.Equal(
            AnnotationPointerKind.Move,
            AnnotationPointerPolicy.Resolve(document, null, inside, inside, null, 0));
        Assert.Equal(
            AnnotationPointerKind.None,
            AnnotationPointerPolicy.Resolve(document, null, outside, outside, null, 0));
    }

    /// <summary>選択中のハンドルの上では、位置に合ったサイズ変更になる。</summary>
    [Theory]
    [InlineData(0, AnnotationPointerKind.ResizeTopLeft)]
    [InlineData(1, AnnotationPointerKind.ResizeTop)]
    [InlineData(2, AnnotationPointerKind.ResizeTopRight)]
    [InlineData(3, AnnotationPointerKind.ResizeLeft)]
    [InlineData(4, AnnotationPointerKind.ResizeRight)]
    [InlineData(5, AnnotationPointerKind.ResizeBottomLeft)]
    [InlineData(6, AnnotationPointerKind.ResizeBottom)]
    [InlineData(7, AnnotationPointerKind.ResizeBottomRight)]
    internal void Resolve_ハンドルはサイズ変更(int index, AnnotationPointerKind expected)
    {
        var document = CreateDocument();
        var selected = document.Items[0].Id;
        var handles = new EsRect[8];
        for (var i = 0; i < handles.Length; i++)
        {
            handles[i] = new EsRect(1000 + (i * 20), 1000, 8, 8);
        }

        var kind = AnnotationPointerPolicy.Resolve(
            document, selected, new EsPoint(0, 0), new EsPoint(handles[index].Center.X, handles[index].Center.Y), handles, 0);

        Assert.Equal(expected, kind);
    }

    /// <summary>しっぽの先端はハンドルや本体より優先する。</summary>
    [Fact]
    internal void Resolve_しっぽの先端を最優先する()
    {
        var document = CreateDocument();
        var bubble = document.Items.OfType<BubbleAnnotation>().First();
        var handles = new EsRect[8];
        for (var i = 0; i < handles.Length; i++)
        {
            handles[i] = new EsRect(bubble.Tail.Tip.X, bubble.Tail.Tip.Y, 8, 8);
        }

        var kind = AnnotationPointerPolicy.Resolve(
            document,
            bubble.Id,
            bubble.Tail.Tip,
            new EsPoint(bubble.Tail.Tip.X + 4, bubble.Tail.Tip.Y + 4),
            handles,
            2);

        Assert.Equal(AnnotationPointerKind.Tail, kind);
    }

    /// <summary>吹き出しと枠線を持つ文書を作る。</summary>
    /// <returns>文書。</returns>
    private static AnnotationDocument CreateDocument()
    {
        var document = new AnnotationDocument(400, 300);
        var frame = AnnotationGeometry.CreateFrame(
            new EsRect(0, 0, 100, 60), AnnotationDefaults.FrameAppearance, document.ImageSize);
        var bubble = AnnotationGeometry.CreateBubble(
            new EsRect(150, 100, 120, 80),
            RichText.Create("あ", TextDefaults.DefaultStyle),
            AnnotationDraftDefaults.Default,
            document.ImageSize);
        return document.Add(frame).Add(bubble);
    }
}
