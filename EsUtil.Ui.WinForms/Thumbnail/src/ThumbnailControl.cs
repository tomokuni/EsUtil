using System.ComponentModel;
using System.Drawing.Drawing2D;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.Ui.WinForms.Thumbnail;

/// <summary>サムネイル一覧の WinForms 版コントロール（折り返し・下端揃え・スクロール・ホバー・選択）。</summary>
/// <remarks>
/// <b>2 つのページで同じ見た目にする</b>: 枠・影・名前の行・ホバー・並べ方をここ 1 か所に置き、<br/>
/// ページは並べる画像と名前を渡すだけにする。<br/>
/// <br/>
/// <b>並べ方</b>: 列数・行の高さ・下端揃えはコア（<see cref="ThumbnailLayout"/>）が単一所有する。<br/>
/// <b>見た目</b>: 色・影・名前の体裁は提示層（<see cref="ThumbnailPresenter"/>）が単一所有する。<br/>
/// <b>本クラスが持つもの</b>: 描画 API の呼び出し（GDI+）・入力の受付・スクロールだけである。<br/>
/// <br/>
/// <b>画像の所有権</b>: <see cref="Add"/> と <see cref="SetImage"/> へ渡した画像は本コントロールが所有し、<br/>
/// 差し替え・<see cref="ClearItems"/>・破棄のときに解放する（呼び出し側で破棄しない）。
/// </remarks>
public sealed class ThumbnailControl : ScrollableControl
{
    /// <summary>並べる項目。</summary>
    private readonly List<Item> _items = [];

    /// <summary>選択（単一選択の規則はコアが単一所有する）。</summary>
    private readonly ThumbnailSelection _selection = new();

    /// <summary>ツールヒント。</summary>
    private readonly ToolTip _toolTip = new();

    /// <summary>計算に必要な設定。</summary>
    private ThumbnailOptions _options = ThumbnailOptions.Default;

    /// <summary>描画にだけ必要な外観。</summary>
    private ThumbnailAppearance _appearance = ThumbnailAppearance.Default;

    /// <summary>提示層（設定・外観・描画計画の解決）。</summary>
    private readonly ThumbnailPresenter _presenter = new();

    /// <summary>ホバー中の添字。</summary>
    private int _hoveredIndex = -1;

    /// <summary>最後に解決した配置（無効な場合は null）。</summary>
    private ThumbnailLayoutResult? _layout;

    /// <summary>ログの出力先（未設定は <see cref="NullLogger"/>）。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>サムネイル一覧のコントロールを生成する。</summary>
    public ThumbnailControl()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor,
            true);

        AutoScroll = true;
        BackColor = SystemColors.Window;
        _selection.Changed += (_, _) =>
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        };
    }

    /// <summary>選択中の添字が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>項目が 2 回押されたときに発生する。</summary>
    public event EventHandler<ThumbnailActivatedEventArgs>? ItemActivated;

    /// <summary>ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定すると <see cref="NullLogger"/> になる。</value>
    /// <remarks>
    /// 設定・外観の変更は提示層が記録し、選択の変更はコア（<see cref="ThumbnailSelection"/>）が記録する。<br/>
    /// 本コントロールは画像と項目数の変更を記録するため、同じロガーを両者へ渡す。
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ILogger Log
    {
        get => _log;
        set
        {
            _log = value ?? NullLogger.Instance;
            _presenter.Log = _log;
            _selection.Log = _log;
        }
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Thumbnail]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>
    /// 本コントロールのログ定義（<see cref="ThumbnailLogMessage"/>）の接頭辞を設定し、<br/>
    /// 提示層・コアへ伝搬する（集約した接頭辞で全層のログを揃える）。
    /// </remarks>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            ThumbnailLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? ThumbnailLogMessage.DefaultPrefix : value;
            _presenter.LogPrefix = value;
            _selection.LogPrefix = value;
        }
    }

    /// <summary>計算に必要な設定を取得または設定する。</summary>
    /// <value>設定。 既定は <see cref="ThumbnailOptions.Default"/>。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ThumbnailOptions Options
    {
        get => _options;
        set
        {
            _options = value ?? ThumbnailOptions.Default;
            InvalidateLayout();
        }
    }

    /// <summary>外観を取得または設定する。</summary>
    /// <value>外観。 既定は <see cref="ThumbnailAppearance.Default"/>。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ThumbnailAppearance Appearance
    {
        get => _appearance;
        set
        {
            _appearance = value ?? ThumbnailAppearance.Default;
            _presenter.Appearance = _appearance;
            InvalidateLayout();
        }
    }

    /// <summary>暗色テーマかどうかを取得または設定する。</summary>
    /// <value>暗色テーマの場合は true。 既定は false。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsDarkTheme { get; set; }

    /// <summary>画像の幅（DIP）を取得または設定する。</summary>
    /// <value>表示モードの一辺。 0 以下の指定は既定の下限へ引き上げる。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ImageWidth { get; set; } = 96;

    /// <summary>項目数を取得する。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ItemCount => _items.Count;

    /// <summary>選択中の添字を取得または設定する。</summary>
    /// <value>項目の添字。 選択が無い場合は -1。</value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selection.Index;
        set
        {
            _selection.Select(value, _items.Count);
            Invalidate();
        }
    }

    /// <summary>見えている項目の範囲を取得する。</summary>
    /// <value>ビューポートと交差する項目の範囲。 項目が無い場合は空。</value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ThumbnailRange VisibleRange
        => EnsureLayout().VisibleRange(-AutoScrollPosition.Y, ClientSize.Height);

    /// <summary>項目の外枠の矩形（内容の座標）を取得する。</summary>
    /// <param name="index">項目の添字。</param>
    /// <returns>外枠の矩形。 範囲外の場合は空の矩形。</returns>
    public Rectangle ItemBounds(int index)
        => index >= 0 && index < _items.Count ? ToRect(EnsureLayout().Slot(index)) : Rectangle.Empty;

    /// <summary>一覧を空にする。</summary>
    public void ClearItems()
    {
        var previous = _items.Count;
        foreach (var item in _items)
        {
            item.Image?.Dispose();
        }

        _items.Clear();
        _hoveredIndex = -1;
        _selection.Clear();
        InvalidateLayout();
        _log.LogItemCountChanged(previous, 0);
    }

    /// <summary>サムネイルを 1 件追加する。</summary>
    /// <param name="image">表示する画像。 所有権は本コントロールへ移る。</param>
    /// <param name="caption">サムネイルの下へ表示する名前。</param>
    /// <param name="toolTip">ツールヒント。 null または空の場合は表示しない。</param>
    /// <param name="isCurrent">現在表示中の画像かどうか（true のとき名前を太字にする）。</param>
    /// <param name="tag">2 回押しの通知へ渡す任意の値。</param>
    /// <returns>追加した項目の添字。</returns>
    public int Add(Bitmap? image, string caption, string? toolTip = null, bool isCurrent = false, object? tag = null)
    {
        var item = new Item(image, caption, toolTip, isCurrent, tag);
        item.UpdateHeight(ImageWidth);
        var previous = _items.Count;
        _items.Add(item);
        InvalidateLayout();
        _log.LogItemCountChanged(previous, _items.Count);
        return _items.Count - 1;
    }

    /// <summary>項目の画像を差し替える。</summary>
    /// <param name="index">項目の添字。</param>
    /// <param name="image">新しい画像。 所有権は本コントロールへ移る。 直前の画像は解放する。</param>
    public void SetImage(int index, Bitmap? image)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        var item = _items[index];
        var previous = item.Image;
        var previousDescription = ThumbnailLogMessage.Describe(previous);
        item.Image = image;
        previous?.Dispose();
        item.UpdateHeight(ImageWidth);
        InvalidateLayout();
        _log.LogImageChanged($"Image[{index}]", previousDescription, ThumbnailLogMessage.Describe(image));
    }

    /// <summary>項目の名前を差し替える。</summary>
    /// <param name="index">項目の添字。</param>
    /// <param name="caption">新しい名前。</param>
    public void SetCaption(int index, string caption)
    {
        if (index >= 0 && index < _items.Count)
        {
            _items[index].Caption = caption;
            Invalidate();
        }
    }

    /// <summary>項目を表示領域へスクロールする。</summary>
    /// <param name="index">項目の添字。</param>
    public void ScrollToIndex(int index)
    {
        var layout = EnsureLayout();
        if (index < 0 || index >= layout.Count)
        {
            return;
        }

        var slot = layout.Slot(index);
        var top = -AutoScrollPosition.Y;
        if (slot.Y < top)
        {
            AutoScrollPosition = new Point(0, (int)slot.Y);
        }
        else if (slot.Bottom > top + ClientSize.Height)
        {
            AutoScrollPosition = new Point(0, (int)Math.Max(0, slot.Bottom - ClientSize.Height));
        }
    }

    /// <summary>配置を解決し直す。</summary>
    public void RefreshLayout() => InvalidateLayout();

    /// <summary>項目を描画する。</summary>
    /// <param name="e">描画のイベント引数。</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPaint(e);

        var layout = EnsureLayout();
        if (layout.Count == 0)
        {
            return;
        }

        var state = e.Graphics.Save();
        try
        {
            e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            for (var i = 0; i < _items.Count; i++)
            {
                DrawTile(e.Graphics, layout, i);
            }
        }
        finally
        {
            e.Graphics.Restore(state);
        }
    }

    /// <summary>1 件分の項目を描画する。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="layout">配置。</param>
    /// <param name="index">項目の添字。</param>
    private void DrawTile(Graphics graphics, ThumbnailLayoutResult layout, int index)
    {
        var item = _items[index];
        var plan = _presenter.ResolveDrawPlan(
            layout, index, item.Caption, item.IsCurrent, _selection.Index, _hoveredIndex, IsDarkTheme);

        foreach (var layer in plan.Layers)
        {
            switch (layer)
            {
                case ThumbnailDrawLayer.Background:
                    using (var brush = new SolidBrush(ToColor(plan.BackgroundArgb)))
                    {
                        graphics.FillRectangle(brush, ToRect(plan.Slot));
                    }

                    break;

                case ThumbnailDrawLayer.Shadow:
                    DrawShadow(graphics, plan);
                    break;

                case ThumbnailDrawLayer.Image:
                    if (item.Image is { } image)
                    {
                        var previous = graphics.InterpolationMode;
                        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                        graphics.PixelOffsetMode = PixelOffsetMode.Half;
                        graphics.SmoothingMode = SmoothingMode.None;
                        try
                        {
                            graphics.DrawImage(image, ToRect(plan.Image));
                        }
                        finally
                        {
                            graphics.InterpolationMode = previous;
                        }
                    }

                    break;

                case ThumbnailDrawLayer.Label:
                    DrawLabel(graphics, plan, item.Caption);
                    break;
            }
        }
    }

    /// <summary>影を描画する（1 画素の帯を単色で塗る）。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="plan">描画計画。</param>
    private static void DrawShadow(Graphics graphics, ThumbnailDrawPlan plan)
    {
        if (plan.Shadow is not { } shadow)
        {
            return;
        }

        // アイテムの描画は DIP と同じ単位（倍率 1）で行う
        foreach (var band in shadow.BuildDeviceBands(
            plan.Image.X, plan.Image.Y, plan.Image.Width, plan.Image.Height, scaling: 1))
        {
            using var brush = new SolidBrush(ToColor(band.Argb));
            graphics.FillRectangle(brush, band.X, band.Y, band.Width, band.Height);
        }
    }

    /// <summary>名前を描画する。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="plan">描画計画。</param>
    /// <param name="caption">表示する名前。</param>
    private void DrawLabel(Graphics graphics, ThumbnailDrawPlan plan, string caption)
    {
        using var font = new Font(
            DefaultFont.FontFamily,
            (float)plan.Label.FontSize,
            plan.Label.IsBold ? FontStyle.Bold : FontStyle.Regular);

        var color = plan.Label.ForegroundArgb == 0 ? ForeColor : ToColor(plan.Label.ForegroundArgb);
        TextRenderer.DrawText(
            graphics,
            caption,
            font,
            ToRect(plan.Label.Bounds),
            color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
    }

    /// <summary>ポインターの移動に合わせてホバーを切り替える。</summary>
    /// <param name="e">マウスのイベント引数。</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var index = EnsureLayout().IndexAt(new EsPoint(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y));
        if (index == _hoveredIndex)
        {
            return;
        }

        _hoveredIndex = index;
        _toolTip.SetToolTip(this, index >= 0 && index < _items.Count ? _items[index].ToolTip : null);
        Invalidate();
    }

    /// <summary>ポインターが外れたときにホバーを解除する。</summary>
    /// <param name="e">イベントの引数。</param>
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredIndex < 0)
        {
            return;
        }

        _hoveredIndex = -1;
        Invalidate();
    }

    /// <summary>項目が押されたときに選択を移す。</summary>
    /// <param name="e">マウスのイベント引数。</param>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _selection.Select(
            EnsureLayout().IndexAt(new EsPoint(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y)),
            _items.Count);
    }

    /// <summary>項目が 2 回押されたときに通知する。</summary>
    /// <param name="e">マウスのイベント引数。</param>
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var index = EnsureLayout().IndexAt(new EsPoint(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y));
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        _selection.Select(index, _items.Count);
        ItemActivated?.Invoke(this, new ThumbnailActivatedEventArgs(index, _items[index].Tag));
    }

    /// <summary>大きさが変わったときに配置を解決し直す。</summary>
    /// <param name="e">イベントの引数。</param>
    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        InvalidateLayout();
    }

    /// <summary>保持している資源を解放する。</summary>
    /// <param name="disposing">マネージドの資源も解放する場合は true。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var item in _items)
            {
                item.Image?.Dispose();
            }

            _items.Clear();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>配置を解決し直す。</summary>
    private void InvalidateLayout()
    {
        _layout = null;
        var layout = EnsureLayout();
        AutoScrollMinSize = new Size((int)Math.Ceiling(layout.ContentSize.Width), (int)Math.Ceiling(layout.ContentSize.Height));
        Invalidate();
    }

    /// <summary>配置を取得する（未解決の場合は解決する）。</summary>
    /// <returns>配置。</returns>
    private ThumbnailLayoutResult EnsureLayout()
    {
        if (_layout is { } cached)
        {
            return cached;
        }

        var heights = new double[_items.Count];
        for (var i = 0; i < _items.Count; i++)
        {
            heights[i] = _items[i].ImageHeight;
        }

        _presenter.Options = _options;
        _presenter.Appearance = _appearance;
        var layout = _presenter.ResolveLayout(Math.Max(0, ClientSize.Width), ImageWidth, heights);
        _layout = layout;
        return layout;
    }

    /// <summary>色（<c>0xAARRGGBB</c>）を GDI+ の色へ変換する。</summary>
    /// <param name="argb">色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>GDI+ の色。</returns>
    private static Color ToColor(uint argb)
        => Color.FromArgb((int)(argb >> 24), (int)((argb >> 16) & 0xFF), (int)((argb >> 8) & 0xFF), (int)(argb & 0xFF));

    /// <summary>矩形を GDI+ の矩形へ変換する。</summary>
    /// <param name="rect">矩形（DIP）。</param>
    /// <returns>GDI+ の矩形（端数は四捨五入する）。</returns>
    private static Rectangle ToRect(EsRect rect)
        => new(
            (int)Math.Round(rect.X),
            (int)Math.Round(rect.Y),
            Math.Max(0, (int)Math.Round(rect.Width)),
            Math.Max(0, (int)Math.Round(rect.Height)));

    /// <summary>一覧に並べる 1 件（画像・名前・状態）。</summary>
    private sealed class Item(Bitmap? image, string caption, string? toolTip, bool isCurrent, object? tag)
    {
        /// <summary>表示する画像（本項目が所有する）。</summary>
        public Bitmap? Image { get; set; } = image;

        /// <summary>表示する名前。</summary>
        public string Caption { get; set; } = caption;

        /// <summary>ツールヒント。</summary>
        public string? ToolTip { get; } = toolTip;

        /// <summary>現在表示中の画像かどうか。</summary>
        public bool IsCurrent { get; } = isCurrent;

        /// <summary>2 回押しの通知へ渡す任意の値。</summary>
        public object? Tag { get; } = tag;

        /// <summary>画像の表示高さ（DIP）。</summary>
        public double ImageHeight { get; private set; }

        /// <summary>画像の縦横比から表示高さを求め直す。</summary>
        /// <param name="imageWidth">画像の幅（DIP）。</param>
        public void UpdateHeight(double imageWidth)
            => ImageHeight = Image is null
                ? imageWidth
                : ThumbnailPresenter.ImageHeight(imageWidth, Image.Width, Image.Height);
    }
}
