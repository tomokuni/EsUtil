using Avalonia.Media;
using EsUtil.Feature.Marquee.AvaloniaUI.Internal;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Marquee.AvaloniaUI;

/// <summary>描画計画（<see cref="MarqueeDrawPlan"/>）を Avalonia の描画 API へ写す描画実装。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く層とその順序は描画層（<see cref="MarqueePresenter"/>）が <see cref="MarqueeDrawPlan.Layers"/> として単一所有する。<br/>
/// 本クラスは順序を判断せず、層ごとに Avalonia の描画 API を呼ぶだけである（座標・色・寸法はすべて計画に解決済み）。<br/>
/// <br/>
/// <b>キャンバスの抽象化を持たない</b>: 描画 API は本クラスが直接呼ぶ（UI フレームワークをまたぐ描画の抽象は、<br/>
/// フレームワークごとの描画モデルの差を吸収しきれないため導入しない）。 色 → ブラシの共有は<br/>
/// <see cref="SolidColorBrushCache"/> が単一所有する。<br/>
/// <br/>
/// <b>テキストは描かない</b>: ラベルは UI 要素（<c>Border</c> + <c>TextBlock</c>）として配置するため、<br/>
/// <see cref="MarqueeDrawLayer.Label"/> の層では何もしない（要素は親より後に描かれるため常に最上位になる）。
/// </remarks>
internal static class MarqueeContextPainter
{
    /// <summary>描画計画を描画順に描く。</summary>
    /// <param name="context">描画先の描画コンテキスト（<c>Render</c> の実行中のみ有効）。 null 不可。</param>
    /// <param name="plan">描画層が解決した描画計画（層の順序・矩形・色を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    /// <remarks>
    /// <b>層ごとに外観の表示可否（<c>Show*</c>）を見て描く</b>: 計画は表示可否で値を消さないため、<br/>
    /// 描くかどうかの判断は本クラス（描画側）が行う（色のアルファを「描かない」の代用にしない）。
    /// </remarks>
    public static void Paint(DrawingContext context, in MarqueeDrawPlan plan)
    {
        ArgumentNullException.ThrowIfNull(context);

        var appearance = plan.Appearance;

        // 描画順は描画層（Layers）が単一所有する。 本クラスは順に写すだけで、順序を判断しない
        foreach (var layer in plan.Layers)
        {
            switch (layer)
            {
                case MarqueeDrawLayer.Decoration:
                    DrawDecoration(context, in plan, appearance);
                    break;

                case MarqueeDrawLayer.Handles when appearance.ShowHandles && plan.Handles is { } handles:
                    DrawHandles(context, in handles, appearance);
                    break;

                case MarqueeDrawLayer.Marker when appearance.ShowMarker && plan.Marker is { } marker:
                    DrawStartMarker(context, in marker, appearance);
                    break;

                case MarqueeDrawLayer.Label:
                    // ラベルは UI 要素として配置するため、本層では何もしない
                    break;
            }
        }
    }

    /// <summary>装飾（暗転マスク・外枠・内側の塗り・内枠）を描く（表示が有効な項目だけ）。</summary>
    /// <param name="context">描画先の描画コンテキスト。</param>
    /// <param name="plan">描画計画。</param>
    /// <param name="appearance">外観（表示可否の所有者）。</param>
    /// <remarks>暗転は「表示可否」と「注目矩形の有無（全面暗転の可否）」の両方で決まる（判断は外観が単一所有する）。</remarks>
    private static void DrawDecoration(DrawingContext context, in MarqueeDrawPlan plan, MarqueeAppearance appearance)
    {
        if (appearance.ShowOverlay && appearance.ShouldDrawOverlay(plan.FocusDipRect is not null))
        {
            FillRects(context, plan.OverlayDipRects, appearance.OverlayColor);
        }

        if (appearance.ShowOuterBorder)
        {
            FillRects(context, plan.OuterBorderDipRects, appearance.OuterBorderColor);
        }

        if (appearance.ShowInnerFill && plan.InnerFillDipRect is { } fill)
        {
            context.DrawRectangle(SolidColorBrushCache.GetOrCreate(appearance.InnerFillColor), null, fill.ToAvaloniaRect());
        }

        if (appearance.ShowInnerBorder)
        {
            FillRects(context, plan.InnerBorderDipRects, appearance.InnerBorderColor);
        }
    }

    /// <summary>ドラッグハンドル（塗り + 枠線）を描く。</summary>
    /// <param name="context">描画先の描画コンテキスト。</param>
    /// <param name="handles">ハンドルの幾何。</param>
    /// <param name="appearance">外観。</param>
    private static void DrawHandles(DrawingContext context, in MarqueeHandles handles, MarqueeAppearance appearance)
    {
        var fill = SolidColorBrushCache.GetOrCreate(appearance.HandleColor);
        var pen = new Pen(SolidColorBrushCache.GetOrCreate(appearance.HandleBorderColor), handles.BorderThickness);

        foreach (var bounds in handles.HandleDipRects)
        {
            context.DrawRectangle(fill, pen, bounds.ToAvaloniaRect());
        }
    }

    /// <summary>始点マーカー（十字線）を描く。</summary>
    /// <param name="context">描画先の描画コンテキスト。</param>
    /// <param name="marker">マーカーの幾何。</param>
    /// <param name="appearance">外観。</param>
    private static void DrawStartMarker(DrawingContext context, in MarqueeMarker marker, MarqueeAppearance appearance)
    {
        var center = marker.Center;
        var half = marker.Size;
        var pen = new Pen(SolidColorBrushCache.GetOrCreate(appearance.MarkerColor), appearance.MarkerThickness);

        context.DrawLine(
            pen, new EsPoint(center.X - half, center.Y).ToAvaloniaPoint(), new EsPoint(center.X + half, center.Y).ToAvaloniaPoint());
        context.DrawLine(
            pen, new EsPoint(center.X, center.Y - half).ToAvaloniaPoint(), new EsPoint(center.X, center.Y + half).ToAvaloniaPoint());
    }

    /// <summary>同色の矩形の一覧を塗る。</summary>
    /// <param name="context">描画先の描画コンテキスト。</param>
    /// <param name="rects">塗る矩形の一覧（空の場合は何もしない）。</param>
    /// <param name="color">塗り色。</param>
    private static void FillRects(DrawingContext context, IReadOnlyList<EsRect> rects, DrawingColor color)
    {
        if (rects.Count == 0)
        {
            return;
        }

        var brush = SolidColorBrushCache.GetOrCreate(color);

        foreach (var rect in rects)
        {
            context.DrawRectangle(brush, null, rect.ToAvaloniaRect());
        }
    }
}
