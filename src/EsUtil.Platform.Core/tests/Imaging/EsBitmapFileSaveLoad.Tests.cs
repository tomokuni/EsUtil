using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>
/// 画像のファイル保存・読み込み（<see cref="EsBitmapBase.Save(string)"/> / <see cref="EsBitmapBase.Save(string, EsImageSaveOptions)"/> / <see cref="EsBitmap.Load(string)"/> / <see cref="EsWritableBitmap.Load(string)"/>）のテスト（Windows 専用・並列実行）。
/// </summary>
/// <remarks>
/// GDI+ は Windows 専用のため、本クラスは <see cref="SupportedOSPlatformAttribute"/> で Windows に限定する。<br/>
/// 実ファイルへ書き出して読み戻すため、各テストが独立した一時ファイルを使い（静的共有状態を持たない）、
/// <c>[Fact]</c> で並列実行できる。
/// </remarks>
[SupportedOSPlatform("windows")]
public class EsBitmapFileSaveLoadTests
{
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
            using var loaded = EsBitmap.Load(path);
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
    internal void Save_nullのパスはArgumentNullExceptionを投げる()
    {
        using var source = EsWritableBitmap.Create(4, 4);

        Assert.Throws<ArgumentNullException>(() => source.Save(null!));
    }

    [Fact]
    internal void Load_nullのパスはArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => EsBitmap.Load(null!));

    [Fact]
    internal void Load_存在しないファイルはArgumentExceptionを投げる()
        => Assert.Throws<ArgumentException>(() => EsBitmap.Load(TempPath(".png")));

    [Fact]
    internal void Load_空白のパスはArgumentExceptionを投げる()
        => Assert.Throws<ArgumentException>(() => EsBitmap.Load(""));

    [Fact]
    internal void Load_書き込み可能な画像として読み込み書き換えられる()
    {
        var path = TempPath(".png");
        try
        {
            using (var source = EsWritableBitmap.Create(6, 4, System.Drawing.Color.FromArgb(255, 10, 20, 30)))
            {
                source.Save(path);
            }

            using var loaded = EsWritableBitmap.Load(path);

            Assert.Equal(6, loaded.Size.Width);
            Assert.Equal(4, loaded.Size.Height);

            // 読み込んだ内容をそのまま書き換えられる（複製を作り直さない）
            loaded.SetPixel(new EsUtil.Platform.Core.Geometry.EsPointInt(0, 0), System.Drawing.Color.FromArgb(255, 40, 50, 60));
            var pixel = loaded.GetPixel(0, 0);
            Assert.Equal((byte)40, pixel.R);
            Assert.Equal((byte)50, pixel.G);
            Assert.Equal((byte)60, pixel.B);
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    internal void Save_JPEGの品質を指定すると出力の大きさが変わる()
    {
        using var source = CreateNoisy(96, 96);
        var low = TempPath(".jpg");
        var high = TempPath(".jpg");
        try
        {
            source.Save(low, new EsImageSaveOptions { JpegQuality = 10 });
            source.Save(high, new EsImageSaveOptions { JpegQuality = 95 });

            var lowLength = File.ReadAllBytes(low).Length;
            var highLength = File.ReadAllBytes(high).Length;

            Assert.True(highLength > lowLength, $"品質 95 の {highLength} バイトが品質 10 の {lowLength} バイト以下です。");
        }
        finally
        {
            DeleteFile(low);
            DeleteFile(high);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    internal void Save_JPEGの品質が範囲外ならArgumentOutOfRangeExceptionを投げる(int quality)
    {
        using var source = EsWritableBitmap.Create(4, 4);
        var path = TempPath(".jpg");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => source.Save(path, new EsImageSaveOptions { JpegQuality = quality }));
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData(".bmp")]
    [InlineData(".png")]
    [InlineData(".gif")]
    [InlineData(".tiff")]
    internal void Save_JPEG以外へ品質を指定するとNotSupportedExceptionを投げる(string extension)
    {
        using var source = EsWritableBitmap.Create(4, 4);
        var path = TempPath(extension);

        Assert.Throws<NotSupportedException>(
            () => source.Save(path, new EsImageSaveOptions { JpegQuality = 80 }));
        Assert.False(File.Exists(path));
    }

    [Fact]
    internal void Save_オプションを指定しても未対応の拡張子はNotSupportedExceptionを投げる()
    {
        using var source = EsWritableBitmap.Create(4, 4);

        Assert.Throws<NotSupportedException>(
            () => source.Save(TempPath(".webp"), new EsImageSaveOptions { JpegQuality = 80 }));
    }

    [Fact]
    internal void Save_既定のオプションは品質を省略した場合と同じ結果になる()
    {
        using var source = CreateNoisy(64, 64);
        var implicitPath = TempPath(".jpg");
        var explicitPath = TempPath(".jpg");
        try
        {
            source.Save(implicitPath);
            source.Save(explicitPath, default);

            Assert.Equal(File.ReadAllBytes(implicitPath), File.ReadAllBytes(explicitPath));
        }
        finally
        {
            DeleteFile(implicitPath);
            DeleteFile(explicitPath);
        }
    }

    [Theory]
    [InlineData(EsImageFileFormat.Bmp, ".bmp")]
    [InlineData(EsImageFileFormat.Png, ".png")]
    [InlineData(EsImageFileFormat.Jpeg, ".jpg")]
    [InlineData(EsImageFileFormat.Gif, ".gif")]
    [InlineData(EsImageFileFormat.Tiff, ".tif")]
    internal void Save_拡張子が無いパスへ形式を指定すると拡張子を補完して保存する(EsImageFileFormat format, string expectedExtension)
    {
        using var source = EsWritableBitmap.Create(4, 4, System.Drawing.Color.Red);
        var path = Path.Combine(Path.GetTempPath(), $"se_bitmap_{Guid.NewGuid():N}");
        var completed = path + expectedExtension;
        try
        {
            source.Save(path, new EsImageSaveOptions { Format = format });

            Assert.False(File.Exists(path));
            Assert.True(File.Exists(completed));
            Assert.Equal(FormatOf(completed), format);
        }
        finally
        {
            DeleteFile(path);
            DeleteFile(completed);
        }
    }

    [Fact]
    internal void Save_形式と一致する拡張子なら形式を指定しても保存できる()
    {
        using var source = EsWritableBitmap.Create(4, 4, System.Drawing.Color.Red);
        var path = TempPath(".jpg");
        try
        {
            source.Save(path, new EsImageSaveOptions { Format = EsImageFileFormat.Jpeg, JpegQuality = 80 });

            Assert.Equal(EsImageFileFormat.Jpeg, FormatOf(path));
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    internal void Save_形式と拡張子が食い違い無視しないならArgumentExceptionを投げる()
    {
        using var source = EsWritableBitmap.Create(4, 4);
        var path = TempPath(".png");

        Assert.Throws<ArgumentException>(
            () => source.Save(path, new EsImageSaveOptions { Format = EsImageFileFormat.Jpeg }));
        Assert.False(File.Exists(path));
    }

    [Fact]
    internal void Save_形式と拡張子が食い違い無視するなら指定形式で与えられた名前へ保存する()
    {
        using var source = EsWritableBitmap.Create(4, 4, System.Drawing.Color.Red);
        var path = TempPath(".png");
        try
        {
            source.Save(path, new EsImageSaveOptions
            {
                Format = EsImageFileFormat.Jpeg,
                IgnoreFormatMismatch = true,
            });

            // ファイル名は与えられたまま（.png）で、中身は指定した形式（JPEG）
            Assert.True(File.Exists(path));
            Assert.Equal(EsImageFileFormat.Jpeg, FormatOf(path));
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    internal void Save_形式を指定しても未対応の拡張子は無視しないならNotSupportedExceptionを投げる()
    {
        using var source = EsWritableBitmap.Create(4, 4);

        Assert.Throws<NotSupportedException>(
            () => source.Save(TempPath(".webp"), new EsImageSaveOptions { Format = EsImageFileFormat.Png }));
    }

    [Fact]
    internal void Save_未対応の拡張子へ形式を指定して無視するなら指定形式で保存する()
    {
        using var source = EsWritableBitmap.Create(4, 4, System.Drawing.Color.Red);
        var path = TempPath(".webp");
        try
        {
            source.Save(path, new EsImageSaveOptions
            {
                Format = EsImageFileFormat.Png,
                IgnoreFormatMismatch = true,
            });

            Assert.Equal(EsImageFileFormat.Png, FormatOf(path));
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    internal void Save_拡張子も形式も無い場合はNotSupportedExceptionを投げる()
    {
        using var source = EsWritableBitmap.Create(4, 4);

        Assert.Throws<NotSupportedException>(
            () => source.Save(Path.Combine(Path.GetTempPath(), $"se_bitmap_{Guid.NewGuid():N}")));
    }

    /// <summary>ファイルの先頭バイトから形式を判別する。</summary>
    /// <param name="path">対象のファイル。</param>
    /// <returns>判別した形式。</returns>
    /// <exception cref="InvalidOperationException">既知の形式でない場合。</exception>
    private static EsImageFileFormat FormatOf(string path)
    {
        var header = File.ReadAllBytes(path).AsSpan(0, 4);
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            return EsImageFileFormat.Png;
        }

        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return EsImageFileFormat.Jpeg;
        }

        if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46)
        {
            return EsImageFileFormat.Gif;
        }

        if (header[0] == 0x42 && header[1] == 0x4D)
        {
            return EsImageFileFormat.Bmp;
        }

        if ((header[0] == 0x49 && header[1] == 0x49) || (header[0] == 0x4D && header[1] == 0x4D))
        {
            return EsImageFileFormat.Tiff;
        }

        throw new InvalidOperationException($"既知の画像形式ではありません（先頭バイト: {Convert.ToHexString(header)}）。");
    }

    /// <summary>一時ファイルを消す（存在しない場合は何もしない）。</summary>
    /// <param name="path">消すファイルのパス。</param>
    private static void DeleteFile(string path)
    {
        if (File.Exists(path)) { File.Delete(path); }
    }

    /// <summary>圧縮の効果を観測できる画像を作る（画素ごとに色が不規則に変わる）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    private static EsWritableBitmap CreateNoisy(int width, int height)
    {
        var bitmap = EsWritableBitmap.Create(width, height);
        var state = 12345u;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                state = (state * 1664525u) + 1013904223u;
                bitmap.SetPixel(x, y, (byte)(state >> 24), (byte)(state >> 16), (byte)(state >> 8), 255);
            }
        }

        return bitmap;
    }
}
