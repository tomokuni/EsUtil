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
/// <b>規則の所有者はコアである</b>: 受理する入力（色名・16 進・<c>+{n}%</c>）、色名の表、保存値（<c>#AARRGGBB</c>）と
/// 画面表示（<c>{色}+{不透明度%}</c>）、並び順、入力の妥当性は
/// <see cref="EsUtil.Platform.Core.Palette"/>（<see cref="ColorString"/> / <see cref="ColorEntry"/> /
/// <see cref="ColorValidation"/>）が単一所有する。 本クラスは WinUI の要素の組み立てと、入力と状態の同期だけを行う。<br/>
/// <b>Native AOT の制約</b>: 一覧へは <c>Items.Add</c> で 1 件ずつ渡す（<c>ItemsSource</c> へ managed コレクションを
/// 渡すと AOT で <c>E_INVALIDARG</c> になる。<c>.github/ビルド設定規約.md</c> §6.1）。<br/>
/// <b>構成</b>: 1 行に「スウォッチ」「テキスト入力」「一覧ボタン（▼）」「カラーピッカー ボタン（…）」を置き、
/// 一覧とピッカーは <see cref="Flyout"/> で開く。
/// </remarks>
public sealed partial class ColorSelectorControl : Grid
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

    /// <summary>▼ と … の間の区切りの垂直線（ボタンと一緒に表示を切り替える）。</summary>
    private readonly Border _listSeparator = CreateSeparator();

    /// <summary>カラーピッカーを開くボタン。</summary>
    private readonly Button _pickerButton = new() { Content = "…" };

    /// <summary>… の手前の区切りの垂直線（ボタンと一緒に表示を切り替える）。</summary>
    private readonly Border _pickerSeparator = CreateSeparator();

    /// <summary>一覧（ListView）。 項目は 1 件ずつ追加する（AOT の制約）。</summary>
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single };

    /// <summary>一覧の上部のタブの行。 設定と中身が変わるたびに作り直す。</summary>
    private readonly StackPanel _tabStrip = new() { Orientation = Orientation.Horizontal, Spacing = 4 };

    /// <summary>一覧ボタン（▼）を表示するかどうか。</summary>
    private bool _showListButton = true;

    /// <summary>カラーピッカー ボタン（…）を表示するかどうか。</summary>
    private bool _showPickerButton = true;

    /// <summary>テーマの色の表示名の接頭辞。</summary>
    private const string ThemeLabelPrefix = "テーマ: ";

    /// <summary>選択中のテーマの色の識別子（例: <c>背景</c>）。 テーマの色でない場合は null。</summary>
    private string? _selectedThemeColor;

    /// <summary>直近に開いた一覧のタブの識別子（次に開いたときに同じタブから始める）。</summary>
    private string? _listTabKey;

    /// <summary>一覧のタブの幅（DIP）。</summary>
    private const double CategoryTabWidth = 96;

    /// <summary>一覧の幅に足す余白（DIP。 枠・項目の左右の余白・スクロール バー）。</summary>
    private const double ListChromeWidth = 28;

    /// <summary>一覧に並べた項目（添字は <see cref="_list"/> の項目と一致する）。</summary>
    private IReadOnlyList<ColorSelectorItem> _entries = [];


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
        // ▼ は入力欄の右端にあるため、右端を揃えて左へ開く（左端を揃えると画面の右外へはみ出す）
        Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,

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

    /// <summary>入力が無効なときに枠線を赤くするかどうか。</summary>
    private bool _highlightBorderOnError;

    /// <summary>選択中の色（保存値）が変わったときに発生する。</summary>
    public event EventHandler? SelectedColorChanged;

    /// <summary>コントロールを生成する。</summary>
    public ColorSelectorControl()
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

        // テーマが変わったら、テーマの色を選んでいる間はその色を解決し直す
        ActualThemeChanged += (_, _) => ReapplyThemeColor();
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

        SetColumn(_listSeparator, 1);
        Children.Add(_listSeparator);

        StyleSegmentButton(_listButton);
        _listButton.Click += (_, _) =>
        {
            // 開くたびに作り直す（テーマの色はそのときのテーマから解決するため、明暗や
            // アクセント色の変更に追随させる）。 表示できるタブが無ければ開かない
            if (!RefreshList())
            {
                return;
            }

            // 一覧を開いたことを残す（ピッカーを開いたログと対。 他 UI と同じ書式）
            _log.LogOpenList(_selectedColor);
            _listFlyout.ShowAt(_listButton);
        };
        SetColumn(_listButton, 2);
        Children.Add(_listButton);

        SetColumn(_pickerSeparator, 3);
        Children.Add(_pickerSeparator);

        StyleSegmentButton(_pickerButton);
        _pickerButton.Click += (_, _) => ShowPicker();
        SetColumn(_pickerButton, 4);
        Children.Add(_pickerButton);

        // 外周の枠（全体を 1 つのコントロールに見せる）
        BorderBrush = new SolidColorBrush(s_borderColor);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);

        _list.SelectionChanged += OnListSelectionChanged;

        _listFlyout.Content = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                _tabStrip,
                new ScrollViewer { Content = _list, MaxHeight = 360 },
            },
        };

        BuildPickerFlyout();

        ApplyViewOptions();
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

    /// <summary>一覧の先頭に UI のテーマの色を並べるかどうかを取得または設定する。</summary>
    /// <value>並べる場合は true（既定）。</value>
    /// <remarks>
    /// <b>値は UI のテーマから取る</b>: テーマの色は UI ごとに異なるため、コアの色名の表には含めない<br/>
    /// （一覧を作るたびに解決するため、明暗の切替とアクセント色の変更に追随する）。<br/>
    /// <b>並べない場合はコアの色名だけ</b>になる（一覧は次に開くときに作り直す）。
    /// </remarks>
    public bool ShowThemeColors { get; set; } = true;

    /// <summary>一覧ボタン（▼）を表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    /// <remarks>
    /// <b>隠すと一覧を開けない</b>: 色名を打ち込むか、カラーピッカーだけを使う場面（狭い場所）で隠す<br/>
    /// （区切りの線も一緒に隠し、入力欄の見た目を崩さない）。
    /// </remarks>
    public bool ShowListButton
    {
        get => _showListButton;
        set
        {
            _showListButton = value;
            ApplyViewOptions();
        }
    }

    /// <summary>カラーピッカー ボタン（…）を表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    /// <remarks>隠すときは区切りの線も一緒に隠す（線だけが残ると入力欄の見た目が崩れる）。</remarks>
    public bool ShowPickerButton
    {
        get => _showPickerButton;
        set
        {
            _showPickerButton = value;
            ApplyViewOptions();
        }
    }

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
    /// <value>文字サイズ。 未設定（<see cref="double.NaN"/>）の場合はテーマの既定値を使う。</value>
    public double TextFontSize
    {
        get => _text.FontSize;
        set => _text.FontSize = value;
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

    /// <summary>表示の切り替え（一覧ボタン・ピッカー ボタン）を反映する。</summary>
    /// <remarks>ボタンだけでなく区切りの線も隠す（線だけが残ると入力欄の見た目が崩れる）。</remarks>
    private void ApplyViewOptions()
    {
        _listButton.Visibility = ToVisibility(_showListButton);
        _listSeparator.Visibility = ToVisibility(_showListButton);
        _pickerButton.Visibility = ToVisibility(_showPickerButton);
        _pickerSeparator.Visibility = ToVisibility(_showPickerButton);
    }

    /// <summary>表示するかどうかを <see cref="Visibility"/> へ写す。</summary>
    /// <param name="value">表示する場合は true。</param>
    /// <returns>表示する場合は <see cref="Visibility.Visible"/>、しない場合は <see cref="Visibility.Collapsed"/>。</returns>
    private static Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

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
            // テーマの色を選んでいる間は識別子を出す（色を写すと、テーマに追随していることが分からない）
            _text.Text = _selectedThemeColor is { } key
                ? ThemeLabelPrefix + key
                : ColorString.ToText(normalized, _textFormat);

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
            ApplyThemeColor(hex);
        }
        else
        {
            ApplyColor(_selectedColor, raiseChanged: false);
        }
    }

    /// <summary>解決したテーマの色を <see cref="SelectedColor"/> へ書く（識別子は消さない）。</summary>
    /// <param name="hex">解決した色（<c>#AARRGGBB</c>）。</param>
    private void ApplyThemeColor(string hex) => ApplyColor(hex, raiseChanged: true);

    /// <summary>検証エラーの強調を枠線へ反映する。</summary>
    private void UpdateBorderValidationVisual()
        => BorderBrush = new SolidColorBrush(_highlightBorderOnError && HasValidationError ? Colors.Red : s_borderColor);

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
        ("文字", static control => ResolveThemeResource("TextFillColorPrimaryBrush") ?? control._text.Foreground),
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

    /// <summary>テーマの色の識別子の一覧を取得する（<see cref="SelectedThemeColor"/> へ設定できる値）。</summary>
    /// <value>この UI が並べるテーマの色の識別子（並びは一覧と同じ）。</value>
    /// <remarks>
    /// <b>利用側は本値を保存すればテーマに追随できる</b>: 色そのもの（<c>#AARRGGBB</c>）を保存すると明暗の切替に追随できない。<br/>
    /// 設定画面の選択肢を作るときにも使える（実際に並ぶのは解決できた色だけである）。
    /// </remarks>
    public static IReadOnlyList<string> ThemeColorKeys { get; } = (string[])[.. s_themeColors.Select(static entry => entry.Key)];

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

    /// <summary>一覧の選択を現在の色へ合わせる。</summary>
    /// <param name="color">現在の色。</param>
    private void UpdateSelectedIndex(DrawingColor color)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (ColorString.TryParse(_entries[i].Value, out var candidate, out _)
                && candidate.R == color.R && candidate.G == color.G && candidate.B == color.B)
            {
                _list.SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>一覧のタブを切り替える（次に開くときも同じタブから始める）。</summary>
    /// <param name="index">選んだタブの位置（<see cref="CreateTabs()"/> の並び）。</param>
    /// <remarks>
    /// <b>片方だけが選ばれる</b>: タブは押すたびに状態が反転するため、選ばれたものだけを選んだ状態にする
    /// （作り直しの前に状態を書くと、相手のタブの通知で往復する）。<br/>
    /// <b>タブは作り直さない</b>: 押したタブ自身を作り直すと、押下の処理の途中で要素が外れる。
    /// 並びは <see cref="CreateTabs()"/> が同じ設定から作り直すため、位置は変わらない。
    /// </remarks>
    internal void ApplyTab(int index)
    {
        var tabs = CreateTabs();
        if (index < 0 || index >= tabs.Count)
        {
            return;
        }

        _listTabKey = tabs[index].Key;
        for (var i = 0; i < _tabStrip.Children.Count; i++)
        {
            if (_tabStrip.Children[i] is ToggleButton button)
            {
                button.IsChecked = i == index;
            }
        }

        PopulateList(tabs[index].Items);
    }

    /// <summary>一覧に出すタブ（題名・識別子・並べる項目）を作る。</summary>
    /// <returns>表示するタブ。 中身の無いタブは含めない。</returns>
    /// <remarks>
    /// <b>表示の設定と中身を 1 か所で決める</b>: タブの有無と一覧の項目が食い違わない（空のタブを出さない）。
    /// </remarks>
    internal List<ColorSelectorTabSpec> CreateTabs()
        => CreateTabs(ShowThemeColorTab, ShowNamedColorTab, CreateItems(), CustomTabs);

    /// <summary>表示の設定と項目から、一覧に出すタブを作る。</summary>
    /// <param name="showThemeColorTab">「テーマカラー」タブを表示するかどうか。</param>
    /// <param name="showNamedColorTab">「色名」タブを表示するかどうか。</param>
    /// <param name="items">一覧に並べる項目（テーマの色と色名）。</param>
    /// <param name="customTabs">利用者が追加するタブ。</param>
    /// <returns>表示するタブ。 中身の無いタブは含めない。</returns>
    /// <remarks>
    /// <b>コントロールを作らずに検証できる形にする</b>: タブの決め方はこの純粋関数が単一所有する
    /// （WinUI 3 のコントロールは XAML ランタイムを必要とするため、テストからは本関数を呼ぶ）。
    /// </remarks>
    internal static List<ColorSelectorTabSpec> CreateTabs(
        bool showThemeColorTab,
        bool showNamedColorTab,
        IReadOnlyList<ColorSelectorItem> items,
        IReadOnlyList<ColorSelectorCustomTab> customTabs)
    {
        var tabs = new List<ColorSelectorTabSpec>();

        if (showThemeColorTab)
        {
            var theme = items.Where(item => item.Category == ColorSelectorCategory.Theme).ToList();
            if (theme.Count > 0)
            {
                tabs.Add(new ColorSelectorTabSpec("テーマカラー", "theme", theme));
            }
        }

        if (showNamedColorTab)
        {
            var named = items.Where(item => item.Category == ColorSelectorCategory.Named).ToList();
            if (named.Count > 0)
            {
                tabs.Add(new ColorSelectorTabSpec("色名", "named", named));
            }
        }

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
                tabs.Add(new ColorSelectorTabSpec(custom.Title, "custom:" + custom.Title, entries));
            }
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

    /// <summary>一覧の幅を決める（枠の幅と内容の幅の大きいほう）。</summary>
    /// <param name="frameWidth">枠（入力欄）の幅（DIP）。</param>
    /// <param name="widest">行のうち最も広いものの幅（DIP）。</param>
    /// <returns>一覧の幅（DIP）。</returns>
    /// <remarks>
    /// <b>枠の幅は変えない</b>: 一覧を開くたびに枠が広がると、開閉のたびに周りのレイアウトが動く。<br/>
    /// 一覧（ポップアップ）だけを内容に合わせて広げる（コントロールの <see cref="FrameworkElement.MinWidth"/> は触らない）。
    /// </remarks>
    internal static double CalculateListWidth(double frameWidth, double widest)
        => Math.Max(frameWidth, widest + ListChromeWidth);

    /// <summary>一覧の幅を枠（入力欄）の幅と内容の幅に合わせる（コントロールの幅は変えない）。</summary>
    /// <param name="widest">行のうち最も広いものの幅（DIP）。</param>
    private void ApplyListWidth(double widest) => _list.Width = CalculateListWidth(ActualWidth, widest);

    /// <summary>一覧のタブを切り替えるタブを作る。</summary>
    /// <param name="text">タブの文言。</param>
    /// <returns>タブ（<see cref="ToggleButton"/>）。</returns>
    private static ToggleButton CreateCategoryTab(string text)
        => new() { Content = text, Width = CategoryTabWidth };

    /// <summary>一覧を作り直す（タブは設定と中身から作り直し、項目は 1 件ずつ追加する）。</summary>
    /// <returns>表示できるタブがある場合は true（無い場合は一覧を開かない）。</returns>
    /// <remarks>
    /// <b>タブは作り直す</b>: 表示の設定（<see cref="ShowThemeColorTab"/> / <see cref="ShowNamedColorTab"/> /
    /// <see cref="CustomTabs"/>）でタブの数が変わるため、固定のタブを持たない。<br/>
    /// <b>同じタブから開く</b>: 直近に開いたタブの識別子（<see cref="_listTabKey"/>）が残っていればそのタブ、
    /// 無ければ先頭のタブを選んだ状態で始める。
    /// </remarks>
    private bool RefreshList()
    {
        var tabs = CreateTabs();
        if (tabs.Count == 0)
        {
            // 表示できるタブが無い（開いても選べる色が無い）
            _tabStrip.Children.Clear();
            _tabStrip.Visibility = Visibility.Collapsed;
            _entries = [];
            _list.Items.Clear();
            return false;
        }

        var index = tabs.FindIndex(tab => string.Equals(tab.Key, _listTabKey, StringComparison.Ordinal));
        if (index < 0)
        {
            index = 0;
        }

        _listTabKey = tabs[index].Key;
        RebuildTabStrip(tabs, index);
        PopulateList(tabs[index].Items);
        return true;
    }

    /// <summary>タブの行を作り直す。</summary>
    /// <param name="tabs">表示するタブ。</param>
    /// <param name="selectedIndex">選んだ状態にするタブの位置。</param>
    /// <remarks>タブが 1 つで題名を隠す設定なら、タブの行そのものを出さない（切り替える相手が無い）。</remarks>
    private void RebuildTabStrip(IReadOnlyList<ColorSelectorTabSpec> tabs, int selectedIndex)
    {
        _tabStrip.Children.Clear();
        for (var i = 0; i < tabs.Count; i++)
        {
            var index = i;
            var button = CreateCategoryTab(tabs[i].Title);
            button.IsChecked = i == selectedIndex;
            button.Click += (_, _) => ApplyTab(index);
            _tabStrip.Children.Add(button);
        }

        _tabStrip.Visibility = ToVisibility(ShouldShowTabStrip(tabs.Count, ShowTabTitles));
    }

    /// <summary>選んだタブの項目を一覧へ並べる（項目は 1 件ずつ追加する）。</summary>
    /// <param name="items">並べる項目。</param>
    private void PopulateList(IReadOnlyList<ColorSelectorItem> items)
    {
        _entries = items;
        _list.Items.Clear();
        var widest = 0.0;
        foreach (var entry in items)
        {
            // 右端に余白を残す（一覧の幅が内容ぴったりになり、長い名前の末尾が切れるため）
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Margin = new Thickness(0, 0, 10, 0),
            };
            row.Children.Add(new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(2),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(
                    ColorString.TryParse(entry.Value, out var swatch, out _) ? ToWinUIColor(swatch) : Colors.Transparent),
            });
            row.Children.Add(new TextBlock { Text = entry.Label, VerticalAlignment = VerticalAlignment.Center });

            // 一覧の幅を内容に合わせるため、行の実寸を測る（追加前は測れない）
            row.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, row.DesiredSize.Width);
            _list.Items.Add(row);
        }

        ApplyListWidth(widest);

        if (ColorString.TryParse(_selectedColor, out var color, out _))
        {
            // 作り直しの途中で選択が動いても「利用者の選択」として扱わない
            // （扱うと並び順を変えただけで色の変更イベントと Setting の Information が出る）
            _applying = true;
            try
            {
                UpdateSelectedIndex(color);
            }
            finally
            {
                _applying = false;
            }
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

        // 直接の入力はテーマの色の選択を解く
        _selectedThemeColor = null;
        ApplyColor(_text.Text, raiseChanged: true);
    }

    /// <summary>テキスト入力からフォーカスが外れたとき、無効な入力を最後の有効値へ戻す。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnTextLostFocus(object sender, RoutedEventArgs e)
    {
        var valid = ColorValidation.IsValid(_text.Text);
        _log.LogLostFocus(_selectedColor, valid);
        if (ColorValidation.ShouldRecover(valid, hasLastValid: true))
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
            // テーマの色は識別子を覚える（明暗・アクセントの変更へ追随させる）。 色名は値だけを写す
            _selectedThemeColor = _entries[index].ThemeKey;
            ApplyColor(_entries[index].Value, raiseChanged: true);
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

        // ピッカーを開いたことを残す（一覧を開いたログと対。 他 UI と同じ書式）
        _log.LogOpenPicker(_selectedColor);
        _pickerFlyout.ShowAt(_pickerButton);
        _log.LogOpenList(_selectedColor);
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
