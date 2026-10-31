using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace EsUtil.Ui.MewUI.SampleKit;

/// <summary>ログを表示するパネル（領域全体へ広がり、最新の行が見える位置までスクロールする）。</summary>
/// <remarks>
/// <b>非同期の表示</b>: バッファ（<see cref="SampleLogBuffer"/>）から一定間隔でまとめて取り出して表示する。
/// ログの書き込み側（コントロール・アプリ）を待たせない。<br/>
/// <b>追従</b>: パネルの大きさは親のレイアウトに従う（文字の折り返しと縦スクロールで全体を使う）。
/// </remarks>
public sealed class SampleLogPanel : StackPanel
{
    /// <summary>画面へ出す行の上限（古い行から捨てる）。</summary>
    private const int MaxDisplayedLines = 500;

    /// <summary>表示の更新間隔。</summary>
    private static readonly TimeSpan s_refreshInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>表示する行（末尾が最新）。</summary>
    private readonly List<string> _displayed = [];

    /// <summary>書き込み側のバッファ。</summary>
    private readonly SampleLogBuffer _buffer;

    /// <summary>ログの本文。</summary>
    private readonly TextBlock _text = new();

    /// <summary>本文のスクロール。</summary>
    private readonly ScrollViewer _scroll = new();

    /// <summary>バッファから取り出すタイマー。</summary>
    private readonly DispatcherTimer _timer = new() { Interval = s_refreshInterval };

    /// <summary>バッファを取り出すための一時領域。</summary>
    private readonly List<string> _pending = [];

    /// <summary>末尾へ寄せる要求が残っているかどうか（レイアウト確定後の次の間隔で実行する）。</summary>
    private bool _scrollPending;

    /// <summary>ログ パネルを初期化する。</summary>
    /// <param name="buffer">表示するバッファ。</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> が null の場合。</exception>
    public SampleLogPanel(SampleLogBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;

        Orientation = Orientation.Vertical;
        Spacing = 6;
        _text.FontSize(11);
        _text.TextWrapping(TextWrapping.Wrap);
        _scroll.Content(_text);

        Add(new Label().Text("ログ").Bold().FontSize(12));
        Add(_scroll);

        _timer.Tick += Refresh;
        _timer.Start();
    }

    /// <summary>バッファから行を取り出して表示を更新する。</summary>
    private void Refresh()
    {
        // 前回の更新で確定したレイアウトを使って末尾へ寄せる
        // （文字を差し替えた直後は拡張後の高さがまだスクロール領域へ反映されていない）
        if (_scrollPending)
        {
            _scroll.ScrollBy(double.MaxValue);
            _scrollPending = false;
        }

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

        // 最新の行が見える位置までスクロールする（今すぐ寄せ、次の間隔でもう一度寄せる）
        _scroll.ScrollBy(double.MaxValue);
        _scrollPending = true;
    }
}
