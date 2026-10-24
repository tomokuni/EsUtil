using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>減色（<see cref="EsReadOnlyBitmap.ReduceColors(int, EsDitherMode)"/> ほか）のテスト（Windows 専用・並列実行）。</summary>
/// <remarks>
/// <b>計算は WIC が単一所有します</b>（本ライブラリは減色の計算を持ちません）。 そのため本テストは
/// <b>「色数へ減らす」という契約</b>だけを検証します（パレットの作り方や距離の測り方は WIC の実装であり、
/// 本ライブラリの仕様ではありません）。<br/>
/// WIC は Windows にのみ存在するため、本クラスは <see cref="SupportedOSPlatformAttribute"/> で Windows に限定します。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public class EsBitmapReduceColorsTests
{
    /// <summary>一時ファイルのパスを作る（拡張子は呼び出し側が指定する）。</summary>
    /// <param name="extension">拡張子（先頭のドットを含む）。</param>
    /// <returns>一時フォルダー内の一意なパス。</returns>
    private static string TempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"se_reduce_{Guid.NewGuid():N}{extension}");

    /// <summary>グラデーションとノイズを持つ読み取り専用の画像を作る（減色の対象）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。 破棄は呼び出し側。</returns>
    private static EsReadOnlyBitmap CreateGradient(int width = 64, int height = 48)
    {
        var random = new Random(12345);
        using var bitmap = EsBitmap.CreateFilled(width, height, span =>
        {
            for (var i = 0; i < span.Length; i += 4)
            {
                var pixel = i / 4;
                var x = pixel % width;
                var y = pixel / width;
                var noise = random.Next(-6, 7);
                span[i + 0] = (byte)Math.Clamp((x * 255 / width) + noise, 0, 255);
                span[i + 1] = (byte)Math.Clamp((y * 255 / height) + noise, 0, 255);
                span[i + 2] = (byte)Math.Clamp(((x + y) * 255 / (width + height)) + noise, 0, 255);
                span[i + 3] = 255;
            }
        });

        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    /// <summary>画像で使われている色の数を数える。</summary>
    /// <param name="bitmap">対象の画像。</param>
    /// <returns>異なる色の数。</returns>
    private static int CountDistinctColors(EsBitmap bitmap)
    {
        var colors = new HashSet<uint>();
        var pixels = bitmap.ReadOnlyPixels;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            colors.Add(BitConverter.ToUInt32(pixels[i..]));
        }

        return colors.Count;
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(256)]
    internal void ReduceColors_指定した色数以下になる(int colorCount)
    {
        using var source = CreateGradient();
        using var reduced = source.ReduceColors(colorCount);

        Assert.True(
            CountDistinctColors(reduced) <= colorCount,
            $"{CountDistinctColors(reduced)} 色になりました（指定は {colorCount} 色）。");
    }

    [Fact]
    internal void ReduceColors_寸法と画素数を変えない()
    {
        using var source = CreateGradient(40, 24);
        using var reduced = source.ReduceColors(8);

        Assert.Equal(source.Size, reduced.Size);
        Assert.Equal(source.ReadOnlyPixels.Length, reduced.ReadOnlyPixels.Length);
    }

    [Fact]
    internal void ReduceColors_元の画像を変更しない()
    {
        using var source = CreateGradient(32, 32);
        var before = source.ReadOnlyPixels.ToArray();

        using var reduced = source.ReduceColors(4);

        Assert.Equal(before, source.ReadOnlyPixels.ToArray());
        Assert.NotEqual(before, reduced.ReadOnlyPixels.ToArray());
    }

    [Fact]
    internal void ReduceColors_完全透明は透明のまま残る()
    {
        // パレットは透明色を 1 つ持てる（WIC のパレットへ透明色の追加を指示する）
        using var bitmap = EsBitmap.CreateFilled(16, 8, span =>
        {
            for (var i = 0; i < span.Length; i += 4)
            {
                var opaque = (i / 4) % 2 == 0;
                span[i + 0] = 0;
                span[i + 1] = 0;
                span[i + 2] = opaque ? (byte)200 : (byte)0;
                span[i + 3] = opaque ? (byte)255 : (byte)0;
            }
        });
        bitmap.SetReadOnly();
        using var source = EsReadOnlyBitmap.FromBitmap(bitmap);

        using var reduced = source.ReduceColors(8);

        Assert.Equal((byte)255, reduced.GetPixel(0, 0).A);
        Assert.Equal((byte)0, reduced.GetPixel(1, 0).A);
    }

    [Theory]
    [InlineData(EsDitherMode.None)]
    [InlineData(EsDitherMode.ErrorDiffusion)]
    internal void ReduceColors_ディザを指定しても色数は指定以下になる(EsDitherMode dither)
    {
        using var source = CreateGradient();
        using var reduced = source.ReduceColors(8, dither);

        Assert.True(CountDistinctColors(reduced) <= 8);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(257)]
    internal void ReduceColors_色数が範囲外ならArgumentOutOfRangeExceptionを投げる(int colorCount)
    {
        using var source = CreateGradient(8, 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => source.ReduceColors(colorCount));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    internal void ReduceColors_定義されていないディザはArgumentOutOfRangeExceptionを投げる(int dither)
    {
        using var source = CreateGradient(8, 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => source.ReduceColors(4, (EsDitherMode)dither));
    }

    [Fact]
    internal void ReduceColors_行の余白の画素を混ぜない()
    {
        // ビューの画素は元画像の行幅を保つ（余白を含む）。 余白の画素を数えると色数が増える
        using var source = CreateGradient(24, 16);
        using var view = source.ToPixmap(new EsRectInt(0, 0, 17, 16));

        using var reduced = view.ReduceColors(4);

        Assert.True(CountDistinctColors(reduced) <= 4, $"{CountDistinctColors(reduced)} 色になりました。");
    }

    [Fact]
    internal void ReduceColors_保存の減色と同じ結果になる()
    {
        // 保存の ColorCount は同じパレットを使う（2 か所に計算を書かないことの検証）
        using var source = CreateGradient(32, 24);
        using var reduced = source.ReduceColors(16);

        var path = TempPath(".png");
        try
        {
            source.Save(path, new EsImageSaveOptions { ColorCount = 16 });

            using var saved = EsReadOnlyBitmap.Load(path);
            Assert.Equal(reduced.ReadOnlyPixels.ToArray(), saved.ReadOnlyPixels.ToArray());
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }
}
