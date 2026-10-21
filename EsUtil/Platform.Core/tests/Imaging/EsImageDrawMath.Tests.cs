using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary><see cref="EsImageDrawMath"/> の単体テスト（Uniform 収め・Fill・空の扱い）。</summary>
/// <remarks>
/// 縦横比を保つ収め方と、領域いっぱいへの引き伸ばしの境界値を固定する。<br/>
/// 空の入力（元画像・描画先のいずれかが 0 以下）は描画しない契約として検証する。
/// </remarks>
public sealed class EsImageDrawMathTests
{
    [Fact]
    internal void ComputeDestination_Uniformは縦横比を保って中央へ配置する()
    {
        // 400x200 の画像を 100x100 の領域へ収める → 100x50（上下に 25 の余白）
        var rect = EsImageDrawMath.ComputeDestination(400, 200, 100, 100, EsImageDrawMode.Uniform);

        Assert.Equal(0f, rect.X);
        Assert.Equal(25f, rect.Y);
        Assert.Equal(100f, rect.Right);
        Assert.Equal(75f, rect.Bottom);
    }

    [Fact]
    internal void ComputeDestination_Uniformは縦長の画像を高さ基準で収める()
    {
        // 200x400 の画像を 100x100 の領域へ収める → 50x100（左右に 25 の余白）
        var rect = EsImageDrawMath.ComputeDestination(200, 400, 100, 100, EsImageDrawMode.Uniform);

        Assert.Equal(25f, rect.X);
        Assert.Equal(0f, rect.Y);
        Assert.Equal(75f, rect.Right);
        Assert.Equal(100f, rect.Bottom);
    }

    [Fact]
    internal void ComputeDestination_縦横比が一致すれば余白なしで収まる()
    {
        var rect = EsImageDrawMath.ComputeDestination(1920, 1080, 960, 540, EsImageDrawMode.Uniform);

        Assert.Equal(0f, rect.X);
        Assert.Equal(0f, rect.Y);
        Assert.Equal(960f, rect.Right);
        Assert.Equal(540f, rect.Bottom);
    }

    [Fact]
    internal void ComputeDestination_Fillは領域全体へ引き伸ばす()
    {
        var rect = EsImageDrawMath.ComputeDestination(400, 200, 100, 100, EsImageDrawMode.Fill);

        Assert.Equal(0f, rect.X);
        Assert.Equal(0f, rect.Y);
        Assert.Equal(100f, rect.Right);
        Assert.Equal(100f, rect.Bottom);
    }

    [Theory]
    [InlineData(0, 100, 100, 100)]
    [InlineData(100, 0, 100, 100)]
    [InlineData(100, 100, 0, 100)]
    [InlineData(100, 100, 100, 0)]
    [InlineData(-10, 100, 100, 100)]
    [InlineData(100, 100, -10, 100)]
    internal void ComputeDestination_空の入力は空の矩形を返す(
        int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        Assert.True(EsImageDrawMath.ComputeDestination(sourceWidth, sourceHeight, targetWidth, targetHeight, EsImageDrawMode.Uniform).IsEmpty);
        Assert.True(EsImageDrawMath.ComputeDestination(sourceWidth, sourceHeight, targetWidth, targetHeight, EsImageDrawMode.Fill).IsEmpty);
    }

    [Fact]
    internal void ComputeDestination_元画像より大きい領域へはみ出さない()
    {
        // 40x20 の画像を 200x200 の領域へ Uniform で収める → 200x100（拡大はされる）
        var rect = EsImageDrawMath.ComputeDestination(40, 20, 200, 200, EsImageDrawMode.Uniform);

        Assert.Equal(0f, rect.X);
        Assert.Equal(50f, rect.Y);
        Assert.Equal(200f, rect.Right);
        Assert.Equal(150f, rect.Bottom);
    }

    [Fact]
    internal void ComputeDestination_Uniformは元画像より小さい領域で縮小する()
    {
        // 1000x500 の画像を 100x100 の領域へ収める → 100x50（縮小して中央へ）
        var rect = EsImageDrawMath.ComputeDestination(1000, 500, 100, 100, EsImageDrawMode.Uniform);

        Assert.Equal(0f, rect.X);
        Assert.Equal(25f, rect.Y);
        Assert.Equal(100f, rect.Right);
        Assert.Equal(75f, rect.Bottom);
    }

    [Fact]
    internal void ComputeDestination_端数が出る余白でも中央へ対称に配置する()
    {
        // 余白が割り切れない場合でも、上下（左右）の余白が等分される（中央寄せの対称性）
        var rect = EsImageDrawMath.ComputeDestination(300, 100, 100, 100, EsImageDrawMode.Uniform);

        Assert.Equal(0f, rect.X);
        Assert.Equal(100f, rect.Right);
        // 高さは 100/3（端数を含む）のため、中心と高さで検証する
        Assert.Equal(50.0, (rect.Y + rect.Bottom) / 2.0, 4);
        Assert.Equal(100.0 / 3.0, rect.Bottom - rect.Y, 4);
    }
}
