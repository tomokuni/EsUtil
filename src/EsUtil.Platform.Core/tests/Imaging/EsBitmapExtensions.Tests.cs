using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>
/// <see cref="EsBitmapExtensions"/>（画像を変更しない操作）の単体テスト。
/// </summary>
/// <remarks>
/// 切り出し（<see cref="EsBitmapExtensions.Crop(IEsBitmap, EsRectInt)"/>）は<b>借用した読み取り専用の画像からも</b>
/// 呼べる必要がある（実測: ルーペが表示アダプタの画像を切り出す）。 そのため本テストは
/// <see cref="IEsBitmap"/> を受ける形を固定し、<see cref="EsWritableBitmap"/> からも呼べることを併せて検証する。<br/>
/// 静的共有状態を持たないため、UI フレームワークの初期化に依存しない（<c>[Fact]</c> で並列実行される）。
/// </remarks>
public class EsBitmapExtensionsTests
{
    /// <summary>赤で塗りつぶした書き換え可能な画像を生成する（切り出し元）。</summary>
    private static EsWritableBitmap CreateRed(int w = 10, int h = 10)
        => EsWritableBitmap.Create(w, h, System.Drawing.Color.FromArgb(255, 255, 0, 0));

    // ====================================================================
    // Crop（矩形切り出し）
    // ====================================================================

    [Fact]
    internal void Crop_範囲内の矩形を切り出す()
    {
        using var src = CreateRed();

        using var result = src.Crop(new EsRectInt(2, 2, 4, 4));

        Assert.Equal(4, result.Size.Width);
        Assert.Equal(4, result.Size.Height);
        // 切り出しピクセルが保持されている
        Assert.Equal((byte)255, result.GetPixel(0, 0).R);
    }

    [Fact]
    internal void Crop_読み取り専用の画像からも切り出せる()
    {
        using var src = EsBitmap.Create(10, 10, System.Drawing.Color.FromArgb(255, 255, 0, 0));

        using var result = src.Crop(new EsRectInt(1, 1, 3, 3));

        // 借用した読み取り専用の画像から、書き換え可能な切り出し結果が得られる
        Assert.Equal(3, result.Size.Width);
        Assert.Equal((byte)255, result.GetPixel(0, 0).R);
    }

    [Fact]
    internal void Crop_成分指定のオーバーロードは矩形と同じ結果になる()
    {
        using var src = CreateRed();

        using var byRect = src.Crop(new EsRectInt(2, 2, 4, 4));
        using var byComponents = src.Crop(2, 2, 4, 4);

        Assert.Equal(byRect.Size, byComponents.Size);
        Assert.Equal(byRect.GetPixel(1, 1), byComponents.GetPixel(1, 1));
    }

    [Fact]
    internal void Crop_幅0の矩形は例外を投げる()
    {
        using var src = CreateRed();

        Assert.Throws<ArgumentOutOfRangeException>(() => src.Crop(new EsRectInt(0, 0, 0, 4)));
    }

    [Fact]
    internal void Crop_高さ0の矩形は例外を投げる()
    {
        using var src = CreateRed();

        Assert.Throws<ArgumentOutOfRangeException>(() => src.Crop(new EsRectInt(0, 0, 4, 0)));
    }

    [Fact]
    internal void Crop_範囲外は透明パディングで要求サイズを返す()
    {
        using var src = CreateRed();

        // 矩形が右端を超える場合、コピーできる部分のみコピーし、範囲外は透明（0）のまま
        using var result = src.Crop(new EsRectInt(8, 0, 10, 10));

        Assert.Equal(10, result.Size.Width);
        Assert.Equal(10, result.Size.Height);
        // コピー可能な左側 2px 分は赤（R=255）が保持される
        Assert.Equal((byte)255, result.GetPixel(0, 0).R);
        // 範囲外（右側 8px 分）は透明（R=0）
        Assert.Equal((byte)0, result.GetPixel(9, 0).R);
    }

    [Fact]
    internal void Crop_完全に範囲外でも要求サイズを透明パディングで返す()
    {
        using var src = CreateRed();

        // 矩形がソースと交差しない場合でも、要求サイズの全透明画像を返す
        using var result = src.Crop(new EsRectInt(20, 20, 5, 5));

        Assert.Equal(5, result.Size.Width);
        Assert.Equal(5, result.Size.Height);
        // 全ピクセル透明
        Assert.Equal((byte)0, result.GetPixel(0, 0).R);
        Assert.Equal((byte)0, result.GetPixel(4, 4).A);
    }

    [Fact]
    internal void Crop_左上がソース範囲外の場合は透明パディングで位置関係を保つ()
    {
        using var src = CreateRed();

        // 矩形の左上がソースの外（負）の場合、外側は透明のまま、重なる部分は位置関係を保ってコピーする
        using var result = src.Crop(new EsRectInt(-3, -3, 10, 10));

        Assert.Equal(10, result.Size.Width);
        Assert.Equal(10, result.Size.Height);
        // ソース外（左上の 3x3）は透明
        Assert.Equal((byte)0, result.GetPixel(0, 0).A);
        Assert.Equal((byte)0, result.GetPixel(2, 2).A);
        // ソース (0,0) はデスティネーションの (3,3) へ写る（内容が左上へずれない）
        Assert.Equal((byte)255, result.GetPixel(3, 3).R);
        Assert.Equal((byte)255, result.GetPixel(9, 9).R);
    }

    [Fact]
    internal void Crop_左上が大きく外れた場合は全ピクセル透明を返す()
    {
        using var src = CreateRed();

        // 要求矩形がソースと全く重ならない（左上が負で大きく外れる）場合も、要求サイズの全透明を返す
        using var result = src.Crop(new EsRectInt(-20, -20, 5, 5));

        Assert.Equal(5, result.Size.Width);
        Assert.Equal(5, result.Size.Height);
        Assert.Equal((byte)0, result.GetPixel(0, 0).A);
        Assert.Equal((byte)0, result.GetPixel(4, 4).A);
    }

    [Fact]
    internal void Crop_全面を切り出す場合はパディングを持たない()
    {
        // パディングが無い場合は透明化を行わないため、全ピクセルがコピー元の色で埋まることを固定する
        using var src = EsWritableBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 255, 0, 0));

        using var result = src.Crop(new EsRectInt(0, 0, 4, 4));

        Assert.Equal((byte)255, result.GetPixel(0, 0).R);
        Assert.Equal((byte)255, result.GetPixel(3, 3).R);
        Assert.Equal((byte)255, result.GetPixel(3, 3).A);
    }

    [Fact]
    internal void Crop_パディングは上端と下端と中間行の左右をすべて透明にする()
    {
        using var src = EsWritableBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 255, 0, 0));

        // 上下左右すべてにパディングが出る矩形（左上負・サイズ超過）
        using var result = src.Crop(new EsRectInt(-1, -2, 10, 10));

        // 上端のパディング行（2 行）
        Assert.Equal((byte)0, result.GetPixel(5, 0).A);
        Assert.Equal((byte)0, result.GetPixel(5, 1).A);
        // 中間行の左パディング（1 列）と右パディング（5 列）
        Assert.Equal((byte)0, result.GetPixel(0, 2).A);
        Assert.Equal((byte)0, result.GetPixel(5, 2).A);
        // 下端のパディング行（4 行。 高さ 10 のうち上端 2 + コピー 4）
        Assert.Equal((byte)0, result.GetPixel(5, 9).A);
        // コピーされた領域は赤のまま
        Assert.Equal((byte)255, result.GetPixel(1, 2).R);
        Assert.Equal((byte)255, result.GetPixel(4, 5).R);
    }

    [Fact]
    internal void Crop_切り出し元が空なら透明を返す()
    {
        using var src = EsBitmap.Create(0, 0);

        using var result = src.Crop(new EsRectInt(0, 0, 4, 4));

        Assert.Equal(4, result.Size.Width);
        Assert.Equal(0, result.GetPixel(0, 0).A);
    }

    [Fact]
    internal void Crop_破棄後は例外を投げる()
    {
        var src = CreateRed();
        src.Dispose();

        Assert.Throws<ObjectDisposedException>(() => src.Crop(new EsRectInt(0, 0, 4, 4)));
    }

    [Fact]
    internal void Crop_nullはArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => EsBitmapExtensions.Crop(null!, new EsRectInt(0, 0, 4, 4)));

    // ====================================================================
    // 縮小（DownscaleNearest / DownscaleBilinear / DownscaleBox / Resize）
    // ====================================================================

    /// <summary>公開している縮小の実装（アルゴリズム別の共通の契約を 1 本のテストで回す）。</summary>
    /// <remarks>契約（寸法・縦横比・所有権・例外）はアルゴリズムに依らず同じなので、実装を差し替えて検証する。</remarks>
    public static TheoryData<string, Func<IEsBitmap, int, EsWritableBitmap>> Downscalers => new()
    {
        { nameof(EsBitmapExtensions.DownscaleNearest), EsBitmapExtensions.DownscaleNearest },
        { nameof(EsBitmapExtensions.DownscaleBilinear), EsBitmapExtensions.DownscaleBilinear },
        { nameof(EsBitmapExtensions.DownscaleBox), EsBitmapExtensions.DownscaleBox },
    };

    /// <summary>黒地に 1 画素幅の白線を持つ画像を生成する（縮小で線が残るかの検証用）。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="lineX">白線の列（0 起点）。</param>
    /// <returns>生成した画像。 所有権は呼び出し側。</returns>
    private static EsWritableBitmap CreateThinLine(int width, int height, int lineX)
    {
        var bitmap = EsWritableBitmap.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            bitmap.SetPixel(lineX, y, 255, 255, 255, 255);
        }

        return bitmap;
    }

    /// <summary>指定座標の輝度（BGR の最大値）を返す。</summary>
    /// <param name="bitmap">対象の画像。</param>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>輝度（0〜255）。</returns>
    private static byte Brightness(IEsBitmap bitmap, int x, int y)
    {
        var (b, g, r, _) = bitmap.GetPixel(x, y);
        return Math.Max(b, Math.Max(g, r));
    }

    [Theory]
    [MemberData(nameof(Downscalers))]
    internal void 縮小_上限を超える画像は長辺が上限へ収まる(string name, Func<IEsBitmap, int, EsWritableBitmap> downscale)
    {
        Assert.NotNull(name);
        using var src = CreateRed(200, 100);

        using var result = downscale(src, 50);

        // 長辺（幅）が上限へ収まり、縦横比が保たれる（200:100 = 2:1）
        Assert.Equal(50, result.Size.Width);
        Assert.Equal(25, result.Size.Height);
        Assert.Equal(50 * 4, result.Stride);
    }

    [Theory]
    [MemberData(nameof(Downscalers))]
    internal void 縮小_縦長の画像では高さが上限へ収まる(string name, Func<IEsBitmap, int, EsWritableBitmap> downscale)
    {
        Assert.NotNull(name);
        using var src = CreateRed(100, 400);

        using var result = downscale(src, 50);

        Assert.Equal(50, result.Size.Height);
        Assert.Equal(12, result.Size.Width);
    }

    [Theory]
    [MemberData(nameof(Downscalers))]
    internal void 縮小_長辺が上限と同じなら等倍の複製を返す(string name, Func<IEsBitmap, int, EsWritableBitmap> downscale)
    {
        Assert.NotNull(name);
        using var src = CreateRed(40, 20);

        using var result = downscale(src, 40);

        Assert.Equal(40, result.Size.Width);
        Assert.Equal(20, result.Size.Height);
        // 元の画像をそのまま返さない（戻り値の所有権を呼び出し側へ統一する）
        Assert.NotSame(src, result);
    }

    [Theory]
    [MemberData(nameof(Downscalers))]
    internal void 縮小_画素を保つ(string name, Func<IEsBitmap, int, EsWritableBitmap> downscale)
    {
        Assert.NotNull(name);
        using var src = EsWritableBitmap.Create(8, 8, System.Drawing.Color.FromArgb(255, 10, 20, 30));

        using var result = downscale(src, 4);

        Assert.Equal(4, result.Size.Width);
        Assert.Equal(4, result.Size.Height);
        var (b, g, r, a) = result.GetPixel(2, 2);
        Assert.Equal((byte)30, b);
        Assert.Equal((byte)20, g);
        Assert.Equal((byte)10, r);
        Assert.Equal((byte)255, a);
    }

    [Theory]
    [MemberData(nameof(Downscalers))]
    internal void 縮小_1ピクセル未満には縮小しない(string name, Func<IEsBitmap, int, EsWritableBitmap> downscale)
    {
        Assert.NotNull(name);
        using var src = CreateRed(10, 1000);

        using var result = downscale(src, 2);

        // 高さは上限、幅は最低 1 ピクセル
        Assert.Equal(2, result.Size.Height);
        Assert.Equal(1, result.Size.Width);
    }

    [Theory]
    [MemberData(nameof(Downscalers))]
    internal void 縮小_拡大が必要な上限はNotSupportedExceptionを投げる(string name, Func<IEsBitmap, int, EsWritableBitmap> downscale)
    {
        Assert.NotNull(name);
        using var src = CreateRed(40, 20);

        // 長辺（40）より大きい上限は拡大を要求することになる（黙って等倍を返さない）
        var ex = Assert.Throws<NotSupportedException>(() => downscale(src, 50));
        Assert.Contains("拡大", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Downscalers))]
    internal void 縮小_上限が0以下ならArgumentOutOfRangeExceptionを投げる(string name, Func<IEsBitmap, int, EsWritableBitmap> downscale)
    {
        Assert.NotNull(name);
        using var src = CreateRed(4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => downscale(src, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => downscale(src, -1));
    }

    [Theory]
    [MemberData(nameof(Downscalers))]
    internal void 縮小_nullはArgumentNullExceptionを投げる(string name, Func<IEsBitmap, int, EsWritableBitmap> downscale)
    {
        Assert.NotNull(name);
        Assert.Throws<ArgumentNullException>(() => downscale(null!, 10));
    }

    [Fact]
    internal void 縮小_Resizeは既定のアルゴリズムと同じ結果になる()
    {
        using var src = CreateRed(200, 100);

        using var byResize = src.Resize(50);
        using var byBilinear = src.DownscaleBilinear(50);

        Assert.Equal(byBilinear.Size, byResize.Size);
        Assert.Equal(byBilinear.GetPixel(10, 10), byResize.GetPixel(10, 10));
    }

    [Fact]
    internal void 縮小_Resizeも拡大は未サポート()
    {
        using var src = CreateRed(40, 20);

        Assert.Throws<NotSupportedException>(() => src.Resize(50));
    }

    [Fact]
    internal void 縮小_細線の残り方はアルゴリズムで異なる()
    {
        // 40x4 の黒地に、最近傍の対応表（0,4,8,…,36 列）が読まない位置（21 列）へ 1 画素幅の白線を置く
        using var src = CreateThinLine(40, 4, lineX: 21);

        using var nearest = src.DownscaleNearest(10);
        using var bilinear = src.DownscaleBilinear(10);
        using var box = src.DownscaleBox(10);

        Assert.Equal(10, nearest.Size.Width);
        Assert.Equal(1, nearest.Size.Height);

        // 最近傍は比例位置の画素だけを読むため、間に落ちた細線は残らない
        Assert.Equal(0, Brightness(nearest, 5, 0));
        // 双線形は線と隣の画素を等分する（255 の半分）
        Assert.Equal(127, Brightness(bilinear, 5, 0));
        // 面積平均は線を含む矩形の平均（4 画素に 1 本 = 255 / 4）
        Assert.Equal(63, Brightness(box, 5, 0));
    }
}
