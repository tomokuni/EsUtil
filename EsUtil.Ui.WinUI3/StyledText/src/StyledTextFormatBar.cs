using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Ui.WinUI3.StyledText;

/// <summary>本文の書式（文字単位）を操作するバー（WinUI 3 版）。</summary>
/// <remarks>
/// <b>適用先は選択範囲</b>: 選んだ書式は選択範囲へ適用し、選択が無いときは以降の入力へ回す
/// （既存の文字は変えない。 判定はレイヤーが単一所有する）。<br/>
/// <b>色は色選択コントロールを使う</b>: 代表色と ColorPicker から選ぶ（自前の色入力を作らない）。
/// </remarks>
public sealed partial class StyledTextFormatBar : ContentControl, IStyledTextFormatBar
{
    /// <summary>適用先のレイヤー。</summary>
    private readonly ITextFormatBarContext _context;

    /// <summary>バーの構成（フォントの一覧・色の選択）。</summary>
    private readonly StyledTextFormatBarOptions _options;

    /// <summary>書式バーに出す項目の組み合わせ。</summary>
    private readonly TextFormatBarItemVisibility _items;

    /// <summary>選べるフォント名の一覧。</summary>
    private readonly IReadOnlyList<string> _fonts;

    /// <summary>太字の切り替え。</summary>
    private readonly ToggleButton _bold;

    /// <summary>斜体の切り替え。</summary>
    private readonly ToggleButton _italic;

    /// <summary>下線の切り替え。</summary>
    private readonly ToggleButton _underline;

    /// <summary>取り消し線の切り替え。</summary>
    private readonly ToggleButton _strike;

    /// <summary>フォントの選択。</summary>
    private readonly ComboBox _font;

    /// <summary>文字サイズの入力。</summary>
    private readonly NumberBox _size;

    /// <summary>色の入力。</summary>
    private readonly EsUtil.Ui.WinUI3.ColorSelector.ColorSelectorControl _color;

    /// <summary>サイズの入力欄を上下中央へ寄せるために内側の入力欄で調整する量（DIP）。</summary>
    /// <remarks>
    /// <b>NumberBox は内側の文字を上寄りに描く</b>。
    /// <b>枠は内側の入力欄が描く</b>ため、入力欄そのものを動かすと枠もずれる。
    /// 枠を他の項目と揃えたまま文字だけを中央へ寄せるには、内側の余白と縦の寄せで調整します。
    /// </remarks>
    private const double NumberBoxTextTrim = 2.4;

    /// <summary>色の入力欄の幅（DIP）。</summary>
    /// <remarks>
    /// <b>幅はコアが単一所有する</b>: 色名とカラーコードの表記の長さから決める（UI ごとに決めると
    /// 同じ表記でもバーの長さが変わる）。
    /// </remarks>
    private static readonly double ColorSelectorWidth =
        TextFormatBarLayout.ResolveColorSelectorWidth(RichTextMetrics.Measure);

    /// <summary>通知による更新中かどうか（利用者の操作と区別する）。</summary>
    private bool _refreshing;

    /// <summary>書式バーを初期化する。</summary>
    /// <param name="context">書式を読み書きする相手（ホストが実装する）。</param>
    /// <param name="options">バーの構成（フォントの一覧・色の選択）。 省略すると既定の構成を使う。</param>
    public StyledTextFormatBar(ITextFormatBarContext context, StyledTextFormatBarOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _options = options ?? new StyledTextFormatBarOptions();
        _fonts = _options.Fonts;
        _items = _options.Items;

        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,

            // 高さの違う項目が混ざっても縦位置を揃える（規則の所有者はコアの ItemHeight）
            VerticalAlignment = VerticalAlignment.Center,
        };
        _bold = StyledTextFormatBarControls.Toggle("B", false, value => ApplyDecoration(style => style with { IsBold = value }), bold: true);
        _italic = StyledTextFormatBarControls.Toggle("I", false, value => ApplyDecoration(style => style with { IsItalic = value }), italic: true);
        _underline = StyledTextFormatBarControls.Toggle(
            "U", false, value => ApplyDecoration(style => style with { IsUnderline = value }), underline: true);
        _strike = StyledTextFormatBarControls.Toggle(
            "S", false, value => ApplyDecoration(style => style with { IsStrike = value }), strike: true);

        _font = new ComboBox
        {
            ItemsSource = _fonts,
            SelectedIndex = 0,
            Width = TextFormatBarLayout.FontSelectorWidth,

            // **フォント名は直接入力できる**（一覧に無いフォントも指定できる。 標準の編集可の
            // コンボボックスであるため Ctrl+C / Ctrl+V もそのまま使える）
            IsEditable = true,
            IsTextSearchEnabled = true,

            // **高さを明示する**: 既定の高さでは文字の上が欠ける
            MinHeight = TextFormatBarLayout.ItemHeight,
            Height = TextFormatBarLayout.ItemHeight,
            FontSize = StyledTextFormatBarControls.FontSize,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        _font.SelectionChanged += (_, _) =>
        {
            if (_refreshing || _font.SelectedItem is not string family)
            {
                return;
            }

            ApplyFull(style => style with { FontFamily = family });
        };

        // 入力したフォント名は Enter とフォーカスを外したときに適用する（1 文字ごとに適用しない）
        _font.TextSubmitted += (_, e) =>
        {
            if (!_refreshing && !string.IsNullOrWhiteSpace(e.Text))
            {
                ApplyFull(style => style with { FontFamily = e.Text });
            }
        };
        _font.LostFocus += (_, _) =>
        {
            var family = _font.Text;
            if (!_refreshing && !string.IsNullOrWhiteSpace(family))
            {
                ApplyFull(style => style with { FontFamily = family });
            }
        };

        _size = new NumberBox
        {
            Minimum = TextLimits.FontSizeMin,
            Maximum = TextLimits.FontSizeMax,
            SmallChange = 1,
            Value = TextDefaults.FontSizeDefault,
            Width = TextFormatBarLayout.SizeSelectorWidth,

            // **枠は動かさない**: NumberBox 自体を動かすと枠もずれる。
            // 内側の文字は Loaded 後に `ShiftInnerText` で下げる。
            MinHeight = TextFormatBarLayout.ItemHeight,
            Height = TextFormatBarLayout.ItemHeight,

            // **数字は上下中央へ置く**: NumberBox は内側の入力欄を上寄りに描き、`VerticalContentAlignment`
            // を受け付けない。 枠は他の項目と同じ高さにし、内側の入力欄だけを下げる。
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = StyledTextFormatBarControls.FontSize,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };

        _size.Loaded += (_, _) => ShiftInnerText(_size, NumberBoxTextTrim);

        _size.ValueChanged += (_, e) =>
        {
            if (_refreshing || double.IsNaN(e.NewValue))
            {
                return;
            }

            ApplyFull(style => style with { FontSize = e.NewValue });
        };

        _color = (_options.CreateColorSelector ?? CreateDefaultSelector)(
            TextDefaults.DefaultStyle.Color.ToHexText(), ColorTextFormat.NameWithAlpha);
        _color.SelectedColorChanged += (_, _) =>
        {
            if (_refreshing)
            {
                return;
            }

            var color = ParseColor(_color.SelectedColor);
            ApplyFull(style => style with { Color = color });
        };

        // 並び順はコア（TextFormatBarLayout.Order）が単一所有する。
        // ここで順を決めると 4 つの UI 版で操作の位置がずれる。
        var items = new Dictionary<TextFormatBarItem, UIElement>
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
            var element = (FrameworkElement)items[item];
            element.Height = TextFormatBarLayout.ItemHeight;
            element.MinHeight = TextFormatBarLayout.ItemHeight;
            element.VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(element);
        }

        // 枠・余白・下地はコアの外観（AnnotationPanelAppearance）が単一所有する。
        // ここで持つと、パネルの枠と二重になって「余計な辺枠」に見える。
        Content = panel;

        // **キーはバーへ集める**: バーへフォーカスが移っても本文のキー操作（矢印・書式の切り替え）を保つ
        KeyDown += OnBarKeyDown;

        Refresh();
    }

    /// <summary>バーの画面要素を取得する（ホストはこれをパネルへ載せる）。</summary>
    public FrameworkElement View => this;

    /// <summary>バーの幅（DIP）を取得する。</summary>
    /// <remarks>出す項目だけで求める（規則はコアの <see cref="TextFormatBarLayout.ResolveWidth"/> が単一所有する）。</remarks>
    public double PreferredWidth => TextFormatBarLayout.ResolveWidth(_items, ColorSelectorWidth);

    /// <summary>本文のキー操作を転送する処理を設定する。</summary>
    /// <param name="forward">転送する処理（キーの引数を受け取る。<see langword="null"/> は転送しない）。</param>
    public void SetKeyHandler(Func<KeyRoutedEventArgs, bool>? forward) => _forwardKey = forward;

    /// <summary>本文へフォーカスを戻す処理を設定する。</summary>
    /// <param name="request">フォーカスを戻す処理（<see langword="null"/> は何もしない）。</param>
    public void SetFocusRequest(Action? request) => _requestFocus = request;

    /// <summary>本文のキー操作を転送する処理（ホストが設定する）。</summary>
    private Func<KeyRoutedEventArgs, bool>? _forwardKey;

    /// <summary>本文へフォーカスを戻す処理（ホストが設定する）。</summary>
    private Action? _requestFocus;

    /// <summary>バーへ届いたキーを本文へ転送する（入力欄の内側では転送しない）。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">キーの引数。</param>
    /// <remarks>
    /// <b>入力欄の内側では転送しない</b>: 数値・色の入力欄は自分のキー（矢印・文字）を使う。
    /// 転送すると入力欄の編集が壊れる。
    /// </remarks>
    private void OnBarKeyDown(object sender, KeyRoutedEventArgs e)
    {
        _ = sender;

        if (e.Handled || _forwardKey is not { } forward)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source
            && (IsInside(source, _size) || IsInside(source, _font) || IsInside(source, _color)))
        {
            return;
        }

        if (forward(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>要素が指定した祖先の内側にあるかどうかを判定する。</summary>
    /// <param name="element">判定する要素。</param>
    /// <param name="ancestor">祖先。</param>
    /// <returns>内側にある場合は <see langword="true"/>。</returns>
    private static bool IsInside(DependencyObject element, DependencyObject ancestor)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>既定の色の選択を作る（標準のタブだけを出す）。</summary>
    /// <param name="color">現在の色（<c>#AARRGGBB</c>）。</param>
    /// <param name="textFormat">入力欄の表記。</param>
    /// <returns>色の選択。</returns>
    private static EsUtil.Ui.WinUI3.ColorSelector.ColorSelectorControl CreateDefaultSelector(
        string color, EsUtil.Platform.Core.Palette.ColorTextFormat textFormat)
        => new()
        {
            SelectedColor = color,
            TextFormat = textFormat,
            Width = ColorSelectorWidth,
            FontSize = StyledTextFormatBarControls.FontSize,
        };

    /// <summary>書式バーの色の入力欄へ出す表記を取得または設定する。</summary>
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

    /// <summary>現在の選択範囲の書式を各操作へ反映する。</summary>
    public void Refresh()
    {
        _refreshing = true;
        try
        {
            var style = _context.SelectedTextStyle;
            _bold.IsChecked = _context.SelectedTextMatches(static candidate => candidate.IsBold);
            _italic.IsChecked = _context.SelectedTextMatches(static candidate => candidate.IsItalic);
            _underline.IsChecked = _context.SelectedTextMatches(static candidate => candidate.IsUnderline);
            _strike.IsChecked = _context.SelectedTextMatches(static candidate => candidate.IsStrike);

            var index = _fonts.ToList().IndexOf(style.FontFamily);
            _font.SelectedIndex = index >= 0 ? index : 0;
            _size.Value = style.FontSize;
            _color.SelectedColor = style.Color.ToHexText();
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>装飾を切り替える（選択範囲の本文へ適用する）。</summary>
    /// <param name="applyToModel">本文へ適用する処理。</param>
    private void ApplyDecoration(Func<TextStyle, TextStyle> applyToModel)
    {
        _context.Transform(applyToModel);
        Refresh();

        // バーの操作でフォーカスが移るため、本文へ戻して続けて入力できるようにする
        _requestFocus?.Invoke();
    }

    /// <summary>書式を丸ごと適用する（色・大きさ・フォント）。</summary>
    /// <param name="change">書式を変える処理。</param>
    private void ApplyFull(Func<TextStyle, TextStyle> change)
    {
        _context.Apply(change(_context.SelectedTextStyle));
        Refresh();

        // バーの操作でフォーカスが移るため、本文へ戻して続けて入力できるようにする
        _requestFocus?.Invoke();
    }

    /// <summary>保存値（<c>#AARRGGBB</c>）を色へ変換する。</summary>
    /// <param name="text">保存値。</param>
    /// <returns>色。 解釈できない場合は既定の文字色。</returns>
    /// <remarks>文字列の解釈は <see cref="DrawingColorExtensions.TryParseHexText"/> が単一所有する（自前で桁数を数えない）。</remarks>
    private static DrawingColor ParseColor(string? text)
        => DrawingColorExtensions.TryParseHexText(text, out var color) ? color : TextDefaults.DefaultStyle.Color;

    /// <summary>要素の内側にある入力欄の文字を上下中央へ置く。</summary>
    /// <param name="root">対象の要素。</param>
    /// <param name="trim">文字を下げる量（DIP）。</param>
    /// <remarks>
    /// <b>枠は内側の入力欄（<see cref="TextBox"/>）が描く</b>ため、入力欄を動かすと枠も一緒にずれる。
    /// 枠を他の項目と揃えたまま文字だけを下げるには、内側の余白を増やします。
    /// </remarks>
    private static void ShiftInnerText(DependencyObject root, double trim)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox box)
            {
                var padding = box.Padding;
                box.Padding = new Thickness(padding.Left, padding.Top + trim, padding.Right, padding.Bottom);
                box.VerticalContentAlignment = VerticalAlignment.Center;
                return;
            }

            ShiftInnerText(child, trim);
        }
    }
}
