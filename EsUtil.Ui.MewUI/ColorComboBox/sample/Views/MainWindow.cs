using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Palette;
using DrawingColor = System.Drawing.Color;
using MewColor = Aprillz.MewUI.Color;

namespace EsUtil.Ui.MewUI.ColorComboBox.Sample.Views;

/// <summary>色選択コントロールの動作確認ウィンドウを組み立てる。</summary>
/// <remarks>
/// <b>確認できること</b>: 色名・16 進・不透明度 % の入力、一覧からの選択、カラーピッカー、入力の検証、
/// 並び順の切り替え、保存値（<c>#AARRGGBB</c>）の受け取り。<br/>
/// 設定画面と同じ形（ラベルとコントロールを並べる）にしてあるため、そのまま流用の参考になる。
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
        var preview = new Border
        {
            Height = 64,
            BorderThickness = 1,
            BorderBrush = MewColor.FromRgb(0x80, 0x80, 0x80),
        };
        var status = new TextBlock().Text(string.Empty);
        var error = new TextBlock().Text(string.Empty).FontSize(11);
        error.Foreground = MewColor.FromRgb(0xD0, 0x20, 0x20);

        var values = new string[s_items.Length];
        var controls = new ColorComboBoxControl[s_items.Length];
        var rows = new StackPanel().Vertical().Spacing(8);

        for (var i = 0; i < s_items.Length; i++)
        {
            var (label, initial) = s_items[i];
            values[i] = initial;

            var control = new ColorComboBoxControl { SelectedColor = initial, Width = RowWidth };
            controls[i] = control;
            var index = i;
            control.SelectedColorChanged += (_, _) =>
            {
                values[index] = control.SelectedColor;
                UpdateStatus(status, error, values, preview);
            };

            rows.Children(LabelRow(label, control));
        }

        var sortMode = new ComboBox().Items(["色相順", "色名順"]);
        sortMode.SelectedIndex = 0;
        sortMode.SelectionChanged += _ =>
        {
            var mode = sortMode.SelectedIndex == 1 ? ColorSortMode.ByName : ColorSortMode.ByHue;
            foreach (var control in controls)
            {
                control.ColorSortMode = mode;
            }
        };

        var sortRow = new StackPanel().Horizontal().Spacing(8).Children(
            new TextBlock().Text("一覧の並び順").Width(LabelWidth),
            sortMode);

        var textFormat = new ComboBox().Items(["色名 + 不透明度", "#AARRGGBB"]);
        textFormat.SelectedIndex = 0;
        textFormat.SelectionChanged += _ =>
        {
            var format = textFormat.SelectedIndex == 1 ? ColorTextFormat.HexCode : ColorTextFormat.NameWithAlpha;
            foreach (var control in controls)
            {
                control.TextFormat = format;
            }
        };

        var formatRow = new StackPanel().Horizontal().Spacing(8).Children(
            new TextBlock().Text("入力欄の表記").Width(LabelWidth),
            textFormat);

        var body = new StackPanel().Vertical().Spacing(12).Children(
            new TextBlock().Text("色選択コントロール（MewUI）").FontSize(16),
            new TextBlock()
                .Text("入力できる形: 色名（Red）/ 16 進（#RRGGBB・#AARRGGBB）/ 不透明度（Red+50%）。 無効な入力はフォーカスを外すと戻ります。")
                .FontSize(11),
            new Border
            {
                Child = rows,
                Padding = new Thickness(12),
                BorderThickness = 1,
                BorderBrush = MewColor.FromRgb(0x50, 0x50, 0x50),
            },
            sortRow,
            formatRow,
            new TextBlock().Text("プレビュー（枠線 = 1 番目 / 背景 = 2 番目の色）").FontSize(11),
            preview,
            status,
            error);

        var window = new Window()
            .Title("ColorComboBoxControl サンプル（MewUI）")
            .Resizable(760, 620, minWidth: 640, minHeight: 400);
        window.Content(new ScrollViewer().Content(new Border { Child = body, Padding = new Thickness(16) }));

        UpdateStatus(status, error, values, preview);
        return window;
    }

    /// <summary>ラベルとコントロールを横に並べる。</summary>
    /// <param name="label">ラベル。</param>
    /// <param name="control">コントロール。</param>
    /// <returns>並べた要素。</returns>
    private static StackPanel LabelRow(string label, ColorComboBoxControl control)
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
