using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinUI3.Marquee.Internal;

/// <summary>描画計画（<see cref="MarqueeDrawPlan"/>）から層ごとの矩形の列を組み立てる。</summary>
/// <remarks>
/// <b>計算を持たない</b>: 矩形は計画に DIP で解決済みである。 本クラスは「どの層にどの矩形を、どの色で」<br/>
/// 置くかだけを持ち、XAML の要素を作らないため、ウィンドウ無しで検証できる。<br/>
/// <br/>
/// <b>層の順序</b>: 計画の <see cref="MarqueeDrawPlan.Layers"/> の順に、装飾 → ハンドル → 始点マーカー → ラベル。<br/>
/// <b>表示可否</b>: 計画は表示可否で値を消さないため、置くかどうかは本クラスが外観の <c>Show*</c> を見て決める。
/// </remarks>
internal static class MarqueeShapePlan
{
    /// <summary>描画計画から矩形の列を組み立てる（層の順序を保つ）。</summary>
    /// <param name="plan">描画計画。</param>
    /// <param name="rects">矩形を追加する先（装飾・ハンドル・始点マーカー）。</param>
    /// <param name="labels">表示するラベルの配置指示を格納する先。</param>
    /// <exception cref="ArgumentNullException"><paramref name="rects"/> または <paramref name="labels"/> が null の場合。</exception>
    /// <remarks>描く内容が無い項目は追加しない（要素の無駄を作らない）。</remarks>
    public static void Build(
        MarqueeDrawPlan plan, MarqueeRectList rects, List<MarqueeLabelPlacement> labels)
    {
        ArgumentNullException.ThrowIfNull(rects);
        ArgumentNullException.ThrowIfNull(labels);

        var appearance = plan.Appearance;

        // 描画順は描画層（Layers）が単一所有する。 本クラスは順に写すだけで、順序を判断しない
        var layers = plan.Layers;
        for (var i = 0; i < layers.Count; i++)
        {
            switch (layers[i])
            {
                case MarqueeDrawLayer.Decoration:
                    AddDecoration(plan, appearance, rects);
                    break;

                case MarqueeDrawLayer.Handles when appearance.ShowHandles && plan.Handles is { } handles:
                    AddHandles(handles, appearance, rects);
                    break;

                case MarqueeDrawLayer.Marker when appearance.ShowMarker && plan.Marker is { } marker:
                    AddMarker(marker, appearance, rects);
                    break;

                case MarqueeDrawLayer.Label:
                    AddLabels(plan, labels);
                    break;
            }
        }
    }

    /// <summary>装飾（暗転マスク・外枠・内側の塗り・内枠）を追加する。</summary>
    /// <param name="plan">描画計画。</param>
    /// <param name="appearance">外観（表示可否の所有者）。</param>
    /// <param name="rects">追加先。</param>
    private static void AddDecoration(
        MarqueeDrawPlan plan, MarqueeAppearance appearance, MarqueeRectList rects)
    {
        if (appearance.ShowOverlay && appearance.ShouldDrawOverlay(plan.FocusDipRect is not null))
        {
            AddRects(rects, plan.OverlayDipRects, ToArgb(appearance.OverlayColor));
        }

        if (appearance.ShowOuterBorder)
        {
            AddRects(rects, plan.OuterBorderDipRects, ToArgb(appearance.OuterBorderColor));
        }

        if (appearance.ShowInnerFill && plan.InnerFillDipRect is { } fill)
        {
            rects.Add(fill.X, fill.Y, fill.Width, fill.Height, ToArgb(appearance.InnerFillColor));
        }

        if (appearance.ShowInnerBorder)
        {
            AddRects(rects, plan.InnerBorderDipRects, ToArgb(appearance.InnerBorderColor));
        }
    }

    /// <summary>ドラッグハンドル（塗り + 枠線）を追加する。</summary>
    /// <param name="handles">ハンドルの幾何。</param>
    /// <param name="appearance">外観。</param>
    /// <param name="rects">追加先。</param>
    /// <remarks>枠線は塗りの外側へ太さの分だけ広げた矩形の 4 辺で表す（要素に枠線を持たせない）。</remarks>
    private static void AddHandles(MarqueeHandles handles, MarqueeAppearance appearance, MarqueeRectList rects)
    {
        var thickness = Math.Max(0, handles.BorderThickness);
        var fillArgb = ToArgb(appearance.HandleColor);
        var borderArgb = ToArgb(appearance.HandleBorderColor);

        for (var i = 0; i < handles.HandleDipRects.Count; i++)
        {
            var bounds = handles.HandleDipRects[i];
            rects.Add(bounds.X, bounds.Y, bounds.Width, bounds.Height, fillArgb);
            if (thickness > 0)
            {
                AddOutline(rects, bounds, thickness, borderArgb);
            }
        }
    }

    /// <summary>始点マーカー（十字線）を追加する。</summary>
    /// <param name="marker">マーカーの幾何。</param>
    /// <param name="appearance">外観。</param>
    /// <param name="rects">追加先。</param>
    /// <remarks>十字線は 2 本の細い矩形で表す（線を描く要素を別に持たない）。</remarks>
    private static void AddMarker(MarqueeMarker marker, MarqueeAppearance appearance, MarqueeRectList rects)
    {
        var thickness = Math.Max(0, appearance.MarkerThickness);
        if (thickness <= 0)
        {
            return;
        }

        var argb = ToArgb(appearance.MarkerColor);
        var centerX = marker.Center.X;
        var centerY = marker.Center.Y;
        var half = marker.Size;

        rects.Add(centerX - half, centerY - (thickness / 2), half * 2, thickness, argb);
        rects.Add(centerX - (thickness / 2), centerY - half, thickness, half * 2, argb);
    }

    /// <summary>表示するラベルの配置指示を追加する。</summary>
    /// <param name="plan">描画計画。</param>
    /// <param name="labels">追加先。</param>
    private static void AddLabels(MarqueeDrawPlan plan, List<MarqueeLabelPlacement> labels)
    {
        for (var i = 0; i < plan.Labels.Count; i++)
        {
            var label = plan.Labels[i];
            if (label.IsVisible && label.Text.Length > 0)
            {
                labels.Add(label);
            }
        }
    }

    /// <summary>矩形の列を追加する。</summary>
    /// <param name="rects">追加先。</param>
    /// <param name="source">追加する矩形（DIP）。</param>
    /// <param name="argb">塗り色。</param>
    private static void AddRects(MarqueeRectList rects, IReadOnlyList<EsRect> source, uint argb)
    {
        for (var i = 0; i < source.Count; i++)
        {
            var rect = source[i];
            rects.Add(rect.X, rect.Y, rect.Width, rect.Height, argb);
        }
    }

    /// <summary>矩形の輪郭を 4 辺の矩形として追加する。</summary>
    /// <param name="rects">追加先。</param>
    /// <param name="bounds">対象の矩形。</param>
    /// <param name="thickness">線の太さ。</param>
    /// <param name="argb">線の色。</param>
    private static void AddOutline(
        MarqueeRectList rects, EsRect bounds, double thickness, uint argb)
    {
        rects.Add(bounds.X, bounds.Y, bounds.Width, thickness, argb);
        rects.Add(bounds.X, bounds.Y + bounds.Height - thickness, bounds.Width, thickness, argb);
        rects.Add(bounds.X, bounds.Y, thickness, bounds.Height, argb);
        rects.Add(bounds.X + bounds.Width - thickness, bounds.Y, thickness, bounds.Height, argb);
    }

    /// <summary>色を <c>0xAARRGGBB</c> へ変換する。</summary>
    /// <param name="color">変換する色。</param>
    /// <returns>変換した色。</returns>
    public static uint ToArgb(System.Drawing.Color color)
        => ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
}
