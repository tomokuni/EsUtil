using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsSizeInt"/> の単体テスト（整数のサイズと空の判定・DIP サイズへの変換）。</summary>
/// <remarks>UI 初期化が不要な純粋な値型のため、<c>[Fact]</c> で並列実行される。</remarks>
public class EsSizeIntTests
{
    [Fact]
    public void 成分をそのまま保持する()
    {
        var size = new EsSizeInt(1920, 1080);

        Assert.Equal(1920, size.Width);
        Assert.Equal(1080, size.Height);
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        Assert.Equal(new EsSizeInt(4, 8), new EsSizeInt(4, 8));
        Assert.NotEqual(new EsSizeInt(4, 8), new EsSizeInt(8, 4));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(4, 0)]
    [InlineData(-1, 8)]
    [InlineData(4, -1)]
    public void IsEmpty_幅または高さが0以下なら真(int width, int height)
    {
        Assert.True(new EsSizeInt(width, height).IsEmpty);
    }

    [Fact]
    public void IsEmpty_幅と高さがともに正なら偽()
    {
        Assert.False(new EsSizeInt(1, 1).IsEmpty);
    }

    [Fact]
    public void ToEsSize_成分をそのまま引き継ぐ()
    {
        var size = new EsSizeInt(1920, 1080).ToEsSize();

        Assert.Equal(1920.0, size.Width);
        Assert.Equal(1080.0, size.Height);
    }

    [Fact]
    public void ToEsSize_負の寸法も引き継ぐ()
    {
        var size = new EsSizeInt(-3, -4).ToEsSize();

        Assert.Equal(-3.0, size.Width);
        Assert.Equal(-4.0, size.Height);
    }

    // ====================================================================
    // Zero / WithWidth / WithHeight
    // ====================================================================

    [Fact]
    public void Zero_は幅と高さとも0()
    {
        Assert.Equal(new EsSizeInt(0, 0), EsSizeInt.Zero);
    }

    [Fact]
    public void WithWidth_WithHeight_指定した成分だけを差し替える()
    {
        var size = new EsSizeInt(1, 2);

        Assert.Equal(new EsSizeInt(9, 2), size.WithWidth(9));
        Assert.Equal(new EsSizeInt(1, 9), size.WithHeight(9));
    }

    [Fact]
    public void 加減算は成分ごとに行う()
    {
        Assert.Equal(new EsSizeInt(4, 6), new EsSizeInt(1, 2) + new EsSizeInt(3, 4));
        Assert.Equal(new EsSizeInt(-2, -2), new EsSizeInt(1, 2) - new EsSizeInt(3, 4));
    }

    // ====================================================================
    // FromEsSize（丸め規則の集約）
    // ====================================================================

    [Fact]
    public void FromEsSize_既定は四捨五入で成分ごとに丸める()
    {
        Assert.Equal(new EsSizeInt(2, 3), EsSizeInt.FromEsSize(new EsSize(1.5, 2.5)));
    }

    [Fact]
    public void FromEsSize_丸め方法を指定できる()
    {
        var size = new EsSize(1.9, -1.9);

        Assert.Equal(new EsSizeInt(1, -1), EsSizeInt.FromEsSize(size, EsRounding.Truncate));
        Assert.Equal(new EsSizeInt(2, -1), EsSizeInt.FromEsSize(size, EsRounding.Ceiling));
    }

    // ====================================================================
    // タプル
    // ====================================================================

    [Fact]
    public void FromTuple_ToTuple_タプルと往復する()
    {
        Assert.Equal(new EsSizeInt(3, 4), EsSizeInt.FromTuple((3, 4)));
        Assert.Equal((3, 4), new EsSizeInt(3, 4).ToTuple());
    }

    [Fact]
    public void Deconstruct_タプルへ分解できる()
    {
        var (width, height) = new EsSizeInt(3, 4);

        Assert.Equal(3, width);
        Assert.Equal(4, height);
    }
}
