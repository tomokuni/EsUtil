using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>
/// <see cref="EsBitmapOperations"/>（画像を変更せずに新しい画像を作る操作）の単体テスト。
/// </summary>
/// <remarks>
/// 操作（切り出し・縮小・減色）は<b>両方の画像型が同じ名前のメンバー</b>として公開し（実装は <see cref="EsBitmapOperations"/> が
/// 単一所有する）、本テストは<b>読み取り専用の画像として受け取った場合</b>の手順を固定する（借用した読み取り専用の画像を含む）。
/// <b>元の画像が変更されないこと・凍結されないこと</b>も確かめる。<br/>
/// 静的共有状態を持たないため、UI フレームワークの初期化に依存しない（<c>[Fact]</c> で並列実行される）。
/// </remarks>
public class EsBitmapOperationsTests
{
    /// <summary>赤で塗りつぶした読み取り専用の画像を生成する（切り出し元）。</summary>
    /// <param name="w">幅。</param>
    /// <param name="h">高さ。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    private static EsReadOnlyBitmap CreateRed(int w = 10, int h = 10)
        => CreateFilled(w, h, System.Drawing.Color.FromArgb(255, 255, 0, 0));

    /// <summary>単色で塗りつぶした読み取り専用の画像を生成する。</summary>
    /// <param name="w">幅。</param>
    /// <param name="h">高さ。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>書き終えてから凍結し、<see cref="EsReadOnlyBitmap.FromBitmap"/> で読み取り専用の面を作る
    /// （凍結済みは複製されないため、画素を 0 コピーで共有する）。</remarks>
    private static EsReadOnlyBitmap CreateFilled(int w, int h, System.Drawing.Color color)
    {
        using var bitmap = EsBitmap.Create(w, h, color);
        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

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
    internal void Crop_書き換え可能な画像は読み取り専用として渡してから切り出せる()
    {
        using var writable = EsBitmap.Create(10, 10, System.Drawing.Color.FromArgb(255, 255, 0, 0));

        // 読み取り専用として受け取った場合の入口を固定する（FromBitmap は未凍結なので複製になる）
        using var read = EsReadOnlyBitmap.FromBitmap(writable);

        using var result = read.Crop(new EsRectInt(1, 1, 3, 3));

        // 読み取り専用の画像から、書き換え可能な切り出し結果が得られる
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
        using var src = CreateFilled(4, 4, System.Drawing.Color.FromArgb(255, 255, 0, 0));

        using var result = src.Crop(new EsRectInt(0, 0, 4, 4));

        Assert.Equal((byte)255, result.GetPixel(0, 0).R);
        Assert.Equal((byte)255, result.GetPixel(3, 3).R);
        Assert.Equal((byte)255, result.GetPixel(3, 3).A);
    }

    [Fact]
    internal void Crop_パディングは上端と下端と中間行の左右をすべて透明にする()
    {
        using var src = CreateFilled(4, 4, System.Drawing.Color.FromArgb(255, 255, 0, 0));

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
        using var src = EsReadOnlyBitmap.FromPixelCopy(0, 0, []);

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

    // ====================================================================
    // 縮小（Resize の品質: Low / Mid / High）
    // ====================================================================

    /// <summary>公開している縮小の契約（寸法・縦横比・所有権・例外）を品質別に回す。</summary>
    /// <remarks>契約は品質に依らず同じなので、引数だけを差し替えて検証する。</remarks>
    public static TheoryData<EsImageQuality> Qualities =>
    [
        EsImageQuality.Low,
        EsImageQuality.Mid,
        EsImageQuality.High,
    ];

    /// <summary>黒地に 1 画素幅の白線を持つ画像を生成する（縮小で線が残るかの検証用）。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="lineX">白線の列（0 起点）。</param>
    /// <returns>生成した画像。 所有権は呼び出し側。</returns>
    private static EsReadOnlyBitmap CreateThinLine(int width, int height, int lineX)
    {
        using var bitmap = EsBitmap.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            bitmap.SetPixel(lineX, y, 255, 255, 255, 255);
        }

        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    /// <summary>指定座標の輝度（BGR の最大値）を返す。</summary>
    /// <param name="bitmap">対象の画像。</param>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>輝度（0〜255）。</returns>
    private static byte Brightness(EsReadOnlyBitmap bitmap, int x, int y)
    {
        var (b, g, r, _) = bitmap.GetPixel(x, y);
        return Math.Max(b, Math.Max(g, r));
    }

    /// <summary>書き換え可能な画像の指定座標の輝度（BGR の最大値）を返す。</summary>
    /// <param name="bitmap">対象の画像。 書き換えない。</param>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>輝度（0〜255）。</returns>
    /// <remarks>凍結しない（複製して読み取る）。 縮小の結果のような一時的な画像を計測するために使う。</remarks>
    private static byte Brightness(EsBitmap bitmap, int x, int y)
    {
        using var read = EsReadOnlyBitmap.FromBitmap(bitmap);
        return Brightness(read, x, y);
    }

    [Theory]
    [MemberData(nameof(Qualities))]
    internal void 縮小_上限を超える画像は長辺が上限へ収まる(EsImageQuality quality)
    {
        using var src = CreateRed(200, 100);

        using var result = src.Resize(50, quality);

        // 長辺（幅）が上限へ収まり、縦横比が保たれる（200:100 = 2:1）
        Assert.Equal(50, result.Size.Width);
        Assert.Equal(25, result.Size.Height);
        Assert.Equal(50 * 4, result.Stride);
    }

    [Theory]
    [MemberData(nameof(Qualities))]
    internal void 縮小_縦長の画像では高さが上限へ収まる(EsImageQuality quality)
    {
        using var src = CreateRed(100, 400);

        using var result = src.Resize(50, quality);

        Assert.Equal(50, result.Size.Height);
        Assert.Equal(12, result.Size.Width);
    }

    [Theory]
    [MemberData(nameof(Qualities))]
    internal void 縮小_長辺が上限と同じなら等倍の複製を返す(EsImageQuality quality)
    {
        using var src = CreateRed(40, 20);

        using var result = src.Resize(40, quality);

        Assert.Equal(40, result.Size.Width);
        Assert.Equal(20, result.Size.Height);
        // 元の画像をそのまま返さない（戻り値の所有権を呼び出し側へ統一する）
        Assert.NotSame(src, result);
    }

    [Theory]
    [MemberData(nameof(Qualities))]
    internal void 縮小_画素を保つ(EsImageQuality quality)
    {
        using var src = CreateFilled(8, 8, System.Drawing.Color.FromArgb(255, 10, 20, 30));

        using var result = src.Resize(4, quality);

        Assert.Equal(4, result.Size.Width);
        Assert.Equal(4, result.Size.Height);
        var (b, g, r, a) = result.GetPixel(2, 2);
        Assert.Equal((byte)30, b);
        Assert.Equal((byte)20, g);
        Assert.Equal((byte)10, r);
        Assert.Equal((byte)255, a);
    }

    [Theory]
    [MemberData(nameof(Qualities))]
    internal void 縮小_1ピクセル未満には縮小しない(EsImageQuality quality)
    {
        using var src = CreateRed(10, 1000);

        using var result = src.Resize(2, quality);

        // 高さは上限、幅は最低 1 ピクセル
        Assert.Equal(2, result.Size.Height);
        Assert.Equal(1, result.Size.Width);
    }

    [Theory]
    [MemberData(nameof(Qualities))]
    internal void 縮小_拡大が必要な上限はNotSupportedExceptionを投げる(EsImageQuality quality)
    {
        using var src = CreateRed(40, 20);

        // 長辺（40）より大きい上限は拡大を要求することになる（黙って等倍を返さない）
        var ex = Assert.Throws<NotSupportedException>(() => src.Resize(50, quality));
        Assert.Contains("拡大", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Qualities))]
    internal void 縮小_上限が0以下ならArgumentOutOfRangeExceptionを投げる(EsImageQuality quality)
    {
        using var src = CreateRed(4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => src.Resize(0, quality));
        Assert.Throws<ArgumentOutOfRangeException>(() => src.Resize(-1, quality));
    }

    [Fact]
    internal void 縮小_既定の品質はMid()
    {
        using var src = CreateRed(200, 100);

        using var byDefault = src.Resize(50);
        using var byBilinear = src.Resize(50, EsImageQuality.Mid);

        Assert.Equal(byBilinear.Size, byDefault.Size);
        Assert.Equal(byBilinear.GetPixel(10, 10), byDefault.GetPixel(10, 10));
    }

    [Fact]
    internal void 縮小_定義されていない品質はArgumentOutOfRangeExceptionを投げる()
    {
        using var src = CreateRed(200, 100);

        Assert.Throws<ArgumentOutOfRangeException>(() => src.Resize(50, (EsImageQuality)99));
    }

    [Fact]
    internal void 縮小_細線の残り方は品質で異なる()
    {
        // 40x4 の黒地に、最近傍（Low）の対応表（0,4,8,…,36 列）が読まない位置（21 列）へ 1 画素幅の白線を置く
        using var src = CreateThinLine(40, 4, lineX: 21);

        using var nearest = src.Resize(10, EsImageQuality.Low);
        using var bilinear = src.Resize(10, EsImageQuality.Mid);
        using var box = src.Resize(10, EsImageQuality.High);

        Assert.Equal(10, nearest.Size.Width);
        Assert.Equal(1, nearest.Size.Height);

        // Low（最近傍）は比例位置の画素だけを読むため、間に落ちた細線は残らない
        Assert.Equal(0, Brightness(nearest, 5, 0));
        // Mid（双線形）は線と隣の画素を等分する（255 の半分）
        Assert.Equal(127, Brightness(bilinear, 5, 0));
        // High（面積平均）は線を含む矩形の平均（4 画素に 1 本 = 255 / 4）
        Assert.Equal(63, Brightness(box, 5, 0));
    }

    [Fact]
    internal void 縮小_面積平均は中間の丸めを持たない()
    {
        // 40x40 の一様な画像（値 3）を 8x8 へ縮小する: 面積平均は 3 のまま（2 パスでも同じだが、
        // 中間で割る実装だと丸めが入る。 1 パスであることを値で固定する）
        using var src = CreateFilled(40, 40, System.Drawing.Color.FromArgb(255, 3, 5, 7));

        using var result = src.Resize(8, EsImageQuality.High);

        var (b, g, r, _) = result.GetPixel(4, 4);
        Assert.Equal((byte)7, b);
        Assert.Equal((byte)5, g);
        Assert.Equal((byte)3, r);
    }

    [Fact]
    internal void 縮小_面積平均は大きい画像でも結果が同じ_並列化の確認()
    {
        // 並列化のしきい値（262,144 画素）を跨ぐ寸法で、逐次と並列の結果が一致することを確認する
        using var src = CreateThinLine(600, 480, lineX: 301);

        using var result = src.Resize(150, EsImageQuality.High);

        Assert.Equal(150, result.Size.Width);
        Assert.Equal(120, result.Size.Height);
        // 線（1 画素幅 × 480 行）は 4 画素幅のブロックへ平均される（輝度 = 255 × 480 / 4 / 480 = 63）
        Assert.Equal(63, Brightness(result, 75, 60));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(16)]
    [InlineData(256)]
    internal void 減色_指定した色数以下になる(int colorCount)
    {
        using var src = CreateColorful(64, 64);

        using var result = src.ReduceColors(colorCount);

        Assert.True(
            CountDistinctColors(result) <= colorCount,
            $"{colorCount} 色を指定しましたが {CountDistinctColors(result)} 色になりました。");
    }

    [Fact]
    internal void 減色_寸法を変えない()
    {
        using var src = CreateColorful(40, 24);

        using var result = src.ReduceColors(8);

        Assert.Equal(40, result.Size.Width);
        Assert.Equal(24, result.Size.Height);
    }

    [Fact]
    internal void 減色_箱が多い画像でも同じ入力色は同じ出力色になる()
    {
        // 5 ビットの箱を最大限使う画像を作る（箱の分割が最も多くなる経路。 箱を 1 本の配列の区間で
        // 持つ実装では、箱が多い画像で配列の範囲を外す誤りが起きやすい）
        using var src = CreateNoisy(384, 384);
        var sources = EnumerateColors(src);
        Assert.True(
            new HashSet<uint>(sources).Count > 4096,
            $"{sources.Length} 画素で {new HashSet<uint>(sources).Count} 色しかありません。");

        using var result = src.ReduceColors(EsBitmapOperations.MaximumColorCount);
        var destinations = EnumerateColors(result);

        Assert.True(CountDistinctColors(result) <= EsBitmapOperations.MaximumColorCount);

        // 同じ入力色は必ず同じ出力色へ置き換わる（参照表が色ごとに 1 つであること）
        var mapped = new Dictionary<uint, uint>();
        for (var i = 0; i < sources.Length; i++)
        {
            if (mapped.TryGetValue(sources[i], out var first))
            {
                Assert.Equal(first, destinations[i]);
            }
            else
            {
                mapped[sources[i]] = destinations[i];
            }
        }
    }

    [Fact]
    internal void 減色_使われている色より多い色数を指定しても色は増えない()
    {
        using var src = CreateFilled(8, 8, System.Drawing.Color.FromArgb(255, 10, 20, 30));

        using var result = src.ReduceColors(256);

        Assert.Equal(1, CountDistinctColors(result));
        Assert.Equal(System.Drawing.Color.FromArgb(255, 10, 20, 30), result.GetPixel(new EsPointInt(0, 0)));
    }

    [Fact]
    internal void 減色_元の画像を変更しない()
    {
        using var src = CreateColorful(32, 32);
        var before = src.ReadOnlyPixels.ToArray();

        using var result = src.ReduceColors(4);

        Assert.Equal(before, src.ReadOnlyPixels.ToArray());
    }

    [Fact]
    internal void 減色_書き換え可能な画像は読み取り専用として渡してから減色できる()
    {
        using var writable = EsBitmap.Create(16, 16, System.Drawing.Color.FromArgb(255, 12, 34, 56));

        // 読み取り専用として受け取った場合の入口を固定する（FromBitmap は未凍結なので複製になる）
        using var source = EsReadOnlyBitmap.FromBitmap(writable);

        using var result = source.ReduceColors(4);

        Assert.Equal(16, result.Size.Width);
        Assert.True(CountDistinctColors(result) <= 4);
    }

    [Fact]
    internal void 減色_画素が元の色のいずれかへ寄る()
    {
        using var src = CreateColorful(32, 32);
        var before = src.ReadOnlyPixels.ToArray();

        using var result = src.ReduceColors(16);

        // 減色は平均色を作るため一致はしないが、色差は 5 ビットの箱の幅（8）を大きく超えない
        var pixels = result.ReadOnlyPixels;
        var maxDifference = 0;
        for (var i = 0; i < pixels.Length; i++)
        {
            maxDifference = Math.Max(maxDifference, Math.Abs(pixels[i] - before[i]));
        }

        Assert.True(maxDifference <= 96, $"最大色差 {maxDifference} が大きすぎます。");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(257)]
    internal void 減色_色数が範囲外ならArgumentOutOfRangeExceptionを投げる(int colorCount)
    {
        using var src = CreateColorful(8, 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => src.ReduceColors(colorCount));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    internal void 減色_定義されていない品質はArgumentOutOfRangeExceptionを投げる(int quality)
    {
        using var src = CreateColorful(8, 8);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => src.ReduceColors(8, (EsImageQuality)quality));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    internal void 減色_定義されていないディザはArgumentOutOfRangeExceptionを投げる(int dither)
    {
        using var src = CreateColorful(8, 8);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => src.ReduceColors(8, EsImageQuality.Mid, (EsDitherMode)dither));
    }

    [Theory]
    [MemberData(nameof(Qualities))]
    internal void 減色_品質を変えても色数は指定以下になる(EsImageQuality quality)
    {
        using var src = CreateColorful(64, 64);

        using var result = src.ReduceColors(16, quality);

        Assert.True(
            CountDistinctColors(result) <= 16,
            $"{quality} で {CountDistinctColors(result)} 色になりました。");
    }

    [Fact]
    internal void 減色_品質HighはLowより色差が小さい()
    {
        // 色数の少ない減色では中央値分割の結果を詰め直す効果が最も大きい（実測: 写真風 16 色で 14.34 → 12.64）
        using var src = CreateNoisy(64, 64);

        using var low = src.ReduceColors(16, EsImageQuality.Low);
        using var high = src.ReduceColors(16, EsImageQuality.High);

        Assert.True(
            MeanChannelDifference(src, high) <= MeanChannelDifference(src, low),
            $"High の色差 {MeanChannelDifference(src, high):F2} が Low の {MeanChannelDifference(src, low):F2} を超えました。");
    }

    [Theory]
    [MemberData(nameof(Qualities))]
    internal void 減色_ディザを指定しても色数は指定以下になる(EsImageQuality quality)
    {
        using var src = CreateNoisy(64, 64);

        using var result = src.ReduceColors(16, quality, EsDitherMode.FloydSteinberg);

        Assert.True(
            CountDistinctColors(result) <= 16,
            $"{quality} で {CountDistinctColors(result)} 色になりました。");
    }

    [Fact]
    internal void 減色_ディザは平坦な色の画像を変えない()
    {
        // 誤差が 0 の画素には何も配られないため、単色の画像はディザの有無で結果が同じになる
        using var src = CreateFilled(32, 32, System.Drawing.Color.FromArgb(255, 12, 34, 56));

        using var plain = src.ReduceColors(8);
        using var dithered = src.ReduceColors(8, EsImageQuality.Mid, EsDitherMode.FloydSteinberg);

        Assert.Equal(plain.ReadOnlyPixels.ToArray(), dithered.ReadOnlyPixels.ToArray());
    }

    [Fact]
    internal void 減色_ディザはグラデーションの局所平均を元の色へ近づける()
    {
        // ディザは画素ごとの誤差を増やすため、目的（離れて見たときに元の色へ戻る）をブロック平均で測る
        using var src = CreateHorizontalGradient(256, 16);

        using var plain = src.ReduceColors(4);
        using var dithered = src.ReduceColors(4, EsImageQuality.Mid, EsDitherMode.FloydSteinberg);

        var plainError = BlockMeanDifference(src, plain, block: 8);
        var ditheredError = BlockMeanDifference(src, dithered, block: 8);

        Assert.True(
            ditheredError < plainError,
            $"ディザの局所平均の差 {ditheredError:F2} が、なしの {plainError:F2} を下回りませんでした。");
    }

    /// <summary>1 チャネルあたりの平均絶対誤差を求める（画素ごと）。</summary>
    /// <param name="source">元の画像。</param>
    /// <param name="result">比べる画像（同じ寸法であること）。</param>
    /// <returns>平均絶対誤差。</returns>
    private static double MeanChannelDifference(EsReadOnlyBitmap source, EsBitmap result)
    {
        var before = source.ReadOnlyPixels;
        var after = result.ReadOnlyPixels;
        long total = 0;
        for (var y = 0; y < result.Height; y++)
        {
            for (var x = 0; x < result.Width; x++)
            {
                var i = (y * source.Stride) + (x * EsReadOnlyBitmap.BytesPerPixel);
                var j = (y * result.Stride) + (x * EsReadOnlyBitmap.BytesPerPixel);
                for (var channel = 0; channel < EsReadOnlyBitmap.BytesPerPixel; channel++)
                {
                    total += Math.Abs(before[i + channel] - after[j + channel]);
                }
            }
        }

        return (double)total / (result.Width * result.Height * EsReadOnlyBitmap.BytesPerPixel);
    }

    /// <summary>ブロック平均の差（1 チャネルあたりの平均絶対誤差）を求める。</summary>
    /// <param name="source">元の画像。</param>
    /// <param name="result">比べる画像（同じ寸法であること）。</param>
    /// <param name="block">ブロックの辺（ピクセル）。</param>
    /// <returns>ブロック平均の差。</returns>
    /// <remarks>ディザの目的（離れて見たときに元の色へ戻る）を測るために使う（<see cref="EsDitherMode"/> の備考）。</remarks>
    private static double BlockMeanDifference(EsReadOnlyBitmap source, EsBitmap result, int block)
    {
        var before = source.ReadOnlyPixels;
        var after = result.ReadOnlyPixels;
        long total = 0;
        var blocks = 0;
        for (var by = 0; by < result.Height; by += block)
        {
            for (var bx = 0; bx < result.Width; bx += block)
            {
                for (var channel = 0; channel < EsReadOnlyBitmap.BytesPerPixel; channel++)
                {
                    long beforeSum = 0;
                    long afterSum = 0;
                    var count = 0;
                    for (var y = by; y < Math.Min(by + block, result.Height); y++)
                    {
                        for (var x = bx; x < Math.Min(bx + block, result.Width); x++)
                        {
                            beforeSum += before[(y * source.Stride) + (x * EsReadOnlyBitmap.BytesPerPixel) + channel];
                            afterSum += after[(y * result.Stride) + (x * EsReadOnlyBitmap.BytesPerPixel) + channel];
                            count++;
                        }
                    }

                    total += Math.Abs((beforeSum / count) - (afterSum / count));
                    blocks++;
                }
            }
        }

        return (double)total / blocks;
    }

    [Fact]
    internal void 減色_破棄後は例外を投げる()
    {
        var src = CreateColorful(8, 8);
        src.Dispose();

        Assert.Throws<ObjectDisposedException>(() => src.ReduceColors(16));
    }

    [Fact]
    internal void 減色_透明な画素は透明のまま残る()
    {
        using var src = CreateWithAlpha();

        using var result = src.ReduceColors(8);

        // 3 段階のアルファ（不透明 / 半透明 / 完全透明）がそのまま残る
        var alphas = new HashSet<byte>();
        var pixels = result.ReadOnlyPixels;
        for (var i = 3; i < pixels.Length; i += EsReadOnlyBitmap.BytesPerPixel)
        {
            alphas.Add(pixels[i]);
        }

        Assert.Contains((byte)255, alphas);
        Assert.Contains((byte)128, alphas);
        Assert.Contains((byte)0, alphas);
    }

    // ====================================================================
    // 書き換え可能な画像のメンバー（凍結せずに読む）
    // ====================================================================

    [Fact]
    internal void Crop_書き換え可能な画像から呼べて元は凍結されない()
    {
        using var bitmap = EsBitmap.Create(10, 10, System.Drawing.Color.FromArgb(255, 1, 2, 3));

        using var result = bitmap.Crop(new EsRectInt(2, 2, 4, 4));

        // 元は凍結されない（画素を読むだけの操作であり、書き込みの入口を閉じない）
        Assert.False(bitmap.IsReadOnly);
        bitmap.SetPixel(0, 0, 9, 9, 9, 9);
        Assert.Equal((byte)9, bitmap.GetPixel(0, 0).B);

        // 切り出しの結果は独立している（元の書き込みに影響されない）
        Assert.Equal((byte)3, result.GetPixel(0, 0).B);
        Assert.Equal(4, result.Width);

        // 成分指定の版も同じ結果になる
        using var byComponents = bitmap.Crop(2, 2, 4, 4);
        Assert.Equal(result.ReadOnlyPixels.ToArray(), byComponents.ReadOnlyPixels.ToArray());
    }

    [Fact]
    internal void Resize_書き換え可能な画像から呼べて元は凍結されない()
    {
        using var bitmap = EsBitmap.Create(20, 10, System.Drawing.Color.FromArgb(255, 1, 2, 3));

        using var result = bitmap.Resize(5, EsImageQuality.High);

        Assert.False(bitmap.IsReadOnly);
        Assert.Equal(5, result.Width);
        Assert.Equal((byte)3, result.GetPixel(0, 0).B);
    }

    [Fact]
    internal void ReduceColors_書き換え可能な画像から呼べて元は凍結されない()
    {
        using var bitmap = EsBitmap.CreateFilled(8, 8, span => span.Fill(200));

        using var result = bitmap.ReduceColors(16);

        Assert.False(bitmap.IsReadOnly);
        Assert.Equal((byte)200, result.GetPixel(0, 0).B);
    }

    [Fact]
    internal void 書き換え可能な画像のメンバーは破棄済みなら例外になる()
    {
        var bitmap = EsBitmap.Create(4, 4);
        bitmap.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bitmap.Crop(new EsRectInt(0, 0, 2, 2)));
        Assert.Throws<ObjectDisposedException>(() => bitmap.Resize(2));
        Assert.Throws<ObjectDisposedException>(() => bitmap.ReduceColors(16));
    }

    /// <summary>透明・半透明・不透明の 3 段階を持つ画像を作る（RGB は同じ）。</summary>
    /// <param name="width">幅（ピクセル。 3 等分する）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    private static EsReadOnlyBitmap CreateWithAlpha(int width = 48, int height = 8)
    {
        using var bitmap = EsBitmap.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var alpha = x < width / 3 ? (byte)255 : x < (width * 2 / 3) ? (byte)128 : (byte)0;
                bitmap.SetPixel(x, y, 40, 80, 120, alpha);
            }
        }

        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    /// <summary>色数の多い画像を作る（減色の効果を観測するため）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    private static EsReadOnlyBitmap CreateColorful(int width, int height)
    {
        using var bitmap = EsBitmap.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, (byte)((x * 7) & 0xFF), (byte)((y * 11) & 0xFF), (byte)(((x + y) * 13) & 0xFF), 255);
            }
        }

        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    /// <summary>横方向の滑らかなグラデーションを作る（ディザの検証用）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    private static EsReadOnlyBitmap CreateHorizontalGradient(int width, int height)
    {
        using var bitmap = EsBitmap.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = (byte)(x * 255 / width);
                bitmap.SetPixel(x, y, value, value, value, 255);
            }
        }

        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    /// <summary>画素ごとに色が不規則に変わる画像を作る（減色の箱を最も多く使う画像）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>決定的な擬似乱数を使う理由</b>: 同じ画像を毎回作る（テストの再現性のため）。
    /// 色が全範囲に散るため、5 ビットの箱（32³ = 32,768 個）がほぼ埋まる。
    /// </remarks>
    private static EsReadOnlyBitmap CreateNoisy(int width, int height)
    {
        using var bitmap = EsBitmap.Create(width, height);
        var seed = 987_654_321u;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                seed = (seed * 1664525u) + 1013904223u;
                var red = (byte)(seed >> 24);
                seed = (seed * 1664525u) + 1013904223u;
                var green = (byte)(seed >> 24);
                seed = (seed * 1664525u) + 1013904223u;
                var blue = (byte)(seed >> 24);
                bitmap.SetPixel(x, y, red, green, blue, 255);
            }
        }

        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    // ====================================================================
    // 行の余白（Stride > 幅 × 4）
    // ====================================================================

    [Fact]
    internal void 切り出し_行の余白を含むソースでも余白の画素を混ぜない()
    {
        var dense = CreateGradientPixels(6, 4);
        var stride = (6 * EsReadOnlyBitmap.BytesPerPixel) + 12;
        var padded = CreatePadded(dense, 6, 4, stride, padding: 0xAB);

        using var expected = EsReadOnlyBitmap.FromPixelCopy(6, 4, dense);
        using var fromDense = expected.Crop(new EsRectInt(0, 0, 6, 4));
        using var fromPadded = EsBitmapOperations.Crop(padded, 6, 4, stride, 0, 0, 6, 4);

        Assert.Equal(fromDense.ReadOnlyPixels.ToArray(), fromPadded.ReadOnlyPixels.ToArray());
    }

    [Fact]
    internal void 切り出し_行の余白を含むソースでも外側の透明パディングと行の対応が変わらない()
    {
        var dense = CreateGradientPixels(6, 4);
        var stride = (6 * EsReadOnlyBitmap.BytesPerPixel) + 12;
        var padded = CreatePadded(dense, 6, 4, stride, padding: 0xAB);

        using var expected = EsReadOnlyBitmap.FromPixelCopy(6, 4, dense);
        using var fromDense = expected.Crop(new EsRectInt(-2, -1, 10, 6));
        using var fromPadded = EsBitmapOperations.Crop(padded, 6, 4, stride, -2, -1, 10, 6);

        Assert.Equal(fromDense.ReadOnlyPixels.ToArray(), fromPadded.ReadOnlyPixels.ToArray());
    }

    [Theory]
    [InlineData(EsImageQuality.Low)]
    [InlineData(EsImageQuality.Mid)]
    [InlineData(EsImageQuality.High)]
    internal void 縮小_行の余白を含むソースでも余白の画素を混ぜない(EsImageQuality quality)
    {
        // 並列実行の経路（縮小先の画素数がしきい値以上）も通すため、400x300 → 長辺 320 を使う
        var dense = CreateGradientPixels(400, 300);
        var stride = (400 * EsReadOnlyBitmap.BytesPerPixel) + 64;
        var padded = CreatePadded(dense, 400, 300, stride, padding: 0xAB);

        using var expected = EsReadOnlyBitmap.FromPixelCopy(400, 300, dense);
        using var fromDense = expected.Resize(320, quality);
        using var fromPadded = EsBitmapOperations.Resize(padded, 400, 300, stride, 320, quality);

        Assert.Equal(fromDense.ReadOnlyPixels.ToArray(), fromPadded.ReadOnlyPixels.ToArray());
    }

    [Fact]
    internal void 減色_行の余白は色として数えない()
    {
        // 密な画素は 2 色だけ（余白は 3 色目にして、数えてしまうと結果が変わるようにする）
        var dense = new byte[8 * 4 * EsReadOnlyBitmap.BytesPerPixel];
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var i = ((y * 8) + x) * EsReadOnlyBitmap.BytesPerPixel;
                var value = (byte)((x < 4) ? 10 : 200);
                dense[i] = value;
                dense[i + 1] = value;
                dense[i + 2] = value;
                dense[i + 3] = 255;
            }
        }

        var stride = (8 * EsReadOnlyBitmap.BytesPerPixel) + 8;
        var padded = CreatePadded(dense, 8, 4, stride, padding: 0xAB);

        using var expected = EsReadOnlyBitmap.FromPixelCopy(8, 4, dense);
        using var fromDense = expected.ReduceColors(2);
        using var fromPadded = EsBitmapOperations.ReduceColors(padded, 8, 4, stride, 2);

        Assert.Equal(fromDense.ReadOnlyPixels.ToArray(), fromPadded.ReadOnlyPixels.ToArray());
    }

    /// <summary>勾配のある密な画素を生成する（位置ごとに値が異なるため、行や列のずれを検出できる）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>画素（BGRA8888。 幅 × 4 × 高さ バイト）。</returns>
    private static byte[] CreateGradientPixels(int width, int height)
    {
        var pixels = new byte[width * height * EsReadOnlyBitmap.BytesPerPixel];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = ((y * width) + x) * EsReadOnlyBitmap.BytesPerPixel;
                pixels[i] = (byte)(x * 7);
                pixels[i + 1] = (byte)(y * 11);
                pixels[i + 2] = (byte)((x + y) * 5);
                pixels[i + 3] = 255;
            }
        }

        return pixels;
    }

    /// <summary>密な画素へ行の余白を足した画素を生成する（余白は指定した値で埋める）。</summary>
    /// <param name="dense">密な画素（幅 × 4 × 高さ バイト）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride">生成する画素の 1 ラインあたりのバイト数（幅 × 4 より大きいこと）。</param>
    /// <param name="padding">余白を埋める値（画素として数えられていないことを検出するための目印）。</param>
    /// <returns>余白付きの画素（<c>stride × 高さ</c> バイト）。</returns>
    private static byte[] CreatePadded(byte[] dense, int width, int height, int stride, byte padding)
    {
        var rowBytes = width * EsReadOnlyBitmap.BytesPerPixel;
        var pixels = new byte[stride * height];
        pixels.AsSpan().Fill(padding);
        for (var y = 0; y < height; y++)
        {
            dense.AsSpan(y * rowBytes, rowBytes).CopyTo(pixels.AsSpan(y * stride, rowBytes));
        }

        return pixels;
    }

    /// <summary>画像で使われている色の数を数える。</summary>
    /// <param name="bitmap">対象の画像。</param>
    /// <returns>異なる色の数。</returns>
    private static int CountDistinctColors(EsReadOnlyBitmap bitmap)
    {
        var colors = new HashSet<uint>();
        var pixels = bitmap.ReadOnlyPixels;
        for (var i = 0; i < pixels.Length; i += EsReadOnlyBitmap.BytesPerPixel)
        {
            colors.Add(
                (uint)((pixels[i] << 24) | (pixels[i + 1] << 16) | (pixels[i + 2] << 8) | pixels[i + 3]));
        }

        return colors.Count;
    }

    /// <summary>書き換え可能な画像で使われている色の数を数える。</summary>
    /// <param name="bitmap">対象の画像。 書き換えない。</param>
    /// <returns>異なる色の数。</returns>
    /// <remarks>凍結しない（複製して数える）。 減色の結果のような一時的な画像を計測するために使う。</remarks>
    private static int CountDistinctColors(EsBitmap bitmap)
    {
        using var read = EsReadOnlyBitmap.FromBitmap(bitmap);
        return CountDistinctColors(read);
    }

    /// <summary>画素を左上から右下の順に列挙する（色は ARGB の 32 ビット）。</summary>
    /// <param name="bitmap">対象の画像。 書き換えない。</param>
    /// <returns>色の並び（左上から右下。 長さは画素数）。</returns>
    private static uint[] EnumerateColors(EsReadOnlyBitmap bitmap)
    {
        var pixels = bitmap.ReadOnlyPixels;
        var stride = bitmap.Stride;
        var colors = new uint[bitmap.Width * bitmap.Height];
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var i = (y * stride) + (x * EsReadOnlyBitmap.BytesPerPixel);
                colors[(y * bitmap.Width) + x] =
                    ((uint)pixels[i + 3] << 24) | ((uint)pixels[i + 2] << 16) | ((uint)pixels[i + 1] << 8) | pixels[i];
            }
        }

        return colors;
    }

    /// <summary>書き換え可能な画像の画素を左上から右下の順に列挙する。</summary>
    /// <param name="bitmap">対象の画像。 書き換えない。</param>
    /// <returns>色の並び（左上から右下。 長さは画素数）。</returns>
    private static uint[] EnumerateColors(EsBitmap bitmap)
    {
        using var read = EsReadOnlyBitmap.FromBitmap(bitmap);
        return EnumerateColors(read);
    }
}
