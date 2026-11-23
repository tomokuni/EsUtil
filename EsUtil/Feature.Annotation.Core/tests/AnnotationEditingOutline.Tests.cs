using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.Annotation.Presenter.Drawing;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary>本文を編集中の注釈を囲む破線（<see cref="AnnotationEditingOutline"/>）の規則を検証する。</summary>
/// <remarks>
/// 見た目の寸法（太さ・破線の刻み・張り出し）は 4 つの UI 版が同じ値を使うため、コアが単一所有します。
/// 対象の矩形は**本文**（吹き出しの <c>Bounds</c> はしっぽを含む）です。
/// </remarks>
public sealed class AnnotationEditingOutlineTests
{
    /// <summary>破線の矩形は注釈の矩形から外側へ張り出す。</summary>
    [Fact]
    internal void Resolve_外側へ張り出す()
    {
        var expected = new EsRect(
            10 - AnnotationEditingOutline.Offset,
            20 - AnnotationEditingOutline.Offset,
            100 + (AnnotationEditingOutline.Offset * 2),
            50 + (AnnotationEditingOutline.Offset * 2));

        Assert.Equal(expected, AnnotationEditingOutline.Resolve(new EsRect(10, 20, 100, 50)));
    }

    /// <summary>元画像ピクセルの矩形と表示倍率から DIP の矩形を求める。</summary>
    [Fact]
    internal void Resolve_表示倍率と位置を掛ける()
    {
        var actual = AnnotationEditingOutline.Resolve(
            new EsRect(100, 50, 200, 100), 0.5, new EsPoint(30, 40));

        Assert.Equal(
            new EsRect(
                50 + 30 - AnnotationEditingOutline.Offset,
                25 + 40 - AnnotationEditingOutline.Offset,
                100 + (AnnotationEditingOutline.Offset * 2),
                50 + (AnnotationEditingOutline.Offset * 2)),
            actual);
    }

    /// <summary>吹き出しは本文の矩形を囲む（しっぽの先端まで伸ばさない）。</summary>
    [Fact]
    internal void ResolveSourceRect_吹き出しは本文の矩形を返す()
    {
        var image = new EsSize(400, 300);
        var bubble = AnnotationGeometry.CreateBubble(
            new EsRect(40, 40, 120, 80),
            RichText.Create("あ", AnnotationDefaults.TextStyle),
            AnnotationDraftDefaults.Default,
            image);

        var actual = AnnotationEditingOutline.ResolveSourceRect(bubble);

        Assert.Equal(bubble.ResolveBodyRect(), actual);
        Assert.True(actual.Height < bubble.Bounds.Height || actual.Width < bubble.Bounds.Width);
    }

    /// <summary>枠線は自分の矩形を囲む。</summary>
    [Fact]
    internal void ResolveSourceRect_枠線は自分の矩形を返す()
    {
        var frame = AnnotationGeometry.CreateFrame(
            new EsRect(30, 40, 100, 60), AnnotationDefaults.FrameAppearance, new EsSize(400, 300));

        Assert.Equal(frame.Bounds, AnnotationEditingOutline.ResolveSourceRect(frame));
    }
}
