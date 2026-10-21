using EsUtil.Platform.Core.Svg;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Svg;

/// <summary><see cref="SvgPathRasterizer"/> の単体テスト（図形の画素への展開）。</summary>
/// <remarks>
/// ウィンドウ アイコンは ICO（画素）を要求するため、形状から画素を作る処理を固定する。<br/>
/// 表示に依存しないため、XAML を初期化せずに検証できる。
/// </remarks>
public class SvgPathRasterizerTests
{
    /// <summary>四角形の図形が一辺いっぱいへ広がることを検証する。</summary>
    [Fact]
    internal void Rasterize_四角形を一辺いっぱいへ広げる()
    {
        var figures = SvgPathParser.Parse("M4 4 L20 4 L20 20 L4 20 Z");

        var pixels = SvgPathRasterizer.Rasterize(figures, 8, 0xFF000000);

        Assert.Equal(8 * 8 * 4, pixels.Length);

        // 四角形は中央にあり、外接矩形が一辺いっぱいへ広がるため四隅も塗られる
        Assert.Equal(255, Alpha(pixels, 8, 0, 0));
        Assert.Equal(255, Alpha(pixels, 8, 7, 7));
    }

    /// <summary>図形の外側が透明のままであることを検証する。</summary>
    [Fact]
    internal void Rasterize_図形の外側は透明にする()
    {
        // 縦長の図形（外接矩形の左右に余白ができる）
        var figures = SvgPathParser.Parse("M0 0 L12 0 L12 24 L0 24 Z");

        var pixels = SvgPathRasterizer.Rasterize(figures, 16, 0xFF102030);

        // 外接矩形を一辺いっぱいへ広げると、幅 8 の図形が中央（X 4〜12）に置かれる
        Assert.NotEqual(0, Alpha(pixels, 16, 8, 8));
        Assert.Equal(0, Alpha(pixels, 16, 14, 8));
    }

    /// <summary>指定した色で塗ることを検証する。</summary>
    [Fact]
    internal void Rasterize_指定した色で塗る()
    {
        var figures = SvgPathParser.Parse("M0 0 L24 0 L24 24 L0 24 Z");

        var pixels = SvgPathRasterizer.Rasterize(figures, 4, 0xFF112233);

        var offset = (2 * 4 * 4) + (2 * 4);
        Assert.Equal(0x33, pixels[offset]);      // B
        Assert.Equal(0x22, pixels[offset + 1]);  // G
        Assert.Equal(0x11, pixels[offset + 2]);  // R
        Assert.Equal(0xFF, pixels[offset + 3]);  // A
    }

    /// <summary>図形が無い場合に全て透明になることを検証する。</summary>
    [Fact]
    internal void Rasterize_図形が無い場合は透明な画素を返す()
    {
        var pixels = SvgPathRasterizer.Rasterize([], 4, 0xFF000000);

        Assert.Equal(4 * 4 * 4, pixels.Length);
        Assert.All(Enumerable.Range(0, 16), index => Assert.Equal(0, pixels[(index * 4) + 3]));
    }

    /// <summary>曲線を含む図形が塗られることを検証する。</summary>
    [Fact]
    internal void Rasterize_曲線を含む図形を塗る()
    {
        // 円に近い形（4 つの 3 次ベジェ）
        var figures = SvgPathParser.Parse(
            "M12 2 C17 2 22 7 22 12 C22 17 17 22 12 22 C7 22 2 17 2 12 C2 7 7 2 12 2 Z");

        var pixels = SvgPathRasterizer.Rasterize(figures, 16, 0xFF000000);

        // 中心は塗られ、四隅は透明になる（円が四角形より小さいため）
        Assert.NotEqual(0, Alpha(pixels, 16, 8, 8));
        Assert.Equal(0, Alpha(pixels, 16, 0, 0));
    }

    /// <summary>指定座標のアルファ値を取得する。</summary>
    /// <param name="pixels">画素（BGRA8888）。</param>
    /// <param name="size">一辺（ピクセル）。</param>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>アルファ値。</returns>
    private static int Alpha(byte[] pixels, int size, int x, int y) => pixels[(((y * size) + x) * 4) + 3];
}
