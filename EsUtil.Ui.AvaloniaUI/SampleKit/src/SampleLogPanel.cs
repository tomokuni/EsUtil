using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace EsUtil.Ui.AvaloniaUI.SampleKit;

/// <summary>ログを表示するパネル（領域全体へ広がり、最新の行が見える位置までスクロールする）。</summary>
/// <remarks>
/// <b>非同期の表示</b>: バッファ（<see cref="SampleLogBuffer"/>）から一定間隔でまとめて取り出して表示する。
/// ログの書き込み側（コントロール・アプリ）を待たせない。<br/>
/// <b>一覧で表示する理由</b>: 1 つの折り返しテキストへ全行を入れると、行数に比例して 1 フレームの費用が増える。
/// 一覧（<see cref="ListBox"/>）は見えている行だけを実体化するため、行数が増えても描画の費用は変わらない。<br/>
/// <b>選択とコピー</b>: 行を選んで（複数可）見出しの「コピー」を押すと、選んだ行をクリップボードへ写す
/// （選んでいないときは表示中のすべての行）。
/// </remarks>
public sealed class SampleLogPanel : DockPanel
{
    /// <summary>画面へ出す行の上限（古い行から捨てる）。</summary>
    /// <remarks>
    /// 画面に出せる行数より十分多い値にする（上限を超えた行は捨て、見えている内容とコピーの対象を一致させる）。
    /// </remarks>
    private const int MaxDisplayedLines = 150;

    /// <summary>表示の更新間隔。</summary>
    private static readonly TimeSpan s_refreshInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>表示する行（末尾が最新）。</summary>
    private readonly List<string> _displayed = [];

    /// <summary>書き込み側のバッファ。</summary>
    private readonly SampleLogBuffer _buffer;

    /// <summary>行を表示する一覧（見えている行だけを実体化する）。</summary>
    private readonly ListBox _list = new() { FontSize = 11, SelectionMode = SelectionMode.Multiple };

    /// <summary>バッファを取り出すための一時領域。</summary>
    private readonly List<string> _pending = [];

    /// <summary>ログ パネルを初期化する。</summary>
    /// <param name="buffer">表示するバッファ。</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> が null の場合。</exception>
    public SampleLogPanel(SampleLogBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;

        var copy = new Button { Content = "コピー", FontSize = 11 };
        copy.Click += (_, _) => _ = CopySelectionOrAllAsync();

        var header = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 0, 0, 6),
            Children =
            {
                new TextBlock { Text = "ログ", FontWeight = Avalonia.Media.FontWeight.Bold, FontSize = 12 },
                copy,
            },
        };
        DockPanel.SetDock(header, Dock.Top);
        Children.Add(header);

        Children.Add(_list);

        var timer = new DispatcherTimer { Interval = s_refreshInterval };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
    }

    /// <summary>選んだ行（無ければ表示中のすべての行）をクリップボードへ写す。</summary>
    /// <returns>完了を待つためのタスク。</returns>
    /// <remarks>表示の対象は画面に出ている行だけにする（上限で捨てた行は含めない）。 クリップボードが無い環境では何もしない。</remarks>
    private async Task CopySelectionOrAllAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        var selected = _list.SelectedItems?.OfType<string>().ToList() ?? [];
        var lines = selected.Count > 0 ? selected : _displayed;
        await clipboard.SetTextAsync(string.Join(Environment.NewLine, lines)).ConfigureAwait(true);
    }

    /// <summary>バッファから行を取り出して表示を更新する。</summary>
    private void Refresh()
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

        // 一覧へ入れ直して最新の行までスクロールする（見えている行だけが実体化される）
        _list.ItemsSource = _displayed.ToArray();
        if (_displayed.Count > 0)
        {
            _list.ScrollIntoView(_displayed.Count - 1);
        }
    }
}