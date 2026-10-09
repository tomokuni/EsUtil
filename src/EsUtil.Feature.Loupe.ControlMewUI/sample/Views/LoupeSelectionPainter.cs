using SkiaSharp;

namespace EsUtil.Feature.Loupe.MewUI.Sample.Views;

/// <summary>ルーペ画像の上へ選択矩形の破線を描く（<c>LoupeControl.Rendered</c> の使用例）。</summary>
/// <remarks>
/// <b>示すこと</b>: 描画完了イベントのキャンバスへ、ホスト固有の付加情報（選択矩形の破線）を追加描画できる。<br/>
/// 描画位置は描画計画（<see cref="LoupeDrawPlan.MagnifiedImage"/>）から求める。<br/>
/// 切り出し領域（対象座標）と描画領域（クライアントの物理ピクセル）の比が「対象 1 ピクセル → 描画先の物理ピクセル」の倍率であるため、<br/>
/// 対象座標の矩形をこの倍率で換算すれば拡大画像と正確に一致する（切り出しの位置ずれ（切り上げ）も自動的に吸収される）。<br/>
/// <br/>
/// <b>破線と太さの単位</b>: 描画面は物理ピクセルのため、DIP で見せたい寸法は <see cref="LoupeDrawPlan.ScaleDipToPixel"/> で換算する。
/// </remarks>
internal static class LoupeSelectionPainter
{
    /// <summary>枠線の色（オレンジ）。</summary>
    private static readonly SKColor s_strokeColor = SKColor.Parse("#FF8C00");

    /// <summary>破線 1 本の長さ（DIP）。</summary>
    private const float DashLengthDip = 4.0f;

    /// <summary>破線の間隔（DIP）。</summary>
    private const float DashGapDip = 2.0f;

    /// <summary>枠線の太さ（DIP）。</summary>
    private const float StrokeThicknessDip = 2.0f;

    /// <summary>選択矩形の破線をルーペ画像へ描く。</summary>
    /// <param name="canvas">描画完了イベントが渡したキャンバス（描画中のみ有効）。</param>
    /// <param name="plan">実際に使われた描画計画（切り出しと描画の矩形を持つ）。</param>
    /// <param name="selection">選択矩形の状態（表示可否と対象座標の矩形）。</param>
    /// <remarks>
    /// 拡大中心が未設定（<see cref="LoupeDrawPlan.MagnifiedImage"/> が null）の場合は描かない<br/>
    /// （描く位置を決める入力が無い）。 選択矩形が非表示または空の場合は描かない。
    /// </remarks>
    internal static void Paint(SKCanvas canvas, in LoupeDrawPlan plan, SelectionState selection)
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
        var draw = new SKRect(
            (float)((source.X - magnified.CropSourceRect.X) * scale),
            (float)((source.Y - magnified.CropSourceRect.Y) * scale),
            (float)((source.Right - magnified.CropSourceRect.X) * scale),
            (float)((source.Bottom - magnified.CropSourceRect.Y) * scale));

        var thickness = (float)plan.ScaleDipToPixel(StrokeThicknessDip);

        canvas.Save();
        try
        {
            using var stroke = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = s_strokeColor,
                StrokeWidth = thickness,
                PathEffect = SKPathEffect.CreateDash(
                    [plan.ScaleDipToPixel(DashLengthDip), plan.ScaleDipToPixel(DashGapDip)], 0),
                IsAntialias = false,
            };
            canvas.DrawRect(draw, stroke);
        }
        finally
        {
            canvas.Restore();
        }
    }
}
