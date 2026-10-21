using EsUtil.Ui.WinForms.Interop.Imaging;
using Xunit;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary><see cref="ImageDrawMath"/> のテスト（配置の矩形計算）。</summary>
/// <remarks>UI ホストを必要としないため <c>[Fact]</c> で並列実行できる。</remarks>
public sealed class ImageDrawMathTests
{
    /// <summary>縦横比を保つ配置が中央寄せになることを検証する。</summary>
    [Fact]
    internal void Uniform_縦横比を保って中央へ置く()
    {
        // 4x2 の画像を 8x8 へ収める → 倍率は min(2, 4) = 2 → 8x4 を縦方向の中央へ置く
        var destination = ImageDrawMath.ComputeDestination(4, 2, 8, 8, ImageDrawMode.Uniform);

        Assert.Equal(0, destination.X);
        Assert.Equal(2, destination.Y);
        Assert.Equal(8, destination.Width);
        Assert.Equal(4, destination.Height);
    }

    /// <summary>領域へ引き伸ばす配置が全域になることを検証する。</summary>
    [Fact]
    internal void Fill_領域の全域を返す()
    {
        var destination = ImageDrawMath.ComputeDestination(4, 2, 8, 8, ImageDrawMode.Fill);

        Assert.Equal(0, destination.X);
        Assert.Equal(0, destination.Y);
        Assert.Equal(8, destination.Width);
        Assert.Equal(8, destination.Height);
    }

    /// <summary>元画像が拡大される場合も縦横比が保たれることを検証する。</summary>
    [Fact]
    internal void Uniform_拡大でも縦横比を保つ()
    {
        var destination = ImageDrawMath.ComputeDestination(100, 50, 1000, 1000, ImageDrawMode.Uniform);

        Assert.Equal(1000, destination.Width);
        Assert.Equal(500, destination.Height);
        Assert.Equal(250, destination.Y);
    }

    /// <summary>元画像または描画先が空の場合は空の矩形を返すことを検証する。</summary>
    /// <param name="sourceWidth">元画像の幅。</param>
    /// <param name="sourceHeight">元画像の高さ。</param>
    /// <param name="targetWidth">描画先の幅。</param>
    /// <param name="targetHeight">描画先の高さ。</param>
    [Theory]
    [InlineData(0, 10, 10, 10)]
    [InlineData(10, 0, 10, 10)]
    [InlineData(10, 10, 0, 10)]
    [InlineData(10, 10, 10, 0)]
    [InlineData(-1, 10, 10, 10)]
    internal void 空の入力は空の矩形を返す(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        foreach (var mode in Enum.GetValues<ImageDrawMode>())
        {
            Assert.True(ImageDrawMath.ComputeDestination(sourceWidth, sourceHeight, targetWidth, targetHeight, mode).IsEmpty);
        }
    }
}
