using EsUtil.Ui.WinUI3.Marquee.Internal;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;

namespace EsUtil.Ui.WinUI3.Marquee.Tests.Internal;

/// <summary><see cref="SkiaGeometryInterop"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 矩形とサイズの変換は描画のたびに呼ばれるため、<br/>
/// 「左上と右下で表現されること」「負のサイズを保持すること」「丸めが入らないこと」を検証する。
/// </remarks>
public class SkiaGeometryInteropTests
{
    /// <summary>矩形が左上と右下で表現されることを検証する。</summary>
    [Fact]
    internal void ToSKRect_左上と右下で表現する()
        => Assert.Equal(new SKRect(10, 20, 30, 40), new EsRect(10, 20, 20, 20).ToSKRect());

    /// <summary>負のサイズもそのまま写すことを検証する（コアは負のサイズを保持できる）。</summary>
    [Fact]
    internal void ToSKRect_負のサイズもそのまま写す()
        => Assert.Equal(new SKRect(10, 20, 5, 14), new EsRect(10, 20, -5, -6).ToSKRect());

    /// <summary>小数の座標が丸められないことを検証する。</summary>
    [Fact]
    internal void ToSKRect_小数を保持する()
    {
        var converted = new EsRect(1.5, 2.25, 3.75, 4.5).ToSKRect();

        Assert.Equal(1.5f, converted.Left);
        Assert.Equal(2.25f, converted.Top);
        Assert.Equal(5.25f, converted.Right);
        Assert.Equal(6.75f, converted.Bottom);
    }

    /// <summary>Skia の整数サイズがコアのサイズへ写ることを検証する。</summary>
    [Fact]
    internal void ToEsSize_整数サイズをコアのサイズへ写す()
    {
        var converted = new SKSizeI(200, 100).ToEsSize();

        Assert.Equal(200, converted.Width);
        Assert.Equal(100, converted.Height);
    }

    /// <summary>空のサイズ（0）もそのまま写ることを検証する。</summary>
    [Fact]
    internal void ToEsSize_空のサイズも写す() => Assert.Equal(default, SKSizeI.Empty.ToEsSize());
}
