using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.MewUI.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置の規則は提示層が単一所有する</b>: 選択の上・下の判断と置ける領域への収めは
/// <see cref="FloatingPanelPlacement"/> が決める（4 つの UI 版で同じ結果になる）。<br/>
/// <b>出す条件は本文コントロールが単一所有する</b>: ホストがバーを置く場合と同じ判断
/// （<see cref="StyledTextControl.ShouldShowFormatBar"/>）を使う。<br/>
/// <b>ウィンドウの中身は 1 度だけ決める</b>: バーは項目の変更で作り直されるため、そのたびに
/// ウィンドウの中身を差し替えると要素の付け替えが重なって「既に親がある」で落ちる。 中身は変わらない根
/// （<see cref="Border"/>）を 1 度だけ載せ、根の子だけを差し替える。<br/>
/// <b>枠なしのウィンドウへ載せる</b>: ポップアップへ載せると、その中から開く色の選択が使えない。
/// 枠なしのウィンドウなら、中から開く一覧は普通のウィンドウの子になり、そのまま使える。
/// </remarks>
/// <param name="owner">書式バーを所有する本文コントロール。</param>
internal sealed class StyledTextFormatBarPopup(StyledTextControl owner)
{
    /// <summary>パネルの外観（枠・余白・高さ）。</summary>
    private static readonly FloatingPanelAppearance s_appearance = FloatingPanelAppearance.FormatBar;

    /// <summary>バーの入れ物（ウィンドウの中身。 1 度だけ載せる）。 枠と余白は外観が決める。</summary>
    private readonly Border _root = new()
    {
        Background = RichTextPainter.ToMewColor(s_appearance.Background),
        BorderBrush = RichTextPainter.ToMewColor(s_appearance.Border),
        BorderThickness = s_appearance.BorderThickness,
        CornerRadius = s_appearance.CornerRadius,
        Padding = new Thickness(
            s_appearance.Padding, s_appearance.VerticalPadding,
            s_appearance.Padding, s_appearance.VerticalPadding),
    };

    /// <summary>バーを載せる枠なしのウィンドウ（初回の表示で作る）。</summary>
    private Window? _window;

    /// <summary>ウィンドウを表示したかどうか（<c>Show</c> は 1 度だけ呼ぶ）。</summary>
    private bool _shown;

    /// <summary>利用者が動かした後の左上（DIP。 まだ動かしていない場合は <see langword="null"/>）。</summary>
    private EsPoint? _manualPosition;

    /// <summary>直前に見たポインターの位置（ウィンドウの内側の DIP）。</summary>
    /// <remarks>
    /// <b>前回との差だけを使う</b>: ウィンドウを動かすと、動いた先で同じカーソル位置の通知がまた届く。
    /// 開始位置からの差で動かすと、その通知のたびに余計に動いて移動量が増幅される。
    /// </remarks>
    private EsPoint _lastCursor;

    /// <summary>バーをポップアップで表示中かどうかを取得する。</summary>
    public bool IsOpen => _shown && _window is not null;

    /// <summary>いまの状態に合わせて、バーを出し入れし、位置を合わせる。</summary>
    public void Update()
    {
        if (!owner.ShouldShowFormatBar || owner.HostWindow is not { } main)
        {
            Close();
            return;
        }

        var bar = owner.FormatBar;
        bar.Refresh();

        var window = EnsureWindow();
        SetBar(bar);

        var panelSize = s_appearance.ResolvePanelSize(MeasureBar(bar));
        window.WindowSize = WindowSize.Fixed(panelSize.Width, panelSize.Height);

        // **位置はウィンドウ座標**: コントロールの Bounds はウィンドウ基準であるため、そのまま足せる
        var anchor = ToEsRect(owner.SelectionBounds);
        var available = new EsRect(0, 0, main.ClientSize.Width, main.ClientSize.Height);

        // **動かした位置を選択より優先する**: 選択が変わるたびに元の位置へ戻すと、動かした意味が無くなる
        var position = _manualPosition is { } manual
            ? FloatingPanelPlacement.Clamp(manual, panelSize.Width, panelSize.Height, available)
            : FloatingPanelPlacement.ResolveOutside(
                new EsRect(anchor.X + owner.Bounds.X, anchor.Y + owner.Bounds.Y, anchor.Width, anchor.Height),
                panelSize,
                available,
                FloatingPanelPlacement.FormatBarGap);

        if (!_shown)
        {
            window.Show(main);
            _shown = true;
        }

        // **位置は画面の座標**: 求めた位置はウィンドウの内側の DIP であるため、ウィンドウの位置を足す
        window.MoveTo(
            (int)Math.Round(main.Position.X + position.X),
            (int)Math.Round(main.Position.Y + position.Y));
    }

    /// <summary>バーを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        // **中身を先に外す**: 隠したウィンドウに要素を残すと、次に載せるときに「既に親がある」で落ちる
        _root.Child = null;

        // **動かした位置は捨てる**: 次に出したときは選択の位置から出す
        _manualPosition = null;

        if (_shown)
        {
            _window?.Hide();
            _shown = false;
        }
    }

    /// <summary>根へバーを載せる（既に載っていれば何もしない）。</summary>
    /// <param name="bar">書式バー。</param>
    private void SetBar(IStyledTextFormatBar bar)
    {
        if (ReferenceEquals(_root.Child, bar.View))
        {
            return;
        }

        // **1 つの要素は 1 つの親しか持てない**: ホストのレイアウトに残っていれば外してから載せる
        DetachFromParent(bar.View);
        _root.Child = bar.View;
    }

    /// <summary>バーを以前の親から外す（1 つの要素は 1 つの親しか持てない）。</summary>
    /// <param name="view">バーの画面要素。</param>
    private static void DetachFromParent(FrameworkElement view)
    {
        switch (view.Parent)
        {
            case Border border when ReferenceEquals(border.Child, view):
                border.Child = null;
                break;
            case Panel panel:
                panel.Remove(view);
                break;
            default:
                break;
        }
    }

    /// <summary>バーを載せるウィンドウを用意する。</summary>
    /// <returns>枠なしのウィンドウ。</returns>
    private Window EnsureWindow()
    {
        if (_window is { } existing)
        {
            return existing;
        }

        _window = new Window
        {
            Borderless = true,
            BorderThickness = 0,
            Title = "書式バー",
            Content = _root,
        };

        // **余白を掴んで動かせるようにする**: 中身（ボタン・入力欄）は押下を処理済みにするため、
        // 本ハンドラーは余白を掴んだときだけ呼ばれる
        _root.OnMouseDown(BeginDrag);
        _root.OnMouseMove(ContinueDrag);
        _root.OnMouseUp(_ => EndDrag());
        return _window;
    }

    /// <summary>バーの実寸（DIP）を測る。</summary>
    /// <param name="bar">書式バー。</param>
    /// <returns>実寸。 測れない場合は希望寸法。</returns>
    private static EsSize MeasureBar(IStyledTextFormatBar bar)
    {
        var desired = bar.View.DesiredSize;
        var width = desired.Width > 0 ? desired.Width : bar.PreferredWidth;
        var height = desired.Height > 0 ? desired.Height : TextFormatBarLayout.ItemHeight;
        return new EsSize(Math.Max(1, width), Math.Max(1, height));
    }

    /// <summary>バーの余白を掴んで動かし始める。</summary>
    /// <param name="e">マウスの引数。</param>
    private void BeginDrag(MouseEventArgs e)
    {
        if (!e.LeftButton || owner.HostWindow is not { } main)
        {
            return;
        }

        // **ウィンドウの内側の座標で測る**: 動かすのはバーのウィンドウであり、本文のウィンドウは動かない
        var cursor = e.GetPosition(main);
        _manualPosition ??= new EsPoint(
            _window is { } window ? window.Position.X - main.Position.X : 0,
            _window is { } w2 ? w2.Position.Y - main.Position.Y : 0);
        _lastCursor = new EsPoint(cursor.X, cursor.Y);
    }

    /// <summary>バーをドラッグして動かす。</summary>
    /// <param name="e">マウスの引数。</param>
    private void ContinueDrag(MouseEventArgs e)
    {
        if (!e.LeftButton || _window is not { } window || owner.HostWindow is not { } main)
        {
            return;
        }

        var cursor = e.GetPosition(main);
        var current = new EsPoint(cursor.X, cursor.Y);
        var delta = new EsPoint(current.X - _lastCursor.X, current.Y - _lastCursor.Y);
        _lastCursor = current;

        if (Math.Abs(delta.X) < 0.01 && Math.Abs(delta.Y) < 0.01)
        {
            return;
        }

        var panelSize = s_appearance.ResolvePanelSize(MeasureBar(owner.FormatBar));
        var available = new EsRect(0, 0, main.ClientSize.Width, main.ClientSize.Height);
        var moved = new EsPoint(_manualPosition!.Value.X + delta.X, _manualPosition.Value.Y + delta.Y);
        _manualPosition = FloatingPanelPlacement.Clamp(moved, panelSize.Width, panelSize.Height, available);
        window.MoveTo(
            (int)Math.Round(main.Position.X + _manualPosition.Value.X),
            (int)Math.Round(main.Position.Y + _manualPosition.Value.Y));
    }

    /// <summary>バーのドラッグを終える。</summary>
    private void EndDrag() => _lastCursor = default;

    /// <summary>MewUI の矩形を本層の矩形へ写す。</summary>
    /// <param name="rect">MewUI の矩形。</param>
    /// <returns>本層の矩形。</returns>
    private static EsRect ToEsRect(Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
