using EsUtil.Feature.Loupe.WinForms.Internal;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace EsUtil.Feature.Loupe.WinForms.Tests.Internal;

/// <summary><see cref="EsGeometryDrawingInterop"/>（コアの幾何型と System.Drawing の相互変換）のテスト。</summary>
public class EsGeometryDrawingInteropTests
{
    /// <summary>コアの矩形が Floating の矩形へそのまま写ることを検証する。</summary>
    /// <param name="x">左上 X。</param>
    /// <param name="y">左上 Y。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    [Theory]
    [InlineData(1.0, 2.0, 3.0, 4.0)]
    [InlineData(-5.5, -6.25, 7.5, 8.75)]
    [InlineData(0.0, 0.0, 0.0, 0.0)]
    internal void ToRectangleF_成分をそのまま写す(double x, double y, double width, double height)
    {
        var rect = new EsRect(x, y, width, height);

        var converted = rect.ToRectangleF();

        Assert.Equal(new DrawingRectangleF((float)x, (float)y, (float)width, (float)height), converted);
    }

    /// <summary>コアの整数矩形が整数の矩形へそのまま写ることを検証する。</summary>
    /// <param name="x">左上 X。</param>
    /// <param name="y">左上 Y。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    [Theory]
    [InlineData(1, 2, 3, 4)]
    [InlineData(-5, -6, 7, 8)]
    [InlineData(0, 0, 0, 0)]
    internal void ToRectangle_成分をそのまま写す(int x, int y, int width, int height)
    {
        var rect = new EsRectInt(x, y, width, height);

        var converted = rect.ToRectangle();

        Assert.Equal(new DrawingRectangle(x, y, width, height), converted);
    }

    /// <summary>コアのサイズが Floating のサイズへそのまま写ることを検証する。</summary>
    [Fact]
    internal void ToSizeF_成分をそのまま写す()
    {
        var size = new EsSize(12.5, 7.25);

        var converted = size.ToSizeF();

        Assert.Equal(new SizeF(12.5f, 7.25f), converted);
    }
}
