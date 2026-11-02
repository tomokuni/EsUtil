using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.WinUI3.SampleKit;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.ColorSelector.Sample.Views;

/// <summary>色選択コントロールの動作確認ウィンドウ。</summary>
/// <remarks>
/// <b>確認できること</b>: 色名・16 進・不透明度 % の入力、一覧からの選択、カラーピッカー、入力の検証、
/// 並び順の切り替え、保存値（<c>#AARRGGBB</c>）の受け取り。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 入力欄とプレビュー / 右下: ログ）を使う。
/// </remarks>
public sealed class MainWindow : Window
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

    /// <summary>ログ（Serilog + 非同期表示）。</summary>
    private readonly SampleLogging _logging = new();

    /// <summary>各項目の保存値。</summary>
    private readonly string[] _values = [.. s_items.Select(static item => item.Initial)];

    /// <summary>各項目のコントロール。</summary>
    private readonly ColorSelectorControl[] _controls = new ColorSelectorControl[s_items.Length];

    /// <summary>プレビュー。</summary>
    private readonly Border _preview = new()
    {
        Height = 64,
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x80, 0x80, 0x80)),
    };

    /// <summary>保存値の表示。</summary>
    private readonly TextBlock _status = new();

    /// <summary>エラーの表示。</summary>
    private readonly TextBlock _error = new()
    {
        FontSize = 11,
        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xD0, 0x20, 0x20)),
    };

    /// <summary>ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        // 「選択色」は 1 番目のコントロールの状態を映す（利用者がコントロール側で色を変えたら表示も追随する）。
        // 行から設定した値を書き戻すと入力中の文字が置き換わるため、その往復だけは反映しない
        var applyingFromRow = false;
        SampleField? selectedColorRow = null;

        var rows = new StackPanel { Spacing = 8 };

        for (var i = 0; i < s_items.Length; i++)
        {
            var (label, initial) = s_items[i];
            var control = new ColorSelectorControl
            {
                SelectedColor = initial,
                Width = RowWidth,
                Log = logger,
                LogPrefix = "[ColorSelector]",
            };
            _controls[i] = control;
            var index = i;
            control.SelectedColorChanged += (_, _) =>
            {
                _values[index] = control.SelectedColor;
                if (index == 0 && !applyingFromRow)
                {
                    selectedColorRow?.SetText(control.SelectedColor);
                }

                UpdateStatus();
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = LabelWidth,
                VerticalAlignment = VerticalAlignment.Center,
            });
            row.Children.Add(control);
            rows.Children.Add(row);
        }

        // 「選択色」は 1 番目のコントロールの状態を映す（利用者がコントロール側で色を変えたら表示も追随する）。
        // 行から設定した値を書き戻すと入力中の文字が置き換わるため、その往復だけは反映しない。
        // 対象のコントロールを作った後で行を作る（初期値の適用先が存在する状態にする）
        selectedColorRow = SampleSettings.TextRow("選択色", s_items[0].Initial, value =>
        {
            applyingFromRow = true;
            try
            {
                _controls[0].SelectedColor = value;
            }
            finally
            {
                applyingFromRow = false;
            }
        });
        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                selectedColorRow.Element,
                SampleSettings.Choice("並び順", ["色相順", "色名順"], 0, index =>
                {
                    var mode = index == 1 ? ColorSortMode.ByName : ColorSortMode.ByHue;
                    foreach (var control in _controls)
                    {
                        control.ColorSortMode = mode;
                    }
                }),
                SampleSettings.Choice("入力欄の表記", ["色名 + 不透明度", "#AARRGGBB"], 0, index =>
                {
                    var format = index == 1 ? ColorTextFormat.HexCode : ColorTextFormat.NameWithAlpha;
                    foreach (var control in _controls)
                    {
                        control.TextFormat = format;
                    }
                }),
                SampleSettings.Check("テーマの色を並べる", true, value =>
                {
                    foreach (var control in _controls)
                    {
                        control.ShowThemeColors = value;
                    }
                }),
                SampleSettings.Slider("文字サイズ", 12, 8, 24, 1, true, value =>
                {
                    foreach (var control in _controls)
                    {
                        control.TextFontSize = value;
                    }
                }),
                SampleSettings.Check("入力が無効なら枠線を赤くする", false, value =>
                {
                    foreach (var control in _controls)
                    {
                        control.HighlightBorderOnError = value;
                    }
                }),
                SampleSettings.Text("ログ接頭辞", "[ColorSelector]", value =>
                {
                    foreach (var control in _controls)
                    {
                        control.LogPrefix = value;
                    }
                }),
            },
        };

        // ── サンプル設定（サンプル固有の操作） ──
        var samples = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.LogLevel(_logging),
                SampleSettings.Buttons(("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
                new TextBlock
                {
                    Text = "入力できる形: 色名（Red）/ 16 進（#RRGGBB・#AARRGGBB）/ 不透明度（Red+50%）。 無効な入力はフォーカスを外すと戻ります。",
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                },
                SampleSettings.Buttons(("保存値を初期値へ戻す", ResetValues)),
                _status,
                _error,
            },
        };

        var content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "色選択コントロール（WinUI 3）", FontSize = 16 },
                    new Border
                    {
                        Child = rows,
                        Padding = new Thickness(12),
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x50, 0x50, 0x50)),
                    },
                    new TextBlock { Text = "プレビュー（枠線 = 1 番目 / 背景 = 2 番目の色）", FontSize = 11 },
                    _preview,
                },
            },
        };

        var log = new SampleLogPanel(_logging.Buffer);
        Title = "ColorSelectorControl サンプル（WinUI 3）";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
        Content = SampleShell.CreateContent(properties, methodSettings: null, samples, content, log);
        Closed += (_, _) => _logging.Dispose();

        UpdateStatus();
    }

    /// <summary>保存値を初期値へ戻す。</summary>
    private void ResetValues()
    {
        for (var i = 0; i < s_items.Length; i++)
        {
            _values[i] = s_items[i].Initial;
            _controls[i].SelectedColor = s_items[i].Initial;
        }

        UpdateStatus();
    }

    /// <summary>状態の表示とプレビューを更新する。</summary>
    private void UpdateStatus()
    {
        _status.Text = "保存値: " + string.Join(" / ", _values);
        _error.Text = _values.Any(value => !ColorValidation.IsValid(value))
            ? "無効な入力があります（保存前に直してください）。"
            : string.Empty;

        if (ColorString.TryParse(_values[0], out var border, out _))
        {
            _preview.BorderBrush = new SolidColorBrush(ToWinUI(border));
        }

        if (ColorString.TryParse(_values[1], out var background, out _))
        {
            _preview.Background = new SolidColorBrush(ToWinUI(background));
        }
    }

    /// <summary>コアの色を WinUI の色へ写す。</summary>
    /// <param name="color">コアの色。</param>
    /// <returns>WinUI の色。</returns>
    private static Windows.UI.Color ToWinUI(DrawingColor color)
        => Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
}
