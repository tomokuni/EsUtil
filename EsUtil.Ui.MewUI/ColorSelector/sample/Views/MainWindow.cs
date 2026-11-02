using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.MewUI.SampleKit;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;
using MewColor = Aprillz.MewUI.Color;

namespace EsUtil.Ui.MewUI.ColorSelector.Sample.Views;

/// <summary>色選択コントロールの動作確認ウィンドウを組み立てる。</summary>
/// <remarks>
/// <b>確認できること</b>: 色名・16 進・不透明度 % の入力、一覧からの選択、カラーピッカー、入力の検証、
/// 並び順の切り替え、保存値（<c>#AARRGGBB</c>）の受け取り。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 入力欄とプレビュー / 右下: ログ）を使う。
/// </remarks>
internal static class MainWindow
{
    /// <summary>コントロールの幅（DIP）。</summary>
    private const double RowWidth = 320;

    /// <summary>ラベルの幅（DIP）。</summary>
    private const double LabelWidth = 120;

    /// <summary>一覧に出す設定項目（ラベルと初期値）。</summary>
    private static readonly (string Label, string Initial)[] s_items =
    [
        ("枠線の色", "#FFA9A9A9"),
        ("背景の色", "#FF202020"),
        ("テキストの色", "White"),
        ("強調の色", "Orange+75%"),
    ];

    /// <summary>ウィンドウを組み立てる。</summary>
    /// <returns>生成したウィンドウ。</returns>
    public static Window Create()
    {
        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var log = new SampleLogPanel(logging.Buffer);
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        var preview = new Border
        {
            Height = 64,
            BorderThickness = 1,
            BorderBrush = MewColor.FromRgb(0x80, 0x80, 0x80),
        };
        var status = new TextBlock().Text(string.Empty).FontSize(11);
        var error = new TextBlock().Text(string.Empty).FontSize(11);
        error.Foreground = MewColor.FromRgb(0xD0, 0x20, 0x20);

        // 「選択色」は 1 番目のコントロールの状態を映す（利用者がコントロール側で色を変えたら表示も追随する）。
        // 行から設定した値を書き戻すと入力中の文字が置き換わるため、その往復だけは反映しない
        var applyingFromRow = false;
        SampleField? selectedColorRow = null;

        var values = new string[s_items.Length];
        var controls = new ColorSelectorControl[s_items.Length];
        var rows = new StackPanel().Vertical().Spacing(8);

        for (var i = 0; i < s_items.Length; i++)
        {
            var (label, initial) = s_items[i];
            values[i] = initial;

            var control = new ColorSelectorControl
            {
                SelectedColor = initial,
                Width = RowWidth,
                Log = logger,
                LogPrefix = "[ColorSelector]",
            };
            controls[i] = control;
            var index = i;
            control.SelectedColorChanged += (_, _) =>
            {
                values[index] = control.SelectedColor;
                if (index == 0 && !applyingFromRow)
                {
                    selectedColorRow?.SetText(control.SelectedColor);
                }

                UpdateStatus(status, error, values, preview);
            };

            rows.Children(LabelRow(label, control));
        }

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        selectedColorRow = SampleSettings.TextRow("選択色", s_items[0].Initial, value =>
        {
            applyingFromRow = true;
            try
            {
                controls[0].SelectedColor = value;
            }
            finally
            {
                applyingFromRow = false;
            }
        });

        var properties = new StackPanel().Vertical().Spacing(6).Children(
            selectedColorRow.Element,
            SampleSettings.Choice(
                "並び順",
                ["色相順", "色名順"],
                0,
                index =>
                {
                    var mode = index == 1 ? ColorSortMode.ByName : ColorSortMode.ByHue;
                    foreach (var control in controls)
                    {
                        control.ColorSortMode = mode;
                    }
                }),
            SampleSettings.Choice(
                "入力欄の表記",
                ["色名 + 不透明度", "#AARRGGBB"],
                0,
                index =>
                {
                    var format = index == 1 ? ColorTextFormat.HexCode : ColorTextFormat.NameWithAlpha;
                    foreach (var control in controls)
                    {
                        control.TextFormat = format;
                    }
                }),
            SampleSettings.Check("テーマの色を並べる", true, value =>
            {
                foreach (var control in controls)
                {
                    control.ShowThemeColors = value;
                }
            }),
            SampleSettings.Slider("文字サイズ", 12, 8, 24, 1, true, value =>
            {
                foreach (var control in controls)
                {
                    control.TextFontSize = value;
                }
            }),
            SampleSettings.Check("入力が無効なら枠線を赤くする", false, value =>
            {
                foreach (var control in controls)
                {
                    control.HighlightBorderOnError = value;
                }
            }),
            SampleSettings.Text("ログ接頭辞", "[ColorSelector]", value =>
            {
                foreach (var control in controls)
                {
                    control.LogPrefix = value;
                }
            }));

        // ── サンプル設定（サンプル固有の操作） ──
        var samples = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.LogLevel(logging),
            SampleSettings.Buttons(
                ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
            new TextBlock
            {
                Text = "入力できる形: 色名（Red）/ 16 進（#RRGGBB・#AARRGGBB）/ 不透明度（Red+50%）。 無効な入力はフォーカスを外すと戻ります。",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
            },
            SampleSettings.Buttons(("保存値を初期値へ戻す", ResetValues)),
            status,
            error);

        var content = new StackPanel().Vertical().Spacing(12).Children(
            new TextBlock().Text("色選択コントロール（MewUI）").FontSize(16),
            new Border
            {
                Child = rows,
                Padding = new Thickness(12),
                BorderThickness = 1,
                BorderBrush = MewColor.FromRgb(0x50, 0x50, 0x50),
            },
            new TextBlock().Text("プレビュー（枠線 = 1 番目 / 背景 = 2 番目の色）").FontSize(11),
            preview);

        var window = SampleShell.Create(
            "ColorSelector サンプル (MewUI)",
            properties,
            methodSettings: null,
            samples,
            new ScrollViewer().Content(content),
            log);
        window.Closed += () => logging.Dispose();

        UpdateStatus(status, error, values, preview);
        return window;

        // 保存値を初期値へ戻す
        void ResetValues()
        {
            for (var i = 0; i < s_items.Length; i++)
            {
                values[i] = s_items[i].Initial;
                controls[i].SelectedColor = s_items[i].Initial;
            }

            UpdateStatus(status, error, values, preview);
        }
    }

    /// <summary>ラベルとコントロールを横に並べる。</summary>
    /// <param name="label">ラベル。</param>
    /// <param name="control">コントロール。</param>
    /// <returns>並べた要素。</returns>
    private static StackPanel LabelRow(string label, ColorSelectorControl control)
        => new StackPanel().Horizontal().Spacing(8).Children(
            new TextBlock().Text(label).Width(LabelWidth).VerticalAlignment(VerticalAlignment.Center),
            control);

    /// <summary>状態の表示とプレビューを更新する。</summary>
    /// <param name="status">保存値の表示。</param>
    /// <param name="error">エラーの表示。</param>
    /// <param name="values">各項目の保存値。</param>
    /// <param name="preview">プレビュー。</param>
    private static void UpdateStatus(TextBlock status, TextBlock error, string[] values, Border preview)
    {
        status.Text = "保存値: " + string.Join(" / ", values);
        error.Text = values.Any(value => !ColorValidation.IsValid(value))
            ? "無効な入力があります（保存前に直してください）。"
            : string.Empty;

        if (ColorString.TryParse(values[0], out var border, out _))
        {
            preview.BorderBrush = ToMew(border);
        }

        if (ColorString.TryParse(values[1], out var background, out _))
        {
            preview.Background = ToMew(background);
        }
    }

    /// <summary>コアの色を MewUI の色へ写す。</summary>
    /// <param name="color">コアの色。</param>
    /// <returns>MewUI の色。</returns>
    private static MewColor ToMew(DrawingColor color) => MewColor.FromArgb(color.A, color.R, color.G, color.B);
}
