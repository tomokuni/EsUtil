using DrawingColor = System.Drawing.Color;
using EsUtil.Ui.WinUI3.Marquee.Internal;
using SkiaSharp;
using Xunit;

namespace EsUtil.Ui.WinUI3.Marquee.Tests.Internal;

/// <summary><see cref="SkiaColorInterop"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// コアの色型（<see cref="DrawingColor"/>）と Skia の色型の変換は描画の全経路が使うため、<br/>
/// 「ARGB がそのまま写ること」「アルファが保たれること」「往復で値が変わらないこと」を検証する。
/// </remarks>
public class SkiaColorInteropTests
{
    /// <summary>コアの色型から Skia の色型へ ARGB がそのまま写ることを検証する。</summary>
    /// <param name="alpha">アルファ。</param>
    /// <param name="red">赤。</param>
    /// <param name="green">緑。</param>
    /// <param name="blue">青。</param>
    [Theory]
    [InlineData(255, 0, 0, 0)]
    [InlineData(255, 255, 255, 255)]
    [InlineData(0, 255, 0, 0)]
    [InlineData(128, 169, 169, 169)]
    [InlineData(153, 12, 34, 56)]
    internal void ToSKColor_ARGBをそのまま写す(byte alpha, byte red, byte green, byte blue)
    {
        var converted = DrawingColor.FromArgb(alpha, red, green, blue).ToSKColor();

        Assert.Equal(alpha, converted.Alpha);
        Assert.Equal(red, converted.Red);
        Assert.Equal(green, converted.Green);
        Assert.Equal(blue, converted.Blue);
    }

    /// <summary>Skia の色型からコアの色型へ ARGB がそのまま写ることを検証する。</summary>
    /// <param name="alpha">アルファ。</param>
    /// <param name="red">赤。</param>
    /// <param name="green">緑。</param>
    /// <param name="blue">青。</param>
    [Theory]
    [InlineData(255, 0, 0, 0)]
    [InlineData(0, 255, 0, 0)]
    [InlineData(128, 169, 169, 169)]
    internal void ToDrawingColor_ARGBをそのまま写す(byte alpha, byte red, byte green, byte blue)
    {
        var converted = new SKColor(red, green, blue, alpha).ToDrawingColor();

        Assert.Equal(alpha, converted.A);
        Assert.Equal(red, converted.R);
        Assert.Equal(green, converted.G);
        Assert.Equal(blue, converted.B);
    }

    /// <summary>往復変換で値が変わらないことを検証する。</summary>
    /// <param name="alpha">アルファ。</param>
    /// <param name="red">赤。</param>
    /// <param name="green">緑。</param>
    /// <param name="blue">青。</param>
    [Theory]
    [InlineData(255, 1, 2, 3)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(102, 200, 100, 50)]
    internal void 変換_往復で値が変わらない(byte alpha, byte red, byte green, byte blue)
    {
        var original = DrawingColor.FromArgb(alpha, red, green, blue);

        Assert.Equal(original, original.ToSKColor().ToDrawingColor());
    }
}
