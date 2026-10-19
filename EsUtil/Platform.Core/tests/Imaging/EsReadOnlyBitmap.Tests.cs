using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary><see cref="EsReadOnlyBitmap"/>（読み取り専用の画像）の単体テスト。</summary>
/// <remarks>
/// <see cref="EsReadOnlyBitmap"/> は静的共有状態を持たないため、本テストは UI フレームワークの初期化に依存しない
/// （コレクションに属さない <c>[Fact]</c> として並列実行される）。<br/>
/// <b>書き込みの入口が無いことはコンパイル時に決まる</b>ため本テストでは検証できない（書こうとした時点でビルドが失敗する）。
/// 本テストは読み取りの面と、読み取り専用の画像を作る入口（作成・複製・借用・充填）を検証する。
/// 生成の入口は書き換え可能な型と対であるため、同じ引数で両型を作って画素が一致することも検証する。
/// 書き換え可能な画像からの生成（<see cref="EsReadOnlyBitmap.FromBitmap"/>）は <see cref="EsBitmapTests"/> が担う。
/// </remarks>
public class EsReadOnlyBitmapTests
{
    /// <summary>指定サイズの読み取り専用画像（透明）を生成する。</summary>
    private static EsReadOnlyBitmap Create(int w = 4, int h = 4)
        => EsReadOnlyBitmap.FromPixelCopy(w, h, new byte[w * EsReadOnlyBitmap.BytesPerPixel * h]);

    /// <summary>2 つの画像が同じ画素領域を指すかどうかを返す（コピーされていないことの判定に使う）。</summary>
    private static bool SameStorage(EsReadOnlyBitmap a, EsReadOnlyBitmap b)
        => a.ReadOnlyPixels.Length > 0
            && b.ReadOnlyPixels.Length > 0
            && Unsafe.AreSame(
                ref MemoryMarshal.GetReference(a.ReadOnlyPixels),
                ref MemoryMarshal.GetReference(b.ReadOnlyPixels));

    /// <summary>ビューが元画像の指定位置を指しているかどうかを返す（コピーされていないことの判定に使う）。</summary>
    /// <param name="view">判定するビュー。</param>
    /// <param name="source">元の画像。</param>
    /// <param name="x">ビューの左上が対応する元画像の X 座標。</param>
    /// <param name="y">ビューの左上が対応する元画像の Y 座標。</param>
    /// <returns>ビューの先頭が元画像の <paramref name="x"/>, <paramref name="y"/> と同じアドレスなら true。</returns>
    private static bool StartsAtStorage(EsPixmap view, EsReadOnlyBitmap source, int x, int y)
    {
        var offset = (y * source.Stride) + (x * EsReadOnlyBitmap.BytesPerPixel);
        return Unsafe.AreSame(
            ref MemoryMarshal.GetReference(view.ReadOnlyPixels),
            ref MemoryMarshal.GetReference(source.ReadOnlyPixels[offset..]));
    }

    // ====================================================================
    // 読み取りの面
    // ====================================================================

    [Fact]
    internal void 寸法とストライドを公開する()
    {
        using var bmp = Create(6, 8);

        Assert.Equal(6, bmp.Width);
        Assert.Equal(8, bmp.Height);
        Assert.Equal(new EsSizeInt(6, 8), bmp.Size);
        // ストライドは幅×4 で固定される（行パディングなし。 整列は確保の基底のみ）
        Assert.Equal(6 * 4, bmp.Stride);
        Assert.False(bmp.IsDisposed);
        Assert.True(bmp.IsReadOnly);
    }

    [Fact]
    internal void ReadOnlyPixels_バッファ全体を指すコピーなしのビューを返す()
    {
        using var src = EsBitmap.Create(4, 3, System.Drawing.Color.FromArgb(255, 3, 2, 1));
        src.SetReadOnly();

        // 凍結済みなので FromBitmap は 0 コピーで共有する
        using var read = EsReadOnlyBitmap.FromBitmap(src);

        Assert.Equal(read.Stride * 3, read.ReadOnlyPixels.Length);
        // 先頭ピクセルが BGRA の順で読める
        Assert.Equal((byte)1, read.ReadOnlyPixels[0]);
        Assert.Equal((byte)2, read.ReadOnlyPixels[1]);
        Assert.Equal((byte)3, read.ReadOnlyPixels[2]);
        Assert.Equal((byte)255, read.ReadOnlyPixels[3]);
    }

    [Fact]
    internal void GetPixel_成分と色の両方の形で返す()
    {
        using var src = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 30, 20, 10));
        src.SetReadOnly();

        // 凍結済みなので FromBitmap は 0 コピーで共有する
        using var read = EsReadOnlyBitmap.FromBitmap(src);

        var (B, G, R, A) = read.GetPixel(1, 2);
        Assert.Equal((byte)10, B);
        Assert.Equal((byte)20, G);
        Assert.Equal((byte)30, R);
        Assert.Equal((byte)255, A);

        var color = read.GetPixel(new EsPointInt(1, 2));
        Assert.Equal((byte)10, color.B);
        Assert.Equal((byte)30, color.R);
        Assert.Equal((byte)255, color.A);
    }

    [Fact]
    internal void GetPixel_範囲外はArgumentOutOfRangeExceptionを投げる()
    {
        using var bmp = Create(2, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => bmp.GetPixel(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => bmp.GetPixel(-1, 0));
    }

    [Fact]
    internal void ToSizeText_幅x高さ形式で返す()
    {
        using var bmp = Create(1920, 1080);
        Assert.Equal("1920x1080", bmp.ToSizeText());
    }

    [Fact]
    internal void ToSizeText_0サイズでも形式は保つ()
    {
        using var bmp = Create(0, 0);
        Assert.Equal("0x0", bmp.ToSizeText());
    }

    // ====================================================================
    // 生成（作成・複製・借用・充填）
    // ====================================================================

    /// <summary>同じ引数で両型の画像を生成し、全ピクセルが一致することを確かめる（入口の対称性の検証）。</summary>
    /// <param name="read">読み取り専用の画像。</param>
    /// <param name="writable">書き換え可能な画像。</param>
    private static void AssertSamePixels(EsReadOnlyBitmap read, EsBitmap writable)
    {
        Assert.Equal(writable.Size, read.Size);
        Assert.Equal(writable.Stride, read.Stride);

        for (var y = 0; y < read.Height; y++)
        {
            for (var x = 0; x < read.Width; x++)
            {
                Assert.Equal(writable.GetPixel(x, y), read.GetPixel(x, y));
            }
        }
    }

    [Fact]
    internal void Create_指定サイズの透明な画像を生成する_書き換え可能な側と同じ値になる()
    {
        using var read = EsReadOnlyBitmap.Create(6, 8);
        using var writable = EsBitmap.Create(6, 8);

        // 書き込みの入口が無い型でも、書き換え可能な型と同じ引数で同じ画素が得られる（入口の対称性）
        Assert.Equal(new EsSizeInt(6, 8), read.Size);
        Assert.Equal(6 * 4, read.Stride);
        Assert.True(read.IsReadOnly);
        AssertSamePixels(read, writable);

        // 全ピクセル透明
        Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)0), read.GetPixel(5, 7));
    }

    [Fact]
    internal void Create_サイズ指定のオーバーロードは寸法をそのまま使う()
    {
        using var read = EsReadOnlyBitmap.Create(new EsSizeInt(3, 5));

        Assert.Equal(new EsSizeInt(3, 5), read.Size);
    }

    [Fact]
    internal void Create_色付きで全ピクセルを塗りつぶす_書き換え可能な側と同じ値になる()
    {
        var color = System.Drawing.Color.FromArgb(255, 255, 0, 0);

        using var read = EsReadOnlyBitmap.Create(6, 8, color);
        using var writable = EsBitmap.Create(6, 8, color);

        AssertSamePixels(read, writable);

        // 赤・不透明（BGRA=0,0,255,255）
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), read.GetPixel(3, 4));
    }

    [Fact]
    internal void Create_サイズと色のオーバーロードは全ピクセルを塗りつぶす()
    {
        var color = System.Drawing.Color.FromArgb(255, 0, 0, 255);

        using var read = EsReadOnlyBitmap.Create(new EsSizeInt(4, 4), color);
        using var writable = EsBitmap.Create(new EsSizeInt(4, 4), color);

        AssertSamePixels(read, writable);
        Assert.Equal((byte)255, read.GetPixel(2, 2).B);
    }

    [Fact]
    internal void Create_空のサイズでも例外にならない_書き換え可能な側と同じ値になる()
    {
        using var read = EsReadOnlyBitmap.Create(new EsSizeInt(0, 0));
        using var writable = EsBitmap.Create(new EsSizeInt(0, 0));

        Assert.True(read.Size.IsEmpty);
        Assert.True(read.ReadOnlyPixels.IsEmpty);
        AssertSamePixels(read, writable);
    }

    [Fact]
    internal void Create_負のサイズはArgumentOutOfRangeExceptionを投げる()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EsReadOnlyBitmap.Create(-1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => EsReadOnlyBitmap.Create(4, -1));
    }

    [Fact]
    internal void FromPixelCopy_画素を複製して生成する()
    {
        var source = new byte[3 * 2 * 4];
        source[0] = 11;
        source[1] = 22;
        source[2] = 33;
        source[3] = 44;

        using var bmp = EsReadOnlyBitmap.FromPixelCopy(3, 2, source);

        Assert.Equal((3, 2), (bmp.Width, bmp.Height));
        Assert.Equal(((byte)11, (byte)22, (byte)33, (byte)44), bmp.GetPixel(0, 0));

        // 複製である（元の配列を書き換えても画像は変わらない）
        source[0] = 99;
        Assert.Equal((byte)11, bmp.GetPixel(0, 0).B);
    }

    [Fact]
    internal void FromPixelCopy_長さが寸法と一致しない場合はArgumentExceptionを投げる()
        => Assert.Throws<ArgumentException>(() => EsReadOnlyBitmap.FromPixelCopy(3, 2, new byte[3 * 2 * 4 - 1]));

    [Fact]
    internal void FromPixelCopy_負のサイズはArgumentOutOfRangeExceptionを投げる()
        => Assert.Throws<ArgumentOutOfRangeException>(() => EsReadOnlyBitmap.FromPixelCopy(-1, 2, default));

    [Fact]
    internal void InstallPixels_外部の領域を複製せずに読む()
    {
        var external = new byte[4 * 4 * 4];
        external[0] = 42;

        var handle = GCHandle.Alloc(external, GCHandleType.Pinned);
        try
        {
            using var bmp = EsReadOnlyBitmap.InstallPixels(4, 4, handle.AddrOfPinnedObject());

            // 複製していない（外部の領域の値がそのまま読める）
            Assert.Equal((byte)42, bmp.GetPixel(0, 0).B);

            // 借用である（外部の領域を書き換えると画像の読み取りにも現れる）
            external[0] = 43;
            Assert.Equal((byte)43, bmp.GetPixel(0, 0).B);
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    internal void InstallPixels_解放のコールバックを破棄時に1回だけ呼ぶ()
    {
        var called = 0;
        var external = new byte[2 * 2 * 4];
        var handle = GCHandle.Alloc(external, GCHandleType.Pinned);
        try
        {
            var bmp = EsReadOnlyBitmap.InstallPixels(2, 2, handle.AddrOfPinnedObject(), _ => called++);

            Assert.Equal(0, called);
            bmp.Dispose();
            Assert.Equal(1, called);
            bmp.Dispose();
            Assert.Equal(1, called);
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    internal void InstallPixels_画素があるのにアドレスが0ならArgumentExceptionを投げる()
        => Assert.Throws<ArgumentException>(() => EsReadOnlyBitmap.InstallPixels(2, 2, 0));

    [Fact]
    internal void CreateFilled_充填処理を_1回で行う()
    {
        using var bmp = EsReadOnlyBitmap.CreateFilled(3, 2, span => span.Fill(5));

        Assert.Equal((3, 2), (bmp.Width, bmp.Height));
        Assert.All(bmp.ReadOnlyPixels.ToArray(), v => Assert.Equal((byte)5, v));
    }

    [Fact]
    internal void CreateFilled_充填処理が例外なら確保した領域を解放する()
    {
        Assert.Throws<InvalidOperationException>(() => EsReadOnlyBitmap.CreateFilled(4, 4, _ => throw new InvalidOperationException()));
        Assert.Throws<ArgumentNullException>(() => EsReadOnlyBitmap.CreateFilled(4, 4, null!));
    }

    // ====================================================================
    // 複製（Copy。 常に独立した画素）と共有の入口（FromBitmap / ToPixmap）
    // ====================================================================

    [Fact]
    internal void Copy_画素を共有しない独立した複製を生成する()
    {
        using var src = Create(4, 4);

        using var copy = src.Copy();

        // 読み取り専用同士でも画素は共有しない（Copy は常に複製。 費用は実測: 4K で 5.7 ms / 31 MiB）
        Assert.False(SameStorage(src, copy), "読み取り専用の画像の複製が画素を共有しています。");
        Assert.NotSame(src, copy);
        Assert.Equal(src.ReadOnlyPixels.Length, copy.ReadOnlyPixels.Length);
        Assert.Equal(src.GetPixel(1, 1), copy.GetPixel(1, 1));
    }

    /// <summary>凍結済みの共有（<see cref="EsReadOnlyBitmap.FromBitmap"/>）と 0 コピーのビュー（<see cref="EsReadOnlyBitmap.ToPixmap"/>）が同じ画素を指すことを検証する。</summary>
    [Fact]
    internal void 共有の入口はFromBitmapとToPixmap_どちらも同じ画素を指す()
    {
        using var mutable = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 10, 20, 30));
        mutable.SetReadOnly();

        // 凍結済みの書き換え可能な画像からは、何度作っても同じ画素を共有する（0 コピー）
        using var first = EsReadOnlyBitmap.FromBitmap(mutable);
        using var second = EsReadOnlyBitmap.FromBitmap(mutable);
        Assert.True(SameStorage(first, second), "凍結済みの共有が複製になっています。");

        // ビューも元の画素を指す（複製しない）
        using var view = first.ToPixmap(new EsRectInt(1, 2, 2, 2));
        Assert.True(StartsAtStorage(view, first, 1, 2), "ビューが元の画素を指していません（複製されています）。");
    }

    [Fact]
    internal void Copy_先に元を破棄しても複製は読める()
    {
        var src = Create(4, 4);
        var copy = src.Copy();

        src.Dispose();

        // 複製は独立した画素を持つため、元を破棄しても複製は読める
        Assert.False(copy.IsDisposed);
        Assert.Equal(4 * 4 * 4, copy.ReadOnlyPixels.Length);

        copy.Dispose();
    }

    [Fact]
    internal void Copy_空のサイズでも例外にならない()
    {
        using var bmp = Create(0, 0);
        using var copy = bmp.Copy();

        Assert.True(copy.Size.IsEmpty);
        Assert.True(copy.ReadOnlyPixels.IsEmpty);
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
    internal void ReadOnlyPixels_破棄後はObjectDisposedExceptionを投げる()
    {
        var bmp = Create(4, 4);
        bmp.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { _ = bmp.ReadOnlyPixels.Length; });
        Assert.Throws<ObjectDisposedException>(() => bmp.GetPixel(0, 0));
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
        static WeakReference CreateUnreachable() => new(EsReadOnlyBitmap.FromPixelCopy(16, 16, new byte[16 * 16 * 4]));
    }
}
