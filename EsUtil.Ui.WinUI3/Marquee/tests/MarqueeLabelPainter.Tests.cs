using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Marquee.Core.Contract;
using EsUtil.Ui.WinUI3.Marquee.Internal;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;

namespace EsUtil.Ui.WinUI3.Marquee.Tests;

/// <summary><see cref="MarqueeLabelPainter"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 本 UI 版はラベルも描画面へ描くため、配置指示（矩形・余白・体裁）がそのまま画素になることを検証する。<br/>
/// 実寸の測定（<see cref="SkiaText.Measure"/>）と描画（<see cref="SkiaText.Draw"/>）が同じエンジンであることは、<br/>
/// プレゼンターが解決した計画を描くテスト（背景の内側に文字が収まること）で確かめる。
/// </remarks>
public class MarqueeLabelPainterTests
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

    /// <summary>配置指示 1 件だけを持つ描画計画を組み立てる。</summary>
    /// <param name="placement">配置指示。</param>
    /// <returns>組み立てた描画計画。</returns>
    private static MarqueeDrawPlan CreatePlan(params MarqueeLabelPlacement[] placement)
        => new(
            MarqueeAppearance.Default,
            new EsRect(0, 0, 200, 200),
            null,
            null,
            [],
            [],
            null,
            [],
            null,
            null,
            [MarqueeDrawLayer.Label],
            placement);

    /// <summary>ラベル 1 件の体裁（サイズラベルと同じ既定の体裁）。</summary>
    /// <param name="rect">配置矩形。</param>
    /// <param name="text">文言。</param>
    /// <param name="isVisible">表示可否。</param>
    /// <returns>配置指示。</returns>
    private static MarqueeLabelPlacement CreatePlacement(EsRect rect, string text, bool isVisible = true)
        => new(
            rect,
            4,
            4,
            text,
            14,
            DrawingColor.White,
            DrawingColor.FromArgb(153, 0, 0, 0),
            isVisible);

    /// <summary>領域内で白に近い（文字色の）画素の数を数える。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="rect">数える矩形。</param>
    /// <returns>白に近い画素数。</returns>
    private static int CountTextPixels(SKBitmap bitmap, EsRect rect)
    {
        var count = 0;

        for (var y = (int)rect.Y; y < (int)rect.Bottom; y++)
        {
            for (var x = (int)rect.X; x < (int)rect.Right; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha > 0 && pixel.Red > 200 && pixel.Green > 200 && pixel.Blue > 200)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>ラベルの背景を配置指示の矩形どおりに塗ることを検証する。</summary>
    [Fact]
    internal void Paint_ラベルの背景を塗る()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var rect = new EsRect(10, 10, 60, 20);
            var plan = CreatePlan(CreatePlacement(rect, "(60, 60)"));

            MarqueeLabelPainter.Paint(canvas, in plan);

            // 背景色は半透明の黒（ARGB 153,0,0,0）
            Assert.Equal(new SKColor(0, 0, 0, 153), bitmap.GetPixel(12, 12));
            // 矩形の外は塗らない
            Assert.Equal(0, bitmap.GetPixel(9, 9).Alpha);
            Assert.Equal(0, bitmap.GetPixel(80, 40).Alpha);
        }
    }

    /// <summary>背景の内側（内側余白の分だけ入った位置）に文字を描くことを検証する。</summary>
    [Fact]
    internal void Paint_文字を内側余白の分だけ入れて描く()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var rect = new EsRect(10, 10, 60, 20);
            var plan = CreatePlan(CreatePlacement(rect, "(60, 60)"));

            MarqueeLabelPainter.Paint(canvas, in plan);

            Assert.True(CountTextPixels(bitmap, rect) > 0, "文字が描画されていない");
            // 内側余白の左上（文字より外側）は背景のままである
            Assert.Equal(new SKColor(0, 0, 0, 153), bitmap.GetPixel(11, 11));
        }
    }

    /// <summary>表示を止めたラベルは描かないことを検証する（計画は可否で指示を消さない）。</summary>
    [Fact]
    internal void Paint_表示を止めたラベルは描かない()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var rect = new EsRect(10, 10, 60, 20);
            var plan = CreatePlan(CreatePlacement(rect, "(60, 60)", isVisible: false));

            MarqueeLabelPainter.Paint(canvas, in plan);

            Assert.Equal(0, bitmap.GetPixel(12, 12).Alpha);
        }
    }

    /// <summary>文言が空のラベルは背景も描かないことを検証する（計画に空の文言が残っていても描かない）。</summary>
    [Fact]
    internal void Paint_文言が空なら描かない()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var rect = new EsRect(10, 10, 60, 20);
            var plan = CreatePlan(CreatePlacement(rect, string.Empty));

            MarqueeLabelPainter.Paint(canvas, in plan);

            Assert.Equal(0, bitmap.GetPixel(12, 12).Alpha);
        }
    }

    /// <summary>複数のラベルを並び順に描くことを検証する。</summary>
    [Fact]
    internal void Paint_複数のラベルを描く()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var first = new EsRect(10, 10, 60, 20);
            var second = new EsRect(10, 40, 60, 20);
            var plan = CreatePlan(
                CreatePlacement(first, "(60, 60)"),
                CreatePlacement(second, "(100, 100)"));

            MarqueeLabelPainter.Paint(canvas, in plan);

            Assert.Equal(new SKColor(0, 0, 0, 153), bitmap.GetPixel((int)first.X + 2, (int)first.Y + 2));
            Assert.Equal(new SKColor(0, 0, 0, 153), bitmap.GetPixel((int)second.X + 2, (int)second.Y + 2));
            // どちらの矩形にも入らない位置は塗らない
            Assert.Equal(0, bitmap.GetPixel(100, 100).Alpha);
        }
    }

    /// <summary>計画がラベルを持たない場合は何も描かないことを検証する。</summary>
    [Fact]
    internal void Paint_ラベルが無ければ何も描かない()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var plan = CreatePlan();

            MarqueeLabelPainter.Paint(canvas, in plan);

            Assert.Equal(0, bitmap.GetPixel(12, 12).Alpha);
        }
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void Paint_キャンバスがnullなら例外()
    {
        var plan = CreatePlan(CreatePlacement(new EsRect(10, 10, 60, 20), "(60, 60)"));

        Assert.Throws<ArgumentNullException>(() => MarqueeLabelPainter.Paint(null!, in plan));
    }

    /// <summary>プレゼンターが解決したラベルが背景の内側に収まることを検証する。</summary>
    /// <remarks>
    /// 実寸の測定と描画が食い違うと、文字が背景からはみ出して切れる（本 UI は同じ描画 API で測るため一致する）。
    /// </remarks>
    [Fact]
    internal void Paint_計画のラベルが背景の内側に収まる()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var presenter = new MarqueePresenter(new NoopEffects());
            presenter.SetCoordTransform(CoordTransform.Identity);
            presenter.SetSelection(new EsRect(20, 20, 60, 60));
            var plan = presenter.ResolveDrawPlan(new EsSize(200, 200), SkiaText.Measure);

            MarqueeLabelPainter.Paint(canvas, in plan);

            var label = plan.Labels[0];
            Assert.True(label.IsVisible, "サイズラベルが表示される計画になっていない");
            Assert.True(label.DipRect.Width > label.PaddingX * 2, "ラベルの幅が内側余白だけの大きさになっている");
            Assert.True(label.DipRect.Height > label.PaddingY * 2, "ラベルの高さが内側余白だけの大きさになっている");
            Assert.True(CountTextPixels(bitmap, label.DipRect) > 0, "ラベルの文字が描画されていない");
        }
    }

    /// <summary>コアが要求する効果を受け取らないテスト用の実装（描画の検証には効果が要らない）。</summary>
    private sealed class NoopEffects : MarqueeEffectsBase
    {
    }
}
