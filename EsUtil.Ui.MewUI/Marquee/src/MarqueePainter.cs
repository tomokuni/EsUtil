using Aprillz.MewUI.Rendering;
using EsUtil.Ui.MewUI.Interop;
using EsUtil.Ui.MewUI.Marquee.Internal;

namespace EsUtil.Ui.MewUI.Marquee;

/// <summary>描画計画（<see cref="MarqueeDrawPlan"/>）を MewUI の描画 API へ写す描画実装。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く層とその順序は描画層（<see cref="MarqueePresenter"/>）が <see cref="MarqueeDrawPlan.Layers"/> として単一所有する。<br/>
/// 本クラスは順序を判断せず、層ごとに描画 API を呼ぶだけである（座標・色・寸法はすべて計画に解決済み）。<br/>
/// <b>キャンバスの抽象化を持たない</b>: 描画 API は本クラスが直接呼ぶ（フレームワーク標準の <see cref="IGraphicsContext"/>）。<br/>
/// <b>テキストは描かない</b>: ラベルは UI 要素（<c>Border</c> + <c>TextBlock</c>）として配置するため、<br/>
/// <see cref="MarqueeDrawLayer.Label"/> の層では何もしない。
/// </remarks>
internal static class MarqueePainter
{
    /// <summary>描画計画を描画順に描く。</summary>
    /// <param name="context">描画先のコンテキスト（DIP 座標で描画できる状態であること）。 null 不可。</param>
    /// <param name="plan">描画層が解決した描画計画（層の順序・矩形・色を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    /// <remarks>
    /// <b>層ごとに外観の表示可否（<c>Show*</c>）を見て描く</b>: 計画は表示可否で値を消さないため、<br/>
    /// 描くかどうかの判断は本クラス（描画側）が行う（色のアルファを「描かない」の代用にしない）。
    /// </remarks>
    public static void Paint(IGraphicsContext context, in MarqueeDrawPlan plan)
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
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="plan">描画計画。</param>
    /// <param name="appearance">外観（表示可否の所有者）。</param>
    /// <remarks>暗転は「表示可否」と「注目矩形の有無（全面暗転の可否）」の両方で決まる（判断は外観が単一所有する）。</remarks>
    private static void DrawDecoration(IGraphicsContext context, in MarqueeDrawPlan plan, MarqueeAppearance appearance)
    {
        if (appearance.ShowOverlay && appearance.ShouldDrawOverlay(plan.FocusDipRect is not null))
        {
            MewFill.Rects(context, plan.OverlayDipRects, appearance.OverlayColor);
        }

        if (appearance.ShowOuterBorder)
        {
            MewFill.Rects(context, plan.OuterBorderDipRects, appearance.OuterBorderColor);
        }

        if (appearance.ShowInnerFill && plan.InnerFillDipRect is { } fill)
        {
            MewFill.Rect(context, fill, appearance.InnerFillColor);
        }

        if (appearance.ShowInnerBorder)
        {
            MewFill.Rects(context, plan.InnerBorderDipRects, appearance.InnerBorderColor);
        }
    }

    /// <summary>ドラッグハンドル（塗り + 枠線）を描く。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="handles">ハンドルの幾何。</param>
    /// <param name="appearance">外観。</param>
    private static void DrawHandles(IGraphicsContext context, in MarqueeHandles handles, MarqueeAppearance appearance)
    {
        var fillColor = appearance.HandleColor.ToMewUIColor();
        var borderColor = appearance.HandleBorderColor.ToMewUIColor();

        foreach (var bounds in handles.HandleDipRects)
        {
            var rect = bounds.ToMewUIRect();
            context.FillRectangle(rect, fillColor);
            context.DrawRectangle(rect, borderColor, handles.BorderThickness);
        }
    }

    /// <summary>始点マーカー（十字線）を描く。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="marker">マーカーの幾何。</param>
    /// <param name="appearance">外観。</param>
    private static void DrawStartMarker(IGraphicsContext context, in MarqueeMarker marker, MarqueeAppearance appearance)
    {
        var color = appearance.MarkerColor.ToMewUIColor();
        var thickness = appearance.MarkerThickness;
        var center = marker.Center;
        var half = marker.Size;

        context.DrawLine(
            new(center.X - half, center.Y), new(center.X + half, center.Y), color, thickness);
        context.DrawLine(
            new(center.X, center.Y - half), new(center.X, center.Y + half), color, thickness);
    }
}
