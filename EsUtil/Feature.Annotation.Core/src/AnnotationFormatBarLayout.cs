namespace EsUtil.Feature.Annotation;

/// <summary>書式バーの項目の並び順と寸法（単一所有）。</summary>
/// <remarks>
/// <b>装飾を先・値を後</b>: 太字・斜体・下線・取り消し線（押すだけの操作）を先に置き、
/// 色・大きさ・フォント名（選ぶ操作）を後に置く。 操作の種類がまとまり、視線の移動が少なくなる。
/// <b>項目の高さは揃える</b>: 高さが違うと縦位置が不揃いになり、押す位置もずれる（実測 2026-11-21）。
/// </remarks>
public static class AnnotationFormatBarLayout
{
    /// <summary>項目の並び順を取得する。</summary>
    /// <value>太字・斜体・下線・取り消し線・色・フォントの大きさ・フォント名 の順。</value>
    public static IReadOnlyList<AnnotationFormatBarItem> Order { get; } =
    [
        AnnotationFormatBarItem.Bold,
        AnnotationFormatBarItem.Italic,
        AnnotationFormatBarItem.Underline,
        AnnotationFormatBarItem.Strike,
        AnnotationFormatBarItem.Color,
        AnnotationFormatBarItem.Size,
        AnnotationFormatBarItem.Font,
    ];

    /// <summary>フォント名の選択の幅（DIP）。</summary>
    /// <remarks>
    /// <b>幅は 4 つの UI 版で同じにする</b>: UI ごとに違うと、同じ内容でも書式バーの長さが変わり、
    /// 置ける幅を超えて項目が切れます（実測 2026-11-21: WinUI 3 版だけ 130 DIP で文字が切れました）。
    /// </remarks>
    public const double FontSelectorWidth = 210;

    /// <summary>フォントの大きさの入力の幅（DIP）。</summary>
    public const double SizeSelectorWidth = 104;

    /// <summary>色の入力欄で「現在色の四角」に空ける左の余白（DIP）。</summary>
    private const double ColorSwatchIndent = 24;

    /// <summary>色の入力欄の展開ボタンの幅（DIP）。</summary>
    private const double ColorToggleWidth = 34;

    /// <summary>色の幅を測る文字サイズ（DIP）。</summary>
    private const double ColorMeasureFontSize = 12;

    /// <summary>色の入力欄の幅（DIP）を求める。</summary>
    /// <param name="measureText">文字列と書式から実寸を測る関数（UI が渡す）。</param>
    /// <returns>幅（DIP）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// <b>必要な幅は表記から決める</b>: 「■ + 色名 + 100%」と「■ + <c>#FFFFFFFF</c>」の広いほうへ
    /// 四角と展開ボタンの分を足します。 固定値（240 DIP）にすると、はっきり余る幅が空いて
    /// 「不必要に長い」見た目になります（実測 2026-11-22: WinUI 3 版だけ 240 DIP でした）。
    /// </remarks>
    public static double ResolveColorSelectorWidth(
        Func<string, AnnotationTextStyle, EsUtil.Platform.Core.Geometry.EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);

        var style = AnnotationDefaults.TextStyle with { FontSize = ColorMeasureFontSize };
        var nameWidth = measureText("薄い灰色+100%", style).Width;
        var hexWidth = measureText(HexTextForMeasure, style).Width;
        return Math.Ceiling(ColorSwatchIndent + Math.Max(nameWidth, hexWidth) + ColorToggleWidth);
    }

    /// <summary>色の入力欄の幅（DIP）を求める（カラーコードだけを収める）。</summary>
    /// <param name="measureText">文字列と書式から実寸を測る関数（UI が渡す）。</param>
    /// <returns>幅（DIP）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// <b>フライアウトの行はカラーコードの幅で足りる</b>: 行の他の入力欄（数値・選択肢）と同じ幅に揃えると
    /// 1 列の見た目が整います。 色名まで収める幅にすると、色名は数文字しかないのに
    /// <b>右側が大きく空きます</b>（実測 2026-11-22: 右クリックのフライアウトが「不必要に長い」との指摘）。
    /// <c>#FFFFFFFF</c> は入力欄に出る最も長い表記であるため、これが収まれば足ります。
    /// </remarks>
    public static double ResolveHexColorSelectorWidth(
        Func<string, AnnotationTextStyle, EsUtil.Platform.Core.Geometry.EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);

        var style = AnnotationDefaults.TextStyle with { FontSize = ColorMeasureFontSize };
        var hexWidth = measureText(HexTextForMeasure, style).Width;
        return Math.Ceiling(ColorSwatchIndent + hexWidth + ColorToggleWidth + HexTextSlack);
    }

    /// <summary>色の入力欄の幅を測るための最も長い表記（<c>#AARRGGBB</c>）。</summary>
    private const string HexTextForMeasure = "#FFFFFFFF";

    /// <summary>色の入力欄の内側の余白の見込み（DIP）。</summary>
    /// <remarks>
    /// <b>文字の実寸だけでは足りない</b>: 入力欄は文字の左右に内側の余白を持ち、文字の実寸は
    /// その外側の幅です。 余白を見込まないと、<c>#FFFFFFFF</c> の末尾が切れます
    /// （実測 2026-11-22: WinUI 3 版で 8 桁のコードが見切れた）。 実測の近似（<c>文字数 × サイズ × 0.5</c>）を
    /// 使う UI もあるため、余裕を持たせます。
    /// </remarks>
    private const double HexTextSlack = 14;

    /// <summary>項目の高さ（DIP）を取得する。</summary>
    /// <value>32 DIP。</value>
    /// <remarks>
    /// <b>すべての項目で同じ高さにする</b>: ボタン・入力欄・一覧で高さが違うと、書式バーの縦位置が
    /// 揃わず押しにくい。 UI はこの値を各項目の高さに与え、縦方向の中央へ寄せる。<br/>
    /// <b>32 DIP を下回る値にしてはいけない</b>: AvaloniaUI の <c>ComboBox</c> と WinUI 3 の
    /// <c>ToggleButton</c> の最小の高さは 32 DIP である。 26 DIP を与えたところ、内側の入力欄だけが
    /// 元の高さを保ち、<b>色の入力欄の上下の枠線が切れた</b>（実測 2026-11-21）。<br/>
    /// <b>この値はパネルの高さにも使う</b>（<see cref="AnnotationPanelAppearance.FormatBar"/> の
    /// <c>FixedContentHeight</c>）。 変えるとパネルの高さと置く位置も一緒に変わる。
    /// </remarks>
    public const double ItemHeight = 32;
}

