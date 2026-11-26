using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Input;

namespace EsUtil.Ui.MewUI.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置と枠は提示層が単一所有する</b>: 対象に被せない配置・置ける領域への収め・ドラッグの規則は
/// <see cref="FloatingPanelPlacement"/>、枠・余白・高さは <see cref="FloatingPanelAppearance.FormatBar"/> が
/// 決める（4 つの UI 版で同じ結果になる）。<br/>
/// <b>枠なしのウィンドウへ載せる</b>: 注釈のパネルと同じ作りにする（同じ見た目）。<br/>
/// <b>位置は本文のウィンドウの内側の座標で持つ</b>: ウィンドウを動かしてもバーが付いてくる。<br/>
/// <b>ドラッグの差はカーソルの画面位置から取る</b>: イベントの座標は要素やウィンドウの座標系であり、
/// 動かすと基準も動く（移動量が増幅される。 実測）。
/// </remarks>
/// <param name="owner">書式バーを所有する本文コントロール。</param>
internal sealed class StyledTextFormatBarPopup(StyledTextControl owner)
{
    /// <summary>パネルの外観（枠・余白・高さ）。</summary>
    private static readonly FloatingPanelAppearance s_appearance = FloatingPanelAppearance.FormatBar;


    /// <summary>バーの入れ物（ウィンドウの中身。 枠と余白は外観が決める）。</summary>
    private readonly Border _root = new()
    {
        Background = RichTextPainter.ToMewColor(s_appearance.Background),
        BorderBrush = RichTextPainter.ToMewColor(s_appearance.Border),
        BorderThickness = s_appearance.BorderThickness,
        CornerRadius = s_appearance.CornerRadius,
    };

    /// <summary>バーを載せる枠なしのウィンドウ（初回の表示で作る）。</summary>
    private Window? _window;

    /// <summary>ウィンドウを表示したかどうか（<c>Show</c> は 1 度だけ呼ぶ）。</summary>
    private bool _shown;

    /// <summary>利用者が動かした後の左上（本文のウィンドウの内側の DIP。 まだ動かしていない場合は <see langword="null"/>）。</summary>
    /// <remarks>
    /// <b>動かした位置を選択より優先する</b>: 選択が変わるたびに元の位置へ戻すと、動かした意味が無くなる。
    /// 閉じたときに捨てる（次に出したときは選択の位置から出す）。
    /// </remarks>
    private EsPoint? _manualPosition;

    /// <summary>押していない間に見た最後の位置（根の座標の DIP）。 押した位置の代わりに使う。</summary>
    /// <remarks>
    /// <b>押下の通知からは取らない</b>: 押した位置は、押していないときの移動の位置で足りる。 MewUI の
    /// 押下の通知は要素へ届かないが、移動の通知はウィンドウへ届く（実測）。 押下の通知には押す前の状態が
    /// 入っており（<c>LeftButton</c> が false）、そのままでは押したことを見分けられない。
    /// </remarks>
    private EsPoint _restPosition;

    /// <summary>OS の移動の仕組みが動いている間かどうか。</summary>
    private bool _dragging;

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

        if (!_shown)
        {
            window.Show(main);
            _shown = true;
        }

        ApplyPosition(window, ResolvePosition(main, panelSize));
    }


    /// <summary>バーを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        // **動かした位置は捨てる**: 次に出したときは選択の位置から出す
        _manualPosition = null;

        // **中身を先に外す**: 隠したウィンドウに要素を残すと、次に載せるときに「既に親がある」で落ちる
        _root.Child = null;

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

        // **余白は中身の外側に空ける**: MewUI の Border は余白（Padding）の範囲を押下の対象に
        // しないため、余白を Margin にして下地の上へ出す（掴めるようにする）
        bar.View.Margin = new Thickness(
            s_appearance.Padding, s_appearance.VerticalPadding,
            s_appearance.Padding, s_appearance.VerticalPadding);
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

    /// <summary>バーの左上（画面の DIP）を求める。</summary>
    /// <param name="main">本文のウィンドウ。</param>
    /// <param name="panelSize">パネルの大きさ（DIP）。</param>
    /// <returns>左上の位置（画面の DIP）。</returns>
    private EsPoint ResolvePosition(Window main, EsSize panelSize)
    {
        var bounds = owner.Bounds;
        var selection = owner.SelectionBounds;
        var available = new EsRect(0, 0, main.ClientSize.Width, main.ClientSize.Height);

        if (_manualPosition is { } manual)
        {
            var kept = FloatingPanelPlacement.Clamp(
                manual, panelSize.Width, panelSize.Height, available);
            return new EsPoint(main.Position.X + kept.X, main.Position.Y + kept.Y);
        }

        // **座標は本文のウィンドウの内側の DIP**（MewUI の Bounds はウィンドウ基準）
        var anchor = new EsRect(
            bounds.X + selection.X, bounds.Y + selection.Y, selection.Width, selection.Height);
        var placed = FloatingPanelPlacement.ResolveOutside(
            anchor, panelSize, available, FloatingPanelPlacement.FormatBarGap);
        return new EsPoint(main.Position.X + placed.X, main.Position.Y + placed.Y);
    }


    /// <summary>バーを画面の位置へ移す。</summary>
    /// <param name="window">バーのウィンドウ。</param>
    /// <param name="position">左上の位置（画面の DIP）。</param>
    private static void ApplyPosition(Window window, EsPoint position)
        => window.MoveTo((int)Math.Round(position.X), (int)Math.Round(position.Y));

    /// <summary>バーを押したまま動かしたら、OS の移動の仕組みで動かす（動きが閾値を超えてから）。</summary>
    /// <param name="e">マウスの引数。</param>
    /// <remarks>
    /// <b>バーのどこを掴んでも動かせる</b>: 移動の通知はウィンドウへ届くため（実測）、中身（ボタン・入力欄）の
    /// 上からでも動かせる。 要素ごとにハンドラーを繋ぐ必要がない。<br/>
    /// <b>押下では動かさない</b>: 動きが <see cref="DragThreshold"/> を超えた時点で初めて動かす。 動かさなければ
    /// 従来どおり項目の操作になる（OS の移動の仕組みは押下を項目から取り上げるため、クリックにならない）。<br/>
    /// <b>移動は OS の移動の仕組みに任せる</b>: 枠なしのウィンドウは移動のつまみを持たないため
    /// <see cref="Window.DragMove"/> を使う（MewUI の案内どおり）。 要素へ届く移動の通知で動かすと、
    /// ウィンドウが動いた分だけ基準も動き、移動量が増幅される（実測）。
    /// </remarks>
    private void OnWindowMouseMove(MouseEventArgs e)
    {
        if (_window is not { } window || owner.HostWindow is not { } main)
        {
            return;
        }

        var point = e.GetPosition(_root);
        var position = new EsPoint(point.X, point.Y);

        // **押していない間の位置を覚える**: 押した位置はこの位置で表す（押下の通知は要素へ届かない）
        if (!e.LeftButton)
        {
            _restPosition = position;
            return;
        }

        if (_dragging || !DragThreshold.IsExceeded(_restPosition, position))
        {
            return;
        }

        // **押したウィンドウを有効にしてから動かす**: 有効でないウィンドウでは、押したままの
        // 移動の通知が届かない（実測。 最初の 1 回だけ動かない）
        _dragging = true;
        window.Activate();
        window.DragMove();
        _dragging = false;

        // **動かした位置を覚える**: 次の更新で選択の位置へ戻さない（離すまで戻らないので、ここで終わる）
        _manualPosition = new EsPoint(
            window.Position.X - main.Position.X, window.Position.Y - main.Position.Y);
    }

    /// <summary>バーを載せるウィンドウを用意する。</summary>
    /// <returns>枠なしのウィンドウ。</returns>
    private Window EnsureWindow()
    {
        if (_window is { } existing)
        {
            return existing;
        }

        // **ウィンドウの飾りを残さない**: 枠と余白はパネル（根）が持つ。 ウィンドウ側に残すと
        // 枠が二重になり、押下もウィンドウの余白に吸われる（注釈のパネルと同じ設定にする）
        _window = new Window
        {
            Borderless = true,
            BorderThickness = 0,
            BorderBrush = Aprillz.MewUI.Color.Transparent,
            Padding = Thickness.Zero,
            Title = "書式バー",
            Content = _root,
        };

        // **バーのどこを掴んでも動かせるようにする**: 移動の通知はウィンドウへ届く（実測）
        _root.Cursor = CursorType.SizeAll;
        _window.OnMouseMove(OnWindowMouseMove);
        return _window;
    }

    /// <summary>バーの中身の実寸（DIP）を測る。</summary>
    /// <param name="bar">書式バー。</param>
    /// <returns>実寸。 測れない場合は希望寸法。</returns>
    private static EsSize MeasureBar(IStyledTextFormatBar bar)
    {
        var desired = bar.View.DesiredSize;
        var width = desired.Width > 0 ? desired.Width : bar.PreferredWidth;
        var height = s_appearance.ResolveContentHeight(desired.Height);
        return new EsSize(Math.Max(1, width), Math.Max(1, height));
    }
}
