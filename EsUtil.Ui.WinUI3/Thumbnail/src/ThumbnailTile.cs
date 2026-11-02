using EsUtil.Ui.WinUI3.Thumbnail.Internal;
using EsUtil.Platform.Core.Imaging;
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

    /// <summary>強調の帯の色（UI がテーマから解決した値）。</summary>
    private uint _highlightArgb;

    /// <summary>名前の実効の文字色を取得する（テーマが決めた値。 強調の帯の色を導くのに使う）。</summary>
    /// <value>解決済みのブラシ。 未解決の場合は null。</value>
    public Brush? LabelForeground => _label.Foreground;

    /// <summary>ツールヒントの幅の上限（DIP）。 名前が長い場合はこの幅で折り返す。</summary>
    private const double ToolTipMaxWidth = 280;

    /// <summary>ツールヒントの内容を作る（長い名前は折り返す）。</summary>
    /// <param name="text">表示する文言。</param>
    /// <returns>ツールヒントの要素。</returns>
    private static TextBlock CreateToolTip(string text)
        => new() { Text = text, MaxWidth = ToolTipMaxWidth, TextWrapping = TextWrapping.Wrap };

    /// <summary>表示する画像。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>サムネイル 1 件分の要素を生成する。</summary>
    /// <param name="image">表示する画像。 null の場合は画像を描かない。</param>
    /// <param name="caption">表示する名前。</param>
    /// <param name="toolTip">ツールヒント。 null または空の場合は表示しない。</param>
    /// <param name="tag">項目へ付ける任意の値。</param>
    public ThumbnailTile(EsReadOnlyBitmap? image, string caption, string? toolTip, object? tag)
    {
        Tag = tag;
        Caption = caption;

        // 入力は一覧が位置から判定する（子要素のヒット テストに依存しない）
        IsHitTestVisible = false;
        _viewport.IsHitTestVisible = false;
        _label.IsHitTestVisible = false;

        Children.Add(_viewport);
        Children.Add(_label);
        // ツールヒントには省略していない名前を出す（折り返して全体を読めるようにする）。
        // 子の要素がポインターを受け取るため、子にも同じ内容を設定する（親をたどって解決する実装に依存しない）
        var tipText = string.IsNullOrEmpty(toolTip) ? caption : $"{caption}{Environment.NewLine}{toolTip}";
        ToolTipService.SetToolTip(this, CreateToolTip(tipText));
        ToolTipService.SetToolTip(_viewport, CreateToolTip(tipText));
        ToolTipService.SetToolTip(_label, CreateToolTip(tipText));

        SetImage(image);
        SetCaption(caption);
    }

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
        => Apply(plan, options, 0);

    /// <summary>描画計画を適用する（強調の帯の色を指定する）。</summary>
    /// <param name="plan">1 件分の描画計画。</param>
    /// <param name="options">計算に必要な設定（名前の余白）。</param>
    /// <param name="highlightArgb">強調の帯の色（<c>0xAARRGGBB</c>。 0 は描かない）。 UI がテーマから解決して渡す。</param>
    public void Apply(ThumbnailDrawPlan plan, ThumbnailOptions options, uint highlightArgb)
    {
        _plan = plan;
        _highlightArgb = highlightArgb;
        ApplyVisual();

        _label.FontSize = plan.Label.FontSize;
        // 文言は計画が決める（2 行に収まらない名前は拡張子を残して省略済み）
        _label.Text = plan.Label.Text;
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
        _viewport.Apply(plan, _source, 1 / scaling, _highlightArgb);
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
