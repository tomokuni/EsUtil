using EsUtil.Ui.WinForms.Marquee.Internal;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Ui.WinForms.Marquee.Tests.Internal;

/// <summary><see cref="GraphicsFill"/> の矩形塗りのテスト。</summary>
/// <remarks>
/// 描画先は <see cref="Graphics"/> だけであるため、<see cref="Bitmap"/> 上の実寸の画素を検査する。
/// </remarks>
public sealed class GraphicsFillTests
{
    /// <summary>矩形の内側だけが塗られ、外側は変わらないことを検証する。</summary>
    [Fact]
    internal void Rect_指定した矩形の内側だけを塗る()
    {
        using var bitmap = TestHelper.CreateCanvas(60, 60);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            GraphicsFill.Rect(graphics, new EsRect(10, 20, 20, 30), Color.Red);
        }

        Assert.Equal(Color.Red.ToArgb(), TestHelper.Pixel(bitmap, 10, 20).ToArgb());
        Assert.Equal(Color.Red.ToArgb(), TestHelper.Pixel(bitmap, 29, 49).ToArgb());
        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 9, 20).ToArgb());
        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 30, 49).ToArgb());
        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 10, 19).ToArgb());
        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 10, 50).ToArgb());
    }

    /// <summary>アルファが画素へ反映される（半透明の塗りが下地と混ざる）ことを検証する。</summary>
    [Fact]
    internal void Rect_アルファを反映する()
    {
        using var bitmap = TestHelper.CreateCanvas(20, 20);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            GraphicsFill.Rect(graphics, new EsRect(0, 0, 20, 20), Color.FromArgb(128, 0, 0, 0));
        }

        var pixel = TestHelper.Pixel(bitmap, 10, 10);

        // 白（255）に黒の 50% を重ねると中間の灰色になる（GDI+ の丸めで 127〜128 になる）
        Assert.InRange(pixel.R, 127, 128);
        Assert.Equal(pixel.R, pixel.G);
        Assert.Equal(pixel.R, pixel.B);
    }

    /// <summary>アルファ 0 の塗りが下地を変えないことを検証する。</summary>
    [Fact]
    internal void Rect_アルファ0は下地を変えない()
    {
        using var bitmap = TestHelper.CreateCanvas(20, 20);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            GraphicsFill.Rect(graphics, new EsRect(0, 0, 20, 20), Color.FromArgb(0, 255, 0, 0));
        }

        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 10, 10).ToArgb());
    }

    /// <summary>幅または高さが 0 以下の矩形は塗らないことを検証する。</summary>
    /// <param name="width">矩形の幅。</param>
    /// <param name="height">矩形の高さ。</param>
    [Theory]
    [InlineData(0.0, 10.0)]
    [InlineData(10.0, 0.0)]
    [InlineData(-5.0, 10.0)]
    [InlineData(10.0, -5.0)]
    internal void Rect_空の矩形は塗らない(double width, double height)
    {
        using var bitmap = TestHelper.CreateCanvas(20, 20);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            GraphicsFill.Rect(graphics, new EsRect(5, 5, width, height), Color.Red);
        }

        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 5, 5).ToArgb());
    }

    /// <summary>複数の矩形が同じ色で塗られることを検証する。</summary>
    [Fact]
    internal void Rects_列挙した矩形をまとめて塗る()
    {
        using var bitmap = TestHelper.CreateCanvas(60, 60);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            GraphicsFill.Rects(
                graphics, [new EsRect(0, 0, 10, 10), new EsRect(20, 20, 10, 10)], Color.Blue);
        }

        Assert.Equal(Color.Blue.ToArgb(), TestHelper.Pixel(bitmap, 5, 5).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), TestHelper.Pixel(bitmap, 25, 25).ToArgb());
        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 15, 15).ToArgb());
    }

    /// <summary>空の一覧では何も塗らないことを検証する。</summary>
    [Fact]
    internal void Rects_空の一覧では何もしない()
    {
        using var bitmap = TestHelper.CreateCanvas(20, 20);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            GraphicsFill.Rects(graphics, [], Color.Red);
        }

        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 10, 10).ToArgb());
    }

    /// <summary>空の矩形が混ざっていても、有効な矩形だけが塗られることを検証する。</summary>
    [Fact]
    internal void Rects_空の矩形は飛ばして塗る()
    {
        using var bitmap = TestHelper.CreateCanvas(40, 40);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            GraphicsFill.Rects(graphics, [new EsRect(0, 0, 0, 0), new EsRect(10, 10, 10, 10)], Color.Green);
        }

        Assert.Equal(Color.Green.ToArgb(), TestHelper.Pixel(bitmap, 15, 15).ToArgb());
    }

    /// <summary>null の引数が引数例外になることを検証する。</summary>
    [Fact]
    internal void 引数_未指定はArgumentNullExceptionになる()
    {
        using var bitmap = TestHelper.CreateCanvas(10, 10);
        using var graphics = Graphics.FromImage(bitmap);

        Assert.Throws<ArgumentNullException>(() => GraphicsFill.Rect(null!, new EsRect(0, 0, 1, 1), Color.Red));
        Assert.Throws<ArgumentNullException>(() => GraphicsFill.Rects(null!, [], Color.Red));
        Assert.Throws<ArgumentNullException>(() => GraphicsFill.Rects(graphics, null!, Color.Red));
    }
}
