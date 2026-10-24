using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Palette;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DrawingColor = System.Drawing.Color;
using MewColor = Aprillz.MewUI.Color;

namespace EsUtil.Ui.MewUI.ColorComboBox;

/// <summary>色を選ぶコントロール（色名の入力・一覧からの選択・カラーピッカー）。</summary>
/// <remarks>
/// <b>規則の所有者はコアである</b>: 受理する入力（色名・16 進・<c>+{n}%</c>）、色名の表、保存値（<c>#AARRGGBB</c>）と
/// 画面表示（<c>{色}+{不透明度%}</c>）、並び順、入力の妥当性は
/// <see cref="EsUtil.Platform.Core.Palette"/>（<see cref="ColorString"/> / <see cref="ColorEntry"/> /
/// <see cref="ColorValidation"/>）が単一所有する。 本クラスは MewUI の要素の組み立てと、
/// 入力と状態の同期だけを行う（AvaloniaUI 版と同じ意味の値になる）。<br/>
/// <b>構成</b>: 1 行に「スウォッチ（現在色）」「テキスト入力」「一覧ボタン（▼）」「カラーピッカー ボタン（…）」を置く。
/// 一覧とピッカーは <see cref="Popup"/> で開く（MewUI の <see cref="ComboBox"/> は編集可能でないため、
/// テキスト入力は別の <see cref="TextBox"/> が持つ）。
/// </remarks>
public sealed class ColorComboBoxControl : Grid
{
    /// <summary>スウォッチの一辺（DIP）。</summary>
    private const double SwatchSize = 16.0;

    /// <summary>スウォッチと入力欄の左端・文字との間の余白（DIP）。</summary>
    private const double SwatchGap = 6.0;

    /// <summary>テキスト入力の最小幅（DIP）。</summary>
    private const double TextBoxMinWidth = 120.0;

    /// <summary>現在の色を示すスウォッチ。</summary>
    private readonly Border _swatch;

    /// <summary>色文字列の入力欄。</summary>
    private readonly TextBox _text;

    /// <summary>色の一覧を開くボタン。</summary>
    private readonly Button _listButton;

    /// <summary>カラーピッカーを開くボタン。</summary>
    private readonly Button _pickerButton;

    /// <summary>一覧のポップアップ。</summary>
    private readonly Popup _listPopup;

    /// <summary>ピッカーのポップアップ。</summary>
    private readonly Popup _pickerPopup;

    /// <summary>一覧（色名順または色相順）。</summary>
    private readonly ListBox _list;

    /// <summary>MewUI のカラーピッカー。</summary>
    private readonly ColorPicker _picker;

    /// <summary>入力の反映中かどうか（入力と状態の往復でイベントが循環するのを防ぐ）。</summary>
    private bool _applying;

    /// <summary>内部ロガー。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞（null は既定値を使う）。</summary>
    private string? _logPrefix;

    /// <summary>現在の色（保存値）。</summary>
    private string _selectedColor = "#FFFFFFFF";

    /// <summary>一覧の並び順。</summary>
    private ColorSortMode _colorSortMode = ColorSortMode.ByHue;

    /// <summary>選択中の色（保存値の文字列）が変わったときに発生する。</summary>
    public event EventHandler? SelectedColorChanged;

    /// <summary>コントロールを生成する。</summary>
    public ColorComboBoxControl()
    {
        _swatch = new Border
        {
            Width = SwatchSize,
            Height = SwatchSize,
            BorderBrush = MewColor.FromRgb(0x80, 0x80, 0x80),
            BorderThickness = 1,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _text = new TextBox { Text = "White", MinWidth = TextBoxMinWidth };
        _text.TextChanged += OnTextChanged;
        _text.LostFocus += OnTextLostFocus;

        _listButton = new Button().Content("▼");
        _listButton.Click += ToggleList;

        _pickerButton = new Button().Content("…");
        _pickerButton.Click += TogglePicker;

        _list = new ListBox();
        _list.SelectionChanged += _ => ApplyListSelection();
        _listPopup = new Popup();
        _listPopup.Content(_list);
        _listPopup.Closed += (_, _) => _listButton.Content("▼");

        _picker = new ColorPicker
        {
            Kind = ColorPickerKind.Both,
            ShowAlpha = true,
            SelectedColor = ToMewColor(DrawingColor.White),
        };
        _picker.SelectedColorChanged += OnPickerColorChanged;

        var ok = new Button().Content("OK");
        ok.Click += () => ClosePicker(apply: true);
        var cancel = new Button().Content("キャンセル");
        cancel.Click += () => ClosePicker(apply: false);
        var pickerButtons = new StackPanel().Horizontal().Spacing(8);
        pickerButtons.Children(ok, cancel);

        var pickerBody = new StackPanel().Vertical().Spacing(8);
        pickerBody.Children(_picker, pickerButtons);
        _pickerPopup = new Popup();
        _pickerPopup.Content(pickerBody);
        _pickerPopup.Closed += (_, _) => _pickerButton.Content("…");

        // スウォッチは入力欄の内側へ重ねる（同じセルに置き、文字が重ならないよう左を空ける）
        _swatch.HorizontalAlignment = HorizontalAlignment.Left;
        _swatch.Margin = new Thickness(SwatchGap, 0, 0, 0);
        _text.Padding = new Thickness(SwatchSize + SwatchGap, 0, 0, 0);
        var textArea = new Grid();
        textArea.Add(_text);
        textArea.Add(_swatch);

        // テキスト部・一覧ボタン・ピッカーボタンを隙間なく並べる（1 つの入力欄に見せる）
        var row = new StackPanel().Horizontal().Spacing(0);
        row.Children(textArea, _listButton, _pickerButton);
        Add(row);

        RefreshList();
        ApplyColor(_selectedColor, raiseChanged: false);
    }

    /// <summary>選択中の色（保存値）を取得または設定する。</summary>
    /// <value><c>#AARRGGBB</c>（アルファが先頭）の保存値。 色名や <c>+{n}%</c> の入力も設定できる（正規化する）。</value>
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

    /// <summary>色を設定し、入力欄とスウォッチへ反映する。</summary>
    /// <param name="value">設定する色（保存値・色名・<c>+{n}%</c> のいずれか）。</param>
    /// <param name="raiseChanged">変更イベントを発火する場合は true。</param>
    private void ApplyColor(string? value, bool raiseChanged)
    {
        var normalized = ColorString.ToArgbHex(value);
        var isValid = ColorValidation.IsValid(value);
        var state = ColorValidation.Determine(value, isValid);

        _applying = true;
        try
        {
            _text.Text = ColorString.ToDisplayText(normalized);
            if (ColorString.TryParse(normalized, out var color, out _))
            {
                _swatch.Background = ToMewColor(color);
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
        var entries = _list.ItemsSource as IReadOnlyList<ColorEntry>;
        if (entries is null)
        {
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var candidate = entries[i].Color;
            if (candidate.R == color.R && candidate.G == color.G && candidate.B == color.B)
            {
                _list.SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>一覧を作り直す。</summary>
    private void RefreshList()
    {
        var entries = ColorEntry.GetSorted(_colorSortMode);
        _list.Items(entries, entry => entry.Name);
        if (ColorString.TryParse(_selectedColor, out var color, out _))
        {
            UpdateSelectedIndex(color);
        }
    }

    /// <summary>テキスト入力が変わったときに色を反映する。</summary>
    /// <param name="text">入力された文字列。</param>
    private void OnTextChanged(string text)
    {
        if (_applying)
        {
            return;
        }

        ApplyColor(text, raiseChanged: true);
    }

    /// <summary>テキスト入力からフォーカスが外れたとき、無効な入力を最後の有効値へ戻す。</summary>
    private void OnTextLostFocus()
    {
        if (!ColorValidation.ShouldRecover(ColorValidation.IsValid(_text.Text), hasLastValid: true))
        {
            return;
        }

        ApplyColor(_selectedColor, raiseChanged: false);
    }

    /// <summary>一覧の選択が変わったときに色を反映する。</summary>
    private void ApplyListSelection()
    {
        if (_list.SelectedItem is ColorEntry entry)
        {
            ApplyColor(entry.Name, raiseChanged: true);
        }
    }

    /// <summary>カラーピッカーの色が変わったときにスウォッチだけ先に反映する。</summary>
    /// <param name="color">選ばれた色。</param>
    private void OnPickerColorChanged(MewColor color)
        => _swatch.Background = color;

    /// <summary>一覧のポップアップを開閉する。</summary>
    private void ToggleList()
    {
        if (_listPopup.IsOpen)
        {
            _listPopup.Close();
            return;
        }

        _listPopup.ShowAt(this, new Rect(0, Bounds.Height, Bounds.Width, 320), PopupAnchorSide.Below);
        _listButton.Content("▲");
    }

    /// <summary>カラーピッカーのポップアップを開閉する。</summary>
    private void TogglePicker()
    {
        if (_pickerPopup.IsOpen)
        {
            _pickerPopup.Close();
            return;
        }

        if (ColorString.TryParse(_selectedColor, out var color, out _))
        {
            _picker.SelectedColor = ToMewColor(color);
        }

        _pickerPopup.ShowAt(this, new Rect(0, Bounds.Height, 320, 420), PopupAnchorSide.Below);
        _pickerButton.Content("▲");
    }

    /// <summary>ピッカーを閉じ、必要なら色を反映する。</summary>
    /// <param name="apply">色を反映する場合は true。</param>
    private void ClosePicker(bool apply)
    {
        if (apply)
        {
            var color = _picker.SelectedColor;
            ApplyColor(ColorString.ToArgbHex(DrawingColor.FromArgb(color.A, color.R, color.G, color.B)), raiseChanged: true);
        }

        _pickerPopup.Close();
    }

    /// <summary>コアの色を MewUI の色へ写す。</summary>
    /// <param name="color">コアの色。</param>
    /// <returns>同じ ARGB を持つ MewUI の色。</returns>
    private static MewColor ToMewColor(DrawingColor color)
        => MewColor.FromArgb(color.A, color.R, color.G, color.B);
}
