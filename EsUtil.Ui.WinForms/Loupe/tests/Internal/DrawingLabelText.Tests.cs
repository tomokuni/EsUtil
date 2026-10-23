
using EsUtil.Ui.WinForms.Loupe.Internal;
using Xunit;
using DrawingBrush = System.Drawing.SolidBrush;
using DrawingColor = System.Drawing.Color;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingPointF = System.Drawing.PointF;

namespace EsUtil.Ui.WinForms.Loupe.Tests.Internal;

/// <summary><see cref="DrawingLabelText"/>（ラベルの実寸測定と描画）のテスト。</summary>
/// <remarks>
/// 測る API と描く API を揃えることが本クラスの目的であるため、<br/>
/// 「DIP の実寸がスケーリング係数によらない」ことと「描いた画素が現れる」ことを検証する。
/// </remarks>
public class DrawingLabelTextTests
{
    /// <summary>実寸が正の値になることを検証する。</summary>
    [Fact]
    internal void Measure_実寸が正の値になる()
    {
        var size = DrawingLabelText.Measure("(12, 34)", 12.0, 1.0);

        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    }

    /// <summary>実寸（DIP）がスケーリング係数によらないことを検証する。</summary>
    /// <remarks>
    /// フォントサイズは DIP で指定されるため、DPI が変わってもラベルの見た目の大きさは変わらない<br/>
    /// （本クラスは画素で測って DIP へ戻す）。
    /// </remarks>
    [Fact]
    internal void Measure_実寸がスケーリング係数によらない()
    {
        var baseline = DrawingLabelText.Measure("(12, 34)", 12.0, 1.0);
        var doubled = DrawingLabelText.Measure("(12, 34)", 12.0, 2.0);

        Assert.Equal(baseline.Width, doubled.Width, 1);
        Assert.Equal(baseline.Height, doubled.Height, 1);
    }

    /// <summary>無効なスケーリング係数は基準値として扱われることを検証する。</summary>
    /// <param name="scaling">無効な係数。</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    internal void Measure_無効なスケーリング係数は基準値として扱う(double scaling)
    {
        var baseline = DrawingLabelText.Measure("(12, 34)", 12.0, 1.0);
        var invalid = DrawingLabelText.Measure("(12, 34)", 12.0, scaling);

        Assert.Equal(baseline.Width, invalid.Width, 1);
        Assert.Equal(baseline.Height, invalid.Height, 1);
    }

    /// <summary>長い文字列の方が広く測られることを検証する。</summary>
    [Fact]
    internal void Measure_長い文字列の方が広い()
    {
        var shortText = DrawingLabelText.Measure("(1, 2)", 12.0, 1.0);
        var longText = DrawingLabelText.Measure("(1234, 5678)", 12.0, 1.0);

        Assert.True(longText.Width > shortText.Width);
    }

    /// <summary>フォントサイズが大きい方が大きく測られることを検証する。</summary>
    [Fact]
    internal void Measure_フォントサイズが大きい方が大きい()
    {
        var small = DrawingLabelText.Measure("(12, 34)", 8.0, 1.0);
        var large = DrawingLabelText.Measure("(12, 34)", 24.0, 1.0);

        Assert.True(large.Width > small.Width);
        Assert.True(large.Height > small.Height);
    }

    /// <summary>文字列が null なら例外になることを検証する。</summary>
    [Fact]
    internal void Measure_文字列がnullなら例外()
        => Assert.Throws<ArgumentNullException>(() => DrawingLabelText.Measure(null!, 12.0, 1.0));

    /// <summary>描いた文字の画素が現れることを検証する。</summary>
    [Fact]
    internal void Draw_文字の画素が現れる()
    {
        using var bitmap = new Bitmap(64, 32, DrawingPixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(DrawingColor.Black);

        DrawingLabelText.Draw(graphics, "AB", 12.0, 1.0, new DrawingPointF(2, 2), DrawingColor.White);

        Assert.True(CountWhitePixels(bitmap) > 0);
    }

    /// <summary>文字列が空の場合は何も描かないことを検証する。</summary>
    /// <param name="text">描く文字列。</param>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    internal void Draw_文字列が空なら描かない(string? text)
    {
        using var bitmap = new Bitmap(64, 32, DrawingPixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(DrawingColor.Black);

        DrawingLabelText.Draw(graphics, text!, 12.0, 1.0, new DrawingPointF(2, 2), DrawingColor.White);

        Assert.Equal(0, CountWhitePixels(bitmap));
    }

    /// <summary>描画先が null なら例外になることを検証する。</summary>
    [Fact]
    internal void Draw_描画先がnullなら例外()
        => Assert.Throws<ArgumentNullException>(
            () => DrawingLabelText.Draw(null!, "AB", 12.0, 1.0, new DrawingPointF(0, 0), DrawingColor.White));

    /// <summary>描いた文字が指定した色になることを検証する（黒地に赤で描く）。</summary>
    [Fact]
    internal void Draw_指定した色で描く()
    {
        var red = DrawingColor.FromArgb(255, 255, 0, 0);
        using var bitmap = new Bitmap(64, 32, DrawingPixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(DrawingColor.Black);

        DrawingLabelText.Draw(graphics, "AB", 12.0, 1.0, new DrawingPointF(2, 2), red);

        using var brush = new DrawingBrush(red);
        var found = false;
        for (var y = 0; y < bitmap.Height && !found; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.R > 128 && pixel.G < 64 && pixel.B < 64)
                {
                    found = true;
                    break;
                }
            }
        }

        Assert.True(found);
    }

    /// <summary>白に近い画素の数を数える。</summary>
    /// <param name="bitmap">数えるビットマップ。</param>
    /// <returns>白に近い画素の数。</returns>
    private static int CountWhitePixels(Bitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) is { R: > 200, G: > 200, B: > 200 })
                {
                    count++;
                }
            }
        }

        return count;
    }
}
