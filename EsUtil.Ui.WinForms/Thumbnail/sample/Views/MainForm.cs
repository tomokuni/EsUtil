using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.WinForms.Interop.Imaging;
using EsUtil.Ui.WinForms.SampleKit;
using EsUtil.Ui.WinForms.Thumbnail.Sample.Imaging;
using EsUtil.Ui.WinForms.Thumbnail.Sample.Presentation;
using Microsoft.Extensions.Logging;
using DrawingBitmap = System.Drawing.Bitmap;

namespace EsUtil.Ui.WinForms.Thumbnail.Sample.Views;

/// <summary>サムネイル一覧の動作確認フォーム。</summary>
/// <remarks>
/// <b>確認できること</b>: 折り返し配置・可視範囲・選択・現在項目の強調・暗色テーマ・末尾へのスクロール。<br/>
/// 画像は <see cref="TestImageFactory"/> が生成する（外部ファイルを必要としない）。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 一覧 / 右下: ログ）を使う。
/// </remarks>
internal static class MainForm
{
    /// <summary>メイン フォームを生成する。</summary>
    /// <returns>生成したフォーム。</returns>
    public static Form Create()
    {
        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        var images = new List<DrawingBitmap>();
        var appearance = ThumbnailAppearance.Default;
        var count = 0;

        var status = new Label { AutoSize = true, Text = "サムネイルを選択してください。" };
        var thumbnails = new ThumbnailControl
        {
            Dock = DockStyle.Fill,
            ImageWidth = 160,
            Log = logger,
            LogPrefix = "[Thumbnail]",
        };

        // 選択と現在項目は利用者の操作でコントロール側が変えるため、設定パネルの表示を追随させる
        var selectionRow = SampleSettings.NumberRow(
            "選択インデックス", -1, -1, 10000, 0, value => thumbnails.SelectedIndex = (int)value);
        var currentRow = SampleSettings.NumberRow(
            "現在インデックス", -1, -1, 10000, 0, value => thumbnails.CurrentIndex = (int)value);

        thumbnails.SelectionChanged += (_, _) =>
        {
            selectionRow.SetValue(thumbnails.SelectedIndex);

            // クリックした項目を「現在項目」にする（現在項目のラベルが太字になる。 ブラウザでいえば開いている項目）
            thumbnails.CurrentIndex = thumbnails.SelectedIndex;
            status.Text = $"選択: {thumbnails.SelectedIndex}（{thumbnails.ItemCount} 件中）";
        };
        thumbnails.CurrentIndexChanged += (_, _) => currentRow.SetValue(thumbnails.CurrentIndex);
        thumbnails.ItemActivated += (_, e) =>
        {
            // 2 回押しで開いた項目を現在項目にする（現在項目のラベルが太字になる）
            thumbnails.CurrentIndex = e.Index;
            status.Text = $"確定: {e.Index}";
        };

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = Column(
            SampleSettings.Check("暗色テーマ", false, value =>
            {
                thumbnails.IsDarkTheme = value;
                thumbnails.Refresh();
            }),
            SampleSettings.Number("画像の幅", 160, 40, 400, 0, value => thumbnails.ImageWidth = value),
            selectionRow.Element,
            currentRow.Element,
            SampleSettings.Slider("行間", 8, 0, 32, 1, true, value => thumbnails.Options = thumbnails.Options with { RowSpace = value }),
            SampleSettings.Slider("列間", 8, 0, 32, 1, true, value => thumbnails.Options = thumbnails.Options with { ColumnSpace = value }),
            SampleSettings.Slider("ラベルの左右の余白", 1, 0, 24, 1, true, value => thumbnails.Options = thumbnails.Options with { LabelHorizontalPadding = value }),
            SampleSettings.Slider("内容の余白", 2, 0, 24, 1, true, value => thumbnails.Options = thumbnails.Options with { ContentPadding = value }),
            SampleSettings.Slider("画像の最小幅", 16, 16, 320, 8, true, value => thumbnails.Options = thumbnails.Options with { MinimumImageWidth = value }),
            SampleSettings.Slider("最大の列数", 0, 0, 16, 1, true, value => thumbnails.Options = thumbnails.Options with { MaximumColumns = (int)value }),
            SampleSettings.Slider("ラベルの文字サイズ", 11, 6, 24, 1, true, value => Apply(appearance with { LabelFontSize = value })),
            SampleSettings.Slider("ラベルの上下の余白", 1, 0, 16, 1, true, value => Apply(appearance with { LabelVerticalPadding = value })),
            SampleSettings.Check("現在項目のラベルを太字にする", true, value => Apply(appearance with { BoldCurrentLabel = value })),
            SampleSettings.Check("ホバーの背景を表示", true, value => Apply(appearance with { ShowHoverBackground = value })),
            SampleSettings.Check("選択の背景を表示", true, value => Apply(appearance with { ShowSelectionBackground = value })),
            SampleSettings.Check("影の表示", true, value => Apply(appearance with { ShowShadow = value })),
            SampleSettings.Check("選択時に影を隠す", false, value => Apply(appearance with { HideShadowWhenSelected = value })),
            SampleSettings.Text("ログ接頭辞", "[Thumbnail]", value => thumbnails.LogPrefix = value),
            SampleSettings.Check("描画計画を差し替える（強調色）", false, value =>
                thumbnails.DrawPlanResolver = value ? static plan => GuideThumbnailPlan.Resolve(in plan) : null));

        // ── サンプル設定（サンプル固有の操作。 並びは他 UI と共通） ──
        var samples = Column(
            SampleSettings.LogLevel(logging),
            status,
            SampleSettings.Buttons(
                ("追加", () => AddItem()),
                ("12 件に戻す", ResetItems),
                ("末尾へスクロール", () => thumbnails.ScrollToIndex(thumbnails.ItemCount - 1)),
                ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))));

        for (var i = 0; i < 12; i++)
        {
            AddItem();
        }

        thumbnails.SelectedIndex = 0;
        thumbnails.CurrentIndex = 0;

        var log = new SampleLogPanel(logging.Buffer);
        var form = SampleShell.Create("ThumbnailControl サンプル（WinForms）", properties, samples, thumbnails, log);
        form.FormClosed += (_, _) =>
        {
            foreach (var image in images)
            {
                image.Dispose();
            }

            logging.Dispose();
        };

        return form;

        // 12 件に戻す
        void ResetItems()
        {
            thumbnails.ClearItems();
            foreach (var image in images)
            {
                image.Dispose();
            }

            images.Clear();
            count = 0;
            for (var i = 0; i < 12; i++)
            {
                AddItem();
            }

            thumbnails.SelectedIndex = 0;
            thumbnails.CurrentIndex = 0;
        }

        // 画像を 1 件生成して一覧へ追加する
        void AddItem()
        {
            var image = TestImageFactory.Create(320, 200, count);
            images.Add(EsBitmapDrawingInterop.ToDrawingBitmap(EsReadOnlyBitmap.FromBitmap(image)));

            thumbnails.Add(images[^1], $"capture-{count:D4}.png", $"生成した画像 {count}", isCurrent: false);

            count++;
            status.Text = $"{count} 件を表示しています。";
        }

        // 外観を更新してコントロールへ反映する
        void Apply(ThumbnailAppearance value)
        {
            appearance = value;
            thumbnails.Appearance = value;
            thumbnails.Refresh();
        }
    }

    /// <summary>設定の行を縦に並べる。</summary>
    /// <param name="rows">行。</param>
    /// <returns>縦に並べた要素。</returns>
    private static Control Column(params Control[] rows)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        panel.Controls.AddRange(rows);
        return panel;
    }
}
