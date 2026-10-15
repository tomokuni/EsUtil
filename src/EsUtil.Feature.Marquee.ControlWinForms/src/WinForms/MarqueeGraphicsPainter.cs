using EsUtil.Feature.Marquee.WinForms.Internal;

namespace EsUtil.Feature.Marquee.WinForms;

/// <summary>描画計画（<see cref="MarqueeDrawPlan"/>）を GDI+ の描画 API へ写す描画実装。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く層とその順序は描画層（<see cref="MarqueePresenter"/>）が <see cref="MarqueeDrawPlan.Layers"/> として単一所有する。<br/>
/// 本クラスは順序を判断せず、層ごとに GDI+ の描画 API を呼ぶだけである（座標・色・寸法はすべて計画に解決済み）。<br/>
/// <br/>
/// <b>描画面の座標系</b>: 本クラスは与えられた <see cref="Graphics"/> の座標系（DIP）で描く。<br/>
/// DIP から物理ピクセルへの拡大は呼び出し側（<see cref="MarqueeControl"/>）が描画面の変換として適用する。<br/>
/// <br/>
/// <b>文字も本クラスが描く</b>: ラベルは計画の <see cref="MarqueeDrawPlan.Labels"/> が持つ配置指示（文言・背景・色・内側余白）に従い、<br/>
/// 図形と同じ描画面へ描く（文字の実寸測定は <see cref="GraphicsText"/> が単一所有する）。<br/>
/// <br/>
/// <b>Bitmap 上で検証できる</b>: 描画先は <see cref="Graphics"/> だけであるため、ウィンドウを持たない単体テストでも実寸の画素を検査できる。
/// </remarks>
internal static class MarqueeGraphicsPainter
{
    /// <summary>枠線・十字線に使うペン（スレッドごとに 1 つを使い回す）。</summary>
    [ThreadStatic]
    private static Pen? s_stroke;

    /// <summary>枠線のペンを取得する（太さと色だけを書き換える）。</summary>
    /// <param name="thickness">線の太さ（DIP）。</param>
    /// <param name="color">線の色。</param>
    /// <returns>書き換え済みのペン（スレッドごとに共有する 1 つ）。</returns>
    private static Pen ResolveStroke(double thickness, Color color)
    {
        var pen = s_stroke ??= new Pen(color);
        pen.Color = color;
        pen.Width = (float)thickness;
        return pen;
    }

    /// <summary>描画計画を描画順に描く。</summary>
    /// <param name="graphics">描画先（DIP 座標で描画できる状態であること）。 null 不可。</param>
    /// <param name="plan">描画層が解決した描画計画（層の順序・矩形・色を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> が null の場合。</exception>
    /// <remarks>
    /// <b>層ごとに外観の表示可否（<c>Show*</c>）を見て描く</b>: 計画は表示可否で値を消さないため、<br/>
    /// 描くかどうかの判断は本クラス（描画側）が行う（色のアルファを「描かない」の代用にしない）。
    /// </remarks>
    public static void Paint(Graphics graphics, in MarqueeDrawPlan plan)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        var appearance = plan.Appearance;
        var layers = plan.Layers;

        // 描画順は描画層（Layers）が単一所有する。 本クラスは順に写すだけで、順序を判断しない
        for (var index = 0; index < layers.Count; index++)
        {
            switch (layers[index])
            {
                case MarqueeDrawLayer.Decoration:
                    DrawDecoration(graphics, in plan, appearance);
                    break;

                case MarqueeDrawLayer.Handles when appearance.ShowHandles && plan.Handles is { } handles:
                    DrawHandles(graphics, in handles, appearance);
                    break;

                case MarqueeDrawLayer.Marker when appearance.ShowMarker && plan.Marker is { } marker:
                    DrawStartMarker(graphics, in marker, appearance);
                    break;

                case MarqueeDrawLayer.Label:
                    DrawLabels(graphics, in plan);
                    break;
            }
        }
    }

    /// <summary>装飾（暗転マスク・外枠・内側の塗り・内枠）を描く（表示が有効な項目だけ）。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="plan">描画計画。</param>
    /// <param name="appearance">外観（表示可否の所有者）。</param>
    /// <remarks>暗転は「表示可否」と「注目矩形の有無（全面暗転の可否）」の両方で決まる（判断は外観が単一所有する）。</remarks>
    private static void DrawDecoration(Graphics graphics, in MarqueeDrawPlan plan, MarqueeAppearance appearance)
    {
        if (appearance.ShowOverlay && appearance.ShouldDrawOverlay(plan.FocusDipRect is not null))
        {
            GraphicsFill.Rects(graphics, plan.OverlayDipRects, appearance.OverlayColor);
        }

        if (appearance.ShowOuterBorder)
        {
            GraphicsFill.Rects(graphics, plan.OuterBorderDipRects, appearance.OuterBorderColor);
        }

        if (appearance.ShowInnerFill && plan.InnerFillDipRect is { } fill)
        {
            GraphicsFill.Rect(graphics, fill, appearance.InnerFillColor);
        }

        if (appearance.ShowInnerBorder)
        {
            GraphicsFill.Rects(graphics, plan.InnerBorderDipRects, appearance.InnerBorderColor);
        }
    }

    /// <summary>ドラッグハンドル（塗り + 枠線）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="handles">ハンドルの幾何。</param>
    /// <param name="appearance">外観。</param>
    /// <remarks>塗りと枠線の 2 つの資源を N 個の矩形で共有するため、本メソッドが描画 API を直接呼ぶ。</remarks>
    private static void DrawHandles(Graphics graphics, in MarqueeHandles handles, MarqueeAppearance appearance)
    {
        var stroke = ResolveStroke(handles.BorderThickness, appearance.HandleBorderColor);
        var handleRects = handles.HandleDipRects;

        for (var index = 0; index < handleRects.Count; index++)
        {
            var rect = handleRects[index];
            GraphicsFill.Rect(graphics, rect, appearance.HandleColor);
            graphics.DrawRectangle(stroke, rect.ToRectangleF());
        }
    }

    /// <summary>始点マーカー（十字線）を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="marker">マーカーの幾何。</param>
    /// <param name="appearance">外観。</param>
    private static void DrawStartMarker(Graphics graphics, in MarqueeMarker marker, MarqueeAppearance appearance)
    {
        var stroke = ResolveStroke(appearance.MarkerThickness, appearance.MarkerColor);

        var center = marker.Center;
        var half = marker.Size;
        graphics.DrawLine(
            stroke, (float)(center.X - half), (float)center.Y, (float)(center.X + half), (float)center.Y);
        graphics.DrawLine(
            stroke, (float)center.X, (float)(center.Y - half), (float)center.X, (float)(center.Y + half));
    }

    /// <summary>テキスト（サイズラベル・カーソル座標ラベル）を表示が有効なものだけ描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="plan">描画計画。</param>
    /// <remarks>背景を先に塗ってから文字を置く（配置指示の内側余白の分だけ文字を内側へ入れる）。</remarks>
    private static void DrawLabels(Graphics graphics, in MarqueeDrawPlan plan)
    {
        var labels = plan.Labels;

        for (var index = 0; index < labels.Count; index++)
        {
            var label = labels[index];
            if (!label.IsVisible)
            {
                continue;
            }

            GraphicsFill.Rect(graphics, label.DipRect, label.BackgroundColor);
            GraphicsText.Draw(
                graphics,
                label.Text,
                label.DipRect.X + label.PaddingX,
                label.DipRect.Y + label.PaddingY,
                label.FontSize,
                label.TextColor);
        }
    }
}
