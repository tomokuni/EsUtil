using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Feature.Loupe.AvaloniaUI.Internal;
using Xunit;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests.Internal;

/// <summary><see cref="EsReadOnlyBitmap"/> の Avalonia WriteableBitmap 変換テスト（ヘッドレス依存・直列実行）。</summary>
/// <remarks>
/// <c>Bitmap.ToEsBitmap()</c> / <c>bitmap.ToAvaloniaWriteableBitmap()</c> は
/// <c>WriteableBitmap</c> の生成・ピクセルコピーを伴うため、Avalonia ヘッドレスプラットフォームが必要。<br/>
/// そのため <see cref="AvaloniaHeadlessGroup"/> 内で直列実行する（純粋計算テストは
/// <c>EsBitmapTests</c> で並列実行）。<br/>
/// サイズ・ストライドを主に検証する（ピクセル値はヘッドレス環境で不安定なため検証しない）。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class EsBitmapAvaloniaTests
{
    /// <summary>テスト用の WriteableBitmap を生成する。</summary>
    private static WriteableBitmap CreateBitmap(int width, int height)
        => new(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    [AvaloniaFact]
    internal void ToAvaloniaWriteableBitmap_サイズが一致する()
    {
        using var bmp = EsBitmap.Create(8, 8);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);

        var result = read.ToAvaloniaWriteableBitmap();

        Assert.Equal(new PixelSize(8, 8), result.PixelSize);
    }

    [AvaloniaFact]
    internal void ToEsBitmap_サイズが一致する()
    {
        var src = CreateBitmap(8, 8);

        using var bmp = src.ToEsBitmap();

        Assert.Equal(8, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);
    }

    [AvaloniaFact]
    internal void Avalonia_往復変換でサイズが一致する()
    {
        // Bitmap → EsReadOnlyBitmap → Bitmap の往復でサイズが保持される。
        var src = CreateBitmap(12, 10);

        using var bmp = src.ToEsBitmap();
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        var back = read.ToAvaloniaWriteableBitmap();

        Assert.Equal(src.PixelSize, back.PixelSize);
    }

    [AvaloniaFact]
    internal void ToEsBitmap_元Bitmapは変更されない()
    {
        var src = CreateBitmap(8, 8);

        using var bmp = src.ToEsBitmap();

        // 元のサイズが保持される（複製のため元は変更されない）
        Assert.Equal(new PixelSize(8, 8), src.PixelSize);
    }

    [AvaloniaFact]
    internal void ToEsBitmap_内部ストライドが幅4倍以上になる()
    {
        // Avalonia の WriteableBitmap.RowBytes はプラットフォーム依存で幅の4バイト以上になり得る。
        // EsReadOnlyBitmap は幅の4バイト以上を保持し、以降のピクセル演算で正しく扱う。
        var src = CreateBitmap(7, 5);

        using var bmp = src.ToEsBitmap();

        Assert.Equal(7, bmp.Size.Width);
        Assert.Equal(5, bmp.Size.Height);
        Assert.True(bmp.Stride >= 7 * 4);
    }
}
