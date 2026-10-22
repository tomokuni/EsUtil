using EsUtil.Ui.WinForms.Interop.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary><see cref="ImageRenderer"/> のテスト（配置と画素）。</summary>
/// <remarks>
/// ウィンドウを必要としないため、オフスクリーンのビットマップ上で画素を実測できる。
/// </remarks>
public sealed class ImageRendererTests
{
    /// <summary>領域全体へ引き伸ばす配置が全域を覆うことを検証する。</summary>
    [Fact]
    internal void Render_Fillは領域全体を覆う()
    {
        using var source = TestHelper.CreateBitmap(4, 2, DrawingColor.FromArgb(255, 0, 128, 0));
        using var target = TestHelper.CreateTarget(8, 8);

        ImageRenderer.Render(target.Graphics, target.Size, ToBitmap(source), EsImageDrawMode.Fill);

        Assert.Equal(DrawingColor.FromArgb(255, 0, 128, 0), target.GetPixel(0, 0));
        Assert.Equal(DrawingColor.FromArgb(255, 0, 128, 0), target.GetPixel(7, 7));
    }

    /// <summary>縦横比を保つ配置が余白へ描かないことを検証する。</summary>
    [Fact]
    internal void Render_Uniformは余白へ描かない()
    {
        using var source = TestHelper.CreateBitmap(4, 2, DrawingColor.FromArgb(255, 0, 128, 0));
        using var target = TestHelper.CreateTarget(8, 8);

        ImageRenderer.Render(target.Graphics, target.Size, ToBitmap(source), EsImageDrawMode.Uniform);

        // 4x2 を 8x8 へ収める → 8x4 を縦の中央（y=2..5）へ描く
        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(4, 0));
        Assert.Equal(DrawingColor.FromArgb(255, 0, 128, 0), target.GetPixel(4, 3));
        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(4, 7));
    }

    /// <summary>画像が null の場合は何も描画しないことを検証する。</summary>
    [Fact]
    internal void Render_画像が無ければ描かない()
    {
        using var target = TestHelper.CreateTarget(8, 8);

        ImageRenderer.Render(target.Graphics, target.Size, null, EsImageDrawMode.Fill);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(0, 0));
    }

    /// <summary>描画先が空の場合は何も描画しないことを検証する。</summary>
    [Fact]
    internal void Render_描画先が空なら描かない()
    {
        using var source = TestHelper.CreateBitmap(4, 2, DrawingColor.FromArgb(255, 0, 128, 0));
        using var target = TestHelper.CreateTarget(8, 8);

        ImageRenderer.Render(target.Graphics, new Size(0, 0), ToBitmap(source), EsImageDrawMode.Fill);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(0, 0));
    }

    /// <summary>描画面が null の場合に例外になることを検証する。</summary>
    [Fact]
    internal void Render_描画面がnullなら例外になる()
        => Assert.Throws<ArgumentNullException>(
            () => ImageRenderer.Render(null!, new Size(8, 8), null, EsImageDrawMode.Fill));

    /// <summary>テスト用にコアの画像から GDI+ のビットマップを作る。</summary>
    /// <param name="source">変換元の画像。</param>
    /// <returns>GDI+ のビットマップ。 破棄は呼び出し側。</returns>
    private static Bitmap ToBitmap(EsReadOnlyBitmap source)
        => EsBitmapDrawingInterop.ToDrawingBitmap(source);
}
