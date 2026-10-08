using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>
/// <see cref="EsWritableBitmap"/>（書き換え可能な画像）の単体テスト。
/// </summary>
/// <remarks>
/// <see cref="EsWritableBitmap"/> は静的共有状態を持たないため、本テストは UI フレームワークの初期化に依存しない
/// （コレクションに属さない <c>[Fact]</c> として並列実行される）。<br/>
/// 読み取りのみの検証は <see cref="EsBitmapTests"/> が担う（本テストは書き換えの面を検証する）。
/// </remarks>
public class EsWritableBitmapTests
{
    /// <summary>指定サイズの書き換え可能な画像を生成する。</summary>
    private static EsWritableBitmap Create(int w = 4, int h = 4) => EsWritableBitmap.Create(w, h);

    // ====================================================================
    // 生成・基本プロパティ
    // ====================================================================

    [Fact]
    internal void Create_指定サイズで生成する()
    {
        using var bmp = Create(6, 8);
        Assert.Equal(6, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);
        Assert.Equal(6 * 4, bmp.Stride);
        Assert.False(bmp.IsDisposed);
    }

    [Fact]
    internal void Create_色付きで全ピクセルを塗りつぶす()
    {
        using var bmp = EsWritableBitmap.Create(6, 8, System.Drawing.Color.FromArgb(255, 255, 0, 0));

        var (B, G, R, A) = bmp.GetPixel(3, 4);
        Assert.Equal((byte)0, B);
        Assert.Equal((byte)0, G);
        Assert.Equal((byte)255, R);
        Assert.Equal((byte)255, A);
    }

    /// <summary>未初期化確保はサイズとバッファを用意する（ピクセルの値は保証しない）ことを検証する。</summary>
    [Fact]
    internal void CreateUninitialized_サイズとバッファを用意する()
    {
        using var bmp = EsWritableBitmap.CreateUninitialized(6, 8);

        Assert.Equal(6, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);
        Assert.Equal(6 * 4, bmp.Stride);
        Assert.Equal(bmp.Stride * bmp.Height, bmp.WritablePixels.Length);
        Assert.False(bmp.IsDisposed);
    }

    [Fact]
    internal void CreateUninitialized_サイズ指定のオーバーロードは寸法をそのまま使う()
    {
        using var bmp = EsWritableBitmap.CreateUninitialized(new EsSizeInt(3, 5));

        Assert.Equal(new EsSizeInt(3, 5), bmp.Size);
    }

    [Fact]
    internal void CreateUninitialized_空のサイズなら例外にならない()
    {
        using var bmp = EsWritableBitmap.CreateUninitialized(0, 0);

        Assert.True(bmp.Size.IsEmpty);
        Assert.True(bmp.WritablePixels.IsEmpty);
    }

    [Fact]
    internal void Create_空のサイズでもClearは例外にならない()
    {
        using var bmp = EsWritableBitmap.Create(new EsSizeInt(0, 0));

        Assert.True(bmp.Size.IsEmpty);
        bmp.Clear(System.Drawing.Color.Red);
    }

    // ====================================================================
    // 書き換えの面（IEsWritableBitmap）
    // ====================================================================

    [Fact]
    internal void SetPixel_設定したピクセルを取得できる()
    {
        using var bmp = Create();
        bmp.SetPixel(1, 2, b: 10, g: 20, r: 30, a: 255);
        var (B, G, R, A) = bmp.GetPixel(1, 2);
        Assert.Equal((byte)10, B);
        Assert.Equal((byte)20, G);
        Assert.Equal((byte)30, R);
        Assert.Equal((byte)255, A);
    }

    /// <summary>座標型（<see cref="EsPointInt" />）のオーバーロードが成分オーバーロードと一致することを検証する。</summary>
    [Fact]
    internal void SetPixel_座標型のオーバーロードは成分と一致する()
    {
        using var bmp = Create();
        bmp.SetPixel(new EsPointInt(1, 2), System.Drawing.Color.FromArgb(255, 30, 20, 10));

        var (b, g, r, a) = bmp.GetPixel(1, 2);
        Assert.Equal((byte)10, b);
        Assert.Equal((byte)20, g);
        Assert.Equal((byte)30, r);
        Assert.Equal((byte)255, a);
    }

    [Fact]
    internal void TrySetPixel_範囲外はfalseを返し例外にしない()
    {
        using var bmp = Create(4, 4);
        var color = System.Drawing.Color.Red;

        Assert.True(bmp.TrySetPixel(3, 3, color));
        Assert.False(bmp.TrySetPixel(4, 3, color));
        Assert.False(bmp.TrySetPixel(3, 4, color));
        Assert.False(bmp.TrySetPixel(-1, 0, color));
        Assert.False(bmp.TrySetPixel(0, -1, color));
    }

    [Fact]
    internal void Clear_全ピクセルが指定色になる()
    {
        using var bmp = Create();
        bmp.Clear(b: 0, g: 0, r: 255, a: 255); // 赤・不透明

        var (B, G, R, A) = bmp.GetPixel(3, 3);
        Assert.Equal((byte)0, B);
        Assert.Equal((byte)0, G);
        Assert.Equal((byte)255, R);
        Assert.Equal((byte)255, A);
    }

    [Fact]
    internal void Clear_アルファ乗算済みの色で塗りつぶす()
    {
        using var bmp = Create();
        // 50% アルファの赤（R=255, A=128）
        bmp.Clear(b: 0, g: 0, r: 255, a: 128);
        var (B, G, R, A) = bmp.GetPixel(0, 0);
        Assert.Equal((byte)128, A);
    }

    [Fact]
    internal void Clear_色型のオーバーロードは成分と一致する()
    {
        using var bmp = Create();
        bmp.Clear(System.Drawing.Color.FromArgb(255, 3, 2, 1));

        var (B, G, R, A) = bmp.GetPixel(0, 0);
        Assert.Equal((byte)1, B);
        Assert.Equal((byte)2, G);
        Assert.Equal((byte)3, R);
        Assert.Equal((byte)255, A);
    }

    [Fact]
    internal void WritablePixels_への書き込みがPixelsとGetPixelに見える()
    {
        using var bmp = Create(4, 3);
        var offset = bmp.Stride * 2 + 3 * IEsBitmap.BytesPerPixel;

        bmp.WritablePixels[offset] = 7;

        // 書き込みの面と読み取りの面が同じバッファを指す
        Assert.Equal((byte)7, bmp.Pixels[offset]);
        Assert.Equal((byte)7, bmp.GetPixel(3, 2).B);
    }

    [Fact]
    internal void WritablePixels_破棄後は例外になる()
    {
        var bmp = Create(4, 4);
        bmp.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { _ = bmp.WritablePixels.Length; });
    }

    // ====================================================================
    // アルファ喪失の防止（BGRA8888・Premul に統一し、alpha=0 を強制しない）
    // ====================================================================

    [Fact]
    internal void SetPixel_部分透明アルファを保持する()
    {
        using var bmp = Create();
        // 50% アルファの青（BGRA: B=255, G=0, R=0, A=128）
        bmp.SetPixel(2, 3, b: 255, g: 0, r: 0, a: 128);
        var (B, G, R, A) = bmp.GetPixel(2, 3);
        Assert.Equal((byte)255, B);
        Assert.Equal((byte)0, G);
        Assert.Equal((byte)0, R);
        Assert.Equal((byte)128, A); // アルファが喪失（0 に強制）されない
    }

    [Fact]
    internal void SetPixel_完全透明アルファも保持する()
    {
        using var bmp = Create();
        bmp.SetPixel(0, 0, b: 10, g: 20, r: 30, a: 0);
        var (B, G, R, A) = bmp.GetPixel(0, 0);
        Assert.Equal((byte)0, A);
        Assert.Equal((byte)10, B);
        Assert.Equal((byte)20, G);
        Assert.Equal((byte)30, R);
    }

    // ====================================================================
    // 複製（書き換え可能な複製）
    // ====================================================================

    [Fact]
    internal void Clone_独立した複製を生成する()
    {
        using var src = Create();
        src.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 255);
        using var clone = src.Clone();

        clone.SetPixel(0, 0, b: 9, g: 8, r: 7, a: 255);

        // 複製先の書き換えが複製元へ波及しない
        Assert.Equal((byte)1, src.GetPixel(0, 0).B);
        Assert.Equal((byte)9, clone.GetPixel(0, 0).B);
    }

    [Fact]
    internal void Create_読み取り専用の画像から書き換え可能な複製を作る()
    {
        using var source = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 3, 2, 1));
        using var copy = EsWritableBitmap.Create(source);

        Assert.Equal(4, copy.Width);
        Assert.Equal(4, copy.Height);
        Assert.Equal((byte)1, copy.GetPixel(0, 0).B);

        // 複製先を書き換えても複製元は変わらない（バッファを共有しない）
        copy.SetPixel(0, 0, b: 9, g: 9, r: 9, a: 9);
        Assert.Equal((byte)1, source.GetPixel(0, 0).B);
    }

    [Fact]
    internal void Create_nullはArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => EsWritableBitmap.Create(null!));

    [Fact]
    internal void Create_破棄済みの元は例外を投げる()
    {
        var source = EsBitmap.Create(4, 4);
        source.Dispose();

        Assert.Throws<ObjectDisposedException>(() => EsWritableBitmap.Create(source));
    }

    // ====================================================================
    // 解放
    // ====================================================================

    [Fact]
    internal void Dispose_二重破棄しても例外にならない()
    {
        var bmp = Create();
        bmp.Dispose();
        bmp.Dispose();
        Assert.True(bmp.IsDisposed);
    }
}
