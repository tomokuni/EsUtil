using EsUtil.Feature.Marquee.MewUI.Internal;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;

namespace EsUtil.Feature.Marquee.MewUI.Tests.Internal;

/// <summary><see cref="SkiaGeometryInterop"/> の変換のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 変換は MewUI 版の描画層が共用するため、座標が欠落・入れ替わりなく保持されることを検証する。
/// </remarks>
public class SkiaGeometryInteropTests
{
    /// <summary>矩形が左上と右下で同じ値へ変換されることを検証する。</summary>
    [Fact]
    internal void ToSKRect_左上と右下を保持して変換する()
    {
        var result = new EsRect(10, 20, 30, 40).ToSKRect();

        Assert.Equal(10f, result.Left);
        Assert.Equal(20f, result.Top);
        Assert.Equal(40f, result.Right);
        Assert.Equal(60f, result.Bottom);
    }

    /// <summary>負のサイズ（退化した矩形）も保持することを検証する。</summary>
    [Fact]
    internal void ToSKRect_負のサイズも保持する()
    {
        var result = new EsRect(5, 5, -10, -20).ToSKRect();

        Assert.Equal(5f, result.Left);
        Assert.Equal(5f, result.Top);
        Assert.Equal(-5f, result.Right);
        Assert.Equal(-15f, result.Bottom);
    }

    /// <summary>小数値を保持して変換することを検証する。</summary>
    [Fact]
    internal void ToSKRect_小数値を保持する()
    {
        var result = new EsRect(1.5, 2.25, 3.75, 4.5).ToSKRect();

        Assert.Equal(1.5f, result.Left);
        Assert.Equal(2.25f, result.Top);
        Assert.Equal(5.25f, result.Right);
        Assert.Equal(6.75f, result.Bottom);
    }

    /// <summary>Skia の整数サイズが幅・高さを保持して変換されることを検証する。</summary>
    [Fact]
    internal void ToEsSize_幅と高さを保持して変換する()
    {
        var result = new SKSizeI(1920, 1080).ToEsSize();

        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
    }

    /// <summary>0 サイズもそのまま変換されることを検証する（描画前の表面サイズ）。</summary>
    [Fact]
    internal void ToEsSize_0サイズもそのまま変換する()
    {
        var result = new SKSizeI(0, 0).ToEsSize();

        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
    }
}
