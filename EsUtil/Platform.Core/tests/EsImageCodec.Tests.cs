using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary>画像の窓口（<see cref="EsImageCodec"/>）の単体テスト（Windows 専用・並列実行）。</summary>
/// <remarks>
/// <b>画素を寸法と一緒に渡す形だけを検証します</b>（本クラスは画像型に依存しないため、テストも画像型を作らずに
/// <see cref="byte"/> の配列で受け渡します）。 実装は WIC が単一所有するため、ここでは
/// <b>往復して同じ画素が戻ること</b>と<b>形式の対応</b>だけを確かめます。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public class EsImageCodecTests
{
    /// <summary>一時ファイルのパスを作る。</summary>
    /// <param name="extension">拡張子（先頭のドットを含む）。</param>
    /// <returns>一時フォルダー内の一意なパス。</returns>
    private static string TempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"se_codec_{Guid.NewGuid():N}{extension}");

    /// <summary>位置ごとに値が異なる密な画素を作る（行や列のずれを検出できる）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>画素（BGRA8888・不透明。 幅 × 4 × 高さ バイト）。</returns>
    private static byte[] CreateGradientPixels(int width, int height)
    {
        var pixels = new byte[width * height * EsReadOnlyBitmap.BytesPerPixel];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * EsReadOnlyBitmap.BytesPerPixel;
                pixels[offset + 0] = (byte)(x * 255 / width);
                pixels[offset + 1] = (byte)(y * 255 / height);
                pixels[offset + 2] = (byte)((x + y) % 256);
                pixels[offset + 3] = 255;
            }
        }

        return pixels;
    }

    [Theory]
    [InlineData(".png", EsImageFileFormat.Png)]
    [InlineData("png", EsImageFileFormat.Png)]
    [InlineData("Png", EsImageFileFormat.Png)]
    [InlineData(".JPG", EsImageFileFormat.Jpeg)]
    [InlineData(".jxr", EsImageFileFormat.JpegXr)]
    [InlineData(".heic", EsImageFileFormat.Heif)]
    [InlineData(".jxl", EsImageFileFormat.JpegXl)]
    internal void TryParseFormat_拡張子と名前を受け付ける(string text, EsImageFileFormat expected)
    {
        Assert.True(EsImageCodec.TryParseFormat(text, out var format));
        Assert.Equal(expected, format);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".webp")]
    [InlineData("Png2")]
    internal void TryParseFormat_解釈できない場合はfalse(string? text)
    {
        Assert.False(EsImageCodec.TryParseFormat(text, out var format));
        Assert.Equal(EsImageFileFormat.Png, format);
    }

    [Theory]
    [InlineData(EsImageFileFormat.Png, ".png")]
    [InlineData(EsImageFileFormat.Jpeg, ".jpg")]
    [InlineData(EsImageFileFormat.Bmp, ".bmp")]
    [InlineData(EsImageFileFormat.Gif, ".gif")]
    [InlineData(EsImageFileFormat.Tiff, ".tif")]
    [InlineData(EsImageFileFormat.JpegXr, ".jxr")]
    [InlineData(EsImageFileFormat.Heif, ".heic")]
    [InlineData(EsImageFileFormat.JpegXl, ".jxl")]
    internal void GetExtension_形式ごとに1つへ固定する(EsImageFileFormat format, string expected)
        => Assert.Equal(expected, EsImageCodec.GetExtension(format));

    [Fact]
    internal void GetExtension_定義されていない値は例外()
        => Assert.Throws<ArgumentOutOfRangeException>(() => EsImageCodec.GetExtension((EsImageFileFormat)999));

    [Theory]
    [InlineData(EsImageFileFormat.Png, "PNG")]
    [InlineData(EsImageFileFormat.Jpeg, "JPEG")]
    [InlineData(EsImageFileFormat.JpegXr, "JPEG XR")]
    [InlineData(EsImageFileFormat.Heif, "HEIF")]
    [InlineData(EsImageFileFormat.JpegXl, "JPEG XL")]
    internal void GetDisplayName_OSに依らない名前を返す(EsImageFileFormat format, string expected)
        => Assert.Equal(expected, EsImageCodec.GetDisplayName(format));

    [Fact]
    internal void GetExtension_往復して同じ形式になる()
    {
        foreach (var format in Enum.GetValues<EsImageFileFormat>())
        {
            Assert.True(EsImageCodec.TryParseFormat(EsImageCodec.GetExtension(format), out var parsed));
            Assert.Equal(format, parsed);
        }
    }

    [Fact]
    internal void GetResizedSize_縦横比を保ち長辺を上限へ揃える()
    {
        var size = EsImageCodec.GetResizedSize(new EsSizeInt(1920, 1080), 960);
        Assert.Equal(new EsSizeInt(960, 540), size);

        var portrait = EsImageCodec.GetResizedSize(new EsSizeInt(1080, 1920), 960);
        Assert.Equal(new EsSizeInt(540, 960), portrait);
    }

    [Fact]
    internal void GetResizedSize_上限が長辺と等しければ同じ寸法を返す()
    {
        var size = new EsSizeInt(800, 600);
        Assert.Equal(size, EsImageCodec.GetResizedSize(size, 800));
    }

    [Fact]
    internal void GetResizedSize_拡大が必要な場合は例外()
        => Assert.Throws<NotSupportedException>(() => EsImageCodec.GetResizedSize(new EsSizeInt(800, 600), 1024));

    [Theory]
    [InlineData(0, 600, 800)]
    [InlineData(800, 0, 800)]
    [InlineData(800, 600, 0)]
    internal void GetResizedSize_許容範囲外は例外(int width, int height, int maxSize)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => EsImageCodec.GetResizedSize(new EsSizeInt(width, height), maxSize));

    [Fact]
    internal void ListableExtensions_判定に使う拡張子を含む()
    {
        Assert.Contains(".png", EsImageCodec.ListableExtensions);
        Assert.True(EsImageCodec.IsImageFile("photo.PNG"));
        Assert.False(EsImageCodec.IsImageFile("notes.txt"));
    }

    [Fact]
    internal void SaveExtensions_保存できる形式を含む()
    {
        var extensions = EsImageCodec.SaveExtensions;

        Assert.NotEmpty(extensions);
        Assert.Contains(".png", extensions);

        // 一覧の値はそのまま保存先の選択肢にできる（小文字・先頭のドットを含む）
        Assert.All(extensions, extension => Assert.StartsWith(".", extension, StringComparison.Ordinal));
        Assert.All(extensions, extension => Assert.DoesNotContain(extension, char.IsUpper));
    }

    [Fact]
    internal void Save_保存した画素をそのまま読み戻せる()
    {
        var path = TempPath(".png");
        try
        {
            var size = new EsSizeInt(32, 24);
            var pixels = CreateGradientPixels(size.Width, size.Height);

            EsImageCodec.Save(pixels, size, path);

            var (loaded, loadedSize) = EsImageCodec.Decode(File.ReadAllBytes(path));
            Assert.Equal(size, loadedSize);
            Assert.Equal(pixels, loaded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    internal void Save_行幅を指定しても余白の画素を混ぜない()
    {
        var densePath = TempPath(".png");
        var paddedPath = TempPath(".png");
        try
        {
            var size = new EsSizeInt(32, 24);
            var dense = CreateGradientPixels(size.Width, size.Height);
            var stride = (size.Width * EsReadOnlyBitmap.BytesPerPixel) + 16;
            var padded = new byte[stride * size.Height];
            for (var y = 0; y < size.Height; y++)
            {
                Array.Copy(dense, y * size.Width * EsReadOnlyBitmap.BytesPerPixel, padded, y * stride, size.Width * EsReadOnlyBitmap.BytesPerPixel);
                for (var i = (y * stride) + (size.Width * EsReadOnlyBitmap.BytesPerPixel); i < (y + 1) * stride; i++)
                {
                    padded[i] = 0xAB;
                }
            }

            EsImageCodec.Save(dense, size, densePath);
            EsImageCodec.Save(padded, size, stride, paddedPath);

            Assert.Equal(File.ReadAllBytes(densePath), File.ReadAllBytes(paddedPath));
        }
        finally
        {
            File.Delete(densePath);
            File.Delete(paddedPath);
        }
    }

    [Fact]
    internal void ReadSize_画素を読まずに寸法を返す()
    {
        var path = TempPath(".png");
        try
        {
            var size = new EsSizeInt(48, 16);
            EsImageCodec.Save(CreateGradientPixels(size.Width, size.Height), size, path);

            var bytes = File.ReadAllBytes(path);
            Assert.Equal(size, EsImageCodec.ReadSize(bytes));
            Assert.Equal(new EsSizeInt(24, 8), EsImageCodec.ReadSize(bytes, 24));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    internal void Decode_呼び出し側の領域へ直接書く()
    {
        var path = TempPath(".png");
        try
        {
            var size = new EsSizeInt(16, 12);
            var pixels = CreateGradientPixels(size.Width, size.Height);
            EsImageCodec.Save(pixels, size, path);

            var destination = new byte[pixels.Length];
            EsImageCodec.Decode(File.ReadAllBytes(path), 0, destination);

            Assert.Equal(pixels, destination);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    internal void Decode_領域が足りない場合は例外()
    {
        var path = TempPath(".png");
        try
        {
            var size = new EsSizeInt(16, 12);
            EsImageCodec.Save(CreateGradientPixels(size.Width, size.Height), size, path);

            var tooSmall = new byte[16 * 4];
            Assert.Throws<ArgumentException>(() => EsImageCodec.Decode(File.ReadAllBytes(path), 0, tooSmall));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    internal void Decode_長辺の上限へ縮小して読み込む()
    {
        var path = TempPath(".png");
        try
        {
            var size = new EsSizeInt(64, 32);
            EsImageCodec.Save(CreateGradientPixels(size.Width, size.Height), size, path);

            var (pixels, loadedSize) = EsImageCodec.Decode(File.ReadAllBytes(path), 32);

            Assert.Equal(new EsSizeInt(32, 16), loadedSize);
            Assert.Equal(32 * 4 * 16, pixels.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(EsImageScalingMode.EsNearestNeighbor)]
    [InlineData(EsImageScalingMode.EsBilinear)]
    [InlineData(EsImageScalingMode.EsBox)]
    internal void Resize_長辺の上限へ縮小した画素を返す(EsImageScalingMode mode)
    {
        var size = new EsSizeInt(64, 32);
        var pixels = CreateGradientPixels(size.Width, size.Height);

        var (resized, resizedSize) = EsImageCodec.Resize(pixels, size, size.Width * EsReadOnlyBitmap.BytesPerPixel, 32, mode);

        Assert.Equal(new EsSizeInt(32, 16), resizedSize);
        Assert.Equal(32 * 4 * 16, resized.Length);
    }

    [Fact]
    internal void Resize_拡大が必要な場合は例外()
    {
        var size = new EsSizeInt(64, 32);
        var pixels = CreateGradientPixels(size.Width, size.Height);

        Assert.Throws<NotSupportedException>(
            () => EsImageCodec.Resize(pixels, size, size.Width * EsReadOnlyBitmap.BytesPerPixel, 128));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(16)]
    [InlineData(256)]
    internal void ReduceColors_指定した色数以下になる(int colorCount)
    {
        var size = new EsSizeInt(64, 48);
        var pixels = CreateGradientPixels(size.Width, size.Height);

        var reduced = EsImageCodec.ReduceColors(pixels, size, size.Width * EsReadOnlyBitmap.BytesPerPixel, colorCount);

        var colors = new HashSet<uint>();
        for (var i = 0; i + 3 < reduced.Length; i += EsReadOnlyBitmap.BytesPerPixel)
        {
            colors.Add(BitConverter.ToUInt32(reduced, i));
        }

        Assert.True(colors.Count <= colorCount, $"{colors.Count} 色になりました（指定は {colorCount} 色）。");
        Assert.Equal(pixels.Length, reduced.Length);
    }

    [Fact]
    internal void Save_拡張子が対応形式でない場合は例外()
    {
        var path = TempPath(".txt");
        try
        {
            var size = new EsSizeInt(4, 4);
            Assert.Throws<NotSupportedException>(
                () => EsImageCodec.Save(CreateGradientPixels(size.Width, size.Height), size, path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
