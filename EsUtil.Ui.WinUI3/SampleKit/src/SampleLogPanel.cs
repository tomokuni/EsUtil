using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EsUtil.Ui.WinUI3.SampleKit;

/// <summary>ログを表示するパネル（領域全体へ広がり、最新の行が見える位置までスクロールする）。</summary>
/// <remarks>
/// <b>非同期の表示</b>: バッファ（<see cref="SampleLogBuffer"/>）から一定間隔でまとめて取り出して表示する。
/// ログの書き込み側（コントロール・アプリ）を待たせない。<br/>
/// <b>一覧で表示する理由</b>: 1 つの折り返しテキストへ全行を入れると、行数に比例して描画の費用が増える。
/// 一覧（<see cref="ListView"/>）は見えている行だけを実体化するため、行数が増えても描画の費用は変わらない。<br/>
/// <b>選択とコピー</b>: 行を選んで（複数可）見出しの「コピー」を押すと、選んだ行をクリップボードへ写す
/// （選んでいないときは表示中のすべての行）。
/// </remarks>
public sealed class SampleLogPanel : Grid
{
    /// <summary>画面へ出す行の上限（古い行から捨てる）。</summary>
    private const int MaxDisplayedLines = 150;

    /// <summary>表示の更新間隔。</summary>
    private static readonly TimeSpan s_refreshInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>表示する行（末尾が最新）。</summary>
    private readonly List<string> _displayed = [];

    /// <summary>書き込み側のバッファ。</summary>
    private readonly SampleLogBuffer _buffer;

    /// <summary>行を表示する一覧（見えている行だけを実体化する）。</summary>
    // 文字サイズは設定パネル（SampleSettings.FontSize）へ揃える
    private readonly ListView _list = new()
    {
        FontSize = SampleSettings.FontSize,
        SelectionMode = ListViewSelectionMode.Extended,
    };

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

        // 行の高さは文字が切れない最小の高さにする（テーマの既定は上下の余白が大きく、1 画面に入る行数が減る）
        var itemStyle = new Style(typeof(ListViewItem));
        itemStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, SampleSettings.FontSize + 8));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 0, 4, 0)));
        _list.ItemContainerStyle = itemStyle;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var copy = new Button { Content = "コピー", FontSize = SampleSettings.FontSize };
        copy.Click += (_, _) => CopySelectionOrAll();

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 6),
            Children =
            {
                new TextBlock
                {
                    Text = "ログ",
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    FontSize = 12,
                },
                copy,
            },
        };
        Children.Add(header);

        SetRow(_list, 1);
        Children.Add(_list);

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

        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(string.Join(Environment.NewLine, lines));
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
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

        // 一覧へ入れ直して最新の行までスクロールする（見えている行だけが実体化される）。
        // 項目は直接追加する（ItemsSource への代入は Native AOT の制約に反する。 ビルドの DEP 検査が強制する）
        _list.Items.Clear();
        foreach (var line in _displayed)
        {
            _list.Items.Add(line);
        }

        if (_displayed.Count > 0)
        {
            _list.ScrollIntoView(_displayed[^1]);
        }
    }
}
