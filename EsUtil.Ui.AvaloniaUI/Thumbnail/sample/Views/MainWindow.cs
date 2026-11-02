using Avalonia.Controls;
using EsUtil.Ui.AvaloniaUI.Interop.Imaging;
using EsUtil.Ui.AvaloniaUI.SampleKit;
using EsUtil.Ui.AvaloniaUI.Thumbnail.Sample.Imaging;
using EsUtil.Ui.AvaloniaUI.Thumbnail.Sample.Presentation;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.AvaloniaUI.Thumbnail.Sample.Views;

/// <summary>サムネイル一覧の動作確認ウィンドウ。</summary>
/// <remarks>
/// <b>確認できること</b>: 折り返し配置・可視範囲・選択の強調（帯と太字）・暗色テーマ・末尾へのスクロール。<br/>
/// 画像は <see cref="TestImageFactory"/> が生成する（外部ファイルを必要としない）。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 一覧 / 右下: ログ）を使う。
/// </remarks>
public sealed class MainWindow : Window
{
    /// <summary>生成した画像（一覧が参照するため保持する）。</summary>
    private readonly List<EsBitmap> _images = [];

    /// <summary>ログ（Serilog + 非同期表示）。</summary>
    private readonly SampleLogging _logging = new();

    /// <summary>状態を表示するラベル。</summary>
    private readonly TextBlock _status = new() { Text = "サムネイルを選択してください。" };

    /// <summary>サムネイル一覧。</summary>
    private readonly ThumbnailControl _thumbnails = new() { ImageWidth = 160 };

    /// <summary>追加した件数。</summary>
    private int _count;

    /// <summary>ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        Title = "Thumbnail サンプル (AvaloniaUI)";

        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");
        _thumbnails.Log = logger;
        _thumbnails.LogPrefix = "[Thumbnail]";

        // 選択は利用者の操作でコントロール側が変えるため、設定パネルの表示を追随させる
        var selectionRow = SampleSettings.NumberRow(
            "選択インデックス", -1, -1, 10000, 1, true, value => _thumbnails.SelectedIndex = (int)value);

        _thumbnails.SelectionChanged += (_, _) =>
        {
            selectionRow.SetValue(_thumbnails.SelectedIndex);
            _status.Text = $"選択: {_thumbnails.SelectedIndex}（{_thumbnails.ItemCount} 件中）";
        };
        _thumbnails.ItemActivated += (_, e) => _status.Text = $"確定: {e.Index}";

        var appearance = ThumbnailAppearance.Default;

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Number("画像の幅", 160, 40, 400, 10, true, value => _thumbnails.ImageWidth = value),
                selectionRow.Element,
                SampleSettings.Slider("行間", 8, 0, 32, 1, true, value => _thumbnails.Options = _thumbnails.Options with { RowSpace = value }),
                SampleSettings.Slider("列間", 8, 0, 32, 1, true, value => _thumbnails.Options = _thumbnails.Options with { ColumnSpace = value }),
                SampleSettings.Slider("内容の余白", 2, 0, 24, 1, true, value => _thumbnails.Options = _thumbnails.Options with { ContentPadding = value }),
                SampleSettings.Slider("ラベルの左右の余白", 1, 0, 24, 1, true, value => _thumbnails.Options = _thumbnails.Options with { LabelHorizontalPadding = value }),
                SampleSettings.Slider("画像の最小幅", 16, 16, 320, 8, true, value => _thumbnails.Options = _thumbnails.Options with { MinimumImageWidth = value }),
                SampleSettings.Slider("最大の列数", 0, 0, 16, 1, true, value => _thumbnails.Options = _thumbnails.Options with { MaximumColumns = (int)value }),
                SampleSettings.Slider("ラベルの文字サイズ", 11, 6, 24, 1, true, value => Apply(appearance with { LabelFontSize = value })),
                SampleSettings.Slider("ラベルの上下の余白", 1, 0, 16, 1, true, value => Apply(appearance with { LabelVerticalPadding = value })),
                SampleSettings.Check("選択項目（クリックした項目）のラベルを太字にする", true, value => Apply(appearance with { BoldSelectedLabel = value })),
                SampleSettings.Check("ホバーの背景を表示", true, value => Apply(appearance with { ShowHoverBackground = value })),
                SampleSettings.Check("選択の背景を表示", true, value => Apply(appearance with { ShowSelectionBackground = value })),
                SampleSettings.Check("影の表示", true, value => Apply(appearance with { ShowShadow = value })),
                SampleSettings.Text("ログ接頭辞", "[Thumbnail]", value => _thumbnails.LogPrefix = value),
                SampleSettings.Check("描画計画を差し替える（ホバーも選択の帯）", false, value =>
                    _thumbnails.DrawPlanResolver = value ? static plan => GuideThumbnailPlan.Resolve(in plan) : null),
            },
        };

        // ── API操作（対象コントロールのメソッド実行） ──
        var methods = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Buttons(
                    ("追加", AddItem),
                    ("12 件に戻す", ResetItems),
                    ("末尾へスクロール", () => _thumbnails.ScrollToIndex(_thumbnails.ItemCount - 1))),
            },
        };

        // ── サンプル設定（サンプルアプリの外観と状態） ──
        var samples = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.LogLevel(_logging),
                _status,
                SampleSettings.Buttons(("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
            },
        };

        for (var i = 0; i < 12; i++)
        {
            AddItem();
        }

        _thumbnails.SelectedIndex = 0;

        var log = new SampleLogPanel(_logging.Buffer);
        var window = SampleShell.Create(
            "Thumbnail サンプル (AvaloniaUI)",
            properties,
            methods,
            samples,
            _thumbnails,
            log);
        Content = window.Content;
        Title = window.Title;
        Width = window.Width;
        Height = window.Height;
        MinWidth = window.MinWidth;
        MinHeight = window.MinHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Closed += (_, _) =>
        {
            _logging.Dispose();
            foreach (var image in _images)
            {
                image.Dispose();
            }
        };

        return;

        // 外観を更新してコントロールへ反映する
        void Apply(ThumbnailAppearance value)
        {
            appearance = value;
            _thumbnails.Appearance = value;
            _thumbnails.Refresh();
        }
    }

    /// <summary>画像を 1 件生成して一覧へ追加する。</summary>
    private void AddItem()
    {
        var image = TestImageFactory.Create(320, 200, _count);
        _images.Add(image);

        _thumbnails.Add(
            EsReadOnlyBitmap.FromBitmap(image).ToAvaloniaBitmap(),
            $"capture-{_count:D4}.png",
            $"生成した画像 {_count}");

        _count++;
        _status.Text = $"{_count} 件を表示しています。";
    }

    /// <summary>12 件に戻す（すべて作り直す）。</summary>
    private void ResetItems()
    {
        _thumbnails.ClearItems();
        foreach (var image in _images)
        {
            image.Dispose();
        }

        _images.Clear();
        _count = 0;
        for (var i = 0; i < 12; i++)
        {
            AddItem();
        }

        _thumbnails.SelectedIndex = 0;
    }
}
