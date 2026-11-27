using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Input;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace EsUtil.Ui.WinUI3.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置の規則は提示層が単一所有する</b>: 選択の上・下の判断と置ける領域への収めは
/// <see cref="FloatingPanelPlacement"/> が決める（4 つの UI 版で同じ結果になる）。<br/>
/// <b>出す条件は本文コントロールが単一所有する</b>: ホストがバーを置く場合と同じ判断
/// （<see cref="StyledTextControl.ShouldShowFormatBar"/>）を使う。<br/>
/// <b>ポップアップの中身は 1 度だけ決める</b>: バーは項目の変更で作り直されるため、そのたびに
/// ポップアップの中身を差し替えると要素の付け替えが重なって「既に親がある」で落ちる。 中身は変わらない根
/// （<see cref="Border"/>）を 1 度だけ載せ、根の子だけを差し替える。<br/>
/// <b>ウィンドウの外へも出せる</b>: <c>ShouldConstrainToRootBounds</c> を切ると、ウィンドウの内側に
/// 収まらない位置でも表示できる（位置は本クラスがウィンドウの内側へ収める）。
/// </remarks>
/// <param name="owner">書式バーを所有する本文コントロール。</param>
internal sealed class StyledTextFormatBarPopup(StyledTextControl owner)
{
    /// <summary>パネルの外観（枠・余白・高さ）。</summary>
    private static readonly FloatingPanelAppearance s_appearance = FloatingPanelAppearance.FormatBar;

    /// <summary>バーの入れ物（ポップアップの中身。 1 度だけ載せる）。 枠と余白は外観が決める。</summary>
    private readonly Border _root = new()
    {
        Background = RichTextPainter.ToBrush(s_appearance.Background),
        BorderBrush = RichTextPainter.ToBrush(s_appearance.Border),
        BorderThickness = new Thickness(s_appearance.BorderThickness),
        CornerRadius = new CornerRadius(s_appearance.CornerRadius),
        Padding = new Thickness(
            s_appearance.Padding, s_appearance.VerticalPadding,
            s_appearance.Padding, s_appearance.VerticalPadding),
    };

    /// <summary>バーを載せるポップアップ（初回の表示で作る）。</summary>
    private Popup? _popup;

    /// <summary>利用者が動かした後の左上（ウィンドウの座標の DIP。 まだ動かしていない場合は <see langword="null"/>）。</summary>
    private EsPoint? _manualPosition;

    /// <summary>押した位置（画面の DIP）。 ドラッグの開始の判定に使う。</summary>
    private EsPoint _pressCursor;

    /// <summary>ドラッグ開始時のバーの左上（コントロールの座標の DIP）。</summary>
    private EsPoint _dragOrigin;

    /// <summary>ドラッグ開始時のカーソルの位置（画面の DIP）。</summary>
    private EsPoint _dragOriginCursor;

    /// <summary>押したままかどうか（まだドラッグが始まっていない押下を含む）。</summary>
    private bool _pressed;

    /// <summary>ドラッグ中かどうか（<see cref="DragThreshold"/> を超えて動いた後）。</summary>
    private bool _dragging;

    /// <summary>カーソルの位置（画面の物理ピクセル）を取得する手段。</summary>
    /// <remarks>
    /// <b>ポインターの引数からは取らない</b>: ポップアップは別のウィンドウに載るため、本文のウィンドウを
    /// 基準にした位置は動かしても変わらない（実測）。 画面の位置なら基準が動かない。
    /// </remarks>
    private readonly ICursorService _cursor = new WinCursorService();

    /// <summary>バーをポップアップで表示中かどうかを取得する。</summary>
    public bool IsOpen => _popup is { IsOpen: true };

    /// <summary>いまの状態に合わせて、バーを出し入れし、位置を合わせる。</summary>
    public void Update()
    {
        if (!owner.ShouldShowFormatBar || owner.XamlRoot is not { } root)
        {
            Close();
            return;
        }

        var bar = owner.FormatBar;
        bar.Refresh();

        var popup = EnsurePopup(root);
        SetBar(bar);

        var panelSize = s_appearance.ResolvePanelSize(MeasureBar(bar));
        var origin = ResolveOrigin();
        var available = new EsRect(-origin.X, -origin.Y, root.Size.Width, root.Size.Height);

        // **動かした位置を選択より優先する**: 選択が変わるたびに元の位置へ戻すと、動かした意味が無くなる
        var position = _manualPosition is { } manual
            ? FloatingPanelPlacement.Clamp(manual, panelSize.Width, panelSize.Height, available)
            : FloatingPanelPlacement.ResolveOutside(
                ToEsRect(owner.SelectionBounds), panelSize, available, FloatingPanelPlacement.FormatBarGap);

        // **位置はウィンドウの左上を原点とする DIP**: コントロールの原点を足して写す
        popup.HorizontalOffset = origin.X + position.X;
        popup.VerticalOffset = origin.Y + position.Y;
        popup.IsOpen = true;
    }

    /// <summary>バーを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        // **中身を先に外す**: 閉じたポップアップに要素を残すと、次に載せるときに「既に親がある」で落ちる
        _root.Child = null;

        // **動かした位置は捨てる**: 次に出したときは選択の位置から出す
        _manualPosition = null;
        _dragging = false;

        if (_popup is { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }
    }

    /// <summary>バーを押した（まだドラッグは始めない）。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    /// <remarks>
    /// <b>押下では動かさない</b>: 押しただけで動かすと、ボタンの操作ができなくなる。 押した位置を覚えるだけで、
    /// <b>動きが <see cref="DragThreshold"/> を超えた時点</b>で初めて動かす（<see cref="OnBarPointerMoved"/>）。<br/>
    /// <b>処理済みの押下も受ける</b>: 中身（ボタン・入力欄）は押下を処理済みにするため、通常の購読では届かない。
    /// バーの全域を掴めるようにするため、処理済みも受ける。
    /// </remarks>
    private void OnBarPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _ = sender;
        if (_popup is not { } popup
            || !_cursor.TryGetCursorScreenPoint(out var screen))
        {
            return;
        }

        // **項目の押下は妨げない**: ここでは処理済みにせず、捕捉もしない（ドラッグが始まるまで項目が使える）
        // 位置はコントロールの座標で持つ（置ける領域と同じ座標系にする）
        var origin = ResolveOrigin();
        _dragOrigin = new EsPoint(popup.HorizontalOffset - origin.X, popup.VerticalOffset - origin.Y);
        _pressCursor = ToDip(screen);
        _pressed = true;
        _dragging = false;
    }

    /// <summary>バーをドラッグして動かす（動きが閾値を超えてから）。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnBarPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;
        if (!_pressed || _popup is not { } popup || owner.XamlRoot is not { } root
            || !_cursor.TryGetCursorScreenPoint(out var screen))
        {
            return;
        }

        var cursor = ToDip(screen);
        if (!_dragging)
        {
            if (!DragThreshold.IsExceeded(_pressCursor, cursor))
            {
                return;
            }

            // **ドラッグが始まったら押下を項目から取り上げる**: 取り上げると項目は「押された」状態を
            // 失うため、離してもクリックにならない（ドラッグをボタンの操作と見なさない）
            _dragging = true;
            _dragOriginCursor = _pressCursor;
            _root.CapturePointer(e.Pointer);
        }

        var moved = FloatingPanelPlacement.ResolveDrag(_dragOrigin, _dragOriginCursor, cursor);
        var panelSize = s_appearance.ResolvePanelSize(MeasureBar(owner.FormatBar));
        var origin = ResolveOrigin();
        var available = new EsRect(-origin.X, -origin.Y, root.Size.Width, root.Size.Height);
        _manualPosition = FloatingPanelPlacement.Clamp(
            moved, panelSize.Width, panelSize.Height, available);

        popup.HorizontalOffset = origin.X + _manualPosition.Value.X;
        popup.VerticalOffset = origin.Y + _manualPosition.Value.Y;
        e.Handled = true;
    }

    /// <summary>バーを離した。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    /// <remarks>
    /// <b>ドラッグはクリックと見なさない</b>: ドラッグ中だった場合は処理済みにして、項目の解放処理
    /// （クリック）へ届かせない。
    /// </remarks>
    private void OnBarPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;
        if (!_pressed)
        {
            return;
        }

        var wasDragging = _dragging;
        _pressed = false;
        _dragging = false;
        _root.ReleasePointerCapture(e.Pointer);

        if (wasDragging)
        {
            e.Handled = true;
        }
    }




    /// <summary>画面の位置（物理ピクセル）を DIP へ写す。</summary>
    /// <param name="screen">画面の位置（物理ピクセル）。</param>
    /// <returns>画面の位置（DIP）。</returns>
    /// <remarks>ウィンドウの DIP と同じ倍率で写す（倍率が同じため差は位置だけになる）。</remarks>
    private EsPoint ToDip(EsPointInt screen)
    {
        var scale = _root.XamlRoot?.RasterizationScale is { } value && value > 0 ? value : 1;
        return new EsPoint(screen.X / scale, screen.Y / scale);
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
            case Panel panel:
                panel.Children.Remove(view);
                break;
            case Border border when ReferenceEquals(border.Child, view):
                border.Child = null;
                break;
            case ContentControl content when ReferenceEquals(content.Content, view):
                content.Content = null;
                break;
            default:
                break;
        }
    }

    /// <summary>バーを載せるポップアップを用意する。</summary>
    /// <param name="root">XAML のルート。</param>
    /// <returns>ポップアップ。</returns>
    private Popup EnsurePopup(XamlRoot root)
    {
        if (_popup is { } existing)
        {
            existing.XamlRoot = root;
            return existing;
        }

        _popup = new Popup
        {
            XamlRoot = root,
            ShouldConstrainToRootBounds = false,
            IsLightDismissEnabled = false,
            Child = _root,
        };

        // **余白を掴んで動かせるようにする**: 中身（ボタン・入力欄）は押下を処理済みにするため、
        // 本ハンドラーは余白を掴んだときだけ呼ばれる
        // **バーのどこを掴んでも動かせるようにする**: 中身（ボタン・入力欄）は押下を処理済みにするため、
        // 通常の購読では届かない。 処理済みも受け、動かしたときだけドラッグとする
        _root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnBarPointerPressed), handledEventsToo: true);
        _root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnBarPointerMoved), handledEventsToo: true);
        _root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnBarPointerReleased), handledEventsToo: true);
        return _popup;
    }

    /// <summary>このコントロールの左上（ウィンドウの座標の DIP）を求める。</summary>
    /// <returns>左上の位置。 求められない場合は原点。</returns>
    private Windows.Foundation.Point ResolveOrigin()
        => owner.TransformToVisual(null) is { } transform ? transform.TransformPoint(default) : default;

    /// <summary>バーの実寸（DIP）を測る。</summary>
    /// <param name="bar">書式バー。</param>
    /// <returns>実寸。 測れない場合は希望寸法。</returns>
    /// <remarks>
    /// <b>中身を測ってから出す</b>: 出した直後の実寸は 0 のため、それだけで位置を決めると画面の外へ出る。
    /// </remarks>
    private static EsSize MeasureBar(IStyledTextFormatBar bar)
    {
        bar.View.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = bar.View.DesiredSize;
        var width = desired.Width > 0 ? desired.Width : bar.PreferredWidth;
        var height = desired.Height > 0 ? desired.Height : TextFormatBarLayout.ItemHeight;
        return new EsSize(Math.Max(1, width), Math.Max(1, height));
    }

    /// <summary>WinUI 3 の矩形を本層の矩形へ写す。</summary>
    /// <param name="rect">WinUI 3 の矩形。</param>
    /// <returns>本層の矩形。</returns>
    private static EsRect ToEsRect(Windows.Foundation.Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
