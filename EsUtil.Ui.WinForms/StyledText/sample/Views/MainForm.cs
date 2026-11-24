using EsUtil.Ui.WinForms.SampleKit;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.WinForms.StyledText.Sample.Views;

/// <summary>サンプルのメイン フォーム（<see cref="StyledTextControl"/> の使い方を示す）。</summary>
/// <remarks>
/// <b>示すこと</b>: 本文を入力欄で覆わず、その場で表示・編集できること。 すべての操作
/// （キャレット・選択・入力・削除・書式・クリップボード）を API から行えること。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 本文 / 右下: ログ）を使う。
/// </remarks>
public static class MainForm
{
    /// <summary>サンプル フォームを作る。</summary>
    /// <returns>作ったフォーム。</returns>
    public static Form Create()
    {
        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var log = new SampleLogPanel(logging.Buffer);
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        // 本文は run 単位の書式で持つ（色・太字を混ぜられる）。 本文はコントロールが単一所有する
        var editor = new StyledTextControl
        {
            RichTextValue = RichText.Create("ここが重要です。\n文字単位の書式は API でも変えられます。", TextDefaults.DefaultStyle)
                .ApplyStyle(6, 4, TextDefaults.DefaultStyle with
                {
                    IsBold = true,
                    Color = System.Drawing.Color.FromArgb(255, 200, 0, 0),
                }),
            LineHeightFactor = 1.2,
            Dock = DockStyle.Fill,
        };
        editor.RichTextChanged += (_, _) =>
        {
            logger.LogInformation("本文が変わりました。");
        };
        editor.SelectionChanged += (_, _) =>
        {
            logger.LogDebug("選択が変わりました。");
        };
        editor.EditingChanged += (_, _) =>
        {
            logger.LogInformation("編集の状態が変わりました。");
        };

        var content = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
        content.Controls.Add(editor);

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        properties.Controls.Add(SampleSettings.Slider("行間", 1.2, 0.8, 2.0, 0.1, true, value => editor.LineHeightFactor = value));
        properties.Controls.Add(SampleSettings.Slider("表示倍率", 1.0, 0.5, 2.0, 0.1, true, value => editor.RenderScale = value));
        properties.Controls.Add(SampleSettings.Slider("文字サイズ", 20, 8, 48, 1, true, value =>
            editor.ApplyTextStyle(0, 0, TextDefaults.DefaultStyle with { FontSize = value })));

        // ── API操作（対象コントロールのメソッド実行） ──
        var methods = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        methods.Controls.Add(SampleSettings.Buttons(
            ("編集を開始", () => editor.BeginEditing()),
            ("編集を終了", editor.EndEditing),
            ("すべて選択", editor.SelectAll)));
        methods.Controls.Add(SampleSettings.Buttons(
            ("太字の切替", () => editor.TransformTextStyle(static style => style with { IsBold = !style.IsBold })),
            ("斜体の切替", () => editor.TransformTextStyle(static style => style with { IsItalic = !style.IsItalic })),
            ("下線の切替", () => editor.TransformTextStyle(static style => style with { IsUnderline = !style.IsUnderline }))));
        methods.Controls.Add(SampleSettings.Buttons(
            ("取り消し線の切替", () => editor.TransformTextStyle(static style => style with { IsStrike = !style.IsStrike })),
            ("文字色を変える", () => editor.ApplyTextStyle(
                TextDefaults.DefaultStyle with { Color = System.Drawing.Color.FromArgb(255, 0, 90, 200) })),
            ("書式を戻す", () => editor.ApplyTextStyle(TextDefaults.DefaultStyle))));
        methods.Controls.Add(SampleSettings.Buttons(
            ("選択をコピー", editor.Copy),
            ("選択を切り取り", editor.Cut),
            ("貼り付け", editor.Paste)));

        // ── サンプル設定（サンプルアプリの外観と状態） ──
        var samples = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        samples.Controls.Add(SampleSettings.LogLevel(logging));
        samples.Controls.Add(SampleSettings.Buttons(
            ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）")),
            ("本文を初期化", () =>
            {
                editor.EndEditing();
                editor.RichTextValue = RichText.Create("ここが重要です。", TextDefaults.DefaultStyle);
            }
        )));

        return SampleShell.Create(
            "StyledText サンプル (WinForms)", properties, methods, samples, content, log);
    }
}
