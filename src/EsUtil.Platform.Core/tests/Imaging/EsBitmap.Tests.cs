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
/// 書き換え可能な画像から読み取り専用の画像を作る規則（<see cref="EsReadOnlyBitmap.FromBitmap"/> が共有するか
/// 複製するか）も本テストが担う（元が凍結済みなら 0 コピーで共有・未凍結なら複製）。
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
    // 複製からの生成（FromPixelCopy。 読み取り専用側の同名メソッドと対）
    // ====================================================================

    [Fact]
    internal void FromPixelCopy_画素を複製して生成する()
    {
        var source = new byte[3 * 2 * 4];
        source[0] = 11;
        source[1] = 22;
        source[2] = 33;
        source[3] = 44;

        using var bmp = EsBitmap.FromPixelCopy(3, 2, source);

        Assert.Equal((3, 2), (bmp.Width, bmp.Height));
        Assert.Equal(3 * 4, bmp.Stride);
        Assert.Equal(((byte)11, (byte)22, (byte)33, (byte)44), bmp.GetPixel(0, 0));
        Assert.False(bmp.IsReadOnly);

        // 複製である（元の配列を書き換えても画像は変わらない）
        source[0] = 99;
        Assert.Equal((byte)11, bmp.GetPixel(0, 0).B);

        // 書き換え可能な画像として生成される（読み取り専用側の FromPixelCopy との違いは書き込みの有無）
        bmp.SetPixel(0, 0, 9, 9, 9, 9);
        Assert.Equal((byte)9, bmp.GetPixel(0, 0).B);
        Assert.Equal((byte)99, source[0]);
    }

    [Fact]
    internal void FromPixelCopy_長さが寸法と一致しない場合はArgumentExceptionを投げる()
        => Assert.Throws<ArgumentException>(() => EsBitmap.FromPixelCopy(3, 2, new byte[(3 * 2 * 4) - 1]));

    [Fact]
    internal void FromPixelCopy_負のサイズはArgumentOutOfRangeExceptionを投げる()
        => Assert.Throws<ArgumentOutOfRangeException>(() => EsBitmap.FromPixelCopy(-1, 2, default));

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

        // 書き込みのビューへ書くと読み取り専用のビューにも現れる（同じ領域を指す）
        bmp.Pixels[0] = 42;

        Assert.Equal((byte)42, bmp.ReadOnlyPixels[0]);
        Assert.Equal(bmp.Pixels.Length, bmp.ReadOnlyPixels.Length);
    }

    // ====================================================================
    // 読み取りビュー（ToPixmap。 読み取り専用側の同名メソッドと対）
    // ====================================================================

    [Fact]
    internal void ToPixmap_書き込みがビューへ現れる0コピーのビューを返す()
    {
        using var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.FromArgb(255, 1, 2, 3));

        using var view = bmp.ToPixmap(new EsRectInt(2, 1, 3, 2));

        // 返るのは読み取りビュー（書き込みの入口を持たない型）
        Assert.IsType<EsPixmap>(view);

        Assert.Equal(3, view.Width);
        Assert.Equal(2, view.Height);
        Assert.Equal(bmp.Stride, view.Stride);
        Assert.Equal(bmp.GetPixel(2, 1), view.GetPixel(0, 0));

        // 0 コピー（書き換え可能な側の書き込みがビューに現れる。 ビューは同じ画素を指す）
        bmp.SetPixel(2, 1, b: 9, g: 8, r: 7, a: 255);
        Assert.Equal(((byte)9, (byte)8, (byte)7, (byte)255), view.GetPixel(0, 0));

        // ビューは元画像の寿命に依存しない（先に元を破棄しても読める）
        bmp.Dispose();
        Assert.Equal((byte)9, view.GetPixel(0, 0).B);
    }

    [Fact]
    internal void ToPixmap_範囲が外に出る矩形と空の矩形はArgumentOutOfRangeExceptionを投げる()
    {
        using var bmp = EsBitmap.Create(4, 4);

        // 内側の限界（全面）は許される
        using var full = bmp.ToPixmap(new EsRectInt(0, 0, 4, 4));
        Assert.Equal(4, full.Width);

        // 右・下・左・上へ 1 ピクセルでも出ると拒否する（ビューは透明で埋められない）
        Assert.Throws<ArgumentOutOfRangeException>(() => bmp.ToPixmap(new EsRectInt(3, 0, 2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => bmp.ToPixmap(new EsRectInt(0, 3, 2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => bmp.ToPixmap(new EsRectInt(-1, 0, 2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => bmp.ToPixmap(new EsRectInt(0, -1, 2, 2)));

        // 幅・高さは 1 以上
        Assert.Throws<ArgumentOutOfRangeException>(() => bmp.ToPixmap(new EsRectInt(0, 0, 0, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => bmp.ToPixmap(new EsRectInt(0, 0, 2, 0)));
    }

    [Fact]
    internal void ToPixmap_破棄済みならObjectDisposedExceptionを投げる()
    {
        var bmp = Create(4, 4);
        bmp.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bmp.ToPixmap(new EsRectInt(0, 0, 2, 2)));
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
    internal void SetReadOnly_二重に呼んでも例外にならない()
    {
        using var bmp = Create(2, 2);

        bmp.SetReadOnly();
        bmp.SetReadOnly();

        Assert.True(bmp.IsReadOnly);
        Assert.Equal(2, bmp.Width);
    }

    [Fact]
    internal void SetReadOnly_凍結後の複製は書き込みできる()
    {
        using var frozen = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        frozen.SetReadOnly();

        using var read = EsReadOnlyBitmap.FromBitmap(frozen);
        using var writable = EsBitmap.FromReadOnlyBitmap(read);

        Assert.False(writable.IsReadOnly);
        writable.SetPixel(0, 0, 9, 9, 9, 9);
        Assert.Equal((byte)3, frozen.GetPixel(0, 0).B);
    }

    [Fact]
    internal void 元が凍結済みなら複製しない()
    {
        using var bmp = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        bmp.SetReadOnly();

        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var readAgain = EsReadOnlyBitmap.FromBitmap(bmp);

        // 凍結済みなら 0 コピー（元と同じ画素領域を指す）
        Assert.True(SameStorage(read, readAgain), "凍結済みなのに複製されています。");

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
        bmp.SetPixel(0, 0, 9, 9, 9, 9);
        Assert.Equal((byte)3, read.GetPixel(0, 0).B);
        Assert.Equal((byte)9, bmp.GetPixel(0, 0).B);

        // 画素の領域も共有しない（凍結すると FromBitmap は元と同じ領域を共有するため、指す領域を比べられる）
        bmp.SetReadOnly();
        using var view = EsReadOnlyBitmap.FromBitmap(bmp);
        Assert.False(SameStorage(view, read), "未凍結なのに共有されています。");
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
    internal void 凍結前に取得したビューは凍結後も書ける_取得時にしか判定できない()
    {
        using var bmp = Create(4, 4);

        // 凍結の前に取得した書き込み可能なビュー
        var span = bmp.Pixels;

        bmp.SetReadOnly();

        // 取得済みの Span は残る（Span は raw なビューであり、取得した後に無効化できない）
        // これは Skia も同じ（GetPixels / GetPixelSpan の書き込みを凍結で止められない）
        span[0] = 9;

        Assert.Equal((byte)9, bmp.GetPixel(0, 0).B);

        // 以後の「取得」は拒否される（穴は「凍結前に取ったビュー」に限定される）
        Assert.Throws<InvalidOperationException>(() => { _ = bmp.Pixels.Length; });
    }

    [Fact]
    internal void 凍結後は更新系の入口がすべて例外になる()
    {
        using var bmp = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        bmp.SetReadOnly();

        // 参照（取得）・点の書き込み・塗りつぶしのすべてが例外になる
        Assert.Throws<InvalidOperationException>(() => { _ = bmp.Pixels.Length; });
        Assert.Throws<InvalidOperationException>(() => bmp.SetPixel(0, 0, 1, 2, 3, 4));
        Assert.Throws<InvalidOperationException>(() => bmp.SetPixel(new EsPointInt(0, 0), System.Drawing.Color.Red));
        Assert.Throws<InvalidOperationException>(() => bmp.TrySetPixel(0, 0, System.Drawing.Color.Red));
        Assert.Throws<InvalidOperationException>(() => bmp.TrySetPixel(new EsPointInt(0, 0), System.Drawing.Color.Red));
        Assert.Throws<InvalidOperationException>(() => bmp.Clear(1, 2, 3, 4));
        Assert.Throws<InvalidOperationException>(() => bmp.Clear(System.Drawing.Color.Red));

        // 読み取りは続けられる
        Assert.Equal((byte)3, bmp.GetPixel(0, 0).B);
    }

    [Fact]
    internal void 凍結後も生成の入口は使える_新しい実体を作るため()
    {
        using var frozen = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        frozen.SetReadOnly();

        // 生成の入口（InstallPixels / CreateFilled / Copy / FromReadOnlyBitmap）は新しい実体を作るため、
        // 凍結の対象ではない（凍結した実体の画素は変わらない）
        using var copy = frozen.Copy();
        using var read = EsReadOnlyBitmap.FromBitmap(frozen);
        using var fromReadOnly = EsBitmap.FromReadOnlyBitmap(read);
        using var filled = EsBitmap.CreateFilled(2, 2, span => span.Fill(9));

        Assert.False(copy.IsReadOnly);
        Assert.False(fromReadOnly.IsReadOnly);
        Assert.False(filled.IsReadOnly);

        copy.SetPixel(0, 0, 7, 7, 7, 7);
        Assert.Equal((byte)3, frozen.GetPixel(0, 0).B);
    }

    [Fact]
    internal void 凍結は実体ごとではなく画素領域ごと_同じメモリを借用すると凍結を迂回できる()
    {
        using var bmp = EsBitmap.Create(2, 2, System.Drawing.Color.FromArgb(255, 1, 2, 3));
        bmp.SetReadOnly();

        // 同じメモリを別の実体として借用する（アドレスを取るには fixed が要る）
        nint address;
        unsafe
        {
            fixed (byte* p = bmp.ReadOnlyPixels)
            {
                address = (nint)p;
            }
        }

        using var alias = EsBitmap.InstallPixels(2, 2, address);

        // 借用した実体は凍結されていない（別の画素領域として扱われる）
        Assert.False(alias.IsReadOnly);
        alias.SetPixel(0, 0, 9, 9, 9, 9);

        // 凍結した実体の画素が変わる（凍結は「画素領域」ではなく「同じ領域を指す別の実体」を守れない）
        Assert.Equal((byte)9, bmp.GetPixel(0, 0).B);
    }

    [Fact]
    internal void SetReadOnly_破棄済みならObjectDisposedExceptionを投げる()
    {
        var bmp = Create(2, 2);
        bmp.Dispose();

        Assert.Throws<ObjectDisposedException>(bmp.SetReadOnly);
    }

    // ====================================================================
    // 複製（Copy）と生成（Create）
    // ====================================================================

    [Fact]
    internal void Copy_独立した画素を持つ複製を生成する()
    {
        using var src = Create();
        src.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 255);
        using var copy = src.Copy();

        copy.SetPixel(0, 0, b: 9, g: 8, r: 7, a: 255);

        // 複製先の書き換えが複製元へ波及しない（書き換え可能な画像同士は共有しない）
        Assert.Equal((byte)1, src.GetPixel(0, 0).B);
        Assert.Equal((byte)9, copy.GetPixel(0, 0).B);

        // 画素の領域も共有しない（凍結すると FromBitmap が 0 コピーで共有するため、指す領域を比べられる）
        src.SetReadOnly();
        copy.SetReadOnly();
        using var srcView = EsReadOnlyBitmap.FromBitmap(src);
        using var copyView = EsReadOnlyBitmap.FromBitmap(copy);
        Assert.False(SameStorage(srcView, copyView), "書き換え可能な画像の複製が画素を共有しています。");
    }

    [Fact]
    internal void Copy_アルファを保持したまま複製する()
    {
        using var src = Create();
        src.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 128);
        using var copy = src.Copy();

        var (B, _, _, A) = copy.GetPixel(0, 0);
        Assert.Equal((byte)1, B);
        Assert.Equal((byte)128, A);
    }

    [Fact]
    internal void Copy_空のサイズでも例外にならない()
    {
        using var bmp = Create(0, 0);
        using var copy = bmp.Copy();

        Assert.True(copy.Size.IsEmpty);
    }

    [Fact]
    internal void FromReadOnlyBitmap_読み取り専用の画像から書き換え可能な複製を作る()
    {
        using var source = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 3, 2, 1));
        using var read = EsReadOnlyBitmap.FromBitmap(source);
        using var copy = EsBitmap.FromReadOnlyBitmap(read);

        Assert.Equal(4, copy.Width);
        Assert.Equal(4, copy.Height);
        Assert.Equal((byte)1, copy.GetPixel(0, 0).B);

        // 生成した側は書き換えられる（凍結は元の画素領域の性質であり、複製へは及ばない）
        Assert.False(copy.IsReadOnly);

        // 複製先を書き換えても複製元は変わらない
        copy.SetPixel(0, 0, 9, 9, 9, 9);
        Assert.Equal((byte)1, read.GetPixel(0, 0).B);
    }

    [Fact]
    internal void FromReadOnlyBitmap_nullならArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => EsBitmap.FromReadOnlyBitmap(null!));

    [Fact]
    internal void FromReadOnlyBitmap_破棄済みならObjectDisposedExceptionを投げる()
    {
        var source = EsBitmap.Create(2, 2);
        source.SetReadOnly();

        var read = EsReadOnlyBitmap.FromBitmap(source);
        read.Dispose();

        Assert.Throws<ObjectDisposedException>(() => EsBitmap.FromReadOnlyBitmap(read));

        source.Dispose();
    }

    /// <summary>変換の往復で、2 つの入口（<see cref="EsReadOnlyBitmap.FromBitmap"/> と
    /// <see cref="EsBitmap.FromReadOnlyBitmap"/>）が対になっていることを検証する。</summary>
    [Fact]
    internal void 変換の往復_凍結済みの書き換え可能側は0コピーで共有し戻す側は複製する()
    {
        using var source = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 3, 2, 1));
        source.SetReadOnly();

        // 往路: 凍結済みの書き換え可能な画像 → 読み取り専用（0 コピーで共有）
        using var read = EsReadOnlyBitmap.FromBitmap(source);
        using var readAgain = EsReadOnlyBitmap.FromBitmap(source);
        Assert.True(SameStorage(read, readAgain), "凍結済みの往路が複製になっています。");

        // 復路: 読み取り専用 → 書き換え可能（書き込む側は共有できないため複製）
        using var back = EsBitmap.FromReadOnlyBitmap(read);
        Assert.False(back.IsReadOnly);

        // 内容は往復で変わらない
        for (var y = 0; y < back.Height; y++)
        {
            for (var x = 0; x < back.Width; x++)
            {
                Assert.Equal(read.GetPixel(x, y), back.GetPixel(x, y));
            }
        }

        // 復路の書き込みは読み取り専用の画像へ届かない（複製である）
        back.SetPixel(0, 0, 9, 9, 9, 9);
        Assert.Equal((byte)1, read.GetPixel(0, 0).B);
        Assert.Equal((byte)1, source.GetPixel(0, 0).B);
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
}
