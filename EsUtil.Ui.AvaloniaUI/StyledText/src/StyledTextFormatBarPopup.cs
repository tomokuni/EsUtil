using Avalonia;
using Avalonia.Controls;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置の規則は提示層が単一所有する</b>: 選択の上・下の判断と置ける領域への収めは
/// <see cref="TextFormatBarPlacement"/> が決める（4 つの UI 版で同じ結果になる）。<br/>
/// <b>出す条件は本文コントロールが単一所有する</b>: ホストがバーを置く場合と同じ判断
/// （<see cref="StyledTextControl.ShouldShowFormatBar"/>）を使う。<br/>
/// <b>バーは本文コントロールが所有する</b>: 本クラスは画面要素を置き、位置を合わせるだけである。<br/>
/// <b>枠なしのウィンドウへ載せる</b>: ポップアップへ載せると、その中から開く色の一覧が潰れて使えない。
/// 枠なしのウィンドウなら、中から開く一覧は普通のウィンドウの子になり、そのまま使える。
/// </remarks>
/// <param name="owner">書式バーを所有する本文コントロール。</param>
internal sealed class StyledTextFormatBarPopup(StyledTextControl owner)
{
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

        // **バーは作り直される**: 項目や構成が変わっても出ているバーが古くならないよう、毎回付け直す
        // **1 つの要素は 1 つの親しか持てない**: ホストのレイアウトに残っていれば外してから載せる
        DetachFromParent(bar.View);
        window.Content = bar.View;

        var barSize = MeasureBar(bar);
        window.Width = Math.Max(1, barSize.Width);
        window.Height = Math.Max(1, barSize.Height);

        var scale = main.RenderScaling > 0 ? main.RenderScaling : 1;
        var origin = owner.TranslatePoint(default, main) ?? default;
        var position = TextFormatBarPlacement.ResolvePopupPosition(
            ToEsRect(owner.SelectionBounds),
            barSize,
            new EsRect(-origin.X, -origin.Y, main.ClientSize.Width, main.ClientSize.Height),
            TextFormatBarPlacement.Gap);

        // **ウィンドウの位置は画面の物理ピクセル**: コントロールの原点と求めた位置を写して足す
        var x = main.Position.X + (int)Math.Round((origin.X + position.X) * scale);
        var y = main.Position.Y + (int)Math.Round((origin.Y + position.Y) * scale);

        if (!window.IsVisible)
        {
            window.Show(main);
        }

        window.Position = new PixelPoint(x, y);
    }

    /// <summary>バーを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        UnhookMain();
        if (_window is { } window)
        {
            // **中身を外す**: ホストが埋め込みへ戻せるよう、バーをウィンドウから切り離す
            window.Content = null;
            if (window.IsVisible)
            {
                window.Hide();
            }
        }
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
