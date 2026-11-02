using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.MewUI.SampleKit;
using EsUtil.Ui.MewUI.Thumbnail.Sample.Imaging;
using EsUtil.Ui.MewUI.Thumbnail.Sample.Presentation;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.MewUI.Thumbnail.Sample.Views;

/// <summary>サムネイル一覧の動作確認ウィンドウ。</summary>
/// <remarks>
/// <b>確認できること</b>: 折り返し配置・可視範囲・選択の強調（帯と太字）・暗色テーマ・末尾へのスクロール。<br/>
/// 画像は <see cref="TestImageFactory"/> が生成する（外部ファイルを必要としない）。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 一覧 / 右下: ログ）を使う。
/// </remarks>
internal static class MainWindow
{
    /// <summary>生成した画像（一覧が参照するため、静的で保持する）。</summary>
    private static readonly List<EsBitmap> s_images = [];

    /// <summary>追加した件数。</summary>
    private static int s_count;

    /// <summary>ウィンドウを組み立てる。</summary>
    /// <returns>生成したウィンドウ。</returns>
    public static Window Create()
    {
        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var log = new SampleLogPanel(logging.Buffer);
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        var status = new TextBlock().Text("サムネイルを選択してください。").FontSize(11);
        var appearance = ThumbnailAppearance.Default;

        var thumbnails = new ThumbnailControl
        {
            ImageWidth = 160,
            Log = logger,
            LogPrefix = "[Thumbnail]",
        };

        // 選択は利用者の操作でコントロール側が変えるため、設定パネルの表示を追随させる
        var selectionRow = SampleSettings.NumberRow(
            "選択インデックス", -1, -1, 10000, 1, true, value => thumbnails.SelectedIndex = (int)value);

        thumbnails.SelectionChanged += (_, _) =>
        {
            selectionRow.SetValue(thumbnails.SelectedIndex);
            status.Text = $"選択: {thumbnails.SelectedIndex}（{thumbnails.ItemCount} 件中）";
        };
        thumbnails.ItemActivated += (_, e) => status.Text = $"確定: {e.Index}";

        // プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通）
        var properties = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.Number("画像の幅", 160, 40, 400, 10, true, value => thumbnails.ImageWidth = value),
            selectionRow.Element,
            SampleSettings.Slider("行間", 8, 0, 32, 1, true, value => thumbnails.Options = thumbnails.Options with { RowSpace = value }),
            SampleSettings.Slider("列間", 8, 0, 32, 1, true, value => thumbnails.Options = thumbnails.Options with { ColumnSpace = value }),
            SampleSettings.Slider("内容の余白", 2, 0, 24, 1, true, value => thumbnails.Options = thumbnails.Options with { ContentPadding = value }),
            SampleSettings.Slider("ラベルの左右の余白", 1, 0, 24, 1, true, value => thumbnails.Options = thumbnails.Options with { LabelHorizontalPadding = value }),
            SampleSettings.Slider("画像の最小幅", 16, 16, 320, 8, true, value => thumbnails.Options = thumbnails.Options with { MinimumImageWidth = value }),
            SampleSettings.Slider("最大の列数", 0, 0, 16, 1, true, value => thumbnails.Options = thumbnails.Options with { MaximumColumns = (int)value }),
            SampleSettings.Slider("ラベルの文字サイズ", 11, 6, 24, 1, true, value => Apply(appearance with { LabelFontSize = value })),
            SampleSettings.Slider("ラベルの上下の余白", 1, 0, 16, 1, true, value => Apply(appearance with { LabelVerticalPadding = value })),
            SampleSettings.Check("選択項目（クリックした項目）のラベルを太字にする", true, value => Apply(appearance with { BoldSelectedLabel = value })),
            SampleSettings.Check("ホバーの背景を表示", true, value => Apply(appearance with { ShowHoverBackground = value })),
            SampleSettings.Check("選択の背景を表示", true, value => Apply(appearance with { ShowSelectionBackground = value })),
            SampleSettings.Check("影の表示", true, value => Apply(appearance with { ShowShadow = value })),
            SampleSettings.Text("ログ接頭辞", "[Thumbnail]", value => thumbnails.LogPrefix = value),
            SampleSettings.Check("描画計画を差し替える（ホバーも選択の帯）", false, value =>
                thumbnails.DrawPlanResolver = value ? static plan => GuideThumbnailPlan.Resolve(in plan) : null));

        // API操作（対象コントロールのメソッド実行）
        var methods = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.Buttons(
                ("追加", () => AddItem(thumbnails, status)),
                ("12 件に戻す", () =>
                {
                    thumbnails.ClearItems();
                    s_count = 0;
                    for (var i = 0; i < 12; i++)
                    {
                        AddItem(thumbnails, status);
                    }

                    thumbnails.SelectedIndex = 0;
                }
        ),
                ("末尾へスクロール", () => thumbnails.ScrollToIndex(thumbnails.ItemCount - 1))));

        // サンプル設定（サンプルアプリの外観と状態）
        var samples = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.LogLevel(logging),
            status,
            SampleSettings.Buttons(("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))));

        for (var i = 0; i < 12; i++)
        {
            AddItem(thumbnails, status);
        }

        thumbnails.SelectedIndex = 0;

        var window = SampleShell.Create(
            "ThumbnailControl サンプル（MewUI）",
            properties,
            methods,
            samples,
            thumbnails,
            log);
        window.Closed += () => logging.Dispose();
        return window;

        // 外観を更新してコントロールへ反映する
        void Apply(ThumbnailAppearance value)
        {
            appearance = value;
            thumbnails.Appearance = value;
            thumbnails.Refresh();
        }
    }

    /// <summary>画像を 1 件生成して一覧へ追加する。</summary>
    /// <param name="thumbnails">追加先の一覧。</param>
    /// <param name="status">状態表示。</param>
    private static void AddItem(ThumbnailControl thumbnails, TextBlock status)
    {
        var image = TestImageFactory.Create(320, 200, s_count);
        s_images.Add(image);

        thumbnails.Add(
            EsReadOnlyBitmap.FromBitmap(image),
            $"capture-{s_count:D4}.png",
            $"生成した画像 {s_count}");

        s_count++;
        status.Text = $"{s_count} 件を表示しています。";
    }
}
