using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>画像のファイル保存・読み込み（<see cref="EsReadOnlyBitmap.Save(string)"/> / <see cref="EsReadOnlyBitmap.Save(string, EsImageSaveOptions)"/> / <see cref="EsBitmap.Save(string)"/> / <see cref="EsReadOnlyBitmap.Load(string)"/> / <see cref="EsBitmap.Load(string)"/>）のテスト（Windows 専用・並列実行）。</summary>
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
        using var source = EsBitmap.Create(6, 4, System.Drawing.Color.FromArgb(255, 10, 20, 30));

        // 書き換え可能な画像は保存の入口（読み取り専用の画像を受ける）へそのまま渡せないため、FromBitmap を通す
        // （未凍結なので複製になる）
        using var read = EsReadOnlyBitmap.FromBitmap(source);
        var path = TempPath(extension);
        try
        {
            read.Save(path);

            Assert.True(File.Exists(path));
            using var loaded = EsReadOnlyBitmap.Load(path);
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
        using var source = CreateReadOnly(4, 4, System.Drawing.Color.Red);
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
        using var source = CreateReadOnly(4, 4);

        Assert.Throws<NotSupportedException>(() => source.Save(TempPath(".webp")));
    }

    [Fact]
    internal void Save_空白のパスはArgumentExceptionを投げる()
    {
        using var source = CreateReadOnly(4, 4);

        Assert.Throws<ArgumentException>(() => source.Save("   "));
    }

    [Fact]
    internal void Save_nullのパスはArgumentNullExceptionを投げる()
    {
        using var source = CreateReadOnly(4, 4);

        Assert.Throws<ArgumentNullException>(() => source.Save(null!));
    }

    [Fact]
    internal void Load_nullのパスはArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => EsReadOnlyBitmap.Load(null!));

    [Fact]
    internal void Load_存在しないファイルはArgumentExceptionを投げる()
        => Assert.Throws<ArgumentException>(() => EsReadOnlyBitmap.Load(TempPath(".png")));

    [Fact]
    internal void Load_空白のパスはArgumentExceptionを投げる()
        => Assert.Throws<ArgumentException>(() => EsReadOnlyBitmap.Load(""));

    [Fact]
    internal void Load_書き込み可能な画像として読み込み書き換えられる()
    {
        var path = TempPath(".png");
        try
        {
            using (var source = CreateReadOnly(6, 4, System.Drawing.Color.FromArgb(255, 10, 20, 30)))
            {
                source.Save(path);
            }

            using var loaded = EsBitmap.Load(path);

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
        using var source = CreateReadOnly(4, 4);
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
        using var source = CreateReadOnly(4, 4);
        var path = TempPath(extension);

        Assert.Throws<NotSupportedException>(
            () => source.Save(path, new EsImageSaveOptions { JpegQuality = 80 }));
        Assert.False(File.Exists(path));
    }

    [Fact]
    internal void Save_オプションを指定しても未対応の拡張子はNotSupportedExceptionを投げる()
    {
        using var source = CreateReadOnly(4, 4);

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
        using var source = CreateReadOnly(4, 4, System.Drawing.Color.Red);
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
        using var source = CreateReadOnly(4, 4, System.Drawing.Color.Red);
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
        using var source = CreateReadOnly(4, 4);
        var path = TempPath(".png");

        Assert.Throws<ArgumentException>(
            () => source.Save(path, new EsImageSaveOptions { Format = EsImageFileFormat.Jpeg }));
        Assert.False(File.Exists(path));
    }

    [Fact]
    internal void Save_形式と拡張子が食い違い無視するなら指定形式で与えられた名前へ保存する()
    {
        using var source = CreateReadOnly(4, 4, System.Drawing.Color.Red);
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
        using var source = CreateReadOnly(4, 4);

        Assert.Throws<NotSupportedException>(
            () => source.Save(TempPath(".webp"), new EsImageSaveOptions { Format = EsImageFileFormat.Png }));
    }

    [Fact]
    internal void Save_未対応の拡張子へ形式を指定して無視するなら指定形式で保存する()
    {
        using var source = CreateReadOnly(4, 4, System.Drawing.Color.Red);
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
        using var source = CreateReadOnly(4, 4);

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

    [Theory]
    [InlineData(".bmp")]
    [InlineData(".png")]
    [InlineData(".gif")]
    internal void Save_減色を指定すると色数が減り読み戻せる(string extension)
    {
        using var source = CreateNoisy(64, 64);
        var plain = TempPath(extension);
        var reduced = TempPath(extension);
        try
        {
            source.Save(plain);
            source.Save(reduced, new EsImageSaveOptions { ColorCount = 16 });

            Assert.True(File.Exists(reduced));

            using var loaded = EsReadOnlyBitmap.Load(reduced);
            Assert.Equal(64, loaded.Width);
            Assert.Equal(64, loaded.Height);
            Assert.True(CountDistinctColors(loaded) <= 16, $"{CountDistinctColors(loaded)} 色になりました。");

            // 減色した方が小さい（GIF は元から常に量子化されるため、色数を減らしてもサイズが同じことがある）
            if (extension != ".gif")
            {
                Assert.True(
                    new FileInfo(reduced).Length < new FileInfo(plain).Length,
                    $"減色後 {new FileInfo(reduced).Length} バイトが減色前 {new FileInfo(plain).Length} バイト以上です。");
            }
        }
        finally
        {
            DeleteFile(plain);
            DeleteFile(reduced);
        }
    }

    [Fact]
    internal void Save_減色したBMPは画素が完全に一致する()
    {
        using var source = CreateNoisy(32, 32);
        using var expected = source.ReduceColors(8);
        var path = TempPath(".bmp");
        try
        {
            source.Save(path, new EsImageSaveOptions { ColorCount = 8 });

            using var loaded = EsReadOnlyBitmap.Load(path);

            // BMP はパレットを完全に保つため、ReduceColors の結果と一致する
            Assert.Equal(expected.ReadOnlyPixels.ToArray(), loaded.ReadOnlyPixels.ToArray());
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".tiff")]
    internal void Save_パレットを持てない形式へ減色を指定するとNotSupportedExceptionを投げる(string extension)
    {
        using var source = CreateNoisy(32, 32, levels: 32);
        var path = TempPath(extension);

        Assert.Throws<NotSupportedException>(
            () => source.Save(path, new EsImageSaveOptions { ColorCount = 16 }));
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(257)]
    internal void Save_減色の色数が範囲外ならArgumentOutOfRangeExceptionを投げる(int colorCount)
    {
        using var source = CreateReadOnly(4, 4);
        var path = TempPath(".png");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => source.Save(path, new EsImageSaveOptions { ColorCount = colorCount }));
        Assert.False(File.Exists(path));
    }

    [Fact]
    internal void Save_減色しない保存へ減色の品質を指定するとInvalidOperationExceptionを投げる()
    {
        // 黙って無視すると「設定したのに効かない」ことに気付かないため、組み合わせを例外にする
        using var source = CreateReadOnly(4, 4);
        var path = TempPath(".png");

        Assert.Throws<InvalidOperationException>(
            () => source.Save(path, new EsImageSaveOptions { ColorQuality = EsImageQuality.High }));
        Assert.False(File.Exists(path));
    }

    [Fact]
    internal void Save_減色しない保存へディザを指定するとInvalidOperationExceptionを投げる()
    {
        using var source = CreateReadOnly(4, 4);
        var path = TempPath(".png");

        Assert.Throws<InvalidOperationException>(
            () => source.Save(path, new EsImageSaveOptions { Dither = EsDitherMode.FloydSteinberg }));
        Assert.False(File.Exists(path));
    }

    [Fact]
    internal void Save_GIFは減色しない指定でもディザを受け付ける()
    {
        // GIF は常に量子化されるため、ColorCount を指定しなくても減色の指定が効く
        using var source = CreateNoisy(32, 32);
        var path = TempPath(".gif");
        try
        {
            source.Save(path, new EsImageSaveOptions { Dither = EsDitherMode.FloydSteinberg });

            using var loaded = EsReadOnlyBitmap.Load(path);
            Assert.Equal(32, loaded.Width);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    internal void Save_定義されていない減色の品質はArgumentOutOfRangeExceptionを投げる(int quality)
    {
        using var source = CreateNoisy(4, 4);
        var path = TempPath(".png");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => source.Save(path, new EsImageSaveOptions { ColorCount = 4, ColorQuality = (EsImageQuality)quality }));
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    internal void Save_定義されていないディザはArgumentOutOfRangeExceptionを投げる(int dither)
    {
        using var source = CreateNoisy(4, 4);
        var path = TempPath(".png");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => source.Save(path, new EsImageSaveOptions { ColorCount = 4, Dither = (EsDitherMode)dither }));
        Assert.False(File.Exists(path));
    }

    [Fact]
    internal void Save_減色の品質を指定しても色数は指定以下になる()
    {
        using var source = CreateNoisy(32, 32);
        var path = TempPath(".png");
        try
        {
            source.Save(path, new EsImageSaveOptions { ColorCount = 16, ColorQuality = EsImageQuality.Low });

            using var loaded = EsReadOnlyBitmap.Load(path);
            Assert.True(CountDistinctColors(loaded) <= 16, $"{CountDistinctColors(loaded)} 色になりました。");
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    internal void Save_ディザを指定した減色は平坦な色の画像を変えない()
    {
        // 誤差が 0 の画素には何も配られないため、単色の画像はディザの有無で結果が同じになる
        using var source = CreateReadOnly(32, 32, System.Drawing.Color.FromArgb(255, 12, 34, 56));
        var plain = TempPath(".png");
        var dithered = TempPath(".png");
        try
        {
            source.Save(plain, new EsImageSaveOptions { ColorCount = 8 });
            source.Save(dithered, new EsImageSaveOptions { ColorCount = 8, Dither = EsDitherMode.FloydSteinberg });

            using var plainImage = EsReadOnlyBitmap.Load(plain);
            using var ditheredImage = EsReadOnlyBitmap.Load(dithered);
            Assert.Equal(plainImage.ReadOnlyPixels.ToArray(), ditheredImage.ReadOnlyPixels.ToArray());
        }
        finally
        {
            DeleteFile(plain);
            DeleteFile(dithered);
        }
    }

    [Fact]
    internal void Save_減色しても透過が保たれる()
    {
        using var source = CreateWithAlpha();
        var path = TempPath(".png");
        try
        {
            source.Save(path, new EsImageSaveOptions { ColorCount = 8 });

            using var loaded = EsReadOnlyBitmap.Load(path);

            // PNG はパレットへアルファを書ける（tRNS）ため、3 段階のアルファがそのまま残る
            Assert.Equal((byte)255, loaded.GetPixel(0, 0).A);
            Assert.Equal((byte)128, loaded.GetPixel(source.Width / 2, 0).A);
            Assert.Equal((byte)0, loaded.GetPixel(source.Width - 1, 0).A);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    internal void Save_減色を指定しなくてもGIFは透過を保つ()
    {
        // GDI+ の GIF エンコーダーは透明色の番号をどの画素にも使わせないため、32bpp のまま渡すと透過が失われる。
        // 本ライブラリは GIF を常に自前の量子化で書く（GIF はパレットが必須の形式である）。
        using var source = CreateWithAlpha();
        var path = TempPath(".gif");
        try
        {
            source.Save(path);

            using var loaded = EsReadOnlyBitmap.Load(path);

            Assert.Equal((byte)0, loaded.GetPixel(source.Width - 1, 0).A);
            Assert.Equal((byte)255, loaded.GetPixel(0, 0).A);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    internal void Save_減色したGIFは完全透明を保つ()
    {
        using var source = CreateWithAlpha();
        var path = TempPath(".gif");
        try
        {
            source.Save(path, new EsImageSaveOptions { ColorCount = 8 });

            using var loaded = EsReadOnlyBitmap.Load(path);

            // GIF は 2 値の透過しか持てない（完全透明は残り、半透明は不透明になる）
            Assert.Equal((byte)0, loaded.GetPixel(source.Width - 1, 0).A);
            Assert.Equal((byte)255, loaded.GetPixel(0, 0).A);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    /// <summary>単色で塗りつぶした読み取り専用の画像を生成する（保存の入力）。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>書き終えてから凍結し、<see cref="EsReadOnlyBitmap.FromBitmap"/> で読み取り専用の面を作る
    /// （凍結済みは複製されないため、画素を 0 コピーで共有する）。</remarks>
    private static EsReadOnlyBitmap CreateReadOnly(int width, int height, System.Drawing.Color color)
    {
        using var bitmap = EsBitmap.Create(width, height, color);
        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    /// <summary>透明な読み取り専用の画像を生成する（保存の入力）。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    private static EsReadOnlyBitmap CreateReadOnly(int width, int height)
        => CreateReadOnly(width, height, System.Drawing.Color.Transparent);

    /// <summary>透明・半透明・不透明の 3 段階を持つ画像を作る（RGB は同じ）。</summary>
    /// <param name="width">幅（ピクセル。 3 等分する）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    private static EsReadOnlyBitmap CreateWithAlpha(int width = 48, int height = 8)
    {
        using var bitmap = EsBitmap.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var alpha = x < width / 3 ? (byte)255 : x < (width * 2 / 3) ? (byte)128 : (byte)0;
                bitmap.SetPixel(x, y, 40, 80, 120, alpha);
            }
        }

        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    /// <summary>画像で使われている色の数を数える。</summary>
    /// <param name="bitmap">対象の画像。</param>
    /// <returns>異なる色の数。</returns>
    private static int CountDistinctColors(EsReadOnlyBitmap bitmap)
    {
        var colors = new HashSet<uint>();
        var pixels = bitmap.ReadOnlyPixels;
        for (var i = 0; i < pixels.Length; i += EsReadOnlyBitmap.BytesPerPixel)
        {
            colors.Add((uint)((pixels[i] << 24) | (pixels[i + 1] << 16) | (pixels[i + 2] << 8) | pixels[i + 3]));
        }

        return colors.Count;
    }

    /// <summary>一時ファイルを消す（存在しない場合は何もしない）。</summary>
    /// <param name="path">消すファイルのパス。</param>
    private static void DeleteFile(string path)
    {
        if (File.Exists(path)) { File.Delete(path); }
    }

    /// <summary>圧縮・減色の効果を観測できる画像を作る（画素ごとに色が不規則に変わる）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="levels">1 チャネルあたりの段階数（既定 256 = 最大）。 段階を減らすと色数が減る。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    private static EsReadOnlyBitmap CreateNoisy(int width, int height, int levels = 256)
    {
        using var bitmap = EsBitmap.Create(width, height);
        var state = 12345u;
        var step = 256 / Math.Clamp(levels, 2, 256);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                state = (state * 1664525u) + 1013904223u;
                bitmap.SetPixel(
                    x,
                    y,
                    (byte)((state >> 24) % step * (256 / step)),
                    (byte)((state >> 16) % step * (256 / step)),
                    (byte)((state >> 8) % step * (256 / step)),
                    255);
            }
        }

        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }
}
