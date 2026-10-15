using EsUtil.Feature.Loupe.WinUI3.Internal;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using Xunit;

namespace EsUtil.Feature.Loupe.WinUI3.Tests.Internal;

/// <summary><see cref="EsBitmapSkiaExtensions"/>（コアの画像と Skia のビットマップの相互変換）のテスト。</summary>
/// <remarks>
/// <b>所有権</b>: 描画のたびに使う経路であるため、ビュー（<see cref="SKBitmap"/>）がコアのバッファを指すだけであり、<br/>
/// 破棄してもコアの画像が壊れないことを検証する。<br/>
/// <b>行幅</b>: 切り出しのビューは元画像のストライドを渡す必要がある（幅×4 を渡すと 2 行目以降がずれる）。<br/>
/// そのため列と行で値が変わる画像を使い、切り出しの先頭と最終行を検証する。
/// </remarks>
public class EsBitmapSkiaExtensionsTests
{
    /// <summary>赤（不透明）。</summary>
    private static readonly SKColor s_red = new(255, 0, 0);

    /// <summary>青（不透明）。</summary>
    private static readonly SKColor s_blue = new(0, 0, 255);

    /// <summary>コアの画像の寸法と画素がビューへ写ることを検証する。</summary>
    [Fact]
    internal void ToSKBitmap_寸法と画素を写す()
    {
        using var source = EsBitmap.Create(4, 3, System.Drawing.Color.FromArgb(255, 255, 0, 0));
        source.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(source);

        using var view = read.ToSKBitmap();

        Assert.Equal(4, view.Width);
        Assert.Equal(3, view.Height);
        Assert.Equal(s_red, view.GetPixel(3, 2));
    }

    /// <summary>ビューを破棄してもコアの画像が壊れないことを検証する（非所有ビュー）。</summary>
    [Fact]
    internal void ToSKBitmap_ビューの破棄はコアの画像を壊さない()
    {
        using var source = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 0, 0, 255));
        source.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(source);

        var view = read.ToSKBitmap();
        view.Dispose();

        // コアの画像はそのまま読める（ビューはバッファを所有しない）
        Assert.Equal("4x4", read.ToSizeText());
        Assert.Equal(4 * 4 * EsReadOnlyBitmap.BytesPerPixel, read.ReadOnlyPixels.Length);
    }

    /// <summary>切り出しのビューが元画像の同じ位置の画素を写すことを検証する。</summary>
    /// <remarks>
    /// 切り出しの幅が元画像の幅と異なるため、行幅（元画像のストライド）が正しく渡っていないと<br/>
    /// 2 行目以降の画素がずれる。 先頭行と最終行の両方を検証する。
    /// </remarks>
    [Fact]
    internal void ToSKBitmap_切り出しの画素を元画像の位置どおりに写す()
    {
        using var source = EsBitmap.Create(8, 8);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                source.SetPixel(x, y, b: (byte)x, g: (byte)y, r: 0, a: 255);
            }
        }

        source.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(source);

        // 切り出し (1, 2) から 4 × 4
        using var pixmap = read.ToPixmap(new EsRectInt(1, 2, 4, 4));
        using var view = pixmap.ToSKBitmap();

        Assert.Equal(4, view.Width);
        Assert.Equal(4, view.Height);
        // 先頭画素は元画像の (1, 2)
        Assert.Equal(new SKColor(0, 2, 1, 255), view.GetPixel(0, 0));
        // 最終行の先頭画素は元画像の (1, 5)（行幅が誤っているとここがずれる）
        Assert.Equal(new SKColor(0, 5, 1, 255), view.GetPixel(0, 3));
    }

    /// <summary>破棄済みの画像は例外になることを検証する。</summary>
    [Fact]
    internal void ToSKBitmap_破棄済みの画像は例外()
    {
        var source = EsBitmap.Create(4, 4);
        source.SetReadOnly();
        var read = EsReadOnlyBitmap.FromBitmap(source);
        source.Dispose();
        read.Dispose();

        Assert.Throws<ObjectDisposedException>(() => read.ToSKBitmap());
    }

    /// <summary>Skia のビットマップが複製されてコアの画像になることを検証する。</summary>
    /// <remarks>引数の所有権は呼び出し側に残る（複製後に引数を書き換えても結果は変わらない）。</remarks>
    [Fact]
    internal void ToEsBitmap_複製を作る()
    {
        using var bitmap = new SKBitmap(4, 3);
        bitmap.Erase(s_blue);

        using var created = bitmap.ToEsBitmap();

        // 引数を書き換えても複製は影響を受けない
        bitmap.Erase(s_red);

        Assert.Equal(4, created.Width);
        Assert.Equal(3, created.Height);
        // 画素は (B, G, R, A) の順で取得できる（青は (255, 0, 0, 255)）
        Assert.Equal((255, 0, 0, 255), created.GetPixel(0, 0));
        Assert.Equal((255, 0, 0, 255), created.GetPixel(3, 2));
    }

    /// <summary>引数が null なら例外になることを検証する。</summary>
    [Fact]
    internal void 引数がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => EsBitmapSkiaExtensions.ToSKBitmap((EsReadOnlyBitmap)null!));
        Assert.Throws<ArgumentNullException>(() => EsBitmapSkiaExtensions.ToSKBitmap((EsPixmap)null!));
        Assert.Throws<ArgumentNullException>(() => EsBitmapSkiaExtensions.ToEsBitmap(null!));
    }
}
