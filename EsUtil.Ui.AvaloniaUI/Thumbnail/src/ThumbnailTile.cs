using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.Thumbnail;

/// <summary>サムネイル 1 件分の要素（背景・影・画像・名前）。</summary>
/// <remarks>
/// <b>構成</b>: 背景（ホバー・選択の帯）→ 影 → 画像 → 名前 の順に重ねる。<br/>
/// 位置は描画計画（<see cref="ThumbnailDrawPlan"/>）の矩形をそのまま写す（本クラスは計算を持たない）。<br/>
/// <b>影の描き方は計画の実装で決まる</b>: 既定の <see cref="ThumbnailShadowPlan"/> は <c>BoxShadow</c> に任せる<br/>
/// （Avalonia の <c>BoxShadow</c> が計画の濃さのプロファイルの元である）。 それ以外の<br/>
/// <see cref="IThumbnailShadowPlan"/>（影の算法を差し替えたもの）は、計画が返す帯を自前で塗る。
/// </remarks>
internal sealed class ThumbnailTile : Panel
{
    /// <summary>背景（ホバー・選択の帯）。</summary>
    private readonly Border _background = new();

    /// <summary>差し替えた影の算法の帯を塗る面（既定の計画では空）。</summary>
    private readonly ThumbnailShadowBandsView _shadowBands = new();

    /// <summary>影を付ける枠（画像の矩形に置く）。</summary>
    private readonly Border _shadowFrame = new();

    /// <summary>画像。</summary>
    private readonly Image _image = new() { Stretch = Stretch.Fill };

    /// <summary>名前。</summary>
    private readonly TextBlock _label = new()
    {
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Top,
    };

    /// <summary>名前の実効の文字色を取得する（テーマが決めた値。 強調の帯の色を導くのに使う）。</summary>
    /// <value>解決済みのブラシ。 未解決の場合は null。</value>
    public IBrush? LabelForeground => _label.Foreground;

    /// <summary>最後に適用した描画計画。</summary>
    private ThumbnailDrawPlan? _plan;

    /// <summary>強調の帯の色（UI がテーマから解決した値）。</summary>
    private uint _highlightArgb;

    /// <summary>ツールヒントの幅の上限（DIP）。 名前が長い場合はこの幅で折り返す。</summary>
    private const double ToolTipMaxWidth = 280;

    /// <summary>ツールヒントの内容を作る（長い名前は折り返す）。</summary>
    /// <param name="text">表示する文言。</param>
    /// <returns>ツールヒントの要素。</returns>
    private static TextBlock CreateToolTip(string text)
        => new() { Text = text, MaxWidth = ToolTipMaxWidth, TextWrapping = TextWrapping.Wrap };

    /// <summary>表示する画像（高さの計算に使う）。</summary>
    private IImage? _source;

    /// <summary>画像の幅（DIP）。</summary>
    private double _imageWidth;

    /// <summary>サムネイル 1 件分の要素を生成する。</summary>
    /// <param name="image">表示する画像。 null の場合は画像を描かない。</param>
    /// <param name="caption">表示する名前。</param>
    /// <param name="toolTip">ツールヒント。</param>
    /// <param name="tag">項目へ付ける任意の値。</param>
    /// <param name="isCurrent">現在表示中の画像かどうか。</param>
    public ThumbnailTile(IImage? image, string caption, string? toolTip, object? tag, bool isCurrent)
    {
        Tag = tag;
        IsCurrent = isCurrent;
        Caption = caption;

        Children.Add(_background);
        Children.Add(_shadowBands);
        _shadowFrame.Child = _image;
        Children.Add(_shadowFrame);
        Children.Add(_label);

        // 入力は一覧（親）が位置から判定する（子要素のヒット テストに依存しない）
        _background.IsHitTestVisible = false;
        _shadowBands.IsHitTestVisible = false;
        _shadowFrame.IsHitTestVisible = false;
        _image.IsHitTestVisible = false;
        _label.IsHitTestVisible = false;

        SetImage(image);
        SetCaption(caption);
        // ツールヒントには省略していない名前を出す（折り返して全体を読めるようにする）。
        // 子の要素がポインターを受け取るため、子にも同じ内容を設定する（親をたどって解決する実装に依存しない）
        var tipText = string.IsNullOrEmpty(toolTip) ? caption : $"{caption}{Environment.NewLine}{toolTip}";
        ToolTip.SetTip(this, CreateToolTip(tipText));
        ToolTip.SetTip(_image, CreateToolTip(tipText));
        ToolTip.SetTip(_label, CreateToolTip(tipText));
    }

    /// <summary>現在表示中の画像かどうかを取得する。</summary>
    /// <remarks>一覧の <c>CurrentIndex</c> が変更したときに切り替える（作成後は一覧だけが変更できる）。</remarks>
    public bool IsCurrent { get; private set; }

    /// <summary>現在表示中の画像かどうかを設定する。</summary>
    /// <param name="value">現在表示中の画像の場合は true。</param>
    internal void SetIsCurrent(bool value) => IsCurrent = value;

    /// <summary>表示する名前を取得する。</summary>
    public string Caption { get; private set; }

    /// <summary>画像の幅（DIP）を取得または設定する。</summary>
    public double ImageWidth
    {
        get => _imageWidth;
        set
        {
            if (_imageWidth == value)
            {
                return;
            }

            _imageWidth = value;
            RecomputeHeight();
        }
    }

    /// <summary>画像の表示高さ（DIP）を取得する。</summary>
    /// <value>画像の縦横比から決まる高さ。 画像が無い場合は幅と同じ値（正方形）。</value>
    public double ImageHeight { get; private set; }

    /// <summary>表示する画像を取得する。</summary>
    /// <value>設定されている画像。 未設定は null。</value>
    public IImage? CurrentImage => _source;

    /// <summary>表示する画像を設定する。</summary>
    /// <param name="image">画像。 null の場合は画像を描かない。</param>
    public void SetImage(IImage? image)
    {
        _source = image;
        _image.Source = image;
        RecomputeHeight();
    }

    /// <summary>画像の縦横比から表示高さを求め直す。</summary>
    private void RecomputeHeight()
    {
        ImageHeight = _source is null
            ? _imageWidth
            : ThumbnailPresenter.ImageHeight(_imageWidth, (int)_source.Size.Width, (int)_source.Size.Height);
        InvalidateMeasure();
    }

    /// <summary>表示する名前を設定する。</summary>
    /// <param name="caption">名前。</param>
    public void SetCaption(string caption)
    {
        Caption = caption;
        _label.Text = caption;
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

        _background.Background = plan.Highlight == ThumbnailHighlightKind.None
            ? null
            : new SolidColorBrush(Color.FromUInt32(_highlightArgb));

        // 影の有無は計画が決める（強調中に隠す UI がある）
        if (plan.Shadow is ThumbnailShadowPlan shadow)
        {
            // 既定の算法は BoxShadow に任せる（4 辺・4 隅をそのまま正しく描く）
            _shadowFrame.BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,

                // 上下左右へ同じだけ落とす（ずらすと上辺だけ薄くなり、画像との間に白っぽい隙間が見える）
                OffsetY = 0,
                Blur = shadow.BlurRadius,
                Spread = 0,
                Color = Color.FromUInt32(shadow.ColorArgb),
            });
            _shadowBands.Plan = null;
        }
        else
        {
            // 差し替えた算法は計画が返す帯を塗る（影なしの場合は null）
            _shadowFrame.BoxShadow = default;
            _shadowBands.Plan = plan.Shadow is null ? null : plan;
        }

        // 文言は計画が決める（2 行に収まらない名前は拡張子を残して省略済み）
        _label.Text = plan.Label.Text;
        _label.FontSize = plan.Label.FontSize;
        _label.FontWeight = plan.Label.IsBold ? FontWeight.Bold : FontWeight.Normal;
        _label.Padding = new Thickness(options.LabelHorizontalPadding, plan.Label.VerticalPadding);
        if (plan.Label.ForegroundArgb == 0)
        {
            // 外観が色を指定しない場合はテーマの前色を継承する
            _label.ClearValue(TextBlock.ForegroundProperty);
        }
        else
        {
            _label.Foreground = new SolidColorBrush(Color.FromUInt32(plan.Label.ForegroundArgb));
        }
    }

    /// <summary>子要素を計測する（寸法は描画計画が決めるため、自分の矩形を返すだけにする）。</summary>
    /// <param name="availableSize">利用可能なサイズ。</param>
    /// <returns>項目の外枠の寸法。</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_plan is not { } plan)
        {
            return new Size(0, 0);
        }

        _background.Measure(new Size(plan.Slot.Width, plan.Slot.Height));
        _shadowBands.Measure(new Size(plan.ShadowArea.Width, plan.ShadowArea.Height));
        _shadowFrame.Measure(new Size(plan.Image.Width, plan.Image.Height));
        _image.Measure(new Size(plan.Image.Width, plan.Image.Height));
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

        var origin = plan.Slot;

        _background.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

        var image = ToLocal(plan.Image, origin);
        _shadowBands.Arrange(ToLocal(plan.ShadowArea, origin));
        _shadowFrame.Arrange(image);
        _image.Arrange(new Rect(0, 0, image.Width, image.Height));

        _label.Arrange(ToLocal(plan.Label.Bounds, origin));
        return finalSize;
    }

    /// <summary>内容の座標を項目の座標へ変換する。</summary>
    /// <param name="rect">内容の座標の矩形。</param>
    /// <param name="origin">項目の外枠の矩形（内容の座標）。</param>
    /// <returns>項目の左上を原点とする矩形。</returns>
    private static Rect ToLocal(EsRect rect, EsRect origin)
        => new(rect.X - origin.X, rect.Y - origin.Y, rect.Width, rect.Height);
}
