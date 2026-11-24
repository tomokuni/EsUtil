using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.Annotation.Core.Geometry;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationViewTransform"/> の規則（表示矩形の採用と座標変換）を検証する。</summary>
public sealed class AnnotationViewTransformTests
{
    /// <summary>画像が見切れる（左上が負になる）正しい表示矩形は採用する。</summary>
    /// <remarks>
    /// ズーム 100% で画像がプレビューより大きい場合、左上は負になる。 これを不正として捨てると
    /// 実際の画像と注釈で別の倍率・位置を使い、注釈が画像からずれる。
    /// </remarks>
    [Fact]
    internal void Resolve_見切れる表示矩形も採用する()
    {
        var transform = AnnotationViewTransform.Resolve(
            new EsSize(707, 420),
            new EsSize(640, 400),
            new EsRect(33.6, -10, 640, 400));

        Assert.Equal(1.0, transform.Scale, 3);
        Assert.Equal(33.6, transform.Offset.X, 3);
        Assert.Equal(-10.0, transform.Offset.Y, 3);
    }

    /// <summary>縦横比が画像と違う値は捨てて、中央へ収めた倍率を使う。</summary>
    [Fact]
    internal void Resolve_縦横比が違う値は捨てる()
    {
        var transform = AnnotationViewTransform.Resolve(
            new EsSize(707, 420),
            new EsSize(640, 400),
            new EsRect(0, 0, 640, 640));

        // 640x400 を 707x420 へ収める（高さ基準）
        Assert.Equal(1.05, transform.Scale, 3);
        Assert.Equal(17.5, transform.Offset.X, 3);
        Assert.Equal(0.0, transform.Offset.Y, 3);
    }

    /// <summary>表示矩形が無い場合は中央へ収める。</summary>
    [Fact]
    internal void Resolve_表示矩形が無い場合は中央へ収める()
    {
        var transform = AnnotationViewTransform.Resolve(
            new EsSize(707, 420), new EsSize(640, 400), null);

        Assert.Equal(1.05, transform.Scale, 3);
        Assert.Equal(17.5, transform.Offset.X, 3);
        Assert.Equal(0.0, transform.Offset.Y, 3);
    }

    /// <summary>レイヤーが未配置（大きさ 0）の場合は等倍・原点にする。</summary>
    [Fact]
    internal void Resolve_レイヤー未配置は等倍()
    {
        var transform = AnnotationViewTransform.Resolve(
            new EsSize(0, 0), new EsSize(640, 400), new EsRect(0, 0, 640, 400));

        Assert.Equal(AnnotationViewTransform.Identity, transform);
    }

    /// <summary>元画像ピクセルとローカル DIP を相互に変換できる。</summary>
    [Fact]
    internal void ToSourceとToClient_相互に変換できる()
    {
        var transform = new AnnotationViewTransform(2, new EsPoint(10, 20));

        Assert.Equal(new EsPoint(30, 60), transform.ToClient(new EsPoint(10, 20)));
        Assert.Equal(new EsPoint(10, 20), transform.ToSource(new EsPoint(30, 60)));
    }

    /// <summary>矩形の変換は左上と大きさを同じ倍率で写す。</summary>
    [Fact]
    internal void ToClient_矩形を写す()
    {
        var transform = new AnnotationViewTransform(2, new EsPoint(10, 20));

        Assert.Equal(new EsRect(30, 60, 40, 20), transform.ToClient(new EsRect(10, 20, 20, 10)));
    }

    /// <summary>単精度の座標から変換しても、元画像ピクセルは 1/100 までに丸まる。</summary>
    /// <remarks>
    /// WinUI 3 の <c>Point</c> は単精度であり、そのまま変換すると <c>120.79998779292969</c> のような
    /// 誤差がログ・保存した注釈・当たり判定へ広がる。
    /// </remarks>
    [Fact]
    internal void ToSource_単精度の誤差を残さない()
    {
        var transform = AnnotationViewTransform.Identity;
        var noisy = new EsPoint((float)120.8, (float)277.6);

        var source = transform.ToSource(noisy);

        Assert.Equal(120.8, source.X);
        Assert.Equal(277.6, source.Y);
    }
}
