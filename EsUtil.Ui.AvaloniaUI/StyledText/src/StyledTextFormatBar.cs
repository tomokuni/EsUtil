using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.AvaloniaUI.ColorSelector;
using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Ui.AvaloniaUI.StyledText;

/// <summary>本文を編集中に本文の上へ出す書式バー（文字単位の書式）。</summary>
/// <remarks>
/// <b>本文はコントロールが直接編集する</b>ため、このバーは「選択中の範囲へ書式を適用する」だけを行う。
/// 適用しても編集は続き、結果はその場で本文へ反映される。<br/>
/// <b>項目名は出さない</b>: 幅が限られるため、意味はツールチップで示す（フォント名・大きさ・装飾名とショートカット）。<br/>
/// <b>同じ値でも適用できる</b>: 文字単位で書式が混ざっている範囲を一律に揃えるため、選び直しただけでも適用する。<br/>
/// <b>文字を選んでいない間は操作できない</b>: 適用先が決まらないため、書式の操作をすべて無効にする。<br/>
/// <b>大きさは中身に合わせる</b>: 説明の行と「完了」は置かず、書式の操作だけを 1 行に並べる（編集の邪魔をしない）。
/// </remarks>
public sealed class StyledTextFormatBar : Border, IStyledTextFormatBar
{
    /// <summary>バーのおおよその高さ（DIP。本文の上へ置く位置の計算に使う）。</summary>
    internal const double EstimatedHeight = 40;

    /// <summary>取り消し線のショートカット（一般的な表記に合わせる）。</summary>
    internal const string StrikeShortcut = "Alt+Shift+5";

    /// <summary>色の幅を測れなかったときの幅（DIP。 実測値の近似）。</summary>
    private const double ColorSelectorWidthFallback = 96;

    /// <summary>書式を読み書きする相手（ホストが渡す）。</summary>
    private readonly ITextFormatBarContext _context;

    /// <summary>バーの構成（フォントの一覧・色のタブ・色の表記）。</summary>
    private readonly StyledTextFormatBarOptions _options;

    /// <summary>選べるフォント名の一覧。</summary>
    private readonly IReadOnlyList<string> _fonts;

    /// <summary>フォントの選択。</summary>
    private readonly ComboBox _font;

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

    /// <summary>書式バーに出す項目の組み合わせ。</summary>
    private readonly TextFormatBarItemVisibility _items;

    /// <summary>色の選択の幅（DIP。 <see cref="ResolveColorSelectorWidth"/> が決める）。</summary>
    private readonly double _colorWidth;

    /// <summary>バーの画面要素を取得する（ホストはこれをパネルへ載せる）。</summary>
    public Control View => this;

    /// <summary>バーの幅（DIP。 置く位置の計算に使う）。</summary>
    /// <remarks>出す項目だけで求める（出さない項目の幅を足さない。 規則はコアの <see cref="TextFormatBarLayout.ResolveWidth"/> が単一所有する）。</remarks>
    public double PreferredWidth => TextFormatBarLayout.ResolveWidth(_items, _colorWidth);

    /// <summary>本文のキー操作を転送する処理を取得または設定する。</summary>
    private Func<Key, KeyModifiers, bool>? _forwardKey;

    /// <summary>本文へフォーカスを戻す処理を取得または設定する。</summary>
    private Action? _requestFocus;

    /// <summary>本文のキー操作を転送する処理を設定する。</summary>
    /// <param name="forward">転送する処理（<see langword="null"/> は転送しない）。</param>
    public void SetKeyHandler(Func<Key, KeyModifiers, bool>? forward) => _forwardKey = forward;

    /// <summary>本文へフォーカスを戻す処理を設定する。</summary>
    /// <param name="request">フォーカスを戻す処理（<see langword="null"/> は何もしない）。</param>
    public void SetFocusRequest(Action? request) => _requestFocus = request;

    /// <summary>書式バーを初期化する。</summary>
    /// <param name="context">書式を読み書きする相手（ホストが実装する）。</param>
    /// <param name="options">バーの構成（フォントの一覧・色のタブ）。 省略すると既定の構成を使う。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    public StyledTextFormatBar(ITextFormatBarContext context, StyledTextFormatBarOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _options = options ?? new StyledTextFormatBarOptions();
        _fonts = _options.Fonts;
        _items = _options.Items;
        _colorWidth = ResolveColorSelectorWidth(_options.MeasureText);
        // バーは本文と同じ画面の子として重ねるが、バーへフォーカスが移ると本文のキー操作が止まる。
        // 本文の編集キー（カーソル移動・Shift+矢印の選択など）は本文へ転送する。
        AddHandler(KeyDownEvent, OnFormatBarKeyForward, RoutingStrategies.Tunnel);

        var fonts = _fonts;

        // **フォント名は直接入力できる**（一覧に無いフォントも指定できる。 入力した文字は
        // そのままフォント名として適用する）。 入力欄は標準の編集可のコンボボックスであるため、
        // Ctrl+C / Ctrl+V もそのまま使える。
        _font = new ComboBox
        {
            Width = FontSelectorWidth,
            ItemsSource = fonts,
            SelectedIndex = 0,
            IsEditable = true,
        };
        _font.SelectionChanged += (_, _) => ApplyFont();

        // 同じ値を選び直したときは SelectionChanged が来ないため、閉じたときにも適用する
        _font.DropDownClosed += (_, _) => ApplyFont();

        // 入力したフォント名は Enter とフォーカスを外したときに適用する（1 文字ごとに適用しない）
        _font.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ApplyFont();
                e.Handled = true;
            }
        };
        _font.LostFocus += (_, _) => ApplyFont();
        ToolTip.SetTip(_font, "フォント（直接入力できます）");

        _size = new NumericUpDown
        {
            Width = SizeSelectorWidth,
            Minimum = TextLimits.FontSizeMinDecimal,
            Maximum = TextLimits.FontSizeMaxDecimal,
            Increment = 1m,
            FormatString = "0.#",
        };
        _size.ValueChanged += (_, _) => ApplySize();
        ToolTip.SetTip(_size, "大きさ");

        _color = (_options.CreateColorInput ?? CreateDefaultColorInput)(
            TextDefaults.DefaultStyle.Color.ToHexText(), _options.TextFormat);

        // **幅はバーが決める**: 置く位置の計算（PreferredWidth）と同じ幅にする（主の幅が違うと切れる）
        _color.Width = _colorWidth;
        _color.HighlightBorderOnError = true;
        _color.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorSelectorControl.SelectedColorProperty)
            {
                ApplyColor();
            }
        };

        // 同じ色を選び直したときは値が変わらず変更通知が出ないため、一覧・カラーピッカーの確定でも適用する
        _color.ColorApplied += (_, _) => ApplyColor();

        // 色の入力欄から離れたら、値が変わっていなくても適用する
        _color.LostFocus += (_, _) => ApplyColor();
        ToolTip.SetTip(_color, "色");

        _bold = Toggle("B", "太字（Ctrl+B）", bold: true);
        _italic = Toggle("I", "斜体（Ctrl+I）", italic: true);
        _underline = Toggle("U", "下線（Ctrl+U）", underline: true);
        _strike = Toggle("S", $"取り消し線（{StrikeShortcut}）", strike: true);

        _bold.Click += (_, _) => ToggleDecoration(style => style.IsBold, (style, value) => style with { IsBold = value });
        _italic.Click += (_, _) => ToggleDecoration(style => style.IsItalic, (style, value) => style with { IsItalic = value });
        _underline.Click += (_, _) => ToggleDecoration(style => style.IsUnderline, (style, value) => style with { IsUnderline = value });
        _strike.Click += (_, _) => ToggleDecoration(style => style.IsStrike, (style, value) => style with { IsStrike = value });

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = ControlSpacing,

            // 高さの違う項目が混ざっても縦位置を揃える（規則の所有者はコアの ItemHeight）
            VerticalAlignment = VerticalAlignment.Center,
        };

        // 並び順はコア（TextFormatBarLayout.Order）が単一所有する。
        // ここで順を決めると 4 つの UI 版で操作の位置がずれる。
        var items = new Dictionary<TextFormatBarItem, Control>
        {
            [TextFormatBarItem.Bold] = _bold,
            [TextFormatBarItem.Italic] = _italic,
            [TextFormatBarItem.Underline] = _underline,
            [TextFormatBarItem.Strike] = _strike,
            [TextFormatBarItem.Font] = _font,
            [TextFormatBarItem.Size] = _size,
            [TextFormatBarItem.Color] = _color,
        };

        // **出さない項目は場所ごと詰める**: 空の枠を残すと、押せない余白が挟まって間延びしたバーになる
        foreach (var item in _items.VisibleItems)
        {
            // **高さは項目ごとに違ってはいけない**: 揃えないと縦位置がずれ、押す位置も変わる。
            // 値は提示層の TextFormatBarLayout.ItemHeight が単一所有する
            var control = items[item];
            control.Height = TextFormatBarLayout.ItemHeight;
            control.MinHeight = TextFormatBarLayout.ItemHeight;
            control.VerticalAlignment = VerticalAlignment.Center;
            controls.Children.Add(control);
        }

        // 枠・余白・下地はコアの外観（AnnotationPanelAppearance）が単一所有する。
        // ここで持つと、パネルの枠と二重になって「余計な辺枠」に見える。
        Child = controls;

        // 大きさと色は Enter で確定する（同じ値でも適用する）
        AddHandler(KeyDownEvent, OnBarKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>フォント名の選択の幅（DIP。 値はコアが単一所有する）。</summary>
    internal const double FontSelectorWidth = TextFormatBarLayout.FontSelectorWidth;

    /// <summary>フォントの大きさの入力の幅（DIP。 値はコアが単一所有する）。</summary>
    internal const double SizeSelectorWidth = TextFormatBarLayout.SizeSelectorWidth;

    /// <summary>操作の間の余白（DIP）。</summary>
    private const double ControlSpacing = TextEditorLayout.ControlSpacing;

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

    /// <summary>色の選択の幅を求める（「■ + 全角 4 文字 + 100%」と「■ + #FFFFFFFF」の広いほう）。</summary>
    /// <returns>幅（DIP）。 測れない場合は <see cref="ColorSelectorWidthFallback"/>。</returns>
    /// <remarks>
    /// <b>広いほうに合わせる</b>: 代表色で最も長い色名は全角 4 文字（「薄い灰色」）で、不透明度が付くと
    /// <c>+100%</c> が加わる。 16 進の入力（<c>#FFFFFFFF</c>）はさらに長いため、
    /// 両方の実寸を測って広いほうへ合わせる（どちらの表記でも文字が切れない）。
    /// </remarks>
    internal static double ResolveColorSelectorWidth(Func<string, TextStyle, EsUtil.Platform.Core.Geometry.EsSize>? measureText)
    {
        // **幅の規則はコアが単一所有する**: UI ごとに四角の余白・展開ボタンの幅を決めると、
        // 同じ表記でも幅が変わり、はっきり余る幅が空く
        var width = TextFormatBarLayout.ResolveColorSelectorWidth(measureText ?? RichTextPainter.Measure);
        return width > 0 ? width : ColorSelectorWidthFallback;
    }

    /// <summary>既定の色の入力を作る（標準のタブだけの色選択）。</summary>
    /// <param name="color">現在の色（<c>#AARRGGBB</c>）。</param>
    /// <param name="textFormat">入力欄の表記。</param>
    /// <returns>色の選択。</returns>
    private ColorSelectorControl CreateDefaultColorInput(string color, ColorTextFormat textFormat)
        => new()
        {
            SelectedColor = color,
            TextFormat = textFormat,
            Width = _colorWidth,
            HighlightBorderOnError = true,
        };

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
            _font.Text = style.FontFamily;
            _size.Value = (decimal)style.FontSize;
            _color.SelectedColor = style.Color.ToHexText();

            // 装飾は「選択範囲のすべてが同じ属性か」で押下状態を決める（一部だけ太字の範囲で押されて見えないように）
            _bold.IsChecked = _context.SelectedTextMatches(textStyle => textStyle.IsBold);
            _italic.IsChecked = _context.SelectedTextMatches(textStyle => textStyle.IsItalic);
            _underline.IsChecked = _context.SelectedTextMatches(textStyle => textStyle.IsUnderline);
            _strike.IsChecked = _context.SelectedTextMatches(textStyle => textStyle.IsStrike);

            // 幅が狭いとフォント名・大きさが読めないため、現在値をツールチップにも出す
            ToolTip.SetTip(_font, style.FontFamily);
            ToolTip.SetTip(_size, $"{style.FontSize:0.#}");
        }
        finally
        {
            _isRefreshing = false;
        }

        InvalidateMeasure();
    }

    /// <summary>書式バーへ届いた本文の編集キーを本文へ転送する（カーソル移動・選択の変更を保つ）。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">キーの引数。</param>
    /// <remarks>
    /// <b>入力欄の中では転送しない</b>: 色の入力欄やフォント名の入力欄にフォーカスがあるときは、
    /// <c>Ctrl+C</c> / <c>Ctrl+V</c> や文字入力をその入力欄へ渡します。 転送すると本文のコピー・貼り付けが
    /// 走り、入力欄へ貼り付けられません。
    /// </remarks>
    private void OnFormatBarKeyForward(object? sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.Source is Visual source
            && (IsInside(source, _color) || IsInside(source, _size) || IsInside(source, _font)))
        {
            return;
        }

        // **本文のキー操作は本文コントロールが単一所有する**: バーがフォーカスを持った状態でも、
        // 矢印・選択・書式の切り替えを同じ規則で適用する。
        if (_forwardKey?.Invoke(e.Key, e.KeyModifiers) == true)
        {
            e.Handled = true;
        }
    }

    /// <summary>バーの中で Enter が押されたら、大きさと色を今の値で適用する。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnBarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.Source is not Visual source || !_context.HasSelection)
        {
            return;
        }

        if (IsInside(source, _color))
        {
            ApplyColor();
            RestoreFocusAfterInput();
            e.Handled = true;
        }
        else if (IsInside(source, _size))
        {
            ApplySize();
            RestoreFocusAfterInput();
            e.Handled = true;
        }
    }

    /// <summary>指定された要素が指定された祖先の内側にあるかどうかを判定する。</summary>
    /// <param name="visual">判定する要素。</param>
    /// <param name="ancestor">祖先。</param>
    /// <returns>内側にある場合は <see langword="true"/>。</returns>
    private static bool IsInside(Visual visual, Visual ancestor)
    {
        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>選択中の範囲へフォントを適用する（同じ値でも適用する）。</summary>
    private void ApplyFont()
    {
        if (_isRefreshing)
        {
            return;
        }

        // 一覧から選んだ値でも、直接入力した値でも同じように適用する
        var family = string.IsNullOrWhiteSpace(_font.Text)
            ? _font.SelectedIndex >= 0 ? _fonts[_font.SelectedIndex] : null
            : _font.Text;

        if (string.IsNullOrWhiteSpace(family))
        {
            return;
        }

        Apply(style => style with { FontFamily = family }, restoreFocus: false);
        ToolTip.SetTip(_font, family);
    }

    /// <summary>選択中の範囲へ大きさを適用する（同じ値でも適用する）。</summary>
    private void ApplySize()
    {
        if (_isRefreshing || _size.Value is not { } value)
        {
            return;
        }

        Apply(style => style with { FontSize = (double)value }, restoreFocus: false);
        ToolTip.SetTip(_size, $"{value:0.#}");
    }

    /// <summary>選択中の範囲へ色を適用する（同じ値でも適用する）。</summary>
    private void ApplyColor()
    {
        if (_isRefreshing
            || !EsUtil.Platform.Core.Imaging.DrawingColorExtensions.TryParseHexText(_color.SelectedColor, out var color))
        {
            return;
        }

        Apply(style => style with { Color = color }, restoreFocus: false);
    }

    /// <summary>選択中の範囲へ書式を適用し、表示を更新する。</summary>
    /// <param name="change">書式を新しい値へ変える処理（変えない属性は <c>TransformStyle</c> が保つ）。</param>
    /// <param name="restoreFocus">適用の後に本文へフォーカスを戻すか（入力欄からの適用では <see langword="false"/>）。</param>
    /// <remarks>
    /// <b>選択が無いときは以降の入力へ回す</b>（判定はレイヤーが単一所有するので、ここでは分けない）。<br/>
    /// <b>入力欄からの適用ではフォーカスを戻さない</b>: 入力の途中で本文へ戻すと、その後に打った文字が
    /// 本文へ入る（色の入力欄で実測）。 戻すのは押したボタンのように操作が終わっている場合と、
    /// <c>Enter</c> の処理が終わった後（<see cref="RestoreFocusAfterInput"/>）だけにする。
    /// </remarks>
    private void Apply(Func<TextStyle, TextStyle> change, bool restoreFocus = true)
    {
        _context.TransformStyle(change);
        Refresh();

        if (restoreFocus)
        {
            _requestFocus?.Invoke();
        }
    }

    /// <summary>本文へフォーカスを戻す（今のキー入力を処理し終えてから戻す）。</summary>
    /// <remarks>
    /// <b>同じキーの文字入力を残さない</b>: <c>Enter</c> はキーの通知の後に文字（改行）の通知が続く。
    /// 通知の途中で本文へ戻すと、その改行が本文へ入る（大きさの入力欄で実測）。
    /// </remarks>
    private void RestoreFocusAfterInput()
    {
        if (_requestFocus is { } request)
        {
            Dispatcher.UIThread.Post(request, DispatcherPriority.Background);
        }
    }

    /// <summary>装飾の切り替えボタンを作る。</summary>
    /// <param name="text">文言。</param>
    /// <param name="toolTip">ツールチップ（装飾名とショートカット）。</param>
    /// <param name="bold">太字で表示するかどうか。</param>
    /// <param name="italic">斜体で表示するかどうか。</param>
    /// <param name="underline">下線を引いて表示するかどうか。</param>
    /// <param name="strike">取り消し線を引いて表示するかどうか。</param>
    /// <returns>切り替えボタン。</returns>
    private static ToggleButton Toggle(
        string text, string toolTip, bool bold = false, bool italic = false, bool underline = false, bool strike = false)
    {
        var content = new TextBlock
        {
            Text = text,

            // **記号は行の文字より一回り大きくする**: 記号 1 文字だけのボタンは、行の他の文字と同じ
            // 大きさだと線（下線・取り消し線）と字面が小さく、押せる場所としても見えにくい
            FontSize = TextEditorLayout.ToggleFontSize,
            FontFamily = new FontFamily(TextEditorLayout.ToggleFontFamily),
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,

            // **上下中央へ寄せる**: Consolas の記号は行の下寄りに描かれるため、そのままでは
            // 上へ 2 DIP ずれて見える。 位置は描画だけを動かす（レイアウトは変えない）。
            RenderTransform = new TranslateTransform(0, 2),
        };

        if (underline)
        {
            content.TextDecorations = TextDecorations.Underline;
        }

        if (strike)
        {
            content.TextDecorations = TextDecorations.Strikethrough;
        }

        var button = new ToggleButton
        {
            Content = content,
            Width = ToggleWidth,

            // 高さはコア（TextFormatBarLayout）が単一所有する。 項目ごとに高さが違うと縦位置が揃わない
            Height = TextFormatBarLayout.ItemHeight,
            MinHeight = TextFormatBarLayout.ItemHeight,
            Padding = new Avalonia.Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, toolTip);
        return button;
    }

    /// <summary>装飾の切り替えボタンの幅（DIP）。</summary>
    private const double ToggleWidth = TextEditorLayout.ToggleWidth;

    /// <summary>装飾を切り替える（適用先の先頭の書式で切り替えの向きを決める）。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    private void ToggleDecoration(
        Func<TextStyle, bool> read,
        Func<TextStyle, bool, TextStyle> write)
        => Apply(style => write(style, !read(_context.SelectedTextStyle)));
}
