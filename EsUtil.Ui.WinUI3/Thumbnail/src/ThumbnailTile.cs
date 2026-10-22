using EsUtil.Ui.WinUI3.Thumbnail.Internal;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace EsUtil.Ui.WinUI3.Thumbnail;

/// <summary>サムネイル 1 件分の要素（背景・影・画像・名前）。</summary>
/// <remarks>
/// <b>構成</b>: 背景・影・画像の層と名前の要素を重ねる。 矩形は描画計画<br/>
/// （<see cref="ThumbnailDrawPlan"/>）を写すだけであり、本クラスは計算を持たない。<br/>
/// <b>影は帯で塗る</b>: 計画（<see cref="ThumbnailShadowPlan"/>）の 1 画素の帯を単色で塗る<br/>
/// （縮小キャンバスの内容は XAML 要素より背面に合成されるため、影を要素で重ねると画像が隠れる）。
/// </remarks>
internal sealed partial class ThumbnailTile : Grid
{
    /// <summary>背景・影・画像を描く面。</summary>
    private readonly ThumbnailVisualLayer _viewport = new();

    /// <summary>名前の要素。</summary>
    private readonly TextBlock _label = new();

    /// <summary>最後に適用した描画計画。</summary>
    private ThumbnailDrawPlan? _plan;

    /// <summary>表示する画像。</summary>
    private EsReadOnlyBitmap? _source;

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
    public EsReadOnlyBitmap? Source => _source;

    /// <summary>表示する画像を設定する。</summary>
    /// <param name="image">画像。 null の場合は画像を描かない。</param>
    public void SetImage(EsReadOnlyBitmap? image)
    {
        _source = image;
        ApplyVisual();
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
        ApplyVisual();

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
    }

    /// <summary>背景・影・画像を計画のとおりに反映する。</summary>
    /// <remarks>
    /// 表示の更新は本メソッドへ集約する（画像の差し替えと計画の適用の両方から呼ぶ）。<br/>
    /// 影の帯は物理ピクセルで解決されるため、DIP へ戻す係数（1 / スケーリング係数）を渡す。
    /// </remarks>
    private void ApplyVisual()
    {
        if (_plan is not { } plan)
        {
            return;
        }

        var scaling = XamlRoot?.RasterizationScale ?? 1;
        _viewport.Apply(plan, _source, 1 / scaling);
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
}
