using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Reactive;
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.AvaloniaUI.ColorSelector.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.Ui.AvaloniaUI.ColorSelector;

/// <summary>色を選ぶコントロール（色名の入力・一覧からの選択・カラーピッカー）。</summary>
/// <remarks>
/// <b>1 つの入力欄に見せる</b>: 外周の 1 つの <c>Border</c> の中へ「スウォッチ（現在色）」「テキスト入力」
/// 「一覧ボタン（▼）」「カラーピッカー ボタン（…）」を隙間なく並べ、区切りは枠より薄い垂直線で表す
/// （区画のボタン自身は枠も背景も持たない）。<br/>
/// <b>規則の所有者はコアである</b>: 受理する入力（色名・16 進・<c>+{n}%</c>）、色名の表、保存値（<c>#AARRGGBB</c>）と
/// 画面表示（<c>{色}+{不透明度%}</c>）、並び順、入力の妥当性は
/// <see cref="EsUtil.Platform.Core.Palette"/>（<see cref="ColorString"/> / <see cref="ColorEntry"/> /
/// <see cref="ColorValidation"/>）が単一所有する。 本クラスは要素の組み立てと、入力と状態の同期だけを行う。<br/>
/// <b>ツールチップ</b>: スウォッチとテキスト部には「色名+不透明度」と「<c>#AARRGGBB</c>」の 2 行を出す。<br/>
/// <b>カラーピッカー</b>は <see cref="ColorSelectorFlyoutFactory"/> が構築する（OK / キャンセル付き）。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - コンストラクタ: <see cref="ColorSelectorControl()"/><br/>
/// - 値: <see cref="SelectedColor"/> / <see cref="ColorSortMode"/> / <see cref="TextFormat"/><br/>
/// - 検証: <see cref="HasValidationError"/> / <see cref="ValidationErrorMessage"/> / <see cref="HighlightBorderOnError"/><br/>
/// - 外観（入力欄の文字サイズ）: <see cref="TextFontSize"/><br/>
/// - ログ: <see cref="Log"/><br/>
/// - 関連型: <see cref="ColorSortMode"/>（コア）/ <see cref="ColorTextFormat"/>（コア）/ <see cref="StringToBrushConverter"/> /
///   <see cref="ColorString"/>（コア）/ <see cref="ColorEntry"/>（コア）
/// </remarks>
public sealed partial class ColorSelectorControl : UserControl
{
    // ====================================================================
    // 依存関係プロパティ（StyledProperty）
    // ====================================================================

    /// <summary><see cref="SelectedColor"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<string> SelectedColorProperty =
        AvaloniaProperty.Register<ColorSelectorControl, string>(nameof(SelectedColor),
            defaultValue: "White", defaultBindingMode: BindingMode.TwoWay);

    /// <summary><see cref="ColorSortMode"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<ColorSortMode> ColorSortModeProperty =
        AvaloniaProperty.Register<ColorSelectorControl, ColorSortMode>(nameof(ColorSortMode), defaultValue: ColorSortMode.ByHue);

    /// <summary><see cref="SelectedThemeColor"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<string?> SelectedThemeColorProperty =
        AvaloniaProperty.Register<ColorSelectorControl, string?>(
            nameof(SelectedThemeColor), defaultBindingMode: BindingMode.TwoWay);

    /// <summary><see cref="TextFormat"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<ColorTextFormat> TextFormatProperty =
        AvaloniaProperty.Register<ColorSelectorControl, ColorTextFormat>(
            nameof(TextFormat), defaultValue: ColorTextFormat.NameWithAlpha);

    /// <summary><see cref="HasValidationError"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> HasValidationErrorProperty =
        AvaloniaProperty.Register<ColorSelectorControl, bool>(nameof(HasValidationError), defaultValue: false);

    /// <summary><see cref="ValidationErrorMessage"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<string?> ValidationErrorMessageProperty =
        AvaloniaProperty.Register<ColorSelectorControl, string?>(nameof(ValidationErrorMessage));

    /// <summary><see cref="HighlightBorderOnError"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> HighlightBorderOnErrorProperty =
        AvaloniaProperty.Register<ColorSelectorControl, bool>(nameof(HighlightBorderOnError), defaultValue: false);


    /// <summary><see cref="TextFontSize"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> TextFontSizeProperty =
        AvaloniaProperty.Register<ColorSelectorControl, double>(nameof(TextFontSize), defaultValue: double.NaN);



    // ====================================================================
    // 定数
    // ====================================================================

    // ====================================================================
    // 状態
    // ====================================================================

    /// <summary>開いている一覧（タブ付きのフライアウト）。 閉じるまで保持する。</summary>
    private Flyout? _flyout;

    /// <summary>コンボボックスの標準の一覧を閉じている最中かどうか（開閉の往復を防ぐ）。</summary>
    private bool _suppressDropDown;

    /// <summary>最後に有効だった色（無効な入力の復帰先）。</summary>
    private string? _lastValidColor;

    /// <summary>入力の反映中かどうか（入力と状態の往復でイベントが循環するのを防ぐ）。</summary>
    private bool _applying;

    /// <summary>保存値の正規化中かどうか（正規化の再入を防ぐ）。</summary>
    private bool _normalizing;

    /// <summary>テーマの色を解決して <see cref="SelectedColor"/> へ書いている間かどうか。</summary>
    /// <remarks>解決の書き込みで「利用者が色を選び直した」と解釈してテーマの識別子を消さないために持つ。</remarks>
    private bool _resolvingThemeColor;

    /// <summary>内部ロガー。</summary>

    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞。</summary>
    private string? _logPrefix;

    // ====================================================================
    // 値
    // ====================================================================

    /// <summary>現在選択されている色（双方向バインド用）。</summary>
    /// <value>保存値は <c>#AARRGGBB</c>（アルファ先頭16進）形式（例: "#80FF0000"）。<br/>
    /// 色名や <c>+{n}%</c> 形式を設定することもできる（正規化は <see cref="ColorString"/> で行う）。<br/>
    /// 入力欄の表示は <see cref="TextFormat"/> が決める。</value>
    public string SelectedColor
    {
        get => GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    /// <summary>一覧の色の並び順を取得または設定する。</summary>
    /// <value>色名順 (<see cref="ColorSortMode.ByName"/>) または色相順 (<see cref="ColorSortMode.ByHue"/>)。</value>
    public ColorSortMode ColorSortMode
    {
        get => GetValue(ColorSortModeProperty);
        set => SetValue(ColorSortModeProperty, value);
    }

    /// <summary>選択中のテーマの色の識別子を取得または設定する（例: <c>背景</c>）。</summary>
    /// <value>テーマの色を選んでいるときの識別子。 色名・カラーコードを選んでいるときは null。</value>
    /// <remarks>
    /// <b>テーマの色は名前で覚える</b>: 選んだ時点の色（<c>#AARRGGBB</c>）を保存すると、明暗の切替やアクセント色の変更へ<br/>
    /// 追随できない。 本プロパティが識別子を持つ間は、<see cref="SelectedColor"/> を<b>そのときのテーマの色</b>へ<br/>
    /// 解決し直す（テーマが変わると <see cref="SelectedColor"/> も変わり、変更イベントが届く）。<br/>
    /// 利用側は本値を保存すれば、次回の起動でもテーマに追随する（テーマの色でないときは null を保存する）。
    /// </remarks>
    public string? SelectedThemeColor
    {
        get => GetValue(SelectedThemeColorProperty);
        set => SetValue(SelectedThemeColorProperty, value);
    }

    /// <summary>一覧の先頭に UI のテーマの色（背景・文字・アクセント）を並べるかどうかを取得する。</summary>
    /// <value>並べる場合は true（既定）。</value>
    /// <remarks>
    /// <b>値は UI のテーマから取る</b>: テーマの色は UI ごとに異なるため、コアの色名の表には含めない<br/>
    /// （一覧を開くたびに解決するため、明暗の切替とアクセント色の変更に追随する）。
    /// </remarks>
    /// <summary>一覧の先頭に UI のテーマの色を並べるかどうかを取得または設定する。</summary>
    /// <value>並べる場合は true（既定）。</value>
    /// <remarks>
    /// <b>値は UI のテーマから取る</b>: テーマの色は UI ごとに異なるため、コアの色名の表には含めない<br/>
    /// （一覧を作るたびに解決するため、明暗の切替とアクセント色の変更に追随する）。<br/>
    /// <b>並べない場合はコアの色名だけ</b>になる（一覧は次に開くときに作り直す）。
    /// </remarks>
    public bool ShowThemeColors { get; set; } = true;

    /// <summary>一覧の「カスタム」タブを表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    /// <remarks>中身（<see cref="CustomTabs"/>）が空のときは表示しない（タブだけが出て一覧が空になるのを防ぐ）。</remarks>
    public bool ShowCustomTab { get; set; } = true;

    /// <summary>一覧の「ColorPicker」タブを表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    /// <remarks>タブを開くとカラーピッカー（<c>ColorView</c>）を一覧の中へ直接表示する（別のフライアウトを重ねない）。</remarks>
    public bool ShowPickerTab { get; set; } = true;

    /// <summary>一覧の「テーマカラー」タブを表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    /// <remarks>テーマの色を使わないアプリでは隠せる（タブが 1 つになれば題名も隠せる）。</remarks>
    public bool ShowThemeColorTab { get; set; } = true;

    /// <summary>一覧の「色名」タブを表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    public bool ShowNamedColorTab { get; set; } = true;

    /// <summary>一覧のタブの題名を表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    /// <remarks>
    /// <b>タブが 1 つだけのときに効く</b>: 切り替える相手が無いため、題名を省いて一覧を狭くできる。<br/>
    /// タブが 2 つ以上あるときは切り替えに必要なので、本値に関わらず表示する。
    /// </remarks>
    public bool ShowTabTitles { get; set; } = true;

    /// <summary>一覧へ追加する「カスタム」タブを取得または設定する。</summary>
    /// <value>追加するタブ。 空（既定）の場合は追加しない。</value>
    /// <remarks>
    /// <b>代表色をすぐ選べるようにする</b>: 用途ごとのタブ（例: 枠線の色）を足せる。 空のタブは表示しない。<br/>
    /// 一覧は開くたびに作るため、設定を差し替えたあとの一覧から反映される。
    /// </remarks>
    public IReadOnlyList<ColorSelectorCustomTab> CustomTabs { get; set; } = [];

    /// <summary>入力欄の表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（既定）またはカラーコード。 入力欄は編集できるため、表記と異なる形の入力も受理する。</value>
    public ColorTextFormat TextFormat
    {
        get => GetValue(TextFormatProperty);
        set => SetValue(TextFormatProperty, value);
    }

    // ====================================================================
    // 検証
    // ====================================================================

    /// <summary>バリデーションエラーが有効かどうかを取得する。</summary>
    /// <value>現在の <see cref="SelectedColor"/> が無効な場合 true。</value>
    public bool HasValidationError
    {
        get => GetValue(HasValidationErrorProperty);
        private set => SetValue(HasValidationErrorProperty, value);
    }

    /// <summary>バリデーションエラーメッセージを取得する。</summary>
    /// <value>無効な場合の理由。 有効な場合は null。</value>
    public string? ValidationErrorMessage
    {
        get => GetValue(ValidationErrorMessageProperty);
        private set => SetValue(ValidationErrorMessageProperty, value);
    }

    /// <summary>エラー時に枠線を強調するかどうかを取得または設定する。</summary>
    /// <value>true の場合、バリデーションエラー時に外周の枠線が赤くなる。</value>
    public bool HighlightBorderOnError
    {
        get => GetValue(HighlightBorderOnErrorProperty);
        set => SetValue(HighlightBorderOnErrorProperty, value);
    }

    // ====================================================================
    // 外観
    // ====================================================================


    /// <summary>入力欄の文字サイズ（DIP）を取得または設定する。</summary>
    /// <value>文字サイズ。 <see cref="double.NaN"/>（既定）の場合は親（テーマ）の値を継承する。</value>
    public double TextFontSize
    {
        get => GetValue(TextFontSizeProperty);
        set => SetValue(TextFontSizeProperty, value);
    }

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>内部ロガーを取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は <see cref="NullLogger"/> を使う。</value>
    public ILogger Log
    {
        get => _log;
        set => _log = value ?? NullLogger.Instance;
    }

    /// <summary>ログの接頭辞を取得または設定する。</summary>
    /// <value>接頭辞。 null の場合は既定値を使う。</value>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            ColorSelectorLogMessage.Prefix = value ?? ColorSelectorLogMessage.DefaultPrefix;
        }
    }

    // ====================================================================
    // コンストラクタ・初期化
    // ====================================================================

    /// <summary>コントロールを生成する。</summary>
    public ColorSelectorControl()
    {
        InitializeComponent();

        // 入力はコンボボックスの Text（編集可能）で受ける。 プログラムからの変更も同じ経路で同期する
        // （TextChanged はプログラムからの変更では発火しないため、Text プロパティの変更を直接購読する）
        _combo.GetObservable(ComboBox.TextProperty).Subscribe(new AnonymousObserver<string?>(OnTextValueChanged));
        _combo.LostFocus += OnTextLostFocus;

        // 標準のコンボボックスは自前の一覧を持つため、開いた直後に閉じてタブ付きのフライアウトを出す
        // （見た目を似せて作らず、コンボボックスをそのまま使う）
        _combo.DropDownOpened += OnDropDownOpened;

        if (!Design.IsDesignMode)
        {
            // SelectedColor は 1 回だけ取得して初期値とバリデーションで共有する（二重 getter の排除）
            var initialColor = SelectedColor;
            _lastValidColor = initialColor;
            UpdateTextAndToolTip(initialColor);
            UpdateValidationStatus(initialColor);
        }

        ApplyTextSize();

        // テーマが変わったら、テーマの色を選んでいる間はその色を解決し直す
        ActualThemeVariantChanged += (_, _) => ReapplyThemeColor();
    }

    /// <summary>コンボボックスを開いたときに、標準の一覧の代わりにタブ付きのフライアウトを出す。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// <b>標準の一覧は使わない</b>: 一覧はタブ（カスタム / テーマカラー / 色名 / ColorPicker）で切り替えるため、
    /// コンボボックスが持つ一覧は開いた直後に閉じる（開閉の往復を防ぐため <see cref="_suppressDropDown"/> で抑止する）。
    /// </remarks>
    private void OnDropDownOpened(object? sender, EventArgs e)
    {
        if (_suppressDropDown)
        {
            return;
        }

        _suppressDropDown = true;
        try
        {
            _combo.IsDropDownOpen = false;
        }
        finally
        {
            _suppressDropDown = false;
        }

        _flyout?.Hide();
        if (CreateListFlyout() is not { } flyout)
        {
            return;
        }

        _flyout = flyout;
        Log.LogOpenList(SelectedColor);
        flyout.ShowAt(_combo);
    }

    /// <summary>入力欄（コンボボックス）の文字サイズを反映する。</summary>
    /// <remarks>未設定（NaN）は自分の値を持たせず親（テーマ）の値を継承させる。</remarks>
    private void ApplyTextSize()
    {
        if (double.IsNaN(TextFontSize))
        {
            _combo.ClearValue(TemplatedControl.FontSizeProperty);
        }
        else
        {
            _combo.FontSize = TextFontSize;
        }
    }

    /// <summary>テーマの色を選んでいる間、そのときのテーマの色へ解決し直す。</summary>
    private void ReapplyThemeColor()
    {
        if (SelectedThemeColor is { } key && TryResolveThemeColor(key, out var hex))
        {
            ApplyResolvedThemeColor(hex);
        }
    }

    /// <summary>解決したテーマの色を <see cref="SelectedColor"/> へ書く（識別子は消さない）。</summary>
    /// <param name="hex">解決した色（<c>#AARRGGBB</c>）。</param>
    private void ApplyResolvedThemeColor(string hex)
    {
        if (string.Equals(SelectedColor, hex, StringComparison.Ordinal))
        {
            return;
        }

        _resolvingThemeColor = true;
        try
        {
            SelectedColor = hex;
        }
        finally
        {
            _resolvingThemeColor = false;
        }
    }

    /// <summary>テーマの色の識別子から、そのときのテーマの色を解決する。</summary>
    /// <param name="key">識別子（例: <c>背景</c>）。</param>
    /// <param name="hex">解決した色（<c>#AARRGGBB</c>）。</param>
    /// <returns>解決できた場合は true（その UI に無い色は false）。</returns>
    /// <remarks>
    /// <b>一覧を作る処理と同じ解決を使う</b>: テーマの色の値は一覧を作るたびに UI から読むため、<br/>
    /// 別の表を持つと一覧の表示と解決した色が食い違う。
    /// </remarks>
    public bool TryResolveThemeColor(string key, out string hex)
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

    /// <summary><see cref="_combo"/>（入力欄を兼ねるコンボボックス）。</summary>
    private ComboBox _combo => PART_Combo;

    // ====================================================================
    // プロパティ変更
    // ====================================================================

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedColorProperty)
        {
            // change.NewValue / OldValue は StyledProperty<string> により常に string（分岐なし）
            var color = (string)change.NewValue!;

            // 保存値は #AARRGGBB へ正規化する（色名や #RRGGBB を設定しても保存値の形を保つ）。
            // 正規化した値の通知で残りの処理を行う（同じ処理を 2 回書かない）
            var normalized = ColorString.ToArgbHex(color);
            if (!_normalizing && !string.Equals(normalized, color, StringComparison.Ordinal))
            {
                _normalizing = true;
                try
                {
                    SetCurrentValue(SelectedColorProperty, normalized);
                }
                finally
                {
                    _normalizing = false;
                }

                return;
            }

            // 色を直接選び直したらテーマの識別子は外す（テーマの解決で書いた場合は除く）。
            // 利用者が入力欄へ打ち込んだ場合もここへ来る（_applying の間は入力欄と往復しているだけなので外さない）
            if (!_resolvingThemeColor && !_applying && SelectedThemeColor is not null)
            {
                SetCurrentValue(SelectedThemeColorProperty, null);
            }

            Log.LogSelectedColorChanged((string)change.OldValue!, color);

            // 解析は 1 回だけ行い、最終有効値の追跡とバリデーション更新で共有する（二重解析の排除）
            var valid = ColorValidation.IsValid(color);
            if (valid)
            {
                _lastValidColor = color;
            }

            UpdateValidationStatusCore(color, valid);

            // 入力中の文字を打ち消さないよう、入力欄からの反映では書き戻さない
            if (!_applying)
            {
                UpdateTextAndToolTip(color);
            }
        }
        else if (change.Property == SelectedThemeColorProperty)
        {
            // 識別子を設定されたら、そのときのテーマの色へ解決する（テーマが変わると OnThemeChanged が解決し直す）
            if (change.NewValue is string key && TryResolveThemeColor(key, out var hex))
            {
                ApplyResolvedThemeColor(hex);
            }
        }
        else if (change.Property == TextFormatProperty)
        {
            UpdateTextAndToolTip(SelectedColor);
        }
        else if (change.Property == HighlightBorderOnErrorProperty)
        {
            UpdateBorderValidationVisual();
        }
        else if (change.Property == TextFontSizeProperty)
        {
            ApplyTextSize();
        }

    }

    // ====================================================================
    // 入力イベント処理・非公開 UI ロジック
    // ====================================================================

    /// <summary>入力欄へ色の表記とツールチップを反映する。</summary>
    /// <param name="color">表示する色（保存値）。</param>
    /// <remarks>
    /// ツールチップは 2 行（色名+不透明度 / カラーコード）を出す。<br/>
    /// <b>カスタム タブの色名を優先する</b>: 利用者が付けた名前（例: 枠線の赤）で表示されるため、
    /// 色コードだけを見比べなくても意図した色かどうかが分かる。
    /// </remarks>
    private void UpdateTextAndToolTip(string? color)
    {
        _applying = true;
        try
        {
            var normalized = ColorString.ToArgbHex(color);

            // テーマの色を選んでいる間は識別子を出す（色を写すと、テーマに追随していることが分からない）
            _combo.Text = SelectedThemeColor is { } key
                ? ThemeLabelPrefix + key
                : FormatDisplayText(normalized);

            var tooltip = string.Concat(
                ColorString.ToDisplayText(normalized), Environment.NewLine, ColorString.ToArgbHex(normalized));
            ToolTip.SetTip(_combo, tooltip);
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>入力欄へ出す文字列を決める（カスタム タブの色名を優先する）。</summary>
    /// <param name="normalized">保存値（<c>#AARRGGBB</c>）。</param>
    /// <returns>表示する文字列。</returns>
    /// <remarks>不透明度があるときは色名へ <c>+{n}%</c> を足す（色名だけでは不透明度が伝わらない）。</remarks>
    private string FormatDisplayText(string normalized)
    {
        if (TextFormat == ColorTextFormat.NameWithAlpha && ResolveCustomName(normalized) is { } name)
        {
            return ColorString.TryParse(normalized, out _, out var opacity) && opacity < 0.999
                ? string.Concat(name, "+", ((int)Math.Round(opacity * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture), "%")
                : name;
        }

        return ColorString.ToText(normalized, TextFormat);
    }

    /// <summary>色に一致するカスタム タブの色名を探す。</summary>
    /// <param name="normalized">保存値（<c>#AARRGGBB</c>）。</param>
    /// <returns>見つかった色名。 無い場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>値は色として比べる</b>: 登録が色コードでも色名でも同じ色なら一致させる（文字列の一致では
    /// <c>Red</c> と <c>#FFFF0000</c> が別物になる）。
    /// </remarks>
    private string? ResolveCustomName(string normalized)
    {
        if (!ColorString.TryParse(normalized, out var target, out _))
        {
            return null;
        }

        foreach (var tab in CustomTabs)
        {
            if (tab is null)
            {
                continue;
            }

            foreach (var entry in tab.Colors)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.Value))
                {
                    continue;
                }

                if (ColorString.TryParse(entry.Value, out var candidate, out _)
                    && candidate.R == target.R && candidate.G == target.G && candidate.B == target.B)
                {
                    return entry.Name;
                }
            }
        }

        return null;
    }

    /// <summary>入力欄の変更を <see cref="SelectedColor"/> へ反映する。</summary>
    /// <param name="text">入力欄の文字列。</param>
    private void OnTextValueChanged(string? text)
    {
        if (_applying)
        {
            return;
        }

        _applying = true;
        try
        {
            SelectedColor = text ?? string.Empty;
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>フォーカス喪失時、無効な色を最終有効値に戻す。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// 最終有効値が存在し現在の値が無効な場合は復帰する（復帰後の再検証は
    /// <see cref="OnPropertyChanged(AvaloniaPropertyChangedEventArgs)"/> が行う）。<br/>
    /// 解析は 1 回だけ行い、復帰判定・バリデーション更新・ログ判定で共有する。
    /// </remarks>
    private void OnTextLostFocus(object? sender, RoutedEventArgs e)
    {
        var color = SelectedColor;
        var valid = ColorValidation.IsValid(color);
        var hasLastValid = _lastValidColor is not null;

        // 無効な入力を最終有効値へ復帰するかは純粋関数（ColorValidation.ShouldRecover）で判定する
        if (ColorValidation.ShouldRecover(valid, hasLastValid))
        {
            // 無効な入力は最終有効値へ復帰する（再検証は OnPropertyChanged が行う）
            color = _lastValidColor!;
            SelectedColor = color;
            UpdateTextAndToolTip(color);
        }
        else
        {
            UpdateValidationStatusCore(color, valid);
        }

        // ログ用の最終有効性（復帰時は復帰値が有効なため true）
        Log.LogLostFocus(color, valid || hasLastValid);
    }

    /// <summary>一覧の上部に置くタブの幅（DIP）。 2 つを並べたときに一覧の幅と釣り合う値。</summary>
    private const double CategoryTabWidth = 112;

    /// <summary>一覧の最大の高さ（DIP）。</summary>
    private const double ListMaxHeight = 360;

    /// <summary>一覧の幅に足す余白（DIP。 枠・項目の左右の余白・スクロール バー）。</summary>
    private const double ListChromeWidth = 28;

    /// <summary>直近に開いた一覧のタブの識別子（次に開いたときに同じタブから始める）。</summary>
    private string? _listTabKey;

    /// <summary>一覧の分類を切り替えるタブを作る。</summary>
    /// <param name="text">タブの文言。</param>
    /// <returns>タブ（<see cref="ToggleButton"/>）。</returns>
    /// <remarks>片方だけが選ばれるよう、切り替えは一覧を開く処理の <c>ApplyCategory</c> が両方の状態を書く。</remarks>
    private static ToggleButton CreateCategoryTab(string text) => new()
    {
        Content = text,
        Width = CategoryTabWidth,
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        IsChecked = false,
    };

    /// <summary>色の四角の一辺（DIP。 格子のセル）。</summary>
    private const double SwatchCellSize = 32;

    /// <summary>格子に並べる列数（16 色なら 3 行に収まり、スクロールせずに選べる）。</summary>
    private const int SwatchGridColumns = 6;

    /// <summary>一覧のポップアップ（タブ + 選んだタブの中身）を作る。</summary>
    /// <returns>作ったポップアップ。 表示できるタブが 1 つも無い場合は null。</returns>
    /// <remarks>
    /// <b>タブは分類だけを切り替える</b>: テーマの色が多いため、1 つの一覧へ混ぜると色名を探しにくい。<br/>
    /// <b>タブの並びは「カスタム → テーマカラー → 色名 → ColorPicker」</b>である（利用者が用意した色を先に出す）。<br/>
    /// 前回選んだタブから開く（同じ用途で続けて選ぶときに切り替えの手間を省く）。<br/>
    /// <b>タブが 1 つなら題名を隠せる</b>（<see cref="ShowTabTitles"/>）。 切り替える相手が無いため、一覧を狭くできる。
    /// </remarks>
    internal Flyout? CreateListFlyout()
    {
        var tabs = CreateTabs();
        if (tabs.Count == 0)
        {
            return null;
        }

        var tabStrip = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4 };
        var body = new Panel();
        var panel = new StackPanel { Spacing = 6 };

        // ▼ はコンボボックスの右端にあるため、左端を揃えて開く（右端を揃えると一覧が左へ大きくずれる）
        var flyout = new Flyout { Content = panel, Placement = PlacementMode.BottomEdgeAlignedLeft };

        var tabButtons = new List<ToggleButton>(tabs.Count);
        var views = new List<Control>(tabs.Count);
        for (var i = 0; i < tabs.Count; i++)
        {
            var index = i;
            var button = CreateCategoryTab(tabs[i].Title);
            button.Click += (_, _) => ApplyTab(index);
            tabButtons.Add(button);
            tabStrip.Children.Add(button);

            var view = CreateTabView(tabs[i], flyout);
            view.IsVisible = false;
            views.Add(view);
            body.Children.Add(view);
        }

        if (tabs.Count > 1 || ShowTabTitles)
        {
            panel.Children.Add(tabStrip);
        }

        panel.Children.Add(body);

        var lastIndex = tabs.FindIndex(tab => string.Equals(tab.Key, _listTabKey, StringComparison.Ordinal));
        ApplyTab(lastIndex >= 0 ? lastIndex : 0);
        return flyout;

        void ApplyTab(int index)
        {
            _listTabKey = tabs[index].Key;
            for (var i = 0; i < tabButtons.Count; i++)
            {
                tabButtons[i].IsChecked = i == index;
                views[i].IsVisible = i == index;
            }
        }
    }

    /// <summary>タブの中身を作る（一覧・格子・カラーピッカー）。</summary>
    /// <param name="tab">タブの定義。</param>
    /// <param name="flyout">閉じる対象のポップアップ。</param>
    /// <returns>タブの中身。</returns>
    private Control CreateTabView(ColorSelectorTabSpec tab, Flyout flyout)
    {
        if (tab.IsPicker)
        {
            Log.LogOpenPicker(SelectedColor);
            return ColorSelectorFlyoutFactory.CreateContent(
                SelectedColor,
                Log,
                value =>
                {
                    SelectedColor = value;
                    UpdateTextAndToolTip(SelectedColor);
                    flyout.Hide();
                },
                flyout.Hide);
        }

        return tab.Layout == ColorSelectorTabLayout.Grid
            ? CreateSwatchGrid(tab, flyout)
            : CreateColorList(tab, flyout);
    }

    /// <summary>色名付きの一覧（縦に並べてスクロールする）を作る。</summary>
    /// <param name="tab">タブの定義。</param>
    /// <param name="flyout">閉じる対象のポップアップ。</param>
    /// <returns>一覧。</returns>
    private Control CreateColorList(ColorSelectorTabSpec tab, Flyout flyout)
    {
        var list = new ListBox
        {
            ItemTemplate = (IDataTemplate?)Resources["ColorEntryTemplate"],
            MaxHeight = ListMaxHeight,

            // 左寄せにすると希望サイズが内容（行の実寸）になり、長い名前が切れない
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            ItemsSource = tab.Items,
        };
        ApplyListWidth(list, tab.Items);
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is ColorSelectorItem item)
            {
                ApplyItem(item, flyout);
            }
        };

        return list;
    }

    /// <summary>色の四角だけを 6 列に並べた格子を作る（スクロールせずに選べる）。</summary>
    /// <param name="tab">タブの定義。</param>
    /// <param name="flyout">閉じる対象のポップアップ。</param>
    /// <returns>格子。</returns>
    private Control CreateSwatchGrid(ColorSelectorTabSpec tab, Flyout flyout)
    {
        var grid = new WrapPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            ItemWidth = SwatchCellSize,
            ItemHeight = SwatchCellSize,
            Width = SwatchCellSize * SwatchGridColumns,
        };

        foreach (var item in tab.Items)
        {
            grid.Children.Add(CreateSwatch(item, flyout));
        }

        return grid;
    }

    /// <summary>格子の 1 マス（色の四角）を作る。</summary>
    /// <param name="item">並べる色。</param>
    /// <param name="flyout">閉じる対象のポップアップ。</param>
    /// <returns>色の四角を入れたボタン（名前はツールチップで示す）。</returns>
    private Control CreateSwatch(ColorSelectorItem item, Flyout flyout)
    {
        IBrush brush = ColorString.TryParse(item.Value, out var parsed, out _)
            ? new SolidColorBrush(Avalonia.Media.Color.FromArgb(parsed.A, parsed.R, parsed.G, parsed.B))
            : Brushes.Transparent;

        var button = new Button
        {
            Width = SwatchCellSize,
            Height = SwatchCellSize,
            Padding = new Thickness(2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Content = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(3),
                BorderBrush = new SolidColorBrush(Avalonia.Media.Color.FromArgb(64, 128, 128, 128)),
                BorderThickness = new Thickness(1),
                Background = brush,
            },
        };
        ToolTip.SetTip(button, item.Label);
        button.Click += (_, _) => ApplyItem(item, flyout);
        return button;
    }

    /// <summary>選んだ色を反映して一覧を閉じる。</summary>
    /// <param name="item">選んだ色。</param>
    /// <param name="flyout">閉じるポップアップ。</param>
    private void ApplyItem(ColorSelectorItem item, Flyout flyout)
    {
        // テーマの色は識別子を覚える（明暗・アクセントの変更へ追随させる）。 色名・カスタムは値だけを写す
        SetCurrentValue(SelectedThemeColorProperty, item.ThemeKey);
        SelectedColor = item.Value;
        UpdateTextAndToolTip(SelectedColor);
        flyout.Hide();
    }

    /// <summary>一覧に出すタブ（題名・識別子・並べ方・並べる項目）を作る。</summary>
    /// <returns>表示するタブ。 中身の無いタブは含めない。</returns>
    /// <remarks>
    /// <b>表示の設定と中身を 1 か所で決める</b>: タブの有無と一覧の項目が食い違わない（空のタブを出さない）。<br/>
    /// <b>並びは「カスタム → テーマカラー → 色名 → ColorPicker」</b>である（利用者が用意した色を先に出す）。
    /// </remarks>
    internal List<ColorSelectorTabSpec> CreateTabs()
    {
        var tabs = new List<ColorSelectorTabSpec>();

        if (ShowCustomTab)
        {
            foreach (var custom in CustomTabs)
            {
                if (custom is null || custom.Colors.Count == 0)
                {
                    continue;
                }

                var entries = custom.Colors
                    .Where(color => color is not null && !string.IsNullOrWhiteSpace(color.Value))
                    .Select(color => new ColorSelectorItem(color.Name, color.Value, ColorSelectorCategory.Custom))
                    .ToList();
                if (entries.Count > 0)
                {
                    tabs.Add(new ColorSelectorTabSpec(custom.Title, "custom:" + custom.Title, custom.Layout, entries));
                }
            }
        }

        var items = CreateItems();
        if (ShowThemeColorTab)
        {
            var theme = items.Where(item => item.Category == ColorSelectorCategory.Theme).ToList();
            if (theme.Count > 0)
            {
                tabs.Add(new ColorSelectorTabSpec("テーマカラー", "theme", ColorSelectorTabLayout.List, theme));
            }
        }

        if (ShowNamedColorTab)
        {
            var named = items.Where(item => item.Category == ColorSelectorCategory.Named).ToList();
            if (named.Count > 0)
            {
                tabs.Add(new ColorSelectorTabSpec("色名", "named", ColorSelectorTabLayout.List, named));
            }
        }

        if (ShowPickerTab)
        {
            tabs.Add(new ColorSelectorTabSpec("ColorPicker", "picker", ColorSelectorTabLayout.List, [], IsPicker: true));
        }

        return tabs;
    }

    /// <summary>一覧の幅を枠（入力欄）の幅と内容の幅に合わせる（コントロールの幅は変えない）。</summary>
    /// <param name="list">対象の一覧。</param>
    /// <param name="entries">一覧に並べる項目。</param>
    /// <remarks>
    /// <b>枠の幅は変えない</b>: 一覧を開くたびに枠が広がると、開閉のたびに周りのレイアウトが動く（実測で指摘）。<br/>
    /// 一覧（ポップアップ）だけを内容に合わせて広げる。
    /// </remarks>
    internal void ApplyListWidth(ListBox list, IReadOnlyList<ColorSelectorItem> entries)
    {
        var frameWidth = Bounds.Width;
        var contentWidth = MeasureWidestRow(entries) + ListChromeWidth;
        list.Width = Math.Max(frameWidth, contentWidth);
    }

    /// <summary>行のうち最も広いものの幅（実寸）を測る。</summary>
    /// <param name="entries">測る項目。</param>
    /// <returns>最も広い行の幅（DIP）。 測れない場合は 0。</returns>
    /// <remarks>一覧の項目テンプレートと同じ要素を作って測る（見積もりだと文字の実寸とずれる）。</remarks>
    private double MeasureWidestRow(IEnumerable<ColorSelectorItem> entries)
    {
        if (Resources["ColorEntryTemplate"] is not IDataTemplate template)
        {
            return 0;
        }

        var widest = 0.0;
        foreach (var entry in entries)
        {
            if (template.Build(entry) is not Control row)
            {
                continue;
            }

            row.Measure(Size.Infinity);
            widest = Math.Max(widest, row.DesiredSize.Width);
        }

        return widest;
    }

    /// <summary>一覧に並べる項目を作る（テーマの色を先頭へ置く）。</summary>
    /// <returns>表示名と適用値の組。</returns>
    /// <remarks>
    /// <b>テーマの色は開くたびに解決する</b>: 明暗の切替やアクセント色の変更に追随させるため、値を作り置きしない<br/>
    /// （一覧は開くたびに作り直される）。 解決できない色（地色が透明など）は並べない。
    /// </remarks>
    internal List<ColorSelectorItem> CreateItems()
    {
        var items = new List<ColorSelectorItem>();
        if (ShowThemeColors)
        {
            AddThemeColors(this, items);
        }

        foreach (var entry in ColorEntry.GetSorted(ColorSortMode))
        {
            items.Add(new ColorSelectorItem(entry.Name, entry.Name, ColorSelectorCategory.Named));
        }

        return items;
    }

    /// <summary>テーマの色（識別子と、そのときの色を解決する関数）。 並び順がそのまま一覧の順序になる。</summary>
    /// <remarks>
    /// <b>識別子と解決を 1 箇所に置く</b>: 一覧の表示と <see cref="SelectedThemeColor"/> の解決が同じ出所になり、<br/>
    /// 表を 2 つ持って食い違うことがない（<see cref="ThemeColorKeys"/> も本表から作る）。
    /// </remarks>
    private static readonly (string Key, Func<ColorSelectorControl, IBrush?> Resolve)[] s_themeColors =
    [
        // 3 UI で共通の意味を持つ色（同じ名前・同じ順）
        ("背景", static control => TopLevel.GetTopLevel(control)?.Background),
        ("レイヤー", static control => control.ResolveThemeBrush("SystemControlBackgroundAltHighBrush")),
        ("カード", static control => control.ResolveThemeBrush("SystemControlBackgroundChromeMediumLowBrush")),
        ("コントロールの背景", static control => control.ResolveThemeBrush("TextControlBackground")),
        ("コントロールの枠線", static control => control.ResolveThemeBrush("TextControlBorderBrush")),
        ("区切り線", static control => control.ResolveThemeBrush("SystemControlForegroundBaseLowBrush")),
        ("文字", static control => control.ResolveThemeBrush("SystemControlForegroundBaseHighBrush") ?? control.Foreground),
        ("副次テキスト", static control => control.ResolveThemeBrush("SystemControlForegroundBaseMediumBrush")),
        ("無効の文字", static control => control.ResolveThemeBrush("SystemControlDisabledBaseMediumLowBrush")),
        ("無効の背景", static control => control.ResolveThemeBrush("SystemControlBackgroundBaseLowBrush")),
        ("アクセント", static _ => ResolveAccentBrush()),
        ("アクセント（淡）", static control => control.ResolveThemeBrush("SystemAccentColorLight1")),
        ("アクセント（濃）", static control => control.ResolveThemeBrush("SystemAccentColorDark1")),
        ("アクセント文字", static control => control.ResolveThemeBrush("SystemControlForegroundAccentBrush")),
        ("アクセント上の文字", static control => control.ResolveThemeBrush("SystemControlForegroundChromeWhiteBrush")),
        ("選択の背景", static control => control.ResolveThemeBrush("SystemControlHighlightListAccentLowBrush")),
        ("ホバーの背景", static control => control.ResolveThemeBrush("SystemControlBackgroundListLowBrush")),
        ("押下の背景", static control => control.ResolveThemeBrush("SystemControlBackgroundListMediumBrush")),
        ("フォーカス", static control => control.ResolveThemeBrush("TextControlBorderBrushFocused")),
        ("エラー", static control => control.ResolveThemeBrush("SystemControlErrorTextForegroundBrush")),

        // AvaloniaUI にしか無い色
        ("三次テキスト", static control => control.ResolveThemeBrush("SystemControlForegroundBaseMediumLowBrush")),
        ("アクセント（より淡）", static control => control.ResolveThemeBrush("SystemAccentColorLight2")),
        ("アクセント（より濃）", static control => control.ResolveThemeBrush("SystemAccentColorDark2")),
        ("リンク", static control => control.ResolveThemeBrush("SystemControlHyperlinkTextBrush")),
        ("領域", static control => control.ResolveThemeBrush("SystemRegionBrush")),
        ("グレーの文字", static control => control.ResolveThemeBrush("SystemControlForegroundChromeGrayBrush")),
        ("入力中の背景", static control => control.ResolveThemeBrush("TextControlBackgroundPointerOver")),
        ("枠線（ホバー）", static control => control.ResolveThemeBrush("TextControlBorderBrushPointerOver")),
    ];

    /// <summary>テーマの色の識別子の一覧を取得する（<see cref="SelectedThemeColor"/> へ設定できる値）。</summary>
    /// <value>この UI が並べるテーマの色の識別子（並びは一覧と同じ）。</value>
    /// <remarks>
    /// <b>利用側は本値を保存すればテーマに追随できる</b>: 色そのもの（<c>#AARRGGBB</c>）を保存すると明暗の切替に追随できない。<br/>
    /// 設定画面の選択肢を作るときにも使える（実際に並ぶのは解決できた色だけである）。
    /// </remarks>
    public static IReadOnlyList<string> ThemeColorKeys { get; } = [.. s_themeColors.Select(static entry => entry.Key)];

    /// <summary>一覧の先頭へ並べるテーマの色を追加する（AvaloniaUI のテーマの色をすべて並べる）。</summary>
    /// <param name="control">色を解決する対象（テーマの資源を自分のテーマで解決する）。</param>
    /// <param name="items">追加先。</param>
    /// <remarks>
    /// <b>並び順は 3 UI で共通</b>: 意味の対応する色は同じ名前・同じ順にし、その UI にしか無い色を後ろへ足す<br/>
    /// （UI を替えても同じ名前の色を選べる）。<br/>
    /// <b>値は開くたびに解決する</b>: 明暗の切替とアクセント色の変更に追随させる（資源はテーマごとに値が変わる）。<br/>
    /// <b>解決できない色は並べない</b>: Fluent テーマに無い資源や透過の色を空欄で出さない。
    /// </remarks>
    private static void AddThemeColors(ColorSelectorControl control, List<ColorSelectorItem> items)
    {
        foreach (var (key, resolve) in s_themeColors)
        {
            AddThemeColor(items, ThemeLabelPrefix + key, resolve(control));
        }
    }

    /// <summary>テーマの色を一覧へ追加する（解決できない場合は何もしない）。</summary>
    /// <param name="items">追加先。</param>
    /// <param name="label">表示する名前。</param>
    /// <param name="brush">テーマが決めたブラシ。</param>
    private static void AddThemeColor(List<ColorSelectorItem> items, string label, IBrush? brush)
    {
        if (brush is not ISolidColorBrush { Color.A: > 0 } solid)
        {
            return;
        }

        var hex = ColorString.ToArgbHex(
            System.Drawing.Color.FromArgb(solid.Color.A, solid.Color.R, solid.Color.G, solid.Color.B));

        // 識別子は表示名から作る（「テーマ: 背景」→「背景」）。 色の識別子を 1 箇所で決める
        var key = label.StartsWith(ThemeLabelPrefix, StringComparison.Ordinal)
            ? label[ThemeLabelPrefix.Length..]
            : label;
        items.Add(new ColorSelectorItem(label, hex, ColorSelectorCategory.Theme, key));
    }

    /// <summary>テーマの色の表示名の接頭辞。</summary>
    private const string ThemeLabelPrefix = "テーマ: ";

    /// <summary>テーマの資源をブラシとして取得する。</summary>
    /// <param name="key">資源のキー（Fluent テーマの色の資源）。</param>
    /// <returns>ブラシ。 取得できない場合は null。</returns>
    /// <remarks>
    /// 資源の値は明暗で切り替わるため、実際のテーマ（<see cref="StyledElement.ActualThemeVariant"/>）で解決する<br/>
    /// （一覧を開くたびに解決するので、テーマを切り替えたあとの一覧は新しい値になる）。
    /// </remarks>
    private IBrush? ResolveThemeBrush(string key)
        => this.TryFindResource(key, out var value)
            ? value switch
            {
                IBrush brush => brush,
                Color color => new SolidColorBrush(color),
                _ => null,
            }
            : null;

    /// <summary>テーマのアクセント色を取得する。</summary>
    /// <returns>アクセント色のブラシ。 取得できない場合は null。</returns>
    /// <remarks>資源の名前はテーマごとに異なるため、名前で引かずにプラットフォームが解決した値を読む。</remarks>
    private static IBrush? ResolveAccentBrush()
        => Application.Current?.PlatformSettings?.GetColorValues().AccentColor1 is { } accent
            ? new SolidColorBrush(accent)
            : null;

    // ====================================================================
    // 検証状態
    // ====================================================================

    /// <summary>バリデーション状態を更新し、エラー時に枠線を強調する。</summary>
    /// <param name="color">検証対象の色。</param>
    /// <remarks>テスト（EsUtil.Ui.AvaloniaUI.ColorSelector.Tests）が全分岐を検証するため internal 公開する。</remarks>
    internal void UpdateValidationStatus(string color)
        => UpdateValidationStatusCore(color, ColorValidation.IsValid(color));

    /// <summary>バリデーション状態を更新する。</summary>
    /// <param name="color">検証対象の色。</param>
    /// <param name="isValid">検証済みの結果。</param>
    internal void UpdateValidationStatusCore(string color, bool isValid)
    {
        var state = ColorValidation.Determine(color, isValid);
        HasValidationError = state.HasError;
        ValidationErrorMessage = state.Message;
        UpdateBorderValidationVisual();
    }

    /// <summary>検証エラーの強調を枠線へ反映する。</summary>
    /// <remarks>
    /// <b>Avalonia の標準の方法に対応する</b>: 検証エラーは <c>:error</c> 疑似クラスで表す<br/>
    /// （<c>DataValidationErrors</c> と同じ疑似クラスであり、利用側のテーマ・スタイルが <c>Border:error</c> で装飾できる）。<br/>
    /// <b>強調の可否は <see cref="HighlightBorderOnError"/> が決める</b>: false の間は疑似クラスも付けない<br/>
    /// （既定は無効。 有効にすると同梱のスタイルが枠線を赤にする）。
    /// </remarks>
    private void UpdateBorderValidationVisual()
    {
        var hasError = HighlightBorderOnError && HasValidationError;
        ((IPseudoClasses)_combo.Classes).Set(":error", hasError);
        if (hasError)
        {
            _combo.BorderBrush = Brushes.Red;
        }
        else
        {
            _combo.ClearValue(TemplatedControl.BorderBrushProperty);
        }
    }
}