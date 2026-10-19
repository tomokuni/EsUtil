using EsUtil.Ui.WinUI3.Thumbnail.Internal;
using SkiaSharp;
using Xunit;

namespace EsUtil.Ui.WinUI3.Thumbnail.Tests;

/// <summary>影の帯を Skia のキャンバスへ描く手順（<see cref="SkiaShadowRenderer"/>）の単体テスト。</summary>
/// <remarks>
/// 描画はウィンドウを必要としない（<see cref="SKBitmap"/> 上の実寸の画素を検査する）。<br/>
/// 帯を単色の矩形として塗るだけであるため、どの UI 版でも同じ結果になることを画素で確かめる。
/// </remarks>
public class SkiaShadowRendererTests
{
    /// <summary>帯の矩形だけが指定の色で塗られることを検証する。</summary>
    [Fact]
    internal void Draw_帯の矩形を指定の色で塗る()
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bitmap);

        SkiaShadowRenderer.Draw(canvas, [new ThumbnailShadowBand(2, 1, 3, 2, 0xFFFF0000)]);
        canvas.Flush();

        Assert.Equal(new SKColor(255, 0, 0, 255), bitmap.GetPixel(2, 1));
        Assert.Equal(new SKColor(255, 0, 0, 255), bitmap.GetPixel(4, 2));
        Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha);
        Assert.Equal(0, bitmap.GetPixel(5, 1).Alpha);
    }

    /// <summary>透過度が色のアルファとして反映されることを検証する。</summary>
    [Fact]
    internal void Draw_透過度をアルファとして反映する()
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bitmap);

        SkiaShadowRenderer.Draw(canvas, [new ThumbnailShadowBand(0, 0, 4, 4, 0x80008000)]);
        canvas.Flush();

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(128, pixel.Alpha);
        Assert.Equal(0, pixel.Red);
        Assert.Equal(128, pixel.Green);
    }

    /// <summary>null のキャンバスを拒否することを検証する。</summary>
    [Fact]
    internal void Draw_nullのキャンバスを拒否する()
        => Assert.Throws<ArgumentNullException>(() => SkiaShadowRenderer.Draw(null!, []));
}
