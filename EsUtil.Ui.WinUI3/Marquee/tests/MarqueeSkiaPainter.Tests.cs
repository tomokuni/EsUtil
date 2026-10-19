using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Marquee.Core.Contract;
using EsUtil.Ui.WinUI3.Marquee.Internal;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;

namespace EsUtil.Ui.WinUI3.Marquee.Tests;

/// <summary><see cref="MarqueeSkiaPainter"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 本テストは描画計画を手で組み立てて画素を検証するため、状態機械（プレゼンター）の挙動とは独立に<br/>
/// 「層ごとに描くかどうかを外観の <c>Show*</c> で決めること」「層の順序どおりに描くこと」を固定する。<br/>
/// 実際の計画（プレゼンターが解決した値）で描く統合的な検証は <c>選択の描画</c> のテストが担う。
/// </remarks>
public class MarqueeSkiaPainterTests
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

    /// <summary>検証に必要な値だけを持つ描画計画を組み立てる。</summary>
    /// <param name="appearance">外観。</param>
    /// <param name="layers">描く層の順序。</param>
    /// <param name="overlayRects">暗転して塗る領域。</param>
    /// <param name="outerBorderRects">外枠の矩形。</param>
    /// <param name="handles">ハンドルの幾何。</param>
    /// <param name="marker">始点マーカー。</param>
    /// <param name="focusDipRect">注目矩形。</param>
    /// <param name="labels">ラベルの配置指示。</param>
    /// <returns>組み立てた描画計画。</returns>
    private static MarqueeDrawPlan CreatePlan(
        MarqueeAppearance appearance,
        IReadOnlyList<MarqueeDrawLayer> layers,
        IReadOnlyList<EsRect>? overlayRects = null,
        IReadOnlyList<EsRect>? outerBorderRects = null,
        MarqueeHandles? handles = null,
        MarqueeMarker? marker = null,
        EsRect? focusDipRect = null,
        IReadOnlyList<MarqueeLabelPlacement>? labels = null)
        => new(
            appearance,
            new EsRect(0, 0, 200, 200),
            focusDipRect,
            null,
            overlayRects ?? [],
            outerBorderRects ?? [],
            null,
            [],
            handles,
            marker,
            layers,
            labels ?? []);

    /// <summary>暗転マスクを指定色で塗ることを検証する。</summary>
    [Fact]
    internal void Paint_装飾の暗転を塗る()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var plan = CreatePlan(
                MarqueeAppearance.Default,
                [MarqueeDrawLayer.Decoration],
                overlayRects: [new EsRect(0, 0, 200, 20)],
                focusDipRect: new EsRect(20, 40, 60, 60));

            MarqueeSkiaPainter.Paint(canvas, in plan);

            // 既定の暗転色は半透明の黒（ARGB 128,0,0,0）
            Assert.Equal(new SKColor(0, 0, 0, 128), bitmap.GetPixel(100, 10));
            Assert.Equal(0, bitmap.GetPixel(100, 100).Alpha);
        }
    }

    /// <summary>外枠を指定色で塗ることを検証する。</summary>
    [Fact]
    internal void Paint_外枠を塗る()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var plan = CreatePlan(
                MarqueeAppearance.Default,
                [MarqueeDrawLayer.Decoration],
                outerBorderRects: [new EsRect(0, 0, 200, 2)]);

            MarqueeSkiaPainter.Paint(canvas, in plan);

            // 既定の枠線色は #FFA9A9A9
            Assert.Equal(new SKColor(169, 169, 169, 255), bitmap.GetPixel(100, 1));
            Assert.Equal(0, bitmap.GetPixel(100, 100).Alpha);
        }
    }

    /// <summary>表示を止めた装飾は描かないことを検証する（計画は可否で値を消さない）。</summary>
    /// <param name="showOverlay">暗転の表示可否。</param>
    /// <param name="showOuterBorder">外枠の表示可否。</param>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    internal void Paint_表示を止めた装飾は描かない(bool showOverlay, bool showOuterBorder)
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var appearance = MarqueeAppearance.Default with { ShowOverlay = showOverlay, ShowOuterBorder = showOuterBorder };
            var plan = CreatePlan(
                appearance,
                [MarqueeDrawLayer.Decoration],
                overlayRects: [new EsRect(0, 20, 200, 20)],
                outerBorderRects: [new EsRect(0, 0, 200, 2)],
                focusDipRect: new EsRect(20, 60, 60, 60));

            MarqueeSkiaPainter.Paint(canvas, in plan);

            // 表示を止めた項目だけが描かれない（止めていない項目は描かれる）
            Assert.Equal(showOverlay ? 128 : 0, bitmap.GetPixel(100, 30).Alpha);
            Assert.Equal(showOuterBorder ? 255 : 0, bitmap.GetPixel(100, 1).Alpha);
        }
    }

    /// <summary>注目矩形が無いときは全面暗転を許可した場合だけ塗ることを検証する。</summary>
    /// <param name="showFullOverlay">全面暗転の可否。</param>
    /// <param name="expectedAlpha">期待するアルファ。</param>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 128)]
    internal void Paint_注目矩形が無いときは全面暗転の可否で決まる(bool showFullOverlay, int expectedAlpha)
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var appearance = MarqueeAppearance.Default with { ShowFullOverlay = showFullOverlay };
            var plan = CreatePlan(
                appearance,
                [MarqueeDrawLayer.Decoration],
                overlayRects: [new EsRect(0, 0, 200, 200)]);

            MarqueeSkiaPainter.Paint(canvas, in plan);

            Assert.Equal(expectedAlpha, bitmap.GetPixel(100, 100).Alpha);
        }
    }

    /// <summary>ドラッグハンドルを塗りと枠線で描くことを検証する。</summary>
    [Fact]
    internal void Paint_ドラッグハンドルを描く()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var plan = CreatePlan(
                MarqueeAppearance.Default,
                [MarqueeDrawLayer.Handles],
                handles: new MarqueeHandles([new EsRect(10, 10, 8, 8)], BorderThickness: 1.5));

            MarqueeSkiaPainter.Paint(canvas, in plan);

            // 既定のハンドルの塗りは白
            Assert.Equal(SKColors.White, bitmap.GetPixel(14, 14));
            Assert.Equal(0, bitmap.GetPixel(100, 100).Alpha);
        }
    }

    /// <summary>表示を止めたハンドルは描かないことを検証する。</summary>
    /// <param name="showHandles">ハンドルの表示可否。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    internal void Paint_ハンドルの表示可否に従う(bool showHandles)
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var appearance = MarqueeAppearance.Default with { ShowHandles = showHandles };
            var plan = CreatePlan(
                appearance,
                [MarqueeDrawLayer.Handles],
                handles: new MarqueeHandles([new EsRect(10, 10, 8, 8)], BorderThickness: 1.5));

            MarqueeSkiaPainter.Paint(canvas, in plan);

            Assert.Equal(showHandles ? 255 : 0, bitmap.GetPixel(14, 14).Alpha);
        }
    }

    /// <summary>始点マーカー（十字線）を描くことを検証する。</summary>
    [Fact]
    internal void Paint_始点マーカーを描く()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var plan = CreatePlan(
                MarqueeAppearance.Default,
                [MarqueeDrawLayer.Marker],
                marker: new MarqueeMarker(new EsPoint(100, 100), 8));

            MarqueeSkiaPainter.Paint(canvas, in plan);

            // 十字の中心と腕の先端が描かれる（既定のマーカー色は白）
            Assert.Equal(SKColors.White, bitmap.GetPixel(100, 100));
            Assert.Equal(SKColors.White, bitmap.GetPixel(107, 100));
            Assert.Equal(SKColors.White, bitmap.GetPixel(100, 107));
            // 十字の外は塗られない
            Assert.Equal(0, bitmap.GetPixel(100, 120).Alpha);
        }
    }

    /// <summary>表示を止めたマーカーは描かないことを検証する。</summary>
    /// <param name="showMarker">マーカーの表示可否。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    internal void Paint_マーカーの表示可否に従う(bool showMarker)
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var appearance = MarqueeAppearance.Default with { ShowMarker = showMarker };
            var plan = CreatePlan(
                appearance,
                [MarqueeDrawLayer.Marker],
                marker: new MarqueeMarker(new EsPoint(100, 100), 8));

            MarqueeSkiaPainter.Paint(canvas, in plan);

            Assert.Equal(showMarker ? 255 : 0, bitmap.GetPixel(100, 100).Alpha);
        }
    }

    /// <summary>ラベルの層では何も描かないことを検証する（ラベルは別の描画実装が描く）。</summary>
    [Fact]
    internal void Paint_ラベルの層では何も描かない()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var plan = CreatePlan(
                MarqueeAppearance.Default,
                [MarqueeDrawLayer.Label],
                focusDipRect: new EsRect(20, 20, 60, 60),
                labels:
                [
                    new MarqueeLabelPlacement(
                        new EsRect(20, 20, 60, 20),
                        4,
                        4,
                        "(60, 60)",
                        14,
                        DrawingColor.White,
                        DrawingColor.FromArgb(153, 0, 0, 0),
                        true),
                ]);

            MarqueeSkiaPainter.Paint(canvas, in plan);

            Assert.Equal(0, bitmap.GetPixel(40, 30).Alpha);
        }
    }

    /// <summary>層の順序どおりに描くことを検証する（後から描く層が先の層を覆う）。</summary>
    /// <param name="handlesFirst">ハンドルを先に描くかどうか。</param>
    /// <param name="expectedWhite">ハンドルの画素が白のまま残るかどうか。</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    internal void Paint_層の順序どおりに描く(bool handlesFirst, bool expectedWhite)
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            IReadOnlyList<MarqueeDrawLayer> layers = handlesFirst
                ? [MarqueeDrawLayer.Handles, MarqueeDrawLayer.Decoration]
                : [MarqueeDrawLayer.Decoration, MarqueeDrawLayer.Handles];
            var plan = CreatePlan(
                MarqueeAppearance.Default,
                layers,
                overlayRects: [new EsRect(10, 10, 8, 8)],
                handles: new MarqueeHandles([new EsRect(10, 10, 8, 8)], BorderThickness: 1.5),
                focusDipRect: new EsRect(40, 40, 20, 20));

            MarqueeSkiaPainter.Paint(canvas, in plan);

            Assert.Equal(expectedWhite, bitmap.GetPixel(14, 14) == SKColors.White);
        }
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void Paint_キャンバスがnullなら例外()
    {
        var plan = CreatePlan(MarqueeAppearance.Default, [MarqueeDrawLayer.Decoration]);

        Assert.Throws<ArgumentNullException>(() => MarqueeSkiaPainter.Paint(null!, in plan));
    }

    /// <summary>公開ファサード（<see cref="MarqueeControl.DrawPlan"/>）が同じ描画を行うことを検証する。</summary>
    [Fact]
    internal void DrawPlan_公開ファサードから同じ描画を行う()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var plan = CreatePlan(
                MarqueeAppearance.Default,
                [MarqueeDrawLayer.Decoration],
                outerBorderRects: [new EsRect(0, 0, 200, 2)]);

            MarqueeControl.DrawPlan(canvas, in plan);

            Assert.Equal(new SKColor(169, 169, 169, 255), bitmap.GetPixel(100, 1));
        }
    }

    /// <summary>プレゼンターが解決した計画（選択あり）を描くことを検証する。</summary>
    /// <remarks>
    /// 実際の状態機械を通した計画で、暗転（選択の外側）と外枠（選択の境界）が描かれることを確かめる。
    /// </remarks>
    [Fact]
    internal void Paint_選択の計画を描く()
    {
        var (bitmap, canvas) = CreateTarget(200, 200);
        using (bitmap)
        using (canvas)
        {
            var presenter = new MarqueePresenter(new NoopEffects());
            presenter.SetCoordTransform(CoordTransform.Identity);
            presenter.SetSelection(new EsRect(20, 20, 60, 60));
            var plan = presenter.ResolveDrawPlan(new EsSize(200, 200), SkiaText.Measure);

            MarqueeSkiaPainter.Paint(canvas, in plan);

            // 選択の中心は塗らない（既定では内側の塗りを描かない）
            Assert.Equal(0, bitmap.GetPixel(50, 50).Alpha);
            // 選択の境界（外枠）は塗る（ハンドルの位置を避けて境界の内側を確かめる）
            Assert.Equal(new SKColor(169, 169, 169, 255), bitmap.GetPixel(20, 35));
            // 選択の外側は暗転する（既定の暗転色は半透明の黒）
            Assert.Equal(new SKColor(0, 0, 0, 128), bitmap.GetPixel(5, 5));
        }
    }

    /// <summary>コアが要求する効果を受け取らないテスト用の実装（描画の検証には効果が要らない）。</summary>
    private sealed class NoopEffects : MarqueeEffectsBase
    {
    }
}
