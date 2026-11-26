using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置の規則は提示層が単一所有する</b>: 選択の上・下の判断と置ける領域への収めは
/// <see cref="FloatingPanelPlacement"/> が決める（4 つの UI 版で同じ結果になる）。<br/>
/// <b>出す条件は本文コントロールが単一所有する</b>: ホストがバーを置く場合と同じ判断
/// （<see cref="StyledTextControl.ShouldShowFormatBar"/>）を使う。<br/>
/// <b>バーは本文コントロールが所有する</b>: 本クラスは画面要素を置き、位置を合わせるだけである。<br/>
/// <b>ウィンドウの中身は 1 度だけ決める</b>: バーは項目の変更で作り直されるため、そのたびに
/// ウィンドウの中身を差し替えると要素の付け替えが重なって「既に親がある」で落ちる。 中身は変わらない根
/// （<see cref="Border"/>）を 1 度だけ載せ、根の子だけを差し替える。<br/>
/// <b>枠なしのウィンドウへ載せる</b>: ポップアップへ載せると、その中から開く色の一覧が潰れて使えない。
/// 枠なしのウィンドウなら、中から開く一覧は普通のウィンドウの子になり、そのまま使える。
/// </remarks>
/// <param name="owner">書式バーを所有する本文コントロール。</param>
internal sealed class StyledTextFormatBarPopup(StyledTextControl owner)
{
    /// <summary>バーの入れ物（ウィンドウの中身。 1 度だけ載せる）。</summary>
    /// <remarks>
    /// <b>下地を透明にする</b>: 下地の無い面は押下を受け取らないため、余白を掴んで動かせない。
    /// </remarks>
    private readonly Border _root = new() { Background = Brushes.Transparent };

    /// <summary>利用者が動かした後の左上（DIP。 まだ動かしていない場合は <see langword="null"/>）。</summary>
    /// <remarks>
    /// <b>動かした位置を選択より優先する</b>: 選択が変わるたびに元の位置へ戻すと、動かした意味が無くなる。
    /// 閉じたときに捨てる（次に出したときは選択の位置から出す）。
    /// </remarks>
    private EsPoint? _manualPosition;

    /// <summary>ドラッグ開始時のバーの左上（DIP）。</summary>
    private EsPoint _dragOrigin;

    /// <summary>ドラッグ開始時のポインターの位置（アプリのウィンドウの DIP）。</summary>
    private EsPoint _dragOriginCursor;

    /// <summary>バーを載せる枠なしのウィンドウ（初回の表示で作る）。</summary>
    private Window? _window;

    /// <summary>位置の追随を配線したアプリのウィンドウ（差し替え時に外す）。</summary>
    private Window? _hookedMain;

    /// <summary>バーをポップアップで表示中かどうかを取得する。</summary>
    public bool IsOpen => _window?.IsVisible ?? false;

    /// <summary>いまの状態に合わせて、バーを出し入れし、位置を合わせる。</summary>
    public void Update()
    {
        if (!owner.ShouldShowFormatBar || TopLevel.GetTopLevel(owner) is not Window main)
        {
            Close();
            return;
        }

        var bar = owner.FormatBar;
        bar.Refresh();

        var window = EnsureWindow(main);
        SetBar(bar);

        var barSize = MeasureBar(bar);
        window.Width = Math.Max(1, barSize.Width);
        window.Height = Math.Max(1, barSize.Height);

        var scale = main.RenderScaling > 0 ? main.RenderScaling : 1;
        var origin = owner.TranslatePoint(default, main) ?? default;
        var available = new EsRect(-origin.X, -origin.Y, main.ClientSize.Width, main.ClientSize.Height);

        // **動かした位置を選択より優先する**: 選択が変わるたびに元の位置へ戻すと、動かした意味が無くなる
        var position = _manualPosition is { } manual
            ? FloatingPanelPlacement.Clamp(manual, barSize.Width, barSize.Height, available)
            : FloatingPanelPlacement.ResolveOutside(
                ToEsRect(owner.SelectionBounds), barSize, available, FloatingPanelPlacement.FormatBarGap);

        // **ウィンドウの位置は画面の物理ピクセル**: コントロールの原点と求めた位置を写して足す
        var x = main.Position.X + (int)Math.Round((origin.X + position.X) * scale);
        var y = main.Position.Y + (int)Math.Round((origin.Y + position.Y) * scale);

        if (!window.IsVisible)
        {
            window.Show(main);
        }

        window.Position = new PixelPoint(x, y);
    }

    /// <summary>バーの余白を掴んで動かし始める。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    /// <remarks>
    /// <b>中身の操作を奪わない</b>: ボタンや入力欄は押下を処理済みにするため、本ハンドラーは呼ばれない
    /// （余白を掴んだときだけ動く）。
    /// </remarks>
    private void OnBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;
        if (_window is not { } window || !e.GetCurrentPoint(_root).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragOrigin = _manualPosition ?? ResolveCurrentPosition(window);
        _dragOriginCursor = ToMainDip(window, e);
        e.Pointer.Capture(_root);
        e.Handled = true;
    }

    /// <summary>バーをドラッグして動かす。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnBarPointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;
        if (_window is not { } window || !ReferenceEquals(e.Pointer.Captured, _root))
        {
            return;
        }

        var barSize = MeasureBar(owner.FormatBar);
        var moved = FloatingPanelPlacement.ResolveDrag(_dragOrigin, _dragOriginCursor, ToMainDip(window, e));
        _manualPosition = ClampToMain(moved, barSize);
        ApplyPosition(window, _manualPosition.Value);
        e.Handled = true;
    }

    /// <summary>バーのドラッグを終える。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ = sender;
        if (ReferenceEquals(e.Pointer.Captured, _root))
        {
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    /// <summary>バーの左上（アプリのウィンドウの DIP）を求める。</summary>
    /// <param name="window">バーのウィンドウ。</param>
    /// <returns>左上の位置（アプリのウィンドウの DIP）。</returns>
    private EsPoint ResolveCurrentPosition(Window window)
    {
        if (TopLevel.GetTopLevel(owner) is not Window main)
        {
            return _manualPosition ?? default;
        }

        var scale = main.RenderScaling > 0 ? main.RenderScaling : 1;
        return new EsPoint(
            (window.Position.X - main.Position.X) / scale,
            (window.Position.Y - main.Position.Y) / scale);
    }

    /// <summary>ポインターの位置をアプリのウィンドウの DIP へ写す。</summary>
    /// <param name="window">バーのウィンドウ。</param>
    /// <param name="e">ポインターの引数。</param>
    /// <returns>位置（アプリのウィンドウの DIP）。</returns>
    private EsPoint ToMainDip(Window window, PointerEventArgs e)
    {
        if (TopLevel.GetTopLevel(owner) is not Window main)
        {
            return default;
        }

        var scale = main.RenderScaling > 0 ? main.RenderScaling : 1;
        var screen = window.PointToScreen(e.GetPosition(window));
        return new EsPoint(
            (screen.X - main.Position.X) / scale,
            (screen.Y - main.Position.Y) / scale);
    }

    /// <summary>バーの位置を置ける領域の内側へ収める。</summary>
    /// <param name="position">収める前の位置（アプリのウィンドウの DIP）。</param>
    /// <param name="barSize">バーの大きさ（DIP）。</param>
    /// <returns>収めた後の位置（アプリのウィンドウの DIP）。</returns>
    private EsPoint ClampToMain(EsPoint position, EsSize barSize)
    {
        if (TopLevel.GetTopLevel(owner) is not Window main)
        {
            return position;
        }

        var origin = owner.TranslatePoint(default, main) ?? default;
        var available = new EsRect(-origin.X, -origin.Y, main.ClientSize.Width, main.ClientSize.Height);
        return FloatingPanelPlacement.Clamp(position, barSize.Width, barSize.Height, available);
    }

    /// <summary>バーの左上を画面の位置として適用する。</summary>
    /// <param name="window">バーのウィンドウ。</param>
    /// <param name="position">左上の位置（アプリのウィンドウの DIP）。</param>
    private void ApplyPosition(Window window, EsPoint position)
    {
        if (TopLevel.GetTopLevel(owner) is not Window main)
        {
            return;
        }

        var scale = main.RenderScaling > 0 ? main.RenderScaling : 1;
        var origin = owner.TranslatePoint(default, main) ?? default;
        window.Position = new PixelPoint(
            main.Position.X + (int)Math.Round((origin.X + position.X) * scale),
            main.Position.Y + (int)Math.Round((origin.Y + position.Y) * scale));
    }

    /// <summary>バーを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        UnhookMain();

        // **動かした位置は捨てる**: 次に出したときは選択の位置から出す
        _manualPosition = null;

        // **中身を外す**: ホストが埋め込みへ戻せるよう、バーを根から切り離す
        _root.Child = null;

        if (_window is { IsVisible: true } window)
        {
            window.Hide();
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
    private static void DetachFromParent(Control view)
    {
        switch (view.Parent)
        {
            case Panel panel:
                panel.Children.Remove(view);
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, view):
                decorator.Child = null;
                break;
            case ContentControl content when ReferenceEquals(content.Content, view):
                content.Content = null;
                break;
            default:
                break;
        }
    }

    /// <summary>バーを載せるウィンドウを用意する。</summary>
    /// <param name="main">アプリのウィンドウ（所有者）。</param>
    /// <returns>枠なしのウィンドウ。</returns>
    /// <remarks>
    /// <b>アプリのウィンドウが動いたら置き直す</b>: 位置は画面の絶対座標で決めるため、追随しないと
    /// 本文から離れた場所に取り残される。
    /// </remarks>
    private Window EnsureWindow(Window main)
    {
        if (!ReferenceEquals(_hookedMain, main))
        {
            UnhookMain();
            _hookedMain = main;
            main.PositionChanged += OnMainPositionChanged;
        }

        if (_window is { } existing)
        {
            return existing;
        }

        _window = new Window
        {
            WindowDecorations = WindowDecorations.None,
            ShowInTaskbar = false,
            CanResize = false,
            SizeToContent = SizeToContent.Manual,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Topmost = false,
            Content = _root,
        };

        // **余白を掴んで動かせるようにする**: 中身（ボタン・入力欄）は押下を処理済みにするため、
        // 本ハンドラーは余白を掴んだときだけ呼ばれる。
        _root.PointerPressed += OnBarPointerPressed;
        _root.PointerMoved += OnBarPointerMoved;
        _root.PointerReleased += OnBarPointerReleased;
        return _window;
    }

    /// <summary>アプリのウィンドウが動いたら、バーを置き直す。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">引数。</param>
    private void OnMainPositionChanged(object? sender, PixelPointEventArgs e)
    {
        _ = sender;
        _ = e;
        Update();
    }

    /// <summary>アプリのウィンドウの位置の追随を外す。</summary>
    private void UnhookMain()
    {
        if (_hookedMain is { } main)
        {
            main.PositionChanged -= OnMainPositionChanged;
            _hookedMain = null;
        }
    }

    /// <summary>バーの実寸（DIP）を測る。</summary>
    /// <param name="bar">書式バー。</param>
    /// <returns>実寸。 測れない場合は希望寸法。</returns>
    private static EsSize MeasureBar(IStyledTextFormatBar bar)
    {
        bar.View.Measure(Size.Infinity);
        var desired = bar.View.DesiredSize;
        return new EsSize(Math.Max(1, desired.Width), Math.Max(1, desired.Height));
    }

    /// <summary>Avalonia の矩形を本層の矩形へ写す。</summary>
    /// <param name="rect">Avalonia の矩形。</param>
    /// <returns>本層の矩形。</returns>
    private static EsRect ToEsRect(Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
