using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.WinUI3.SampleKit;
using EsUtil.Ui.WinUI3.Thumbnail.Sample.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EsUtil.Ui.WinUI3.Thumbnail.Sample.Views;

/// <summary>サムネイル一覧の動作確認ウィンドウ。</summary>
/// <remarks>
/// <b>確認できること</b>: 折り返し配置・可視範囲・選択・現在項目の強調・暗色テーマ・末尾へのスクロール。<br/>
/// 画像は <see cref="TestImageFactory"/> が生成する（外部ファイルを必要としない）。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 一覧 / 右下: ログ）を使う。
/// </remarks>
public sealed class MainWindow : Window
{
    /// <summary>生成した画像（一覧が参照するため保持する）。</summary>
    private readonly List<EsBitmap> _images = [];

    /// <summary>ログ（Serilog + 非同期表示）。</summary>
    private readonly SampleLogging _logging = new();

    /// <summary>状態の表示。</summary>
    private readonly TextBlock _status = new() { Text = "サムネイルを選択してください。" };

    /// <summary>サムネイル一覧。</summary>
    private readonly ThumbnailControl _thumbnails = new() { ImageWidth = 160 };

    /// <summary>追加した件数。</summary>
    private int _count;

    /// <summary>ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");
        _thumbnails.Log = logger;
        _thumbnails.LogPrefix = "[Thumbnail]";

        _thumbnails.SelectionChanged += (_, _) =>
            _status.Text = $"選択: {_thumbnails.SelectedIndex}（{_thumbnails.ItemCount} 件中）";
        _thumbnails.ItemActivated += (_, e) => _status.Text = $"確定: {e.Index}";

        var appearance = ThumbnailAppearance.Default;

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Check("暗色テーマ", false, value =>
                {
                    _thumbnails.IsDarkTheme = value;
                    _thumbnails.Refresh();
                }),
                SampleSettings.Number("画像の幅", 160, 40, 400, 10, true, value => _thumbnails.ImageWidth = value),
                SampleSettings.Number("選択インデックス", -1, -1, 10000, 1, true, value => _thumbnails.SelectedIndex = (int)value),
                SampleSettings.Slider("項目の余白", 0, 0, 24, 1, true, value => _thumbnails.Options = _thumbnails.Options with { ItemMargin = value }),
                SampleSettings.Slider("項目の内側の余白", 8, 0, 24, 1, true, value => _thumbnails.Options = _thumbnails.Options with { ItemPadding = value }),
                SampleSettings.Slider("項目の間隔", 8, 0, 32, 1, true, value => _thumbnails.Options = _thumbnails.Options with { ItemSpacing = value }),
                SampleSettings.Slider("ラベルの高さ", 20, 0, 48, 1, true, value => _thumbnails.Options = _thumbnails.Options with { LabelHeight = value }),
                SampleSettings.Slider("ラベルの左右の余白", 4, 0, 24, 1, true, value => _thumbnails.Options = _thumbnails.Options with { LabelHorizontalPadding = value }),
                SampleSettings.Slider("外側の余白の広がり", 2, 0, 24, 1, true, value => _thumbnails.Options = _thumbnails.Options with { SurroundExtent = value }),
                SampleSettings.Slider("内容の余白", 0, 0, 24, 1, true, value => _thumbnails.Options = _thumbnails.Options with { ContentPadding = value }),
                SampleSettings.Slider("画像の最小幅", 64, 16, 320, 8, true, value => _thumbnails.Options = _thumbnails.Options with { MinimumImageWidth = value }),
                SampleSettings.Slider("最大の列数", 0, 0, 16, 1, true, value => _thumbnails.Options = _thumbnails.Options with { MaximumColumns = (int)value }),
                SampleSettings.Slider("ラベルの文字サイズ", 11, 6, 24, 1, true, value => Apply(appearance with { LabelFontSize = value })),
                SampleSettings.Slider("ラベルの上下の余白", 2, 0, 16, 1, true, value => Apply(appearance with { LabelVerticalPadding = value })),
                SampleSettings.Check("現在項目のラベルを太字にする", true, value => Apply(appearance with { BoldCurrentLabel = value })),
                SampleSettings.Check("ホバーの背景を表示", true, value => Apply(appearance with { ShowHoverBackground = value })),
                SampleSettings.Check("選択の背景を表示", true, value => Apply(appearance with { ShowSelectionBackground = value })),
                SampleSettings.Check("強調時は影を隠す", true, value => Apply(appearance with { HideShadowWhenHighlighted = value })),
                SampleSettings.Text("ログ接頭辞", "[Thumbnail]", value => _thumbnails.LogPrefix = value),
            },
        };

        // ── サンプル設定（サンプル固有の操作。 並びは他 UI と共通） ──
        var samples = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                _status,
                SampleSettings.Buttons(
                    ("追加", AddItem),
                    ("12 件に戻す", ResetItems),
                    ("末尾へスクロール", () => _thumbnails.ScrollToIndex(_thumbnails.ItemCount - 1)),
                    ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
            },
        };

        for (var i = 0; i < 12; i++)
        {
            AddItem();
        }

        _thumbnails.SelectedIndex = 0;

        var log = new SampleLogPanel(_logging.Buffer);
        Title = "ThumbnailControl サンプル（WinUI 3）";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
        Content = SampleShell.CreateContent(properties, samples, _thumbnails, log);
        Closed += (_, _) => _logging.Dispose();

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
            EsReadOnlyBitmap.FromBitmap(image),
            $"capture-{_count:D4}.png",
            $"生成した画像 {_count}",
            isCurrent: _count == 0);

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
