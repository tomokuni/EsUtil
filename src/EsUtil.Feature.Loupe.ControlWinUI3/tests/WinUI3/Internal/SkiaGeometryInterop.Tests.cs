using EsUtil.Feature.Loupe.WinUI3.Internal;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;

namespace EsUtil.Feature.Loupe.WinUI3.Tests.Internal;

/// <summary><see cref="SkiaGeometryInterop"/>（コアの幾何型と Skia の幾何型の変換）のテスト。</summary>
/// <remarks>
/// 描画計画の矩形（コア座標）を Skia の描画 API へ渡す経路の検証である。<br/>
/// Skia の矩形は左上と右下で表すため、幅・高さからの換算（右端・下端の算出）に誤りがないことを確かめる。
/// </remarks>
public class SkiaGeometryInteropTests
{
    /// <summary>コアの矩形の左上と右下が Skia の矩形へ写ることを検証する。</summary>
    [Fact]
    internal void ToSKRect_左上と右下を写す()
    {
        var result = new EsRect(1, 2, 3, 4).ToSKRect();

        Assert.Equal(1.0f, result.Left);
        Assert.Equal(2.0f, result.Top);
        // 右端 = X + Width、下端 = Y + Height
        Assert.Equal(4.0f, result.Right);
        Assert.Equal(6.0f, result.Bottom);
        Assert.Equal(3.0f, result.Width);
        Assert.Equal(4.0f, result.Height);
    }

    /// <summary>負のサイズもそのまま写すことを検証する。</summary>
    /// <remarks>コアの矩形は負のサイズを保持できる（空の矩形を Skia へ渡しても値が変わらない）。</remarks>
    [Fact]
    internal void ToSKRect_負のサイズを保持する()
    {
        var result = new EsRect(5, 6, -2, -3).ToSKRect();

        Assert.Equal(5.0f, result.Left);
        Assert.Equal(6.0f, result.Top);
        Assert.Equal(3.0f, result.Right);
        Assert.Equal(3.0f, result.Bottom);
    }

    /// <summary>Skia の整数サイズがコアのサイズへ写ることを検証する。</summary>
    [Fact]
    internal void ToEsSize_成分を写す()
    {
        var result = new SKSizeI(7, 9).ToEsSize();

        Assert.Equal(new EsSize(7, 9), result);
    }
}
