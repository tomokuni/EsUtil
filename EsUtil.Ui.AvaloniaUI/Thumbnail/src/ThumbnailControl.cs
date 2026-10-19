using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.Thumbnail;

/// <summary>サムネイル一覧の Avalonia UI 版コントロール（折り返し・下端揃え・スクロール・ホバー・選択）。</summary>
/// <remarks>
/// <b>2 つのページで同じ見た目にする</b>: 枠・影・名前の行・ホバー・並べ方をここ 1 か所に置き、<br/>
/// ページは並べる画像と名前を渡すだけにする。<br/>
/// <br/>
/// <b>並べ方</b>: 列数・行の高さ・下端揃えはコア（<see cref="ThumbnailLayout"/>）が単一所有する。<br/>
/// <b>見た目</b>: 色・影・名前の体裁は提示層（<see cref="ThumbnailPresenter"/>）が単一所有する。<br/>
/// <b>本クラスが持つもの</b>: 入力の受付（ホバー・選択・2 回押し）・スクロール・要素の配置だけである。<br/>
/// <br/>
/// <b>位置の判定</b>: ポインターの位置から項目を決める（子要素のヒット テストに依存しない）。<br/>
/// ホバー帯は項目の外枠いっぱいであり、隣り合う項目の間に隙間は生じない。
/// </remarks>
public sealed class ThumbnailControl : ScrollViewer
{
    /// <summary>項目を並べるパネル。</summary>
    private readonly ThumbnailPanel _panel;

    /// <summary>ホバー中の項目の添字。</summary>
    private int _hoveredIndex = -1;

    /// <summary>選択中の添字（コアの選択が単一所有する）。</summary>
    private readonly ThumbnailSelection _selection = new();

    /// <summary>サムネイル一覧のコントロールを生成する。</summary>
    public ThumbnailControl()
    {
        Presentation = new ThumbnailPresenter();
        _panel = new ThumbnailPanel(this)
        {
            // 入力は位置から判定するため、余白もヒット テストの対象にする
            Background = Brushes.Transparent,
        };

        Content = _panel;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        _panel.PointerMoved += OnPointerMoved;
        _panel.PointerExited += OnPointerExited;
        _panel.PointerPressed += OnPointerPressed;
        _panel.DoubleTapped += OnDoubleTapped;

        _selection.Changed += (_, _) =>
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            RefreshStates(-1, _selection.Index);
        };
    }

    /// <summary>選択中の添字が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>項目が 2 回押されたときに発生する。</summary>
    public event EventHandler<ThumbnailActivatedEventArgs>? ItemActivated;

    /// <summary>提示層（設定・外観・描画計画）を取得する。</summary>
    public ThumbnailPresenter Presentation { get; }

    /// <summary>計算に必要な設定を取得または設定する。</summary>
    /// <value>設定。 既定は <see cref="ThumbnailOptions.Default"/>。</value>
    /// <remarks>変更後は配置を解決し直す（<see cref="Refresh"/>）。</remarks>
    public ThumbnailOptions Options
    {
        get => Presentation.Options;
        set
        {
            Presentation.Options = value;
            Refresh();
        }
    }

    /// <summary>外観を取得または設定する。</summary>
    /// <value>外観。 既定は <see cref="ThumbnailAppearance.Default"/>。</value>
    public ThumbnailAppearance Appearance
    {
        get => Presentation.Appearance;
        set
        {
            Presentation.Appearance = value;
            Refresh();
        }
    }

    /// <summary>暗色テーマかどうかを取得または設定する。</summary>
    /// <value>暗色テーマの場合は true。 既定は false。</value>
    public bool IsDarkTheme { get; set; }

    /// <summary>画像の幅（DIP）を取得または設定する。</summary>
    /// <value>表示モードの一辺。 0 以下の指定は既定の下限へ引き上げる。</value>
    /// <remarks>この値は全項目に共通である（表示モードで切り替える）。 変更後は <see cref="Refresh"/> を呼ぶ。</remarks>
    public double ImageWidth
    {
        get => _panel.ImageWidth;
        set
        {
            _panel.ImageWidth = value;
            _panel.InvalidateMeasure();
        }
    }

    /// <summary>項目数を取得する。</summary>
    public int ItemCount => _panel.Items.Count;

    /// <summary>選択中の添字を取得または設定する。</summary>
    /// <value>項目の添字。 選択が無い場合は -1。</value>
    public int SelectedIndex
    {
        get => _selection.Index;
        set => _selection.Select(value, _panel.Items.Count);
    }

    /// <summary>見えている項目の範囲を取得する。</summary>
    /// <value>ビューポートと交差する項目の範囲。 項目が無い場合は空。</value>
    public ThumbnailRange VisibleRange
        => _panel.Layout?.VisibleRange(Offset.Y, Viewport.Height) ?? ThumbnailRange.Empty;

    /// <summary>項目の外枠の矩形（内容の座標）を取得する。</summary>
    /// <param name="index">項目の添字。</param>
    /// <returns>外枠の矩形。 未レイアウト・範囲外の場合は空の矩形。</returns>
    /// <remarks>表示の優先度（見えている項目を先に生成する）の計算に使う。</remarks>
    public EsRect SlotBounds(int index) => _panel.Layout?.Slot(index) ?? EsRect.Empty;

    /// <summary>一覧を空にする。</summary>
    public void ClearItems()
    {
        _hoveredIndex = -1;
        _selection.Clear();
        _panel.Clear();
    }

    /// <summary>サムネイルを 1 件追加する。</summary>
    /// <param name="image">表示する画像。 null の場合は画像を描かない（代替画像を渡す）。</param>
    /// <param name="caption">サムネイルの下へ表示する名前。</param>
    /// <param name="toolTip">ツールヒント。 null または空の場合は表示しない。</param>
    /// <param name="isCurrent">現在表示中の画像かどうか（true のとき名前を太字にする）。</param>
    /// <param name="tag">2 回押しの通知へ渡す任意の値。</param>
    /// <returns>追加した項目の添字。</returns>
    public int Add(IImage? image, string caption, string? toolTip = null, bool isCurrent = false, object? tag = null)
    {
        var tile = new ThumbnailTile(image, caption, toolTip, tag, isCurrent)
        {
            ImageWidth = ImageWidth,
        };
        _panel.Add(tile);
        ApplyState(_panel.Items.Count - 1);
        return _panel.Items.Count - 1;
    }

    /// <summary>項目の画像を差し替える。</summary>
    /// <param name="index">項目の添字。</param>
    /// <param name="image">新しい画像。 null の場合は画像を消す。</param>
    public void SetImage(int index, IImage? image)
    {
        if (index < 0 || index >= _panel.Items.Count)
        {
            return;
        }

        _panel.Items[index].SetImage(image);
        _panel.InvalidateMeasure();
    }

    /// <summary>項目の名前を差し替える。</summary>
    /// <param name="index">項目の添字。</param>
    /// <param name="caption">新しい名前。</param>
    public void SetCaption(int index, string caption)
    {
        if (index < 0 || index >= _panel.Items.Count)
        {
            return;
        }

        _panel.Items[index].SetCaption(caption);
        ApplyState(index);
    }

    /// <summary>項目を表示領域へスクロールする。</summary>
    /// <param name="index">項目の添字。</param>
    public void ScrollToIndex(int index)
    {
        if (_panel.Layout is not { } layout || index < 0 || index >= layout.Count)
        {
            return;
        }

        var slot = layout.Slot(index);
        var top = Offset.Y;
        var bottom = top + Viewport.Height;
        if (slot.Y < top)
        {
            Offset = new Vector(Offset.X, Math.Max(0, slot.Y));
        }
        else if (slot.Bottom > bottom)
        {
            Offset = new Vector(Offset.X, Math.Max(0, slot.Bottom - Viewport.Height));
        }
    }

    /// <summary>配置と見た目を解決し直す。</summary>
    /// <remarks>設定・外観・表示モードを変えた後に呼ぶ。</remarks>
    public void Refresh()
    {
        _panel.InvalidateMeasure();
        foreach (var tile in _panel.Items)
        {
            tile.InvalidateMeasure();
        }
    }

    /// <summary>ポインターが項目の上を移動したときにホバーを切り替える。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">ポインターのイベント引数。</param>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(_panel);
        var index = _panel.IndexAt(position);
        if (index == _hoveredIndex)
        {
            return;
        }

        var previous = _hoveredIndex;
        _hoveredIndex = index;
        RefreshStates(previous, index);
    }

    /// <summary>ポインターが一覧の外へ出たときにホバーを解除する。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">ポインターのイベント引数。</param>
    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        var previous = _hoveredIndex;
        _hoveredIndex = -1;
        RefreshStates(previous, -1);
    }

    /// <summary>項目が押されたときに選択を移す。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">ポインターのイベント引数。</param>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_panel).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _selection.Select(_panel.IndexAt(e.GetPosition(_panel)), _panel.Items.Count);
    }

    /// <summary>項目が 2 回押されたときに通知する。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">イベントの引数（2 回押し）。</param>
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        var index = _panel.IndexAt(e.GetPosition(_panel));
        if (index < 0 || index >= _panel.Items.Count)
        {
            return;
        }

        _selection.Select(index, _panel.Items.Count);
        ItemActivated?.Invoke(this, new ThumbnailActivatedEventArgs(index, _panel.Items[index].Tag));
    }

    /// <summary>指定した項目の見た目を解決し直す。</summary>
    /// <param name="index">項目の添字。 範囲外は無視する。</param>
    private void ApplyState(int index)
    {
        if (_panel.Layout is not { } layout || index < 0 || index >= _panel.Items.Count)
        {
            return;
        }

        var tile = _panel.Items[index];
        var plan = Presentation.ResolveDrawPlan(
            layout, index, tile.Caption, tile.IsCurrent, _selection.Index, _hoveredIndex, IsDarkTheme);
        tile.Apply(plan, Presentation.Options);
    }

    /// <summary>指定した 2 件の見た目を解決し直す（ホバー・選択の切り替え）。</summary>
    /// <param name="first">1 件目の添字。 範囲外は無視する。</param>
    /// <param name="second">2 件目の添字。 範囲外は無視する。</param>
    private void RefreshStates(int first, int second)
    {
        ApplyState(first);
        if (second != first)
        {
            ApplyState(second);
        }
    }

    /// <summary>項目を並べるパネル（配置はコアの計算に従う）。</summary>
    private sealed class ThumbnailPanel(ThumbnailControl owner) : Panel
    {
        /// <summary>並べた項目（並び順）。</summary>
        private readonly List<ThumbnailTile> _items = [];

        /// <summary>並べた項目（並び順）を取得する。</summary>
        public List<ThumbnailTile> Items => _items;

        /// <summary>画像の幅（DIP）を取得または設定する。</summary>
        public double ImageWidth { get; set; }

        /// <summary>最後に解決した配置。</summary>
        public ThumbnailLayoutResult? Layout { get; private set; }

        /// <summary>一覧を空にする。</summary>
        public void Clear()
        {
            _items.Clear();
            Children.Clear();
            Layout = null;
            InvalidateMeasure();
        }

        /// <summary>項目を追加する。</summary>
        /// <param name="tile">項目の要素。</param>
        public void Add(ThumbnailTile tile)
        {
            _items.Add(tile);
            Children.Add(tile);
            InvalidateMeasure();
        }

        /// <summary>位置にある項目の添字を求める。</summary>
        /// <param name="position">パネルの座標。</param>
        /// <returns>項目の添字。 該当が無い場合は -1。</returns>
        public int IndexAt(Point position)
            => Layout?.IndexAt(new EsUtil.Platform.Core.Geometry.EsPoint(position.X, position.Y)) ?? -1;

        /// <summary>配置を解決して子要素を計測する。</summary>
        /// <param name="availableSize">利用可能なサイズ。</param>
        /// <returns>内容の寸法。</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? Bounds.Width : availableSize.Width;
            var heights = new double[_items.Count];
            for (var i = 0; i < _items.Count; i++)
            {
                _items[i].ImageWidth = ImageWidth;
                heights[i] = _items[i].ImageHeight;
            }

            var layout = owner.Presentation.ResolveLayout(width, ImageWidth, heights);
            Layout = layout;

            for (var i = 0; i < _items.Count; i++)
            {
                var slot = layout.Slot(i);
                _items[i].Measure(new Size(slot.Width, slot.Height));
            }

            return new Size(layout.ContentSize.Width, layout.ContentSize.Height);
        }

        /// <summary>子要素を配置する。</summary>
        /// <param name="finalSize">最終的なサイズ。</param>
        /// <returns>配置したサイズ。</returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Layout is not { } layout)
            {
                return finalSize;
            }

            for (var i = 0; i < _items.Count; i++)
            {
                var slot = layout.Slot(i);
                owner.ApplyState(i);
                _items[i].Arrange(new Rect(slot.X, slot.Y, slot.Width, slot.Height));
            }

            return finalSize;
        }
    }
}
