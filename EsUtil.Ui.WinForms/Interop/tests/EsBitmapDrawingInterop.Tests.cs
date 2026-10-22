using EsUtil.Ui.WinForms.Interop.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary><see cref="EsBitmapDrawingInterop"/> のテスト（コアの画像 → GDI+ のビットマップ）。</summary>
/// <remarks>
/// ウィンドウを必要としないため、オフスクリーンのビットマップ上で画素を実測できる。<br/>
/// 行の並び（<c>Stride</c>）と色成分の順序は、行列を入れ替えても気づきにくいため<br/>
/// <b>隅ごとに異なる色</b>を置いて検証する。
/// </remarks>
public sealed class EsBitmapDrawingInteropTests
{
    /// <summary>複製のサイズが元画像と一致することを検証する。</summary>
    [Fact]
    internal void ToDrawingBitmap_サイズを保つ()
    {
        using var source = TestHelper.CreateBitmap(3, 2, DrawingColor.Red);
        using var bitmap = EsBitmapDrawingInterop.ToDrawingBitmap(source);

        Assert.Equal(3, bitmap.Width);
        Assert.Equal(2, bitmap.Height);
    }

    /// <summary>単色の画像が同じ色で複製されることを検証する。</summary>
    [Fact]
    internal void ToDrawingBitmap_単色を保つ()
    {
        using var source = TestHelper.CreateBitmap(2, 3, DrawingColor.FromArgb(255, 12, 34, 56));
        using var bitmap = EsBitmapDrawingInterop.ToDrawingBitmap(source);

        Assert.Equal(DrawingColor.FromArgb(255, 12, 34, 56), bitmap.GetPixel(0, 0));
        Assert.Equal(DrawingColor.FromArgb(255, 12, 34, 56), bitmap.GetPixel(1, 2));
    }

    /// <summary>画素の位置（行・列）が入れ替わらないことを検証する。</summary>
    /// <remarks>
    /// 行幅（<c>Stride</c>）は画像ごとに異なるため行単位でコピーする。 行・列・色成分の取り違えを検出する。
    /// </remarks>
    [Fact]
    internal void ToDrawingBitmap_画素の位置を保つ()
    {
        var topLeft = DrawingColor.FromArgb(255, 255, 0, 0);
        var topRight = DrawingColor.FromArgb(255, 0, 255, 0);
        var bottomLeft = DrawingColor.FromArgb(255, 0, 0, 255);
        var bottomRight = DrawingColor.FromArgb(255, 255, 255, 0);

        using var source = TestHelper.CreateBitmap(2, 2, image =>
        {
            image.SetPixel(new EsPointInt(0, 0), topLeft);
            image.SetPixel(new EsPointInt(1, 0), topRight);
            image.SetPixel(new EsPointInt(0, 1), bottomLeft);
            image.SetPixel(new EsPointInt(1, 1), bottomRight);
        });
        using var bitmap = EsBitmapDrawingInterop.ToDrawingBitmap(source);

        Assert.Equal(topLeft, bitmap.GetPixel(0, 0));
        Assert.Equal(topRight, bitmap.GetPixel(1, 0));
        Assert.Equal(bottomLeft, bitmap.GetPixel(0, 1));
        Assert.Equal(bottomRight, bitmap.GetPixel(1, 1));
    }

    /// <summary>引数が null の場合に例外になることを検証する。</summary>
    [Fact]
    internal void ToDrawingBitmap_nullは例外になる()
        => Assert.Throws<ArgumentNullException>(() => EsBitmapDrawingInterop.ToDrawingBitmap(null!));

    /// <summary>破棄済みの画像の場合に例外になることを検証する。</summary>
    [Fact]
    internal void ToDrawingBitmap_破棄済みは例外になる()
    {
        var source = TestHelper.CreateBitmap(2, 2, DrawingColor.Red);
        source.Dispose();

        Assert.Throws<ObjectDisposedException>(() => EsBitmapDrawingInterop.ToDrawingBitmap(source));
    }
}
