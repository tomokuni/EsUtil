using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Capture;

/// <summary><see cref="VirtualScreenComposition"/> の合成規則を検証するテスト。</summary>
/// <remarks>
/// OS を呼ばない（撮影はテストが差し替える）ため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class VirtualScreenCompositionTests
{
    private static readonly (byte B, byte G, byte R, byte A) s_left = (0, 0, 255, 255);
    private static readonly (byte B, byte G, byte R, byte A) s_right = (0, 255, 0, 255);

    [Fact]
    internal void 左右に並ぶモニターを1枚に合成する()
    {
        var monitors = new[] { new EsRectInt(0, 0, 4, 2), new EsRectInt(4, 0, 2, 2) };
        using var image = VirtualScreenComposition.Compose(
            monitors,
            bounds => Solid(bounds, bounds.X == 0 ? s_left : s_right),
            out var origin);

        Assert.NotNull(image);
        Assert.Equal(new EsPointInt(0, 0), origin);
        Assert.Equal(6, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(s_left, (image.GetPixel(0, 1)));
        Assert.Equal(s_right, (image.GetPixel(5, 1)));
    }

    [Fact]
    internal void プライマリより左のモニターは負の原点になる()
    {
        var monitors = new[] { new EsRectInt(-4, 0, 4, 2), new EsRectInt(0, 0, 4, 2) };
        using var image = VirtualScreenComposition.Compose(
            monitors,
            bounds => Solid(bounds, bounds.X < 0 ? s_left : s_right),
            out var origin);

        Assert.NotNull(image);
        Assert.Equal(new EsPointInt(-4, 0), origin);
        Assert.Equal(8, image.Width);
        Assert.Equal(s_left, (image.GetPixel(0, 0)));
        Assert.Equal(s_right, (image.GetPixel(4, 0)));
    }

    [Fact]
    internal void 上下に並ぶモニターを縦へ合成する()
    {
        var monitors = new[] { new EsRectInt(0, -2, 3, 2), new EsRectInt(0, 0, 3, 2) };
        using var image = VirtualScreenComposition.Compose(
            monitors,
            bounds => Solid(bounds, bounds.Y < 0 ? s_left : s_right),
            out var origin);

        Assert.NotNull(image);
        Assert.Equal(new EsPointInt(0, -2), origin);
        Assert.Equal(4, image.Height);
        Assert.Equal(s_left, (image.GetPixel(0, 0)));
        Assert.Equal(s_right, (image.GetPixel(0, 3)));
    }

    [Fact]
    internal void モニターの無い領域は透明のまま残す()
    {
        var monitors = new[] { new EsRectInt(0, 0, 2, 2), new EsRectInt(4, 0, 2, 2) };
        using var image = VirtualScreenComposition.Compose(monitors, bounds => Solid(bounds, s_left), out _);

        Assert.NotNull(image);
        Assert.Equal(6, image.Width);
        Assert.Equal(0, image.GetPixel(2, 0).A);
        Assert.Equal(0, image.GetPixel(3, 1).A);
    }

    [Fact]
    internal void モニターを1つも列挙できない場合はnull()
        => Assert.Null(VirtualScreenComposition.Compose([], bounds => Solid(bounds, s_left), out _));

    [Fact]
    internal void モニターごとに1回だけ撮影する()
    {
        var monitors = new[] { new EsRectInt(0, 0, 2, 2), new EsRectInt(2, 0, 2, 2) };
        var calls = new List<EsRectInt>();

        using var image = VirtualScreenComposition.Compose(
            monitors,
            bounds =>
            {
                calls.Add(bounds);
                return Solid(bounds, s_left);
            },
            out _);

        Assert.NotNull(image);
        Assert.Equal(monitors, calls);
    }

    [Fact]
    internal void 撮影に失敗した場合は例外を伝える()
    {
        var monitors = new[] { new EsRectInt(0, 0, 2, 2), new EsRectInt(2, 0, 2, 2) };

        Assert.Throws<InvalidOperationException>(() => VirtualScreenComposition.Compose(
            monitors,
            bounds =>
            {
                if (bounds.X > 0)
                {
                    throw new InvalidOperationException("撮影できません");
                }

                return Solid(bounds, s_left);
            },
            out _));
    }

    private static EsBitmap Solid(EsRectInt bounds, (byte B, byte G, byte R, byte A) color)
    {
        var image = EsBitmap.Create(bounds.Width, bounds.Height);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                image.SetPixel(x, y, color.B, color.G, color.R, color.A);
            }
        }

        return image;
    }
}
