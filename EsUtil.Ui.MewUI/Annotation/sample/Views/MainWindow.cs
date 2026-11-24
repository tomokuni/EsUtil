using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.MewUI.Annotation.Sample.Imaging;
using EsUtil.Ui.MewUI.SampleKit;
using Microsoft.Extensions.Logging;
using EsUtil.Feature.Annotation.Core.Defaults;

namespace EsUtil.Ui.MewUI.Annotation.Sample.Views;

/// <summary>注釈の動作確認ウィンドウ。</summary>
/// <remarks>
/// <b>確認できること</b>: 枠線・吹き出しの追加と編集・選択とハンドル・しっぽの移動・本文のその場編集・文字単位の書式・元に戻す。<br/>
/// 画像は <see cref="TestImageFactory"/> が生成する（外部ファイルを必要としない）。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 画像 / 右下: ログ）を使う。
/// </remarks>
internal static class MainWindow
{
    /// <summary>生成した画像（コントロールが参照するため、静的で保持する）。</summary>
    private static readonly EsBitmap s_image = TestImageFactory.Create();

    /// <summary>ウィンドウを組み立てる。</summary>
    /// <returns>生成したウィンドウ。</returns>
    public static Window Create()
    {
        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var log = new SampleLogPanel(logging.Buffer);
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        var status = new TextBlock().Text("枠線か吹き出しをクリックして選択します。").FontSize(11);

        var editor = new AnnotationTextEditor { Width = s_image.Width, Height = s_image.Height };
        var layer = editor.Layer;
        layer.Log = logger;
        layer.LogPrefix = "[Annotation]";

        // 画像と文書を設定する（注釈の座標は元画像ピクセル）
        layer.Source = EsReadOnlyBitmap.FromBitmap(s_image);
        layer.SourcePixelWidth = s_image.Width;
        layer.SourcePixelHeight = s_image.Height;

        var core = layer.Core;
        var document = core.CreateDocument(s_image.Width, s_image.Height, "sample.png");
        document = document.Add(core.CreateFrame(document, new EsRect(60, 60, 340, 220)));
        document = document.Add(core.CreateBubble(
            document,
            new EsRect(520, 340, 380, 170),
            new RichText([
                new TextRun("ここが重要です。", AnnotationDefaults.TextStyle),
                new TextRun("文字単位の書式", AnnotationDefaults.TextStyle with { IsBold = true, Color = System.Drawing.Color.FromArgb(255, 200, 0, 0) }),
                new TextRun("は右クリックで編集できます。", AnnotationDefaults.TextStyle),
            ]),
            tail: new BubbleTail(new EsPoint(430, 200), BubbleEdge.Top, 0.3, 26)));

        // 見た目の確認用（角丸・塗り・しっぽの辺が注釈ごとに保たれること）
        var accent = AnnotationDraftDefaults.Default with
        {
            BubbleAppearance = AnnotationDefaults.BubbleAppearance with
            {
                Border = System.Drawing.Color.FromArgb(255, 30, 90, 200),
                BorderThickness = 2,
                Fill = System.Drawing.Color.FromArgb(255, 235, 245, 255),
                CornerRadius = 18,
            },
        };
        document = document.Add(core.CreateBubble(
            document,
            new EsRect(80, 420, 300, 150),
            new RichText([new TextRun("角丸・塗り・しっぽ（右辺）", accent.TextStyle)]),
            accent,
            tail: new BubbleTail(new EsPoint(430, 495), BubbleEdge.Right, 0.5, 20)));
        layer.Document = document;

        var defaults = layer.DraftDefaults;

        // プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通）
        var properties = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.Slider("行間の倍率", AnnotationPresenter.LineHeightFactorDefault, 1.0, 2.0, 0.05, true,
                value => layer.LineHeightFactor = value),
            SampleSettings.Slider("枠線の太さ", defaults.FrameAppearance.BorderThickness, 1, 16, 1, true,
                value => ApplyDraft(draft => draft with { FrameAppearance = draft.FrameAppearance with { BorderThickness = value } })),
            SampleSettings.Slider("本文の文字サイズ", defaults.TextStyle.FontSize, 8, 96, 1, true,
                value => ApplyDraft(draft => draft with { TextStyle = draft.TextStyle with { FontSize = value } })),
            SampleSettings.Text("ログ接頭辞", "[Annotation]", value => layer.LogPrefix = value));

        // API操作（対象コントロールのメソッド実行）
        var methods = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.Buttons(
                ("枠線を追加", () => layer.AddFrame(new EsRect(120, 320, 300, 200))),
                ("吹き出しを追加", () => layer.AddBubble(new EsRect(300, 80, 320, 140), "新しい吹き出し")),
                ("本文を編集", () => layer.BeginTextEditing()),
                ("元に戻す", () => layer.Undo()),
                ("やり直す", () => layer.Redo()),
                ("最前面へ", () => layer.MoveSelectedToFront()),
                ("最背面へ", () => layer.MoveSelectedToBack()),
                ("削除", () => layer.DeleteSelected())));

        // ── サンプル設定（サンプルアプリの外観と状態） ──
        var samples = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.LogLevel(logging),
            new TextBlock().Text("ダブルクリック: 本文 / 右クリック: 外観 / 四隅と辺: サイズ変更 / しっぽの先端: ドラッグ"),
            status,
            SampleSettings.Buttons(("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))));

        layer.SelectionChanged += (_, _) => status.Text = layer.Selected is { } selected
            ? $"選択: {selected.Id}"
            : "選択なし";
        layer.DocumentChanged += (_, _) => status.Text = $"{layer.Document.Items.Length} 件の注釈";

        var window = SampleShell.Create(
            "Annotation サンプル (MewUI)",
            properties,
            methods,
            samples,
            new ScrollViewer().Content(editor),
            log);
        window.Closed += () =>
        {
            logging.Dispose();
            s_image.Dispose();
        };

        return window;

        // 新規作成時の既定値を変える（既存の注釈は変わらない）
        void ApplyDraft(Func<AnnotationDraftDefaults, AnnotationDraftDefaults> update)
        {
            defaults = update(defaults);
            layer.DraftDefaults = defaults;
        }
    }
}
