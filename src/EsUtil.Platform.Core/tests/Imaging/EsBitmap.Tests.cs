using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>
/// <see cref="EsBitmap"/> の単体テスト。
/// </summary>
/// <remarks>
/// <see cref="EsBitmap"/> は静的共有状態を持たないため、本テストは UI フレームワークの初期化に依存しない
/// （コレクションに属さない <c>[Fact]</c> として並列実行される）。<br/>
/// UI 固有の相互変換は各 UI 層のテストが検証する（本テストはピクセル操作のみを対象とする）。
/// </remarks>
public class EsBitmapTests
{
    /// <summary>指定サイズの空のビットマップを生成する。</summary>
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

        // サイズが指定どおり
        Assert.Equal(6, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);

        // 全ピクセルが指定色（BGRA）になる
        var px = bmp.GetPixel(3, 4);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)255, px.R);
        Assert.Equal((byte)255, px.A);
    }

    [Fact]
    internal void Create_破棄後はIsDisposedが真になる()
    {
        var bmp = Create();
        bmp.Dispose();
        Assert.True(bmp.IsDisposed);
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

    /// <summary>未初期化確保はサイズとバッファを用意する（ピクセルの値は保証しない）ことを検証する。</summary>
    [Fact]
    internal void CreateUninitialized_サイズとバッファを用意する()
    {
        using var bmp = EsBitmap.CreateUninitialized(6, 8);

        Assert.Equal(6, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);
        Assert.Equal(6 * 4, bmp.Stride);
        Assert.NotEqual(0, bmp.Address);
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
        Assert.Equal(0, bmp.Address);
    }

    /// <summary>0 サイズ（空）で生成しても例外にならず、ピクセルを持たないことを検証する。</summary>
    [Fact]
    internal void Create_空のサイズなら例外にならない()
    {
        using var bmp = EsBitmap.Create(new EsSizeInt(0, 0));

        Assert.True(bmp.Size.IsEmpty);
        Assert.Equal(0, bmp.Address);
        // Clear も例外にならない（ピクセルを持たないため）
        bmp.Clear(System.Drawing.Color.Red);
    }

    [Fact]
    internal void Dispose_二重破棄しても例外にならない()
    {
        var bmp = Create();
        bmp.Dispose();
        bmp.Dispose();
        Assert.True(bmp.IsDisposed);
    }

    // ====================================================================
    // 画像源（IEsImageSource）
    // ====================================================================

    [Fact]
    internal void GetEsBitmap_自分自身を借用で返す()
    {
        using var bmp = Create(8, 6);

        // 恒等（コピーなし）。 借用のため別インスタンスを作らない
        Assert.Same(bmp, ((IEsImageSource)bmp).GetEsBitmap());
    }

    [Fact]
    internal void WidthとHeightが実装の面に現れる()
    {
        using var bmp = Create(1920, 1080);
        IEsImageSource source = bmp;

        Assert.Equal(1920, source.Width);
        Assert.Equal(1080, source.Height);
    }

    // ====================================================================
    // ピクセル操作
    // ====================================================================

    [Fact]
    internal void SetPixel_設定したピクセルを取得できる()
    {
        using var bmp = Create();
        bmp.SetPixel(1, 2, b: 10, g: 20, r: 30, a: 255);
        var px = bmp.GetPixel(1, 2);
        Assert.Equal((byte)10, px.B);
        Assert.Equal((byte)20, px.G);
        Assert.Equal((byte)30, px.R);
        Assert.Equal((byte)255, px.A);
    }

    [Fact]
    internal void GetPixel_未初期化領域のピクセルを取得できる()
    {
        using var bmp = Create();
        var px = bmp.GetPixel(0, 0);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)0, px.R);
        Assert.Equal((byte)0, px.A);
    }

    /// <summary>座標型（<see cref="EsPointInt" />）のオーバーロードが成分オーバーロードと一致することを検証する。</summary>
    [Fact]
    internal void GetSetPixel_座標型のオーバーロードは成分と一致する()
    {
        using var bmp = Create();
        bmp.SetPixel(new EsPointInt(1, 2), System.Drawing.Color.FromArgb(255, 30, 20, 10));

        var (b, g, r, a) = bmp.GetPixel(1, 2);
        Assert.Equal((byte)10, b);
        Assert.Equal((byte)20, g);
        Assert.Equal((byte)30, r);
        Assert.Equal((byte)255, a);

        // 座標型のオーバーロードは色型（Color）を返す（成分は同じ）
        var color = bmp.GetPixel(new EsPointInt(1, 2));
        Assert.Equal((byte)10, color.B);
        Assert.Equal((byte)20, color.G);
        Assert.Equal((byte)30, color.R);
        Assert.Equal((byte)255, color.A);
    }

    [Fact]
    internal void Clear_全ピクセルが指定色になる()
    {
        using var bmp = Create();
        bmp.Clear(b: 0, g: 0, r: 255, a: 255); // 赤・不透明
        var px = bmp.GetPixel(3, 3);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)255, px.R);
        Assert.Equal((byte)255, px.A);
    }

    [Fact]
    internal void Clear_アルファ乗算済みの色で塗りつぶす()
    {
        using var bmp = Create();
        // 50% アルファの赤（R=255, A=128）
        bmp.Clear(b: 0, g: 0, r: 255, a: 128);
        var px = bmp.GetPixel(0, 0);
        Assert.Equal((byte)128, px.A);
    }

    [Fact]
    internal void Clone_独立した複製を生成する()
    {
        using var bmp = Create();
        bmp.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 255);
        using var clone = bmp.Clone();
        clone.SetPixel(0, 0, b: 9, g: 8, r: 7, a: 255);
        Assert.Equal((byte)1, bmp.GetPixel(0, 0).B);
        Assert.Equal((byte)9, clone.GetPixel(0, 0).B);
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
        var px = bmp.GetPixel(2, 3);
        Assert.Equal((byte)255, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)0, px.R);
        Assert.Equal((byte)128, px.A); // アルファが喪失（0 に強制）されない
    }

    [Fact]
    internal void SetPixel_完全透明アルファも保持する()
    {
        using var bmp = Create();
        bmp.SetPixel(0, 0, b: 10, g: 20, r: 30, a: 0);
        var px = bmp.GetPixel(0, 0);
        Assert.Equal((byte)0, px.A);
        Assert.Equal((byte)10, px.B);
        Assert.Equal((byte)20, px.G);
        Assert.Equal((byte)30, px.R);
    }

    [Fact]
    internal void Clone_アルファを保持したまま複製する()
    {
        using var bmp = Create();
        bmp.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 128);
        using var clone = bmp.Clone();
        var px = clone.GetPixel(0, 0);
        Assert.Equal((byte)1, px.B);
        Assert.Equal((byte)128, px.A);
    }

    // ====================================================================
    // Crop（矩形切り出し）
    // ====================================================================

    [Fact]
    internal void Crop_範囲内の矩形を切り出す()
    {
        using var src = Create(10, 10);
        src.Clear(b: 0, g: 0, r: 255, a: 255); // 赤

        using var result = src.Crop(new EsRectInt(2, 2, 4, 4));

        Assert.Equal(4, result.Size.Width);
        Assert.Equal(4, result.Size.Height);
        // 切り出しピクセルが保持されている
        Assert.Equal((byte)255, result.GetPixel(0, 0).R);
    }

    [Fact]
    internal void Crop_幅0の矩形は例外を投げる()
    {
        using var src = Create(10, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => src.Crop(new EsRectInt(0, 0, 0, 4)));
    }

    [Fact]
    internal void Crop_高さ0の矩形は例外を投げる()
    {
        using var src = Create(10, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => src.Crop(new EsRectInt(0, 0, 4, 0)));
    }

    [Fact]
    internal void Crop_範囲外は透明パディングで要求サイズを返す()
    {
        using var src = Create(10, 10);
        src.Clear(b: 0, g: 0, r: 255, a: 255); // 赤

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
        using var src = Create(10, 10);
        src.Clear(b: 0, g: 0, r: 255, a: 255); // 赤

        // 矩形がソースと交差しない場合でも、要求サイズの全透明ビットマップを返す
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
        using var src = Create(10, 10);
        src.Clear(b: 0, g: 0, r: 255, a: 255); // 赤

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
        using var src = Create(10, 10);
        src.Clear(b: 0, g: 0, r: 255, a: 255); // 赤

        // 要求矩形がソースと全く重ならない（左上が負で大きく外れる）場合も、要求サイズの全透明を返す
        using var result = src.Crop(new EsRectInt(-20, -20, 5, 5));

        Assert.Equal(5, result.Size.Width);
        Assert.Equal(5, result.Size.Height);
        Assert.Equal((byte)0, result.GetPixel(0, 0).A);
        Assert.Equal((byte)0, result.GetPixel(4, 4).A);
    }

    [Fact]
    internal void Crop_破棄後は例外を投げる()
    {
        var src = Create(10, 10);
        src.Dispose();

        Assert.Throws<ObjectDisposedException>(() => src.Crop(new EsRectInt(0, 0, 4, 4)));
    }

    [Fact]
    internal void Crop_全面を切り出す場合はパディングを持たない()
    {
        // パディングが無い場合は透明化を行わないため、全ピクセルがコピー元の色で埋まることを固定する
        using var src = Create(4, 4);
        src.Clear(b: 0, g: 0, r: 255, a: 255); // 赤

        using var result = src.Crop(new EsRectInt(0, 0, 4, 4));

        Assert.Equal((byte)255, result.GetPixel(0, 0).R);
        Assert.Equal((byte)255, result.GetPixel(3, 3).R);
        Assert.Equal((byte)255, result.GetPixel(3, 3).A);
    }

    [Fact]
    internal void Crop_パディングは上端と下端と中間行の左右をすべて透明にする()
    {
        using var src = Create(4, 4);
        src.Clear(b: 0, g: 0, r: 255, a: 255); // 赤

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
        using var src = Create(0, 0);

        using var result = src.Crop(new EsRectInt(0, 0, 4, 4));

        Assert.Equal(4, result.Size.Width);
        Assert.Equal(0, result.GetPixel(0, 0).A);
    }

    [Fact]
    internal void Clone_空のサイズでも例外にならない()
    {
        using var bmp = Create(0, 0);
        using var clone = bmp.Clone();

        Assert.True(clone.Size.IsEmpty);
        Assert.Equal(0, clone.Address);
    }
}
