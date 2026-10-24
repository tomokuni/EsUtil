using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.ColorComboBox.Sample.Views;

/// <summary>色選択コントロールの動作確認ウィンドウ。</summary>
/// <remarks>
/// <b>確認できること</b>: 色名・16 進・不透明度 % の入力、一覧からの選択、カラーピッカー、入力の検証、
/// 並び順の切り替え、保存値（<c>#AARRGGBB</c>）の受け取り。<br/>
/// コントロールは XAML から置かない（コードビハインドで生成する）。
/// </remarks>
public sealed partial class MainWindow : Window
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

    /// <summary>各項目の保存値。</summary>
    private readonly string[] _values = new string[s_items.Length];

    /// <summary>生成したコントロール（並び順の切り替えに使う）。</summary>
    private readonly ColorComboBoxControl[] _controls = new ColorComboBoxControl[s_items.Length];

    /// <summary>ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        InitializeComponent();

        for (var i = 0; i < s_items.Length; i++)
        {
            var (label, initial) = s_items[i];
            _values[i] = initial;

            var control = new ColorComboBoxControl { SelectedColor = initial, Width = RowWidth };
            _controls[i] = control;
            var index = i;
            control.SelectedColorChanged += (_, _) =>
            {
                _values[index] = control.SelectedColor;
                UpdateStatus();
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = label, Width = LabelWidth, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(control);
            Rows.Children.Add(row);
        }

        SortMode.Items.Add("色相順");
        SortMode.Items.Add("色名順");
        SortMode.SelectedIndex = 0;
        SortMode.SelectionChanged += (_, _) =>
        {
            var mode = SortMode.SelectedIndex == 1 ? ColorSortMode.ByName : ColorSortMode.ByHue;
            foreach (var control in _controls)
            {
                control.ColorSortMode = mode;
            }
        };

        UpdateStatus();
    }

    /// <summary>状態の表示とプレビューを更新する。</summary>
    private void UpdateStatus()
    {
        Status.Text = "保存値: " + string.Join(" / ", _values);
        Error.Text = _values.Any(value => !ColorValidation.IsValid(value))
            ? "無効な入力があります（保存前に直してください）。"
            : string.Empty;

        if (ColorString.TryParse(_values[0], out var border, out _))
        {
            Preview.BorderBrush = new SolidColorBrush(ToWinUI(border));
        }

        if (ColorString.TryParse(_values[1], out var background, out _))
        {
            Preview.Background = new SolidColorBrush(ToWinUI(background));
        }
    }

    /// <summary>コアの色を WinUI の色へ写す。</summary>
    /// <param name="color">コアの色。</param>
    /// <returns>WinUI の色。</returns>
    private static Windows.UI.Color ToWinUI(DrawingColor color)
        => Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
}