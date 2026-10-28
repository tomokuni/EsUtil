using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary>読み込んだ画像ファイル（<see cref="EsImageFile"/>）の単体テスト（Windows 専用・並列実行）。</summary>
/// <remarks>
/// <b>1 回の読み込みで寸法と画素を求めること</b>を検証します（ファイルの読み込みは WIC が単一所有するため、
/// ここでは往復して同じ画素が戻ることと、破棄後の扱いだけを確かめます）。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public class EsImageFileTests
{
    /// <summary>一時ファイルのパスを作る。</summary>
    /// <param name="extension">拡張子（先頭のドットを含む）。</param>
    /// <returns>一時フォルダー内の一意なパス。</returns>
    private static string TempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"se_imagefile_{Guid.NewGuid():N}{extension}");

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

    /// <summary>寸法を先に求め、その後に同じ内容から画素を復号できる。</summary>
    [Fact]
    internal void GetSize_画素を読まずに寸法を返す()
    {
        var path = TempPath(".png");
        try
        {
            var size = new EsSizeInt(48, 16);
            EsImageCodec.Save(CreateGradientPixels(size.Width, size.Height), size, path);

            using var file = EsImageFile.Open(path);

            Assert.Equal(size, file.GetSize());
            Assert.Equal(new EsSizeInt(24, 8), file.GetSize(24));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>寸法を求めた後に復号しても、同じ画素が戻る。</summary>
    [Fact]
    internal void Decode_同じ内容から画素を返す()
    {
        var path = TempPath(".png");
        try
        {
            var size = new EsSizeInt(32, 24);
            var pixels = CreateGradientPixels(size.Width, size.Height);
            EsImageCodec.Save(pixels, size, path);

            using var file = EsImageFile.Open(path);

            // 寸法の取得で内容を使い切らず、続く復号でも同じ内容を使えること
            Assert.Equal(size, file.GetSize());
            var (loaded, loadedSize) = file.Decode();
            Assert.Equal(size, loadedSize);
            Assert.Equal(pixels, loaded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>呼び出し側の領域へ直接書く。</summary>
    [Fact]
    internal void Decode_呼び出し側の領域へ直接書く()
    {
        var path = TempPath(".png");
        try
        {
            var size = new EsSizeInt(16, 12);
            var pixels = CreateGradientPixels(size.Width, size.Height);
            EsImageCodec.Save(pixels, size, path);

            using var file = EsImageFile.Open(path);
            var destination = new byte[pixels.Length];
            file.Decode(0, destination);

            Assert.Equal(pixels, destination);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>長辺の上限を渡すと、寸法も画素も縮小される。</summary>
    [Fact]
    internal void Decode_長辺の上限へ縮小して読み込む()
    {
        var path = TempPath(".png");
        try
        {
            var size = new EsSizeInt(64, 32);
            EsImageCodec.Save(CreateGradientPixels(size.Width, size.Height), size, path);

            using var file = EsImageFile.Open(path);
            var (pixels, loadedSize) = file.Decode(32);

            Assert.Equal(new EsSizeInt(32, 16), loadedSize);
            Assert.Equal(32 * 4 * 16, pixels.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>領域が足りない場合は例外になる。</summary>
    [Fact]
    internal void Decode_領域が足りない場合は例外()
    {
        var path = TempPath(".png");
        try
        {
            var size = new EsSizeInt(16, 12);
            EsImageCodec.Save(CreateGradientPixels(size.Width, size.Height), size, path);

            using var file = EsImageFile.Open(path);
            var tooSmall = new byte[16 * 4];

            Assert.Throws<ArgumentException>(() => file.Decode(0, tooSmall));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>破棄した後は内容を使えない。</summary>
    [Fact]
    internal void Dispose_破棄後は内容を使えない()
    {
        var path = TempPath(".png");
        try
        {
            var size = new EsSizeInt(16, 12);
            EsImageCodec.Save(CreateGradientPixels(size.Width, size.Height), size, path);

            var file = EsImageFile.Open(path);
            file.Dispose();

            Assert.Throws<ObjectDisposedException>(() => file.GetSize());
            // 二重の破棄は安全（例外にしない）
            file.Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>存在しないファイルは開けない。</summary>
    [Fact]
    internal void Open_存在しないファイルは例外()
    {
        Assert.Throws<ArgumentException>(() => EsImageFile.Open(TempPath(".png")));
    }
}
