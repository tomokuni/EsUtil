using DrawingColor = System.Drawing.Color;
using DrawingPen = System.Drawing.Pen;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace EsUtil.Feature.Loupe.WinForms.Sample.Views;

/// <summary>ルーペ画像の上へ選択矩形の破線を描く（<c>LoupeControl.Rendered</c> の使用例）。</summary>
/// <remarks>
/// <b>示すこと</b>: 描画完了イベントの描画面へ、ホスト固有の付加情報（選択矩形の破線）を追加描画できる。<br/>
/// 描画位置は描画計画（<see cref="LoupeDrawPlan.MagnifiedImage"/>）から求める。<br/>
/// 切り出し領域（対象座標）と描画領域（クライアントの物理ピクセル）の比が「対象 1 ピクセル → 描画先の物理ピクセル」の倍率であるため、<br/>
/// 対象座標の矩形をこの倍率で換算すれば拡大画像と正確に一致する（切り出しの位置ずれ（切り上げ）も自動的に吸収される）。<br/>
/// <br/>
/// <b>破線と太さの単位</b>: 描画面は物理ピクセルのため、DIP で見せたい寸法は<br/>
/// <see cref="LoupeDrawPlan.ScaleDipToPixel(double)"/> で換算する。
/// </remarks>
internal static class LoupeSelectionPainter
{
    /// <summary>枠線の色（オレンジ）。</summary>
    private static readonly DrawingColor s_strokeColor = DrawingColor.FromArgb(255, 255, 140, 0);

    /// <summary>破線の線の長さ（DIP）。</summary>
    private const double DashLengthDip = 4.0;

    /// <summary>破線の間隔（DIP）。</summary>
    private const double DashGapDip = 2.0;

    /// <summary>枠線の太さ（DIP）。</summary>
    private const double StrokeThicknessDip = 2.0;

    /// <summary>選択矩形の破線をルーペの描画面へ描く。</summary>
    /// <param name="graphics">描画完了イベントが渡した描画面（描画中のみ有効）。 null 不可。</param>
    /// <param name="plan">実際に使われた描画計画（切り出しと描画の矩形を持つ）。</param>
    /// <param name="selection">選択矩形の状態（表示可否と対象座標の矩形）。 null 不可。</param>
    /// <remarks>
    /// 拡大中心が未設定（<see cref="LoupeDrawPlan.MagnifiedImage"/> が null）の場合は描かない<br/>
    /// （描く位置を決める入力が無い）。 選択矩形が非表示または空の場合は描かない。
    /// </remarks>
    internal static void Paint(Graphics graphics, in LoupeDrawPlan plan, SelectionState selection)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(selection);

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
        var draw = new DrawingRectangleF(
            (float)((source.X - magnified.CropSourceRect.X) * scale),
            (float)((source.Y - magnified.CropSourceRect.Y) * scale),
            (float)(source.Width * scale),
            (float)(source.Height * scale));

        var thickness = (float)plan.ScaleDipToPixel(StrokeThicknessDip);
        using var pen = new DrawingPen(s_strokeColor, thickness);

        // 破線の長さと間隔は線の太さを単位とするため、物理ピクセルの長さを太さで割って指定する
        pen.DashPattern =
        [
            (float)(plan.ScaleDipToPixel(DashLengthDip) / thickness),
            (float)(plan.ScaleDipToPixel(DashGapDip) / thickness),
        ];

        graphics.DrawRectangle(pen, draw.X, draw.Y, draw.Width, draw.Height);
    }
}
