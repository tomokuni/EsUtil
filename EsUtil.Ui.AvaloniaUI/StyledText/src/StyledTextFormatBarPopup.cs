using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置と枠は提示層が単一所有する</b>: 対象に被せない配置・置ける領域への収め・ドラッグの規則は
/// <see cref="FloatingPanelPlacement"/>、枠・余白・高さは <see cref="FloatingPanelAppearance.FormatBar"/> が
/// 決める（4 つの UI 版で同じ結果になる）。<br/>
/// <b>出す条件は本文コントロールが単一所有する</b>: ホストがバーを置く場合と同じ判断
/// （<see cref="StyledTextControl.ShouldShowFormatBar"/>）を使う。<br/>
/// <b>バーは本文コントロールが所有する</b>: 本クラスは枠を作り、バーを載せ、位置を合わせるだけである。<br/>
/// <b>パネルの余白を掴んで動かせる</b>: 中身（ボタン・入力欄）は押下を処理済みにするため、本クラスの
/// ドラッグは余白と項目の間でだけ始まる（中身の操作を奪わない）。<br/>
/// <b>ウィンドウの中身は 1 度だけ決める</b>: バーは項目の変更で作り直されるため、そのたびに
/// ウィンドウの中身を差し替えると要素の付け替えが重なって「既に親がある」で落ちる。 中身は変わらない根
/// （<see cref="Border"/>）を 1 度だけ載せ、根の子だけを差し替える。<br/>
/// <b>枠なしのウィンドウへ載せる</b>: ポップアップへ載せると、その中から開く色の一覧が潰れて使えない。
/// 枠なしのウィンドウなら、中から開く一覧は普通のウィンドウの子になり、そのまま使える。
/// </remarks>
/// <param name="owner">書式バーを所有する本文コントロール。</param>
internal sealed class StyledTextFormatBarPopup(StyledTextControl owner)
{
    /// <summary>パネルの外観（枠・余白・高さ）。</summary>
    private static readonly FloatingPanelAppearance s_appearance = FloatingPanelAppearance.FormatBar;

    /// <summary>バーの入れ物（ウィンドウの中身。 1 度だけ載せる）。</summary>
    private readonly Border _root = new();

    /// <summary>利用者が動かした後の左上（DIP。 まだ動かしていない場合は <see langword="null"/>）。</summary>
    /// <remarks>
    /// <b>動かした位置を選択より優先する</b>: 選択が変わるたびに元の位置へ戻すと、動かした意味が無くなる。
    /// 閉じたときに捨てる（次に出したときは選択の位置から出す）。
    /// </remarks>
    private EsPoint? _manualPosition;

    /// <summary>直前に見たポインターの位置（画面の物理ピクセル）。</summary>
    /// <remarks>
    /// <b>前回との差だけを使う</b>: ウィンドウを動かすと、動いた先で同じカーソル位置の通知がまた届く。
    /// 開始位置からの差で動かすと、その通知のたびに余計に動いて移動量が増幅される。
    /// 画面の位置が変わっていない通知は無視する。
    /// </remarks>
    private PixelPoint _lastCursorScreen;

    /// <summary>ドラッグ中かどうか。</summary>
    private bool _dragging;

    /// <summary>バーを載せる枠なしのウィンドウ（初回の表示で作る）。</summary>
    private Window? _window;

    /// <summary>位置の追随を配線したアプリのウィンドウ（差し替え時に外す）。</summary>
    private Window? _hookedMain;

    /// <summary>バーをポップアップで表示中かどうかを取得する。</summary>
    public bool IsOpen => _window?.IsVisible ?? false;

    /// <summary>バーを載せているウィンドウを取得する（テストが実入力の経路を確かめるために使う）。</summary>
    internal Window? Window => _window;

    /// <summary>利用者が動かした後の左上（DIP）を取得する（まだ動かしていない場合は <see langword="null"/>）。</summary>
    internal EsPoint? ManualPosition => _manualPosition;

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

        var panelSize = s_appearance.ResolvePanelSize(MeasureBar(bar));
        window.Width = Math.Max(1, panelSize.Width);
        window.Height = Math.Max(1, panelSize.Height);

        // **座標はアプリのウィンドウの左上を原点とする DIP**（ウィンドウの位置を足すだけで画面へ写せる）
        var scale = main.RenderScaling > 0 ? main.RenderScaling : 1;
        var origin = owner.TranslatePoint(default, main) ?? default;
        var available = new EsRect(0, 0, main.ClientSize.Width, main.ClientSize.Height);
        var selection = ToEsRect(owner.SelectionBounds);
        var anchor = new EsRect(
            selection.X + origin.X, selection.Y + origin.Y, selection.Width, selection.Height);

        // **動かした位置を選択より優先する**: 選択が変わるたびに元の位置へ戻すと、動かした意味が無くなる
        var position = _manualPosition is { } manual
            ? FloatingPanelPlacement.Clamp(manual, panelSize.Width, panelSize.Height, available)
            : FloatingPanelPlacement.ResolveOutside(
                anchor, panelSize, available, FloatingPanelPlacement.FormatBarGap);

        // **ウィンドウの位置は画面の物理ピクセル**: 求めた位置を写して足す
        var placed = new PixelPoint(
            main.Position.X + (int)Math.Round(position.X * scale),
            main.Position.Y + (int)Math.Round(position.Y * scale));

        if (!window.IsVisible)
        {
            window.Show(main);
        }

        window.Position = placed;
    }

    /// <summary>バーを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        UnhookMain();

        // **動かした位置は捨てる**: 次に出したときは選択の位置から出す
        _manualPosition = null;
        _dragging = false;

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

    /// <summary>パネルの余白を掴んで動かし始める。</summary>
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

        _dragging = true;
        _manualPosition ??= ResolveCurrentPosition(window);
        _lastCursorScreen = GetCursorScreen(window, e);
        e.Pointer.Capture(_root);
        e.Handled = true;
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnBarPointerMoved(object? sender, PointerEventArgs e)
    {
        _ = sender;
        if (!_dragging || _window is not { } window || TopLevel.GetTopLevel(owner) is not Window main)
        {
            return;
        }

        // **前回の位置との差だけ動かす**: ウィンドウが動くと、動いた先で同じカーソル位置の通知が
        // また届く。 開始位置からの差で動かすと、そのたびに余計に動いて増幅する。
        var cursor = GetCursorScreen(window, e);
        var scale = main.RenderScaling > 0 ? main.RenderScaling : 1;
        var delta = new EsPoint(
            (cursor.X - _lastCursorScreen.X) / scale,
            (cursor.Y - _lastCursorScreen.Y) / scale);
        _lastCursorScreen = cursor;

        if (Math.Abs(delta.X) < 0.01 && Math.Abs(delta.Y) < 0.01)
        {
            return;
        }

        var panelSize = s_appearance.ResolvePanelSize(MeasureBar(owner.FormatBar));
        var moved = new EsPoint(_manualPosition!.Value.X + delta.X, _manualPosition.Value.Y + delta.Y);
        _manualPosition = ClampToMain(moved, panelSize);
        ApplyPosition(window, _manualPosition.Value);
        e.Handled = true;
    }

    /// <summary>パネルのドラッグを終える。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ = sender;
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        if (ReferenceEquals(e.Pointer.Captured, _root))
        {
            e.Pointer.Capture(null);
        }

        e.Handled = true;
    }

    /// <summary>パネルの左上（アプリのウィンドウの DIP）を求める。</summary>
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

    /// <summary>ポインターの位置を画面の物理ピクセルで求める。</summary>
    /// <param name="window">バーのウィンドウ。</param>
    /// <param name="e">ポインターの引数。</param>
    /// <returns>画面の位置（物理ピクセル）。</returns>
    /// <remarks>
    /// <b>自分で足して求める</b>: ウィンドウの位置は動かした直後には古い値のことがあり、
    /// <c>PointToScreen</c> の結果がずれる。 位置とウィンドウ内の座標から毎回求め直す。
    /// </remarks>
    private PixelPoint GetCursorScreen(Window window, PointerEventArgs e)
    {
        var scale = TopLevel.GetTopLevel(owner) is Window main && main.RenderScaling > 0
            ? main.RenderScaling
            : 1;
        var local = e.GetPosition(window);
        return new PixelPoint(
            window.Position.X + (int)Math.Round(local.X * scale),
            window.Position.Y + (int)Math.Round(local.Y * scale));
    }

    /// <summary>パネルの位置を置ける領域の内側へ収める。</summary>
    /// <param name="position">収める前の位置（アプリのウィンドウの DIP）。</param>
    /// <param name="panelSize">パネルの大きさ（DIP）。</param>
    /// <returns>収めた後の位置（アプリのウィンドウの DIP）。</returns>
    private EsPoint ClampToMain(EsPoint position, EsSize panelSize)
    {
        if (TopLevel.GetTopLevel(owner) is not Window main)
        {
            return position;
        }

        var available = new EsRect(0, 0, main.ClientSize.Width, main.ClientSize.Height);
        return FloatingPanelPlacement.Clamp(position, panelSize.Width, panelSize.Height, available);
    }

    /// <summary>パネルの左上を画面の位置として適用する。</summary>
    /// <param name="window">バーのウィンドウ。</param>
    /// <param name="position">左上の位置（アプリのウィンドウの DIP）。</param>
    private void ApplyPosition(Window window, EsPoint position)
    {
        if (TopLevel.GetTopLevel(owner) is not Window main)
        {
            return;
        }

        var scale = main.RenderScaling > 0 ? main.RenderScaling : 1;
        window.Position = new PixelPoint(
            main.Position.X + (int)Math.Round(position.X * scale),
            main.Position.Y + (int)Math.Round(position.Y * scale));
    }

    /// <summary>パネルの枠・余白・下地を外観から適用する。</summary>
    /// <param name="root">パネルの根。</param>
    private static void ApplyFrame(Border root)
    {
        root.Background = RichTextPainter.ToBrush(s_appearance.Background);
        root.BorderBrush = RichTextPainter.ToBrush(s_appearance.Border);
        root.BorderThickness = new Thickness(s_appearance.BorderThickness);
        root.CornerRadius = new CornerRadius(s_appearance.CornerRadius);
        root.Padding = new Thickness(
            s_appearance.Padding, s_appearance.VerticalPadding,
            s_appearance.Padding, s_appearance.VerticalPadding);

        // **余白が掴む場所だと分かるようにする**: 中身の上では各項目のカーソルになる
        root.Cursor = new Cursor(StandardCursorType.SizeAll);
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

        ApplyFrame(_root);

        // **余白を掴んで動かせるようにする**: 中身（ボタン・入力欄）は押下を処理済みにするため、
        // 本ハンドラーは余白を掴んだときだけ呼ばれる
        _root.PointerPressed += OnBarPointerPressed;
        _root.PointerMoved += OnBarPointerMoved;
        _root.PointerReleased += OnBarPointerReleased;

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

    /// <summary>バーの中身の実寸（DIP）を測る。</summary>
    /// <param name="bar">書式バー。</param>
    /// <returns>実寸。 測れない場合は希望寸法。</returns>
    private static EsSize MeasureBar(IStyledTextFormatBar bar)
    {
        bar.View.Measure(Size.Infinity);
        var desired = bar.View.DesiredSize;
        var width = desired.Width > 0 ? desired.Width : bar.PreferredWidth;
        var height = s_appearance.ResolveContentHeight(desired.Height);
        return new EsSize(Math.Max(1, width), Math.Max(1, height));
    }

    /// <summary>Avalonia の矩形を本層の矩形へ写す。</summary>
    /// <param name="rect">Avalonia の矩形。</param>
    /// <returns>本層の矩形。</returns>
    private static EsRect ToEsRect(Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
