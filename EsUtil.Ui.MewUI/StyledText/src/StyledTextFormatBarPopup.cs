using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.MewUI.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置の規則は提示層が単一所有する</b>: 選択の上・下の判断と置ける領域への収めは
/// <see cref="TextFormatBarPlacement"/> が決める（4 つの UI 版で同じ結果になる）。<br/>
/// <b>出す条件は本文コントロールが単一所有する</b>: ホストがバーを置く場合と同じ判断
/// （<see cref="StyledTextControl.ShouldShowFormatBar"/>）を使う。<br/>
/// <b>枠なしのウィンドウへ載せる</b>: ポップアップへ載せると、その中から開く色の選択が使えない。
/// 枠なしのウィンドウなら、中から開く一覧は普通のウィンドウの子になり、そのまま使える。
/// </remarks>
/// <param name="owner">書式バーを所有する本文コントロール。</param>
internal sealed class StyledTextFormatBarPopup(StyledTextControl owner)
{
    /// <summary>バーを載せる枠なしのウィンドウ（初回の表示で作る）。</summary>
    private Window? _window;

    /// <summary>ウィンドウを表示したかどうか（<c>Show</c> は 1 度だけ呼ぶ）。</summary>
    private bool _shown;

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

        // **バーは 1 つの場所にしか置けない**: ホストのレイアウトに残っていれば外してから載せる
        DetachFromParent(bar.View);
        window.Content = bar.View;

        var barSize = MeasureBar(bar);
        window.WindowSize = WindowSize.Fixed(barSize.Width, barSize.Height);

        // **位置はウィンドウ座標**: コントロールの Bounds はウィンドウ基準であるため、そのまま足せる
        var anchor = ToEsRect(owner.SelectionBounds);
        var position = TextFormatBarPlacement.ResolvePopupPosition(
            new EsRect(anchor.X + owner.Bounds.X, anchor.Y + owner.Bounds.Y, anchor.Width, anchor.Height),
            barSize,
            new EsRect(0, 0, main.ClientSize.Width, main.ClientSize.Height),
            TextFormatBarPlacement.Gap);

        if (!_shown)
        {
            window.Show(main);
            _shown = true;
        }

        window.MoveTo((int)Math.Round(position.X), (int)Math.Round(position.Y));
    }

    /// <summary>バーを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        if (_shown)
        {
            // **中身を外す**: ホストが埋め込みへ戻せるよう、バーをウィンドウから切り離す
            if (_window is { } window)
            {
                window.Content = null;
                window.Hide();
            }

            _shown = false;
        }
    }

    /// <summary>バーを以前の親から外す（1 つの要素は 1 つの親しか持てない）。</summary>
    /// <param name="view">バーの画面要素。</param>
    private static void DetachFromParent(FrameworkElement view)
    {
        if (view.Parent is Border border && ReferenceEquals(border.Child, view))
        {
            border.Child = null;
        }
        else if (view.Parent is Panel panel)
        {
            panel.Remove(view);
        }
    }

    /// <summary>バーを載せるウィンドウを用意する。</summary>
    /// <returns>枠なしのウィンドウ。</returns>
    private Window EnsureWindow()
        => _window ??= new Window
        {
            Borderless = true,
            BorderThickness = 0,
            Title = "書式バー",
        };

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

    /// <summary>MewUI の矩形を本層の矩形へ写す。</summary>
    /// <param name="rect">MewUI の矩形。</param>
    /// <returns>本層の矩形。</returns>
    private static EsRect ToEsRect(Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
