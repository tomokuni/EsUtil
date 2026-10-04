using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;

namespace EsUtil.Feature.Loupe.MewUI.Tests;

/// <summary><see cref="LoupeRenderedEventArgs"/>（ルーペの描画完了通知）のテスト。</summary>
/// <remarks>
/// イベントの発火は実際の描画（ウィンドウ）を必要とするため、引数の契約（保持する値）のみを検証する。<br/>
/// 描画完了時のオーバーレイ描画はホスト（<c>FadeMaskDragRectControl.MewUI</c>）のテストが担う。
/// </remarks>
public class LoupeRenderedEventArgsTests
{
    /// <summary>保持した値がそのまま取得できることを検証する。</summary>
    [Fact]
    internal void 保持した値を取得できる()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(32, 32));
        using var canvas = new SKCanvas(bitmap);
        var topLeft = new EsPoint(4, 6);

        var args = new LoupeRenderedEventArgs(canvas, 32, topLeft, 2.5);

        Assert.Same(canvas, args.Canvas);
        Assert.Equal(32, args.ClientPixelSize);
        Assert.Equal(topLeft, args.CaptureTopLeft);
        Assert.Equal(2.5, args.Scale);
    }

    /// <summary>中心が未設定の場合は切り出し左上が null になることを検証する。</summary>
    [Fact]
    internal void 中心が未設定の場合は切り出し左上がnullになる()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(32, 32));
        using var canvas = new SKCanvas(bitmap);

        var args = new LoupeRenderedEventArgs(canvas, 32, null, 1.0);

        Assert.Null(args.CaptureTopLeft);
    }
}
