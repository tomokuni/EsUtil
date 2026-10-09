using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Loupe.MewUI.Internal;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;

namespace EsUtil.Feature.Loupe.MewUI.Tests.Internal;

/// <summary><see cref="SkiaFill"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 矩形の塗りは MewUI 版の描画層と両 UI の文字描画が共用するため、<br/>
/// 「指定色で塗る」「空の一覧では何もしない」「角丸 0 は四角形」を画素で検証する。
/// </remarks>
public class SkiaFillTests
{
    /// <summary>テスト用の描画先（透明で初期化したビットマップとキャンバス）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した描画先。 破棄は呼び出し側。</returns>
    private static (SKBitmap Bitmap, SKCanvas Canvas) CreateTarget(int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        return (bitmap, new SKCanvas(bitmap));
    }

    /// <summary>矩形を指定色で塗ることを検証する。</summary>
    [Fact]
    internal void Rect_矩形を塗る()
    {
        var (bitmap, canvas) = CreateTarget(40, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaFill.Rect(canvas, new EsRect(10, 5, 20, 10), DrawingColor.FromArgb(255, 0, 0, 255));

            Assert.Equal(new SKColor(0, 0, 255, 255), bitmap.GetPixel(20, 10));
            // 透明の比較は非乗算の読み戻しと一致しないためアルファで判定する
            Assert.Equal(0, bitmap.GetPixel(5, 5).Alpha);
            Assert.Equal(0, bitmap.GetPixel(20, 20).Alpha);
        }
    }

    /// <summary>半透明の色がそのまま塗られることを検証する。</summary>
    [Fact]
    internal void Rect_半透明の色をそのまま塗る()
    {
        var (bitmap, canvas) = CreateTarget(40, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaFill.Rect(canvas, new EsRect(0, 0, 40, 40), DrawingColor.FromArgb(128, 255, 0, 0));

            Assert.Equal(new SKColor(255, 0, 0, 128), bitmap.GetPixel(20, 20));
        }
    }

    /// <summary>複数の矩形を同じ色でまとめて塗ることを検証する。</summary>
    [Fact]
    internal void Rects_複数の矩形を塗る()
    {
        var (bitmap, canvas) = CreateTarget(40, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaFill.Rects(canvas, [new EsRect(0, 0, 40, 5), new EsRect(0, 10, 40, 5)], DrawingColor.White);

            Assert.Equal(SKColors.White, bitmap.GetPixel(20, 2));
            Assert.Equal(SKColors.White, bitmap.GetPixel(20, 12));
            // 塗らない帯は透明のまま
            Assert.Equal(0, bitmap.GetPixel(20, 20).Alpha);
        }
    }

    /// <summary>空の一覧では何も塗らないことを検証する。</summary>
    [Fact]
    internal void Rects_空の一覧なら何も塗らない()
    {
        var (bitmap, canvas) = CreateTarget(40, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaFill.Rects(canvas, [], DrawingColor.White);

            Assert.Equal(0, bitmap.GetPixel(20, 20).Alpha);
        }
    }

    /// <summary>角丸半径 0 は四角形として塗ることを検証する。</summary>
    [Fact]
    internal void RoundRect_半径0なら四角形として塗る()
    {
        var (bitmap, canvas) = CreateTarget(40, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaFill.RoundRect(canvas, new EsRect(10, 10, 20, 20), cornerRadius: 0, DrawingColor.White);

            // 四隅まで塗られる
            Assert.Equal(SKColors.White, bitmap.GetPixel(10, 10));
            Assert.Equal(SKColors.White, bitmap.GetPixel(29, 29));
        }
    }

    /// <summary>角丸半径を指定すると四隅が塗られないことを検証する。</summary>
    [Fact]
    internal void RoundRect_半径ありなら四隅を塗らない()
    {
        var (bitmap, canvas) = CreateTarget(40, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaFill.RoundRect(canvas, new EsRect(10, 10, 20, 20), cornerRadius: 6, DrawingColor.White);

            // 四隅は丸められて塗られない（辺の中央は塗られる）
            Assert.Equal(0, bitmap.GetPixel(10, 10).Alpha);
            Assert.Equal(SKColors.White, bitmap.GetPixel(10, 20));
            Assert.Equal(SKColors.White, bitmap.GetPixel(20, 10));
        }
    }

    /// <summary>負の半径は四角形として扱うことを検証する（補正済みの値だけが来るが、防御として固定する）。</summary>
    [Fact]
    internal void RoundRect_負の半径は四角形として塗る()
    {
        var (bitmap, canvas) = CreateTarget(40, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaFill.RoundRect(canvas, new EsRect(10, 10, 20, 20), cornerRadius: -1, DrawingColor.White);

            Assert.Equal(SKColors.White, bitmap.GetPixel(10, 10));
        }
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    /// <param name="kind">呼び出すメソッド（0 = Rect / 1 = Rects / 2 = RoundRect）。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    internal void Fill_キャンバスがnullなら例外(int kind)
    {
        var rect = new EsRect(0, 0, 10, 10);

        Assert.Throws<ArgumentNullException>(() =>
        {
            switch (kind)
            {
                case 0: SkiaFill.Rect(null!, rect, DrawingColor.White); break;
                case 1: SkiaFill.Rects(null!, [rect], DrawingColor.White); break;
                default: SkiaFill.RoundRect(null!, rect, 0, DrawingColor.White); break;
            }
        });
    }

    /// <summary>一覧が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void Rects_一覧がnullなら例外()
    {
        var (bitmap, canvas) = CreateTarget(10, 10);
        using (bitmap)
        using (canvas)
        {
            Assert.Throws<ArgumentNullException>(() => SkiaFill.Rects(canvas, null!, DrawingColor.White));
        }
    }
}
