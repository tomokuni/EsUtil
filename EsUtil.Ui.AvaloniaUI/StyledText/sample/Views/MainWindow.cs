using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Ui.AvaloniaUI.SampleKit;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.AvaloniaUI.StyledText.Sample.Views;

/// <summary>サンプルのメイン ウィンドウ（<see cref="StyledTextControl"/> の使い方を示す）。</summary>
/// <remarks>
/// <b>示すこと</b>: 本文を入力欄で覆わず、その場で表示・編集できること。 すべての操作
/// （キャレット・選択・入力・削除・書式・クリップボード）を API から行えること。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 本文 / 右下: ログ）を使う。
/// </remarks>
public sealed class MainWindow : Window
{
    /// <summary>本文を表示・編集するコントロール。</summary>
    private readonly StyledTextControl _editor = new();

    /// <summary>ログ（Serilog + 非同期表示）。</summary>
    private readonly SampleLogging _logging = new();

    /// <summary>メイン ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        // 本文は run 単位の書式で持つ（色・太字を混ぜられる）。 本文はコントロールが単一所有する
        _editor.Text = RichText.Create("ここが重要です。\n文字単位の書式は API でも変えられます。", TextDefaults.DefaultStyle)
            .ApplyStyle(6, 4, TextDefaults.DefaultStyle with
            {
                IsBold = true,
                Color = System.Drawing.Color.FromArgb(255, 200, 0, 0),
            });
        _editor.LineHeightFactor = 1.2;
        _editor.TextChanged += (_, _) =>
        {
            logger.LogInformation("本文が変わりました。");
        };
        _editor.SelectionChanged += (_, _) =>
        {
            logger.LogDebug("選択が変わりました。");
        };
        _editor.EditingChanged += (_, _) =>
        {
            logger.LogInformation("編集の状態が変わりました。");
        };

        var content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0)),
            BorderThickness = new Thickness(1),
            Child = _editor,
        };

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Slider("行間", 1.2, 0.8, 2.0, 0.1, true, value => _editor.LineHeightFactor = value),
                SampleSettings.Slider("表示倍率", 1.0, 0.5, 2.0, 0.1, true, value => _editor.RenderScale = value),
                SampleSettings.Slider("文字サイズ", 20, 8, 48, 1, true, value =>
                    _editor.ApplyTextStyle(0, 0, TextDefaults.DefaultStyle with { FontSize = value })),
            },
        };

        // ── API操作（対象コントロールのメソッド実行） ──
        var methods = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Buttons(
                    ("編集を開始", () => _editor.BeginEditing()),
                    ("編集を終了", () => _editor.EndEditing()),
                    ("すべて選択", () => _editor.SelectAll())),
                SampleSettings.Buttons(
                    ("太字の切替", () => _editor.TransformTextStyle(static style => style with { IsBold = !style.IsBold })),
                    ("斜体の切替", () => _editor.TransformTextStyle(static style => style with { IsItalic = !style.IsItalic })),
                    ("下線の切替", () => _editor.TransformTextStyle(static style => style with { IsUnderline = !style.IsUnderline }))),
                SampleSettings.Buttons(
                    ("取り消し線の切替", () => _editor.TransformTextStyle(static style => style with { IsStrike = !style.IsStrike })),
                    ("文字色を変える", () => _editor.ApplyTextStyle(
                        TextDefaults.DefaultStyle with { Color = System.Drawing.Color.FromArgb(255, 0, 90, 200) })),
                    ("書式を戻す", () => _editor.ApplyTextStyle(TextDefaults.DefaultStyle))),
                SampleSettings.Buttons(
                    ("選択をコピー", () => _ = _editor.CopyAsync()),
                    ("選択を切り取り", () => _ = _editor.CutAsync()),
                    ("貼り付け", () => _ = _editor.PasteAsync())),
            },
        };

        // ── サンプル設定（サンプルアプリの外観と状態） ──
        var samples = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.LogLevel(_logging),
                SampleSettings.Buttons(
                    ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）")),
                    ("本文を初期化", ResetText)),
            },
        };

        var log = new SampleLogPanel(_logging.Buffer);
        var window = SampleShell.Create(
            "StyledText サンプル (AvaloniaUI)", properties, methods, samples, content, log);
        Content = window.Content;
        Title = window.Title;
        Width = window.Width;
        Height = window.Height;
        MinWidth = window.MinWidth;
        MinHeight = window.MinHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Closed += (_, _) => _logging.Dispose();
        return;

        // 本文を先頭の 1 行へ戻す（編集の状態も捨てる）
        void ResetText()
        {
            _editor.EndEditing();
            _editor.Text = RichText.Create("ここが重要です。", TextDefaults.DefaultStyle);
        }
    }
}
