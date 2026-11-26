using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.StyledText;

/// <summary>書式バーを選択範囲の近くへポップアップで出す（<see cref="TextFormatBarHosting.Popup"/>）。</summary>
/// <remarks>
/// <b>位置の規則は提示層が単一所有する</b>: 選択の上・下の判断と置ける領域への収めは
/// <see cref="FloatingPanelPlacement"/> が決める（4 つの UI 版で同じ結果になる）。<br/>
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
    /// <summary>パネルの外観（枠・余白・高さ）。</summary>
    private static readonly FloatingPanelAppearance s_appearance = FloatingPanelAppearance.FormatBar;

    /// <summary>バーの入れ物（ウィンドウの中身。 1 度だけ載せる）。 枠と余白は外観が決める。</summary>
    private readonly Panel _root = new()
    {
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(
            s_appearance.Background.A, s_appearance.Background.R,
            s_appearance.Background.G, s_appearance.Background.B),
        Padding = new Padding(
            (int)Math.Round(s_appearance.Padding), (int)Math.Round(s_appearance.VerticalPadding),
            (int)Math.Round(s_appearance.Padding), (int)Math.Round(s_appearance.VerticalPadding)),
    };

    /// <summary>バーを載せる枠なしのウィンドウ（初回の表示で作る）。</summary>
    private Form? _window;

    /// <summary>利用者が動かした後の左上（このコントロールの座標の DIP。 まだ動かしていない場合は <see langword="null"/>）。</summary>
    private EsPoint? _manualPosition;

    /// <summary>ドラッグ開始時のバーの左上（このコントロールの座標の DIP）。</summary>
    private EsPoint _dragOrigin;

    /// <summary>ドラッグ開始時のポインターの位置（このコントロールの座標の DIP）。</summary>
    private EsPoint _dragOriginCursor;

    /// <summary>ドラッグ中かどうか。</summary>
    private bool _dragging;

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

        // **大きさは外観が単一所有する**: 中身に合わせて広げると、枠と余白の分だけ UI ごとに
        // 高さが変わる（実測。 他の UI 版は外観の値で固定している）
        var panelSize = s_appearance.ResolvePanelSize(MeasureBar(bar));
        window.ClientSize = new Size(
            (int)Math.Round(panelSize.Width), (int)Math.Round(panelSize.Height));

        var origin = owner.PointToScreen(Point.Empty);
        var available = ResolveAvailableArea(origin);

        // **動かした位置を選択より優先する**: 選択が変わるたびに元の位置へ戻すと、動かした意味が無くなる
        var position = _manualPosition is { } manual
            ? FloatingPanelPlacement.Clamp(manual, panelSize.Width, panelSize.Height, available)
            : FloatingPanelPlacement.ResolveOutside(
                ToEsRect(owner.SelectionBounds), panelSize, available, FloatingPanelPlacement.FormatBarGap);

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

        // **動かした位置は捨てる**: 次に出したときは選択の位置から出す
        _manualPosition = null;
        _dragging = false;

        if (_window is { Visible: true } window)
        {
            window.Hide();
        }
    }

    /// <summary>バーの余白を掴んで動かし始める。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">マウスの引数。</param>
    private void OnBarMouseDown(object? sender, MouseEventArgs e)
    {
        _ = sender;
        if (_window is not { } window || e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = true;
        _dragOrigin = _manualPosition ?? ToLocal(window.Location);
        _dragOriginCursor = ToLocal(Cursor.Position);
    }

    /// <summary>バーをドラッグして動かす。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">マウスの引数。</param>
    private void OnBarMouseMove(object? sender, MouseEventArgs e)
    {
        _ = sender;
        if (!_dragging || _window is not { } window || e.Button != MouseButtons.Left)
        {
            return;
        }

        var moved = FloatingPanelPlacement.ResolveDrag(_dragOrigin, _dragOriginCursor, ToLocal(Cursor.Position));
        var panelSize = s_appearance.ResolvePanelSize(MeasureBar(owner.FormatBar));
        var origin = owner.PointToScreen(Point.Empty);
        _manualPosition = FloatingPanelPlacement.Clamp(
            moved, panelSize.Width, panelSize.Height, ResolveAvailableArea(origin));
        window.Location = new Point(
            origin.X + (int)Math.Round(_manualPosition.Value.X),
            origin.Y + (int)Math.Round(_manualPosition.Value.Y));
    }

    /// <summary>バーのドラッグを終える。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">マウスの引数。</param>
    private void OnBarMouseUp(object? sender, MouseEventArgs e)
    {
        _ = sender;
        _ = e;
        _dragging = false;
    }

    /// <summary>画面の座標をこのコントロールの座標（DIP）へ写す。</summary>
    /// <param name="screen">画面の座標（ピクセル）。</param>
    /// <returns>このコントロールの座標。</returns>
    private EsPoint ToLocal(Point screen)
    {
        var local = owner.PointToClient(screen);
        return new EsPoint(local.X, local.Y);
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

        // **余白の内側へ収める**: WinForms の余白は Dock した子にだけ効くため、Fill で詰める
        // （左上へ置くと余白の上へ重なり、掴む場所が無くなる）
        bar.View.Dock = DockStyle.Fill;
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
            TopMost = false,
            Padding = Padding.Empty,
        };
        // **枠を描く**: WinForms の Panel は枠の色を持たないため、外観の枠を自分で描く
        _root.Paint += (_, e) =>
        {
            var border = s_appearance.Border;
            using var pen = new Pen(
                Color.FromArgb(border.A, border.R, border.G, border.B),
                (float)Math.Max(1, s_appearance.BorderThickness));
            var half = pen.Width / 2;
            e.Graphics.DrawRectangle(
                pen, half, half,
                Math.Max(0, _root.ClientSize.Width - pen.Width),
                Math.Max(0, _root.ClientSize.Height - pen.Width));
        };
        _root.Cursor = Cursors.SizeAll;

        _window.Controls.Add(_root);

        // **余白を掴んで動かせるようにする**: 中身（ボタン・入力欄）は押下を処理済みにするため、
        // 本ハンドラーは余白を掴んだときだけ呼ばれる
        _root.MouseDown += OnBarMouseDown;
        _root.MouseMove += OnBarMouseMove;
        _root.MouseUp += OnBarMouseUp;
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
