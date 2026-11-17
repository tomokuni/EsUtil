using EsUtil.Platform.Core.Palette;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.ColorSelector;

/// <summary>色を選ぶコントロール（色名の入力・一覧からの選択・カラーピッカー）。</summary>
/// <remarks>
/// <b>1 つの入力欄に見せる</b>: 入力欄と展開ボタンは標準の <see cref="ComboBox"/>（編集可）をそのまま使い、
/// 自前の枠・▼・… を作らない。 展開ボタンを押すと標準の一覧の代わりにタブ付きの <see cref="Flyout"/> を出す。<br/>
/// <b>入力欄は「■ + 色名」で見せる</b>: 標準のコンボボックスは文字しか出せないため、現在色の四角を入力欄の左へ
/// 重ね、文字はその右から始める（文字は上下の中央にそろえる）。 四角は無効な入力の間は隠す。<br/>
/// <b>規則の所有者はコアである</b>: 受理する入力（色名・16 進・<c>+{n}%</c>）、色名の表、保存値（<c>#AARRGGBB</c>）と
/// 画面表示（<c>{色}+{不透明度%}</c>）、並び順、入力の妥当性は
/// <see cref="EsUtil.Platform.Core.Palette"/>（<see cref="ColorString"/> / <see cref="ColorEntry"/> /
/// <see cref="ColorValidation"/>）が単一所有する。 本クラスは WinUI の要素の組み立てと、入力と状態の同期だけを行う。<br/>
/// <b>Native AOT の制約</b>: 一覧へは <c>Items.Add</c> で 1 件ずつ渡す（<c>ItemsSource</c> へ managed コレクションを
/// 渡すと AOT で <c>E_INVALIDARG</c> になる）。<br/>
/// <b>一覧はタブで切り替える</b>: 並びは「カスタム → テーマカラー → 色名 → ColorPicker」で、カスタム タブは
/// 色名付きの一覧と、列数を指定した格子（■ だけ）を選べる。 カラーピッカーはタブの中へ直接置く（別のフライアウトを重ねない）。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - 値: <see cref="SelectedColor"/> / <see cref="ColorSortMode"/> / <see cref="TextFormat"/> /
///   <see cref="SelectedThemeColor"/> / <see cref="ThemeColorKeys"/> / <see cref="TryResolveThemeColor"/><br/>
/// - 一覧の構成: <see cref="ShowThemeColors"/> / <see cref="ShowCustomTab"/> / <see cref="ShowThemeColorTab"/> /
///   <see cref="ShowNamedColorTab"/> / <see cref="ShowPickerTab"/> / <see cref="ShowTabTitles"/> / <see cref="CustomTabs"/><br/>
/// - 検証: <see cref="HasValidationError"/> / <see cref="ValidationErrorMessage"/> / <see cref="HighlightBorderOnError"/><br/>
/// - 外観（入力欄の文字サイズ）: <see cref="TextFontSize"/><br/>
/// - イベント: <see cref="ColorApplied"/>（同じ色を選び直したときも発生する） / <see cref="SelectedColorChanged"/><br/>
/// - ログ: <see cref="Log"/> / <see cref="LogPrefix"/>
/// </remarks>
public sealed partial class ColorSelectorControl : UserControl
{
    // ====================================================================
    // 定数（公開 → internal → private）
    // ====================================================================

    /// <summary>色の四角の一辺（DIP。 格子のセル）。</summary>
    internal const double SwatchCellSize = 32;

    /// <summary>一覧のタブの幅の上限（DIP）。 2 つを並べたときに一覧の幅と釣り合う値。</summary>
    private const double CategoryTabWidth = 112;

    /// <summary>タブの幅の下限（DIP。 数を増やしても文字が読める幅）。</summary>
    private const double MinimumTabWidth = 56;

    /// <summary>タブとタブの間の余白（DIP）。</summary>
    private const double TabSpacing = 4;

    /// <summary>一覧の中身に使える幅の上限（DIP）。</summary>
    /// <remarks>
    /// <b>広げすぎないための上限</b>である（AvaloniaUI 版と同じ 440）。 長い色名やタブの数に合わせて
    /// 一覧が画面いっぱいに広がるのを防ぐ。<br/>
    /// WinUI のフライアウトの既定の最大幅（<c>FlyoutThemeMaxWidth</c> = 456）は、この幅に余白（24）を
    /// 足すと超えて右端のタブが切れる。 <see cref="CreateListPresenterStyle"/> が
    /// <see cref="FlyoutPresenter"/> の幅の上限を外し、この値で止める（実測 2026-11-17）。
    /// </remarks>
    private const double FlyoutMaxContentWidth = 440;

    /// <summary>一覧の最大の高さ（DIP）。</summary>
    private const double ListMaxHeight = 360;

    /// <summary>一覧の幅に足す余白（DIP。 枠・項目の左右の余白・スクロール バー）。</summary>
    private const double ListChromeWidth = 28;

    /// <summary>格子に並べる色の四角の一辺（DIP。 セルの中へ余白を取って置く）。</summary>
    private const double SwatchSize = 20;

    /// <summary>ピッカーのタブの中身の幅（DIP）。</summary>
    private const double PickerWidth = 320;

    /// <summary>操作ボタン（OK / キャンセル）の幅（DIP）。</summary>
    private const double ActionButtonWidth = 70;

    /// <summary>テーマの色の表示名の接頭辞。</summary>
    private const string ThemeLabelPrefix = "テーマ: ";

    /// <summary>入力欄の左に出す現在色の四角の一辺（DIP）。</summary>
    private const double CurrentColorSwatchSize = 12;

    /// <summary>現在色の四角の左の余白（DIP）。</summary>
    private const double CurrentColorSwatchMargin = 7;

    /// <summary>現在色の四角と文字の間の余白（DIP）。</summary>
    private const double CurrentColorSwatchGap = 5;

    /// <summary>入力欄の文字を右へ寄せる幅（DIP。 四角の分だけ空ける）。</summary>
    /// <remarks>AvaloniaUI 版・MewUI 版と同じ値である（「■ + 色名」の見た目を 3 UI で揃える）。</remarks>
    internal const double TextLeftIndent = CurrentColorSwatchMargin + CurrentColorSwatchSize + CurrentColorSwatchGap;

    /// <summary>色の四角の枠線の色（半透明の灰色。 明色・暗色のどちらのテーマでも見える）。</summary>
    private static readonly Windows.UI.Color s_swatchBorderColor =
        Windows.UI.Color.FromArgb(0x40, 0x80, 0x80, 0x80);

    /// <summary>テーマの色（識別子と、そのときの色を解決する関数）。 並び順がそのまま一覧の順序になる。</summary>
    /// <remarks>
    /// <b>識別子と解決を 1 箇所に置く</b>: 一覧の表示と <see cref="SelectedThemeColor"/> の解決が同じ出所になり、<br/>
    /// 表を 2 つ持って食い違うことがない（<see cref="ThemeColorKeys"/> も本表から作る）。
    /// </remarks>
    private static readonly (string Key, Func<ColorSelectorControl, Brush?> Resolve)[] s_themeColors =
    [
        // 3 UI で共通の意味を持つ色（同じ名前・同じ順）
        ("背景", static _ => ResolveThemeResource("ApplicationPageBackgroundThemeBrush")),
        ("レイヤー", static _ => ResolveThemeResource("LayerFillColorDefaultBrush")),
        ("カード", static _ => ResolveThemeResource("CardBackgroundFillColorDefaultBrush")),
        ("コントロールの背景", static _ => ResolveThemeResource("ControlFillColorDefaultBrush")),
        ("コントロールの枠線", static _ => ResolveThemeResource("ControlStrokeColorDefaultBrush")),
        ("区切り線", static _ => ResolveThemeResource("DividerStrokeColorDefaultBrush")),
        ("文字", static control => ResolveThemeResource("TextFillColorPrimaryBrush") ?? control._combo.Foreground),
        ("副次テキスト", static _ => ResolveThemeResource("TextFillColorSecondaryBrush")),
        ("無効の文字", static _ => ResolveThemeResource("TextFillColorDisabledBrush")),
        ("無効の背景", static _ => ResolveThemeResource("ControlFillColorDisabledBrush")),
        ("アクセント", static _ => ResolveAccentBrush()),
        ("アクセント（淡）", static _ => ResolveThemeResource("SystemAccentColorLight1")),
        ("アクセント（濃）", static _ => ResolveThemeResource("SystemAccentColorDark1")),
        ("アクセント文字", static _ => ResolveThemeResource("AccentTextFillColorPrimaryBrush")),
        ("アクセント上の文字", static _ => ResolveThemeResource("TextOnAccentFillColorPrimaryBrush")),
        ("選択の背景", static _ => ResolveThemeResource("SystemControlHighlightAccentBrush")),
        ("ホバーの背景", static _ => ResolveThemeResource("SubtleFillColorSecondaryBrush")),
        ("押下の背景", static _ => ResolveThemeResource("ControlFillColorSecondaryBrush")),
        ("フォーカス", static _ => ResolveThemeResource("FocusStrokeColorOuterBrush")),
        ("エラー", static _ => ResolveThemeResource("SystemFillColorCriticalBrush")),

        // WinUI 3 にしか無い色
        ("三次テキスト", static _ => ResolveThemeResource("TextFillColorTertiaryBrush")),
        ("成功", static _ => ResolveThemeResource("SystemFillColorSuccessBrush")),
        ("注意", static _ => ResolveThemeResource("SystemFillColorCautionBrush")),
        ("成功の背景", static _ => ResolveThemeResource("SystemFillColorSuccessBackgroundBrush")),
        ("注意の背景", static _ => ResolveThemeResource("SystemFillColorCautionBackgroundBrush")),
        ("危険の背景", static _ => ResolveThemeResource("SystemFillColorCriticalBackgroundBrush")),
        ("スモーク", static _ => ResolveThemeResource("SmokeFillColorDefaultBrush")),
        ("強調の枠線", static _ => ResolveThemeResource("ControlStrongStrokeColorDefaultBrush")),
        ("サーフェスの枠線", static _ => ResolveThemeResource("SurfaceStrokeColorDefaultBrush")),
        ("アクセント（より淡）", static _ => ResolveThemeResource("SystemAccentColorLight2")),
        ("アクセント（より濃）", static _ => ResolveThemeResource("SystemAccentColorDark2")),
        ("アプリの背景（濃）", static _ => ResolveThemeResource("SolidBackgroundFillColorBaseAltBrush")),
    ];

    // ====================================================================
    // フィールド
    // ====================================================================

    /// <summary>入力欄を兼ねるコンボボックス（標準の要素をそのまま使う）。</summary>
    private readonly ComboBox _combo = new()
    {
        IsEditable = true,
        IsTextSearchEnabled = false,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    /// <summary>入力欄の左へ重ねる現在色の四角（「■ + 色名」に見せる）。</summary>
    /// <remarks>押下は通す（入力欄の操作を妨げない）。 色は <see cref="UpdateCurrentColorSwatch"/> が塗る。</remarks>
    private readonly Border _currentColorSwatch = new()
    {
        Width = CurrentColorSwatchSize,
        Height = CurrentColorSwatchSize,
        CornerRadius = new CornerRadius(2),
        BorderBrush = new SolidColorBrush(s_swatchBorderColor),
        BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(CurrentColorSwatchMargin, 0, 0, 0),
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed,
    };

    /// <summary>開いている一覧（タブ付きのフライアウト）。 閉じるまで保持する。</summary>
    private Flyout? _flyout;

    /// <summary>コンボボックスの内部の入力欄（編集中の文字を受け取る）。</summary>
    private TextBox? _editableBox;

    /// <summary>コンボボックスの標準の一覧を閉じている最中かどうか（開閉の往復を防ぐ）。</summary>
    private bool _suppressDropDown;

    /// <summary>タブ付きの一覧の表示待ちかどうか（同じ展開で二重に予約しない）。</summary>
    private bool _showPending;

    /// <summary>直近に開いた一覧のタブの識別子（次に開いたときに同じタブから始める）。</summary>
    private string? _listTabKey;

    /// <summary>入力と状態の往復でイベントが循環するのを防ぐ。</summary>
    private bool _applying;

    /// <summary>入力欄へ表示のために書いた文字（この文字の通知は利用者の入力として扱わない）。</summary>
    /// <remarks>
    /// コンボボックスの <c>Text</c> は編集欄へ遅れて伝わるため、<see cref="_applying"/> の間だけでは
    /// プログラムからの変更を弾けない（実測 2026-11-17: 起動直後に表示文字が入力として処理され、
    /// テーマの色の識別子が外れた）。 表示した文字と同じ通知は入力として扱わない。
    /// </remarks>
    private string? _displayText;

    /// <summary>内部ロガー。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞。</summary>
    private string? _logPrefix;

    /// <summary>現在の色（保存値）。</summary>
    private string _selectedColor = "#FFFFFFFF";

    /// <summary>一覧の並び順。</summary>
    private ColorSortMode _colorSortMode = ColorSortMode.ByHue;

    /// <summary>入力欄の表記。</summary>
    private ColorTextFormat _textFormat = ColorTextFormat.NameWithAlpha;

    /// <summary>入力が無効なときに枠線を赤くするかどうか。</summary>
    private bool _highlightBorderOnError;

    /// <summary>選択中のテーマの色の識別子（例: <c>背景</c>）。 テーマの色でない場合は null。</summary>
    private string? _selectedThemeColor;

    /// <summary>選択中の色（保存値）が変わったときに発生する。</summary>
    public event EventHandler? SelectedColorChanged;

    /// <summary>一覧・カラーピッカーで色を確定したときに発生する。</summary>
    /// <remarks>
    /// <b>同じ色を選び直したときも発生する</b>: 文字単位で色が混ざった範囲を一律にそろえる用途では、
    /// <see cref="SelectedColorChanged"/>（同じ値では通知されない）だけでは足りない。<br/>
    /// 入力欄へ打ち込んだ場合は発生しない（確定はフォーカスを外したとき。 一覧とカラーピッカーの確定だけが対象）。
    /// </remarks>
    public event EventHandler? ColorApplied;

    /// <summary>コントロールを生成する。</summary>
    public ColorSelectorControl()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Center;

        // 入力はコンボボックスの編集欄で受ける。 標準の一覧は自前の一覧（タブ付きのフライアウト）へ
        // 差し替えるため、開いた直後に閉じる
        _combo.Loaded += (_, _) => HookEditableText();
        _combo.DropDownOpened += OnDropDownOpened;

        // テーマが変わったら、テーマの色を選んでいる間はその色を解決し直す
        _combo.ActualThemeChanged += (_, _) => ReapplyThemeColor();

        // 標準のコンボボックスは文字しか出せないため、文字の上へ現在色の四角を重ねて「■ + 色名」に見せる
        // （文字はコード側で四角の右から始める）
        var inputArea = new Grid();
        inputArea.Children.Add(_combo);
        inputArea.Children.Add(_currentColorSwatch);
        Content = inputArea;

        // 入力欄の文字を四角の右から始める（内側の入力欄は、テンプレートができた時点でもう一度空ける）
        ApplyComboLayout();

        ApplyColor(_selectedColor, raiseChanged: false);
    }

    // ====================================================================
    // プロパティ
    // ====================================================================

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
        }
    }

    /// <summary>一覧の先頭に UI のテーマの色を並べるかどうかを取得または設定する。</summary>
    /// <value>並べる場合は true（既定）。</value>
    /// <remarks>
    /// <b>値は UI のテーマから取る</b>: テーマの色は UI ごとに異なるため、コアの色名の表には含めない<br/>
    /// （一覧を作るたびに解決するため、明暗の切替とアクセント色の変更に追随する）。<br/>
    /// <b>並べない場合はコアの色名だけ</b>になる（一覧は開くたびに作るため、次の一覧から反映される）。
    /// </remarks>
    public bool ShowThemeColors { get; set; } = true;

    /// <summary>一覧の「カスタム」タブを表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    /// <remarks>中身（<see cref="CustomTabs"/>）が空のときは表示しない（タブだけが出て一覧が空になるのを防ぐ）。</remarks>
    public bool ShowCustomTab { get; set; } = true;

    /// <summary>一覧の「テーマカラー」タブを表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    /// <remarks>テーマの色を使わないアプリでは隠せる（タブが 1 つになれば題名も隠せる）。</remarks>
    public bool ShowThemeColorTab { get; set; } = true;

    /// <summary>一覧の「色名」タブを表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    public bool ShowNamedColorTab { get; set; } = true;

    /// <summary>一覧の「ColorPicker」タブを表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    /// <remarks>タブを開くとカラーピッカー（<see cref="ColorPicker"/>）を一覧の中へ直接表示する（別のフライアウトを重ねない）。</remarks>
    public bool ShowPickerTab { get; set; } = true;

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
                ApplyColor(hex, raiseChanged: true);
            }
            else
            {
                ApplyColor(_selectedColor, raiseChanged: false);
            }
        }
    }

    /// <summary>入力欄の表記を取得または設定する。</summary>
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
    /// <value>文字サイズ。 未設定（<see cref="double.NaN"/>）の場合はテーマの既定値を使う。</value>
    public double TextFontSize
    {
        get => _combo.FontSize;
        set
        {
            if (double.IsNaN(value))
            {
                _combo.ClearValue(Control.FontSizeProperty);
            }
            else
            {
                _combo.FontSize = value;
            }
        }
    }

    /// <summary>入力が無効なときに枠線を赤くするかを取得または設定する。</summary>
    /// <value>true の場合は <see cref="HasValidationError"/> が true の間、入力欄の枠線を赤にする。 既定は false。</value>
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

    /// <summary>テーマの色の識別子の一覧を取得する（<see cref="SelectedThemeColor"/> へ設定できる値）。</summary>
    /// <value>この UI が並べるテーマの色の識別子（並びは一覧と同じ）。</value>
    /// <remarks>
    /// <b>利用側は本値を保存すればテーマに追随できる</b>: 色そのもの（<c>#AARRGGBB</c>）を保存すると明暗の切替に追随できない。<br/>
    /// 設定画面の選択肢を作るときにも使える（実際に並ぶのは解決できた色だけである）。
    /// </remarks>
    public static IReadOnlyList<string> ThemeColorKeys { get; } = (string[])[.. s_themeColors.Select(static entry => entry.Key)];

    // ====================================================================
    // 一覧（タブの構成・ポップアップ）
    // ====================================================================

    /// <summary>表示の設定と項目から、一覧に出すタブを作る。</summary>
    /// <param name="showCustomTab">「カスタム」タブを表示するかどうか。</param>
    /// <param name="showThemeColorTab">「テーマカラー」タブを表示するかどうか。</param>
    /// <param name="showNamedColorTab">「色名」タブを表示するかどうか。</param>
    /// <param name="showPickerTab">「ColorPicker」タブを表示するかどうか。</param>
    /// <param name="items">一覧に並べる項目（テーマの色と色名）。</param>
    /// <param name="customTabs">利用者が追加するタブ。</param>
    /// <returns>表示するタブ。 中身の無いタブは含めない。</returns>
    /// <remarks>
    /// <b>並びは「カスタム → テーマカラー → 色名 → ColorPicker」</b>である（利用者が用意した色を先に出す）。<br/>
    /// <b>コントロールを作らずに検証できる形にする</b>: タブの決め方はこの純粋関数が単一所有する
    /// （WinUI 3 のコントロールは XAML ランタイムを必要とするため、テストからは本関数を呼ぶ）。
    /// </remarks>
    internal static List<ColorSelectorTabSpec> CreateTabs(
        bool showCustomTab,
        bool showThemeColorTab,
        bool showNamedColorTab,
        bool showPickerTab,
        IReadOnlyList<ColorSelectorItem> items,
        IReadOnlyList<ColorSelectorCustomTab> customTabs)
    {
        var tabs = new List<ColorSelectorTabSpec>();

        if (showCustomTab)
        {
            foreach (var custom in customTabs)
            {
                if (custom is null || custom.Colors.Count == 0)
                {
                    continue;
                }

                var entries = custom.Colors
                    .Where(color => !string.IsNullOrWhiteSpace(color.Value))
                    .Select(color => new ColorSelectorItem(color.Name, color.Value, ColorSelectorCategory.Custom))
                    .ToList();
                if (entries.Count > 0)
                {
                    tabs.Add(new ColorSelectorTabSpec(custom.Title, "custom:" + custom.Title, custom.Columns, entries));
                }
            }
        }

        if (showThemeColorTab)
        {
            var theme = items.Where(item => item.Category == ColorSelectorCategory.Theme).ToList();
            if (theme.Count > 0)
            {
                tabs.Add(new ColorSelectorTabSpec("テーマカラー", "theme", 0, theme));
            }
        }

        if (showNamedColorTab)
        {
            var named = items.Where(item => item.Category == ColorSelectorCategory.Named).ToList();
            if (named.Count > 0)
            {
                tabs.Add(new ColorSelectorTabSpec("色名", "named", 0, named));
            }
        }

        if (showPickerTab)
        {
            tabs.Add(new ColorSelectorTabSpec("ColorPicker", "picker", 0, [], IsPicker: true));
        }

        return tabs;
    }

    /// <summary>一覧のタブの行を出すかどうかを決める。</summary>
    /// <param name="tabCount">表示できるタブの数。</param>
    /// <param name="showTabTitles">題名を出す設定かどうか（<see cref="ShowTabTitles"/>）。</param>
    /// <returns>タブの行を出す場合は true。</returns>
    /// <remarks>
    /// <b>タブが 1 つだけのときに効く</b>: 切り替える相手が無いため、題名を省いて一覧を狭くできる。<br/>
    /// タブが 2 つ以上あるときは切り替えに必要なので、設定に関わらず出す。
    /// </remarks>
    internal static bool ShouldShowTabStrip(int tabCount, bool showTabTitles) => tabCount > 1 || showTabTitles;

    /// <summary>一覧（フライアウト）の幅を決める（枠・タブの行・中身のうち最も広いもの。 上限あり）。</summary>
    /// <param name="frameWidth">枠（入力欄）の幅（DIP）。</param>
    /// <param name="showTabStrip">タブの行を出すかどうか。</param>
    /// <param name="tabCount">タブの数。</param>
    /// <param name="contentWidth">タブの中身のうち最も広いものの幅（DIP。 余白を含む）。</param>
    /// <returns>一覧の幅（DIP）。</returns>
    /// <remarks>
    /// <b>タブの行も幅に含める</b>: 含めないと、タブの行だけが広がって一覧（とスクロール バー）が
    /// フライアウトの途中で切れる（実測 2026-11-17）。<br/>
    /// <b>枠の幅は変えない</b>: 一覧を開くたびに枠が広がると、開閉のたびに周りのレイアウトが動く。
    /// 一覧（ポップアップ）だけを広げる（コントロールの幅・最小幅は触らない）。<br/>
    /// <b>上限で止める</b>: フライアウトの既定の最大幅を超えると右端（タブ）が切れる。
    /// 超えた分はタブの幅を詰めて収める（<see cref="FlyoutMaxContentWidth"/>）。
    /// </remarks>
    internal static double ResolveFlyoutWidth(double frameWidth, bool showTabStrip, int tabCount, double contentWidth)
    {
        var width = Math.Max(frameWidth, contentWidth);
        if (showTabStrip)
        {
            width = Math.Max(width, CalculateTabRowWidth(tabCount));
        }

        return Math.Min(width, FlyoutMaxContentWidth);
    }

    /// <summary>タブの行の幅を決める（タブを上限の幅で並べたときの幅）。</summary>
    /// <param name="tabCount">タブの数。</param>
    /// <returns>タブの行の幅（DIP）。 タブが無い場合は 0。</returns>
    internal static double CalculateTabRowWidth(int tabCount)
        => tabCount <= 0 ? 0 : (tabCount * CategoryTabWidth) + ((tabCount - 1) * TabSpacing);

    /// <summary>タブの幅を決める（数を増やすほど詰める）。</summary>
    /// <param name="width">一覧の幅（DIP）。</param>
    /// <param name="count">タブの数。</param>
    /// <returns>タブ 1 つの幅（DIP）。</returns>
    /// <remarks>
    /// <b>タブは決まった幅で並ぶ</b>ため、数が多いと右上（最後のタブ）がフライアウトの外へ出て切れる。
    /// 一覧の幅に収まるよう、タブの幅を詰める（<see cref="MinimumTabWidth"/> から
    /// <see cref="CategoryTabWidth"/> まで）。
    /// </remarks>
    internal static double ResolveTabWidth(double width, int count)
    {
        if (count <= 0)
        {
            return CategoryTabWidth;
        }

        var fitting = (width - ((count - 1) * TabSpacing)) / count;
        return Math.Clamp(fitting, MinimumTabWidth, CategoryTabWidth);
    }

    /// <summary>タブの中身のうち最も広いものの幅を測る（カラーピッカーのタブは除く）。</summary>
    /// <param name="tabs">測るタブ。</param>
    /// <returns>最も広い中身の幅（DIP。 一覧は余白を含む）。</returns>
    /// <remarks>
    /// カラーピッカーのタブは中身が自分の幅を持つため含めない（開いたときに広がる）。<br/>
    /// 格子は列数とセルの大きさで決まるため、行を作らずに求める。
    /// </remarks>
    private static double MeasureWidestTabContent(IReadOnlyList<ColorSelectorTabSpec> tabs)
    {
        var widest = 0.0;
        foreach (var tab in tabs)
        {
            if (tab.IsPicker)
            {
                continue;
            }

            widest = Math.Max(
                widest,
                tab.IsList
                    ? MeasureWidestRow(tab.Items) + ListChromeWidth
                    : CalculateSwatchGridWidth(tab.Columns));
        }

        return widest;
    }

    /// <summary>行のうち最も広いものの幅（実寸）を測る。</summary>
    /// <param name="entries">測る項目。</param>
    /// <returns>最も広い行の幅（DIP）。</returns>
    /// <remarks>一覧に並べる行と同じ要素を作って測る（見積もりだと文字の実寸とずれる）。</remarks>
    private static double MeasureWidestRow(IReadOnlyList<ColorSelectorItem> entries)
    {
        var widest = 0.0;
        foreach (var entry in entries)
        {
            widest = Math.Max(widest, CreateColorRow(entry).DesiredSize.Width);
        }

        return widest;
    }

    /// <summary>格子の幅を決める（セルの大きさ × 列数）。</summary>
    /// <param name="columns">■ を並べる列数。</param>
    /// <returns>格子の幅（DIP）。 列数が 1 未満の場合は 1 列分。</returns>
    /// <remarks>
    /// <b>列数 0 は格子にしない</b>: 0 は色名付きの一覧を表すため、格子を作るときは 1 列へ丸める
    /// （<see cref="CreateSwatchGrid"/> と同じ規則を、行を作らずに確かめられる形にする）。
    /// </remarks>
    internal static double CalculateSwatchGridWidth(int columns)
        => SwatchCellSize * Math.Max(1, columns);

    /// <summary>格子に必要な行数を決める（スクロールせずに収まるかを確かめられる形にする）。</summary>
    /// <param name="itemCount">並べる色の数。</param>
    /// <param name="columns">■ を並べる列数。</param>
    /// <returns>必要な行数。 0 件なら 0。</returns>
    /// <remarks><paramref name="columns"/> 列で折り返す（16 色なら 6 列 × 3 行、40 色なら 8 列 × 5 行）。</remarks>
    internal static int CalculateSwatchRows(int itemCount, int columns)
    {
        var actualColumns = Math.Max(1, columns);
        return itemCount <= 0 ? 0 : (itemCount + actualColumns - 1) / actualColumns;
    }

    /// <summary>入力欄へ出す文字列を決める（カスタム タブの色名を優先する）。</summary>
    /// <param name="normalized">保存値（<c>#AARRGGBB</c>）。</param>
    /// <param name="format">入力欄の表記（<see cref="ColorTextFormat"/>）。</param>
    /// <param name="customTabs">利用者が追加したタブ（色名の出所）。</param>
    /// <returns>表示する文字列。</returns>
    /// <remarks>
    /// <b>色コードだけを見比べなくても意図した色かどうかが分かる</b>: 利用者が付けた名前（例: 枠線の赤）で表示する。<br/>
    /// 不透明度があるときは色名へ <c>+{n}%</c> を足す（色名だけでは不透明度が伝わらない）。
    /// </remarks>
    internal static string FormatDisplayText(
        string normalized, ColorTextFormat format, IReadOnlyList<ColorSelectorCustomTab> customTabs)
    {
        if (format == ColorTextFormat.NameWithAlpha && ResolveCustomName(normalized, customTabs) is { } name)
        {
            return ColorString.TryParse(normalized, out _, out var opacity) && opacity < 0.999
                ? string.Concat(name, "+", ((int)Math.Round(opacity * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture), "%")
                : name;
        }

        return ColorString.ToText(normalized, format);
    }

    /// <summary>色に一致するカスタム タブの色名を探す。</summary>
    /// <param name="normalized">保存値（<c>#AARRGGBB</c>）。</param>
    /// <param name="customTabs">利用者が追加したタブ。</param>
    /// <returns>見つかった色名。 無い場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>値は色として比べる</b>: 登録が色コードでも色名でも同じ色なら一致させる（文字列の一致では
    /// <c>Red</c> と <c>#FFFF0000</c> が別物になる）。
    /// </remarks>
    internal static string? ResolveCustomName(string normalized, IReadOnlyList<ColorSelectorCustomTab> customTabs)
    {
        if (!ColorString.TryParse(normalized, out var target, out _))
        {
            return null;
        }

        foreach (var tab in customTabs)
        {
            if (tab is null)
            {
                continue;
            }

            foreach (var entry in tab.Colors)
            {
                if (string.IsNullOrWhiteSpace(entry.Value))
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

    /// <summary>一覧に出すタブ（題名・識別子・■ の列数・並べる項目）を作る。</summary>
    /// <returns>表示するタブ。 中身の無いタブは含めない。</returns>
    /// <remarks>
    /// <b>表示の設定と中身を 1 か所で決める</b>: タブの有無と一覧の項目が食い違わない（空のタブを出さない）。
    /// </remarks>
    internal List<ColorSelectorTabSpec> CreateTabs()
        => CreateTabs(ShowCustomTab, ShowThemeColorTab, ShowNamedColorTab, ShowPickerTab, CreateItems(), CustomTabs);

    /// <summary>一覧に並べる項目を作る（テーマの色を先頭へ置く）。</summary>
    /// <returns>表示名と適用値の組。</returns>
    /// <remarks>
    /// <b>テーマの色は開くたびに解決する</b>: 明暗の切替やアクセント色の変更に追随させるため、値を作り置きしない。<br/>
    /// 解決できない色（地色が無い等）は並べない。
    /// </remarks>
    internal List<ColorSelectorItem> CreateItems()
    {
        var items = new List<ColorSelectorItem>();
        if (ShowThemeColors)
        {
            AddThemeColors(items);
        }

        foreach (var entry in ColorEntry.GetSorted(_colorSortMode))
        {
            items.Add(new ColorSelectorItem(entry.Name, entry.Name, ColorSelectorCategory.Named));
        }

        return items;
    }

    /// <summary>一覧のポップアップ（タブ + 選んだタブの中身）を作る。</summary>
    /// <returns>作ったポップアップ。 表示できるタブが 1 つも無い場合は null。</returns>
    /// <remarks>
    /// <b>タブは分類だけを切り替える</b>: テーマの色が多いため、1 つの一覧へ混ぜると色名を探しにくい。<br/>
    /// 前回選んだタブから開く（同じ用途で続けて選ぶときに切り替えの手間を省く）。<br/>
    /// <b>タブが 1 つなら題名を隠せる</b>（<see cref="ShowTabTitles"/>）。 切り替える相手が無いため、一覧を狭くできる。
    /// </remarks>
    private Flyout? CreateListFlyout()
    {
        var tabs = CreateTabs();
        if (tabs.Count == 0)
        {
            return null;
        }

        var showTabStrip = ShouldShowTabStrip(tabs.Count, ShowTabTitles);

        // 一覧の幅は**開く前に決める**（枠・タブの行・中身のうち最も広いもの）。
        // 決めずに置くとタブの行だけが広がり、スクロール バーがフライアウトの途中に出る（実測 2026-11-17）
        var width = ResolveFlyoutWidth(ActualWidth, showTabStrip, tabs.Count, MeasureWidestTabContent(tabs));

        // タブは決まった幅で並ぶため、数が多いとフライアウトの既定の最大幅を超えて右端が切れる。
        // 一覧の幅に収まるよう、タブの幅を詰める
        var tabWidth = ResolveTabWidth(width, tabs.Count);
        var tabStrip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = TabSpacing };
        var body = new Grid { MinWidth = width };
        var panel = new StackPanel { Spacing = 6 };

        // 標準のコンボボックスは左端を揃えて一覧を開く（その位置に合わせると一覧が右へ伸びて収まる）
        var flyout = new Flyout
        {
            Content = panel,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,

            // ウィンドウの内側へ押し戻すと、狭いウィンドウで一覧が極端に短くなる（はみ出してよい）
            ShouldConstrainToRootBounds = false,

            // 一覧（ListView）は自分の高さでスクロールし、格子はスクロールしない
            FlyoutPresenterStyle = CreateListPresenterStyle(),
        };

        var tabButtons = new List<ToggleButton>(tabs.Count);
        var slots = new List<Grid>(tabs.Count);
        for (var i = 0; i < tabs.Count; i++)
        {
            var index = i;
            var button = CreateCategoryTab(tabs[i].Title, tabWidth);
            button.Click += (_, _) => ApplyTab(index);
            tabButtons.Add(button);
            tabStrip.Children.Add(button);

            // 中身はそのタブを開いたときに作る（開いていないカラーピッカーを生成しない）。
            // 入れ物だけを先に置き、タブごとの表示は入れ物の可視性で切り替える
            var slot = new Grid { Visibility = Visibility.Collapsed };
            slots.Add(slot);
            body.Children.Add(slot);
        }

        if (showTabStrip)
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

            // 初めて開いたタブだけ中身を作る（2 回目以降は作り直さない）
            if (slots[index].Children.Count == 0)
            {
                slots[index].Children.Add(CreateTabView(tabs[index], flyout, width));
            }

            for (var i = 0; i < tabButtons.Count; i++)
            {
                tabButtons[i].IsChecked = i == index;
                slots[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    /// <summary>タブの中身を作る（一覧・格子・カラーピッカー）。</summary>
    /// <param name="tab">タブの定義。</param>
    /// <param name="flyout">閉じる対象のポップアップ。</param>
    /// <param name="width">一覧の幅（DIP。 スクロール バーが右端へ来るようにそろえる）。</param>
    /// <returns>タブの中身。</returns>
    private FrameworkElement CreateTabView(ColorSelectorTabSpec tab, Flyout flyout, double width)
        => tab.IsPicker
            ? CreatePickerContent(flyout)
            : tab.IsList
                ? CreateColorList(tab, flyout, width)
                : CreateSwatchGrid(tab, flyout);

    /// <summary>色名付きの一覧（縦に並べてスクロールする）を作る。</summary>
    /// <param name="tab">タブの定義。</param>
    /// <param name="flyout">閉じる対象のポップアップ。</param>
    /// <param name="width">一覧の幅（DIP）。</param>
    /// <returns>一覧。</returns>
    /// <remarks>
    /// <b>幅をそろえる</b>: 一覧を内容の幅のまま置くと、スクロール バーがフライアウトの途中に出る
    /// （タブの行だけが広いとき。 実測 2026-11-17）。 一覧はフライアウトの幅いっぱいにする。
    /// </remarks>
    private FrameworkElement CreateColorList(ColorSelectorTabSpec tab, Flyout flyout, double width)
    {
        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = ListMaxHeight,
            Width = width,
        };

        foreach (var item in tab.Items)
        {
            AddColorRow(list, item);
        }

        list.SelectionChanged += (_, _) =>
        {
            var index = list.SelectedIndex;
            if (index >= 0 && index < tab.Items.Count)
            {
                ApplyItem(tab.Items[index], flyout);
            }
        };

        return list;
    }

    /// <summary>一覧へ 1 行（色の四角 + 色名）を追加する。</summary>
    /// <param name="list">追加先の一覧。</param>
    /// <param name="entry">並べる色。</param>
    private static void AddColorRow(ListView list, ColorSelectorItem entry)
        => list.Items.Add(CreateColorRow(entry));

    /// <summary>一覧の 1 行（色の四角 + 色名）を作り、実寸を測る。</summary>
    /// <param name="entry">並べる色。</param>
    /// <returns>作った行（測った実寸を <see cref="UIElement.DesiredSize"/> に持つ）。</returns>
    private static StackPanel CreateColorRow(ColorSelectorItem entry)
    {
        // 右端に余白を残す（一覧の幅が内容ぴったりになり、長い名前の末尾が切れるため）
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 10, 0),
        };
        row.Children.Add(CreateColorSquare(entry, 14, new CornerRadius(2)));
        row.Children.Add(new TextBlock { Text = entry.Label, VerticalAlignment = VerticalAlignment.Center });

        // 一覧の幅を内容に合わせるため、行の実寸を測る（追加前は測れない）
        row.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return row;
    }

    /// <summary>色の四角だけを指定した列数に並べた格子を作る（スクロールせずに選べる）。</summary>
    /// <param name="tab">タブの定義。</param>
    /// <param name="flyout">閉じる対象のポップアップ。</param>
    /// <returns>格子。</returns>
    /// <remarks>列数が 1 未満の場合は 1 列に並べる（0 は格子にしないタブのため実際には通らない）。</remarks>
    private FrameworkElement CreateSwatchGrid(ColorSelectorTabSpec tab, Flyout flyout)
    {
        var columns = Math.Max(1, tab.Columns);
        var grid = new Grid
        {
            Width = CalculateSwatchGridWidth(columns),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        for (var column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SwatchCellSize) });
        }

        var rows = CalculateSwatchRows(tab.Items.Count, columns);
        for (var row = 0; row < rows; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SwatchCellSize) });
        }

        for (var i = 0; i < tab.Items.Count; i++)
        {
            var button = CreateSwatch(tab.Items[i], flyout);
            Grid.SetRow(button, i / columns);
            Grid.SetColumn(button, i % columns);
            grid.Children.Add(button);
        }

        return grid;
    }

    /// <summary>格子の 1 マス（色の四角）を作る。</summary>
    /// <param name="item">並べる色。</param>
    /// <param name="flyout">閉じる対象のポップアップ。</param>
    /// <returns>色の四角を入れたボタン（名前はツールチップで示す）。</returns>
    private Button CreateSwatch(ColorSelectorItem item, Flyout flyout)
    {
        var button = new Button
        {
            Width = SwatchCellSize,
            Height = SwatchCellSize,
            Padding = new Thickness(2),
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = CreateColorSquare(item, SwatchSize, new CornerRadius(3)),
        };
        ToolTipService.SetToolTip(button, item.Label);
        button.Click += (_, _) => ApplyItem(item, flyout);
        return button;
    }

    /// <summary>色の四角（枠線付き）を作る。</summary>
    /// <param name="item">色の出所（<see cref="ColorSelectorItem.Value"/> を解釈する）。</param>
    /// <param name="size">一辺（DIP）。</param>
    /// <param name="cornerRadius">角の丸み。</param>
    /// <returns>色を塗った四角。</returns>
    private static Border CreateColorSquare(ColorSelectorItem item, double size, CornerRadius cornerRadius)
        => new()
        {
            Width = size,
            Height = size,
            CornerRadius = cornerRadius,
            BorderBrush = new SolidColorBrush(s_swatchBorderColor),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(
                ColorString.TryParse(item.Value, out var color, out _) ? ToWinUIColor(color) : Colors.Transparent),
        };

    /// <summary>選んだ色を反映して一覧を閉じる。</summary>
    /// <param name="item">選んだ色。</param>
    /// <param name="flyout">閉じるポップアップ。</param>
    private void ApplyItem(ColorSelectorItem item, Flyout flyout)
    {
        ApplyPlan(PlanItemSelection(item));
        flyout.Hide();
    }

    /// <summary>適用の計画どおりに色を反映し、確定の通知を出す。</summary>
    /// <param name="plan">適用の計画。</param>
    /// <remarks>テーマの色は識別子を覚える（明暗・アクセントの変更へ追随させる）。 色名・カスタムは値だけを写す。</remarks>
    private void ApplyPlan(ColorApplyPlan plan)
    {
        _selectedThemeColor = plan.ThemeKey;
        ApplyColor(plan.Value, raiseChanged: true);
        if (plan.RaiseApplied)
        {
            RaiseColorApplied();
        }
    }

    /// <summary>一覧の項目を選んだときの適用の計画を作る。</summary>
    /// <param name="item">選んだ項目。</param>
    /// <returns>適用の計画（テーマの色の識別子を写し、確定を通知する）。</returns>
    internal static ColorApplyPlan PlanItemSelection(ColorSelectorItem item)
        => new(item.Value, item.ThemeKey, RaiseApplied: true);

    /// <summary>カラーピッカーで確定したときの適用の計画を作る。</summary>
    /// <param name="value">確定した色（<c>#AARRGGBB</c>）。</param>
    /// <returns>適用の計画（テーマの色ではないため識別子は無く、確定を通知する）。</returns>
    internal static ColorApplyPlan PlanPickerSelection(string? value)
        => new(value, ThemeKey: null, RaiseApplied: true);

    /// <summary>カラーピッカー（OK / キャンセル付き）をタブの中身として作る。</summary>
    /// <param name="flyout">閉じる対象のポップアップ。</param>
    /// <returns>カラーピッカーと操作ボタン。</returns>
    /// <remarks>
    /// <b>別のフライアウトを重ねない</b>: ピッカーは一覧のタブの中へ直接置く（外側のフライアウトだけが閉じる）。<br/>
    /// タブを作り直すたびに新しい <see cref="ColorPicker"/> を作る（要素を別の親へ移せないため）。
    /// </remarks>
    private FrameworkElement CreatePickerContent(Flyout flyout)
    {
        _log.LogOpenPicker(_selectedColor);

        var picker = new ColorPicker
        {
            IsAlphaEnabled = true,
            ColorSpectrumShape = ColorSpectrumShape.Box,
            IsColorChannelTextInputVisible = true,
            IsAlphaTextInputVisible = true,
        };
        if (ColorString.TryParse(_selectedColor, out var color, out _))
        {
            picker.Color = ToWinUIColor(color);
        }

        var ok = CreateActionButton("OK");
        ok.Click += (_, _) =>
        {
            var selected = picker.Color;
            ApplyPlan(PlanPickerSelection(
                ColorString.ToArgbHex(DrawingColor.FromArgb(selected.A, selected.R, selected.G, selected.B))));
            flyout.Hide();
        };

        var cancel = CreateActionButton("キャンセル");
        cancel.Click += (_, _) => flyout.Hide();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var body = new StackPanel { Spacing = 8, Width = PickerWidth };
        body.Children.Add(picker);
        body.Children.Add(buttons);
        return body;
    }

    /// <summary>Flyout の操作ボタン（OK / キャンセル）を作る。</summary>
    /// <param name="text">ボタンの表示テキスト。</param>
    /// <returns>幅と中央揃えを揃えたボタン。</returns>
    private static Button CreateActionButton(string text) => new()
    {
        Content = text,
        Width = ActionButtonWidth,
        HorizontalContentAlignment = HorizontalAlignment.Center,
    };

    /// <summary>一覧のタブを切り替えるタブを作る。</summary>
    /// <param name="text">タブの文言。</param>
    /// <param name="width">タブの幅（DIP。 一覧の幅に収まるよう詰めた値）。</param>
    /// <returns>タブ（<see cref="ToggleButton"/>）。</returns>
    /// <remarks>
    /// 片方だけが選ばれるよう、切り替えは一覧を開く処理の <c>ApplyTab</c> が両方の状態を書く。<br/>
    /// 幅を詰めたタブでも文言が収まるよう、左右の余白を小さくして中央にそろえる。 収まらない文言は
    /// 末尾を省略する（狭いタブで文字が途中で切れるのを避ける）。
    /// </remarks>
    private static ToggleButton CreateCategoryTab(string text, double width) => new()
    {
        Content = new TextBlock
        {
            Text = text,
            FontSize = CategoryTabFontSize,
            TextTrimming = TextTrimming.CharacterEllipsis,
        },
        Width = width,
        Padding = new Thickness(4, 2, 4, 2),
        HorizontalContentAlignment = HorizontalAlignment.Center,
    };

    /// <summary>タブの文言の文字サイズ（DIP。 5 つ並べても「ColorPicker」が収まる大きさ）。</summary>
    private const double CategoryTabFontSize = 11;

    /// <summary>一覧のポップアップの表示スタイルを作る（高さの上限を外し、スクロールさせない）。</summary>
    /// <returns>FlyoutPresenter のスタイル。</returns>
    /// <remarks>
    /// <see cref="FlyoutPresenter"/> の既定スタイルは高さを制限し、内容を <c>ScrollViewer</c> に入れる。
    /// 一覧は <see cref="ListMaxHeight"/> で自分がスクロールし、格子はスクロールしないため、外側のスクロールを切る。<br/>
    /// <b>幅の上限も外す</b>: 既定の <c>FlyoutThemeMaxWidth</c>（456）は、決めた一覧の幅
    /// （<see cref="FlyoutMaxContentWidth"/> = 440）にフライアウトの余白を足すと超えてタブが切れる。
    /// 中身の幅は <see cref="ResolveFlyoutWidth"/> が決めるため、ここでは制限しない。
    /// </remarks>
    private static Style CreateListPresenterStyle()
    {
        var style = new Style(typeof(FlyoutPresenter));
        style.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, double.PositiveInfinity));
        style.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty, double.PositiveInfinity));
        style.Setters.Add(new Setter(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled));
        style.Setters.Add(new Setter(ScrollViewer.VerticalScrollModeProperty, ScrollMode.Disabled));
        return style;
    }

    // ====================================================================
    // 入力と状態の同期
    // ====================================================================

    /// <summary>コンボボックスを開いたときに、標準の一覧の代わりにタブ付きのフライアウトを出す。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// <b>標準の一覧は出さない</b>: 一覧はタブ（カスタム / テーマカラー / 色名 / ColorPicker）で切り替えるため、
    /// コンボボックスが持つ一覧は開いた同じ処理の中で閉じる（処理を抜ける前に閉じるため、標準の一覧は一瞬も見えない）。<br/>
    /// <b>一覧は次の入力処理で出す</b>: 開いた処理の中でフライアウトを出すと、直後の標準の一覧の後始末に巻き込まれて
    /// 閉じられることがある（AvaloniaUI 版で開閉が往復して一覧が出なかった。 実測 2026-11-17）。 WinUI 3 の
    /// <see cref="ComboBox"/> は展開ボタンの押下を確実に止める手段が無いため、開いた直後に閉じ、
    /// <c>DispatcherQueue</c> で 1 つ後の処理としてフライアウトを出す。
    /// </remarks>
    private void OnDropDownOpened(object? sender, object e)
    {
        if (_suppressDropDown)
        {
            return;
        }

        // 標準の一覧を閉じる（この処理を抜ける前に閉じるため、標準の一覧は描画されない）
        _suppressDropDown = true;
        try
        {
            _combo.IsDropDownOpen = false;
        }
        finally
        {
            _suppressDropDown = false;
        }

        // 標準の一覧の後始末が終わってから出す（同じ入力処理の中で出すと、閉じる処理に巻き込まれる）
        if (_showPending)
        {
            return;
        }

        _showPending = true;
        if (_combo.DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, ShowListFlyout) != true)
        {
            // 予約できない場合（スレッドに DispatcherQueue が無い等）は、閉じたあとに直接出す
            _showPending = false;
            ShowListFlyout();
        }
    }

    /// <summary>タブ付きの一覧を出す。</summary>
    /// <remarks>表示待ちの印を落としてから出す（一覧を出せなかった場合も次の展開で出せるようにする）。</remarks>
    private void ShowListFlyout()
    {
        _showPending = false;

        _flyout?.Hide();
        if (CreateListFlyout() is not { } flyout)
        {
            return;
        }

        _flyout = flyout;

        // 一覧を開いたことを残す（ピッカーを開いたログと対。 他 UI と同じ書式）
        _log.LogOpenList(_selectedColor);
        flyout.ShowAt(_combo);
    }

    /// <summary>コンボボックスの編集欄（内部の <see cref="TextBox"/>）へ入力の通知を繋ぐ。</summary>
    /// <remarks>
    /// <b>編集欄はテンプレートの中にある</b>: 展開して初めて作られるため、<c>Loaded</c> のあとに探す。<br/>
    /// <c>TextChanged</c> は 1 文字ごとの入力を受けるために必要である（<see cref="ComboBox"/> 自身は
    /// 入力中の文字の変更を知らせない）。
    /// </remarks>
    private void HookEditableText()
    {
        if (_editableBox is not null)
        {
            return;
        }

        _editableBox = FindEditableTextBox(_combo);
        if (_editableBox is null)
        {
            return;
        }

        ApplyComboLayout();
        _editableBox.TextChanged += OnTextChanged;
        _editableBox.LostFocus += OnTextLostFocus;

        // テンプレートが作られる前に設定した Text は、フォーカスが無いときの表示（コンボボックスの選択欄）へ
        // 反映されない（実測 2026-11-17: 初期値の色名が入力欄に出なかった）。 同じ値の再設定では変更通知が
        // 出ないため、一度空にしてから入れ直す
        _applying = true;
        try
        {
            var text = _combo.Text;
            if (!string.IsNullOrEmpty(text))
            {
                _combo.Text = string.Empty;
                _combo.Text = text;
            }

            UpdateInputText(_selectedColor);
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>入力欄の文字を、現在色の四角の右から始まるように寄せる。</summary>
    /// <remarks>
    /// <b>標準のコンボボックスの余白を借りる</b>: 枠と文字はコンボボックスが描くため、文字の開始位置だけを
    /// 余白で動かす（見た目を似せて作り直さない）。 WinUI の入力欄は 2 つの描き方を持つ（フォーカスが無いときは
    /// コンボボックスの選択欄、入力中は内側の入力欄）ため、両方の左を空ける。<br/>
    /// 文字を打っても余白は保たれる（文字の入る領域が狭くなるだけ）。 文字は上下の中央にそろえる
    /// （テーマの既定は上寄せで、四角と高さがずれて見える）。
    /// </remarks>
    private void ApplyComboLayout()
    {
        // フォーカスが無いときの表示は、コンボボックスの選択欄（ContentPresenter）が「コンボボックスの余白」で
        // 描く。 入力中の文字は内側の入力欄が「入力欄の余白」で描くため、両方の左を空ける
        var comboPadding = _combo.Padding;
        _combo.Padding = new Thickness(
            TextLeftIndent, comboPadding.Top, comboPadding.Right, comboPadding.Bottom);
        _combo.VerticalContentAlignment = VerticalAlignment.Center;

        if (_editableBox is null)
        {
            return;
        }

        var padding = _editableBox.Padding;
        _editableBox.Padding = new Thickness(
            TextLeftIndent, padding.Top, padding.Right, padding.Bottom);
        _editableBox.VerticalContentAlignment = VerticalAlignment.Center;
    }

    /// <summary>入力欄の左の四角を現在色で塗る（「■ + 色名」に見せる）。</summary>
    /// <param name="color">表示する色（保存値）。</param>
    /// <remarks>無効な入力の間は四角を隠す（前の色を出すと、その色が選ばれていると誤解される）。</remarks>
    private void UpdateCurrentColorSwatch(string? color)
    {
        if (!TryResolveCurrentColorSwatch(color, out var swatchColor))
        {
            _currentColorSwatch.Visibility = Visibility.Collapsed;
            return;
        }

        _currentColorSwatch.Background = new SolidColorBrush(ToWinUIColor(swatchColor));
        _currentColorSwatch.Visibility = Visibility.Visible;
    }

    /// <summary>入力欄の左の四角を表示するかを決め、塗る色を返す。</summary>
    /// <param name="color">表示する色（保存値）。</param>
    /// <param name="swatchColor">四角に塗る色（表示する場合のみ有効）。</param>
    /// <returns>四角を表示する場合は true。</returns>
    /// <remarks>
    /// <b>表示の可否を 1 箇所で決める</b>: 無効な入力（解析できない文字列）の間は隠す。<br/>
    /// XAML ランタイムを持たないテストからも検証できるよう、UI を持たない純粋関数にしている。
    /// </remarks>
    internal static bool TryResolveCurrentColorSwatch(string? color, out DrawingColor swatchColor)
        => ColorString.TryParse(color, out swatchColor, out _);

    /// <summary>編集欄の <see cref="TextBox"/> を視覚ツリーから探す。</summary>
    /// <param name="root">探す起点。</param>
    /// <returns>見つかった入力欄。 まだ作られていない場合は null。</returns>
    private static TextBox? FindEditableTextBox(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox box)
            {
                return box;
            }

            if (FindEditableTextBox(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>編集欄の文字が変わったときに色を反映する。</summary>
    /// <param name="sender">送信元（編集欄）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_applying)
        {
            return;
        }

        // 表示のために書いた文字は利用者の入力ではない（テーマの色の識別子を外さない）
        var text = ((TextBox)sender).Text;
        if (string.Equals(text, _displayText, StringComparison.Ordinal))
        {
            return;
        }

        _displayText = null;

        // 直接の入力はテーマの色の選択を解く
        _selectedThemeColor = null;
        _applying = true;
        try
        {
            SelectedColor = text;
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>編集欄からフォーカスが外れたとき、無効な入力を最後の有効値へ戻す。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnTextLostFocus(object sender, RoutedEventArgs e)
    {
        var valid = ColorValidation.IsValid(_selectedColor);
        _log.LogLostFocus(_selectedColor, valid);
        if (ColorValidation.ShouldRecover(valid, hasLastValid: true))
        {
            ApplyColor(_selectedColor, raiseChanged: false);
        }
    }

    /// <summary>色を設定し、入力欄へ反映する。</summary>
    /// <param name="value">設定する色。</param>
    /// <param name="raiseChanged">変更イベントを発火する場合は true。</param>
    /// <remarks>
    /// <b>入力中の文字を打ち消さない</b>: 入力欄からの反映（<see cref="_applying"/> が true）では表示を書き戻さない。
    /// </remarks>
    private void ApplyColor(string? value, bool raiseChanged)
    {
        var normalized = ColorString.ToArgbHex(value);
        var state = ColorValidation.Determine(value, ColorValidation.IsValid(value));
        var updateText = !_applying;

        _applying = true;
        try
        {
            if (updateText)
            {
                UpdateInputText(normalized);
            }

            // 入力欄のツールチップは 2 行（色名+不透明度 / カラーコード）を出す
            var tooltip = string.Concat(
                ColorString.ToDisplayText(normalized), Environment.NewLine, ColorString.ToArgbHex(normalized));
            ToolTipService.SetToolTip(_combo, tooltip);

            HasValidationError = state.HasError;
            ValidationErrorMessage = state.Message;
            UpdateBorderValidationVisual();

            // 入力欄の左の四角は入力のたびに追随させる（文字を打っている間も現在の色が分かる）
            UpdateCurrentColorSwatch(normalized);
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
            RaiseSelectedColorChanged();
        }
    }

    /// <summary><see cref="SelectedColorChanged"/> を発生させる。</summary>
    private void RaiseSelectedColorChanged() => SelectedColorChanged?.Invoke(this, EventArgs.Empty);

    /// <summary><see cref="ColorApplied"/> を発生させる。</summary>
    /// <remarks>一覧・カラーピッカーで色を確定するたびに呼ぶ（同じ色を選び直したときも含む）。</remarks>
    private void RaiseColorApplied() => ColorApplied?.Invoke(this, EventArgs.Empty);

    /// <summary>入力欄へ色の表記を反映する。</summary>
    /// <param name="normalized">保存値（<c>#AARRGGBB</c>）。</param>
    /// <remarks>
    /// <b>カスタム タブの色名を優先する</b>: 利用者が付けた名前（例: 枠線の赤）で表示されるため、
    /// 色コードだけを見比べなくても意図した色かどうかが分かる。<br/>
    /// テーマの色を選んでいる間は識別子を出す（色を写すと、テーマに追随していることが分からない）。
    /// </remarks>
    private void UpdateInputText(string normalized)
    {
        var text = _selectedThemeColor is { } key
            ? ThemeLabelPrefix + key
            : FormatDisplayText(normalized, _textFormat, CustomTabs);

        // この文字は表示のためだけのものとして覚える（編集欄からの遅れた通知を入力として扱わない）
        _displayText = text;
        _combo.Text = text;

        // 編集欄が作られている間は、その文字も合わせる（コンボボックスの Text の反映を待たない）
        if (_editableBox is not null && !string.Equals(_editableBox.Text, text, StringComparison.Ordinal))
        {
            _editableBox.Text = text;
        }
    }

    /// <summary>テーマの色の識別子から、そのときのテーマの色を解決する。</summary>
    /// <param name="key">識別子（例: <c>背景</c>）。</param>
    /// <param name="hex">解決した色（<c>#AARRGGBB</c>）。</param>
    /// <returns>解決できた場合は true（その UI に無い色は false）。</returns>
    /// <remarks>一覧を作る処理と同じ解決を使う（別の表を持つと一覧の表示と解決した色が食い違う）。</remarks>
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

    /// <summary>テーマの色を選んでいる間、そのときのテーマの色へ解決し直す。</summary>
    private void ReapplyThemeColor()
    {
        if (_selectedThemeColor is { } key && TryResolveThemeColor(key, out var hex))
        {
            ApplyColor(hex, raiseChanged: true);
        }
        else
        {
            ApplyColor(_selectedColor, raiseChanged: false);
        }
    }

    /// <summary>検証エラーの強調を入力欄の枠線へ反映する。</summary>
    /// <remarks>強調しない間はテーマの枠線へ戻す（既定の見た目を保つ）。</remarks>
    private void UpdateBorderValidationVisual()
    {
        if (_highlightBorderOnError && HasValidationError)
        {
            _combo.BorderBrush = new SolidColorBrush(Colors.Red);
        }
        else
        {
            _combo.ClearValue(Control.BorderBrushProperty);
        }
    }

    // ====================================================================
    // テーマの色
    // ====================================================================

    /// <summary>一覧の先頭へ並べるテーマの色を追加する（WinUI 3 のテーマの色をすべて並べる）。</summary>
    /// <param name="items">追加先。</param>
    /// <remarks>
    /// <b>並び順は 3 UI で共通</b>: 意味の対応する色は同じ名前・同じ順にし、その UI にしか無い色を後ろへ足す<br/>
    /// （UI を替えても同じ名前の色を選べる）。<br/>
    /// <b>値は開くたびに解決する</b>: 明暗の切替とアクセント色の変更に追随させる。<br/>
    /// <b>解決できない色は並べない</b>（テーマに無い資源や透過の色を空欄で出さない）。
    /// </remarks>
    private void AddThemeColors(List<ColorSelectorItem> items)
    {
        foreach (var (key, resolve) in s_themeColors)
        {
            AddThemeColor(items, ThemeLabelPrefix + key, resolve(this));
        }
    }

    /// <summary>テーマの色を一覧へ追加する（解決できない場合は何もしない）。</summary>
    /// <param name="items">追加先。</param>
    /// <param name="label">表示する名前。</param>
    /// <param name="brush">テーマが決めたブラシ。</param>
    private static void AddThemeColor(List<ColorSelectorItem> items, string label, Brush? brush)
    {
        if (brush is not SolidColorBrush { Color.A: > 0 } solid)
        {
            return;
        }

        // 識別子は表示名から作る（「テーマ: 背景」→「背景」）。 色の識別子を 1 箇所で決める
        var key = label.StartsWith(ThemeLabelPrefix, StringComparison.Ordinal)
            ? label[ThemeLabelPrefix.Length..]
            : label;
        items.Add(new ColorSelectorItem(
            label,
            ColorString.ToArgbHex(DrawingColor.FromArgb(solid.Color.A, solid.Color.R, solid.Color.G, solid.Color.B)),
            ColorSelectorCategory.Theme,
            key));
    }

    /// <summary>テーマの資源をブラシとして取得する。</summary>
    /// <param name="key">資源のキー。</param>
    /// <returns>ブラシ。 取得できない場合は null。</returns>
    private static Brush? ResolveThemeResource(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) != true)
        {
            return null;
        }

        return value switch
        {
            Brush brush => brush,
            Windows.UI.Color color => new SolidColorBrush(color),
            _ => null,
        };
    }

    /// <summary>テーマのアクセント色を取得する。</summary>
    /// <returns>アクセント色のブラシ。 取得できない場合は null。</returns>
    private static Brush? ResolveAccentBrush()
        => Application.Current?.Resources["SystemAccentColor"] is Windows.UI.Color accent
            ? new SolidColorBrush(accent)
            : null;

    /// <summary>コアの色を WinUI の色へ写す。</summary>
    /// <param name="color">コアの色。</param>
    /// <returns>同じ ARGB を持つ色。</returns>
    private static Windows.UI.Color ToWinUIColor(DrawingColor color)
        => Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
}
