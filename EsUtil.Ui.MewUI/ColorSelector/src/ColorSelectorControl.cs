using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Palette;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DrawingColor = System.Drawing.Color;
using MewColor = Aprillz.MewUI.Color;

namespace EsUtil.Ui.MewUI.ColorSelector;

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
public sealed class ColorSelectorControl : Grid
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

    /// <summary>一覧の最小幅（DIP）。 タブを切り替えても幅が変わらないようにする。</summary>
    private const double ListMinWidth = 200;

    /// <summary>一覧の分類を切り替えるタブの幅（DIP）。 2 つを並べたときに一覧の幅と釣り合う値。</summary>
    private const double CategoryTabWidth = 96;

    /// <summary>テーマの色の表示名の接頭辞。</summary>
    private const string ThemeLabelPrefix = "テーマ: ";

    /// <summary>選択中のテーマの色の識別子（例: <c>背景</c>）。 テーマの色でない場合は null。</summary>
    private string? _selectedThemeColor;

    /// <summary>直近に選んだ一覧の分類（次に開いたときに同じタブから始める）。</summary>
    private ColorSelectorCategory _listCategory = ColorSelectorCategory.Theme;

    /// <summary>タブの状態を自分で書いている間かどうか（相手のタブの変化通知で往復しないようにする）。</summary>
    private bool _switchingCategory;

    /// <summary>一覧の上部のタブ（テーマカラー / 色名）。</summary>
    private readonly ToggleButton _themeTab = CreateCategoryTab("テーマカラー");

    /// <summary>色名のタブ。</summary>
    private readonly ToggleButton _namedTab = CreateCategoryTab("標準の色");

    /// <summary>一覧の幅に足す余白（DIP。 枠と、行の文字が端に接しないための余裕）。</summary>
    /// <remarks>行の実寸と項目の余白は実測値であり、端数だけで足りない分を本値が補う（実測 2026-11-02）。</remarks>
    private const double ListWidthAllowance = 8.0;

    /// <summary>一覧の 1 行（色の四角 + 名前）を作るテンプレート。</summary>
    /// <remarks>
    /// 名前だけを並べると選ぶ色が分からないため、他 UI と同じく色の四角を添える<br/>
    /// （値は保存値（<c>#AARRGGBB</c> または色名）であり、四角の色はその値から解決する）。<br/>
    /// <b>文字サイズを明示する</b>: 画面へ付いていない行を測ると既定の文字サイズで測ってしまい、<br/>
    /// 実際に並ぶ行より広い幅を要求する（実測 2026-11-02: 96 DIP の行が 137.6 DIP になった）。<br/>
    /// 実測と表示を同じ大きさにするため、テーマの文字サイズを渡す（他 UI の一覧もテーマの文字サイズで並ぶ）。
    /// </remarks>
    private readonly IDataTemplate _itemTemplate;

    /// <summary>テーマの文字サイズを解決できない場合に使う文字サイズ（DIP）。</summary>
    private const double FallbackFontSize = 12.0;

    /// <summary>一覧の行の文字サイズ（DIP）を取得する。</summary>
    /// <value>テーマの文字サイズ。 テーマを解決できない場合（<c>Application.Run</c> 前）は <see cref="FallbackFontSize"/>。</value>
    /// <remarks>
    /// <b>行の実測と表示で同じ値を読む</b>: 行を画面へ付ける前に測ると、既定の文字サイズで測って実際の行と<br/>
    /// 大きさが食い違う（実測 2026-11-02）。 測る側（<see cref="ApplyListWidth"/>）と作る側（<see cref="CreateItemRow"/>）が<br/>
    /// 同じ値を読むことで、必要な幅と実際の行が一致する。
    /// </remarks>
    private static double ListFontSize
        => Application.IsRunning ? Application.Current.Theme.Metrics.FontSize : FallbackFontSize;

    /// <summary>枠の色（半透明の灰色。 明色・暗色のどちらのテーマでも見える）。</summary>
    private static readonly MewColor s_borderColor = MewColor.FromArgb(0x80, 0x80, 0x80, 0x80);

    /// <summary>区切りの色（枠より薄い）。</summary>
    private static readonly MewColor s_separatorColor = MewColor.FromArgb(0x40, 0x80, 0x80, 0x80);

    /// <summary>検証エラーの枠線の色（他の UI と同じ赤）。</summary>
    private static readonly MewColor s_errorColor = MewColor.FromRgb(0xFF, 0x00, 0x00);

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

    /// <summary>外周の枠（検証エラーの強調で色を変える）。</summary>
    private readonly Border _frame;

    /// <summary>入力が無効なときに枠線を赤くするかどうか。</summary>
    private bool _highlightBorderOnError;

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
    public ColorSelectorControl()
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

        // 一覧の幅は**行の内容に合わせる**（既定のまま置くと、ポップアップが使える領域の幅いっぱいへ広がる。
        // 実測 2026-11-02: 入力欄が 200 DIP でも一覧が 600 DIP 以上になっていた）。
        // 左寄せにすると希望サイズが内容の実寸になり、文字サイズや行の文言が変われば幅も追随する
        _list = new ListBox { HorizontalAlignment = HorizontalAlignment.Left };
        _list.SelectionChanged += _ => ApplyListSelection();

        _itemTemplate = new DelegateTemplate<ColorSelectorItem>(
            _ => CreateItemRow(ListFontSize),
            static (view, item, _, _) => ApplyItem(view, item),
            static (_, _, _, _) => { });
        // 上部のタブ（テーマカラー / 色名）。 分類ごとに一覧を差し替える
        // （テーマの色が多いため、1 つの一覧へ混ぜると色名を探しにくい）
        _themeTab.IsChecked(true);
        _themeTab.OnCheckedChanged(isChecked => OnCategoryTabChanged(ColorSelectorCategory.Theme, isChecked));
        _namedTab.OnCheckedChanged(isChecked => OnCategoryTabChanged(ColorSelectorCategory.Named, isChecked));
        var tabStrip = new StackPanel().Horizontal().Spacing(4).Children(_themeTab, _namedTab);

        _listPopup = new Popup();
        _listPopup.Content(new StackPanel().Vertical().Spacing(6).Children(tabStrip, _list));
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

        _frame = new Border
        {
            Child = row,
            BorderBrush = s_borderColor,
            BorderThickness = 1,
            CornerRadius = 3,
        };
        Add(_frame);

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

    /// <summary>一覧の先頭に UI のテーマの色を並べるかどうかを取得または設定する。</summary>
    /// <value>並べる場合は true（既定）。</value>
    /// <remarks>
    /// <b>値は UI のテーマから取る</b>: テーマの色は UI ごとに異なるため、コアの色名の表には含めない<br/>
    /// （一覧を作るたびに解決するため、明暗の切替とアクセント色の変更に追随する）。<br/>
    /// <b>並べない場合はコアの色名だけ</b>になる（一覧は次に開くときに作り直す）。
    /// </remarks>
    public bool ShowThemeColors { get; set; } = true;
    /// <summary>選択中のテーマの色の識別子を取得または設定する（例: <c>背景</c>）。</summary>
    /// <value>テーマの色を選んでいるときの識別子。 色名・カラーコードを選んでいるときは null。</value>
    /// <remarks>
    /// <b>テーマの色は名前で覚える</b>: 選んだ時点の色（<c>#AARRGGBB</c>）を保存すると、明暗の切替やアクセント色の変更へ<br/>
    /// 追随できない。 本プロパティが識別子を持つ間は <see cref="SelectedColor"/> を<b>そのときのテーマの色</b>へ<br/>
    /// 解決し直す（テーマが変わると <see cref="SelectedColor"/> も変わり、変更イベントが届く）。
    /// </remarks>
    public string? SelectedThemeColor
    {
        get => _selectedThemeColor;
        set
        {
            if (string.Equals(_selectedThemeColor, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectedThemeColor = value;
            if (value is not null && TryResolveThemeColor(value, out var hex))
            {
                ApplyThemeColor(hex);
            }
            else
            {
                ApplyColor(_selectedColor, raiseChanged: false);
            }
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

    /// <summary>入力欄の文字サイズ（DIP）を取得または設定する。</summary>
    /// <value>文字サイズ。 <see cref="double.NaN"/>（既定）の場合はテーマの既定（<c>ThemeMetrics.FontSize</c>）を使う。</value>
    /// <remarks>
    /// <b>MewUI の文字サイズは継承されない</b>: 他の UI（Avalonia / WinUI 3）は親の <c>FontSize</c> を継承するため
    /// 周囲の入力欄と同じ大きさになるが、MewUI の要素は自分の値だけを使う。 設定画面のように周囲の入力欄より
    /// 小さい文字で表示されるのを避けるため、ホストが同じ値を設定できるようにする（実測 2026-10-25）。
    /// </remarks>
    public double TextFontSize
    {
        get => _text.FontSize;
        set => _text.FontSize(value);
    }


    /// <summary>入力が無効なときに枠線を赤くするかを取得または設定する。</summary>
    /// <value>true の場合は <see cref="HasValidationError"/> が true の間、外周の枠線を赤にする。 既定は false。</value>
    public bool HighlightBorderOnError
    {
        get => _highlightBorderOnError;
        set
        {
            _highlightBorderOnError = value;
            UpdateBorderValidationVisual();
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
    /// <value>接頭辞。 null の場合は既定値（<c>[ColorSelector]</c>）を使う。</value>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            ColorSelectorLogMessage.Prefix = value ?? ColorSelectorLogMessage.DefaultPrefix;
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
            // テーマの色を選んでいる間は識別子を出す（色を写すと、テーマに追随していることが分からない）
            _text.Text = _selectedThemeColor is { } key
                ? ThemeLabelPrefix + key
                : ColorString.ToText(normalized, _textFormat);

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
            UpdateBorderValidationVisual();
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

    /// <summary>テーマが変わったときに、テーマの色を選んでいる間はその色を解決し直す。</summary>
    /// <param name="oldTheme">変更前のテーマ。</param>
    /// <param name="newTheme">変更後のテーマ。</param>
    protected override void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        base.OnThemeChanged(oldTheme, newTheme);
        ReapplyThemeColor();
    }

    /// <summary>テーマの色の識別子から、そのときのテーマの色を解決する。</summary>
    /// <param name="key">識別子（例: <c>背景</c>）。</param>
    /// <param name="hex">解決した色（<c>#AARRGGBB</c>）。</param>
    /// <returns>解決できた場合は true（その UI に無い色は false）。</returns>
    /// <remarks>一覧を作る処理と同じ解決を使う（別の表を持つと一覧の表示と解決した色が食い違う）。</remarks>
    internal bool TryResolveThemeColor(string key, out string hex)
    {
        foreach (var item in CreateItems())
        {
            if (string.Equals(item.ThemeKey, key, StringComparison.Ordinal))
            {
                hex = item.Value;
                return true;
            }
        }

        hex = string.Empty;
        return false;
    }

    /// <summary>テーマの色を選んでいる間、そのときのテーマの色へ解決し直す。</summary>
    private void ReapplyThemeColor()
    {
        if (_selectedThemeColor is { } key && TryResolveThemeColor(key, out var hex))
        {
            ApplyThemeColor(hex);
        }
        else
        {
            ApplyColor(_selectedColor, raiseChanged: false);
        }
    }

    /// <summary>解決したテーマの色を <see cref="SelectedColor"/> へ書く（識別子は消さない）。</summary>
    /// <param name="hex">解決した色（<c>#AARRGGBB</c>）。</param>
    private void ApplyThemeColor(string hex)
    {
        _applying = true;
        try
        {
            _selectedColor = hex;
        }
        finally
        {
            _applying = false;
        }

        ApplyColor(hex, raiseChanged: true);
    }

    /// <summary>検証エラーの強調を枠線へ反映する。</summary>
    private void UpdateBorderValidationVisual()
        => _frame.BorderBrush = _highlightBorderOnError && HasValidationError ? s_errorColor : s_borderColor;

    /// <summary>一覧の 1 行（色の四角 + 名前）を作る。</summary>
    /// <param name="fontSize">行の文字サイズ（DIP）。</param>
    /// <returns>作った要素。</returns>
    private static Border CreateItemRow(double fontSize)
    {
        var swatch = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = 2,
            BorderBrush = MewColor.FromRgb(0x80, 0x80, 0x80),
            BorderThickness = 1,
            VerticalAlignment = VerticalAlignment.Center,
        };

        return new Border
        {
            Background = MewColor.Transparent,
            Child = new StackPanel()
                .Horizontal()
                .Spacing(6)
                .Children(swatch, new TextBlock { FontSize = fontSize, VerticalAlignment = VerticalAlignment.Center }),
        };
    }

    /// <summary>一覧の 1 行へ色と名前を写す。</summary>
    /// <param name="view">テンプレートが作った要素。</param>
    /// <param name="item">項目（表示名と適用値）。</param>
    /// <remarks>
    /// <b>表示名と適用値は別である</b>: テーマの色は表示名（<c>テーマ: 背景</c>）と適用値（<c>#FFF7F8FA</c>）が異なり、<br/>
    /// 色名は両者が同じである。 行の文字には必ず表示名を書き、四角の色は適用値から解決する<br/>
    /// （適用値を書くとテーマの色だけ 16 進の文字列が並び、他 UI と表記が変わる。 実測 2026-11-02）。
    /// </remarks>
    private static void ApplyItem(FrameworkElement view, ColorSelectorItem item)
    {
        if (view is not Border { Child: StackPanel panel } || panel.Children.Count < 2
            || panel.Children[0] is not Border swatch || panel.Children[1] is not TextBlock text)
        {
            return;
        }

        text.Text = item.Label;
        swatch.Background = ColorString.TryParse(item.Value, out var color, out _)
            ? MewColor.FromArgb(color.A, color.R, color.G, color.B)
            : MewColor.Transparent;
    }

    /// <summary>一覧の選択を現在の色へ合わせる。</summary>
    /// <param name="color">現在の色。</param>
    private void UpdateSelectedIndex(DrawingColor color)
    {
        if (_list.ItemsSource is not IReadOnlyList<ColorSelectorItem> entries)
        {
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            if (ColorString.TryParse(entries[i].Value, out var candidate, out _)
                && candidate.R == color.R && candidate.G == color.G && candidate.B == color.B)
            {
                _list.SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>一覧に並べる項目を作る（テーマの色を先頭へ置く）。</summary>
    /// <returns>表示名と適用値の組。</returns>
    /// <remarks>
    /// <b>テーマの色は開くたびに解決する</b>: 明暗の切替やアクセント色の変更に追随させるため、値を作り置きしない。<br/>
    /// 解決できない色は並べない。
    /// </remarks>
    internal List<ColorSelectorItem> CreateItems()
    {
        var items = new List<ColorSelectorItem>();
        if (ShowThemeColors && Application.IsRunning)
        {
            AddThemeColors(items);
        }

        foreach (var entry in ColorEntry.GetSorted(_colorSortMode))
        {
            // 一覧の表記は入力欄の表記に合わせる（16 進の設定なら 16 進で並べる）
            items.Add(new ColorSelectorItem(
                ColorString.ToText(entry.Name, _textFormat), entry.Name, ColorSelectorCategory.Named));
        }

        return items;
    }

    /// <summary>一覧の先頭へ並べるテーマの色を追加する（MewUI のパレットの色をすべて並べる）。</summary>
    /// <param name="items">追加先。</param>
    /// <remarks>
    /// <b>並び順は 3 UI で共通</b>: 意味の対応する色は同じ名前・同じ順にし、その UI にしか無い色を後ろへ足す<br/>
    /// （UI を替えても同じ名前の色を選べる）。<br/>
    /// <b>値は一覧を作り直すたびに解決する</b>（`Application.Run` の前はパレットが無いため、そのときは並ばない）。
    /// </remarks>
    private static void AddThemeColors(List<ColorSelectorItem> items)
    {
        var palette = Application.Current.Theme.Palette;

        // 3 UI で共通の意味を持つ色（同じ名前・同じ順）
        AddThemeColor(items, "テーマ: 背景", palette.WindowBackground);
        AddThemeColor(items, "テーマ: レイヤー", palette.ContainerBackground);
        AddThemeColor(items, "テーマ: カード", palette.ControlBackground);
        AddThemeColor(items, "テーマ: コントロールの背景", palette.ButtonFace);
        AddThemeColor(items, "テーマ: コントロールの枠線", palette.ControlBorder);
        AddThemeColor(items, "テーマ: 区切り線", palette.DisabledControlBackground);
        AddThemeColor(items, "テーマ: 文字", palette.WindowText);
        AddThemeColor(items, "テーマ: 副次テキスト", palette.PlaceholderText);
        AddThemeColor(items, "テーマ: 無効の文字", palette.DisabledText);
        AddThemeColor(items, "テーマ: 無効の背景", palette.ButtonDisabledBackground);
        AddThemeColor(items, "テーマ: アクセント", palette.Accent);
        AddThemeColor(items, "テーマ: アクセント（淡）", palette.AccentHoverOverlay);
        AddThemeColor(items, "テーマ: アクセント（濃）", palette.AccentBorderHotOverlay);
        AddThemeColor(items, "テーマ: アクセント文字", palette.AccentText);
        AddThemeColor(items, "テーマ: アクセント上の文字", palette.AccentText);
        AddThemeColor(items, "テーマ: 選択の背景", palette.SelectionBackground);
        AddThemeColor(items, "テーマ: ホバーの背景", palette.ButtonHoverBackground);
        AddThemeColor(items, "テーマ: 押下の背景", palette.ButtonPressedBackground);
        AddThemeColor(items, "テーマ: フォーカス", palette.Focus);
        AddThemeColor(items, "テーマ: エラー", palette.Error);

        // MewUI にしか無い色
        AddThemeColor(items, "テーマ: 選択の文字", palette.SelectionText);
        AddThemeColor(items, "テーマ: 無効のアクセント", palette.DisabledAccent);
        AddThemeColor(items, "テーマ: アクセントの枠線（ホバー）", palette.AccentBorderHotOverlay);
        AddThemeColor(items, "テーマ: アクセントの枠線（押下）", palette.AccentBorderActiveOverlay);
        AddThemeColor(items, "テーマ: アクセントの押下", palette.AccentPressedOverlay);
        AddThemeColor(items, "テーマ: スクロール バー", palette.ScrollBarThumb);
        AddThemeColor(items, "テーマ: スクロール バー（ホバー）", palette.ScrollBarThumbHover);
        AddThemeColor(items, "テーマ: スクロール バー（押下）", palette.ScrollBarThumbActive);
    }

    /// <summary>テーマの色を一覧へ追加する（解決できない場合は何もしない）。</summary>
    /// <param name="items">追加先。</param>
    /// <param name="label">表示する名前。</param>
    /// <param name="color">テーマが決めた色。</param>
    /// <remarks>透過（アルファ 0）の色は選んでも見えないため並べない（他 UI と同じ判定）。</remarks>
    private static void AddThemeColor(List<ColorSelectorItem> items, string label, MewColor color)
    {
        if (color.A == 0)
        {
            return;
        }

        // 識別子は表示名から作る（「テーマ: 背景」→「背景」）。 色の識別子を 1 箇所で決める
        var key = label.StartsWith(ThemeLabelPrefix, StringComparison.Ordinal)
            ? label[ThemeLabelPrefix.Length..]
            : label;
        items.Add(new ColorSelectorItem(label, ToHex(color), ColorSelectorCategory.Theme, key));
    }

    /// <summary>MewUI の色を保存値（<c>#AARRGGBB</c>）にする。</summary>
    /// <param name="color">色。</param>
    /// <returns><c>#AARRGGBB</c> 形式の文字列。</returns>
    private static string ToHex(MewColor color) => ColorString.ToArgbHex(DrawingColor.FromArgb(color.A, color.R, color.G, color.B));

    /// <summary>一覧を作り直す。</summary>
    private void RefreshList()
    {
        var entries = CreateItems();

        // 作り直しの途中で選択が動いても「利用者の選択」として扱わない（他 UI の一覧選択と同じ判定）
        // 扱うと、並び順を変えただけで色の変更として処理される
        _applying = true;
        try
        {
            var category = entries.Exists(item => item.Category == _listCategory)
                ? _listCategory
                : entries.Exists(item => item.Category == ColorSelectorCategory.Theme)
                    ? ColorSelectorCategory.Theme
                    : ColorSelectorCategory.Named;
            var visible = entries.Where(item => item.Category == category).ToList();
            _list.ItemsSource = ItemsView.Create(visible);
            _list.ItemTemplate = _itemTemplate;
            ApplyListWidth(visible);
            if (ColorString.TryParse(_selectedColor, out var color, out _))
            {
                UpdateSelectedIndex(color);
            }
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>一覧の分類（タブ）を切り替える。</summary>
    /// <param name="category">表示する分類。</param>
    /// <remarks>
    /// <b>片方だけが選ばれる</b>: タブは押すたびに状態が反転するため、選ばれた方だけを選んだ状態にし、<br/>
    /// 一覧は分類で絞り直す（並び順と幅は分類ごとに決まる）。
    /// </remarks>
    internal void ApplyCategory(ColorSelectorCategory category)
    {
        _listCategory = category;

        // 両方の状態を書くと相手のタブの変化通知も届くため、書き込みの間は通知を無視する
        _switchingCategory = true;
        try
        {
            _themeTab.IsChecked(category == ColorSelectorCategory.Theme);
            _namedTab.IsChecked(category == ColorSelectorCategory.Named);
        }
        finally
        {
            _switchingCategory = false;
        }

        RefreshList();
    }

    /// <summary>タブが選ばれたときに分類を切り替える（外れた通知と、自分で書いた通知は無視する）。</summary>
    /// <param name="category">タブの分類。</param>
    /// <param name="isChecked">タブが選ばれたかどうか。</param>
    private void OnCategoryTabChanged(ColorSelectorCategory category, bool isChecked)
    {
        if (!isChecked || _switchingCategory || _listCategory == category)
        {
            return;
        }

        ApplyCategory(category);
    }

    /// <summary>一覧の分類を切り替えるタブを作る。</summary>
    /// <param name="text">タブの文言。</param>
    /// <returns>タブ（<see cref="ToggleButton"/>）。</returns>
    private static ToggleButton CreateCategoryTab(string text)
        => new ToggleButton { Width = CategoryTabWidth }.Content(text);

    /// <summary>一覧の幅を内容（行の実寸）に合わせる。</summary>
    /// <param name="entries">一覧に並べる項目。</param>
    /// <remarks>
    /// <b>ListBox の既定の幅は内容と関係なく広い</b>: 行が 2 件でも 237.6 DIP を要求し、行を増やすとさらに広がる<br/>
    /// （実測 2026-11-02）。 そのまま置くと一覧だけが入力欄より大幅に広くなるため、行を実測して幅を決める。<br/>
    /// <b>文字サイズ・文言を変えても追随する</b>: 見積もりではなく実際の行（テンプレートが作る要素）を測るため、<br/>
    /// テーマの文字サイズが変わっても必要な幅が変わる（固定値を置くと文字が切れるか余りが出る）。
    /// </remarks>
    private void ApplyListWidth(List<ColorSelectorItem> entries)
    {
        // 画面へ出ていない間（アプリ実行前・ヘッドレス）は測らない。 文字の測定は描画のバックエンドを要求するため、
        // ここで測ると生成そのものが失敗する（実測 2026-11-02）。 幅は開くたびに作り直すので、実行後に測れば足りる
        if (!Application.IsRunning)
        {
            _list.Width = double.NaN;
            return;
        }

        var widest = 0.0;
        foreach (var entry in entries)
        {
            var row = CreateItemRow(ListFontSize);
            ApplyItem(row, entry);
            row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, row.DesiredSize.Width);
        }

        var padding = _list.ItemPadding;
        _list.Width = Math.Max(ListMinWidth, widest + padding.Left + padding.Right + ListWidthAllowance);
    }

    /// <summary>テキスト入力が変わったときに色を反映する。</summary>
    /// <param name="text">入力された文字列。</param>
    private void OnTextChanged(string text)
    {
        if (_applying)
        {
            return;
        }

        // 直接の入力はテーマの色の選択を解く
        _selectedThemeColor = null;
        ApplyColor(text, raiseChanged: true);
    }

    /// <summary>テキスト入力からフォーカスが外れたとき、無効な入力を最後の有効値へ戻す。</summary>
    private void OnTextLostFocus()
    {
        var valid = ColorValidation.IsValid(_text.Text);
        _log.LogLostFocus(_selectedColor, valid);
        if (!ColorValidation.ShouldRecover(valid, hasLastValid: true))
        {
            return;
        }

        ApplyColor(_selectedColor, raiseChanged: false);
    }

    /// <summary>一覧の選択が変わったときに色を反映する。</summary>
    private void ApplyListSelection()
    {
        // 色を反映すると一覧の選択も同じ色へ更新される。 その往復を「利用者の選択」として扱わない
        // （他 UI の一覧選択も同じ判定を持つ）
        if (_applying)
        {
            return;
        }

        if (_list.SelectedItem is ColorSelectorItem item)
        {
            // テーマの色は識別子を覚える（明暗・アクセントの変更へ追随させる）。 色名は値だけを写す
            _selectedThemeColor = item.ThemeKey;
            ApplyColor(item.Value, raiseChanged: true);

            // 選んだら一覧を閉じる（開いたままにすると、選んだ色が一覧の裏に隠れて確認できない。 他 UI も選ぶと閉じる）
            _listPopup.Close();
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

        // 開くたびに作り直す（テーマの色は Application.Run 後にしか解決できず、明暗やアクセント色の
        // 変更にも追随させる必要があるため、構築時に作った一覧をそのまま使わない）
        RefreshList();

        // アンカーは**ウィンドウ座標**である（自領域の座標ではない）。 自領域の矩形をそのまま渡すとウィンドウ左上へ出る
        // アンカーは ▼ のボタン（自領域だと入力欄の左端から開いてしまう）
        _listPopup.ShowAt(this, _listButton.Bounds, PopupAnchorSide.Below);
        _listButton.Content("▲");
        _log.LogOpenList(_selectedColor);
    }

    /// <summary>カラーピッカーのドロップダウンを開閉する。</summary>
    private void TogglePicker()
    {
        if (ColorString.TryParse(_selectedColor, out var color, out _))
        {
            _picker.SelectedColor = ToMewColor(color);
        }

        _picker.IsDropDownOpen = !_picker.IsDropDownOpen;
        if (_picker.IsDropDownOpen)
        {
            // ピッカーを開いたことを残す（一覧を開いたログと対。 他 UI と同じ書式）
            _log.LogOpenPicker(_selectedColor);
        }
    }

    /// <summary>コアの色を MewUI の色へ写す。</summary>
    /// <param name="color">コアの色。</param>
    /// <returns>同じ ARGB を持つ MewUI の色。</returns>
    private static MewColor ToMewColor(DrawingColor color)
        => MewColor.FromArgb(color.A, color.R, color.G, color.B);
}