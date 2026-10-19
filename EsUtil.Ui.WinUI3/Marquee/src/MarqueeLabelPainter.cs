using EsUtil.Ui.WinUI3.Marquee.Internal;
using SkiaSharp;

namespace EsUtil.Ui.WinUI3.Marquee;

/// <summary>描画計画のラベル（サイズラベル・カーソル座標ラベル）を Skia の描画 API で描く実装。</summary>
/// <remarks>
/// <b>位置付け</b>: 文言・位置・体裁（背景色・文字色・内側余白・フォントサイズ）は描画層（<see cref="MarqueePresenter"/>）が<br/>
/// <see cref="MarqueeDrawPlan.Labels"/> として解決済みである。 本クラスは配置指示を写すだけで、判断を持たない。<br/>
/// <br/>
/// <b>なぜ要素ではなく描画面へ描くか</b>: 本 UI のコントロールは描画面（<c>SKXamlCanvas</c>）そのものであり、<br/>
/// ラベルを子要素として重ねるには要素のレイアウトをもう一度走らせる必要がある。<br/>
/// 同じ描画面へ描けば、実寸の測定（<see cref="SkiaText.Measure"/>）と描画（<see cref="SkiaText.Draw"/>）が同じエンジンになり、<br/>
/// 背景と文字の寸法が食い違わない（文字が背景からはみ出して切れる不具合を作らない）。<br/>
/// <br/>
/// <b>表示可否は描画側が判断する</b>: 計画は表示可否（外観の <c>Show*</c>）で指示を消さないため、<br/>
/// 描くかどうかは本クラスが配置指示の <see cref="MarqueeLabelPlacement.IsVisible"/> を見て決める。
/// </remarks>
internal static class MarqueeLabelPainter
{
    /// <summary>ラベルを配置指示の順に描く（表示が有効なものだけ）。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。 null 不可。</param>
    /// <param name="plan">描画層が解決した描画計画（配置指示・体裁を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>
    /// <b>背景は配置指示の矩形そのもの</b>である（矩形は「文字の実寸 + 内側余白 × 2」として描画層が解決済み）。<br/>
    /// 文字は同じ内側余白だけ内側へ入れて描く（要素の内側余白と同じ規則）。
    /// </remarks>
    public static void Paint(SKCanvas canvas, in MarqueeDrawPlan plan)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var labels = plan.Labels;

        for (var index = 0; index < labels.Count; index++)
        {
            var label = labels[index];
            if (!label.IsVisible || label.Text.Length == 0)
            {
                continue;
            }

            SkiaFill.Rect(canvas, label.DipRect, label.BackgroundColor);
            SkiaText.Draw(
                canvas,
                label.Text,
                label.DipRect.X + label.PaddingX,
                label.DipRect.Y + label.PaddingY,
                label.FontSize,
                label.TextColor);
        }
    }
}
