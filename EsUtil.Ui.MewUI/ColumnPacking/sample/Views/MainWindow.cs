using System.Collections.ObjectModel;
using System.Globalization;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Ui.MewUI.SampleKit;
using Microsoft.Extensions.Logging;
using MewColor = Aprillz.MewUI.Color;

namespace EsUtil.Ui.MewUI.ColumnPacking.Sample.Views;

/// <summary>サンプルのメイン ウィンドウ（<see cref="ColumnPackingPanel"/> の使い方を示す）。</summary>
/// <remarks>
/// <b>示すこと</b>: <see cref="DualSourceItemsPanel.SetItems"/>（データ）と直接の子（コードで足した要素）を<br/>
/// 同時に並べられること、<see cref="ColumnPackingPanel.ColumnLimit"/> と余白で配置が変わること、<br/>
/// レイアウト結果（<see cref="ColumnPackingPanel.LayoutMinHeight"/> など）がプロパティとして読めること。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: パネル / 右下: ログ）を使う。
/// </remarks>
internal static class MainWindow
{
    /// <summary>項目を生成するときの名前の一覧。</summary>
    private static readonly string[] s_labels =
    [
        "丸括弧", "角括弧", "波括弧", "引用符", "カンマ", "ピリオド",
        "コロン", "セミコロン", "不等号", "イコール", "プラス", "マイナス",
        "はてな", "感嘆符", "シャープ", "ドル", "パーセント", "アンパサンド",
        "アスタリスク", "スラッシュ",
    ];

    /// <summary>データから子を作るときの見た目（データ駆動の子は青、直接の子は橙）。</summary>
    /// <remarks>
    /// MewUI のテンプレートは「要素を作る」と「項目を束縛する」が分かれるため、<see cref="DelegateTemplate{T}"/> で組み立てる。
    /// </remarks>
    private static readonly IDataTemplate s_itemTemplate = new DelegateTemplate<SampleItem>(
        static _ => CreateItemBorder(),
        static (view, item, _, _) => ApplyItem(view, item),
        static (_, _, _, _) => { });

    /// <summary>ウィンドウを組み立てる。</summary>
    /// <returns>生成したウィンドウ。</returns>
    public static Window Create()
    {
        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var log = new SampleLogPanel(logging.Buffer);
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        // パネルのログをログ領域へ流す（プロパティ変更は Information、レイアウト結果の変化は Debug）
        var panel = new ColumnPackingPanel
        {
            ColumnLimit = 10,
            RowSpace = 8,
            ColumnSpace = 8,
            ItemTemplate = s_itemTemplate,
            Log = logger,
            LogPrefix = "[ColumnPacking]",
        };

        // 順序が重要: ItemTemplate を先に設定する（この時点の子は再構築で消えるため、直接の子はその後に足す）。
        // その後に直接の子を足し、SetItems でデータを渡す（初回の再構築で「直接の子」として保存される）
        panel.Add(CreateDirectChild("直接の子 A", 56));
        panel.Add(CreateDirectChild("直接の子 B", 72));

        var status = new TextBlock().Text("待機中...").FontSize(11);
        status.TextWrapping = TextWrapping.Wrap;

        var items = new ObservableCollection<SampleItem>();
        var itemCount = 20;

        // データを渡して子を作り直す（保持したリストをそのまま並べる入口）
        RebuildItems();

        var contentArea = new ScrollViewer().Content(panel);
        contentArea.HorizontalScroll = ScrollMode.Disabled;
        contentArea.VerticalScroll = ScrollMode.Auto;

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.Slider("列の上限", 10, 1, 16, 1, true, value => panel.ColumnLimit = (int)value),
            SampleSettings.Slider("行間", 8, 0, 24, 1, true, value => panel.RowSpace = value),
            SampleSettings.Slider("列間", 8, 0, 24, 1, true, value => panel.ColumnSpace = value),
            SampleSettings.Choice("配置の方法", ["貪欲法", "動的計画法", "二分探索"], 2, index =>
                panel.Method = index switch
                {
                    1 => ColumnPacker.Method.DynamicProgramming,
                    2 => ColumnPacker.Method.BinarySearch,
                    _ => ColumnPacker.Method.Greedy,
                }),
            // データから作った子と直接の子（コードで足した要素）の並び順を切り替える
            SampleSettings.Check("直接の子を後ろに置く", true, value => panel.ItemsSourceFirst = value),
            SampleSettings.Text("ログ接頭辞", "[ColumnPacking]", value => panel.LogPrefix = value));

        // ── メソッド操作（対象コントロールのメソッド実行） ──
        var methods = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.Buttons(("項目を再設定（SetItems）", () => RebuildItems())));

        // ── サンプル設定（サンプルアプリの外観と状態） ──
        var samples = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.LogLevel(logging),
            SampleSettings.Number("アイテム数", itemCount, 1, 60, 1, true, value =>
            {
                itemCount = (int)value;
                RebuildItems();
            }),
            SampleSettings.Buttons(
                ("レイアウト結果をログへ出力", LogLayout),
                ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
            status);

        var window = SampleShell.Create(
            "ColumnPacking パネル サンプル（MewUI）", properties, methods, samples, contentArea, log);
        window.Closed += () => logging.Dispose();

        // レイアウト結果は配置のたびに変わるため、パネルの大きさが変わったら表示を追随させる
        panel.SizeChanged += _ => UpdateStatus();
        UpdateStatus();
        return window;

        // 項目を作り直す（データ側だけを差し替える。 直接の子は初回の SetItems で保存済み）
        void RebuildItems()
        {
            items.Clear();
            foreach (var item in CreateItems(itemCount))
            {
                items.Add(item);
            }

            // プロパティ（ItemsSource）を介さない入口へ渡す（保持するリストをそのまま並べる）
            panel.SetItems(items);

            UpdateStatus();
        }

        // レイアウト結果をログへ出力する（公開プロパティの値だけを 1 行で出す）
        void LogLayout()
        {
            if (panel.LayoutMinHeight is not { } minHeight)
            {
                logger.LogInformation("レイアウトはまだ確定していません。");
                return;
            }

            var usedWidth = panel.LayoutUsedWidth ?? 0;
            var columns = panel.LayoutColumnCount ?? 0;
            var segments = panel.LayoutColumnSegments;
            var text = string.Create(
                CultureInfo.InvariantCulture,
                $"レイアウト結果: MinHeight={minHeight:F1} UsedWidth={usedWidth:F1} 列数={columns} ColumnSegments={segments}");
            logger.Log(LogLevel.Information, default, text, null, static (state, _) => state);
        }

        // レイアウト結果を画面へ出す
        void UpdateStatus()
        {
            status.Text = panel.LayoutMinHeight is { } minHeight
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"最小の高さ: {minHeight:F1} / 使用幅: {panel.LayoutUsedWidth:F1} / 列数: {panel.LayoutColumnCount} / 列の区切り: {panel.LayoutColumnSegments}")
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

    /// <summary>データから作る子の土台（枠だけ）を作る。</summary>
    /// <returns>作った要素。</returns>
    /// <remarks>
    /// <b>文字色も固定する理由</b>: タイルの地色は明るい色で固定している。 文字色をテーマの既定（ダークでは白）に任せると、
    /// 明るい地色の上で読めなくなる（実測 2026-11-01）。
    /// </remarks>
    private static Border CreateItemBorder()
        => new()
        {
            Background = MewColor.FromRgb(0xE8, 0xF0, 0xFE),
            BorderBrush = MewColor.FromRgb(0x1E, 0x88, 0xE5),
            BorderThickness = 1,
            CornerRadius = 4,
            Child = new TextBlock
            {
                Foreground = MewColor.FromRgb(0x20, 0x20, 0x20),
                Margin = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

    /// <summary>データから作る子へ項目の値を写す。</summary>
    /// <param name="view">テンプレートが作った要素。</param>
    /// <param name="item">束縛する項目。</param>
    private static void ApplyItem(FrameworkElement view, SampleItem item)
    {
        if (view is not Border border)
        {
            return;
        }

        border.Height = item.Height;
        if (border.Child is TextBlock text)
        {
            text.Text = item.Label;
        }
    }

    /// <summary>項目と一緒に並べる直接の子を作る。</summary>
    /// <param name="text">表示する文言。</param>
    /// <param name="height">高さ（DIP）。</param>
    /// <returns>作った要素。</returns>
    private static Border CreateDirectChild(string text, double height)
        => new()
        {
            Height = height,
            Background = MewColor.FromRgb(0xFF, 0xF3, 0xE0),
            BorderBrush = MewColor.FromRgb(0xFB, 0x8C, 0x00),
            BorderThickness = 1,
            CornerRadius = 4,
            Child = new TextBlock
            {
                Text = text,
                Foreground = MewColor.FromRgb(0x20, 0x20, 0x20),
                Margin = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
}
