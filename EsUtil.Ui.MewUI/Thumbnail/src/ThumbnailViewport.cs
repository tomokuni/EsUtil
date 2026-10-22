using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;
using EsUtil.Ui.MewUI.Thumbnail.Internal;

namespace EsUtil.Ui.MewUI.Thumbnail;

/// <summary>背景・影・画像を描く内部の層（フレームワーク標準の描画）。</summary>
/// <remarks>
/// 描画計画（<see cref="ThumbnailDrawPlan"/>）の値と画素を写すだけであり、判断を持たない。<br/>
/// 影の帯は計画（<see cref="IThumbnailShadowPlan.BuildDeviceBands"/>）が単一所有する。
/// </remarks>
internal sealed class ThumbnailViewport : Control
{
    /// <summary>変換した画像を保持する結び付け。</summary>
    private readonly MewImageBinding _images = new();

    /// <summary>描画計画。</summary>
    public ThumbnailDrawPlan? Plan { get; set; }

    /// <summary>表示する画像。</summary>
    public EsReadOnlyBitmap? Source { get; set; }

    /// <summary>画像の縦横比に応じた要求サイズを返す（一覧が大きさを決める）。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>描画計画の外枠の寸法。</returns>
    protected override Size MeasureContent(Size availableSize)
        => Plan is { } plan ? new Size(plan.Slot.Width, plan.Slot.Height) : new Size(0, 0);

    /// <summary>背景・影・画像を描画する。</summary>
    /// <param name="context">描画先のコンテキスト（DIP 座標）。</param>
    protected override void OnRender(IGraphicsContext context)
    {
        if (Plan is not { } plan || RenderSize.Width <= 0 || RenderSize.Height <= 0)
        {
            return;
        }

        // 背景（ホバー・選択の帯）
        if (plan.BackgroundArgb != 0)
        {
            context.FillRectangle(
                new Rect(0, 0, RenderSize.Width, RenderSize.Height),
                Color.FromArgb(plan.BackgroundArgb));
        }

        // 画像の矩形（外枠を原点とする DIP 座標）
        var imageRect = new EsRect(
            plan.Image.X - plan.Slot.X,
            plan.Image.Y - plan.Slot.Y,
            plan.Image.Width,
            plan.Image.Height);

        if (imageRect.Width <= 0 || imageRect.Height <= 0)
        {
            return;
        }

        // 影は物理ピクセルの帯で描く（画素の格子に一致させる）
        if (plan.Shadow is { } shadow)
        {
            var scaling = DpiScaler.NormalizeScaling(context.DpiScale);
            foreach (var band in shadow.BuildDeviceBands(
                imageRect.X, imageRect.Y, imageRect.Width, imageRect.Height, scaling))
            {
                // 帯は物理ピクセルで作られるため、DIP へ戻してから描く
                context.FillRectangle(
                    new Rect(band.X / scaling, band.Y / scaling, band.Width / scaling, band.Height / scaling),
                    Color.FromArgb(band.Argb));
            }
        }

        if (Source is { } bitmap && !bitmap.IsDisposed)
        {
            if (_images.Resolve(bitmap) is { } image)
            {
                context.ImageScaleQuality = ImageScaleQuality.Fast;
                context.DrawImage(image, new EsRect(0, 0, bitmap.Width, bitmap.Height).ToMewUIRect(), imageRect.ToMewUIRect());
            }
        }
    }
}
