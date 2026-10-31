using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Ui.MewUI.Thumbnail;

/// <summary>サムネイル 1 件分の要素（背景・影・画像・名前）。</summary>
/// <remarks>
/// <b>構成</b>: フレームワーク標準の描画（背景・影・画像）と名前の要素を重ねる。 矩形は描画計画<br/>
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

    /// <summary>ツールヒントの幅の上限（DIP）。 名前が長い場合はこの幅で折り返す。</summary>
    private const double ToolTipMaxWidth = 280;

    /// <summary>ツールヒントの内容を作る（長い名前は折り返す）。</summary>
    /// <param name="text">表示する文言。</param>
    /// <returns>ツールヒントの要素。</returns>
    private static TextBlock CreateToolTip(string text)
        => new() { Text = text, MaxWidth = ToolTipMaxWidth, TextWrapping = TextWrapping.Wrap };

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
        // ツールヒントには省略していない名前を出す（折り返して全体を読めるようにする）。
        // 子の要素がポインターを受け取るため、子にも同じ内容を設定する（親をたどって解決する実装に依存しない）
        var tipText = string.IsNullOrEmpty(toolTip) ? caption : $"{caption}{Environment.NewLine}{toolTip}";
        ToolTip = CreateToolTip(tipText);
        _viewport.ToolTip = CreateToolTip(tipText);
        _label.ToolTip = CreateToolTip(tipText);

        SetImage(image);
        SetCaption(caption);
    }

    /// <summary>現在表示中の画像かどうかを取得する。</summary>
    /// <remarks>一覧の <c>CurrentIndex</c> が変更したときに切り替える（作成後は一覧だけが変更できる）。</remarks>
    public bool IsCurrent { get; private set; }

    /// <summary>現在表示中の画像かどうかを設定する。</summary>
    /// <param name="value">現在表示中の画像の場合は true。</param>
    internal void SetIsCurrent(bool value) => IsCurrent = value;

    /// <summary>実際に表示している名前（描画計画の文言）を取得する。</summary>
    /// <value>2 行に収まらない名前は省略済みの文言。</value>
    public string LabelText => _label.Text;
    /// <summary>表示する名前を取得する。</summary>
    public string Caption { get; private set; }

    /// <summary>表示する画像を取得する。</summary>
    public EsReadOnlyBitmap? Source => _viewport.Source;

    /// <summary>表示する画像を設定する。</summary>
    /// <param name="image">画像。 null の場合は画像を描かない。</param>
    public void SetImage(EsReadOnlyBitmap? image)
    {
        _viewport.Source = image;

        // 描画面は寸法が変わらなければ再描画されないため、明示的に要求する（画像の差し替えを反映する）
        _viewport.InvalidateVisual();
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

        // 描画面は寸法が変わらなければ再描画されないため、明示的に要求する（背景・影の変化を反映する）
        _viewport.InvalidateVisual();

        _label.FontSize = plan.Label.FontSize;
        _label.TextAlignment = TextAlignment.Center;
        _label.VerticalTextAlignment = TextAlignment.Top;
        _label.TextWrapping = TextWrapping.Wrap;
        _label.Padding = new Thickness(
            options.LabelHorizontalPadding, plan.Label.VerticalPadding, options.LabelHorizontalPadding, plan.Label.VerticalPadding);
        // 文言は計画が決める（2 行に収まらない名前は拡張子を残して省略済み）
        _label.Text = plan.Label.Text;
        _label.FontWeight = plan.Label.IsBold ? FontWeight.Bold : FontWeight.Normal;

        if (plan.Label.ForegroundArgb != 0)
        {
            _label.Foreground = Color.FromArgb(plan.Label.ForegroundArgb);
        }
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
    /// <param name="bounds">本要素へ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// MewUI の <c>Arrange</c> はウィンドウ絶対座標を受け取るため、子の座標は本要素の絶対座標を<br/>
    /// 加算して渡す（相対座標を渡すと要素が画面の左上へ寄る）。
    /// </remarks>
    protected override void ArrangeContent(Rect bounds)
    {
        if (_plan is not { } plan)
        {
            return;
        }

        _viewport.Arrange(new Rect(bounds.X, bounds.Y, plan.Slot.Width, plan.Slot.Height));
        _label.Arrange(new Rect(
            bounds.X + plan.Label.Bounds.X - plan.Slot.X,
            bounds.Y + plan.Label.Bounds.Y - plan.Slot.Y,
            plan.Label.Bounds.Width,
            plan.Label.Bounds.Height));
    }

}
