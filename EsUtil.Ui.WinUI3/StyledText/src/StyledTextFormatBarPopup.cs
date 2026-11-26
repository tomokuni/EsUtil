using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

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
    /// <summary>バーの入れ物（ポップアップの中身。 1 度だけ載せる）。</summary>
    private readonly Border _root = new();

    /// <summary>バーを載せるポップアップ（初回の表示で作る）。</summary>
    private Popup? _popup;

    /// <summary>利用者が動かした後の左上（ウィンドウの座標の DIP。 まだ動かしていない場合は <see langword="null"/>）。</summary>
    private EsPoint? _manualPosition;

    /// <summary>ドラッグ開始時のバーの左上（ウィンドウの座標の DIP）。</summary>
    private EsPoint _dragOrigin;

    /// <summary>ドラッグ開始時のポインターの位置（ウィンドウの座標の DIP）。</summary>
    private EsPoint _dragOriginCursor;

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

        var barSize = MeasureBar(bar);
        var origin = ResolveOrigin();
        var available = new EsRect(-origin.X, -origin.Y, root.Size.Width, root.Size.Height);

        // **動かした位置を選択より優先する**: 選択が変わるたびに元の位置へ戻すと、動かした意味が無くなる
        var position = _manualPosition is { } manual
            ? FloatingPanelPlacement.Clamp(manual, barSize.Width, barSize.Height, available)
            : FloatingPanelPlacement.ResolveOutside(
                ToEsRect(owner.SelectionBounds), barSize, available, FloatingPanelPlacement.FormatBarGap);

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

        if (_popup is { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }
    }

    /// <summary>バーの余白を掴んで動かし始める。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnBarPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _ = sender;
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse
            && e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Touch
            && e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Pen)
        {
            return;
        }

        _dragOrigin = _manualPosition ?? new EsPoint(ResolveOrigin().X, ResolveOrigin().Y);
        _dragOriginCursor = ToWindowDip(e);
        _root.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>バーをドラッグして動かす。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnBarPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _ = sender;
        if (_popup is not { } popup || owner.XamlRoot is not { } root)
        {
            return;
        }

        var barSize = MeasureBar(owner.FormatBar);
        var moved = FloatingPanelPlacement.ResolveDrag(_dragOrigin, _dragOriginCursor, ToWindowDip(e));
        var available = new EsRect(-ResolveOrigin().X, -ResolveOrigin().Y, root.Size.Width, root.Size.Height);
        _manualPosition = FloatingPanelPlacement.Clamp(moved, barSize.Width, barSize.Height, available);

        popup.HorizontalOffset = _manualPosition.Value.X;
        popup.VerticalOffset = _manualPosition.Value.Y;
        e.Handled = true;
    }

    /// <summary>バーのドラッグを終える。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnBarPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _ = sender;
        _root.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    /// <summary>ポインターの位置をウィンドウの座標（DIP）へ写す。</summary>
    /// <param name="e">ポインターの引数。</param>
    /// <returns>位置（ウィンドウの座標の DIP）。</returns>
    private static EsPoint ToWindowDip(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(null).Position;
        return new EsPoint(position.X, position.Y);
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
        _root.PointerPressed += OnBarPointerPressed;
        _root.PointerMoved += OnBarPointerMoved;
        _root.PointerReleased += OnBarPointerReleased;
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
