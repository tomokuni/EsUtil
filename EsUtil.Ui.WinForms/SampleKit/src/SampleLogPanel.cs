namespace EsUtil.Ui.WinForms.SampleKit;

/// <summary>ログを表示するパネル（領域全体へ広がり、最新の行が見える位置までスクロールする）。</summary>
/// <remarks>
/// <b>非同期の表示</b>: バッファ（<see cref="SampleLogBuffer"/>）から一定間隔でまとめて取り出して表示する。
/// ログの書き込み側（コントロール・アプリ）を待たせない。<br/>
/// <b>追従</b>: パネルの大きさは親のレイアウトに従う（文字の折り返しと縦スクロールで全体を使う）。
/// </remarks>
public sealed class SampleLogPanel : TableLayoutPanel
{
    /// <summary>画面へ出す行の上限（古い行から捨てる）。</summary>
    private const int MaxDisplayedLines = 500;

    /// <summary>表示の更新間隔（ミリ秒）。</summary>
    private const int RefreshInterval = 200;

    /// <summary>本文の文字サイズ（pt。 11 DIP = 8.25 pt。 他 UI のログと同じ大きさにする）。</summary>
    private const float LogFontSizePoint = 8.25f;

    /// <summary>表示する行（末尾が最新）。</summary>
    private readonly List<string> _displayed = [];

    /// <summary>書き込み側のバッファ。</summary>
    private readonly SampleLogBuffer _buffer;

    /// <summary>ログの本文。</summary>
    private readonly TextBox _text = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        WordWrap = true,
        Dock = DockStyle.Fill,
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

        Controls.Add(new Label { Text = "ログ", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Dock = DockStyle.Fill }, 0, 0);
        Controls.Add(_text, 0, 1);

        _timer.Tick += (_, _) => RefreshFromBuffer();
        _timer.Start();
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

        _text.Text = string.Join(Environment.NewLine, _displayed);

        // 最新の行が見える位置までスクロールする
        _text.SelectionStart = _text.TextLength;
        _text.ScrollToCaret();
    }
}
