using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.MewUI.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置と枠は提示層が単一所有する</b>: 対象に被せない配置・置ける領域への収め・ドラッグの規則は
/// <see cref="FloatingPanelPlacement"/>、枠・余白・高さは <see cref="FloatingPanelAppearance.FormatBar"/> が
/// 決める（4 つの UI 版で同じ結果になる）。<br/>
/// <b>枠なしのウィンドウへ載せる</b>: 注釈のパネルと同じ作りにする（同じ見た目）。<br/>
/// <b>位置は画面の座標で持つ</b>: ウィンドウを動かすと、ウィンドウを基準にした座標は動いた分だけ戻る。
/// 画面の座標で持てば、動かしても基準が変わらない。<br/>
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

    /// <summary>利用者が動かした量（DIP。 まだ動かしていない場合は原点）。</summary>
    /// <remarks>
    /// <b>動かした位置を選択より優先する</b>: 選択が変わるたびに元の位置へ戻すと、動かした意味が無くなる。
    /// 閉じたときに捨てる（次に出したときは選択の位置から出す）。
    /// </remarks>
    private EsPoint _manualOffset;

    /// <summary>ドラッグ開始時のバーの左上（画面の DIP）。</summary>
    private EsPoint _dragOrigin;

    /// <summary>直前に見たカーソルの位置（画面の物理ピクセル）。</summary>
    private EsPoint _lastCursor;

    /// <summary>ドラッグ中かどうか。</summary>
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

        MoveToScreen(window, ResolvePosition(main, panelSize));
    }

    /// <summary>バーを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        // **動かした位置は捨てる**: 次に出したときは選択の位置から出す
        _manualOffset = default;
        _dragging = false;

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

        if (_dragging || _manualOffset.X != 0 || _manualOffset.Y != 0)
        {
            var kept = FloatingPanelPlacement.Clamp(
                _manualOffset, panelSize.Width, panelSize.Height, available);
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
    private static void MoveToScreen(Window window, EsPoint position)
        => window.MoveTo((int)Math.Round(position.X), (int)Math.Round(position.Y));

    /// <summary>バーの余白を掴んで動かし始める。</summary>
    /// <param name="e">マウスの引数。</param>
    /// <remarks>
    /// <b>押下の可否は LeftButton で判定しない</b>: 押下の通知には押す前の状態が入っており、
    /// ここで弾くとドラッグが始まらない（実測）。 押下の通知自体が「押された」ことを表す。
    /// </remarks>
    private void BeginDrag(MouseEventArgs e)
    {
        _ = e;
        if (_window is not { } window || owner.HostWindow is not { } main
            || !NativeCursor.TryGetPosition(out var x, out var y))
        {
            return;
        }

        var scale = main.DpiScale > 0 ? main.DpiScale : 1;
        _dragOrigin = new EsPoint(window.Position.X / scale, window.Position.Y / scale);
        _lastCursor = new EsPoint(x, y);
        _dragging = true;
    }

    /// <summary>バーをドラッグして動かす。</summary>
    /// <param name="e">マウスの引数。</param>
    private void ContinueDrag(MouseEventArgs e)
    {
        _ = e;
        if (!_dragging || _window is not { } window || owner.HostWindow is not { } main
            || !NativeCursor.TryGetPosition(out var x, out var y))
        {
            return;
        }

        var cursor = new EsPoint(x, y);
        var scale = main.DpiScale > 0 ? main.DpiScale : 1;
        var delta = new EsPoint((cursor.X - _lastCursor.X) / scale, (cursor.Y - _lastCursor.Y) / scale);
        _lastCursor = cursor;

        if (Math.Abs(delta.X) < 0.01 && Math.Abs(delta.Y) < 0.01)
        {
            return;
        }

        var panelSize = s_appearance.ResolvePanelSize(MeasureBar(owner.FormatBar));
        var available = new EsRect(0, 0, main.ClientSize.Width, main.ClientSize.Height);
        _manualOffset = FloatingPanelPlacement.Clamp(
            new EsPoint(_manualOffset.X + delta.X, _manualOffset.Y + delta.Y),
            panelSize.Width,
            panelSize.Height,
            available);

        MoveToScreen(window, new EsPoint(_dragOrigin.X + _manualOffset.X, _dragOrigin.Y + _manualOffset.Y));
    }

    /// <summary>バーのドラッグを終える。</summary>
    private void EndDrag()
    {
        _dragging = false;
        _lastCursor = default;
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
        _root.Cursor = CursorType.SizeAll;
        _root.OnMouseDown(BeginDrag);
        _root.OnMouseMove(ContinueDrag);
        _root.OnMouseUp(_ => EndDrag());
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
