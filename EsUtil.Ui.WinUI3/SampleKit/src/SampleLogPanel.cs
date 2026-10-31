using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EsUtil.Ui.WinUI3.SampleKit;

/// <summary>ログを表示するパネル（領域全体へ広がり、最新の行が見える位置までスクロールする）。</summary>
/// <remarks>
/// <b>非同期の表示</b>: バッファ（<see cref="SampleLogBuffer"/>）から一定間隔でまとめて取り出して表示する。
/// ログの書き込み側（コントロール・アプリ）を待たせない。<br/>
/// <b>追従</b>: パネルの大きさは親のレイアウトに従う（文字の折り返しと縦スクロールで全体を使う）。
/// </remarks>
public sealed class SampleLogPanel : Grid
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
    private readonly TextBlock _text = new()
    {
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        IsTextSelectionEnabled = true,
    };

    /// <summary>本文のスクロール。</summary>
    private readonly ScrollViewer _scroll = new();

    /// <summary>バッファから取り出すタイマー。</summary>
    private readonly DispatcherTimer _timer = new() { Interval = s_refreshInterval };

    /// <summary>バッファを取り出すための一時領域。</summary>
    private readonly List<string> _pending = [];

    /// <summary>ログ パネルを初期化する。</summary>
    /// <param name="buffer">表示するバッファ。</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> が null の場合。</exception>
    public SampleLogPanel(SampleLogBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new TextBlock
        {
            Text = "ログ",
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 6),
        };
        Children.Add(header);

        _scroll.Content = _text;
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        SetRow(_scroll, 1);
        Children.Add(_scroll);

        _timer.Tick += (_, _) => RefreshFromBuffer();
        _timer.Start();
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

        // 最新の行が見える位置までスクロールする（範囲は ChangeView が現在の拡張へ丸める）
        _scroll.ChangeView(null, double.MaxValue, null);
    }
}
