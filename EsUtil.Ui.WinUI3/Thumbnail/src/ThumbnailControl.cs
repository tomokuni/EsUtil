using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.Ui.WinUI3.Thumbnail;

/// <summary>サムネイル一覧の WinUI 3 版コントロール（折り返し・下端揃え・スクロール・ホバー・選択）。</summary>
/// <remarks>
/// <b>2 つのページで同じ見た目にする</b>: 枠・影・名前の行・ホバー・並べ方をここ 1 か所に置き、<br/>
/// ページは並べる画像と名前を渡すだけにする。<br/>
/// <br/>
/// <b>並べ方</b>: 列数・行の高さ・下端揃えはコア（<see cref="ThumbnailLayout"/>）が単一所有する。<br/>
/// <b>見た目</b>: 色・影・名前の体裁は提示層（<see cref="ThumbnailPresenter"/>）が単一所有する。<br/>
/// <b>本クラスが持つもの</b>: スクロール・入力の受付・要素の配置だけである。<br/>
/// <br/>
/// <b>WinUI 3 の標準パネルには任意の大きさの子を折り返すパネルが無い</b>ため、列数を計算して<br/>
/// <see cref="Canvas"/> へ絶対配置する（コアの計算が返す矩形をそのまま使う）。
/// </remarks>
public sealed class ThumbnailControl : Grid
{
    /// <summary>項目を置く面。</summary>
    private readonly Canvas _canvas = new();

    /// <summary>スクロール。</summary>
    private readonly ScrollViewer _scroll;

    /// <summary>並べる項目。</summary>
    private readonly List<ThumbnailTile> _items = [];

    /// <summary>選択（単一選択の規則はコアが単一所有する）。</summary>
    private readonly ThumbnailSelection _selection = new();

    /// <summary>ホバー中の添字。</summary>
    private int _hoveredIndex = -1;

    /// <summary>ログの出力先（未設定は <see cref="NullLogger"/>）。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>最後に解決した配置。</summary>
    private ThumbnailLayoutResult? _layout;

    /// <summary>サムネイル一覧のコントロールを生成する。</summary>
    public ThumbnailControl()
    {
        Presentation = new ThumbnailPresenter();

        // 背景が未設定の要素はヒット テストの対象にならず、ポインターの位置も取得できない
        _canvas.Background = new SolidColorBrush(Colors.Transparent);
        _scroll = new ScrollViewer
        {
            Padding = new Thickness(0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Colors.Transparent),
            Content = _canvas,
        };

        Children.Add(_scroll);

        // ホバーは一覧の領域で位置から判定する（子要素のヒット テストに依存しない）
        _scroll.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnPointerMoved), handledEventsToo: true);
        _scroll.PointerExited += (_, _) => SetHovered(-1);
        _scroll.PointerPressed += OnPointerPressed;
        _scroll.DoubleTapped += OnDoubleTapped;
        _scroll.ViewChanged += (_, _) => ScrollChanged?.Invoke(this, EventArgs.Empty);

        _selection.Changed += (_, _) =>
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            RefreshStates(-1, _selection.Index);
        };
    }

    /// <summary>一覧がスクロールされたときに発生する。</summary>
    public event EventHandler? ScrollChanged;

    /// <summary>選択中の添字が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>項目が 2 回押されたときに発生する。</summary>
    public event EventHandler<ThumbnailActivatedEventArgs>? ItemActivated;

    /// <summary>提示層（設定・外観・描画計画）を取得する。</summary>
    public ThumbnailPresenter Presentation { get; }

    /// <summary>ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定すると <see cref="NullLogger"/> になる。</value>
    /// <remarks>
    /// 設定・外観の変更は提示層が記録し、選択の変更はコア（<see cref="ThumbnailSelection"/>）が記録する。<br/>
    /// 本コントロールは画像と項目数の変更を記録するため、同じロガーを両者へ渡す。
    /// </remarks>
    public ILogger Log
    {
        get => _log;
        set
        {
            _log = value ?? NullLogger.Instance;
            Presentation.Log = _log;
            _selection.Log = _log;
        }
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Thumbnail]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>
    /// 本コントロールのログ定義（<see cref="ThumbnailLogMessage"/>）の接頭辞を設定し、<br/>
    /// 提示層・コアへ伝搬する（集約した接頭辞で全層のログを揃える）。
    /// </remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            ThumbnailLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? ThumbnailLogMessage.DefaultPrefix : value;
            Presentation.LogPrefix = value;
            _selection.LogPrefix = value;
        }
    }

    /// <summary>計算に必要な設定を取得または設定する。</summary>
    /// <value>設定。 既定は <see cref="ThumbnailOptions.Default"/>。</value>
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
    public double ImageWidth { get; set; } = 96;

    /// <summary>項目数を取得する。</summary>
    public int ItemCount => _items.Count;

    /// <summary>選択中の添字を取得または設定する。</summary>
    /// <value>項目の添字。 選択が無い場合は -1。</value>
    public int SelectedIndex
    {
        get => _selection.Index;
        set => _selection.Select(value, _items.Count);
    }

    /// <summary>見えている項目の範囲を取得する。</summary>
    /// <value>ビューポートと交差する項目の範囲。 項目が無い場合は空。</value>
    public ThumbnailRange VisibleRange
        => _layout?.VisibleRange(_scroll.VerticalOffset, _scroll.ViewportHeight) ?? ThumbnailRange.Empty;

    /// <summary>項目の外枠の矩形（内容の座標）を取得する。</summary>
    /// <param name="index">項目の添字。</param>
    /// <returns>外枠の矩形。 未レイアウト・範囲外の場合は空の矩形。</returns>
    public EsRect SlotBounds(int index) => _layout?.Slot(index) ?? EsRect.Empty;

    /// <summary>一覧を空にする。</summary>
    public void ClearItems()
    {
        var previous = _items.Count;
        _hoveredIndex = -1;
        _selection.Clear();
        _items.Clear();
        _canvas.Children.Clear();
        _layout = null;
        _log.LogItemCountChanged(previous, 0);
    }

    /// <summary>サムネイルを 1 件追加する。</summary>
    /// <param name="image">表示する画像。 null の場合は画像を描かない（代替画像を渡す）。</param>
    /// <param name="caption">サムネイルの下へ表示する名前。</param>
    /// <param name="toolTip">ツールヒント。 null または空の場合は表示しない。</param>
    /// <param name="isCurrent">現在表示中の画像かどうか（true のとき名前を太字にする）。</param>
    /// <param name="tag">2 回押しの通知へ渡す任意の値。</param>
    /// <returns>追加した項目の添字。</returns>
    public int Add(
        EsReadOnlyBitmap? image, string caption, string? toolTip = null, bool isCurrent = false, object? tag = null)
    {
        var tile = new ThumbnailTile(image, caption, toolTip, tag, isCurrent);
        var previous = _items.Count;
        _items.Add(tile);
        _canvas.Children.Add(tile);
        _layout = null;
        _log.LogItemCountChanged(previous, _items.Count);
        return _items.Count - 1;
    }

    /// <summary>項目の画像を差し替える。</summary>
    /// <param name="index">項目の添字。</param>
    /// <param name="image">新しい画像。 null の場合は画像を消す。</param>
    public void SetImage(int index, EsReadOnlyBitmap? image)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        var tile = _items[index];
        var previous = tile.Source;
        tile.SetImage(image);
        _layout = null;
        InvalidateMeasure();
        _log.LogImageChanged($"Image[{index}]", previous, image);
    }

    /// <summary>項目の名前を差し替える。</summary>
    /// <param name="index">項目の添字。</param>
    /// <param name="caption">新しい名前。</param>
    public void SetCaption(int index, string caption)
    {
        if (index >= 0 && index < _items.Count)
        {
            _items[index].SetCaption(caption);
            ApplyState(index);
        }
    }

    /// <summary>項目を表示領域へスクロールする。</summary>
    /// <param name="index">項目の添字。</param>
    public void ScrollToIndex(int index)
    {
        if (_layout is not { } layout || index < 0 || index >= layout.Count)
        {
            return;
        }

        var slot = layout.Slot(index);
        var top = _scroll.VerticalOffset;
        if (slot.Y < top)
        {
            _scroll.ChangeView(null, slot.Y, null, disableAnimation: true);
        }
        else if (slot.Bottom > top + _scroll.ViewportHeight)
        {
            _scroll.ChangeView(
                null, Math.Max(0, slot.Bottom - _scroll.ViewportHeight), null, disableAnimation: true);
        }
    }

    /// <summary>配置と見た目を解決し直す。</summary>
    public void Refresh()
    {
        _layout = null;
        InvalidateMeasure();
    }

    /// <summary>子要素を計測する（配置を解決し、子の大きさを決める）。</summary>
    /// <param name="availableSize">利用可能なサイズ。</param>
    /// <returns>内容の寸法。</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? ActualWidth : availableSize.Width;

        var heights = new double[_items.Count];
        for (var i = 0; i < _items.Count; i++)
        {
            var source = _items[i].Source;
            heights[i] = source is null || source.IsDisposed
                ? ImageWidth
                : ThumbnailLayout.ImageHeight(ImageWidth, source.Width, source.Height);
        }

        var layout = Presentation.ResolveLayout(width, ImageWidth, heights);
        _layout = layout;

        for (var i = 0; i < _items.Count; i++)
        {
            var slot = layout.Slot(i);
            _items[i].Measure(new Size(slot.Width, slot.Height));
        }

        _canvas.Width = layout.ContentSize.Width;
        _canvas.Height = layout.ContentSize.Height;
        _scroll.Measure(new Size(width, availableSize.Height));
        return new Size(width, double.IsInfinity(availableSize.Height) ? layout.ContentSize.Height : availableSize.Height);
    }

    /// <summary>子要素を配置する。</summary>
    /// <param name="finalSize">最終的なサイズ。</param>
    /// <returns>配置したサイズ。</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _scroll.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

        if (_layout is not { } layout)
        {
            return finalSize;
        }

        for (var i = 0; i < _items.Count; i++)
        {
            var slot = layout.Slot(i);
            ApplyState(i);
            Canvas.SetLeft(_items[i], slot.X);
            Canvas.SetTop(_items[i], slot.Y);
            _items[i].Arrange(new Rect(0, 0, slot.Width, slot.Height));
        }

        return finalSize;
    }

    /// <summary>ポインターが移動したときにホバーを切り替える。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">ポインターのイベント引数。</param>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        => SetHovered(IndexAt(e.GetCurrentPoint(_canvas).Position));

    /// <summary>項目が押されたときに選択を移す。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">ポインターのイベント引数。</param>
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(_canvas).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _selection.Select(IndexAt(e.GetCurrentPoint(_canvas).Position), _items.Count);
    }

    /// <summary>項目が 2 回押されたときに通知する。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">イベントの引数（2 回押し）。</param>
    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var index = IndexAt(e.GetPosition(_canvas));
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        _selection.Select(index, _items.Count);
        ItemActivated?.Invoke(this, new ThumbnailActivatedEventArgs(index, _items[index].Tag));
    }

    /// <summary>位置にある項目の添字を求める。</summary>
    /// <param name="position">項目の面の座標。</param>
    /// <returns>項目の添字。 該当が無い場合は -1。</returns>
    private int IndexAt(Point position)
        => _layout?.IndexAt(new EsPoint(position.X, position.Y)) ?? -1;

    /// <summary>ホバー中の項目を切り替える。</summary>
    /// <param name="index">新しい添字。 解除する場合は -1。</param>
    private void SetHovered(int index)
    {
        if (index == _hoveredIndex)
        {
            return;
        }

        var previous = _hoveredIndex;
        _hoveredIndex = index;
        RefreshStates(previous, index);
    }

    /// <summary>指定した項目の見た目を解決し直す。</summary>
    /// <param name="index">項目の添字。 範囲外は無視する。</param>
    private void ApplyState(int index)
    {
        if (_layout is not { } layout || index < 0 || index >= _items.Count)
        {
            return;
        }

        var tile = _items[index];
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
}
