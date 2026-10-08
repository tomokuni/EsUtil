using System.Runtime.CompilerServices;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>
/// <see cref="EsBitmap"/>（読み取り専用の画像）の単体テスト。
/// </summary>
/// <remarks>
/// <see cref="EsBitmap"/> は静的共有状態を持たないため、本テストは UI フレームワークの初期化に依存しない
/// （コレクションに属さない <c>[Fact]</c> として並列実行される）。<br/>
/// ピクセルの書き換えを含む検証は <see cref="EsWritableBitmapTests"/> が担う（本テストは読み取りの面だけを検証する）。
/// </remarks>
public class EsBitmapTests
{
    /// <summary>指定サイズの読み取り専用画像を生成する。</summary>
    private static EsBitmap Create(int w = 4, int h = 4) => EsBitmap.Create(w, h);

    // ====================================================================
    // 生成・基本プロパティ
    // ====================================================================

    [Fact]
    internal void Create_指定サイズで生成する()
    {
        using var bmp = Create(6, 8);
        Assert.Equal(6, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);
        // ストライドは幅×4 で固定される（行パディングなし。 整列は確保の基底のみ）
        Assert.Equal(6 * 4, bmp.Stride);
        Assert.False(bmp.IsDisposed);
    }

    [Fact]
    internal void Create_色付きで指定サイズを生成し全ピクセルが塗りつぶされる()
    {
        // 赤・不透明（BGRA=0,0,255,255）で生成し、全ピクセルが指定色になることを検証する。
        var color = System.Drawing.Color.FromArgb(255, 255, 0, 0); // A=255, R=255, G=0, B=0
        using var bmp = EsBitmap.Create(6, 8, color);

        Assert.Equal(6, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);

        var px = bmp.GetPixel(3, 4);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)255, px.R);
        Assert.Equal((byte)255, px.A);
    }

    [Fact]
    internal void Create_サイズ指定のオーバーロードは寸法をそのまま使う()
    {
        using var bmp = EsBitmap.Create(new EsSizeInt(6, 8));

        Assert.Equal(6, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);
        // サイズはコアの独自型（整数版）で公開される
        Assert.Equal(new EsSizeInt(6, 8), bmp.Size);
    }

    [Fact]
    internal void Create_サイズと色のオーバーロードは全ピクセルを塗りつぶす()
    {
        var color = System.Drawing.Color.FromArgb(255, 0, 0, 255); // 青・不透明
        using var bmp = EsBitmap.Create(new EsSizeInt(4, 4), color);

        var px = bmp.GetPixel(2, 2);
        Assert.Equal((byte)255, px.B);
        Assert.Equal((byte)255, px.A);
    }

    /// <summary>0 サイズ（空）で生成しても例外にならず、ピクセルを持たないことを検証する。</summary>
    [Fact]
    internal void Create_空のサイズなら例外にならない()
    {
        using var bmp = EsBitmap.Create(new EsSizeInt(0, 0));

        Assert.True(bmp.Size.IsEmpty);
        Assert.True(bmp.Pixels.IsEmpty);
    }

    [Fact]
    internal void Create_破棄後はIsDisposedが真になる()
    {
        var bmp = Create();
        bmp.Dispose();
        Assert.True(bmp.IsDisposed);
    }

    // ====================================================================
    // 読み取りの面（IEsBitmap）
    // ====================================================================

    [Fact]
    internal void IEsBitmap_寸法とピクセルを具体型と同じ値で公開する()
    {
        using var bmp = EsBitmap.Create(4, 3, System.Drawing.Color.FromArgb(255, 10, 20, 30));
        IEsBitmap read = bmp;

        Assert.Equal(4, read.Width);
        Assert.Equal(3, read.Height);
        Assert.Equal(new EsSizeInt(4, 3), read.Size);
        Assert.Equal(4 * IEsBitmap.BytesPerPixel, read.Stride);
        Assert.Equal(bmp.GetPixel(1, 1), read.GetPixel(1, 1));
    }

    [Fact]
    internal void Pixels_バッファ全体を指すコピーなしのビューを返す()
    {
        using var src = EsWritableBitmap.Create(4, 3, System.Drawing.Color.FromArgb(255, 3, 2, 1));
        IEsBitmap read = src;

        Assert.Equal(read.Stride * 3, read.Pixels.Length);
        // 先頭ピクセルが BGRA の順で読める
        Assert.Equal((byte)1, read.Pixels[0]);
        Assert.Equal((byte)2, read.Pixels[1]);
        Assert.Equal((byte)3, read.Pixels[2]);
        Assert.Equal((byte)255, read.Pixels[3]);
    }

    [Fact]
    internal void GetPixel_座標型のオーバーロードは色型で返す()
    {
        using var src = EsWritableBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 30, 20, 10));
        IEsBitmap read = src;

        var color = read.GetPixel(new EsPointInt(1, 2));
        Assert.Equal((byte)10, color.B);
        Assert.Equal((byte)20, color.G);
        Assert.Equal((byte)30, color.R);
        Assert.Equal((byte)255, color.A);
    }

    [Fact]
    internal void IEsBitmap_破棄状態は面から判定できる()
    {
        var bmp = EsBitmap.Create(2, 2);
        IEsBitmap read = bmp;

        Assert.False(read.IsDisposed);

        bmp.Dispose();

        // 実体の型へダウンキャストせずに面から判定できる（表示アダプタなど直接実装する画像も同じ手段で調べられる）
        Assert.True(read.IsDisposed);
    }

    [Fact]
    internal void GetPixel_未初期化領域は透明になる()
    {
        using var bmp = Create();
        var px = bmp.GetPixel(0, 0);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)0, px.R);
        Assert.Equal((byte)0, px.A);
    }

    // ====================================================================
    // 複製（読み取り専用の複製）
    // ====================================================================

    [Fact]
    internal void Clone_独立した複製を生成する()
    {
        using var src = EsWritableBitmap.Create(4, 4);
        src.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 255);
        using var clone = src.Clone();

        Assert.Equal((byte)1, clone.GetPixel(0, 0).B);
        Assert.False(clone.IsDisposed);
        Assert.NotSame(src, clone);
    }

    [Fact]
    internal void Clone_アルファを保持したまま複製する()
    {
        using var src = EsWritableBitmap.Create(4, 4);
        src.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 128);
        using var clone = src.Clone();

        var px = clone.GetPixel(0, 0);
        Assert.Equal((byte)1, px.B);
        Assert.Equal((byte)128, px.A);
    }

    [Fact]
    internal void Clone_空のサイズでも例外にならない()
    {
        using var bmp = Create(0, 0);
        using var clone = bmp.Clone();

        Assert.True(clone.Size.IsEmpty);
        Assert.True(clone.Pixels.IsEmpty);
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

    [Fact]
    internal void Pixels_破棄後は例外になる()
    {
        var bmp = Create(4, 4);
        bmp.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { _ = bmp.Pixels.Length; });
    }

    [Fact]
    internal void Dispose_しなくても参照を切ればGCが回収する()
    {
        var weak = CreateUnreachable();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Dispose を忘れてもアンマネージド バッファは解放される（所有者は SafeAlignedBuffer のファイナライザ）
        Assert.False(weak.IsAlive);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference CreateUnreachable() => new(EsBitmap.Create(16, 16));
    }
}
