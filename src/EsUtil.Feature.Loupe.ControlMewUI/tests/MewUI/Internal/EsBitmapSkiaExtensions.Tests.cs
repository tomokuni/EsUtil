using EsUtil.Feature.Loupe.MewUI.Internal;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using Xunit;

namespace EsUtil.Feature.Loupe.MewUI.Tests.Internal;

/// <summary><see cref="EsBitmapSkiaExtensions"/> の単体テスト（コアのビットマップと Skia の相互変換）。</summary>
/// <remarks>
/// SkiaSharp に依存するため本テスト プロジェクト（<c>EsUtil.Feature.Loupe.MewUI.Tests</c>）が担う。<br/>
/// UI フレームワークの初期化に依存しないため、<c>[Fact]</c> として並列実行される。
/// </remarks>
public class EsBitmapSkiaExtensionsTests
{
    [Fact]
    internal void ToSKBitmap_同じピクセルを指す非所有ビューを返す()
    {
        using var bmp = EsBitmap.Create(6, 8);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);

        using var view = read.ToSKBitmap();

        Assert.Equal(6, view.Width);
        Assert.Equal(8, view.Height);
        Assert.Equal(SKColorType.Bgra8888, view.ColorType);
        Assert.Equal(SKAlphaType.Premul, view.AlphaType);
        // ストライドは幅×4（コアの固定値と一致する）
        Assert.Equal(read.Stride, view.RowBytes);
    }

    /// <summary>ビューがコアのピクセルを共有していることを検証する（コピーしていない）。</summary>
    /// <remarks>非所有ビューであるため、ビューの画素領域はコアの画素領域と同一のアドレスになる。</remarks>
    [Fact]
    internal void ToSKBitmap_はコアのピクセルを共有する()
    {
        using var bmp = EsBitmap.Create(2, 2);
        bmp.SetPixel(1, 1, b: 10, g: 20, r: 30, a: 255);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var view = read.ToSKBitmap();

        unsafe
        {
            // ビューはコアの画素領域を直接指す（複製しない）
            fixed (byte* pixels = read.ReadOnlyPixels)
            {
                Assert.Equal((nint)pixels, view.GetPixels());
            }
        }

        Assert.Equal(new SKColor(30, 20, 10, 255), view.GetPixel(1, 1));
    }

    /// <summary>ビューを破棄してもコアのバッファが解放されないことを検証する。</summary>
    [Fact]
    internal void ToSKBitmap_ビューを破棄してもコアは使用できる()
    {
        using var bmp = EsBitmap.Create(2, 2);
        bmp.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 255);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);

        using (var view = read.ToSKBitmap())
        {
            Assert.Equal(2, view.Width);
        }

        // ビューの破棄後もコアのピクセルは有効（所有権はコアにあるため）
        var (B, G, R, A) = read.GetPixel(0, 0);
        Assert.Equal((byte)1, B);
        Assert.Equal((byte)3, R);
    }

    /// <summary>呼び出しごとに新しいビューを返すことを検証する（呼び出し側が破棄する契約）。</summary>
    /// <remarks>
    /// ビューは <see cref="SKBitmap"/> のインスタンスでありファイナライザを持つため、
    /// 呼び出し側が破棄しないと毎回ファイナライザ キューへ積まれる（描画のたびに作る用途では必須）。<br/>
    /// 内部で使い回していると本契約が崩れるため、同一インスタンスでないことを固定する。
    /// </remarks>
    [Fact]
    internal void ToSKBitmap_呼び出しごとに新しいビューを返す()
    {
        using var bmp = EsBitmap.Create(2, 2);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);

        using var first = read.ToSKBitmap();
        using var second = read.ToSKBitmap();

        Assert.NotSame(first, second);
    }

    [Fact]
    internal void ToSKBitmap_破棄済みなら例外()
    {
        var bmp = EsBitmap.Create(2, 2);
        bmp.SetReadOnly();
        var read = EsReadOnlyBitmap.FromBitmap(bmp);
        // 画素は読み取り専用の面が共有して引き継ぐため、書き換え可能の実体はここで解放する
        bmp.Dispose();

        read.Dispose();

        Assert.Throws<ObjectDisposedException>(() => read.ToSKBitmap());
    }

    [Fact]
    internal void ToEsBitmap_複製を生成し引数の所有権は残る()
    {
        using var sk = new SKBitmap(new SKImageInfo(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul));

        using var bmp = sk.ToEsBitmap();

        Assert.Equal(4, bmp.Size.Width);
        Assert.Equal(4, bmp.Size.Height);
        // 引数は破棄されていない（所有権は呼び出し側に残る）
        Assert.False(sk.IsNull);
        Assert.Equal(4, sk.Width);
    }

    [Fact]
    internal void ToEsBitmap_部分透明アルファを保持する()
    {
        using var sk = new SKBitmap(new SKImageInfo(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul));
        unsafe
        {
            var ptr = (byte*)sk.GetPixels();
            ptr[0] = 255; // B
            ptr[1] = 0;   // G
            ptr[2] = 0;   // R
            ptr[3] = 128; // A
        }

        using var bmp = sk.ToEsBitmap();

        var (B, G, R, A) = bmp.GetPixel(0, 0);
        Assert.Equal((byte)255, B);
        Assert.Equal((byte)0, G);
        Assert.Equal((byte)0, R);
        Assert.Equal((byte)128, A);
    }

    /// <summary>画素を持たない（未確保の）ビットマップを変換しても例外にならず、透明を返すことを検証する。</summary>
    [Fact]
    internal void ToEsBitmap_画素を持たないビットマップは透明を返す()
    {
        using var sk = new SKBitmap();

        using var bmp = sk.ToEsBitmap();

        Assert.True(bmp.Size.IsEmpty);
        Assert.True(bmp.ReadOnlyPixels.IsEmpty);
    }

    /// <summary>コアと Skia の間で往復してもピクセルが保たれることを検証する。</summary>
    /// <remarks>Avalonia ヘッドレス初期化に依存せず、アルファ付きピクセルが往復できることを固定する。</remarks>
    [Fact]
    internal void 往復でアルファ付きピクセルを保持する()
    {
        using var bmp = EsBitmap.Create(3, 3);
        bmp.SetPixel(2, 1, b: 10, g: 20, r: 30, a: 64);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);

        using var view = read.ToSKBitmap();
        using var copied = view.Copy().ToEsBitmap();

        var (B, G, R, A) = copied.GetPixel(2, 1);
        Assert.Equal((byte)10, B);
        Assert.Equal((byte)20, G);
        Assert.Equal((byte)30, R);
        Assert.Equal((byte)64, A);
    }

    // ====================================================================
    // EsPixmap（一部を指す 0 コピーのビュー）→ SKBitmap
    // ====================================================================

    /// <summary>座標ごとに色が変わる読み取り専用の画像を生成する（位置の取り違えを検出するため）。</summary>
    /// <param name="w">幅（ピクセル）。</param>
    /// <param name="h">高さ（ピクセル）。</param>
    /// <returns>生成した画像。 破棄は呼び出し側。</returns>
    private static EsReadOnlyBitmap CreateGradient(int w = 8, int h = 6)
    {
        using var bitmap = EsBitmap.Create(w, h);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                bitmap.SetPixel(x, y, b: (byte)x, g: (byte)y, r: (byte)(x + y), a: 255);
            }
        }

        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    /// <summary>ビューの画素が元画像の同じ位置と一致することを検証する（位置関係を保つ）。</summary>
    /// <remarks>
    /// 行幅を元画像のストライドのまま渡しているため、幅が元画像より狭いビューでも行がずれない。
    /// ビューの幅を「元画像の幅」にすると行ずれが起きないため、<b>幅を狭くして</b>検証する。
    /// </remarks>
    [Fact]
    internal void ToSKBitmap_ビューの画素は元画像の同じ位置と一致する()
    {
        using var source = CreateGradient(8, 6);

        using var view = source.ToPixmap(new EsRectInt(2, 1, 3, 2));
        using var skia = view.ToSKBitmap();

        for (var y = 0; y < view.Height; y++)
        {
            for (var x = 0; x < view.Width; x++)
            {
                var (b, g, r, a) = source.GetPixel(2 + x, 1 + y);
                Assert.Equal(new SKColor(r, g, b, a), skia.GetPixel(x, y));
            }
        }
    }

    /// <summary>ビューの寸法と行幅（元画像のストライド）が SKBitmap へ引き継がれることを検証する。</summary>
    [Fact]
    internal void ToSKBitmap_寸法と行幅を引き継ぐ()
    {
        using var source = CreateGradient(8, 6);

        using var view = source.ToPixmap(new EsRectInt(1, 2, 4, 3));
        using var skia = view.ToSKBitmap();

        Assert.Equal(4, skia.Width);
        Assert.Equal(3, skia.Height);
        Assert.Equal(SKColorType.Bgra8888, skia.ColorType);
        Assert.Equal(SKAlphaType.Premul, skia.AlphaType);

        // 行幅は元画像のストライドのまま（幅×4 ではない。 行の余白を含む連続ビューを渡すため）
        Assert.Equal(source.Stride, view.Stride);
        Assert.Equal(source.Stride, skia.RowBytes);
        Assert.True(skia.RowBytes > view.Width * EsPixmap.BytesPerPixel, "行幅が幅×4 と等しいと行の余白を検証できない");
    }

    /// <summary>SKBitmap（ビュー）を破棄してもコアの画素が解放されないことを検証する。</summary>
    [Fact]
    internal void ToSKBitmap_ビューを破棄してもコアの画素は解放されない()
    {
        using var source = CreateGradient(8, 6);
        var view = source.ToPixmap(new EsRectInt(0, 0, 4, 4));

        using (var skia = view.ToSKBitmap())
        {
            Assert.Equal(4, skia.Width);
        }

        // SKBitmap の破棄後もコアの画素は有効（所有権はコアにあるため）
        var (b, g, r, a) = view.GetPixel(3, 2);
        Assert.Equal((byte)3, b);
        Assert.Equal((byte)2, g);
        Assert.Equal((byte)5, r);
        Assert.Equal((byte)255, a);

        var original = source.GetPixel(3, 2);
        Assert.Equal(original, (b, g, r, a));

        view.Dispose();
    }

    /// <summary>破棄済みのビューからの変換が例外になることを検証する。</summary>
    [Fact]
    internal void ToSKBitmap_破棄済みのビューは例外()
    {
        using var source = CreateGradient(8, 6);
        var view = source.ToPixmap(new EsRectInt(0, 0, 4, 4));
        view.Dispose();

        Assert.Throws<ObjectDisposedException>(() => view.ToSKBitmap());
    }
}
