using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>
/// <see cref="EsBitmap"/>（書き換え可能な画像）の単体テスト。
/// </summary>
/// <remarks>
/// <see cref="EsBitmap"/> は静的共有状態を持たないため、本テストは UI フレームワークの初期化に依存しない
/// （コレクションに属さない <c>[Fact]</c> として並列実行される）。<br/>
/// 読み取りだけの検証は <see cref="EsReadOnlyBitmapTests"/> が担う（本テストは書き換えの面を検証する）。
/// 2 つの実体が同じ画素を指すかどうかの判定は <see cref="SameStorage"/> が単一所有する。
/// </remarks>
public class EsBitmapTests
{
    /// <summary>指定サイズの書き換え可能な画像を生成する。</summary>
    private static EsBitmap Create(int w = 4, int h = 4) => EsBitmap.Create(w, h);

    /// <summary>2 つの画像が同じ画素領域を指すかどうかを返す（コピーされていないことの判定に使う）。</summary>
    private static bool SameStorage(EsReadOnlyBitmap a, EsReadOnlyBitmap b)
        => a.ReadOnlyPixels.Length > 0
            && b.ReadOnlyPixels.Length > 0
            && Unsafe.AreSame(
                ref MemoryMarshal.GetReference(a.ReadOnlyPixels),
                ref MemoryMarshal.GetReference(b.ReadOnlyPixels));

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
        Assert.False(bmp.IsReadOnly);
    }

    [Fact]
    internal void Create_サイズ指定のオーバーロードは寸法をそのまま使う()
    {
        using var bmp = EsBitmap.Create(new EsSizeInt(6, 8));

        Assert.Equal(new EsSizeInt(6, 8), bmp.Size);
    }

    [Fact]
    internal void Create_色付きで全ピクセルを塗りつぶす()
    {
        // 赤・不透明（BGRA=0,0,255,255）
        using var bmp = EsBitmap.Create(6, 8, System.Drawing.Color.FromArgb(255, 255, 0, 0));

        var (B, G, R, A) = bmp.GetPixel(3, 4);
        Assert.Equal((byte)0, B);
        Assert.Equal((byte)0, G);
        Assert.Equal((byte)255, R);
        Assert.Equal((byte)255, A);
    }

    [Fact]
    internal void Create_サイズと色のオーバーロードは全ピクセルを塗りつぶす()
    {
        using var bmp = EsBitmap.Create(new EsSizeInt(4, 4), System.Drawing.Color.FromArgb(255, 0, 0, 255));

        var (B, _, _, A) = bmp.GetPixel(2, 2);
        Assert.Equal((byte)255, B);
        Assert.Equal((byte)255, A);
    }

    [Fact]
    internal void Create_空のサイズでも例外にならない()
    {
        using var bmp = EsBitmap.Create(new EsSizeInt(0, 0));

        Assert.True(bmp.Size.IsEmpty);
        Assert.True(bmp.Pixels.IsEmpty);
    }

    [Fact]
    internal void Create_負のサイズはArgumentOutOfRangeExceptionを投げる()
        => Assert.Throws<ArgumentOutOfRangeException>(() => EsBitmap.Create(-1, 4));

    /// <summary>未初期化確保はサイズとバッファを用意する（ピクセルの値は保証しない）ことを検証する。</summary>
    [Fact]
    internal void CreateUninitialized_サイズとバッファを用意する()
    {
        using var bmp = EsBitmap.CreateUninitialized(6, 8);

        Assert.Equal(6, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);
        Assert.Equal(6 * 4, bmp.Stride);
        Assert.Equal(bmp.Stride * bmp.Height, bmp.Pixels.Length);
        Assert.False(bmp.IsDisposed);
    }

    [Fact]
    internal void CreateUninitialized_サイズ指定のオーバーロードは寸法をそのまま使う()
    {
        using var bmp = EsBitmap.CreateUninitialized(new EsSizeInt(3, 5));

        Assert.Equal(new EsSizeInt(3, 5), bmp.Size);
    }

    [Fact]
    internal void CreateUninitialized_空のサイズなら例外にならない()
    {
        using var bmp = EsBitmap.CreateUninitialized(0, 0);

        Assert.True(bmp.Size.IsEmpty);
        Assert.True(bmp.Pixels.IsEmpty);
    }

    [Fact]
    internal void CreateFilled_充填処理を_1回で行う()
    {
        using var bmp = EsBitmap.CreateFilled(4, 2, span => span.Fill(7));

        Assert.Equal(4, bmp.Width);
        Assert.Equal(2, bmp.Height);
        Assert.All(bmp.ReadOnlyPixels.ToArray(), v => Assert.Equal((byte)7, v));
    }

    [Fact]
    internal void CreateFilled_充填処理が例外なら確保した領域を解放する()
    {
        Assert.Throws<InvalidOperationException>(() => EsBitmap.CreateFilled(4, 4, _ => throw new InvalidOperationException()));
    }

    // ====================================================================
    // 書き換えの面（EsBitmap）
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

    /// <summary>座標型（<see cref="EsPointInt"/>）のオーバーロードが成分オーバーロードと一致することを検証する。</summary>
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
    internal void SetPixel_範囲外はArgumentOutOfRangeExceptionを投げる()
    {
        using var bmp = Create(4, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => bmp.SetPixel(4, 0, 1, 2, 3, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => bmp.SetPixel(-1, 0, 1, 2, 3, 4));
    }

    [Fact]
    internal void TrySetPixel_範囲外はfalseを返し例外にしない()
    {
        using var bmp = Create(4, 4);

        Assert.True(bmp.TrySetPixel(3, 3, System.Drawing.Color.Red));
        Assert.False(bmp.TrySetPixel(4, 3, System.Drawing.Color.Red));
        Assert.False(bmp.TrySetPixel(-1, 0, System.Drawing.Color.Red));
        Assert.False(bmp.TrySetPixel(new EsPointInt(0, 4), System.Drawing.Color.Red));
        Assert.True(bmp.TrySetPixel(new EsPointInt(0, 0), System.Drawing.Color.Red));
    }

    [Fact]
    internal void TrySetPixel_破棄済みは範囲外でも例外を投げる()
    {
        var bmp = Create(4, 4);
        bmp.Dispose();

        // 範囲外（false の経路）でも破棄済みは例外にする（無効な画像を使い続けない）
        Assert.Throws<ObjectDisposedException>(() => bmp.TrySetPixel(9, 9, System.Drawing.Color.Red));
    }

    [Fact]
    internal void Clear_全ピクセルを指定色で塗りつぶす()
    {
        using var bmp = Create(3, 3);
        bmp.Clear(System.Drawing.Color.FromArgb(255, 1, 2, 3));

        Assert.Equal(bmp.GetPixel(0, 0), bmp.GetPixel(2, 2));
        var (B, G, R, A) = bmp.GetPixel(2, 2);
        Assert.Equal((byte)3, B);
        Assert.Equal((byte)2, G);
        Assert.Equal((byte)1, R);
        Assert.Equal((byte)255, A);
    }

    [Fact]
    internal void Clear_成分指定でも同じ結果になる()
    {
        using var bmp = Create(2, 2);
        bmp.Clear(b: 4, g: 5, r: 6, a: 7);

        var (B, G, R, A) = bmp.GetPixel(1, 1);
        Assert.Equal(((byte)4, (byte)5, (byte)6, (byte)7), (B, G, R, A));
    }

    [Fact]
    internal void Clear_空のサイズでも例外にならない()
    {
        using var bmp = EsBitmap.Create(new EsSizeInt(0, 0));
        bmp.Clear(System.Drawing.Color.Red);
    }

    // ====================================================================
    // 画素のビュー
    // ====================================================================

    [Fact]
    internal void Pixels_書き込み可能なビューで書き換えられる()
    {
        using var bmp = Create(2, 2);
        var pixels = bmp.Pixels;

        Assert.Equal(bmp.Stride * bmp.Height, pixels.Length);

        // BGRA の順で書く（先頭ピクセル）
        pixels[0] = 11;
        pixels[1] = 22;
        pixels[2] = 33;
        pixels[3] = 255;

        Assert.Equal((byte)11, bmp.GetPixel(0, 0).B);
        Assert.Equal((byte)33, bmp.GetPixel(0, 0).R);
    }

    [Fact]
    internal void ReadOnlyPixels_書き込みのビューと同じ領域を指す()
    {
        using var bmp = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        EsReadOnlyBitmap read = bmp;

        Assert.True(SameStorage(read, bmp), "読み取り専用の面と書き込みのビューが別の領域を指しています。");
        Assert.Equal(bmp.ReadOnlyPixels[0], bmp.Pixels[0]);
    }

    // ====================================================================
    // 凍結（SetReadOnly）
    // ====================================================================

    [Fact]
    internal void SetReadOnly_凍結すると書き込みが例外になる()
    {
        using var bmp = Create(4, 4);
        bmp.SetReadOnly();

        Assert.True(bmp.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => bmp.SetPixel(0, 0, 1, 2, 3, 4));
        Assert.Throws<InvalidOperationException>(() => bmp.TrySetPixel(0, 0, System.Drawing.Color.Red));
        Assert.Throws<InvalidOperationException>(() => bmp.Clear(System.Drawing.Color.Red));
        Assert.Throws<InvalidOperationException>(() => { _ = bmp.Pixels.Length; });
    }

    [Fact]
    internal void SetReadOnly_凍結しても読み取りは続けられる()
    {
        using var bmp = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        bmp.SetReadOnly();

        Assert.Equal((byte)3, bmp.GetPixel(0, 0).B);
        Assert.Equal(bmp.Stride * 4, bmp.ReadOnlyPixels.Length);
    }

    [Fact]
    internal void SetReadOnly_凍結後の複製は書き込みできる()
    {
        using var frozen = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        frozen.SetReadOnly();

        using var writable = EsBitmap.Create(frozen);

        Assert.False(writable.IsReadOnly);
        writable.SetPixel(0, 0, 9, 9, 9, 9);
        Assert.Equal((byte)3, frozen.GetPixel(0, 0).B);
    }

    [Fact]
    internal void SetReadOnly_読み取り専用として渡した先で凍結すると書き換え可能な側も書けなくなる()
    {
        using var bmp = Create(4, 4);

        // 読み取り専用として渡す（継承なので変換もコピーも起きない）
        EsReadOnlyBitmap read = bmp;
        Assert.False(read.IsReadOnly);

        // 渡した側（受け取った側）で凍結する
        read.SetReadOnly();

        // 書き換え可能な側の実体も書けなくなる（凍結は画素領域の性質であるため）
        Assert.True(bmp.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => bmp.SetPixel(0, 0, 1, 2, 3, 4));
    }

    [Fact]
    internal void 元が凍結済みなら複製しない()
    {
        using var bmp = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        bmp.SetReadOnly();

        using var read = EsReadOnlyBitmap.FromBitmap(bmp);

        // 凍結済みなら 0 コピー（同じ画素領域を指す）
        Assert.True(SameStorage(bmp, read), "凍結済みなのに複製されています。");

        // 破棄の順序に依存しない（参照を数える）
        bmp.Dispose();
        Assert.Equal((byte)3, read.GetPixel(0, 0).B);
    }

    [Fact]
    internal void 元が未凍結なら複製する()
    {
        using var bmp = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);

        Assert.False(bmp.IsReadOnly);

        // 未凍結なら複製（元の書き込みが読み取り専用の画像へ届かず、元も凍結されない）
        Assert.False(SameStorage(bmp, read), "未凍結なのに共有されています。");
        bmp.SetPixel(0, 0, 9, 9, 9, 9);
        Assert.Equal((byte)3, read.GetPixel(0, 0).B);
        Assert.Equal((byte)9, bmp.GetPixel(0, 0).B);
    }

    [Fact]
    internal void 破棄済みならObjectDisposedExceptionを投げる()
    {
        var bmp = Create(2, 2);
        bmp.Dispose();

        Assert.Throws<ObjectDisposedException>(() => EsReadOnlyBitmap.FromBitmap(bmp));
    }

    [Fact]
    internal void nullならArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => EsReadOnlyBitmap.FromBitmap(null!));

    [Fact]
    internal void 継承_読み取り専用として見ても実体が書き換え可能ならCloneは独立した複製を返す()
    {
        using var bmp = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        EsReadOnlyBitmap read = bmp;

        // Clone は仮想メソッドのため、実体（EsBitmap）の実装が呼ばれる（独立した画素の複製）
        using var copy = Assert.IsType<EsBitmap>(read.Clone());

        copy.SetPixel(0, 0, 9, 9, 9, 9);
        Assert.Equal((byte)3, bmp.GetPixel(0, 0).B);
        Assert.False(SameStorage(bmp, copy), "書き換え可能な実体の Clone が画素を共有しています。");
    }

    [Fact]
    internal void 継承_書き換え可能な側の書き込みは読み取り専用として見た画素にも現れる()
    {
        using var bmp = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        EsReadOnlyBitmap read = bmp;

        var before = read.GetPixel(0, 0).B;

        // 書き換え可能な側（同じ実体を保持している）の書き込み
        bmp.SetPixel(0, 0, 9, 8, 7, 255);

        // 読み取り専用として見た画素も変わる（同じ画素領域を指すため。 凍結していない場合の副作用）
        Assert.NotEqual(before, read.GetPixel(0, 0).B);
        Assert.Equal((byte)9, read.GetPixel(0, 0).B);

        // 凍結すると、この経路の書き込みが止まる
        read.SetReadOnly();
        Assert.Throws<InvalidOperationException>(() => bmp.SetPixel(0, 0, 0, 0, 0, 0));
    }

    [Fact]
    internal void SetReadOnly_破棄済みならObjectDisposedExceptionを投げる()
    {
        var bmp = Create(2, 2);
        bmp.Dispose();

        Assert.Throws<ObjectDisposedException>(bmp.SetReadOnly);
    }

    // ====================================================================
    // 複製（Clone）と生成（Create）
    // ====================================================================

    [Fact]
    internal void Clone_独立した画素を持つ複製を生成する()
    {
        using var src = Create();
        src.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 255);
        using var clone = src.Clone();

        clone.SetPixel(0, 0, b: 9, g: 8, r: 7, a: 255);

        // 複製先の書き換えが複製元へ波及しない（書き換え可能な画像同士は共有しない）
        Assert.Equal((byte)1, src.GetPixel(0, 0).B);
        Assert.Equal((byte)9, clone.GetPixel(0, 0).B);
        Assert.False(SameStorage(src, clone), "書き換え可能な画像の複製が画素を共有しています。");
    }

    [Fact]
    internal void Clone_アルファを保持したまま複製する()
    {
        using var src = Create();
        src.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 128);
        using var clone = src.Clone();

        var (B, _, _, A) = clone.GetPixel(0, 0);
        Assert.Equal((byte)1, B);
        Assert.Equal((byte)128, A);
    }

    [Fact]
    internal void Clone_空のサイズでも例外にならない()
    {
        using var bmp = Create(0, 0);
        using var clone = bmp.Clone();

        Assert.True(clone.Size.IsEmpty);
    }

    [Fact]
    internal void Create_読み取り専用の画像から書き換え可能な複製を作る()
    {
        using var source = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 3, 2, 1));
        using var copy = EsBitmap.Create(source);

        Assert.Equal(4, copy.Width);
        Assert.Equal(4, copy.Height);
        Assert.Equal((byte)1, copy.GetPixel(0, 0).B);
        Assert.False(SameStorage(source, copy), "読み取り専用の画像からの複製が画素を共有しています。");

        // 複製先を書き換えても複製元は変わらない
        copy.SetPixel(0, 0, 9, 9, 9, 9);
        Assert.Equal((byte)1, source.GetPixel(0, 0).B);
    }

    [Fact]
    internal void Create_借用画像がnullならArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => EsBitmap.Create(null!));

    [Fact]
    internal void Create_借用画像が破棄済みならObjectDisposedExceptionを投げる()
    {
        using var source = EsBitmap.Create(2, 2);
        var read = (EsReadOnlyBitmap)source;
        source.Dispose();

        Assert.Throws<ObjectDisposedException>(() => EsBitmap.Create(read));
    }

    // ====================================================================
    // 借用（InstallPixels）
    // ====================================================================

    [Fact]
    internal void InstallPixels_外部の領域を複製せずに読み書きする()
    {
        var external = new byte[4 * 3 * 4];
        external[0] = 42;

        var handle = GCHandle.Alloc(external, GCHandleType.Pinned);
        try
        {
            using var bmp = EsBitmap.InstallPixels(4, 3, handle.AddrOfPinnedObject());

            // 複製していない（外部の領域の値がそのまま読める）
            Assert.Equal((byte)42, bmp.GetPixel(0, 0).B);
            Assert.Equal(4 * 4, bmp.Stride);

            // 書き込みは外部の領域へ届く
            bmp.SetPixel(1, 1, b: 7, g: 8, r: 9, a: 10);
            var offset = (1 * bmp.Stride) + (1 * 4);
            Assert.Equal((byte)7, external[offset]);
            Assert.Equal((byte)10, external[offset + 3]);
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
            var bmp = EsBitmap.InstallPixels(2, 2, handle.AddrOfPinnedObject(), _ => called++);

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
        => Assert.Throws<ArgumentException>(() => EsBitmap.InstallPixels(2, 2, 0));

    [Fact]
    internal void InstallPixels_負のサイズはArgumentOutOfRangeExceptionを投げる()
        => Assert.Throws<ArgumentOutOfRangeException>(() => EsBitmap.InstallPixels(-1, 2, 0));

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
    internal void Dispose_破棄後は画素の取得が例外になる()
    {
        var bmp = Create(4, 4);
        bmp.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { _ = bmp.Pixels.Length; });
        Assert.Throws<ObjectDisposedException>(() => bmp.SetPixel(0, 0, 1, 2, 3, 4));
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

    // ====================================================================
    // 読み取り専用としての利用（upcast。 変換もコピーも起きない）
    // ====================================================================

    [Fact]
    internal void EsReadOnlyBitmap_書き換え可能な画像をそのまま渡せる()
    {
        using var bmp = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 10, 20, 30));

        // 暗黙に読み取り専用の型として渡せる（キャストも変換も不要）
        EsReadOnlyBitmap read = bmp;

        Assert.True(SameStorage(read, bmp), "読み取り専用の面への変換でコピーが起きています。");
        Assert.Equal((byte)30, read.GetPixel(1, 1).B);
        Assert.Equal(bmp.ReadOnlyPixels.Length, read.ReadOnlyPixels.Length);
    }
}
