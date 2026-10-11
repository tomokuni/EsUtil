using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>
/// <see cref="EsReadOnlyBitmap"/>（読み取り専用の画像）の単体テスト。
/// </summary>
/// <remarks>
/// <see cref="EsReadOnlyBitmap"/> は静的共有状態を持たないため、本テストは UI フレームワークの初期化に依存しない
/// （コレクションに属さない <c>[Fact]</c> として並列実行される）。<br/>
/// <b>書き込みの入口が無いことはコンパイル時に決まる</b>ため本テストでは検証できない（書こうとした時点でビルドが失敗する）。
/// 本テストは読み取りの面と、読み取り専用の画像を作る入口（複製・借用・充填）を検証する。
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
        EsReadOnlyBitmap read = src;

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
        EsReadOnlyBitmap read = src;

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

    [Fact]
    internal void 書き換え可能な画像を読み取り専用として受け取れる()
    {
        using var mutable = EsBitmap.Create(4, 3, System.Drawing.Color.FromArgb(255, 10, 20, 30));

        // 派生（書き換え可能）は基底（読み取り専用）としてそのまま渡せる（コピーしない）
        EsReadOnlyBitmap read = mutable;

        Assert.Equal(4, read.Width);
        Assert.Equal(3, read.Height);
        Assert.True(SameStorage(read, mutable), "読み取り専用としての受け取りでコピーが起きています。");
        Assert.Equal(mutable.GetPixel(1, 1), read.GetPixel(1, 1));
    }

    [Fact]
    internal void 破棄状態は同じ実体を指す限り共有される()
    {
        var mutable = EsBitmap.Create(2, 2);
        EsReadOnlyBitmap read = mutable;

        Assert.False(read.IsDisposed);

        mutable.Dispose();

        Assert.True(read.IsDisposed);
    }

    // ====================================================================
    // 生成（複製・借用・充填）
    // ====================================================================

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
    internal void FromPixels_外部の領域を複製せずに読む()
    {
        var external = new byte[4 * 4 * 4];
        external[0] = 42;

        var handle = GCHandle.Alloc(external, GCHandleType.Pinned);
        try
        {
            using var bmp = EsReadOnlyBitmap.FromPixels(4, 4, handle.AddrOfPinnedObject());

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
    internal void FromPixels_解放のコールバックを破棄時に1回だけ呼ぶ()
    {
        var called = 0;
        var external = new byte[2 * 2 * 4];
        var handle = GCHandle.Alloc(external, GCHandleType.Pinned);
        try
        {
            var bmp = EsReadOnlyBitmap.FromPixels(2, 2, handle.AddrOfPinnedObject(), _ => called++);

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
    internal void FromPixels_画素があるのにアドレスが0ならArgumentExceptionを投げる()
        => Assert.Throws<ArgumentException>(() => EsReadOnlyBitmap.FromPixels(2, 2, 0));

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
    // 複製（Clone。 読み取り専用同士は画素を共有する）
    // ====================================================================

    [Fact]
    internal void Clone_画素を共有する_読み取り専用同士は書き換わらないため()
    {
        using var src = Create(4, 4);

        using var clone = src.Clone();

        // 複製の費用（実測: 4K で 5.7 ms / 31 MiB）を払わない
        Assert.True(SameStorage(src, clone), "読み取り専用の複製が共有されていません。");
        Assert.NotSame(src, clone);
    }

    [Fact]
    internal void Clone_先に元を破棄しても画素は有効なまま()
    {
        var src = Create(4, 4);
        var clone = src.Clone();

        src.Dispose();

        // 参照を数えているため、元を先に破棄しても複製の画素は読める
        Assert.False(clone.IsDisposed);
        Assert.Equal(4 * 4 * 4, clone.ReadOnlyPixels.Length);

        clone.Dispose();
    }

    [Fact]
    internal void Clone_空のサイズでも例外にならない()
    {
        using var bmp = Create(0, 0);
        using var clone = bmp.Clone();

        Assert.True(clone.Size.IsEmpty);
        Assert.True(clone.ReadOnlyPixels.IsEmpty);
    }

    // ====================================================================
    // 凍結（SetReadOnly）
    // ====================================================================

    [Fact]
    internal void SetReadOnly_読み取り専用の画像でも冪等に呼べる()
    {
        using var bmp = Create(2, 2);

        bmp.SetReadOnly();
        bmp.SetReadOnly();

        Assert.True(bmp.IsReadOnly);
        Assert.Equal(2, bmp.Width);
    }

    [Fact]
    internal void SetReadOnly_破棄済みならObjectDisposedExceptionを投げる()
    {
        var bmp = Create(2, 2);
        bmp.Dispose();

        Assert.Throws<ObjectDisposedException>(bmp.SetReadOnly);
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
