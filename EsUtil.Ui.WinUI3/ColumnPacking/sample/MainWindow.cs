using System.Collections.ObjectModel;
using System.Globalization;
using EsUtil.Ui.WinUI3.SampleKit;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace EsUtil.Ui.WinUI3.ColumnPacking.Sample;

/// <summary>サンプルのメイン ウィンドウ（<see cref="ColumnPackingPanel"/> の使い方を示す）。</summary>
/// <remarks>
/// <b>示すこと</b>: <see cref="DualSourceItemsPanel.SetItems"/>（データ）と直接の子（コードで足した要素）を
/// 同時に並べられること、<see cref="ColumnPackingPanel.ColumnLimit"/> と余白で配置が変わること、
/// レイアウト結果（<see cref="ColumnPackingPanel.LayoutMinHeight"/> など）が依存関係プロパティとして読めること。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: パネル / 右下: ログ）を使う。
/// </remarks>
public sealed class MainWindow : Window
{
    /// <summary>項目を生成するときの名前の一覧。</summary>
    private static readonly string[] s_labels =
    [
        "丸括弧", "角括弧", "波括弧", "引用符", "カンマ", "ピリオド",
        "コロン", "セミコロン", "不等号", "イコール", "プラス", "マイナス",
        "はてな", "感嘆符", "シャープ", "ドル", "パーセント", "アンパサンド",
        "アスタリスク", "スラッシュ",
    ];

    /// <summary>ログ（Serilog + 非同期表示）。</summary>
    private readonly SampleLogging _logging = new();

    /// <summary>列を並べるパネル（レイアウト結果の表示付き）。</summary>
    private readonly ColumnPackingPanel _panel = new() { ColumnLimit = 10, RowSpace = 8, ColumnSpace = 8 };

    /// <summary>レイアウト結果の表示。</summary>
    private readonly TextBlock _status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Text = "待機中..." };

    /// <summary>並べる項目（<see cref="DualSourceItemsPanel.SetItems"/> へ渡すデータ）。</summary>
    private readonly ObservableCollection<SampleItem> _items = [];

    /// <summary>並べる項目の数。</summary>
    private int _itemCount = 20;

    /// <summary>メイン ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        // パネルのログをログ領域へ流す（プロパティ変更は Information、レイアウト結果の変化は Debug）
        _panel.Log = logger;
        _panel.LogPrefix = "[ColumnPacking]";

        // 順序が重要: ItemTemplate を先に設定する（この時点の子は再構築で消えるため、直接の子はその後に足す）。
        // その後に直接の子を足し、SetItems でデータを渡す（初回の再構築で「直接の子」として保存される）
        _panel.ItemTemplate = (DataTemplate)Application.Current.Resources["SampleItemTemplate"];
        _panel.Children.Add(CreateDirectChild("直接の子 A", 56));
        _panel.Children.Add(CreateDirectChild("直接の子 B", 72));

        RebuildItems();

        var contentArea = new ScrollViewer
        {
            Content = _panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Slider("列の上限", 10, 1, 16, 1, true, value => _panel.ColumnLimit = (int)value),
                SampleSettings.Slider("行間", 8, 0, 24, 1, true, value => _panel.RowSpace = value),
                SampleSettings.Slider("列間", 8, 0, 24, 1, true, value => _panel.ColumnSpace = value),
                SampleSettings.Choice("配置の方法", ["貪欲法", "動的計画法", "二分探索"], 2, index =>
                    _panel.Method = index switch
                    {
                        1 => EsUtil.Feature.ColumnPacking.ColumnPacker.Method.DynamicProgramming,
                        2 => EsUtil.Feature.ColumnPacking.ColumnPacker.Method.BinarySearch,
                        _ => EsUtil.Feature.ColumnPacking.ColumnPacker.Method.Greedy,
                    }),
                // データから作った子と直接の子（コードで足した要素）の並び順を切り替える
                SampleSettings.Check("直接の子を後ろに置く", true, value => _panel.ItemsSourceFirst = value),
                SampleSettings.Text("ログ接頭辞", "[ColumnPacking]", value => _panel.LogPrefix = value),
            },
        };

        // ── サンプル設定（サンプル固有の操作。 並びは他 UI と共通） ──
        var samples = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.LogLevel(_logging),
                SampleSettings.Number("アイテム数", _itemCount, 1, 60, 1, true, value =>
                {
                    _itemCount = (int)value;
                    RebuildItems();
                }),
                SampleSettings.Buttons(
                    ("レイアウト結果をログへ出力", () => LogLayout()),
                    ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
                _status,
            },
        };

        var log = new SampleLogPanel(_logging.Buffer);
        Title = "ColumnPacking パネル サンプル（WinUI 3）";
        // ── API操作（対象コントロールのメソッド実行） ──
        var methods = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Buttons(("項目を再設定（SetItems）", () => RebuildItems())),
            },
        };

        Content = SampleShell.CreateContent(properties, methods, samples, contentArea, log);

        // ウィンドウの大きさは DIP で指定する（他の UI と同じ 1400 x 900）
        SampleShell.ApplyWindowSize(this);

        _panel.SizeChanged += (_, _) => UpdateStatus();
        UpdateStatus();
        Closed += (_, _) => _logging.Dispose();

        return;

        // 項目を作り直す（データ側だけを差し替える。 直接の子は初回の SetItems で保存済み）
        void RebuildItems()
        {
            _items.Clear();
            foreach (var item in CreateItems(_itemCount))
            {
                _items.Add(item);
            }

            // 依存関係プロパティ（ItemsSource）を介さない入口へ渡す（AOT では ItemsSource に managed コレクションを渡せない。
            // ビルド設定規約 §6.1）
            _panel.SetItems(_items);

            UpdateStatus();
        }

        // レイアウト結果をログへ出力する（公開プロパティの値だけを 1 行で出す）
        void LogLayout()
        {
            if (_panel.LayoutMinHeight is not { } minHeight)
            {
                _logging.Logger.LogInformation("レイアウトはまだ確定していません。");
                return;
            }

            var usedWidth = _panel.LayoutUsedWidth ?? 0;
            var columns = _panel.LayoutColumnCount ?? 0;
            var segments = _panel.LayoutColumnSegments;
            var text = string.Create(
                CultureInfo.InvariantCulture,
                $"レイアウト結果: MinHeight={minHeight:F1} UsedWidth={usedWidth:F1} 列数={columns} ColumnSegments={segments}");
            _logging.Logger.Log(LogLevel.Information, default, text, null, static (state, _) => state);
        }

        // レイアウト結果を画面へ出す
        void UpdateStatus()
        {
            _status.Text = _panel.LayoutMinHeight is { } minHeight
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"最小の高さ: {minHeight:F1} / 使用幅: {_panel.LayoutUsedWidth:F1} / 列数: {_panel.LayoutColumnCount} / 列の区切り: {_panel.LayoutColumnSegments}")
                : "レイアウトはまだ確定していません。";
        }
    }

    /// <summary>指定した件数の項目を作る（高さは 40〜96 DIP で巡回させる）。</summary>
    /// <param name="count">作る件数。</param>
    /// <returns>作った項目。</returns>
    private static ObservableCollection<SampleItem> CreateItems(int count)
    {
        var items = new ObservableCollection<SampleItem>();
        for (var index = 0; index < count; index++)
        {
            var label = string.Create(
                CultureInfo.InvariantCulture,
                $"{s_labels[index % s_labels.Length]} {index + 1}");
            items.Add(new SampleItem(label, 40 + ((index % 8) * 8)));
        }

        return items;
    }

    /// <summary>項目と一緒に並べる直接の子を作る。</summary>
    /// <param name="text">表示する文言。</param>
    /// <param name="height">高さ（DIP）。</param>
    /// <returns>作った要素。</returns>
    /// <remarks>
    /// <b>文字色も固定する理由</b>: タイルの地色は明るい色で固定している。 文字色をテーマの既定（ダークでは白）に任せると、
    /// 明るい地色の上で読めなくなる（実測 2026-11-01）。
    /// </remarks>
    private static Border CreateDirectChild(string text, double height)
        => new()
        {
            Height = height,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xF3, 0xE0)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFB, 0x8C, 0x00)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x20, 0x20, 0x20)),
                Margin = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
}
