using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace EsUtil.Feature.Thumbnail.AvaloniaUI;

/// <summary>差し替えた影の算法が返す帯を塗る面。</summary>
/// <remarks>
/// <b>既定の算法（<see cref="ThumbnailShadowPlan"/>）は使わない</b>: 既定は <c>BoxShadow</c> が描く<br/>
/// （そのぼかしが計画の濃さのプロファイルの元である）。 本クラスは <see cref="IThumbnailShadowPlan"/> を<br/>
/// 実装した別の算法（例: ぼかしを使う・ずらす・描かない）の帯を単色の矩形として塗る。<br/>
/// <b>端数の扱い</b>: 物理ピクセルの帯を DIP へ戻すとき、わずかに内側へ寄せる<br/>
/// （割り切れない倍率で掛け戻すと 1 画素の隙間ができる。 実測）。
/// </remarks>
internal sealed class ThumbnailShadowBandsView : Control
{
    /// <summary>端数の丸めで生じる隙間を埋めるための内側への寄せ（DIP）。</summary>
    private const double Bias = 0.001;

    /// <summary>色ごとのブラシ（描画のたびに作り直さない）。</summary>
    private readonly Dictionary<uint, IBrush> _brushes = [];

    /// <summary>適用した描画計画。</summary>
    private ThumbnailDrawPlan? _plan;

    /// <summary>適用した描画計画を取得または設定する。</summary>
    /// <value>描画計画。 既定の算法・影なしの場合は null（何も描かない）。</value>
    public ThumbnailDrawPlan? Plan
    {
        get => _plan;
        set
        {
            if (_plan == value)
            {
                return;
            }

            _plan = value;
            InvalidateVisual();
        }
    }

    /// <summary>影の帯を描く。</summary>
    /// <param name="context">描画先。</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_plan is not { Shadow: { } shadow } plan || plan.Image.IsEmpty)
        {
            return;
        }

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        if (scaling <= 0)
        {
            return;
        }

        // 本要素は影を含む矩形へ配置されるため、画像の矩形を自分の座標へ移す
        var imageX = plan.Image.X - plan.ShadowArea.X;
        var imageY = plan.Image.Y - plan.ShadowArea.Y;
        foreach (var band in shadow.BuildDeviceBands(imageX, imageY, plan.Image.Width, plan.Image.Height, scaling))
        {
            context.DrawRectangle(Brush(band.Argb), null, ToDip(band, scaling));
        }
    }

    /// <summary>色のブラシを取得する。</summary>
    /// <param name="argb">色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>不変のブラシ。</returns>
    private IBrush Brush(uint argb)
    {
        if (_brushes.TryGetValue(argb, out var brush))
        {
            return brush;
        }

        brush = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        _brushes[argb] = brush;
        return brush;
    }

    /// <summary>物理ピクセルの帯を DIP の矩形へ変換する。</summary>
    /// <param name="band">帯（物理ピクセル）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率。</param>
    /// <returns>DIP の矩形（格子に一致する）。</returns>
    private static Rect ToDip(ThumbnailShadowBand band, double scaling)
        => new(
            (band.X + Bias) / scaling,
            (band.Y + Bias) / scaling,
            (band.Width - (Bias * 2)) / scaling,
            (band.Height - (Bias * 2)) / scaling);
}
