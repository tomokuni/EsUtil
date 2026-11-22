using System.Globalization;
using EsUtil.Platform.Core.Caching;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core.Palette;

/// <summary>色文字列（色名 / 16 進 / 不透明度 %）の解析・整形・正規化。</summary>
/// <remarks>
/// <b>2 つの形式を定義する</b>:<br/>
/// - <b>保存値</b>（<see cref="ToArgbHex(DrawingColor)"/> の出力）: <c>#AARRGGBB</c>（アルファが先頭。 例: <c>#80FF0000</c>）<br/>
/// - <b>画面表示</b>（<see cref="ToDisplayText"/> / <see cref="Format"/> の出力）: <c>{色}+{不透明度%}</c>
///   （色名があれば色名、無ければ <c>#RRGGBB</c>。 不透明度が 100% なら <c>+…</c> を省く）<br/>
/// 入力は両方の形式（色名 / <c>#RRGGBB</c> / <c>#AARRGGBB</c> / <c>+{n}%</c>）を <see cref="TryParse"/> が受け付ける。<br/>
/// <br/>
/// <b>色名の出所は <see cref="ColorNames"/>（Windows 標準の色名）</b>である。 解釈と整形を 1 か所へ置くことで、
/// 4 つの UI 版のコントロールが同じ文字列を同じ意味で扱える（UI ごとに色名の表や桁数の扱いがずれない）。<br/>
/// <b>解析・整形の結果は容量制限付きで共有する</b>: テキスト入力はキーストロークごとに同じ文字列で呼ばれるため、
/// 変換（色名の逆引き・16 進の組み立て）を毎回行わない（容量管理は <see cref="CapacityLimitedCache{TKey, TValue}"/>
/// が単一所有する）。
/// </remarks>
public static class ColorString
{
    /// <summary>解析結果のキャッシュ（入力文字列 → 解析結果）。</summary>
    private static readonly CapacityLimitedCache<string, (bool Success, DrawingColor Color, double Opacity)> s_parseCache =
        new(text => TryParseCore(text, out var color, out var opacity) ? (true, color, opacity) : (false, DrawingColor.Empty, 1.0), StringComparer.Ordinal);

    /// <summary>表示テキストのキャッシュ（入力文字列 → 画面表示）。</summary>
    private static readonly CapacityLimitedCache<string, string> s_displayTextCache =
        new(FormatDisplayText, StringComparer.Ordinal);

    /// <summary>正規化した保存値のキャッシュ（入力文字列 → <c>#AARRGGBB</c>）。</summary>
    private static readonly CapacityLimitedCache<string, string> s_normalizedCache =
        new(NormalizeArgbHex, StringComparer.Ordinal);

    /// <summary>色名が定義されているかを調べる。</summary>
    /// <param name="name">色名（例: <c>Red</c>）。 大文字小文字は無視する。 null 可。</param>
    /// <returns>定義されている場合は <see langword="true"/>。</returns>
    public static bool IsDefinedColorName(string? name) => ColorNames.IsDefined(name);

    /// <summary>色文字列を解析して色と不透明度を得る。</summary>
    /// <param name="text">解析する文字列（例: <c>Red</c> / <c>#336699</c> / <c>Red+50%</c> / <c>#336699+75%</c>）。 null 可。</param>
    /// <param name="color">解析した色（<c>+{n}%</c> がある場合はアルファを不透明度で置き換えた色）。</param>
    /// <param name="opacity">解析した不透明度（0.0〜1.0）。</param>
    /// <returns>解析できた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>受理する形</b>: 色名（<see cref="ColorNames"/> の表）/ <c>#RRGGBB</c> / <c>#AARRGGBB</c>（<c>#</c> は省略可）/
    /// それらに <c>+{0〜100}%</c> を付けた形。 <b>それ以外は拒否する</b>（3・4 桁の 16 進や、% の付かない <c>+50</c> など）。
    /// </remarks>
    public static bool TryParse(string? text, out DrawingColor color, out double opacity)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            color = DrawingColor.Empty;
            opacity = 1.0;
            return false;
        }

        var cached = s_parseCache.GetOrAdd(text);
        color = cached.Color;
        opacity = cached.Opacity;
        return cached.Success;
    }

    /// <summary>色と不透明度を入力欄の表記へ整形する。</summary>
    /// <param name="color">色（アルファは見ない。 不透明度は <paramref name="opacity"/> が決める）。</param>
    /// <param name="opacity">不透明度（0.0〜1.0）。</param>
    /// <returns>整形した文字列（色名があれば <c>Red</c> / <c>Red+50%</c>、無ければ <c>#80FF0000</c>）。</returns>
    /// <remarks>
    /// <b>色名が無い色は常に <c>#AARRGGBB</c>（8 桁）で表す</b>: 6 桁の <c>#RRGGBB</c> に不透明度を
    /// <c>+{n}%</c> で足す形にすると、<b>同じ色でも色名の有無で桁数と形が変わり</b>、不透明度が見えない
    /// 表記も生まれます（実測 2026-11-22）。 8 桁にアルファを含めれば、1 つの表記で色も不透明度も分かります。<br/>
    /// <b>色名がある色は <c>{色名}+{不透明度%}</c></b> のままです（色名のほうが読みやすく、
    /// 不透明度は色名だけでは表せないため）。 不透明度が 100% のときは <c>+…</c> を省きます。
    /// </remarks>
    public static string Format(DrawingColor color, double opacity)
    {
        if (ColorNames.FindName(color) is { } name)
        {
            var percent = (int)Math.Round(Math.Clamp(opacity, 0.0, 1.0) * 100);
            return percent >= 100 ? name : $"{name}+{percent}%";
        }

        var alpha = (byte)Math.Round(Math.Clamp(opacity, 0.0, 1.0) * 255.0);
        return $"#{alpha:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    /// <summary>保存値を画面表示用の文字列へ変換する。</summary>
    /// <param name="text">保存値（例: <c>#80FF0000</c> / <c>Red</c> / <c>Red+50%</c>）。 null 可。</param>
    /// <returns>画面表示（例: <c>Red+50%</c>）。 解析できない場合は入力をそのまま返す。</returns>
    /// <remarks>
    /// <c>+{n}%</c> を含まない入力（<c>#AARRGGBB</c> など）は<b>アルファを不透明度として解釈する</b>。
    /// </remarks>
    public static string ToDisplayText(string? text)
        => string.IsNullOrWhiteSpace(text) ? text ?? string.Empty : s_displayTextCache.GetOrAdd(text);

    /// <summary>色文字列を指定の表記へ変換する（入力欄へ出す文字列）。</summary>
    /// <param name="text">保存値（例: <c>#80FF0000</c>）。 null 可。</param>
    /// <param name="format">表記（色名 + 不透明度 / カラーコード）。</param>
    /// <returns>表記へ変換した文字列。 解析できない場合は入力をそのまま返す。</returns>
    /// <exception cref="ArgumentOutOfRangeException">定義されていない表記の場合。</exception>
    public static string ToText(string? text, ColorTextFormat format) => format switch
    {
        ColorTextFormat.NameWithAlpha => ToDisplayText(text),
        ColorTextFormat.HexCode => ToArgbHex(text),
        var other => throw new ArgumentOutOfRangeException(nameof(format), other, "定義されていない表記です。"),
    };

    /// <summary>色文字列を、利用者が付けた色名（別名）を解決してから解析する。</summary>
    /// <param name="text">解析する文字列（例: <c>黒</c> / <c>#336699</c>）。 null 可。</param>
    /// <param name="resolveAlias">色名を保存値（<c>#AARRGGBB</c> など）へ解決する処理。 見つからない場合は null を返す。</param>
    /// <param name="color">解析した色。</param>
    /// <param name="opacity">解析した不透明度（0.0〜1.0）。</param>
    /// <returns>解析できた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>一覧に出す色名も入力として受け付ける</b>: 画面には利用者が付けた名前（例: <c>黒</c>）が出るため、
    /// 同じ文字列を貼り付けたときに「無効な色コード」と表示されるのは矛盾します（実測 2026-11-23）。
    /// 別名の表は UI（カスタム タブ）が持つため、解決だけを呼び出し側から受け取ります。<br/>
    /// 別名が解決できない場合は通常の解析（<see cref="TryParse"/>）と同じ結果になります。
    /// </remarks>
    public static bool TryParseWithAliases(
        string? text, Func<string, string?> resolveAlias, out DrawingColor color, out double opacity)
    {
        ArgumentNullException.ThrowIfNull(resolveAlias);

        if (TryParse(text, out color, out opacity))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var aliased = resolveAlias(text);
        return !string.IsNullOrWhiteSpace(aliased) && TryParse(aliased, out color, out opacity);
    }

    /// <summary>任意の色文字列を <c>#AARRGGBB</c>（アルファが先頭）へ正規化する。</summary>
    /// <param name="text">入力（例: <c>Red</c> / <c>Red+50%</c> / <c>#FF0000</c>）。 null 可。</param>
    /// <returns><c>#AARRGGBB</c> の保存値（例: <c>#80FF0000</c>）。 解析できない場合は入力をそのまま返す。</returns>
    public static string ToArgbHex(string? text)
        => string.IsNullOrWhiteSpace(text) ? text ?? string.Empty : s_normalizedCache.GetOrAdd(text);

    /// <summary>利用者が付けた色名（別名）を解決してから <c>#AARRGGBB</c> へ正規化する。</summary>
    /// <param name="text">入力（例: <c>黒</c> / <c>#FF0000</c>）。 null 可。</param>
    /// <param name="resolveAlias">色名を解決する処理。 見つからない場合は null を返す。</param>
    /// <returns><c>#AARRGGBB</c> の保存値。 解析できない場合は入力をそのまま返す。</returns>
    /// <remarks>別名を解決できた場合だけ変換します（解決できない入力は <see cref="ToArgbHex(string?)"/> と同じ）。</remarks>
    public static string ToArgbHex(string? text, Func<string, string?> resolveAlias)
    {
        ArgumentNullException.ThrowIfNull(resolveAlias);

        if (string.IsNullOrWhiteSpace(text) || TryParse(text, out _, out _))
        {
            return ToArgbHex(text);
        }

        var aliased = resolveAlias(text);
        return string.IsNullOrWhiteSpace(aliased) ? text : ToArgbHex(aliased);
    }

    /// <summary>色を <c>#AARRGGBB</c> の形へ変換する。</summary>
    /// <param name="color">対象の色。</param>
    /// <returns><c>#AARRGGBB</c> の文字列（例: <c>#80FF0000</c>）。</returns>
    /// <remarks>不透明度はアルファとして統合される（保存値の形式である）。</remarks>
    public static string ToArgbHex(DrawingColor color) => color.ToHexText();

    /// <summary>色文字列を解析する本体（キャッシュを持たない）。</summary>
    /// <param name="text">解析する文字列（空白は呼び出し側で除外済み）。</param>
    /// <param name="color">解析した色。</param>
    /// <param name="opacity">解析した不透明度。</param>
    /// <returns>解析できた場合は <see langword="true"/>。</returns>
    private static bool TryParseCore(string text, out DrawingColor color, out double opacity)
    {
        color = DrawingColor.Empty;
        opacity = 1.0;

        var plusIndex = text.LastIndexOf('+');
        if (plusIndex > 0 && text.EndsWith('%'))
        {
            var colorPart = text[..plusIndex];
            var opacityPart = text[(plusIndex + 1)..^1];
            if (!int.TryParse(opacityPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent)
                || percent is < 0 or > 100
                || !TryParseColorPart(colorPart, out var parsed))
            {
                return false;
            }

            opacity = percent / 100.0;
            // 50 * 2.55 = 127.4999… の誤差を避けるため整数で計算してから丸める（50% → 128）
            color = DrawingColor.FromArgb((byte)Math.Round(percent * 255.0 / 100.0), parsed.R, parsed.G, parsed.B);
            return true;
        }

        return TryParseColorPart(text, out color);
    }

    /// <summary>色の部分（色名または 16 進）を解析する。</summary>
    /// <param name="text">解析する文字列。</param>
    /// <param name="color">解析した色。</param>
    /// <returns>解析できた場合は <see langword="true"/>。</returns>
    private static bool TryParseColorPart(string text, out DrawingColor color)
        => ColorNames.TryGet(text, out color) || DrawingColorExtensions.TryParseHexText(text, out color);

    /// <summary>表示テキストを組み立てる（キャッシュの生成関数）。</summary>
    /// <param name="text">保存値。</param>
    /// <returns>画面表示。</returns>
    private static string FormatDisplayText(string text)
    {
        if (!TryParse(text, out var color, out var opacity))
        {
            return text;
        }

        // 「+{n}%」が無い入力はアルファを不透明度として扱う
        if (!text.Contains('+', StringComparison.Ordinal))
        {
            opacity = color.A / 255.0;
        }

        return Format(color, opacity);
    }

    /// <summary>保存値を正規化する（キャッシュの生成関数）。</summary>
    /// <param name="text">入力。</param>
    /// <returns><c>#AARRGGBB</c>。 解析できない場合は入力そのもの。</returns>
    private static string NormalizeArgbHex(string text)
        => TryParse(text, out var color, out _) ? ToArgbHex(color) : text;
}
