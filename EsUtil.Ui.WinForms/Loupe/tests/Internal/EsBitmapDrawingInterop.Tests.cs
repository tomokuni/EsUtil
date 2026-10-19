using EsUtil.Ui.WinForms.Loupe.Internal;
using Xunit;
using DrawingColor = System.Drawing.Color;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace EsUtil.Ui.WinForms.Loupe.Tests.Internal;

/// <summary><see cref="EsBitmapDrawingInterop"/>（コアの画像 → GDI+ のビットマップ）のテスト。</summary>
/// <remarks>
/// 画素は「B = X、G = Y」の画像を使い、切り出した位置が対応することを検証する<br/>
/// （行幅（<c>Stride</c>）と原点の渡し方が誤っていると、複製した画素の位置がずれる）。
/// </remarks>
public class EsBitmapDrawingInteropTests
{
    /// <summary>指定領域の画素がそのまま複製されることを検証する。</summary>
    /// <remarks>
    /// 元画像の幅と切り出し幅を変えておく（行幅を「切り出し幅 × 4」として扱う実装では、2 行目以降がずれる）。
    /// </remarks>
    [Fact]
    internal void CopyRegion_指定領域の画素をそのまま複製する()
    {
        using var source = TestHelper.CreatePositionMap(24, 24);

        using var region = EsBitmapDrawingInterop.CopyRegion(source, x: 5, y: 7, width: 6, height: 4);

        Assert.Equal(6, region.Width);
        Assert.Equal(4, region.Height);
        Assert.Equal(DrawingPixelFormat.Format32bppPArgb, region.PixelFormat);

        // 複製した (0, 0) は元画像の (5, 7)、複製した (5, 3) は元画像の (10, 10)
        Assert.Equal(DrawingColor.FromArgb(255, 0, 7, 5).ToArgb(), region.GetPixel(0, 0).ToArgb());
        Assert.Equal(DrawingColor.FromArgb(255, 0, 10, 10).ToArgb(), region.GetPixel(5, 3).ToArgb());
    }

    /// <summary>画像全体を複製できることを検証する（境界値）。</summary>
    [Fact]
    internal void CopyRegion_画像全体を複製できる()
    {
        using var source = TestHelper.CreatePositionMap(8, 6);

        using var region = EsBitmapDrawingInterop.CopyRegion(source, x: 0, y: 0, width: 8, height: 6);

        Assert.Equal(DrawingColor.FromArgb(255, 0, 0, 0).ToArgb(), region.GetPixel(0, 0).ToArgb());
        Assert.Equal(DrawingColor.FromArgb(255, 0, 5, 7).ToArgb(), region.GetPixel(7, 5).ToArgb());
    }

    /// <summary>画像が null なら例外になることを検証する。</summary>
    [Fact]
    internal void CopyRegion_画像がnullなら例外()
        => Assert.Throws<ArgumentNullException>(() => EsBitmapDrawingInterop.CopyRegion(null!, 0, 0, 1, 1));

    /// <summary>領域の幅・高さが 1 未満なら例外になることを検証する。</summary>
    /// <param name="width">領域の幅。</param>
    /// <param name="height">領域の高さ。</param>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, -1)]
    internal void CopyRegion_領域が1ピクセル未満なら例外(int width, int height)
    {
        using var source = TestHelper.CreatePositionMap(8, 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => EsBitmapDrawingInterop.CopyRegion(source, 0, 0, width, height));
    }

    /// <summary>領域が画像の外へ出る場合は例外になることを検証する。</summary>
    /// <param name="x">領域の左上 X。</param>
    /// <param name="y">領域の左上 Y。</param>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(7, 0)]
    [InlineData(0, 7)]
    internal void CopyRegion_領域が画像の外なら例外(int x, int y)
    {
        using var source = TestHelper.CreatePositionMap(8, 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => EsBitmapDrawingInterop.CopyRegion(source, x, y, 2, 2));
    }

    /// <summary>破棄済みの画像を渡すと例外になることを検証する。</summary>
    [Fact]
    internal void CopyRegion_破棄済みの画像なら例外()
    {
        var source = TestHelper.CreatePositionMap(8, 8);
        source.Dispose();

        Assert.Throws<ObjectDisposedException>(() => EsBitmapDrawingInterop.CopyRegion(source, 0, 0, 2, 2));
    }
}
