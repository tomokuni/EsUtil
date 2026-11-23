using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinUI3.Annotation.Sample.Imaging;
using EsUtil.Ui.WinUI3.SampleKit;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EsUtil.Feature.Annotation.Core.Defaults;

namespace EsUtil.Ui.WinUI3.Annotation.Sample.Views;

/// <summary>注釈の動作確認ウィンドウ。</summary>
/// <remarks>
/// <b>確認できること</b>: 枠線・吹き出しの追加と編集・選択とハンドル・しっぽの移動・本文のその場編集・文字単位の書式・元に戻す。<br/>
/// 画像は <see cref="TestImageFactory"/> が生成する（外部ファイルを必要としない）。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 画像 / 右下: ログ）を使う。
/// </remarks>
public sealed class MainWindow : Window
{
    /// <summary>生成した画像（コントロールが参照するため保持する）。</summary>
    private readonly EsBitmap _image = TestImageFactory.Create();

    /// <summary>ログ（Serilog + 非同期表示）。</summary>
    private readonly SampleLogging _logging = new();

    /// <summary>状態の表示。</summary>
    private readonly TextBlock _status = new() { Text = "枠線か吹き出しをクリックして選択します。" };

    /// <summary>ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        Title = "Annotation サンプル (WinUI3)";

        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        var editor = new AnnotationTextEditor { Width = _image.Width, Height = _image.Height };
        var layer = editor.Layer;
        layer.Log = logger;
        layer.LogPrefix = "[Annotation]";
        layer.MeasureText = AnnotationTextRunMeasure.Measure;

        // 画像と文書を設定する（注釈の座標は元画像ピクセル）
        layer.Source = EsReadOnlyBitmap.FromBitmap(_image);
        layer.SourcePixelWidth = _image.Width;
        layer.SourcePixelHeight = _image.Height;

        var core = layer.Core;
        var document = core.CreateDocument(_image.Width, _image.Height, "sample.png");
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
        var properties = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Slider("行間の倍率", AnnotationPresenter.LineHeightFactorDefault, 1.0, 2.0, 0.05, true,
                    value => layer.LineHeightFactor = value),
                SampleSettings.Slider("枠線の太さ", defaults.FrameAppearance.BorderThickness, 1, 16, 1, true,
                    value => ApplyDraft(draft => draft with { FrameAppearance = draft.FrameAppearance with { BorderThickness = value } })),
                SampleSettings.Slider("本文の文字サイズ", defaults.TextStyle.FontSize, 8, 96, 1, true,
                    value => ApplyDraft(draft => draft with { TextStyle = draft.TextStyle with { FontSize = value } })),
                SampleSettings.Text("ログ接頭辞", "[Annotation]", value => layer.LogPrefix = value),
            },
        };

        // ── API操作（対象コントロールのメソッド実行） ──
        var methods = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Buttons(
                    ("枠線を追加", () => layer.AddFrame(new EsRect(120, 320, 300, 200))),
                    ("吹き出しを追加", () => layer.AddBubble(new EsRect(300, 80, 320, 140), "新しい吹き出し")),
                    ("本文を編集", () => layer.BeginTextEditing()),
                    ("元に戻す", () => layer.Undo()),
                    ("やり直す", () => layer.Redo()),
                    ("削除", () => layer.DeleteSelected())),
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

        layer.SelectionChanged += (_, _) => _status.Text = layer.Selected is { } selected
            ? $"選択: {selected.Id}"
            : "選択なし";
        layer.DocumentChanged += (_, _) => _status.Text = $"{layer.Document.Items.Length} 件の注釈";

        var content = new ScrollViewer { Content = editor };
        var window = SampleShell.Create(Title, properties, methods, samples, content, new SampleLogPanel(_logging.Buffer));
        Content = window.Content;
        Closed += (_, _) =>
        {
            _logging.Dispose();
            _image.Dispose();
        };

        return;

        // 新規作成時の既定値を変える（既存の注釈は変わらない）
        void ApplyDraft(Func<AnnotationDraftDefaults, AnnotationDraftDefaults> update)
        {
            defaults = update(defaults);
            layer.DraftDefaults = defaults;
        }
    }
}
