using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;

namespace EsUtil.Ui.MewUI.Loupe.Sample.Views;

/// <summary>ルーペ画像の上へ選択矩形の破線を描く（<c>LoupeControl.Rendered</c> の使用例）。</summary>
/// <remarks>
/// <b>示すこと</b>: 描画完了イベントの描画コンテキストへ、ホスト固有の付加情報（選択矩形の破線）を追加描画できる。<br/>
/// 描画位置は描画計画（<see cref="LoupeDrawPlan.MagnifiedImage"/>）から求める。<br/>
/// 切り出し領域（対象座標）と描画領域（クライアントの物理ピクセル）の比が「対象 1 ピクセル → 描画先の物理ピクセル」の倍率であるため、<br/>
/// 対象座標の矩形をこの倍率で換算すれば拡大画像と正確に一致する。<br/>
/// <br/>
/// <b>破線と太さの単位</b>: 描画面は物理ピクセルのため、DIP で見せたい寸法は <see cref="LoupeDrawPlan.ScaleDipToPixel"/> で換算する。<br/>
/// <b>破線の描き方</b>: MewUI の描画 API は線種を持たないため、辺ごとに線分へ分解して描く。
/// </remarks>
internal static class LoupeSelectionPainter
{
    /// <summary>枠線の色（オレンジ）。</summary>
    private static readonly System.Drawing.Color s_strokeColor = System.Drawing.Color.FromArgb(255, 255, 140, 0);

    /// <summary>破線 1 本の長さ（DIP）。</summary>
    private const double DashLengthDip = 4.0;

    /// <summary>破線の間隔（DIP）。</summary>
    private const double DashGapDip = 2.0;

    /// <summary>枠線の太さ（DIP）。</summary>
    private const double StrokeThicknessDip = 2.0;

    /// <summary>選択矩形の破線をルーペ画像へ描く。</summary>
    /// <param name="context">描画完了イベントが渡した描画コンテキスト（描画中のみ有効）。</param>
    /// <param name="plan">実際に使われた描画計画（切り出しと描画の矩形を持つ）。</param>
    /// <param name="selection">選択矩形の状態（表示可否と対象座標の矩形）。</param>
    /// <remarks>
    /// 拡大中心が未設定（<see cref="LoupeDrawPlan.MagnifiedImage"/> が null）の場合は描かない<br/>
    /// （描く位置を決める入力が無い）。 選択矩形が非表示または空の場合は描かない。
    /// </remarks>
    internal static void Paint(IGraphicsContext context, in LoupeDrawPlan plan, SelectionState selection)
    {
        if (!selection.IsVisible || plan.MagnifiedImage is not { } magnified)
        {
            return;
        }

        var source = selection.Rect;
        if (source.Width <= 0 || source.Height <= 0 || magnified.CropSourceRect.Width <= 0)
        {
            return;
        }

        // 対象 1 ピクセル → 描画先の物理ピクセルの倍率（切り出し領域と描画領域の比）
        var scale = magnified.DrawClientPixelRect.Width / magnified.CropSourceRect.Width;
        var left = (source.X - magnified.CropSourceRect.X) * scale;
        var top = (source.Y - magnified.CropSourceRect.Y) * scale;
        var right = (source.Right - magnified.CropSourceRect.X) * scale;
        var bottom = (source.Bottom - magnified.CropSourceRect.Y) * scale;

        var thickness = plan.ScaleDipToPixel(StrokeThicknessDip);
        var dash = plan.ScaleDipToPixel(DashLengthDip);
        var gap = plan.ScaleDipToPixel(DashGapDip);
        var color = Color.FromArgb(s_strokeColor.A, s_strokeColor.R, s_strokeColor.G, s_strokeColor.B);

        StrokeDashedLine(context, left, top, right, top, color, thickness, dash, gap);
        StrokeDashedLine(context, right, top, right, bottom, color, thickness, dash, gap);
        StrokeDashedLine(context, right, bottom, left, bottom, color, thickness, dash, gap);
        StrokeDashedLine(context, left, bottom, left, top, color, thickness, dash, gap);
    }

    /// <summary>1 本の線分を破線で描く。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="x0">始点の X 座標。</param>
    /// <param name="y0">始点の Y 座標。</param>
    /// <param name="x1">終点の X 座標。</param>
    /// <param name="y1">終点の Y 座標。</param>
    /// <param name="color">線の色。</param>
    /// <param name="thickness">線の太さ。</param>
    /// <param name="dash">破線 1 本の長さ。</param>
    /// <param name="gap">破線の間隔。</param>
    private static void StrokeDashedLine(
        IGraphicsContext context, double x0, double y0, double x1, double y1,
        Color color, double thickness, double dash, double gap)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= 0 || dash <= 0 || gap <= 0)
        {
            context.DrawLine(new Point(x0, y0), new Point(x1, y1), color, thickness);
            return;
        }

        var stepX = dx / length;
        var stepY = dy / length;
        for (var start = 0.0; start < length; start += dash + gap)
        {
            var end = Math.Min(start + dash, length);
            context.DrawLine(
                new Point(x0 + (stepX * start), y0 + (stepY * start)),
                new Point(x0 + (stepX * end), y0 + (stepY * end)),
                color,
                thickness);
        }
    }
}
