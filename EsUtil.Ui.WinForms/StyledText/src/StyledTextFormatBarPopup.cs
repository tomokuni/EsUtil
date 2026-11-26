using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置の規則は提示層が単一所有する</b>: 選択の上・下の判断と置ける領域への収めは
/// <see cref="TextFormatBarPlacement"/> が決める（4 つの UI 版で同じ結果になる）。<br/>
/// <b>出す条件は本文コントロールが単一所有する</b>: ホストがバーを置く場合と同じ判断
/// （<see cref="StyledTextControl.ShouldShowFormatBar"/>）を使う。<br/>
/// <b>枠なしのウィンドウへ載せる</b>: ドロップダウンへ載せると、その中から開く色の選択が閉じてしまう。
/// 枠なしのウィンドウなら、中から開く一覧は普通のウィンドウの子になり、そのまま使える。
/// </remarks>
/// <param name="owner">書式バーを所有する本文コントロール。</param>
internal sealed class StyledTextFormatBarPopup(StyledTextControl owner)
{
    /// <summary>バーを載せる枠なしのウィンドウ（初回の表示で作る）。</summary>
    private Form? _window;

    /// <summary>バーをポップアップで表示中かどうかを取得する。</summary>
    public bool IsOpen => _window is { Visible: true };

    /// <summary>いまの状態に合わせて、バーを出し入れし、位置を合わせる。</summary>
    public void Update()
    {
        if (!owner.ShouldShowFormatBar || !owner.IsHandleCreated)
        {
            Close();
            return;
        }

        var bar = owner.FormatBar;
        bar.Refresh();

        var window = EnsureWindow();

        // **バーは作り直される**: 項目や構成が変わっても出ているバーが古くならないよう、毎回付け直す
        if (!ReferenceEquals(window.Controls.Count > 0 ? window.Controls[0] : null, bar.View))
        {
            window.SuspendLayout();
            window.Controls.Clear();
            window.Controls.Add(bar.View);
            window.ResumeLayout();
        }

        var barSize = MeasureBar(bar);
        var origin = owner.PointToScreen(Point.Empty);
        var position = TextFormatBarPlacement.ResolvePopupPosition(
            ToEsRect(owner.SelectionBounds),
            barSize,
            ResolveAvailableArea(origin),
            TextFormatBarPlacement.Gap);

        window.Location = new Point(
            origin.X + (int)Math.Round(position.X),
            origin.Y + (int)Math.Round(position.Y));

        if (!window.Visible)
        {
            window.Show();
            window.Location = new Point(
                origin.X + (int)Math.Round(position.X),
                origin.Y + (int)Math.Round(position.Y));
        }
    }

    /// <summary>バーを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        if (_window is not { } window)
        {
            return;
        }

        // **中身を外す**: ホストが埋め込みへ戻せるよう、バーをウィンドウから切り離す
        if (window.Controls.Count > 0)
        {
            window.Controls.Clear();
        }

        if (window.Visible)
        {
            window.Hide();
        }
    }

    /// <summary>バーを載せるウィンドウを用意する。</summary>
    /// <returns>枠なしのウィンドウ。</returns>
    private Form EnsureWindow()
    {
        if (_window is { } existing)
        {
            return existing;
        }

        _window = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            TopMost = false,
            Padding = Padding.Empty,
        };
        return _window;
    }

    /// <summary>バーを置ける領域（このコントロールの座標の DIP）を求める。</summary>
    /// <param name="origin">このコントロールの左上（画面のピクセル）。</param>
    /// <returns>画面の作業領域をこのコントロールの座標へ写した領域。</returns>
    private EsRect ResolveAvailableArea(Point origin)
    {
        var area = Screen.FromControl(owner).WorkingArea;
        return new EsRect(
            area.X - origin.X,
            area.Y - origin.Y,
            Math.Max(1, area.Width),
            Math.Max(1, area.Height));
    }

    /// <summary>バーの実寸（DIP）を測る。</summary>
    /// <param name="bar">書式バー。</param>
    /// <returns>実寸。 測れない場合は希望寸法。</returns>
    private static EsSize MeasureBar(IStyledTextFormatBar bar)
    {
        var preferred = bar.View.GetPreferredSize(Size.Empty);
        var width = preferred.Width > 0 ? preferred.Width : bar.PreferredWidth;
        var height = preferred.Height > 0 ? preferred.Height : TextFormatBarLayout.ItemHeight;
        return new EsSize(Math.Max(1, width), Math.Max(1, height));
    }

    /// <summary>WinForms の矩形を本層の矩形へ写す。</summary>
    /// <param name="rect">WinForms の矩形。</param>
    /// <returns>本層の矩形。</returns>
    private static EsRect ToEsRect(RectangleF rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
