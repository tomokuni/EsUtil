using EsUtil.SaveEvidence.Platform.Skia;
using SkiaSharp;

namespace EsUtil.Feature.Marquee.MewUI;

/// <summary>描画計画（<see cref="MarqueeDrawPlan"/>）を Skia の描画 API へ写す描画実装。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く層とその順序は描画層（<see cref="MarqueePresenter"/>）が <see cref="MarqueeDrawPlan.Layers"/> として単一所有する。<br/>
/// 本クラスは順序を判断せず、層ごとに Skia の描画 API を呼ぶだけである（座標・色・寸法はすべて計画に解決済み）。<br/>
/// <br/>
/// <b>キャンバスの抽象化を持たない</b>: 描画 API は本クラスが直接呼ぶ。 共有できるプリミティブは<br/>
/// <see cref="SkiaFill"/> / <see cref="SkiaText"/> / <see cref="SkiaLabel"/> が単一所有する。<br/>
/// <br/>
/// <b>テキストは描かない</b>: ラベルは UI 要素（<c>Border</c> + <c>TextBlock</c>）として配置するため、<br/>
/// <see cref="MarqueeDrawLayer.Label"/> の層では何もしない。
/// </remarks>
internal static class MarqueeSkiaPainter
{
    /// <summary>描画計画を描画順に描く。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。 null 不可。</param>
    /// <param name="plan">描画層が解決した描画計画（層の順序・矩形・色を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    public static void Paint(SKCanvas canvas, in MarqueeDrawPlan plan)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var appearance = plan.Appearance;

        // 描画順は描画層（Layers）が単一所有する。 本クラスは順に写すだけで、順序を判断しない
        foreach (var layer in plan.Layers)
        {
            switch (layer)
            {
                case MarqueeDrawLayer.Decoration:
                    DrawDecoration(canvas, in plan, appearance);
                    break;

                case MarqueeDrawLayer.Handles when plan.Handles is { } handles:
                    DrawHandles(canvas, in handles, appearance);
                    break;

                case MarqueeDrawLayer.Marker when plan.Marker is { } marker:
                    DrawStartMarker(canvas, in marker, appearance);
                    break;

                case MarqueeDrawLayer.Label:
                    // ラベルは UI 要素として配置するため、本層では何もしない
                    break;
            }
        }
    }

    /// <summary>装飾（暗転マスク・外枠・内側の塗り・内枠）を描く。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="plan">描画計画。</param>
    /// <param name="appearance">外観。</param>
    private static void DrawDecoration(SKCanvas canvas, in MarqueeDrawPlan plan, MarqueeAppearance appearance)
    {
        SkiaFill.Rects(canvas, plan.OverlayDipRects, appearance.OverlayColor);
        SkiaFill.Rects(canvas, plan.OuterBorderDipRects, appearance.OuterBorderColor);

        if (plan.InnerFillDipRect is { } fill)
        {
            SkiaFill.Rect(canvas, fill, appearance.InnerFillColor);
        }

        SkiaFill.Rects(canvas, plan.InnerBorderDipRects, appearance.InnerBorderColor);
    }

    /// <summary>ドラッグハンドル（塗り + 枠線）を描く。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="handles">ハンドルの幾何。</param>
    /// <param name="appearance">外観。</param>
    /// <remarks>塗りと枠線の 2 ペイントを N 個の矩形で共有するため、本メソッドが描画 API を直接呼ぶ。</remarks>
    private static void DrawHandles(SKCanvas canvas, in MarqueeHandles handles, MarqueeAppearance appearance)
    {
        using var fill = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Fill,
            Color = appearance.HandleColor.ToSKColor(),
        };
        using var stroke = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)handles.BorderThickness,
            Color = appearance.HandleBorderColor.ToSKColor(),
        };

        foreach (var bounds in handles.HandleDipRects)
        {
            var rect = bounds.ToSKRect();
            canvas.DrawRect(rect, fill);
            canvas.DrawRect(rect, stroke);
        }
    }

    /// <summary>始点マーカー（十字線）を描く。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="marker">マーカーの幾何。</param>
    /// <param name="appearance">外観。</param>
    private static void DrawStartMarker(SKCanvas canvas, in MarqueeMarker marker, MarqueeAppearance appearance)
    {
        using var paint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)appearance.MarkerThickness,
            Color = appearance.MarkerColor.ToSKColor(),
        };

        var center = marker.Center;
        var half = marker.Size;
        canvas.DrawLine((float)(center.X - half), (float)center.Y, (float)(center.X + half), (float)center.Y, paint);
        canvas.DrawLine((float)center.X, (float)(center.Y - half), (float)center.X, (float)(center.Y + half), paint);
    }
}
