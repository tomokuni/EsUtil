using EsUtil.Ui.WinUI3.Thumbnail.Internal;
using EsUtil.Platform.Core.Imaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;

namespace EsUtil.Ui.WinUI3.Thumbnail;

/// <summary>サムネイル 1 件分の要素（背景・影・画像・名前）。</summary>
/// <remarks>
/// <b>構成</b>: Skia の描画面（背景・影・画像）と名前の要素を重ねる。 矩形は描画計画<br/>
/// （<see cref="ThumbnailDrawPlan"/>）を写すだけであり、本クラスは計算を持たない。<br/>
/// <b>影は帯で塗る</b>: 計画（<see cref="ThumbnailShadowPlan"/>）の 1 画素の帯を単色で塗る<br/>
/// （縮小キャンバスの内容は XAML 要素より背面に合成されるため、影を要素で重ねると画像が隠れる）。
/// </remarks>
internal sealed class ThumbnailTile : Grid
{
    /// <summary>背景・影・画像を描く面。</summary>
    private readonly ThumbnailViewport _viewport = new();

    /// <summary>名前の要素。</summary>
    private readonly TextBlock _label = new();

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

        // 入力は一覧が位置から判定する（子要素のヒット テストに依存しない）
        IsHitTestVisible = false;
        _viewport.IsHitTestVisible = false;
        _label.IsHitTestVisible = false;

        Children.Add(_viewport);
        Children.Add(_label);
        if (!string.IsNullOrEmpty(toolTip))
        {
            ToolTipService.SetToolTip(this, toolTip);
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
        _label.TextWrapping = TextWrapping.Wrap;
        _label.MaxLines = 2;
        _label.TextTrimming = TextTrimming.CharacterEllipsis;
        _label.FontWeight = plan.Label.IsBold
            ? Microsoft.UI.Text.FontWeights.SemiBold
            : Microsoft.UI.Text.FontWeights.Normal;
        _label.Padding = new Thickness(
            options.LabelHorizontalPadding, plan.Label.VerticalPadding, options.LabelHorizontalPadding, plan.Label.VerticalPadding);

        if (plan.Label.ForegroundArgb == 0)
        {
            // 外観が色を指定しない場合はテーマの前色を継承する
            _label.ClearValue(TextBlock.ForegroundProperty);
        }
        else
        {
            _label.Foreground = new SolidColorBrush(
                ColorHelper.FromArgb(
                    (byte)(plan.Label.ForegroundArgb >> 24),
                    (byte)(plan.Label.ForegroundArgb >> 16),
                    (byte)(plan.Label.ForegroundArgb >> 8),
                    (byte)plan.Label.ForegroundArgb));
        }

        InvalidateMeasure();
    }

    /// <summary>子要素を計測する（寸法は描画計画が決める）。</summary>
    /// <param name="availableSize">利用可能なサイズ。</param>
    /// <returns>項目の外枠の寸法。</returns>
    protected override Size MeasureOverride(Size availableSize)
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
    /// <param name="finalSize">最終的なサイズ。</param>
    /// <returns>配置したサイズ。</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_plan is not { } plan)
        {
            return finalSize;
        }

        _viewport.Arrange(new Rect(0, 0, plan.Slot.Width, plan.Slot.Height));
        _label.Arrange(new Rect(
            plan.Label.Bounds.X - plan.Slot.X,
            plan.Label.Bounds.Y - plan.Slot.Y,
            plan.Label.Bounds.Width,
            plan.Label.Bounds.Height));
        return finalSize;
    }

    /// <summary>背景・影・画像を Skia で描く面。</summary>
    private sealed class ThumbnailViewport : SKXamlCanvas
    {
        /// <summary>描画計画。</summary>
        public ThumbnailDrawPlan? Plan { get; set; }

        /// <summary>表示する画像。</summary>
        public EsReadOnlyBitmap? Source { get; set; }

        /// <summary>描画する。</summary>
        /// <param name="sender">イベントの送信元。</param>
        /// <param name="e">描画先の表面と情報。</param>
        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (Plan is not { } plan || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            // 描画面（物理ピクセル）と表示領域（DIP）の比から倍率を求める
            var scaling = e.Info.Width / ActualWidth;

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
                    new SKRect(0, 0, (float)(ActualWidth * scaling), (float)(ActualHeight * scaling)), background);
            }

            var imageRect = new SKRect(
                (float)(plan.Image.X - plan.Slot.X),
                (float)(plan.Image.Y - plan.Slot.Y),
                (float)(plan.Image.X - plan.Slot.X + plan.Image.Width),
                (float)(plan.Image.Y - plan.Slot.Y + plan.Image.Height));

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
                    using var view = bitmap.ToSKBitmap();
                    canvas.DrawBitmap(view, imageRect);
                }
                finally
                {
                    canvas.Restore();
                }
            }
        }

        /// <summary>面を生成する（描画の配線だけを行う）。</summary>
        public ThumbnailViewport()
        {
            PaintSurface += OnPaintSurface;
        }
    }
}
