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
/// <b>示すこと</b>: <see cref="DualSourceItemsPanel.ItemsSource"/>（データ）と直接の子（コードで足した要素）を
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
    private readonly ColumnPackingPanelDebugText _panel = new() { ColumnLimit = 4, RowSpace = 8, ColumnSpace = 8 };

    /// <summary>レイアウト結果の表示。</summary>
    private readonly TextBlock _status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Text = "待機中..." };

    /// <summary>並べる項目（<see cref="DualSourceItemsPanel.ItemsSource"/> へ渡す）。</summary>

    /// <summary>並べる項目の数。</summary>
    private int _itemCount = 20;

    /// <summary>メイン ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

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
                SampleSettings.Slider("列の上限", 4, 1, 8, 1, true, value => _panel.ColumnLimit = (int)value),
                SampleSettings.Slider("行間", 8, 0, 24, 1, true, value => _panel.RowSpace = value),
                SampleSettings.Slider("列間", 8, 0, 24, 1, true, value => _panel.ColumnSpace = value),
                // ItemsSource は Native AOT で使えないため、項目は直接の子として並べる（ビルド設定規約 §6.1）
                SampleSettings.Check("直接の子を後に置く", true, value => _panel.ItemsSourceFirst = value),
                SampleSettings.Check("デバッグ表示", false, value => _panel.ShowDebugText = value),
                SampleSettings.Choice("配置の方法", ["貪欲法", "動的計画法", "二分探索"], 0, index =>
                    _panel.Method = index switch
                    {
                        1 => EsUtil.Feature.ColumnPacking.ColumnPacker.Method.DynamicProgramming,
                        2 => EsUtil.Feature.ColumnPacking.ColumnPacker.Method.BinarySearch,
                        _ => EsUtil.Feature.ColumnPacking.ColumnPacker.Method.Greedy,
                    }),
            },
        };

        // ── サンプル設定（サンプル固有の操作。 並びは他 UI と共通） ──
        var samples = new StackPanel
        {
            Spacing = 6,
            Children =
            {
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
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
        Content = SampleShell.CreateContent(properties, samples, contentArea, log);

        _panel.SizeChanged += (_, _) => UpdateStatus();
        UpdateStatus();
        Closed += (_, _) => _logging.Dispose();

        return;

        // 項目を作り直す（ItemsSource は使わず直接の子として並べる）
        void RebuildItems()
        {
            var items = CreateItems(_itemCount);
            _panel.Children.Clear();
            foreach (var item in items)
            {
                _panel.Children.Add(CreateItemChild(item));
            }

            _panel.Children.Add(CreateDirectChild("直接の子 A", 56));
            _panel.Children.Add(CreateDirectChild("直接の子 B", 72));
            UpdateStatus();
        }

        // レイアウト結果をログへ出力する
        void LogLayout()
        {
            var text = _panel.GetDebugText();
            _logging.Logger.Log(LogLevel.Information, default, text, null, static (state, _) => state);
            UpdateStatus();
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

    /// <summary>項目 1 件分の要素を作る。</summary>
    /// <param name="item">作る項目。</param>
    /// <returns>作った要素。</returns>
    private static Border CreateItemChild(SampleItem item)
        => new()
        {
            Height = item.Height,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xE8, 0xF0, 0xFE)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x1E, 0x88, 0xE5)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock { Text = item.Label, Margin = new Thickness(8, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center },
        };

    /// <summary>項目と一緒に並べる直接の子を作る。</summary>
    /// <param name="text">表示する文言。</param>
    /// <param name="height">高さ（DIP）。</param>
    /// <returns>作った要素。</returns>
    private static Border CreateDirectChild(string text, double height)
        => new()
        {
            Height = height,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xF3, 0xE0)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFB, 0x8C, 0x00)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock { Text = text, Margin = new Thickness(8, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center },
        };
}
