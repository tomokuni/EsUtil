using EsUtil.Platform.Core.Palette;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.ColorComboBox;

/// <summary>色を選ぶコントロール（色名の入力・一覧からの選択・カラーピッカー）。</summary>
/// <remarks>
/// <b>規則の所有者はコアである</b>: 受理する入力（色名・16 進・<c>+{n}%</c>）、色名の表、保存値（<c>#AARRGGBB</c>）と
/// 画面表示（<c>{色}+{不透明度%}</c>）、並び順、入力の妥当性は
/// <see cref="EsUtil.Platform.Core.Palette"/>（<see cref="ColorString"/> / <see cref="ColorEntry"/> /
/// <see cref="ColorValidation"/>）が単一所有する。 本クラスは WinUI の要素の組み立てと、入力と状態の同期だけを行う。<br/>
/// <b>Native AOT の制約</b>: 一覧へは <c>Items.Add</c> で 1 件ずつ渡す（<c>ItemsSource</c> へ managed コレクションを
/// 渡すと AOT で <c>E_INVALIDARG</c> になる。<c>.github/ビルド設定規約.md</c> §6.1）。<br/>
/// <b>構成</b>: 1 行に「スウォッチ」「テキスト入力」「一覧ボタン（▼）」「カラーピッカー ボタン（…）」を置き、
/// 一覧とピッカーは <see cref="Flyout"/> で開く。
/// </remarks>
public sealed partial class ColorComboBoxControl : Grid
{
    /// <summary>スウォッチの一辺（DIP）。</summary>
    private const double SwatchSize = 16.0;

    /// <summary>テキスト入力の最小幅（DIP）。</summary>
    private const double TextBoxMinWidth = 120.0;

    /// <summary>現在の色を示すスウォッチ。</summary>
    private readonly Border _swatch;

    /// <summary>色文字列の入力欄。</summary>
    private readonly TextBox _text = new() { MinWidth = TextBoxMinWidth, Text = "White" };

    /// <summary>色の一覧を開くボタン。</summary>
    private readonly Button _listButton = new() { Content = "▼" };

    /// <summary>カラーピッカーを開くボタン。</summary>
    private readonly Button _pickerButton = new() { Content = "…" };

    /// <summary>一覧（ListView）。 項目は 1 件ずつ追加する（AOT の制約）。</summary>
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single };

    /// <summary>一覧に並べた色（添字は <see cref="_list"/> の項目と一致する）。</summary>
    private IReadOnlyList<ColorEntry> _entries = [];

    /// <summary>カラーピッカー。</summary>
    private readonly ColorPicker _picker = new()
    {
        IsAlphaEnabled = true,
        ColorSpectrumShape = ColorSpectrumShape.Box,
        IsColorChannelTextInputVisible = true,
        IsAlphaTextInputVisible = true,
    };

    /// <summary>一覧のポップアップ。</summary>
    private readonly Flyout _listFlyout = new() { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };

    /// <summary>ピッカーのポップアップ。</summary>
    private readonly Flyout _pickerFlyout = new() { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };

    /// <summary>入力と状態の往復でイベントが循環するのを防ぐ。</summary>
    private bool _applying;

    /// <summary>内部ロガー。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞。</summary>
    private string? _logPrefix;

    /// <summary>現在の色（保存値）。</summary>
    private string _selectedColor = "#FFFFFFFF";

    /// <summary>一覧の並び順。</summary>
    private ColorSortMode _colorSortMode = ColorSortMode.ByHue;

    /// <summary>選択中の色（保存値）が変わったときに発生する。</summary>
    public event EventHandler? SelectedColorChanged;

    /// <summary>コントロールを生成する。</summary>
    public ColorComboBoxControl()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnSpacing = 6;

        _swatch = new Border
        {
            Width = SwatchSize,
            Height = SwatchSize,
            BorderBrush = new SolidColorBrush(Colors.Gray),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Colors.White),
        };
        SetColumn(_swatch, 0);
        Children.Add(_swatch);

        _text.TextChanged += OnTextChanged;
        _text.LostFocus += OnTextLostFocus;
        SetColumn(_text, 1);
        Children.Add(_text);

        _listButton.Click += (_, _) => _listFlyout.ShowAt(_listButton);
        SetColumn(_listButton, 2);
        Children.Add(_listButton);

        _pickerButton.Click += (_, _) => ShowPicker();
        SetColumn(_pickerButton, 3);
        Children.Add(_pickerButton);

        _list.SelectionChanged += OnListSelectionChanged;
        _listFlyout.Content = new ScrollViewer { Content = _list, MaxHeight = 360 };

        BuildPickerFlyout();

        RefreshList();
        ApplyColor(_selectedColor, raiseChanged: false);
    }

    /// <summary>選択中の色（保存値）を取得または設定する。</summary>
    /// <value><c>#AARRGGBB</c>（アルファが先頭）の保存値。 色名や <c>+{n}%</c> を設定すると正規化される。</value>
    public string SelectedColor
    {
        get => _selectedColor;
        set => ApplyColor(value, raiseChanged: true);
    }

    /// <summary>一覧の並び順を取得または設定する。</summary>
    /// <value>色名順（<see cref="ColorSortMode.ByName"/>）または色相順（<see cref="ColorSortMode.ByHue"/>）。 既定は色相順。</value>
    public ColorSortMode ColorSortMode
    {
        get => _colorSortMode;
        set
        {
            if (_colorSortMode == value)
            {
                return;
            }

            _colorSortMode = value;
            RefreshList();
        }
    }

    /// <summary>バリデーション エラーがあるかを取得する。</summary>
    /// <value>現在の入力が無効な場合 true。</value>
    public bool HasValidationError { get; private set; }

    /// <summary>バリデーション エラーのメッセージを取得する。</summary>
    /// <value>無効な場合の理由。 有効な場合は null。</value>
    public string? ValidationErrorMessage { get; private set; }

    /// <summary>内部ロガーを取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は <see cref="NullLogger"/> を使う。</value>
    public ILogger Log
    {
        get => _log;
        set => _log = value ?? NullLogger.Instance;
    }

    /// <summary>ログの接頭辞を取得または設定する。</summary>
    /// <value>接頭辞。 null の場合は既定値（<c>[ColorComboBox]</c>）を使う。</value>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            LogMessage.Prefix = value ?? LogMessage.DefaultPrefix;
        }
    }

    /// <summary>ピッカーのポップアップを組み立てる。</summary>
    private void BuildPickerFlyout()
    {
        var ok = new Button { Content = "OK" };
        ok.Click += (_, _) => ClosePicker(apply: true);
        var cancel = new Button { Content = "キャンセル" };
        cancel.Click += (_, _) => ClosePicker(apply: false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var body = new StackPanel { Spacing = 8, Width = 320 };
        body.Children.Add(_picker);
        body.Children.Add(buttons);
        _pickerFlyout.Content = body;
    }

    /// <summary>色を設定し、入力欄とスウォッチへ反映する。</summary>
    /// <param name="value">設定する色。</param>
    /// <param name="raiseChanged">変更イベントを発火する場合は true。</param>
    private void ApplyColor(string? value, bool raiseChanged)
    {
        var normalized = ColorString.ToArgbHex(value);
        var state = ColorValidation.Determine(value, ColorValidation.IsValid(value));

        _applying = true;
        try
        {
            _text.Text = ColorString.ToDisplayText(normalized);
            if (ColorString.TryParse(normalized, out var color, out _))
            {
                _swatch.Background = new SolidColorBrush(ToWinUIColor(color));
                UpdateSelectedIndex(color);
            }

            HasValidationError = state.HasError;
            ValidationErrorMessage = state.Message;
        }
        finally
        {
            _applying = false;
        }

        _log.LogSettingChanged(nameof(SelectedColor), _selectedColor, normalized);
        var changed = !string.Equals(_selectedColor, normalized, StringComparison.Ordinal);
        _selectedColor = normalized;
        if (changed && raiseChanged)
        {
            SelectedColorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>一覧の選択を現在の色へ合わせる。</summary>
    /// <param name="color">現在の色。</param>
    private void UpdateSelectedIndex(DrawingColor color)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            var candidate = _entries[i].Color;
            if (candidate.R == color.R && candidate.G == color.G && candidate.B == color.B)
            {
                _list.SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>一覧を作り直す（項目は 1 件ずつ追加する）。</summary>
    private void RefreshList()
    {
        _entries = ColorEntry.GetSorted(_colorSortMode);
        _list.Items.Clear();
        foreach (var entry in _entries)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(2),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(ToWinUIColor(entry.Color)),
            });
            row.Children.Add(new TextBlock { Text = entry.Name, VerticalAlignment = VerticalAlignment.Center });
            _list.Items.Add(row);
        }

        if (ColorString.TryParse(_selectedColor, out var color, out _))
        {
            UpdateSelectedIndex(color);
        }
    }

    /// <summary>テキスト入力が変わったときに色を反映する。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_applying)
        {
            return;
        }

        ApplyColor(_text.Text, raiseChanged: true);
    }

    /// <summary>テキスト入力からフォーカスが外れたとき、無効な入力を最後の有効値へ戻す。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnTextLostFocus(object sender, RoutedEventArgs e)
    {
        if (ColorValidation.ShouldRecover(ColorValidation.IsValid(_text.Text), hasLastValid: true))
        {
            ApplyColor(_selectedColor, raiseChanged: false);
        }
    }

    /// <summary>一覧の選択が変わったときに色を反映する。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applying)
        {
            return;
        }

        var index = _list.SelectedIndex;
        if (index >= 0 && index < _entries.Count)
        {
            ApplyColor(_entries[index].Name, raiseChanged: true);
            _listFlyout.Hide();
        }
    }

    /// <summary>ピッカーを開く（現在の色を初期値にする）。</summary>
    private void ShowPicker()
    {
        if (ColorString.TryParse(_selectedColor, out var color, out _))
        {
            _picker.Color = ToWinUIColor(color);
        }

        _pickerFlyout.ShowAt(_pickerButton);
    }

    /// <summary>ピッカーを閉じ、必要なら色を反映する。</summary>
    /// <param name="apply">色を反映する場合は true。</param>
    private void ClosePicker(bool apply)
    {
        if (apply)
        {
            var color = _picker.Color;
            ApplyColor(
                ColorString.ToArgbHex(DrawingColor.FromArgb(color.A, color.R, color.G, color.B)),
                raiseChanged: true);
        }

        _pickerFlyout.Hide();
    }

    /// <summary>コアの色を WinUI の色へ写す。</summary>
    /// <param name="color">コアの色。</param>
    /// <returns>同じ ARGB を持つ色。</returns>
    private static Windows.UI.Color ToWinUIColor(DrawingColor color)
        => Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
}
