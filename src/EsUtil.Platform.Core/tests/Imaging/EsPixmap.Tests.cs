using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>
/// <see cref="EsPixmap"/>（画像の一部を指す 0 コピーの読み取りビュー）の単体テスト。
/// </summary>
/// <remarks>
/// ビューの生成は <see cref="EsReadOnlyBitmap.ToPixmap"/> が単一所有するため、本テストは画像側から作る。<br/>
/// <see cref="EsPixmap"/> は静的共有状態を持たないため、本テストは UI フレームワークの初期化に依存しない
/// （コレクションに属さない <c>[Fact]</c> として並列実行される）。
/// </remarks>
public class EsPixmapTests
{
    /// <summary>座標ごとに色が変わる読み取り専用の画像を生成する（位置の取り違えを検出するため）。</summary>
    /// <param name="w">幅（ピクセル）。</param>
    /// <param name="h">高さ（ピクセル）。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>書き終えてから凍結し、<see cref="EsReadOnlyBitmap.FromBitmap"/> で 0 コピーの共有を作る。</remarks>
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

    // ====================================================================
    // 0 コピー（元の画素を指す）
    // ====================================================================

    [Fact]
    internal void ToPixmap_元の画素を指す0コピーのビューを返す()
    {
        using var source = CreateGradient(8, 6);

        using var view = source.ToPixmap(new EsRectInt(2, 1, 3, 2));

        Assert.Equal(3, view.Width);
        Assert.Equal(2, view.Height);
        Assert.Equal(new EsSizeInt(3, 2), view.Size);

        // ビューの先頭は元画像の (2, 1) と同じアドレス（行の余白を含むため行単位で読み飛ばす）
        var offset = (1 * source.Stride) + (2 * EsReadOnlyBitmap.BytesPerPixel);
        Assert.True(
            Unsafe.AreSame(
                ref MemoryMarshal.GetReference(view.ReadOnlyPixels),
                ref MemoryMarshal.GetReference(source.ReadOnlyPixels[offset..])),
            "ビューが元の画素を指していません（複製されています）。");
    }

    // ====================================================================
    // 読み取り（行幅を考慮する）
    // ====================================================================

    [Fact]
    internal void GetPixel_元画像の対応する位置と一致する()
    {
        using var source = CreateGradient(8, 6);
        var rect = new EsRectInt(2, 1, 4, 3);

        using var view = source.ToPixmap(rect);

        for (var y = 0; y < view.Height; y++)
        {
            for (var x = 0; x < view.Width; x++)
            {
                // ビューの (x, y) は元画像の (rect.X + x, rect.Y + y) と同じ画素
                Assert.Equal(source.GetPixel(rect.X + x, rect.Y + y), view.GetPixel(x, y));
                Assert.Equal(
                    source.GetPixel(new EsPointInt(rect.X + x, rect.Y + y)),
                    view.GetPixel(new EsPointInt(x, y)));
            }
        }
    }

    [Fact]
    internal void GetPixel_ビューの範囲外はArgumentOutOfRangeExceptionを投げる()
    {
        using var source = CreateGradient(4, 4);
        using var view = source.ToPixmap(new EsRectInt(1, 1, 2, 2));

        Assert.Throws<ArgumentOutOfRangeException>(() => view.GetPixel(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => view.GetPixel(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => view.GetPixel(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => view.GetPixel(0, -1));
    }

    [Fact]
    internal void Stride_元画像のストライドを保つ()
    {
        using var source = CreateGradient(16, 4);

        using var view = source.ToPixmap(new EsRectInt(3, 1, 5, 2));

        Assert.Equal(source.Stride, view.Stride);
        Assert.True(
            view.Stride >= view.Width * EsPixmap.BytesPerPixel,
            $"行幅 {view.Stride} が幅 {view.Width} に対して小さすぎます。");

        // 行の余白を含むため、ビューの画素領域は幅 × 4 × 高さと同じとは限らない
        Assert.Equal(view.Stride * view.Height, view.ReadOnlyPixels.Length);
    }

    [Fact]
    internal void ToSizeText_幅x高さ形式で返す()
    {
        using var source = CreateGradient(8, 6);

        using var view = source.ToPixmap(new EsRectInt(1, 2, 4, 3));

        Assert.Equal("4x3", view.ToSizeText());
    }

    // ====================================================================
    // 寿命（元を先に破棄しても読める）
    // ====================================================================

    [Fact]
    internal void 先に元を破棄してもビューは読める()
    {
        var source = CreateGradient(4, 4);
        using var view = source.ToPixmap(new EsRectInt(1, 1, 2, 2));
        var expected = view.GetPixel(1, 1);

        source.Dispose();

        // ビューは元の画素への参照を 1 つ持つため、先に元を破棄しても読める（解放は最後の 1 つ）
        Assert.False(view.IsDisposed);
        Assert.Equal(expected, view.GetPixel(1, 1));
    }

    // ====================================================================
    // 範囲（元画像の内側に限る）
    // ====================================================================

    [Fact]
    internal void ToPixmap_範囲が外に出る矩形と空の矩形はArgumentOutOfRangeExceptionを投げる()
    {
        using var source = CreateGradient(4, 4);

        // 内側の限界（全面）は許される
        using var full = source.ToPixmap(new EsRectInt(0, 0, 4, 4));
        Assert.Equal(4, full.Width);
        Assert.Equal(4, full.Height);

        // 右・下・左・上へ 1 ピクセルでも出ると拒否する（ビューは透明で埋められない）
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ToPixmap(new EsRectInt(3, 0, 2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ToPixmap(new EsRectInt(0, 3, 2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ToPixmap(new EsRectInt(-1, 0, 2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ToPixmap(new EsRectInt(0, -1, 2, 2)));

        // 幅・高さは 1 以上
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ToPixmap(new EsRectInt(0, 0, 0, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ToPixmap(new EsRectInt(0, 0, 2, 0)));
    }

    // ====================================================================
    // 入れ子（さらに 0 コピーで切り出す）
    // ====================================================================

    [Fact]
    internal void ToPixmap_入れ子のビューは元画像の絶対座標を指す()
    {
        using var source = CreateGradient(8, 8);

        using var parent = source.ToPixmap(new EsRectInt(2, 3, 4, 4));
        using var child = parent.ToPixmap(new EsRectInt(1, 2, 2, 2));

        Assert.Equal(2, child.Width);
        Assert.Equal(2, child.Height);
        Assert.Equal(child.Stride, source.Stride);

        for (var y = 0; y < child.Height; y++)
        {
            for (var x = 0; x < child.Width; x++)
            {
                // 子ビューの (x, y) は元画像の (2 + 1 + x, 3 + 2 + y)
                Assert.Equal(source.GetPixel(3 + x, 5 + y), child.GetPixel(x, y));
            }
        }

        // 入れ子でも範囲は親の内側に限る
        Assert.Throws<ArgumentOutOfRangeException>(() => parent.ToPixmap(new EsRectInt(3, 3, 2, 2)));
    }

    // ====================================================================
    // 破棄
    // ====================================================================

    [Fact]
    internal void 破棄後はObjectDisposedExceptionを投げる()
    {
        using var source = CreateGradient(4, 4);
        var view = source.ToPixmap(new EsRectInt(1, 1, 2, 2));

        view.Dispose();

        Assert.True(view.IsDisposed);
        Assert.Throws<ObjectDisposedException>(view.ThrowIfDisposed);
        Assert.Throws<ObjectDisposedException>(() => { _ = view.ReadOnlyPixels.Length; });
        Assert.Throws<ObjectDisposedException>(() => view.GetPixel(0, 0));
        Assert.Throws<ObjectDisposedException>(() => view.ToPixmap(new EsRectInt(0, 0, 1, 1)));

        // 寸法は値として保持したまま（破棄で画素だけを手放す）
        Assert.Equal("2x2", view.ToSizeText());
    }

    [Fact]
    internal void Dispose_冪等で元画像の画素を解放しない()
    {
        using var source = CreateGradient(4, 3);
        var view = source.ToPixmap(new EsRectInt(1, 1, 2, 2));

        view.Dispose();
        view.Dispose();

        Assert.True(view.IsDisposed);

        // ビューの破棄は元の画素への参照を返すだけであり、画素を解放しない（元画像はそのまま読める）
        Assert.False(source.IsDisposed);
        Assert.Equal((byte)1, source.GetPixel(1, 1).B);
        Assert.Equal((byte)1, source.GetPixel(1, 1).G);
    }

    // ====================================================================
    // ビューを対象にした操作（切り出しの実体化を挟まない）
    // ====================================================================

    [Theory]
    [InlineData(EsImageQuality.Low)]
    [InlineData(EsImageQuality.Mid)]
    [InlineData(EsImageQuality.High)]
    internal void Resize_切り出しの画像を縮小した結果と一致する(EsImageQuality quality)
    {
        using var source = CreateGradient(64, 48);
        var rect = new EsRectInt(9, 7, 32, 24);

        // 画像型の経路: 切り出しの複製（実体化）を作ってから縮小する
        using var cropped = source.Crop(rect);
        using var expected = cropped.Resize(16, quality);

        // ビューの経路: 実体化を挟まずにビューの範囲だけを縮小する
        using var view = source.ToPixmap(rect);
        using var actual = view.Resize(16, quality);

        Assert.Equal(expected.Size, actual.Size);
        Assert.Equal(expected.ReadOnlyPixels.ToArray(), actual.ReadOnlyPixels.ToArray());
    }

    [Fact]
    internal void ReduceColors_切り出しの画像を減色した結果と一致する()
    {
        using var source = CreateGradient(64, 48);
        var rect = new EsRectInt(9, 7, 32, 24);

        using var cropped = source.Crop(rect);
        using var expected = cropped.ReduceColors(4);

        using var view = source.ToPixmap(rect);
        using var actual = view.ReduceColors(4);

        Assert.Equal(expected.Size, actual.Size);
        Assert.Equal(expected.ReadOnlyPixels.ToArray(), actual.ReadOnlyPixels.ToArray());
    }

    [Fact]
    internal void Resize_ビューの範囲外へ出る上限は拡大として拒否される()
    {
        using var source = CreateGradient(32, 24);
        using var view = source.ToPixmap(new EsRectInt(4, 4, 16, 8));

        // 長辺（16）を超える上限は拡大になるため拒否される（画像型と同じ規則）
        Assert.Throws<NotSupportedException>(() => view.Resize(17));
        using var result = view.Resize(16);
        Assert.Equal(16, result.Width);
        Assert.Equal(8, result.Height);
    }

    [Fact]
    internal void Resize_破棄済みならObjectDisposedExceptionを投げる()
    {
        using var source = CreateGradient(16, 16);
        var view = source.ToPixmap(new EsRectInt(0, 0, 8, 8));
        view.Dispose();

        Assert.Throws<ObjectDisposedException>(() => view.Resize(4));
    }

    [Fact]
    internal void ReduceColors_破棄済みならObjectDisposedExceptionを投げる()
    {
        using var source = CreateGradient(16, 16);
        var view = source.ToPixmap(new EsRectInt(0, 0, 8, 8));
        view.Dispose();

        Assert.Throws<ObjectDisposedException>(() => view.ReduceColors(4));
    }

    [Fact]
    internal void ReduceColors_色数の範囲外はArgumentOutOfRangeExceptionを投げる()
    {
        using var source = CreateGradient(16, 16);
        using var view = source.ToPixmap(new EsRectInt(0, 0, 8, 8));

        Assert.Throws<ArgumentOutOfRangeException>(() => view.ReduceColors(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => view.ReduceColors(257));
    }
}
