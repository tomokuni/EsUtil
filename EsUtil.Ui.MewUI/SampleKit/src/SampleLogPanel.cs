using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace EsUtil.Ui.MewUI.SampleKit;

/// <summary>ログを表示するパネル（領域全体へ広がり、最新の行が見える位置までスクロールする）。</summary>
/// <remarks>
/// <b>非同期の表示</b>: バッファ（<see cref="SampleLogBuffer"/>）から一定間隔でまとめて取り出して表示する。
/// ログの書き込み側（コントロール・アプリ）を待たせない。<br/>
/// <b>仮想化する理由</b>: 1 つの折り返しテキストへ全行を入れると、MewUI は描画のたびに全行を描き直すため、
/// 500 行で 1 フレーム約 125 ms かかり、ルーペの追従が止まる（実測 2026-10-31）。
/// 一覧（<see cref="ListBox"/>）は見えている行だけを実体化するため、行数が増えても描画の費用は変わらない。<br/>
/// <b>選択とコピー</b>: 行を選んで（複数可）Ctrl+C を押すと、選んだ行をクリップボードへ写す
/// （選んでいないときは表示中のすべての行）。
/// </remarks>
public sealed class SampleLogPanel : Grid
{
    /// <summary>画面へ出す行の上限（古い行から捨てる）。</summary>
    /// <remarks>
    /// <b>上限は 150 行にする</b>: 一覧は見えている行だけを実体化するが、末尾へ寄せる要求（<c>ScrollIntoView</c>）は
    /// 途中の行も実体化するため、200 行を超えると 1 フレームの費用が 16 ms を超える（実測 2026-10-31: 200 行は 1.5 ms、
    /// 250 行で 15 ms 超）。 画面に出せる行数より十分多いため、実用上の不足は無い。
    /// </remarks>
    private const int MaxDisplayedLines = 150;

    /// <summary>表示の更新間隔。</summary>
    private static readonly TimeSpan s_refreshInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>書き込み側のバッファ。</summary>
    private readonly SampleLogBuffer _buffer;

    /// <summary>表示する行（末尾が最新）。</summary>
    private readonly List<string> _displayed = [];

    /// <summary>行を表示する一覧（見えている行だけを実体化する）。</summary>
    private readonly ListBox _list = new();

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

        // 一覧へ高さの上限を与える（縦に伸びる入れ物へ入れると、無限の高さで計測されて全行が実体化される。
        // 見えている行だけを描くには、スクロールする領域の高さが確定している必要がある。 実測 2026-10-31）
        this.Columns("*");
        this.Rows("Auto,*");

        // 文字サイズは設定パネル（SampleSettings.FontSize）へ揃える
        _list.FontSize(SampleSettings.FontSize);

        // 行の高さは文字が切れない最小の高さにする（既定の行高は余白が大きく、1 画面に入る行数が減る）
        _list.ItemHeight(SampleSettings.FontSize + 8);
        _list.SelectionMode = ItemsSelectionMode.Multiple;

        // 選択した行は Ctrl+C でクリップボードへ写す（コピーのボタンは置かない）
        _list.OnKeyDown(e =>
        {
            if (e.Key == Key.C && e.ControlKey)
            {
                CopySelection();
                e.Handled = true;
            }
        });

        var header = CreateHeader();
        header.Margin = new Thickness(0, 0, 0, 6);
        SetRow(header, 0);
        SetRow(_list, 1);
        Add(header);
        Add(_list);

        _timer.Tick += Refresh;
        _timer.Start();
    }

    /// <summary>見出し（「ログ」と操作の説明）を作る。</summary>
    /// <returns>見出しの行。</returns>
    private static StackPanel CreateHeader()
        => new StackPanel().Horizontal().Spacing(8).Children(
            new Label().Text("ログ").Bold().FontSize(12),
            new TextBlock()
                .Text("行を選んで Ctrl+C でコピー")
                .FontSize(SampleSettings.FontSize)
                .VerticalAlignment(VerticalAlignment.Center));

    /// <summary>選んだ行をクリップボードへ写す。</summary>
    /// <remarks>
    /// 表示の対象は画面に出ている行だけにする（上限で捨てた行は含めない。 見えている内容と一致させる）。<br/>
    /// 選んでいないときは何もしない（全行を写すと、見えていない行まで意図せず運ぶ）。<br/>
    /// クリップボードが使えない環境（サービスが無い）では何もしない。
    /// </remarks>
    private void CopySelection()
    {
        if (!Application.IsRunning || Application.Current.PlatformServices.Clipboard is not { } clipboard)
        {
            return;
        }

        var selected = _list.SelectedItems.OfType<string>().ToList();
        if (selected.Count > 0)
        {
            clipboard.TrySetText(string.Join(Environment.NewLine, selected));
        }
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
        _list.Items(_displayed, static line => line);
        _list.ScrollIntoView(_displayed.Count - 1);
    }
}