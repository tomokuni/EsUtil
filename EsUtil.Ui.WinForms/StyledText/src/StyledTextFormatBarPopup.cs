using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置の規則は提示層が単一所有する</b>: 選択の上・下の判断と置ける領域への収めは
/// <see cref="TextFormatBarPlacement"/> が決める（4 つの UI 版で同じ結果になる）。<br/>
/// <b>出す条件は本文コントロールが単一所有する</b>: ホストがバーを置く場合と同じ判断
/// （<see cref="StyledTextControl.ShouldShowFormatBar"/>）を使う。<br/>
/// <b>ウィンドウの中身は 1 度だけ決める</b>: バーは項目の変更で作り直されるため、そのたびに
/// ウィンドウの中身を差し替えると要素の付け替えが重なって「既に親がある」で落ちる。 中身は変わらない根
/// （<see cref="Panel"/>）を 1 度だけ載せ、根の子だけを差し替える。<br/>
/// <b>枠なしのウィンドウへ載せる</b>: ドロップダウンへ載せると、その中から開く色の選択が閉じてしまう。
/// 枠なしのウィンドウなら、中から開く一覧は普通のウィンドウの子になり、そのまま使える。
/// </remarks>
/// <param name="owner">書式バーを所有する本文コントロール。</param>
internal sealed class StyledTextFormatBarPopup(StyledTextControl owner)
{
    /// <summary>バーの入れ物（ウィンドウの中身。 1 度だけ載せる）。</summary>
    private readonly Panel _root = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
    };

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
        SetBar(bar);

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
        // **中身を先に外す**: 隠したウィンドウに要素を残すと、次に載せるときに「既に親がある」で落ちる
        _root.Controls.Clear();

        if (_window is { Visible: true } window)
        {
            window.Hide();
        }
    }

    /// <summary>根へバーを載せる（既に載っていれば何もしない）。</summary>
    /// <param name="bar">書式バー。</param>
    private void SetBar(IStyledTextFormatBar bar)
    {
        if (_root.Controls.Count == 1 && ReferenceEquals(_root.Controls[0], bar.View))
        {
            return;
        }

        // **1 つの要素は 1 つの親しか持てない**: ホストのレイアウトに残っていれば外してから載せる
        DetachFromParent(bar.View);
        _root.Controls.Clear();
        _root.Controls.Add(bar.View);
    }

    /// <summary>バーを以前の親から外す（1 つの要素は 1 つの親しか持てない）。</summary>
    /// <param name="view">バーの画面要素。</param>
    private static void DetachFromParent(Control view)
    {
        if (view.Parent is Panel panel && panel.Controls.Contains(view))
        {
            panel.Controls.Remove(view);
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
        _window.Controls.Add(_root);
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
