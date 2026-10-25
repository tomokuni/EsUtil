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

    /// <summary>スウォッチと枠・文字との間の余白（DIP）。</summary>
    private const double SwatchGap = 6.0;

    /// <summary>区切りの垂直線の幅（DIP）。</summary>
    private const double SeparatorWidth = 1.0;

    /// <summary>区切りの垂直線の上下の余白（DIP。 枠の内側に収める）。</summary>
    private const double SeparatorMargin = 5.0;

    /// <summary>▼ と … の区画の幅（DIP）。</summary>
    private const double SegmentWidth = 26.0;

    /// <summary>テキスト入力の最小幅（DIP）。</summary>
    private const double TextBoxMinWidth = 120.0;

    /// <summary>枠の色（半透明の灰色。 明色・暗色のどちらのテーマでも見える）。</summary>
    private static readonly Windows.UI.Color s_borderColor =
        Windows.UI.Color.FromArgb(0x80, 0x80, 0x80, 0x80);

    /// <summary>区切りの色（枠より薄い）。</summary>
    private static readonly Windows.UI.Color s_separatorColor =
        Windows.UI.Color.FromArgb(0x40, 0x80, 0x80, 0x80);

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

    /// <summary>一覧のポップアップ（アプリのウィンドウ外へはみ出してよい）。</summary>
    private readonly Flyout _listFlyout = new()
    {
        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,

        // ウィンドウの内側へ押し戻すと、狭いウィンドウで一覧が極端に短くなる（はみ出してよい）
        ShouldConstrainToRootBounds = false,
    };

    /// <summary>ピッカーのポップアップ（アプリのウィンドウ外へはみ出してよい。 スクロールさせない）。</summary>
    private readonly Flyout _pickerFlyout = new()
    {
        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,

        // ピッカーは 1 画面に収まる大きさであり、スクロールさせると操作しにくい（はみ出して全体を見せる）。
        // FlyoutPresenter の既定は高さを制限して ScrollViewer を入れるため、上限を外してスクロールを切る
        ShouldConstrainToRootBounds = false,
        FlyoutPresenterStyle = CreatePickerPresenterStyle(),
    };

    /// <summary>入力と状態の往復でイベントが循環するのを防ぐ。</summary>
    private bool _applying;

    /// <summary>入力欄の左余白（スウォッチの分）を足し終えたかどうか。</summary>
    private bool _textPaddingApplied;

    /// <summary>内部ロガー。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞。</summary>
    private string? _logPrefix;

    /// <summary>現在の色（保存値）。</summary>
    private string _selectedColor = "#FFFFFFFF";

    /// <summary>一覧の並び順。</summary>
    private ColorSortMode _colorSortMode = ColorSortMode.ByHue;

    /// <summary>テキスト部の表記。</summary>
    private ColorTextFormat _textFormat = ColorTextFormat.NameWithAlpha;

    /// <summary>選択中の色（保存値）が変わったときに発生する。</summary>
    public event EventHandler? SelectedColorChanged;

    /// <summary>コントロールを生成する。</summary>
    public ColorComboBoxControl()
    {
        // テキスト部（スウォッチを含む）・▼・… を隙間なく並べ、区切りは薄い垂直線で表す
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnSpacing = 0;

        _swatch = new Border
        {
            Width = SwatchSize,
            Height = SwatchSize,
            BorderBrush = new SolidColorBrush(Colors.Gray),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Colors.White),
        };
        // 入力欄は枠を持たない（枠は本コントロールの外周が 1 つだけ持つ）
        _text.BorderThickness = new Thickness(0);
        _text.Background = new SolidColorBrush(Colors.Transparent);
        _text.TextChanged += OnTextChanged;
        _text.LostFocus += OnTextLostFocus;

        // 入力欄の中で文字が重ならないよう、スウォッチの幅 + 余白だけ左を空ける。
        // 上下の余白はテーマの既定値を使う（0 にすると文字が上端へ寄る。 既定値はスタイル適用後でないと読めないため Loaded で行う）
        _text.Loaded += (_, _) =>
        {
            // Loaded は再配置でも再度発火するため、余白の加算は 1 回だけ行う（重ねて足さない）
            if (_textPaddingApplied)
            {
                return;
            }

            _textPaddingApplied = true;

            // 左の余白は「スウォッチの左余白 + スウォッチ + 文字との間隔」で**置き換える**
            // （テーマの既定値へ足すと間隔が広すぎる。 実測 2026-10-25）。 上下・右はテーマの既定値を保つ
            var padding = _text.Padding;
            _text.Padding = new Thickness((SwatchGap * 2) + SwatchSize, padding.Top, padding.Right, padding.Bottom);
        };

        // スウォッチは入力欄の内側へ重ねる（同じセルに置き、入力欄の背面に敷く）
        var textArea = new Grid();
        textArea.Children.Add(_text);
        textArea.Children.Add(_swatch);
        _swatch.HorizontalAlignment = HorizontalAlignment.Left;
        _swatch.Margin = new Thickness(SwatchGap, 0, 0, 0);
        SetColumn(textArea, 0);
        Children.Add(textArea);

        var separator1 = CreateSeparator();
        SetColumn(separator1, 1);
        Children.Add(separator1);

        StyleSegmentButton(_listButton);
        _listButton.Click += (_, _) => _listFlyout.ShowAt(_listButton);
        SetColumn(_listButton, 2);
        Children.Add(_listButton);

        var separator2 = CreateSeparator();
        SetColumn(separator2, 3);
        Children.Add(separator2);

        StyleSegmentButton(_pickerButton);
        _pickerButton.Click += (_, _) => ShowPicker();
        SetColumn(_pickerButton, 4);
        Children.Add(_pickerButton);

        // 外周の枠（全体を 1 つのコントロールに見せる）
        BorderBrush = new SolidColorBrush(s_borderColor);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);

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

    /// <summary>テキスト部の表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（既定）またはカラーコード。 入力欄は編集できるため、表記と異なる形の入力も受理する。</value>
    public ColorTextFormat TextFormat
    {
        get => _textFormat;
        set
        {
            if (_textFormat == value)
            {
                return;
            }

            _textFormat = value;
            ApplyColor(_selectedColor, raiseChanged: false);
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
    /// <summary>区切りの垂直線を生成する。</summary>
    /// <returns>枠より薄い垂直線（上下に余白を取り、枠の内側へ収める）。</returns>
    private static Border CreateSeparator() => new()
    {
        Width = SeparatorWidth,
        Background = new SolidColorBrush(s_separatorColor),
        Margin = new Thickness(0, SeparatorMargin, 0, SeparatorMargin),
        VerticalAlignment = VerticalAlignment.Stretch,
    };

    /// <summary>区画のボタンを枠・背景なしの外観にする。</summary>
    /// <param name="button">対象のボタン。</param>
    /// <remarks>外周の枠と区切りの垂直線だけで 1 つの入力欄に見せる（ボタン自身の枠は消す）。</remarks>
    private static void StyleSegmentButton(Button button)
    {
        button.Width = SegmentWidth;
        button.BorderThickness = new Thickness(0);
        button.CornerRadius = new CornerRadius(0);
        button.Background = new SolidColorBrush(Colors.Transparent);
        button.Padding = new Thickness(0);
        button.VerticalAlignment = VerticalAlignment.Stretch;
    }
    /// <summary>ピッカーのポップアップの表示スタイルを作る（高さの上限を外し、スクロールさせない）。</summary>
    /// <returns>FlyoutPresenter のスタイル。</returns>
    /// <remarks>
    /// <see cref="FlyoutPresenter"/> の既定スタイルは高さを制限し、内容を <c>ScrollViewer</c> に入れる。
    /// ピッカーは 1 画面に収まるため、上限を外してスクロールを切る（ウィンドウ外へはみ出して全体を見せる）。
    /// </remarks>
    private static Style CreatePickerPresenterStyle()
    {
        var style = new Style(typeof(FlyoutPresenter));
        style.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty, double.PositiveInfinity));
        style.Setters.Add(new Setter(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled));
        style.Setters.Add(new Setter(ScrollViewer.VerticalScrollModeProperty, ScrollMode.Disabled));
        return style;
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
            _text.Text = ColorString.ToText(normalized, _textFormat);

            // スウォッチとテキスト部のツールチップは 2 行（色名+不透明度 / カラーコード）を出す
            var tooltip = string.Concat(
                ColorString.ToDisplayText(normalized), Environment.NewLine, ColorString.ToArgbHex(normalized));
            ToolTipService.SetToolTip(_text, tooltip);
            ToolTipService.SetToolTip(_swatch, tooltip);

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
