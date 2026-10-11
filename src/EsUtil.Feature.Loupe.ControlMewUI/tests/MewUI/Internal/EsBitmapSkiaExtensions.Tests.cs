using EsUtil.Feature.Loupe.MewUI.Internal;
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

        using var view = bmp.ToSKBitmap();

        Assert.Equal(6, view.Width);
        Assert.Equal(8, view.Height);
        Assert.Equal(SKColorType.Bgra8888, view.ColorType);
        Assert.Equal(SKAlphaType.Premul, view.AlphaType);
        // ストライドは幅×4（コアの固定値と一致する）
        Assert.Equal(bmp.Stride, view.RowBytes);
    }

    /// <summary>ビューがコアのピクセルを共有していることを検証する（コピーしていない）。</summary>
    /// <remarks>非所有ビューであるため、コア側の書き込みがビューへ即座に反映される。</remarks>
    [Fact]
    internal void ToSKBitmap_はコアのピクセルを共有する()
    {
        using var bmp = EsBitmap.Create(2, 2);
        using var view = bmp.ToSKBitmap();

        bmp.SetPixel(1, 1, b: 10, g: 20, r: 30, a: 255);

        Assert.Equal(new SKColor(30, 20, 10, 255), view.GetPixel(1, 1));
    }

    /// <summary>ビューを破棄してもコアのバッファが解放されないことを検証する。</summary>
    [Fact]
    internal void ToSKBitmap_ビューを破棄してもコアは使用できる()
    {
        using var bmp = EsBitmap.Create(2, 2);
        bmp.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 255);

        using (var view = bmp.ToSKBitmap())
        {
            Assert.Equal(2, view.Width);
        }

        // ビューの破棄後もコアのピクセルは有効（所有権はコアにあるため）
        var (B, G, R, A) = bmp.GetPixel(0, 0);
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

        using var first = bmp.ToSKBitmap();
        using var second = bmp.ToSKBitmap();

        Assert.NotSame(first, second);
    }

    [Fact]
    internal void ToSKBitmap_破棄済みなら例外()
    {
        var bmp = EsBitmap.Create(2, 2);
        bmp.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bmp.ToSKBitmap());
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

        using var view = bmp.ToSKBitmap();
        using var copied = view.Copy().ToEsBitmap();

        var (B, G, R, A) = copied.GetPixel(2, 1);
        Assert.Equal((byte)10, B);
        Assert.Equal((byte)20, G);
        Assert.Equal((byte)30, R);
        Assert.Equal((byte)64, A);
    }
}
