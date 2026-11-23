using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.WinForms.Annotation.Sample.Imaging;
using EsUtil.Ui.WinForms.SampleKit;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.WinForms.Annotation.Sample.Views;

/// <summary>注釈の動作確認フォーム。</summary>
/// <remarks>
/// <b>確認できること</b>: 枠線・吹き出しの追加と編集・選択とハンドル・しっぽの移動・本文のその場編集・文字単位の書式・元に戻す。<br/>
/// 画像は <see cref="TestImageFactory"/> が生成する（外部ファイルを必要としない）。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 画像 / 右下: ログ）を使う。
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

        var image = TestImageFactory.Create();
        var status = new Label { AutoSize = true, Text = "枠線か吹き出しをクリックして選択します。" };

        var editor = new AnnotationTextEditor { Width = image.Width, Height = image.Height };
        var layer = editor.Layer;
        layer.Log = logger;
        layer.LogPrefix = "[Annotation]";

        // 画像と文書を設定する（注釈の座標は元画像ピクセル）
        layer.Source = EsReadOnlyBitmap.FromBitmap(image);
        layer.SourcePixelWidth = image.Width;
        layer.SourcePixelHeight = image.Height;

        var core = new AnnotationCore(layer.Presenter) { Log = logger, LogPrefix = "[Annotation]" };
        var document = core.CreateDocument(image.Width, image.Height, "sample.png");
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
        layer.Document = document;

        var defaults = layer.DraftDefaults;

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = Column(
            SampleSettings.Slider("行間の倍率", AnnotationPresenter.LineHeightFactorDefault, 1.0, 2.0, 0.05, true,
                value => layer.LineHeightFactor = value),
            SampleSettings.Slider("枠線の太さ", defaults.FrameAppearance.BorderThickness, 1, 16, 1, true,
                value => ApplyDraft(draft => draft with { FrameAppearance = draft.FrameAppearance with { BorderThickness = value } })),
            SampleSettings.Slider("本文の文字サイズ", defaults.TextStyle.FontSize, 8, 96, 1, true,
                value => ApplyDraft(draft => draft with { TextStyle = draft.TextStyle with { FontSize = value } })),
            SampleSettings.Text("ログ接頭辞", "[Annotation]", value => layer.LogPrefix = value));

        // ── API操作（対象コントロールのメソッド実行） ──
        var methods = Column(
            SampleSettings.Buttons(
                ("枠線を追加", () => layer.AddFrame(new EsRect(120, 320, 300, 200))),
                ("吹き出しを追加", () => layer.AddBubble(new EsRect(300, 80, 320, 140), "新しい吹き出し")),
                ("本文を編集", () => layer.BeginTextEditing()),
                ("元に戻す", () => layer.Undo()),
                ("やり直す", () => layer.Redo()),
                ("削除", () => layer.DeleteSelected())));

        // ── サンプル設定（サンプルアプリの外観と状態） ──
        var samples = Column(
            SampleSettings.LogLevel(logging),
            status,
            SampleSettings.Buttons(("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))));

        layer.SelectionChanged += (_, _) => status.Text = layer.Selected is { } selected
            ? $"選択: {selected.Id}"
            : "選択なし";
        layer.DocumentChanged += (_, _) => status.Text = $"{layer.Document.Items.Length} 件の注釈";

        var content = new Panel { AutoScroll = true, Dock = DockStyle.Fill };
        content.Controls.Add(editor);

        var form = SampleShell.Create(
            "Annotation サンプル (WinForms)",
            properties,
            methods,
            samples,
            content,
            new SampleLogPanel(logging.Buffer));
        form.FormClosed += (_, _) =>
        {
            logging.Dispose();
            image.Dispose();
        };

        return form;

        // 新規作成時の既定値を変える（既存の注釈は変わらない）
        void ApplyDraft(Func<AnnotationDraftDefaults, AnnotationDraftDefaults> update)
        {
            defaults = update(defaults);
            layer.DraftDefaults = defaults;
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
