namespace EsUtil.Ui.WinForms.SampleKit;

/// <summary>ログを表示するパネル（領域全体へ広がり、最新の行が見える位置までスクロールする）。</summary>
/// <remarks>
/// <b>非同期の表示</b>: バッファ（<see cref="SampleLogBuffer"/>）から一定間隔でまとめて取り出して表示する。
/// ログの書き込み側（コントロール・アプリ）を待たせない。<br/>
/// <b>一覧で表示する理由</b>: 1 つの複数行テキストへ全行を入れると、行数に比例して描画の費用が増える。
/// 一覧（<see cref="ListBox"/>）は見えている行だけを描くため、行数が増えても描画の費用は変わらない。<br/>
/// <b>選択とコピー</b>: 行を選んで（複数可）見出しの「コピー」を押すと、選んだ行をクリップボードへ写す
/// （選んでいないときは表示中のすべての行）。
/// </remarks>
public sealed class SampleLogPanel : TableLayoutPanel
{
    /// <summary>画面へ出す行の上限（古い行から捨てる）。</summary>
    /// <remarks>画面に出せる行数より十分多い値にする（上限を超えた行は捨て、見えている内容とコピーの対象を一致させる）。</remarks>
    private const int MaxDisplayedLines = 150;

    /// <summary>表示の更新間隔（ミリ秒）。</summary>
    private const int RefreshInterval = 200;

    /// <summary>本文の文字サイズ（pt。 12 DIP = 9 pt。 設定パネルの文字サイズと同じ大きさにする）。</summary>
    private const float LogFontSizePoint = 9f;

    /// <summary>表示する行（末尾が最新）。</summary>
    private readonly List<string> _displayed = [];

    /// <summary>書き込み側のバッファ。</summary>
    private readonly SampleLogBuffer _buffer;

    /// <summary>行を表示する一覧（見えている行だけを描く）。</summary>
    private readonly ListBox _list = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        SelectionMode = SelectionMode.MultiExtended,
        Font = new Font(FontFamily.GenericMonospace, LogFontSizePoint),
        BackColor = SystemColors.Window,
    };

    /// <summary>バッファから取り出すタイマー。</summary>
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = RefreshInterval };

    /// <summary>バッファを取り出すための一時領域。</summary>
    private readonly List<string> _pending = [];

    /// <summary>ログ パネルを初期化する。</summary>
    /// <param name="buffer">表示するバッファ。</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> が null の場合。</exception>
    public SampleLogPanel(SampleLogBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;

        Dock = DockStyle.Fill;
        ColumnCount = 1;
        RowCount = 2;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var copy = new Button { Text = "コピー", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        copy.Click += (_, _) => CopySelectionOrAll();

        var header = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        header.Controls.Add(new Label { Text = "ログ", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        header.Controls.Add(copy);
        Controls.Add(header, 0, 0);
        Controls.Add(_list, 0, 1);

        _timer.Tick += (_, _) => RefreshFromBuffer();
        _timer.Start();
    }

    /// <summary>選んだ行（無ければ表示中のすべての行）をクリップボードへ写す。</summary>
    /// <remarks>表示の対象は画面に出ている行だけにする（上限で捨てた行は含めない）。</remarks>
    private void CopySelectionOrAll()
    {
        var lines = _list.SelectedItems.OfType<string>().ToList();
        if (lines.Count == 0)
        {
            lines = _displayed;
        }

        if (lines.Count > 0)
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }
    }

    /// <summary>保持している資源を破棄する。</summary>
    /// <param name="disposing">マネージド資源を破棄する場合は true。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>バッファから行を取り出して表示を更新する。</summary>
    private void RefreshFromBuffer()
    {
        _pending.Clear();
        if (_buffer.Drain(_pending) == 0)
        {
            return;
        }

        _displayed.AddRange(_pending);
        if (_displayed.Count > MaxDisplayedLines)
        {
            _displayed.RemoveRange(0, _displayed.Count - MaxDisplayedLines);
        }

        // 一覧へ入れ直して最新の行までスクロールする（見えている行だけが描かれる）
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            _list.Items.AddRange([.. _displayed]);
            if (_list.Items.Count > 0)
            {
                _list.TopIndex = _list.Items.Count - 1;
            }
        }
        finally
        {
            _list.EndUpdate();
        }
    }
}