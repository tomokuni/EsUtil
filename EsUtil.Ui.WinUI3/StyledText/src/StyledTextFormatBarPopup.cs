using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace EsUtil.Ui.WinUI3.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置の規則は提示層が単一所有する</b>: 選択の上・下の判断と置ける領域への収めは
/// <see cref="TextFormatBarPlacement"/> が決める（4 つの UI 版で同じ結果になる）。<br/>
/// <b>出す条件は本文コントロールが単一所有する</b>: ホストがバーを置く場合と同じ判断
/// （<see cref="StyledTextControl.ShouldShowFormatBar"/>）を使う。<br/>
/// <b>ポップアップはウィンドウの外へも出せる</b>: <c>ShouldConstrainToRootBounds</c> を切ると、
/// ウィンドウの内側に収まらない位置でも表示できる（位置は本クラスがウィンドウの内側へ収める）。
/// </remarks>
/// <param name="owner">書式バーを所有する本文コントロール。</param>
internal sealed class StyledTextFormatBarPopup(StyledTextControl owner)
{
    /// <summary>バーを載せるポップアップ（初回の表示で作る）。</summary>
    private Popup? _popup;

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

        // **1 つの要素は 1 つの親しか持てない**: ホストのレイアウトに残っていれば外してから載せる
        DetachFromParent(bar.View);
        popup.Child = bar.View;

        var barSize = MeasureBar(bar);
        var origin = ResolveOrigin(root);
        var position = TextFormatBarPlacement.ResolvePopupPosition(
            ToEsRect(owner.SelectionBounds),
            barSize,
            new EsRect(-origin.X, -origin.Y, root.Size.Width, root.Size.Height),
            TextFormatBarPlacement.Gap);

        // **位置はウィンドウの左上を原点とする DIP**: コントロールの原点を足して写す
        popup.HorizontalOffset = origin.X + position.X;
        popup.VerticalOffset = origin.Y + position.Y;
        popup.IsOpen = true;
    }

    /// <summary>バーを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        if (_popup is { } popup)
        {
            popup.IsOpen = false;

            // **中身を外す**: ホストが埋め込みへ戻せるよう、バーをポップアップから切り離す
            popup.Child = null;
        }
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
        };
        return _popup;
    }

    /// <summary>このコントロールの左上（ウィンドウの座標の DIP）を求める。</summary>
    /// <param name="root">XAML のルート。</param>
    /// <returns>左上の位置。 求められない場合は原点。</returns>
    private Windows.Foundation.Point ResolveOrigin(XamlRoot root)
    {
        if (owner.TransformToVisual(null) is not { } transform)
        {
            return default;
        }

        _ = root;
        return transform.TransformPoint(default);
    }

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
