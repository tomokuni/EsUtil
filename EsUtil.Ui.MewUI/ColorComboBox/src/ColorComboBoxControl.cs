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
/// 入力と状態の同期だけを行う（AvaloniaUI 版・WinUI 3 版と同じ意味の値になる）。<br/>
/// <b>構成</b>: 1 つの枠の中へ「スウォッチ（現在色）」「テキスト入力」「一覧ボタン（▼）」「カラーピッカー ボタン（…）」を
/// 隙間なく並べ、区切りは枠より薄い垂直線で表す（1 つの入力欄に見せる）。<br/>
/// <b>一覧は自前のポップアップ</b>、<b>カラーピッカーは <see cref="ColorPicker"/> 自身のドロップダウン</b>を使う
/// （<see cref="ColorPicker"/> はドロップダウン コントロールであり、内容を自前のポップアップへ入れると
/// 折り畳んだ見出ししか表示されない。 実測 2026-10-25）。<br/>
/// <b>ツールチップ</b>: スウォッチとテキスト部には「色名+不透明度」と「<c>#AARRGGBB</c>」の 2 行を出す。
/// </remarks>
public sealed class ColorComboBoxControl : Grid
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
    private static readonly MewColor s_borderColor = MewColor.FromArgb(0x80, 0x80, 0x80, 0x80);

    /// <summary>区切りの色（枠より薄い）。</summary>
    private static readonly MewColor s_separatorColor = MewColor.FromArgb(0x40, 0x80, 0x80, 0x80);

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

    /// <summary>一覧（色名順または色相順）。</summary>
    private readonly ListBox _list;

    /// <summary>カラーピッカー（自身のドロップダウンを開くためのアンカーを兼ねる）。</summary>
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

    /// <summary>テキスト部の表記。</summary>
    private ColorTextFormat _textFormat = ColorTextFormat.NameWithAlpha;

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
            CornerRadius = 2,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(SwatchGap, 0, 0, 0),
            IsHitTestVisible = false,
        };

        // 入力欄は枠を持たない（枠は本コントロールの外周が 1 つだけ持つ）
        _text = new TextBox
        {
            Text = "White",
            MinWidth = TextBoxMinWidth,
            BorderThickness = 0,
            CornerRadius = 0,
            Background = MewColor.Transparent,
            Padding = new Thickness((SwatchGap * 2) + SwatchSize, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _text.TextChanged += OnTextChanged;
        _text.LostFocus += OnTextLostFocus;

        _listButton = CreateSegmentButton("▼");
        _listButton.Click += ToggleList;

        _pickerButton = CreateSegmentButton("…");
        _pickerButton.Click += TogglePicker;

        _list = new ListBox();
        _list.SelectionChanged += _ => ApplyListSelection();
        _listPopup = new Popup();
        _listPopup.Content(_list);
        _listPopup.Closed += (_, _) => _listButton.Content("▼");

        // カラーピッカーは**自身のドロップダウン**を使う（内容を自前のポップアップへ入れると
        // 折り畳んだ見出しだけが表示される。 ColorPicker はドロップダウン コントロールである）。
        // アンカーは … の位置へ置くため 1×1 にして右下へ寄せる
        _picker = new ColorPicker
        {
            Kind = ColorPickerKind.Both,
            ShowAlpha = true,
            SelectedColor = MewColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
            Width = 1,
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
        };
        _picker.SelectedColorChanged += OnPickerColorChanged;

        // ピッカーは**折り畳んだ見出しを描かない**（1×1 でも見出しの「︿」が行の上へはみ出し、
        // … の上に別の矢印が見えていた。 実測 2026-10-25）。
        // 1×1 の入れ物で切り取ったうえで、ドロップダウンの開閉だけを使う
        // （ポップアップ自体は別の面へ出るため、切り取りの影響を受けない）
        var pickerHost = new Grid
        {
            Width = 1,
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
        };
        pickerHost.ClipToBounds(true);
        pickerHost.Add(_picker);

        // スウォッチは入力欄の内側へ重ねる（同じセルに置き、文字が重ならないよう左を空ける）
        var textArea = new Grid();
        textArea.Add(_text);
        textArea.Add(_swatch);

        // テキスト部・▼・… を隙間なく並べ、区切りは薄い垂直線で表す
        var row = new Grid().Columns("*,Auto,Auto,Auto,Auto");
        row.Add(textArea.Column(0));
        row.Add(CreateSeparator().Column(1));
        row.Add(_listButton.Column(2));
        row.Add(CreateSeparator().Column(3));
        row.Add(_pickerButton.Column(4));

        Add(new Border
        {
            Child = row,
            BorderBrush = s_borderColor,
            BorderThickness = 1,
            CornerRadius = 3,
        });

        Add(pickerHost);

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

    /// <summary>区画（▼ / …）のボタンを生成する。</summary>
    /// <param name="glyph">表示する文字。</param>
    /// <returns>枠と背景を持たないボタン（外周の枠と区切りだけで 1 つの入力欄に見せる）。</returns>
    private static Button CreateSegmentButton(string glyph)
    {
        var button = new Button().Content(glyph);
        button.Width = SegmentWidth;
        button.BorderThickness = 0;
        button.CornerRadius = 0;
        button.Background = MewColor.Transparent;
        button.Padding = new Thickness(0);
        button.VerticalAlignment = VerticalAlignment.Stretch;
        return button;
    }

    /// <summary>区切りの垂直線を生成する。</summary>
    /// <returns>枠より薄い垂直線（上下に余白を取り、枠の内側へ収める）。</returns>
    private static Border CreateSeparator() => new()
    {
        Width = SeparatorWidth,
        Background = s_separatorColor,
        Margin = new Thickness(0, SeparatorMargin, 0, SeparatorMargin),
        VerticalAlignment = VerticalAlignment.Stretch,
    };

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
            _text.Text = ColorString.ToText(normalized, _textFormat);

            // スウォッチとテキスト部のツールチップは 2 行（色名+不透明度 / カラーコード）を出す
            var tooltip = string.Concat(
                ColorString.ToDisplayText(normalized), Environment.NewLine, ColorString.ToArgbHex(normalized));
            _text.ToolTip(tooltip);
            _swatch.ToolTip(tooltip);

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

    /// <summary>カラーピッカーの色が変わったときに色を反映する（確定操作を持たないため即時に反映する）。</summary>
    /// <param name="color">選ばれた色。</param>
    private void OnPickerColorChanged(MewColor color)
    {
        if (_applying)
        {
            return;
        }

        ApplyColor(ColorString.ToArgbHex(DrawingColor.FromArgb(color.A, color.R, color.G, color.B)), raiseChanged: true);
    }

    /// <summary>一覧のポップアップを開閉する。</summary>
    private void ToggleList()
    {
        if (_listPopup.IsOpen)
        {
            _listPopup.Close();
            return;
        }

        // アンカーは**ウィンドウ座標**である（自領域の座標ではない）。 自領域の矩形をそのまま渡すとウィンドウ左上へ出る
        _listPopup.ShowAt(this, Bounds, PopupAnchorSide.Below);
        _listButton.Content("▲");
    }

    /// <summary>カラーピッカーのドロップダウンを開閉する。</summary>
    private void TogglePicker()
    {
        if (ColorString.TryParse(_selectedColor, out var color, out _))
        {
            _picker.SelectedColor = ToMewColor(color);
        }

        _picker.IsDropDownOpen = !_picker.IsDropDownOpen;
    }

    /// <summary>コアの色を MewUI の色へ写す。</summary>
    /// <param name="color">コアの色。</param>
    /// <returns>同じ ARGB を持つ MewUI の色。</returns>
    private static MewColor ToMewColor(DrawingColor color)
        => MewColor.FromArgb(color.A, color.R, color.G, color.B);
}