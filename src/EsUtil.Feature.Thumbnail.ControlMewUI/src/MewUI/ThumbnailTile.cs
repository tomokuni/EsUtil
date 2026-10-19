using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Skia.Controls;
using EsUtil.Feature.Thumbnail.MewUI.Internal;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

namespace EsUtil.Feature.Thumbnail.MewUI;

/// <summary>サムネイル 1 件分の要素（背景・影・画像・名前）。</summary>
/// <remarks>
/// <b>構成</b>: Skia の描画面（背景・影・画像）と名前の要素を重ねる。 矩形は描画計画<br/>
/// （<see cref="ThumbnailDrawPlan"/>）を写すだけであり、本クラスは計算を持たない。<br/>
/// <b>影は帯で塗る</b>: 計画（<see cref="ThumbnailShadowPlan"/>）の 1 画素の帯を単色で塗る<br/>
/// （グラデーション ブラシの端の扱いに依存しない）。
/// </remarks>
internal sealed class ThumbnailTile : Panel
{
    /// <summary>背景・影・画像を描く面。</summary>
    private readonly ThumbnailViewport _viewport = new();

    /// <summary>名前の要素。</summary>
    private readonly Label _label = new();

    /// <summary>最後に適用した描画計画。</summary>
    private ThumbnailDrawPlan? _plan;

    /// <summary>サムネイル 1 件分の要素を生成する。</summary>
    /// <param name="image">表示する画像。 null の場合は画像を描かない。</param>
    /// <param name="caption">表示する名前。</param>
    /// <param name="toolTip">ツールヒント。 null または空の場合は表示しない。</param>
    /// <param name="tag">項目へ付ける任意の値。</param>
    /// <param name="isCurrent">現在表示中の画像かどうか。</param>
    public ThumbnailTile(EsReadOnlyBitmap? image, string caption, string? toolTip, object? tag, bool isCurrent)
    {
        Tag = tag;
        IsCurrent = isCurrent;
        Caption = caption;

        Add(_viewport);
        Add(_label);
        if (!string.IsNullOrEmpty(toolTip))
        {
            ToolTip = new Label { Text = toolTip };
        }

        SetImage(image);
        SetCaption(caption);
    }

    /// <summary>現在表示中の画像かどうかを取得する。</summary>
    public bool IsCurrent { get; }

    /// <summary>表示する名前を取得する。</summary>
    public string Caption { get; private set; }

    /// <summary>表示する画像を取得する。</summary>
    public EsReadOnlyBitmap? Source => _viewport.Source;

    /// <summary>表示する画像を設定する。</summary>
    /// <param name="image">画像。 null の場合は画像を描かない。</param>
    public void SetImage(EsReadOnlyBitmap? image)
    {
        _viewport.Source = image;
        InvalidateMeasure();
    }

    /// <summary>表示する名前を設定する。</summary>
    /// <param name="caption">名前。</param>
    public void SetCaption(string caption)
    {
        Caption = caption;
        _label.Text = caption;
        InvalidateMeasure();
    }

    /// <summary>描画計画を適用する。</summary>
    /// <param name="plan">1 件分の描画計画。</param>
    /// <param name="options">計算に必要な設定（名前の余白）。</param>
    public void Apply(ThumbnailDrawPlan plan, ThumbnailOptions options)
    {
        _plan = plan;
        _viewport.Plan = plan;

        _label.FontSize = plan.Label.FontSize;
        _label.TextAlignment = TextAlignment.Center;
        _label.VerticalTextAlignment = TextAlignment.Top;
        _label.TextWrapping = TextWrapping.Wrap;
        _label.Padding = new Thickness(
            options.LabelHorizontalPadding, plan.Label.VerticalPadding, options.LabelHorizontalPadding, plan.Label.VerticalPadding);
        _label.FontWeight = plan.Label.IsBold ? FontWeight.Bold : FontWeight.Normal;

        if (plan.Label.ForegroundArgb != 0)
        {
            _label.Foreground = Color.FromArgb(plan.Label.ForegroundArgb);
        }

        InvalidateMeasure();
    }

    /// <summary>子要素を計測する（寸法は描画計画が決める）。</summary>
    /// <param name="availableSize">利用可能なサイズ。</param>
    /// <returns>項目の外枠の寸法。</returns>
    protected override Size MeasureContent(Size availableSize)
    {
        if (_plan is not { } plan)
        {
            return new Size(0, 0);
        }

        _viewport.Measure(new Size(plan.Slot.Width, plan.Slot.Height));
        _label.Measure(new Size(plan.Label.Bounds.Width, plan.Label.Bounds.Height));
        return new Size(plan.Slot.Width, plan.Slot.Height);
    }

    /// <summary>子要素を配置する。</summary>
    /// <param name="bounds">本要素へ割り当てられた矩形。</param>
    protected override void ArrangeContent(Rect bounds)
    {
        if (_plan is not { } plan)
        {
            return;
        }

        _viewport.Arrange(new Rect(0, 0, plan.Slot.Width, plan.Slot.Height));
        _label.Arrange(new Rect(
            plan.Label.Bounds.X - plan.Slot.X,
            plan.Label.Bounds.Y - plan.Slot.Y,
            plan.Label.Bounds.Width,
            plan.Label.Bounds.Height));
    }

    /// <summary>背景・影・画像を Skia で描く面。</summary>
    private sealed class ThumbnailViewport : SkiaCanvasView
    {
        /// <summary>描画計画。</summary>
        public ThumbnailDrawPlan? Plan { get; set; }

        /// <summary>表示する画像。</summary>
        public EsReadOnlyBitmap? Source { get; set; }

        /// <summary>面を生成する。</summary>
        public ThumbnailViewport()
        {
            ContinuousAnimation = false;
            PaintSurface += OnPaintSurface;
        }

        /// <summary>画像の縦横比に応じた要求サイズを返す（一覧が大きさを決める）。</summary>
        /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
        /// <returns>描画計画の外枠の寸法。</returns>
        protected override Size MeasureContent(Size availableSize)
            => Plan is { } plan ? new Size(plan.Slot.Width, plan.Slot.Height) : new Size(0, 0);

        /// <summary>背景・影・画像を描画する。</summary>
        /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
        /// <param name="info">描画先の情報（幅・高さ）。</param>
        private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
        {
            canvas.Clear(SKColors.Transparent);

            if (Plan is not { } plan || Bounds.Size.Width <= 0 || Bounds.Size.Height <= 0)
            {
                return;
            }

            var scaling = ClientPixelScale.Resolve(info.Width, Bounds.Size.Width, fallback: 1.0);

            // 背景（ホバー・選択の帯）
            if (plan.BackgroundArgb != 0)
            {
                using var background = new SKPaint
                {
                    IsAntialias = false,
                    Style = SKPaintStyle.Fill,
                    Color = new SKColor(
                        (byte)(plan.BackgroundArgb >> 16),
                        (byte)(plan.BackgroundArgb >> 8),
                        (byte)plan.BackgroundArgb,
                        (byte)(plan.BackgroundArgb >> 24)),
                };
                canvas.DrawRect(
                    new SKRect(
                        0,
                        0,
                        (float)(Bounds.Size.Width * scaling),
                        (float)(Bounds.Size.Height * scaling)),
                    background);
            }

            // 画像の矩形（外枠を原点とする DIP 座標）
            var imageRect = new SKRect(
                (float)(plan.Image.X - plan.Slot.X),
                (float)(plan.Image.Y - plan.Slot.Y),
                (float)(plan.Image.X - plan.Slot.X + plan.Image.Width),
                (float)(plan.Image.Y - plan.Slot.Y + plan.Image.Height));

            // 影は物理ピクセルで描く（帯の矩形が画素に一致する）
            if (plan.Shadow is { } shadow && !imageRect.IsEmpty)
            {
                SkiaShadowRenderer.Draw(
                    canvas,
                    shadow.BuildDeviceBands(
                        imageRect.Left, imageRect.Top, imageRect.Width, imageRect.Height, scaling));
            }

            if (Source is { } bitmap && !bitmap.IsDisposed && !imageRect.IsEmpty)
            {
                canvas.Save();
                canvas.Scale((float)scaling);
                try
                {
                    // 非所有ビュー（ピクセルは複製しない。 ファイナライザ対策で直後に破棄する）
                    using var view = bitmap.ToSKBitmap();
                    canvas.DrawBitmap(view, imageRect);
                }
                finally
                {
                    canvas.Restore();
                }
            }
        }
    }
}
