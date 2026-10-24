using EsUtil.Platform.Core.Palette;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core;

/// <summary>色（色名と色文字列）機能の窓口。</summary>
/// <remarks>
/// 型は <see cref="EsUtil.Platform.Core.Palette"/>（<see cref="ColorNames"/> / <see cref="ColorEntry"/> /
/// <see cref="ColorString"/> / <see cref="ColorValidation"/>）に置く。<br/>
/// 本クラスは<b>利用側が <c>using EsUtil.Platform.Core;</c> だけで解釈・整形・一覧の取得を行えるようにする</b>ための
/// 窓口であり、<b>実装を持たない</b>（判断は各型が単一所有し、本クラスは委譲する）。<br/>
/// <b>色名は Windows 標準（<see cref="System.Drawing.KnownColor"/>）である</b>（UI フレームワークの色名の表を使わない
/// ため、4 つの UI 版で同じ名前と同じ並びになる）。
/// </remarks>
public static class EsColor
{
    /// <summary>色文字列を解析する。</summary>
    /// <param name="text">解析する文字列（色名 / <c>#RRGGBB</c> / <c>#AARRGGBB</c> / <c>+{n}%</c>）。 null 可。</param>
    /// <param name="color">解析した色。</param>
    /// <param name="opacity">解析した不透明度（0.0〜1.0）。</param>
    /// <returns>解析できた場合は <see langword="true"/>。</returns>
    public static bool TryParse(string? text, out DrawingColor color, out double opacity)
        => ColorString.TryParse(text, out color, out opacity);

    /// <summary>色文字列を <c>#AARRGGBB</c>（保存値の形）へ正規化する。</summary>
    /// <param name="text">入力。 null 可。</param>
    /// <returns>正規化した文字列。 解析できない場合は入力そのもの。</returns>
    public static string ToArgbHex(string? text) => ColorString.ToArgbHex(text);

    /// <summary>保存値を画面表示用の文字列へ変換する。</summary>
    /// <param name="text">保存値。 null 可。</param>
    /// <returns>画面表示。 解析できない場合は入力そのもの。</returns>
    public static string ToDisplayText(string? text) => ColorString.ToDisplayText(text);

    /// <summary>色と不透明度を画面表示の形へ整形する。</summary>
    /// <param name="color">色。</param>
    /// <param name="opacity">不透明度（0.0〜1.0）。</param>
    /// <returns>整形した文字列。</returns>
    public static string Format(DrawingColor color, double opacity) => ColorString.Format(color, opacity);

    /// <summary>色名が定義されているかを調べる。</summary>
    /// <param name="name">色名。 null 可。</param>
    /// <returns>定義されている場合は <see langword="true"/>。</returns>
    public static bool IsDefinedName(string? name) => ColorString.IsDefinedColorName(name);

    /// <summary>色名を色へ解決する。</summary>
    /// <param name="name">色名。 null 可。</param>
    /// <param name="color">解決した色。</param>
    /// <returns>解決できた場合は <see langword="true"/>。</returns>
    public static bool TryParseName(string? name, out DrawingColor color) => ColorNames.TryGet(name, out color);

    /// <summary>色に対応する色名を探す。</summary>
    /// <param name="color">対象の色。</param>
    /// <returns>色名。 見つからない場合は null。</returns>
    public static string? FindName(DrawingColor color) => ColorNames.FindName(color);

    /// <summary>並び順に応じた色の一覧を取得する。</summary>
    /// <param name="mode">並び順。</param>
    /// <returns>色名と色の一覧。</returns>
    public static IReadOnlyList<ColorEntry> GetPalette(ColorSortMode mode = ColorSortMode.ByHue)
        => ColorEntry.GetSorted(mode);

    /// <summary>色文字列が有効かを調べる。</summary>
    /// <param name="text">対象の文字列。 null 可。</param>
    /// <returns>有効な場合は <see langword="true"/>。</returns>
    public static bool IsValid(string? text) => ColorValidation.IsValid(text);
}
