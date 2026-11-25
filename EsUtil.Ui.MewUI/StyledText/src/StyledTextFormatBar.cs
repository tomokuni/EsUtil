using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Text;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.MewUI.ColorSelector;
using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Ui.MewUI.StyledText;

/// <summary>本文を編集中に本文の上へ出す書式バー（文字単位の書式）。</summary>
/// <remarks>
/// <b>本文はコントロールが直接編集する</b>ため、このバーは「選択中の範囲へ書式を適用する」だけを行う。
/// 適用しても編集は続き、結果はその場で本文へ反映される。<br/>
/// <b>項目名は出さない</b>: 幅が限られるため、意味は現在値（フォント名・大きさ）と装飾ボタンの文言で示す。<br/>
/// <b>同じ値でも適用できる</b>: 文字単位で書式が混ざっている範囲を一律に揃えるため、選び直しただけでも適用する。
/// </remarks>
public sealed class StyledTextFormatBar : ContentControl, IStyledTextFormatBar
{
    /// <summary>バーのおおよその高さ（DIP。本文の上へ置く位置の計算に使う）。</summary>
    internal const double EstimatedHeight = 40;

    /// <summary>書式を読み書きする相手（ホストが渡す）。</summary>
    private readonly ITextFormatBarContext _context;

    /// <summary>バーの構成（フォントの一覧・色のタブ）。</summary>
    private readonly StyledTextFormatBarOptions _options;

    /// <summary>選べるフォント名の一覧。</summary>
    private readonly IReadOnlyList<string> _fonts;

    /// <summary>フォントの選択（枠と展開ボタンだけを描く）。</summary>
    private readonly ComboBox _font;

    /// <summary>フォント名の入力（コンボボックスの文字の位置へ重ねる）。</summary>
    private readonly TextBox _fontText;

    /// <summary>フォントの入力と選択を重ねた入れ物。</summary>
    private readonly Grid _fontCell;

    /// <summary>文字の大きさの入力。</summary>
    private readonly NumericUpDown _size;

    /// <summary>文字色の選択。</summary>
    private readonly ColorSelectorControl _color;

    /// <summary>太字の切り替え。</summary>
    private readonly ToggleButton _bold;

    /// <summary>斜体の切り替え。</summary>
    private readonly ToggleButton _italic;

    /// <summary>下線の切り替え。</summary>
    private readonly ToggleButton _underline;

    /// <summary>取り消し線の切り替え。</summary>
    private readonly ToggleButton _strike;

    /// <summary>表示を更新している最中かどうか（操作の再入を防ぐ）。</summary>
    private bool _isRefreshing;

    /// <summary>書式バーを初期化する。</summary>
    /// <param name="context">書式を読み書きする相手（ホストが実装する）。</param>
    /// <param name="options">バーの構成（フォントの一覧・色のタブ）。 省略すると既定の構成を使う。</param>
    public StyledTextFormatBar(ITextFormatBarContext context, StyledTextFormatBarOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _options = options ?? new StyledTextFormatBarOptions();
        _fonts = _options.Fonts;

        // **フォント名は直接入力できる**: MewUI の ComboBox は読み取り専用のため、
        // コンボボックスが枠と展開ボタンを描き、その文字の位置へ**枠も背景も持たない文字入力**を重ねる
        // （EsUtil の色選択と同じ作り）
        // 標準のテキスト入力を使うため、Ctrl+C / Ctrl+V もそのまま使える。
        _font = new ComboBox().Items([.. _fonts]);
        _font.Width = FontSelectorWidth;
        _font.SelectedIndex = 0;
        _font.OnSelectionChanged(_ =>
        {
            if (!_isRefreshing && _font.SelectedIndex >= 0)
            {
                SetFontText(_fonts[_font.SelectedIndex]);
            }

            ApplyFont();
        });

        // 文字の入力は**コンボボックスの内側**へ重ねる（入力欄の枠・▼はコンボボックスが描く）
        _fontText = new TextBox
        {
            Text = _fonts.Count > 0 ? _fonts[0] : string.Empty,
            Width = FontSelectorWidth - FontArrowAreaWidth,
            MinHeight = TextFormatBarLayout.ItemHeight,
            Height = TextFormatBarLayout.ItemHeight,
            BorderThickness = 0,
            CornerRadius = 0,
            Background = Color.Transparent,
            Padding = new Thickness(FontTextLeftIndent, 0, 0, 0),
            Margin = new Thickness(0, 0, FontArrowAreaWidth, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _fontText.KeyDown += e =>
        {
            if (e.Key == Key.Enter)
            {
                ApplyFont();
                e.Handled = true;
            }
        };
        _fontText.LostFocus += () => ApplyFont();

        _fontCell = new Grid { Width = FontSelectorWidth };
        _fontCell.Add(_font);
        _fontCell.Add(_fontText);

        _size = new NumericUpDown().Minimum(TextLimits.FontSizeMin).Maximum(TextLimits.FontSizeMax).Step(1);
        _size.Width = SizeSelectorWidth;
        _size.Value = TextDefaults.FontSizeDefault;
        _size.OnValueChanged(_ => ApplySize());

        _color = new ColorSelectorControl
        {
            Width = ColorSelectorWidth,
            HighlightBorderOnError = true,
        };
        _options.ConfigureColorSelector?.Invoke(_color, _options.PaletteColumns);
        _color.SelectedColorChanged += (_, _) => ApplyColor();

        // 高さはコア（TextFormatBarLayout）が単一所有する。 項目ごとに高さが違うと縦位置が揃わない
        _font.Height = TextFormatBarLayout.ItemHeight;
        _size.Height = TextFormatBarLayout.ItemHeight;
        _color.Height = TextFormatBarLayout.ItemHeight;

        // **中の入力欄も同じ高さにする**: 色の選択の中の ComboBox は自分の高さを持ち、外側の高さでは
        // 伸びない。 揃えないと色の入力欄だけが低く見える
        _color.Height = TextFormatBarLayout.ItemHeight;

        _bold = CreateToggle("B", bold: true);
        _italic = CreateToggle("I", italic: true);
        _underline = CreateToggle("U", underline: true);
        _strike = CreateToggle("S", strike: true);

        // **キーは装飾の切り替えから集める**: MewUI のイベントは子から親へ伝播しない
        foreach (var toggle in new[] { _bold, _italic, _underline, _strike })
        {
            toggle.KeyDown += OnBarKeyDown;
        }

        _bold.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsBold, static (style, value) => style with { IsBold = value }));
        _italic.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsItalic, static (style, value) => style with { IsItalic = value }));
        _underline.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsUnderline, static (style, value) => style with { IsUnderline = value }));
        _strike.OnCheckedChanged(_ => ToggleDecoration(
            style => style.IsStrike, static (style, value) => style with { IsStrike = value }));

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = ControlSpacing,

            // 高さの違う項目が混ざっても縦位置を揃える（規則の所有者はコアの ItemHeight）
            VerticalAlignment = VerticalAlignment.Center,
        };

        // 並び順はコア（TextFormatBarLayout.Order）が単一所有する。
        // ここで順を決めると 4 つの UI 版で操作の位置がずれる。
        var items = new Dictionary<TextFormatBarItem, Element>
        {
            [TextFormatBarItem.Bold] = _bold,
            [TextFormatBarItem.Italic] = _italic,
            [TextFormatBarItem.Underline] = _underline,
            [TextFormatBarItem.Strike] = _strike,
            [TextFormatBarItem.Font] = _fontCell,
            [TextFormatBarItem.Size] = _size,
            [TextFormatBarItem.Color] = _color,
        };

        foreach (var item in TextFormatBarLayout.Order)
        {
            // **高さは項目ごとに違ってはいけない**: 揃えないと縦位置がずれ、押す位置も変わる。
            // 値は提示層の TextFormatBarLayout.ItemHeight が単一所有する
            var element = (FrameworkElement)items[item];
            element.Height = TextFormatBarLayout.ItemHeight;
            element.MinHeight = TextFormatBarLayout.ItemHeight;
            element.VerticalAlignment = VerticalAlignment.Center;
            controls.Add(element);
        }

        // 枠・余白・下地はコアの外観（AnnotationPanelAppearance）が単一所有する。
        // ここで持つと、パネルの枠と二重になって「余計な辺枠」に見える。
        Content = controls;
    }

    /// <summary>フォントの選択の幅（DIP。 値はコアが単一所有する）。</summary>
    internal const double FontSelectorWidth = TextFormatBarLayout.FontSelectorWidth;

    /// <summary>大きさの入力の幅（DIP。 値はコアが単一所有する）。</summary>
    internal const double SizeSelectorWidth = TextFormatBarLayout.SizeSelectorWidth;

    /// <summary>色の選択の幅（DIP。 規則はコアが単一所有する）。</summary>
    internal static readonly double ColorSelectorWidth =
        TextFormatBarLayout.ResolveColorSelectorWidth(RichTextPainter.Measure);

    /// <summary>操作の間の余白（DIP）。</summary>
    private const double ControlSpacing = TextEditorLayout.ControlSpacing;

    /// <summary>コンボボックスの展開ボタンへ空ける幅（DIP。 文字入力が矢印へ重ならないようにする）。</summary>
    private const double FontArrowAreaWidth = 24;

    /// <summary>文字入力の左の余白（DIP。 コンボボックスの内側の余白に合わせる）。</summary>
    private const double FontTextLeftIndent = 8;

    /// <summary>フォント名の入力を表示へ反映する。</summary>
    /// <param name="family">フォント名。</param>
    private void SetFontText(string family)
    {
        if (!string.Equals(_fontText.Text, family, StringComparison.Ordinal))
        {
            _fontText.Text = family;
        }
    }

    /// <summary>装飾の切り替えボタンの幅（DIP）。</summary>
    private const double ToggleWidth = TextEditorLayout.ToggleWidth;

    /// <summary>バーの画面要素を取得する（ホストはこれをパネルへ載せる）。</summary>
    public FrameworkElement View => this;

    /// <summary>バーの幅（DIP）を取得する。</summary>
    public double PreferredWidth => TextFormatBarLayout.FontSelectorWidth
        + TextFormatBarLayout.SizeSelectorWidth
        + (TextEditorLayout.ToggleWidth * 4);

    /// <summary>本文のキー操作を転送する処理を設定する。</summary>
    /// <param name="forward">転送する処理（キーの引数を受け取る。<see langword="null"/> は転送しない）。</param>
    public void SetKeyHandler(Func<KeyEventArgs, bool>? forward) => _forwardKey = forward;

    /// <summary>本文のキー操作を転送する処理（ホストが設定する）。</summary>
    private Func<KeyEventArgs, bool>? _forwardKey;

    /// <summary>バーへ届いたキーを本文へ転送する（入力欄の内側では転送しない）。</summary>
    /// <param name="e">キーの引数。</param>
    /// <remarks>
    /// <b>装飾の切り替えへ個別に繋ぐ</b>: MewUI のイベントは子から親へ伝播しないため、
    /// バーの根へ繋いでもボタンのキーは届かない。 入力欄（数値・フォント名・色）は自分のキーを使うため
    /// 繋がない（転送すると入力欄の編集が壊れる）。
    /// </remarks>
    private void OnBarKeyDown(KeyEventArgs e)
    {
        if (e.Handled || _forwardKey is not { } forward)
        {
            return;
        }

        if (forward(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>本文へフォーカスを戻す処理を設定する。</summary>
    /// <param name="request">フォーカスを戻す処理（<see langword="null"/> は何もしない）。</param>
    public void SetFocusRequest(Action? request) => _requestFocus = request;

    /// <summary>本文へフォーカスを戻す処理（ホストが設定する）。</summary>
    private Action? _requestFocus;

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（<see cref="ColorTextFormat.NameWithAlpha"/>）。</value>
    /// <remarks>
    /// <b>代表色を選んだら色名で出す</b>: 色名のほうが「どの色を選んだか」を読み取りやすい。
    /// 色名の無い色は <c>#AARRGGBB</c>（8 桁）で出るため、表記によって不透明度が見えなくなることはない
    /// （規則は <c>ColorString.Format</c> が単一所有する）。
    /// </remarks>
    public ColorTextFormat TextFormat
    {
        get => _color.TextFormat;
        set => _color.TextFormat = value;
    }

    /// <summary>現在の書式を表示へ反映する（選択範囲の先頭の書式）。</summary>
    /// <remarks>
    /// <b>文字を選んでいなくても操作できる</b>: 選択が無いときに選んだ書式は、以降の入力へ効く
    /// （無効にすると「書式を変えられない」と見える）
    /// </remarks>
    public void Refresh()
    {
        var style = _context.SelectedTextStyle;

        _isRefreshing = true;
        try
        {
            var index = _fonts.ToList().IndexOf(style.FontFamily);
            _font.SelectedIndex = index >= 0 ? index : -1;
            SetFontText(style.FontFamily);
            _size.Value = style.FontSize;
            _color.SelectedColor = style.Color.ToHexText();

            // 装飾は「選択範囲のすべてが同じ属性か」で押下状態を決める（一部だけ太字の範囲で押されて見えないように）
            _bold.IsChecked = _context.SelectedTextMatches(static textStyle => textStyle.IsBold);
            _italic.IsChecked = _context.SelectedTextMatches(static textStyle => textStyle.IsItalic);
            _underline.IsChecked = _context.SelectedTextMatches(static textStyle => textStyle.IsUnderline);
            _strike.IsChecked = _context.SelectedTextMatches(static textStyle => textStyle.IsStrike);
        }
        finally
        {
            _isRefreshing = false;
        }

        InvalidateMeasure();
    }

    /// <summary>装飾の切り替えボタンを作る。</summary>
    /// <param name="text">文言。</param>
    /// <param name="bold">太字で表示するかどうか。</param>
    /// <param name="italic">斜体で表示するかどうか。</param>
    /// <param name="underline">下線を引いて表示するかどうか。</param>
    /// <param name="strike">取り消し線を引いて表示するかどうか。</param>
    /// <returns>切り替えボタン。</returns>
    private static ToggleButton CreateToggle(
        string text, bool bold = false, bool italic = false, bool underline = false, bool strike = false)
    {
        const double height = TextFormatBarLayout.ItemHeight;

        // **記号自体にも装飾を描く**: 「U」「S」の文字だけでは意味が伝わらない。
        // 線は文字へ持たせる（MewUI の TextBlock は文字単位の装飾を Run で持てる）。 図形を重ねると
        // 文字との位置がずれ、線だけが別の場所に見える。
        var label = new TextBlock
        {
            Width = ToggleWidth,
            Height = height,

            // **上下中央へ寄せる**: 記号は字面の上下に余白を持たない（Consolas の "B" "I" は行の
            // 上寄りに描かれる）ため、そのままでは下へ 4 DIP ずれて見える。
            // MewUI の負の上余白は半分だけ効くため、倍の値を与える。
            Margin = new Thickness(0, -2, 0, 0),

            // **記号は行の文字より一回り大きくする**（値は提示層の AnnotationEditorLayout が単一所有する）
            FontSize = TextEditorLayout.ToggleFontSize,
            FontFamily = TextEditorLayout.ToggleFontFamily,
            TextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        };

        label.Inlines.Add(new Run
        {
            Text = text,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            Italic = italic,
            Decoration = strike
                ? TextDecoration.Strikethrough
                : underline ? TextDecoration.Underline : TextDecoration.None,
        });

        return new ToggleButton
        {
            Content = label,
            Width = ToggleWidth,
            Height = height,
            Padding = new Thickness(0),
        };
    }

    /// <summary>選択中の範囲へフォントを適用する（同じ値でも適用する）。</summary>
    private void ApplyFont()
    {
        if (_isRefreshing)
        {
            return;
        }

        // 一覧から選んだ値でも、直接入力した値でも同じように適用する
        var family = string.IsNullOrWhiteSpace(_fontText.Text)
            ? _font.SelectedIndex >= 0 ? _fonts[_font.SelectedIndex] : null
            : _fontText.Text;

        if (string.IsNullOrWhiteSpace(family))
        {
            return;
        }

        Apply(style => style with { FontFamily = family });
    }

    /// <summary>選択中の範囲へ大きさを適用する（同じ値でも適用する）。</summary>
    private void ApplySize()
    {
        if (_isRefreshing)
        {
            return;
        }

        var value = _size.Value;
        Apply(style => style with { FontSize = value });
    }

    /// <summary>選択中の範囲へ色を適用する（同じ色を選び直しても適用する）。</summary>
    private void ApplyColor()
    {
        if (_isRefreshing || !DrawingColorExtensions.TryParseHexText(_color.SelectedColor, out var color))
        {
            return;
        }

        Apply(style => style with { Color = color });
    }

    /// <summary>選択中の範囲へ書式を適用し、表示とフォーカスを戻す。</summary>
    /// <param name="change">書式を新しい値へ変える処理。</param>
    /// <remarks>
    /// <b>選択が無いときは以降の入力へ回す</b>（判定はレイヤーが単一所有するので、ここでは分けない）。
    /// </remarks>
    private void Apply(Func<TextStyle, TextStyle> change)
    {
        _context.Transform(change);
        Refresh();

        // バーの操作でフォーカスが移るため、本文へ戻して続けて入力できるようにする
        _requestFocus?.Invoke();
    }

    /// <summary>装飾を切り替える（適用先の先頭の書式で切り替えの向きを決める）。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    private void ToggleDecoration(
        Func<TextStyle, bool> read,
        Func<TextStyle, bool, TextStyle> write)
    {
        if (_isRefreshing)
        {
            return;
        }

        Apply(style => write(style, !read(_context.SelectedTextStyle)));
    }
}
