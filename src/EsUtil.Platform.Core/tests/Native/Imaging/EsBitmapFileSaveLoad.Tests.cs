using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native.Imaging;

/// <summary>
/// 画像のファイル保存・読み込み（<see cref="EsBitmapBase.Save(string)"/> / <see cref="EsBitmap.Load(string)"/> / <see cref="EsWritableBitmap.Load(string)"/>）のテスト（Windows 専用・並列実行）。
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
}
