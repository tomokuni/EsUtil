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
/// <b>入力欄は標準の ComboBox を使う</b>: 自前の枠・▼・… のボタン・区切りの線を作らず、コンボボックスの
/// 枠と展開ボタンをそのまま使う。 入力欄の左には**現在色の四角**を重ねて「■ + 色名」に見せる
/// （標準の入力欄は文字しか出せないため。 文字は四角の右から始まるよう左の余白を空ける）。 **展開ボタン（右端 30 DIP）の押下を自前で受けて標準の一覧を開かせず**、
/// タブ付きのポップアップを出す（開いてから閉じる方式は開閉が往復して一覧が出ない。 AvaloniaUI 版の実測
/// 2026-11-17 と同じ）。 表示は押下の処理の後（<c>Dispatcher.BeginInvoke</c>）へ回す。 キーボードの
/// Alt+↓ / F4 でも同じ一覧を出す。<br/>
/// <b>編集できる入力欄は MewUI の ComboBox に無い</b>: MewUI 0.22.1 の <c>ComboBox</c> は読み取り専用の
/// 選択表示だけで、AvaloniaUI 版の <c>IsEditable</c> に当たる API を持たない。 そのため
/// <b>ComboBox の選択表示の上へ、枠も背景も持たない <see cref="TextBox"/> を重ねて文字の入力と表示を担わせる</b>
/// （最小限の代替。 コンボボックス側は項目を持たず、枠と展開ボタンだけを描く）。<br/>
/// <b>一覧はタブ付きのポップアップ</b>（自前の <see cref="Popup"/>）、<b>カラーピッカーは既存の
/// <see cref="ColorPicker"/></b> を使う。 MewUI の <see cref="ColorPicker"/> はそれ自身がドロップダウンで
/// 中身を埋め込めないため、「ColorPicker」タブを選ぶとタブ付きのポップアップを閉じてから
/// ピッカーのドロップダウンを開く（ドロップダウンを重ねない）。<br/>
/// <b>ツールチップ</b>: 入力欄には「色名+不透明度」と「<c>#AARRGGBB</c>」の 2 行を出す。<br/>
/// <b>一覧のタブ</b>: 表示の設定（<see cref="ShowCustomTab"/> / <see cref="ShowThemeColorTab"/> /
/// <see cref="ShowNamedColorTab"/> / <see cref="ShowPickerTab"/>）と <see cref="CustomTabs"/> からタブを作り、
/// 開くたびに作り直す（空のタブは作らない）。 **タブの中身は選んだものだけを作る**（開いていない
/// カラーピッカーのタブを作らない）。 タブが 1 つだけのときは <see cref="ShowTabTitles"/> で
/// 題名の行を隠せる。 一覧を開いてもコントロールの幅は変えない（一覧だけを、枠・タブの行・項目の
/// うち最も広い幅へ広げる）。 一覧はポップアップの幅いっぱいに置き、**スクロール バーを右端へ**そろえる。
/// </remarks>
public sealed class ColorSelectorControl : Grid
{
    /// <summary>標準のコンボボックスの展開ボタンの幅（DIP）。 右端のこの範囲の押下を自前で扱う。</summary>
    private const double ToggleAreaWidth = 30.0;

    /// <summary>MewUI の ComboBox の展開ボタンの幅（DIP。 入力欄が矢印へ重ならないように空ける）。</summary>
    private const double ArrowAreaWidth = 22.0;

    /// <summary>入力欄の左に出す現在色の四角の一辺（DIP）。</summary>
    private const double CurrentColorSwatchSize = 12.0;

    /// <summary>現在色の四角と文字の間の余白（DIP）。</summary>
    private const double CurrentColorSwatchGap = 5.0;

    /// <summary>現在色の四角の左の余白（DIP。 コンボボックスの枠の内側に置く）。</summary>
    private const double CurrentColorSwatchMargin = 7.0;

    /// <summary>入力欄の文字を右へ寄せる幅（DIP。 四角の分だけ空ける）。</summary>
    private const double TextLeftIndent = CurrentColorSwatchMargin + CurrentColorSwatchSize + CurrentColorSwatchGap;

    /// <summary>テキスト入力の最小幅（DIP）。</summary>
    private const double TextBoxMinWidth = 120.0;

    /// <summary>一覧の分類を切り替えるタブの幅（DIP）。 2 つを並べたときに一覧の幅と釣り合う値。</summary>
    private const double CategoryTabWidth = 96.0;

    /// <summary>タブ 1 つの最小幅（DIP。 数を増やしても文言が読める下限）。</summary>
    private const double MinCategoryTabWidth = 56.0;

    /// <summary>タブとタブの間の余白（DIP）。</summary>
    private const double TabSpacing = 4.0;

    /// <summary>ポップアップの影が左右に取る幅（DIP）。</summary>
    /// <remarks>
    /// MewUI のポップアップは影のための余白（左右 8 DIP ずつ）を足して測る。 中身の幅をこの分だけ
    /// 狭くしないと、ポップアップの全体がウィンドウのクライアント領域からはみ出す。
    /// </remarks>
    private const double PopupChromeWidth = 16.0;

    /// <summary>色の四角の一辺（DIP。 格子のセル）。</summary>
    private const double SwatchCellSize = 32.0;

    /// <summary>格子に並べる列数（16 色なら 3 行に収まり、スクロールせずに選べる）。</summary>
    private const int SwatchGridColumns = 6;

    /// <summary>格子の中の色の四角の一辺（DIP）。</summary>
    private const double SwatchSize = 20.0;

    /// <summary>一覧の最大の高さ（DIP）。 格子のタブには効かない（格子はスクロールしない）。</summary>
    private const double ListMaxHeight = 360.0;

    /// <summary>テーマの色の表示名の接頭辞。</summary>
    private const string ThemeLabelPrefix = "テーマ: ";

    /// <summary>一覧の幅に足す余白（DIP。 枠と、行の文字が端に接しないための余裕）。</summary>
    /// <remarks>行の実寸と項目の余白は実測値であり、端数だけで足りない分を本値が補う（実測 2026-11-02）。</remarks>
    private const double ListWidthAllowance = 8.0;

    /// <summary>テーマの文字サイズを解決できない場合に使う文字サイズ（DIP）。</summary>
    private const double FallbackFontSize = 12.0;

    /// <summary>直近に開いた一覧のタブの識別子（次に開いたときに同じタブから始める）。</summary>
    private string? _listTabKey;

    /// <summary>タブの状態を自分で書いている間かどうか（自分で書いた通知で往復しないようにする）。</summary>
    private bool _switchingTabs;

    /// <summary>一覧の選択をプログラムから書いている間かどうか（利用者の選択として扱わない）。</summary>
    private bool _selecting;

    /// <summary>一覧の上部のタブの入れ物（開くたびに中身を作り直す）。</summary>
    private readonly StackPanel _tabStrip;

    /// <summary>一覧の上部のタブのボタン（<see cref="_tabStrip"/> の中身と同じ並び）。</summary>
    private readonly List<ToggleButton> _tabButtons = [];

    /// <summary>表示中のタブの中身の入れ物（タブごとの要素を重ね、1 つだけ表示する）。</summary>
    private readonly Grid _body;

    /// <summary>表示中のタブの中身の入れ物（タブごとに重ね、1 つだけ表示する）。</summary>
    /// <remarks>
    /// <b>中身は開いたときに作る</b>: 全タブの中身を先に作ると、カラーピッカーのタブを開いていないのに
    /// ピッカーのログ（色の選択を開きました）まで出す。 表示するタブの入れ物だけを作り、中身は
    /// <see cref="ApplyTab"/> が最初に表示するときに 1 回だけ作る。
    /// </remarks>
    private readonly List<Grid> _tabSlots = [];

    /// <summary>表示中のタブ（<see cref="RefreshList"/> が作り直す）。</summary>
    private readonly List<ColorSelectorTabSpec> _tabs = [];

    /// <summary>直近に決めた一覧の幅（DIP。 <see cref="RefreshList"/> が決め、タブの中身が使う）。</summary>
    /// <remarks>
    /// <b>幅は中身を作る前に決める</b>: 後から広げると、一覧（とスクロール バー）がポップアップの
    /// 途中に残ったままになる（タブの行だけが広いとき。 実測 2026-11-17）。
    /// </remarks>
    private double _listWidth;

    /// <summary>一覧の 1 行（色の四角 + 名前）を作るテンプレート。</summary>
    /// <remarks>
    /// 名前だけを並べると選ぶ色が分からないため、他 UI と同じく色の四角を添える<br/>
    /// （値は保存値（<c>#AARRGGBB</c> または色名）であり、四角の色はその値から解決する）。<br/>
    /// <b>文字サイズを明示する</b>: 画面へ付いていない行を測ると既定の文字サイズで測ってしまい、<br/>
    /// 実際に並ぶ行より広い幅を要求する（実測 2026-11-02: 96 DIP の行が 137.6 DIP になった）。<br/>
    /// 実測と表示を同じ大きさにするため、テーマの文字サイズを渡す（他 UI の一覧もテーマの文字サイズで並ぶ）。
    /// </remarks>
    private readonly IDataTemplate _itemTemplate;

    /// <summary>検証エラーの枠線の色（他の UI と同じ赤）。</summary>
    private static readonly MewColor s_errorColor = MewColor.FromRgb(0xFF, 0x00, 0x00);

    /// <summary>入力欄と展開ボタンを持つ標準のコンボボックス（枠と矢印を担う）。</summary>
    private readonly ComboBox _combo;

    /// <summary>色文字列の入力と表示を担うテキスト入力（コンボボックスの文字の位置へ重ねる）。</summary>
    private readonly TextBox _text;

    /// <summary>入力欄の左に出す現在色の四角（「■ + 色名」に見せる）。</summary>
    private readonly Border _swatch;

    /// <summary>カラーピッカー（自身のドロップダウンを開くためのアンカーを兼ねる）。</summary>
    private readonly ColorPicker _picker;

    /// <summary>タブ付きの一覧のポップアップ。</summary>
    private readonly Popup _popup;

    /// <summary>ポップアップの中身（タブの行 + 選択中のタブの中身）。</summary>
    private readonly StackPanel _listPanel;

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

    /// <summary>入力が無効なときに枠線を赤くするかどうか。</summary>
    private bool _highlightBorderOnError;

    /// <summary>選択中のテーマの色の識別子（例: <c>背景</c>）。 テーマの色でない場合は null。</summary>
    private string? _selectedThemeColor;

    /// <summary>選択中の色（保存値の文字列）が変わったときに発生する。</summary>
    public event EventHandler? SelectedColorChanged;

    /// <summary>一覧・カラーピッカーで色を確定したときに発生する。</summary>
    /// <remarks>
    /// <b>同じ色を選び直したときも発生する</b>: 文字単位で色が混ざった範囲を一律にそろえる用途では、
    /// <see cref="SelectedColorChanged"/> の変更（同じ値では通知されない）だけでは足りない。<br/>
    /// 入力欄へ打ち込んだ場合は発生しない（確定はフォーカスを外したとき）。
    /// </remarks>
    public event EventHandler? ColorApplied;

    /// <summary>コントロールを生成する。</summary>
    /// <remarks>
    /// <b>入力欄は標準のコンボボックス + 文字入力</b>: コンボボックスが枠と展開ボタンを描き、
    /// その文字の位置へ枠も背景も持たないテキスト入力を重ねる（MewUI の ComboBox に編集可が無いため）。
    /// 展開ボタンを押すと、標準の一覧の代わりにタブ付きのポップアップを出す。
    /// </remarks>
    public ColorSelectorControl()
    {
        _text = new TextBox
        {
            Text = "White",
            MinWidth = TextBoxMinWidth,
            BorderThickness = 0,
            CornerRadius = 0,
            Background = MewColor.Transparent,
            Padding = new Thickness(TextLeftIndent, 0, 0, 0),
            // MewUI の TextBox は文字を自分の領域の下寄りに描くため、四角の中心と文字の中心が
            // そろう分だけ上へ寄せる（実測 2026-11-17: 上へ 1 DIP。 負の上余白は半分だけ効く）
            Margin = new Thickness(0, -2, ArrowAreaWidth, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _text.TextChanged += OnTextChanged;
        _text.LostFocus += OnTextLostFocus;

        // キーボードの展開操作（Alt+↓ / F4）でも同じ一覧を出す（標準の一覧は開かない）
        _text.KeyDown += OnTextKeyDown;

        // 枠と展開ボタンは標準のコンボボックスが担う（自前の枠・▼・… を作らない）。
        // 標準の一覧は使わないため項目は持たせず、**展開ボタン（右端）の押下を自前で受けて標準の開閉を止める**
        // （開いてから閉じる方式は開閉が往復して一覧が出ない。 AvaloniaUI 版の実測 2026-11-17 と同じ）。
        // キーボードの操作は文字入力が受けるため、コンボボックス自身はフォーカスを取らない
        // （取ると Space / ↓ で標準の空の一覧が開く）。
        _combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ChangeOnWheel = false,
        };
        _combo.Focusable(false);
        _combo.MouseDown += OnComboMouseDown;

        // 入力欄は 1 つの行に見せる（枠はコンボボックス、文字はその内側へ重ねたテキスト入力）。
        // 現在色の四角は**最後に重ねる**（コンボボックスとテキスト入力より手前に描く）。
        // 文字は四角の分だけ右から始める（テキスト入力の左の余白で空ける）
        _swatch = new Border
        {
            Width = CurrentColorSwatchSize,
            Height = CurrentColorSwatchSize,
            CornerRadius = 2,
            BorderThickness = 1,
            BorderBrush = MewColor.FromArgb(0x66, 0x80, 0x80, 0x80),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(CurrentColorSwatchMargin, 0, 0, 0),
            IsHitTestVisible = false,
        };
        var row = new Grid();
        row.Add(_combo);
        row.Add(_text);
        row.Add(_swatch);
        Add(row);

        // カラーピッカーは**自身のドロップダウン**を使う（内容を自前のポップアップへ入れると
        // 折り畳んだ見出ししか表示されない。 ColorPicker はドロップダウン コントロールである）。
        // アンカーは 1×1 にして右下へ寄せ、折り畳んだ見出しを描かないよう切り取る
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
        Add(pickerHost);

        _itemTemplate = new DelegateTemplate<ColorSelectorItem>(
            _ => CreateItemRow(ListFontSize),
            static (view, item, _, _) => ApplyItemToRow(view, item),
            static (_, _, _, _) => { });

        // 上部のタブは表示の設定と中身から作り直す（RefreshList が中身を入れ替える）。
        // 分類ごとに一覧を差し替える（テーマの色が多いため、1 つの一覧へ混ぜると色名を探しにくい）
        _tabStrip = new StackPanel().Horizontal().Spacing(TabSpacing);
        _body = new Grid();

        // 一覧の幅は**行の内容に合わせる**（既定のまま置くと、ポップアップが使える領域の幅いっぱいへ広がる）。
        // 左寄せにすると希望サイズが内容の実寸になり、文字サイズや行の文言が変われば幅も追随する
        _listPanel = new StackPanel().Vertical().Spacing(6).Children(_tabStrip, _body);

        _popup = new Popup();
        _popup.Content(_listPanel);

        RefreshList();
        ApplyColor(_selectedColor, raiseChanged: false);
    }

    /// <summary>一覧に並べる行の文字サイズ（DIP）を取得する。</summary>
    /// <value>テーマの文字サイズ。 テーマを解決できない場合（<c>Application.Run</c> 前）は <see cref="FallbackFontSize"/>。</value>
    /// <remarks>
    /// <b>行の実測と表示で同じ値を読む</b>: 行を画面へ付ける前に測ると、既定の文字サイズで測って実際の行と<br/>
    /// 大きさが食い違う（実測 2026-11-02）。 測る側（<see cref="MeasureListContentWidth"/>）と作る側（<see cref="CreateItemRow"/>）が<br/>
    /// 同じ値を読むことで、必要な幅と実際の行が一致する。
    /// </remarks>
    private static double ListFontSize
        => Application.IsRunning ? Application.Current.Theme.Metrics.FontSize : FallbackFontSize;

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

    /// <summary>一覧の「カスタム」タブを表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    /// <remarks>中身（<see cref="CustomTabs"/>）が空のときは表示しない（タブだけが出て一覧が空になるのを防ぐ）。</remarks>
    public bool ShowCustomTab { get; set; } = true;

    /// <summary>一覧の「ColorPicker」タブを表示するかどうかを取得または設定する。</summary>
    /// <value>表示する場合は true（既定）。</value>
    /// <remarks>
    /// <b>MewUI のカラーピッカーはインライン表示できない</b>: <see cref="ColorPicker"/> 自身がドロップダウンであり、
    /// 中身をポップアップへ埋め込む API を持たない。 そのためタブを選ぶとタブ付きの一覧を閉じてから<br/>
    /// ピッカーのドロップダウンを開く（2 つのドロップダウンを重ねない）。
    /// </remarks>
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
    /// 一覧は開くたびに作るため、設定を差し替えたあとの一覧から反映される。<br/>
    /// 並べ方は <see cref="ColorSelectorCustomTab.Layout"/> で選ぶ（一覧 / 色の四角を 6 列に並べる格子）。
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
    /// <value>文字サイズ。 <see cref="double.NaN"/>（既定）の場合はテーマの既定（<c>ThemeMetrics.FontSize</c>）を使う。</value>
    /// <remarks>
    /// <b>MewUI の文字サイズは継承されない</b>: 他の UI（Avalonia / WinUI 3）は親の <c>FontSize</c> を継承するため
    /// 周囲の入力欄と同じ大きさになるが、MewUI の要素は自分の値だけを使う。 設定画面のように周囲の入力欄より
    /// 小さい文字で表示されるのを避けるため、ホストが同じ値を設定できるようにする（実測 2026-10-25）。
    /// </remarks>
    public double TextFontSize
    {
        get => _text.FontSize;
        set
        {
            _text.FontSize(value);
            _combo.FontSize(value);
        }
    }

    /// <summary>入力が無効なときに枠線を赤くするかを取得または設定する。</summary>
    /// <value>true の場合は <see cref="HasValidationError"/> が true の間、入力欄（コンボボックス）の枠線を赤にする。 既定は false。</value>
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

    /// <summary>入力欄を兼ねるコンボボックスを取得する（テストからの検証用）。</summary>
    /// <value>枠と展開ボタンを描く標準のコンボボックス。</value>
    internal ComboBox Combo => _combo;

    /// <summary>文字の入力と表示を担うテキスト入力を取得する（テストからの検証用）。</summary>
    /// <value>コンボボックスの文字の位置へ重ねたテキスト入力。</value>
    internal TextBox TextEditor => _text;

    /// <summary>入力欄の左に出す現在色の四角を取得する（テストからの検証用）。</summary>
    /// <value>「■ + 色名」に見せるための四角。</value>
    internal Border CurrentColorSwatch => _swatch;

    /// <summary>一覧のタブの行を取得する（テストからの検証用）。</summary>
    /// <value>タブ（<see cref="ToggleButton"/>）を並べた行。</value>
    internal StackPanel TabStrip => _tabStrip;

    /// <summary>表示中のタブの中身を入れた入れ物を取得する（テストからの検証用）。</summary>
    /// <value>タブごとの要素を重ね、1 つだけ表示する入れ物。</value>
    internal Grid TabBody => _body;

    /// <summary>現在の一覧のタブを取得する（テストからの検証用）。</summary>
    /// <value><see cref="RefreshList"/> が作ったタブ（<see cref="CreateTabs"/> と同じ並び）。</value>
    internal IReadOnlyList<ColorSelectorTabSpec> Tabs => _tabs;

    /// <summary>一覧のポップアップが開いているかを取得する（テストからの検証用）。</summary>
    /// <value>開いている場合 true。</value>
    internal bool IsListOpen => _popup.IsOpen;

    /// <summary>一覧のタブの行が見えているかを取得する（テストからの検証用）。</summary>
    /// <value>タブが 2 つ以上、または <see cref="ShowTabTitles"/> が true の場合 true。</value>
    internal bool IsTabStripVisible => _tabStrip.IsVisible;

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

    /// <summary>色を設定し、入力欄とツールチップへ反映する。</summary>
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
                : FormatDisplayText(normalized);

            // 入力欄の左の四角は入力のたびに追随させる（文字を打っている間も現在の色が分かる）
            UpdateCurrentColorSwatch(normalized);

            // 入力欄のツールチップは 2 行（色名+不透明度 / カラーコード）を出す
            var tooltip = string.Concat(
                ColorString.ToDisplayText(normalized), Environment.NewLine, ColorString.ToArgbHex(normalized));
            _text.ToolTip(tooltip);
            _combo.ToolTip(tooltip);

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

    /// <summary>入力欄の左の四角を現在色で塗る（「■ + 色名」に見せる）。</summary>
    /// <param name="color">表示する色（保存値）。</param>
    /// <remarks>無効な入力の間は四角を隠す（前の色を出すと、その色が選ばれていると誤解される）。</remarks>
    private void UpdateCurrentColorSwatch(string? color)
    {
        if (ColorString.TryParse(color, out var parsed, out _))
        {
            _swatch.Background = ToMewColor(parsed);
            _swatch.IsVisible = true;
            return;
        }

        _swatch.IsVisible = false;
    }

    /// <summary>入力欄へ出す文字列を決める（カスタム タブの色名を優先する）。</summary>
    /// <param name="normalized">保存値（<c>#AARRGGBB</c>）。</param>
    /// <returns>表示する文字列。</returns>
    /// <remarks>
    /// 不透明度があるときは色名へ <c>+{n}%</c> を足す（色名だけでは不透明度が伝わらない）。<br/>
    /// 保存値はアルファ先頭の 16 進であり <c>+{n}%</c> を含まないため、不透明度はアルファから求める
    /// （<see cref="ColorString.ToDisplayText"/> と同じ解釈）。
    /// </remarks>
    private string FormatDisplayText(string normalized)
    {
        if (_textFormat == ColorTextFormat.NameWithAlpha && ResolveCustomName(normalized) is { } name)
        {
            return ColorString.TryParse(normalized, out var color, out _) && color.A < 255
                ? string.Concat(
                    name,
                    "+",
                    ((int)Math.Round(color.A * 100.0 / 255.0)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "%")
                : name;
        }

        return ColorString.ToText(normalized, _textFormat);
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

    /// <summary>検証エラーの強調を入力欄の枠線へ反映する。</summary>
    /// <remarks>
    /// <b>枠は標準のコンボボックスが持つ</b>: 自前の枠を持たないため、コンボボックスの枠線の色を変える<br/>
    /// （エラーでないときは自分の値を消し、テーマの枠線の色へ戻す）。
    /// </remarks>
    private void UpdateBorderValidationVisual()
    {
        if (_highlightBorderOnError && HasValidationError)
        {
            _combo.BorderBrush(s_errorColor);
        }
        else
        {
            _combo.ClearLocalValue(Control.BorderBrushProperty);
        }
    }

    // ====================================================================
    // 一覧（タブ付きのポップアップ）
    // ====================================================================

    /// <summary>コンボボックスの展開ボタン（右端）が押されたときに、タブ付きのポップアップを出す。</summary>
    /// <param name="e">マウスのイベント引数。</param>
    /// <remarks>
    /// <b>標準の一覧を開かせない</b>: コンボボックスは入力を処理する前に <c>MouseDown</c> を通知するため、<br/>
    /// ここで <see cref="MouseEventArgs.Handled"/> を立てると標準の開閉が行われない（開いてから閉じる方式は
    /// 開閉が往復して一覧が出ない。 AvaloniaUI 版の実測 2026-11-17）。<br/>
    /// <b>右端の展開ボタンの範囲だけを自前で扱う</b>: 文字を扱う左側は標準の入力のまま残す。<br/>
    /// <b>表示は入力の処理の後へ回す</b>: 押下の処理中に開くと、同じ押下で閉じ直されることがある。
    /// </remarks>
    private void OnComboMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButton.Left)
        {
            return;
        }

        // 押下位置はコンボボックス自身の座標で受ける（右端 30 DIP が展開ボタン）
        if (e.GetPosition(_combo).X < _combo.Bounds.Width - ToggleAreaWidth)
        {
            return;
        }

        e.Handled = true;
        ShowListDeferred();
    }

    /// <summary>キーボードの展開操作（Alt+↓ / F4）でタブ付きのポップアップを出す。</summary>
    /// <param name="e">キーのイベント引数。</param>
    /// <remarks>標準の一覧は開かせない（<see cref="OnComboMouseDown"/> と同じ一覧を出す）。</remarks>
    private void OnTextKeyDown(KeyEventArgs e)
    {
        var altDown = e.Key == Key.Down && e.AltKey;
        if (!altDown && e.Key != Key.F4)
        {
            return;
        }

        e.Handled = true;
        ShowListDeferred();
    }

    /// <summary>一覧の表示を入力の処理の後（次の描画の前）へ回す。</summary>
    /// <remarks>
    /// <b>押下の処理中に開かない</b>: 押下の処理の途中でポップアップを開くと、同じ押下を「外側の押下」として
    /// 拾って閉じ直すことがある（AvaloniaUI 版と同じく、入力の処理の後へ回す）。<br/>
    /// アプリ実行前（単体テスト）はそのまま表示する。
    /// </remarks>
    private void ShowListDeferred()
    {
        if (Application.IsRunning
            && Application.Current is { } app
            && app.Dispatcher is { } dispatcher)
        {
            dispatcher.BeginInvoke(DispatcherPriority.Background, ShowList);
            return;
        }

        ShowList();
    }

    /// <summary>タブ付きの一覧のポップアップを開閉する。</summary>
    /// <remarks>
    /// <b>表示できるタブが 1 つも無ければ開かない</b>（空の一覧を見せない）。<br/>
    /// テストから開閉を検証できるよう公開する（開けない場合は何もしないため、アプリ実行前でも安全である）。
    /// </remarks>
    internal void ToggleList()
    {
        if (_popup.IsOpen)
        {
            _popup.Close();
            return;
        }

        ShowList();
    }

    /// <summary>タブ付きの一覧を表示する（開くたびに作り直す）。</summary>
    /// <remarks>
    /// <b>開くたびに作り直す</b>: テーマの色は <c>Application.Run</c> 後にしか解決できず、明暗やアクセント色の
    /// 変更にも追随させる必要があるため、構築時に作った一覧をそのまま使わない。<br/>
    /// <b>タブの中身は選んだものだけを作る</b>: 開いていないタブ（カラーピッカーなど）の内容を作らない。
    /// </remarks>
    private void ShowList()
    {
        RefreshList();
        if (_tabs.Count == 0)
        {
            return;
        }

        // アンカーは**ウィンドウ座標**である（MewUI の Bounds はウィンドウ基準）。 ▼ はコンボボックスの
        // 右端にあるため、左端を揃えて開く（右端を揃えると一覧が左へ大きくずれる）
        _popup.ShowAt(this, _combo.Bounds, PopupAnchorSide.Below);
        if (_popup.IsOpen)
        {
            _log.LogOpenList(_selectedColor);
        }
    }

    /// <summary>一覧のタブと中身を作り直す。</summary>
    /// <remarks>
    /// <b>タブも作り直す</b>: タブは表示の設定（<see cref="ShowCustomTab"/> など）と <see cref="CustomTabs"/> から<br/>
    /// 決まるため、項目と同じ機会に作り直す（空のタブを残さない）。<br/>
    /// <b>中身は選んだタブだけを作る</b>: 表示していないタブ（カラーピッカーなど）の内容を作らない
    /// （開いていないのにピッカーのログを出さない）。<br/>
    /// <b>作り直しの途中で選択が動いても利用者の選択として扱わない</b>: 扱うと、並び順を変えただけで色の変更として処理される。<br/>
    /// タブの作り直しは一覧を開くまで起きないため、テストからの検証用に公開する。
    /// </remarks>
    internal void RefreshList()
    {
        var tabs = CreateTabs();
        _tabs.Clear();
        _tabs.AddRange(tabs);
        _tabButtons.Clear();
        _tabSlots.Clear();
        _tabStrip.Clear();
        _body.Clear();

        // 幅は**タブと中身を作る前に決める**（枠・タブの行・一覧の内容のうち最も広いもの）。
        // 決めずに置くとタブの行だけが広がり、スクロール バーがポップアップの途中に出る（実測 2026-11-17）
        _listWidth = ResolveFlyoutWidth(tabs);
        _body.Width = tabs.Count == 0 ? double.NaN : _listWidth;

        // タブは決まった幅で並ぶため、数が多いとポップアップの右端が切れる。 幅に収まるようタブの幅を詰める
        var tabWidth = ResolveTabWidth(_listWidth, tabs.Count);
        _log.LogDebug( // TEMP
            "[TEMP] width={W} tabs={N} tab={T} max={M}", _listWidth, tabs.Count, tabWidth, PopupMaxWidth);

        for (var i = 0; i < tabs.Count; i++)
        {
            var index = i;
            var button = CreateCategoryTab(tabs[i].Title, tabWidth);
            button.OnCheckedChanged(_ => OnTabChanged(index));
            _tabButtons.Add(button);
            _tabStrip.Add(button);

            // 入れ物だけを作り、中身は表示するときに作る（開いていないタブを作らない）
            var slot = new Grid();
            slot.IsVisible(false);
            _tabSlots.Add(slot);
            _body.Add(slot);
        }

        // タブが 1 つで題名を隠す設定なら、タブの行そのものを出さない（切り替える相手が無い）
        _tabStrip.IsVisible = tabs.Count > 1 || ShowTabTitles;

        _applying = true;
        try
        {
            // 前回のタブが残っていればそこから、無ければ先頭のタブから開く
            // （カラーピッカーのタブは選ぶとポップアップを閉じるため、開き直しでは選ばない）
            var lastIndex = tabs.FindIndex(tab => string.Equals(tab.Key, _listTabKey, StringComparison.Ordinal));
            if (lastIndex < 0 || tabs[lastIndex].IsPicker)
            {
                lastIndex = tabs.FindIndex(tab => !tab.IsPicker);
            }

            ApplyTab(tabs.Count == 0 ? -1 : Math.Max(lastIndex, 0));
        }
        finally
        {
            _applying = false;
        }
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

    /// <summary>タブの中身を作る（色名付きの一覧・色の四角の格子・カラーピッカーのタブ）。</summary>
    /// <param name="tab">タブの定義。</param>
    /// <param name="width">一覧の幅（DIP。 スクロール バーが右端へ来るようにそろえる）。</param>
    /// <returns>タブの中身。</returns>
    /// <remarks>
    /// <b>カラーピッカーのタブは中身を持たない</b>: MewUI の <see cref="ColorPicker"/> はドロップダウンであり、
    /// ポップアップの中へ埋め込めない。 タブを選んだときに一覧を閉じてピッカーのドロップダウンを開くため、
    /// ここでは空の入れ物だけを返す（開いていない間にピッカーのログを出さない）。
    /// <b>この処理はタブを最初に表示するときだけ呼ぶ</b>（全タブを先に作らない）。
    /// </remarks>
    internal FrameworkElement CreateTabView(ColorSelectorTabSpec tab, double width)
    {
        if (tab.IsPicker)
        {
            return new Grid();
        }

        return tab.Layout == ColorSelectorTabLayout.Grid
            ? CreateSwatchGrid(tab)
            : CreateColorList(tab, width);
    }

    /// <summary>色名付きの一覧（縦に並べてスクロールする）を作る。</summary>
    /// <param name="tab">タブの定義。</param>
    /// <param name="width">一覧の幅（DIP）。</param>
    /// <returns>一覧。</returns>
    /// <remarks>
    /// <b>幅をそろえる</b>: 一覧を内容の幅のまま置くと、スクロール バーがポップアップの途中に出る
    /// （タブの行だけが広いとき。 実測 2026-11-17）。 一覧はポップアップの幅いっぱいにする。
    /// </remarks>
    private ListBox CreateColorList(ColorSelectorTabSpec tab, double width)
    {
        var list = new ListBox
        {
            Width = width,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxHeight = ListMaxHeight,
            ItemTemplate = _itemTemplate,
            ItemsSource = ItemsView.Create(tab.Items),
        };
        list.SelectionChanged += _ => ApplyListSelection(list);
        return list;
    }

    /// <summary>色の四角だけを 6 列に並べた格子を作る（スクロールせずに選べる）。</summary>
    /// <param name="tab">タブの定義。</param>
    /// <returns>格子。</returns>
    private UniformGrid CreateSwatchGrid(ColorSelectorTabSpec tab)
    {
        var grid = new UniformGrid().Columns(SwatchGridColumns);
        foreach (var item in tab.Items)
        {
            grid.Add(CreateSwatch(item));
        }

        return grid;
    }

    /// <summary>格子の 1 マス（色の四角）を作る。</summary>
    /// <param name="item">並べる色。</param>
    /// <returns>色の四角を入れたボタン（名前はツールチップで示す）。</returns>
    private Button CreateSwatch(ColorSelectorItem item)
    {
        var swatch = new Border
        {
            Width = SwatchSize,
            Height = SwatchSize,
            CornerRadius = 3,
            BorderBrush = MewColor.FromArgb(0x40, 0x80, 0x80, 0x80),
            BorderThickness = 1,
            Background = ColorString.TryParse(item.Value, out var color, out _)
                ? ToMewColor(color)
                : MewColor.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var button = new Button
        {
            Width = SwatchCellSize,
            Height = SwatchCellSize,
            Padding = new Thickness(2),
            Background = MewColor.Transparent,
            BorderThickness = 0,
        }.Content(swatch);
        button.ToolTip(item.Label);
        button.Click += () => ApplyItem(item);
        return button;
    }

    /// <summary>一覧の選択が変わったときに色を反映する。</summary>
    /// <param name="list">選択された一覧。</param>
    private void ApplyListSelection(ListBox list)
    {
        // 色を反映すると一覧の選択も同じ色へ更新される。 その往復を「利用者の選択」として扱わない
        if (_applying || _selecting)
        {
            return;
        }

        if (list.SelectedItem is ColorSelectorItem item)
        {
            ApplyItem(item);
        }
    }

    /// <summary>選んだ色を反映して一覧を閉じる。</summary>
    /// <param name="item">選んだ色。</param>
    private void ApplyItem(ColorSelectorItem item)
    {
        // テーマの色は識別子を覚える（明暗・アクセントの変更へ追随させる）。 色名・カスタムは値だけを写す
        _selectedThemeColor = item.ThemeKey;
        ApplyColor(item.Value, raiseChanged: true);

        // 選んだら一覧を閉じる（開いたままにすると、選んだ色が一覧の裏に隠れて確認できない。 他 UI も選ぶと閉じる）
        _popup.Close();

        // 同じ色を選び直したときも通知する（値の変更だけでは足りない用途がある）
        RaiseColorApplied();
    }

    /// <summary><see cref="ColorApplied"/> を発生させる。</summary>
    private void RaiseColorApplied() => ColorApplied?.Invoke(this, EventArgs.Empty);

    /// <summary>一覧のタブを切り替える。</summary>
    /// <param name="index">表示するタブの索引（範囲外なら一覧を空にする）。</param>
    /// <remarks>
    /// <b>1 つだけが選ばれる</b>: タブは押すたびに状態が反転するため、選ばれたものだけを選んだ状態にし、<br/>
    /// 中身は選ばれたタブだけを表示する。
    /// </remarks>
    internal void ApplyTab(int index)
    {
        if (index < 0 || index >= _tabs.Count)
        {
            _listTabKey = null;
            foreach (var hidden in _tabSlots)
            {
                hidden.IsVisible(false);
            }

            return;
        }

        var tab = _tabs[index];
        _listTabKey = tab.Key;

        // すべてのタブの状態を書くと自分で書いた通知も届くため、書き込みの間は通知を無視する
        _switchingTabs = true;
        try
        {
            for (var i = 0; i < _tabButtons.Count; i++)
            {
                _tabButtons[i].IsChecked(i == index);
            }
        }
        finally
        {
            _switchingTabs = false;
        }

        // 中身は**最初に表示するときだけ**作る（開いていないタブを作らない）
        var slot = _tabSlots[index];
        if (slot.Count == 0)
        {
            slot.Add(CreateTabView(tab, _listWidth));
        }

        for (var i = 0; i < _tabSlots.Count; i++)
        {
            _tabSlots[i].IsVisible(i == index);
        }

        if (slot[0] is ListBox list)
        {
            SelectCurrent(list, tab.Items);
        }
    }

    /// <summary>タブが押されたときに切り替える（自分で書いた通知は無視する）。</summary>
    /// <param name="index">タブの索引。</param>
    /// <remarks>
    /// <b>外れた通知でも切り替える</b>: タブは押すたびに状態が反転するため、選択中のタブを押し直すと外れる。<br/>
    /// その場合も切り替えを行い、選ばれた状態へ戻す（常に 1 つだけを選んだ状態に保つ）。<br/>
    /// <b>カラーピッカーのタブはドロップダウンを開く</b>: MewUI の <see cref="ColorPicker"/> は中身を
    /// ポップアップへ埋め込めないため、一覧を閉じてからピッカーを開く（ドロップダウンを重ねない）。
    /// </remarks>
    private void OnTabChanged(int index)
    {
        if (_switchingTabs)
        {
            return;
        }

        ApplyTab(index);
        if (index >= 0 && index < _tabs.Count && _tabs[index].IsPicker)
        {
            OpenPicker();
        }
    }

    /// <summary>一覧のタブを作る。</summary>
    /// <param name="text">タブの文言。</param>
    /// <param name="width">タブの幅（DIP）。</param>
    /// <returns>タブ（<see cref="ToggleButton"/>）。</returns>
    private static ToggleButton CreateCategoryTab(string text, double width)
        => new ToggleButton { Width = width }.Content(text);

    /// <summary>一覧（ポップアップ）の幅を求める。</summary>
    /// <param name="tabs">開くタブ。</param>
    /// <returns>幅（DIP）。 枠（入力欄）・タブの行・一覧の内容のうち最も広いもの（ポップアップが収まる幅が上限）。</returns>
    internal double ResolveFlyoutWidth(IReadOnlyList<ColorSelectorTabSpec> tabs)
        => ResolveFlyoutWidth(Bounds.Width, tabs, PopupMaxWidth, ShowTabTitles);

    /// <summary>一覧（ポップアップ）の幅を求める。</summary>
    /// <param name="frameWidth">枠（入力欄）の幅（DIP）。</param>
    /// <param name="tabs">開くタブ。</param>
    /// <param name="maxWidth">ポップアップに使える幅の上限（DIP。 無い場合は <see cref="double.PositiveInfinity"/>）。</param>
    /// <param name="showTabTitles">タブが 1 つでも題名の行を出すかどうか。</param>
    /// <returns>幅（DIP）。</returns>
    /// <remarks>
    /// <b>タブの行も幅に含める</b>: 含めないと、タブの行だけが広がって一覧（とスクロール バー）が
    /// ポップアップの途中で切れる（実測 2026-11-17）。<br/>
    /// カラーピッカーのタブは中身が自分の幅を持つため含めない（開いたときに広がる）。<br/>
    /// <b>上限は「タブを最小の幅で並べた行」までは広げる</b>: 上限で切ると、タブが多いときに
    /// 最後のタブが欠ける（実測 2026-11-17: 14 個で「ColorPicker」が欠けた）。 MewUI のポップアップは
    /// 中身の幅で測って作業領域へ寄せるため、枠より広くても切れない。<br/>
    /// 測る処理は切り出し、ウィンドウを持たない単体テストでも計算を検証できるようにする。
    /// </remarks>
    internal static double ResolveFlyoutWidth(
        double frameWidth,
        IReadOnlyList<ColorSelectorTabSpec> tabs,
        double maxWidth,
        bool showTabTitles)
    {
        var showTabs = tabs.Count > 1 || showTabTitles;
        var width = frameWidth;
        if (showTabs)
        {
            width = Math.Max(width, TabRowWidth(tabs.Count, CategoryTabWidth));
        }

        foreach (var tab in tabs)
        {
            if (tab.IsPicker)
            {
                continue;
            }

            width = tab.Layout == ColorSelectorTabLayout.Grid
                ? Math.Max(width, SwatchCellSize * SwatchGridColumns)
                : Math.Max(width, MeasureListContentWidth(tab.Items));
        }

        var limit = showTabs ? Math.Max(maxWidth, TabRowWidth(tabs.Count, MinCategoryTabWidth)) : maxWidth;
        return Math.Min(width, limit);
    }

    /// <summary>タブを横に並べたときの幅を求める。</summary>
    /// <param name="count">タブの数。</param>
    /// <param name="tabWidth">タブ 1 つの幅（DIP）。</param>
    /// <returns>幅（DIP）。 タブが無い場合は 0。</returns>
    private static double TabRowWidth(int count, double tabWidth)
        => count <= 0 ? 0 : (count * tabWidth) + ((count - 1) * TabSpacing);

    /// <summary>タブの幅を求める（数を増やすほど詰める）。</summary>
    /// <param name="width">一覧（ポップアップ）の幅（DIP）。</param>
    /// <param name="count">タブの数。</param>
    /// <returns>タブ 1 つの幅（DIP）。</returns>
    internal static double ResolveTabWidth(double width, int count)
    {
        if (count <= 0)
        {
            return CategoryTabWidth;
        }

        var fitting = (width - ((count - 1) * TabSpacing)) / count;
        return Math.Clamp(fitting, MinCategoryTabWidth, CategoryTabWidth);
    }

    /// <summary>一覧に並べたときの内容（行と余白）の幅を測る。</summary>
    /// <param name="entries">一覧に並べる項目。</param>
    /// <returns>内容の幅（DIP）。 測れない場合は 0。</returns>
    /// <remarks>
    /// <b>画面へ出ていない間（アプリ実行前・ヘッドレス）は測らない</b>: 文字の測定は描画のバックエンドを
    /// 要求するため、ここで測ると生成そのものが失敗する（実測 2026-11-02）。<br/>
    /// <b>行は実際のテンプレートで測る</b>: 固定値だと文字サイズや文言を変えたときに幅が追随しない。
    /// </remarks>
    private static double MeasureListContentWidth(IReadOnlyList<ColorSelectorItem> entries)
    {
        if (!Application.IsRunning)
        {
            return 0;
        }

        var widest = 0.0;
        foreach (var entry in entries)
        {
            var row = CreateItemRow(ListFontSize);
            ApplyItemToRow(row, entry);
            row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, row.DesiredSize.Width);
        }

        var padding = Application.Current.Theme.Metrics.ItemPadding;
        return widest + padding.Left + padding.Right + ListWidthAllowance;
    }

    /// <summary>ポップアップに使える中身の幅の上限（DIP）を取得する。</summary>
    /// <value>ウィンドウのクライアント領域の幅から影の分を引いた値。 ウィンドウが無い場合は無制限。</value>
    /// <remarks>
    /// <b>MewUI のポップアップは配置できる領域の幅で測る</b>（クライアント領域。 <c>Popup.Place</c>）。
    /// 上限が無いと、タブが多いときにポップアップが右へはみ出して切れる（AvaloniaUI のフライアウトは
    /// 既定の最大幅 480 DIP で切れる。 実測 2026-11-17）。
    /// </remarks>
    private double PopupMaxWidth
        => FindVisualRoot() is Window window
            ? Math.Max(0.0, window.ClientSize.Width - PopupChromeWidth)
            : double.PositiveInfinity;

    /// <summary>一覧の選択を現在の色へ合わせる。</summary>
    /// <param name="list">対象の一覧。</param>
    /// <param name="entries">一覧に並べる項目。</param>
    /// <remarks>選択の書き込みで通知が届いても、利用者の選択として扱わない（<see cref="_selecting"/>）。</remarks>
    private void SelectCurrent(ListBox list, IReadOnlyList<ColorSelectorItem> entries)
    {
        if (!ColorString.TryParse(_selectedColor, out var color, out _))
        {
            return;
        }

        _selecting = true;
        try
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (ColorString.TryParse(entries[i].Value, out var candidate, out _)
                    && candidate.R == color.R && candidate.G == color.G && candidate.B == color.B)
                {
                    list.SelectedIndex = i;
                    return;
                }
            }

            list.SelectedIndex = -1;
        }
        finally
        {
            _selecting = false;
        }
    }

    /// <summary>カラーピッカーのドロップダウンを開く（タブ付きの一覧は閉じる）。</summary>
    /// <remarks>
    /// <b>ドロップダウンを重ねない</b>: 一覧のポップアップを閉じてからピッカーのドロップダウンを開く。<br/>
    /// アンカーは 1×1 の入れ物（右下）であり、入力欄の下へ開く。
    /// </remarks>
    private void OpenPicker()
    {
        _popup.Close();
        if (FindVisualRoot() is not Window)
        {
            return;
        }

        if (ColorString.TryParse(_selectedColor, out var color, out _))
        {
            _picker.SelectedColor = ToMewColor(color);
        }

        if (_picker.IsDropDownOpen)
        {
            return;
        }

        _picker.IsDropDownOpen = true;

        // ピッカーを開いたことを残す（一覧を開いたログと対。 他 UI と同じ書式）
        _log.LogOpenPicker(_selectedColor);
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

        // ピッカーは同じ色を選び直しても確定として扱う（値の変更だけでは足りない用途がある）
        RaiseColorApplied();
    }

    // ====================================================================
    // 一覧の項目
    // ====================================================================

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
            items.Add(new ColorSelectorItem(entry.Name, entry.Name, ColorSelectorCategory.Named));
        }

        return items;
    }

    /// <summary>テーマの色（識別子と、パレットから色を取る関数）。 並び順がそのまま一覧の順序になる。</summary>
    /// <remarks>
    /// <b>識別子と取得を 1 箇所に置く</b>: 一覧の表示と <see cref="SelectedThemeColor"/> の解決が同じ出所になり、<br/>
    /// 表を 2 つ持って食い違うことがない（<see cref="ThemeColorKeys"/> も本表から作る）。
    /// </remarks>
    private static readonly (string Key, Func<Palette, MewColor> Resolve)[] s_themeColors =
    [
        // 3 UI で共通の意味を持つ色（同じ名前・同じ順）
        ("背景", static palette => palette.WindowBackground),
        ("レイヤー", static palette => palette.ContainerBackground),
        ("カード", static palette => palette.ControlBackground),
        ("コントロールの背景", static palette => palette.ButtonFace),
        ("コントロールの枠線", static palette => palette.ControlBorder),
        ("区切り線", static palette => palette.DisabledControlBackground),
        ("文字", static palette => palette.WindowText),
        ("副次テキスト", static palette => palette.PlaceholderText),
        ("無効の文字", static palette => palette.DisabledText),
        ("無効の背景", static palette => palette.ButtonDisabledBackground),
        ("アクセント", static palette => palette.Accent),
        ("アクセント（淡）", static palette => palette.AccentHoverOverlay),
        ("アクセント（濃）", static palette => palette.AccentBorderHotOverlay),
        ("アクセント文字", static palette => palette.AccentText),
        ("アクセント上の文字", static palette => palette.AccentText),
        ("選択の背景", static palette => palette.SelectionBackground),
        ("ホバーの背景", static palette => palette.ButtonHoverBackground),
        ("押下の背景", static palette => palette.ButtonPressedBackground),
        ("フォーカス", static palette => palette.Focus),
        ("エラー", static palette => palette.Error),

        // MewUI にしか無い色
        ("選択の文字", static palette => palette.SelectionText),
        ("無効のアクセント", static palette => palette.DisabledAccent),
        ("アクセントの枠線（ホバー）", static palette => palette.AccentBorderHotOverlay),
        ("アクセントの枠線（押下）", static palette => palette.AccentBorderActiveOverlay),
        ("アクセントの押下", static palette => palette.AccentPressedOverlay),
        ("スクロール バー", static palette => palette.ScrollBarThumb),
        ("スクロール バー（ホバー）", static palette => palette.ScrollBarThumbHover),
        ("スクロール バー（押下）", static palette => palette.ScrollBarThumbActive),
    ];

    /// <summary>テーマの色の識別子の一覧を取得する（<see cref="SelectedThemeColor"/> へ設定できる値）。</summary>
    /// <value>この UI が並べるテーマの色の識別子（並びは一覧と同じ）。</value>
    /// <remarks>
    /// <b>利用側は本値を保存すればテーマに追随できる</b>: 色そのもの（<c>#AARRGGBB</c>）を保存すると明暗の切替に追随できない。<br/>
    /// 設定画面の選択肢を作るときにも使える（実際に並ぶのは解決できた色だけである）。
    /// </remarks>
    public static IReadOnlyList<string> ThemeColorKeys { get; } = [.. s_themeColors.Select(static entry => entry.Key)];

    /// <summary>一覧の先頭へ並べるテーマの色を追加する（MewUI のパレットの色をすべて並べる）。</summary>
    /// <param name="items">追加先。</param>
    /// <remarks>
    /// <b>並び順は 3 UI で共通</b>: 意味の対応する色は同じ名前・同じ順にし、その UI にしか無い色を後ろへ足す<br/>
    /// （UI を替えても同じ名前の色を選べる）。<br/>
    /// <b>値は一覧を作り直すたびに解決する</b>（<c>Application.Run</c> の前はパレットが無いため、そのときは並ばない）。
    /// </remarks>
    private static void AddThemeColors(List<ColorSelectorItem> items)
    {
        var palette = Application.Current.Theme.Palette;
        foreach (var (key, resolve) in s_themeColors)
        {
            AddThemeColor(items, ThemeLabelPrefix + key, resolve(palette));
        }
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

    /// <summary>コアの色を MewUI の色へ写す。</summary>
    /// <param name="color">コアの色。</param>
    /// <returns>同じ ARGB を持つ MewUI の色。</returns>
    private static MewColor ToMewColor(DrawingColor color)
        => MewColor.FromArgb(color.A, color.R, color.G, color.B);

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
    private static void ApplyItemToRow(FrameworkElement view, ColorSelectorItem item)
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
}
