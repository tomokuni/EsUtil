using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Feature.Loupe.AvaloniaUI.Internal;
using Xunit;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests.Internal;

/// <summary>
/// <see cref="EsBitmapAvaloniaAdapter"/> のテスト（コアの画像を Avalonia の表示境界の画像として公開するアダプタ）。
/// </summary>
/// <remarks>
/// <see cref="Bitmap"/> への射影（<c>ToAvaloniaBitmap</c>）が Avalonia の描画バックエンドを必要とするため、
/// 本クラスは <c>[AvaloniaFact]</c> で検証する（<c>[Fact]</c> と混在させると並列実行で競合する）。<br/>
/// 「真実は 1 つ・射影は初回のみ」という不変条件（同じインスタンスが返ること）を中心に検証する。<br/>
/// 実 Skia の描画は共有状態を持つため <see cref="AvaloniaSkiaGroup"/> で直列化する。
/// </remarks>
[Collection("AvaloniaSkia")]
public class EsBitmapAvaloniaAdapterTests
{
    // ====================================================================
    // EsReadOnlyBitmap を真実とした場合
    // ====================================================================

    [AvaloniaFact]
    internal void 読み取り専用の画像を真実とするとGetEsBitmapは恒等で返す()
    {
        using var bmp = EsBitmap.Create(8, 6);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var adapter = new EsBitmapAvaloniaAdapter(read);

        // 恒等（コピーなし。 借用）
        Assert.Same(read, adapter.GetEsBitmap());
    }

    [AvaloniaFact]
    internal void 読み取り専用の画像を真実とするとGetAvaloniaBitmapは初回のみ生成して以降は同じものを返す()
    {
        using var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Red);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var adapter = new EsBitmapAvaloniaAdapter(read);

        var first = adapter.GetAvaloniaBitmap();
        var second = adapter.GetAvaloniaBitmap();

        // 射影は初回の 1 枚だけ（以降は再利用する）
        Assert.Same(first, second);
        Assert.Equal(8, first.PixelSize.Width);
        Assert.Equal(6, first.PixelSize.Height);
    }

    [AvaloniaFact]
    internal void 真実を所有しないためDisposeで真実を破棄しない()
    {
        var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Red);
        bmp.SetReadOnly();
        var read = EsReadOnlyBitmap.FromBitmap(bmp);
        // 画素は読み取り専用の面が共有して引き継ぐため、書き換え可能の実体はここで解放する
        bmp.Dispose();
        var adapter = new EsBitmapAvaloniaAdapter(read);
        _ = adapter.GetAvaloniaBitmap();

        adapter.Dispose();

        // 真実は所有しない（破棄はホスト）。 射影だけを解放する
        Assert.False(read.IsDisposed);
        read.Dispose();
    }

    // ====================================================================
    // Avalonia の Bitmap を包む場合
    // ====================================================================

    [AvaloniaFact]
    internal void AvaloniaのBitmapはコアへ変換してから包む()
    {
        using var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Blue);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var avalonia = read.ToAvaloniaBitmap();

        // 射影元はコアの画像に限るため、Avalonia の画像はコアへ変換してから包む（変換の責務が呼び出し側に現れる）
        using var es = avalonia.ToEsBitmap();
        es.SetReadOnly();
        using var esRead = EsReadOnlyBitmap.FromBitmap(es);
        using var adapter = new EsBitmapAvaloniaAdapter(esRead);

        Assert.Equal(8, adapter.GetEsBitmap().Width);
        Assert.Equal(6, adapter.GetEsBitmap().Height);
        // 表示用の射影は新しく作られる（元の Avalonia 画像とは別のインスタンス）
        Assert.NotSame(avalonia, adapter.GetAvaloniaBitmap());
        Assert.Equal(8, adapter.GetAvaloniaBitmap().PixelSize.Width);
    }

    // ====================================================================
    // 共通（サイズ・破棄・未設定）
    // ====================================================================

    [AvaloniaFact]
    internal void サイズが真実の画像と一致する()
    {
        using var bmp = EsBitmap.Create(12, 7);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var adapter = new EsBitmapAvaloniaAdapter(read);
        Assert.Equal(new Size(12, 7), adapter.Size);
        Assert.Equal(12, adapter.GetEsBitmap().Width);
        Assert.Equal(7, adapter.GetEsBitmap().Height);
    }

    [AvaloniaFact]
    internal void Disposeを複数回呼んでも安全である()
    {
        using var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Red);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        var adapter = new EsBitmapAvaloniaAdapter(read);

        adapter.Dispose();
        adapter.Dispose();
    }

    [AvaloniaFact]
    internal void 幅と高さはGetEsBitmapから取得する()
    {
        using var bmp = EsBitmap.Create(12, 7);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var adapter = new EsBitmapAvaloniaAdapter(read);

        Assert.Equal(12, adapter.GetEsBitmap().Width);
        Assert.Equal(7, adapter.GetEsBitmap().Height);
    }

    [AvaloniaFact]
    internal void 真実が破棄済みの場合は取得で例外を投げる()
    {
        var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Red);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        bmp.Dispose();
        using var adapter = new EsBitmapAvaloniaAdapter(read);
        read.Dispose();

        // 破棄済みの真実は渡さない（null を返して呼び出し側へ判定を強いない）
        Assert.Throws<ObjectDisposedException>(() => adapter.GetEsBitmap());
        Assert.Throws<ObjectDisposedException>(() => adapter.GetAvaloniaBitmap());
    }

    [AvaloniaFact]
    internal void 真実が破棄済みの場合は寸法の取得でも例外を投げる()
    {
        var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Red);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        bmp.Dispose();
        using var adapter = new EsBitmapAvaloniaAdapter(read);
        read.Dispose();

        // 表示の寸法も「無効な画像の値」を返さない（破棄済みを正常値として扱う誤りを防ぐ）
        Assert.Throws<ObjectDisposedException>(() => adapter.Size);
    }

    [AvaloniaFact]
    internal void IsDisposedは真実の破棄状態を返す()
    {
        var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Red);
        bmp.SetReadOnly();
        var read = EsReadOnlyBitmap.FromBitmap(bmp);
        bmp.Dispose();
        var adapter = new EsBitmapAvaloniaAdapter(read);
        var truth = adapter.GetEsBitmap();

        Assert.False(truth.IsDisposed);

        read.Dispose();

        // 真実の破棄が面から見える（描画側が破棄済みを描かない判定に使う）
        Assert.True(truth.IsDisposed);
        adapter.Dispose();
    }

    [AvaloniaFact]
    internal void IsDisposedは本クラスのDisposeでは真にならない()
    {
        using var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Red);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        var adapter = new EsBitmapAvaloniaAdapter(read);
        var truth = adapter.GetEsBitmap();

        adapter.Dispose();

        // 本クラスの Dispose は射影だけを解放する（真実は生存し、再び射影を作れる）
        Assert.False(truth.IsDisposed);
        Assert.Equal(8, truth.Width);
    }

    [AvaloniaFact]
    internal void GetEsBitmapはコアのGetPixelのオーバーロードを公開する()
    {
        using var bmp = EsBitmap.Create(4, 4, System.Drawing.Color.FromArgb(255, 30, 20, 10));
        bmp.SetReadOnly();
        using var readOnly = EsReadOnlyBitmap.FromBitmap(bmp);
        using var adapter = new EsBitmapAvaloniaAdapter(readOnly);
        var read = adapter.GetEsBitmap();

        var (b, g, r, a) = read.GetPixel(1, 2);
        var color = read.GetPixel(new EsPointInt(1, 2));

        Assert.Equal(b, color.B);
        Assert.Equal(g, color.G);
        Assert.Equal(r, color.R);
        Assert.Equal(a, color.A);
    }

    // ====================================================================
    // 画像を所有する生成（Adopt）
    // ====================================================================

    [AvaloniaFact]
    internal void Adoptで生成すると渡した画像は解放される()
    {
        var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Red);
        var adapter = EsBitmapAvaloniaAdapter.Adopt(bmp);

        // 引き取った画像は凍結して読み取り専用の面へ 0 コピーで移し、書き換え可能の実体はその場で解放する
        Assert.True(bmp.IsDisposed);

        adapter.Dispose();
    }

    [AvaloniaFact]
    internal void Adoptの引数nullはArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => EsBitmapAvaloniaAdapter.Adopt(null!));

    [AvaloniaFact]
    internal void コンストラクタ_nullはArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => new EsBitmapAvaloniaAdapter(null!));

    // ====================================================================
    // 表示サイズの上限（DisplayMaxSize）
    // ====================================================================

    [AvaloniaFact]
    internal void 表示上限を指定するとSizeは縮小後の寸法を返す()
    {
        using var bmp = EsBitmap.Create(200, 100);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var adapter = new EsBitmapAvaloniaAdapter(read, displayMaxSize: 50);

        // Avalonia のレイアウトと描画が使う Size は縮小後の寸法になる
        Assert.Equal(new Size(50, 25), adapter.Size);
        // コアの読み取り面は真実の寸法のまま（ピクセルと矛盾しない）
        Assert.Equal(200, adapter.GetEsBitmap().Width);
        Assert.Equal(100, adapter.GetEsBitmap().Height);
        Assert.Equal(new EsSizeInt(200, 100), adapter.GetEsBitmap().Size);
    }

    [AvaloniaFact]
    internal void 表示上限を指定すると描画も縮小した射影を使う()
    {
        using var bmp = EsBitmap.Create(200, 100, System.Drawing.Color.Red);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var adapter = new EsBitmapAvaloniaAdapter(read, displayMaxSize: 50);
        using var target = new RenderTargetBitmap(new PixelSize(50, 25), new Vector(96, 96));

        using (var context = target.CreateDrawingContext())
        {
            // Avalonia が渡す描画元矩形は Size（縮小後）と一致する
            adapter.Draw(context, new Rect(0, 0, adapter.Size.Width, adapter.Size.Height), new Rect(0, 0, 50, 25));
        }

        // 描画が例外なく完了し、射影が縮小後の寸法で作られている
        Assert.Equal(50, adapter.Size.Width);
    }

    [AvaloniaFact]
    internal void 表示上限は保存用の射影に影響しない()
    {
        using var bmp = EsBitmap.Create(200, 100);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var adapter = new EsBitmapAvaloniaAdapter(read, displayMaxSize: 50);

        // 保存・クリップボード用は等倍（表示用の縮小と分ける）
        var full = adapter.GetAvaloniaBitmap();
        Assert.Equal(200, full.PixelSize.Width);
        Assert.Equal(100, full.PixelSize.Height);
    }

    [AvaloniaFact]
    internal void 表示上限が負ならArgumentOutOfRangeExceptionを投げる()
    {
        using var bmp = EsBitmap.Create(4, 4);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);

        Assert.Throws<ArgumentOutOfRangeException>(() => new EsBitmapAvaloniaAdapter(read, displayMaxSize: -1));
    }

    [AvaloniaFact]
    internal void 表示上限が上限以内なら等倍で扱う()
    {
        using var bmp = EsBitmap.Create(40, 20, System.Drawing.Color.Red);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var adapter = new EsBitmapAvaloniaAdapter(read, displayMaxSize: 50);
        using var target = new RenderTargetBitmap(new PixelSize(40, 20), new Vector(96, 96));

        Assert.Equal(new Size(40, 20), adapter.Size);

        using (var context = target.CreateDrawingContext())
        {
            // 縮小が不要な場合も描画できる（等倍の射影を共用し、同じ寸法の複製を 2 つ作らない）
            adapter.Draw(context, new Rect(0, 0, 40, 20), new Rect(0, 0, 40, 20));
        }

        Assert.Equal(new Size(40, 20), adapter.Size);
    }

    [AvaloniaFact]
    internal void 表示上限と長辺が等しい場合は縮小しない()
    {
        using var bmp = EsBitmap.Create(40, 20);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var adapter = new EsBitmapAvaloniaAdapter(read, displayMaxSize: 40);

        // 長辺 = 上限（縮小の必要なし。 コアの縮小も等倍の複製を返す）
        Assert.Equal(new Size(40, 20), adapter.Size);
    }
}
