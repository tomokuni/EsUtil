using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native.Imaging;
using Xunit;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRect = System.Drawing.Rectangle;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary>
/// <see cref="EsWritableBitmap"/> の System.Drawing.Bitmap 変換テスト（Windows 専用・並列実行）。
/// </summary>
/// <remarks>
/// System.Drawing は Windows 専用のため、本クラスは <see cref="SupportedOSPlatformAttribute"/> で Windows に限定する。<br/>
/// 変換は LockBits（ネイティブコピー）ベースのため、Avalonia ヘッドレス初期化に依存せず<br/>
/// <c>[Fact]</c> で並列実行できる（各テストが独立した Bitmap を生成し、静的共有状態を持たない）。
/// </remarks>
[SupportedOSPlatform("windows")]
public class EsBitmapSystemDrawingTests
{
    /// <summary>単色の System.Drawing.Bitmap（Format32bppPArgb）を生成する。</summary>
    private static DrawingBitmap CreateSolidDrawingBitmap(int width, int height, byte b, byte g, byte r, byte a)
    {
        var bitmap = new DrawingBitmap(width, height, DrawingPixelFormat.Format32bppPArgb);
        var data = bitmap.LockBits(new DrawingRect(0, 0, width, height), DrawingImageLockMode.WriteOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            unsafe
            {
                var ptr = (byte*)data.Scan0;
                for (var i = 0; i < data.Stride * height; i += 4)
                {
                    ptr[i] = b;
                    ptr[i + 1] = g;
                    ptr[i + 2] = r;
                    ptr[i + 3] = a;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }

    [Fact]
    internal void ToEsWritableBitmap_ピクセルをコピーする()
    {
        using var sd = CreateSolidDrawingBitmap(4, 4, 0, 0, 255, 255); // 赤・不透明
        using var bmp = sd.ToEsWritableBitmap();
        Assert.Equal(4, bmp.Size.Width);
        Assert.Equal(4, bmp.Size.Height);
        var (B, G, R, A) = bmp.GetPixel(1, 1);
        Assert.Equal((byte)0, B);
        Assert.Equal((byte)255, R);
        Assert.Equal((byte)255, A);
    }

    [Fact]
    internal void ToEsWritableBitmap_部分透明アルファを保持する()
    {
        // 50% アルファの青（BGRA: B=255, G=0, R=0, A=128）
        using var sd = CreateSolidDrawingBitmap(4, 4, 255, 0, 0, 128);
        using var bmp = sd.ToEsWritableBitmap();
        var (B, G, R, A) = bmp.GetPixel(1, 1);
        Assert.Equal((byte)255, B);
        Assert.Equal((byte)0, G);
        Assert.Equal((byte)0, R);
        Assert.Equal((byte)128, A); // アルファが 255 に強制されない
    }

    [Fact]
    internal void ToEsBitmap_読み取り専用の複製を返す()
    {
        using var sd = CreateSolidDrawingBitmap(4, 4, 0, 0, 255, 255); // 赤・不透明
        using var bmp = sd.ToEsBitmap();

        Assert.Equal(4, bmp.Size.Width);
        Assert.Equal(4, bmp.Size.Height);
        Assert.Equal((byte)255, bmp.GetPixel(1, 1).R);
        // 書き換え可能な画像ではない（読み取り専用で返す）
        Assert.IsNotType<EsWritableBitmap>(bmp);
    }

    [Fact]
    internal void ToDrawingBitmap_部分透明アルファを保持する()
    {
        using var bmp = EsWritableBitmap.Create(4, 4);
        bmp.SetPixel(0, 0, b: 255, g: 0, r: 0, a: 128);
        using var sd = bmp.ToDrawingBitmap();
        var data = sd.LockBits(new DrawingRect(0, 0, 1, 1), DrawingImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            unsafe
            {
                var ptr = (byte*)data.Scan0;
                Assert.Equal((byte)255, ptr[0]);
                Assert.Equal((byte)0, ptr[1]);
                Assert.Equal((byte)0, ptr[2]);
                Assert.Equal((byte)128, ptr[3]); // アルファ保持
            }
        }
        finally
        {
            sd.UnlockBits(data);
        }
    }

    [Fact]
    internal void ToDrawingBitmap_複製を生成する()
    {
        using var bmp = EsWritableBitmap.Create(4, 4);
        bmp.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 255);
        using var sd = bmp.ToDrawingBitmap();
        Assert.Equal(bmp.Size.Width, sd.Width);
        Assert.Equal(bmp.Size.Height, sd.Height);
        var data = sd.LockBits(new DrawingRect(0, 0, 1, 1), DrawingImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            unsafe
            {
                var ptr = (byte*)data.Scan0;
                Assert.Equal((byte)1, ptr[0]);
                Assert.Equal((byte)2, ptr[1]);
                Assert.Equal((byte)3, ptr[2]);
                Assert.Equal((byte)255, ptr[3]);
            }
        }
        finally
        {
            sd.UnlockBits(data);
        }
    }

    [Fact]
    internal void SystemDrawing_往復変換でサイズが一致する()
    {
        using var sd = CreateSolidDrawingBitmap(4, 4, 0, 0, 255, 255);
        using var bmp = sd.ToEsWritableBitmap();
        using var back = bmp.ToDrawingBitmap();
        Assert.Equal(sd.Width, back.Width);
        Assert.Equal(sd.Height, back.Height);
    }

    // ====================================================================
    // ファイル保存・読み込み（EsBitmapFileExtensions）
    // ====================================================================

    /// <summary>一時ファイルのパスを作る（拡張子は呼び出し側が指定する）。</summary>
    /// <param name="extension">拡張子（先頭のドットを含む）。</param>
    /// <returns>一時フォルダー内の一意なパス。</returns>
    private static string TempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"se_bitmap_{Guid.NewGuid():N}{extension}");

    [Theory]
    [InlineData(".bmp")]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".gif")]
    [InlineData(".tiff")]
    internal void Save_拡張子に応じた形式で保存しLoadで読み戻せる(string extension)
    {
        using var source = EsWritableBitmap.Create(6, 4, System.Drawing.Color.FromArgb(255, 10, 20, 30));
        var path = TempPath(extension);
        try
        {
            source.Save(path);

            Assert.True(File.Exists(path));
            using var loaded = EsBitmapFileExtensions.Load(path);
            Assert.Equal(6, loaded.Size.Width);
            Assert.Equal(4, loaded.Size.Height);
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    internal void Save_大文字の拡張子も受け付ける()
    {
        using var source = EsWritableBitmap.Create(4, 4, System.Drawing.Color.Red);
        var path = TempPath(".PNG");
        try
        {
            source.Save(path);

            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    internal void Save_未対応の拡張子はNotSupportedExceptionを投げる()
    {
        using var source = EsWritableBitmap.Create(4, 4);

        Assert.Throws<NotSupportedException>(() => source.Save(TempPath(".webp")));
    }

    [Fact]
    internal void Save_空白のパスはArgumentExceptionを投げる()
    {
        using var source = EsWritableBitmap.Create(4, 4);

        Assert.Throws<ArgumentException>(() => source.Save("   "));
    }

    [Fact]
    internal void Save_nullはArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => EsBitmapFileExtensions.Save(null!, TempPath(".png")));

    [Fact]
    internal void Load_存在しないファイルはArgumentExceptionを投げる()
        => Assert.Throws<ArgumentException>(() => EsBitmapFileExtensions.Load(TempPath(".png")));

    [Fact]
    internal void Load_空白のパスはArgumentExceptionを投げる()
        => Assert.Throws<ArgumentException>(() => EsBitmapFileExtensions.Load(""));
}
