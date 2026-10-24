using System.Collections.Concurrent;
using Avalonia.Media;
using CoreColorString = EsUtil.Platform.Core.Palette.ColorString;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>色文字列（色名 / 16 進 / 不透明度 %）の解析・整形・正規化（Avalonia の色型による入口）。</summary>
/// <remarks>
/// <b>規則の所有者はコアである</b>: 受理する入力・色名の表・保存値（<c>#AARRGGBB</c>）と画面表示
/// （<c>{色}+{不透明度%}</c>）の形式は <see cref="CoreColorString"/>（<c>EsUtil.Platform.Core.Palette</c>）が
/// 単一所有する。 本クラスは <see cref="Color"/>（Avalonia の色型）との変換だけを行う
/// （UI ごとに色名の表や桁数の扱いがずれないようにするため）。<br/>
/// <b>保持する公開面</b>: 利用側（設定画面・コンバーター）が <see cref="Color"/> で呼べるよう、メソッド名と引数は
/// 従来のままにする。
/// </remarks>
public static class ColorString
{
    /// <summary>色名（英名）の辞書（色名 → 色）。</summary>
    /// <remarks>
    /// 色名の出所はコア（<see cref="EsUtil.Platform.Core.Palette.ColorNames"/>。 Windows 標準の色名）である。<br/>
    /// 本プロパティは <see cref="ColorEntry"/> が一覧を作るときだけ使う（内部公開）。
    /// </remarks>
    internal static IReadOnlyDictionary<string, Color> ColorNames
        => s_colorNames ??= EsUtil.Platform.Core.Palette.ColorNames.ByName
            .ToDictionary(pair => pair.Key, pair => ToAvalonia(pair.Value), StringComparer.OrdinalIgnoreCase);

    /// <summary>色名の辞書（初回だけ作る）。</summary>
    private static IReadOnlyDictionary<string, Color>? s_colorNames;

    /// <summary>指定された色名が定義されているかを調べる。</summary>
    /// <param name="name">色名（例: <c>Red</c>）。 大文字小文字は無視する。</param>
    /// <returns>定義されている場合は <see langword="true"/>。</returns>
    public static bool IsDefinedColorName(string name) => CoreColorString.IsDefinedColorName(name);

    /// <summary>色文字列を解析して色と不透明度を取得する。</summary>
    /// <param name="text">解析する文字列（例: <c>Red</c> / <c>#336699</c> / <c>Red+50%</c>）。</param>
    /// <param name="color">解析した色（不透明度はアルファへ合成される）。</param>
    /// <param name="opacity">解析した不透明度（0.0〜1.0）。</param>
    /// <returns>解析できた場合は <see langword="true"/>。</returns>
    public static bool TryParse(string? text, out Color color, out double opacity)
    {
        if (CoreColorString.TryParse(text, out var parsed, out opacity))
        {
            color = ToAvalonia(parsed);
            return true;
        }

        color = default;
        return false;
    }

    /// <summary>色と不透明度を <c>{色}+{不透明度%}</c> の形へ整形する。</summary>
    /// <param name="color">色（アルファは見ない）。</param>
    /// <param name="opacity">不透明度（0.0〜1.0）。 1.0 の場合は <c>+…</c> を省く。</param>
    /// <returns>整形した文字列（例: <c>Red</c> / <c>Red+50%</c>）。</returns>
    public static string Format(Color color, double opacity) => CoreColorString.Format(ToDrawing(color), opacity);

    /// <summary>保存値を画面表示用の文字列へ変換する。</summary>
    /// <param name="text">保存値（例: <c>#80FF0000</c>）。</param>
    /// <returns>画面表示（例: <c>Red+50%</c>）。 解析できない場合は入力をそのまま返す。</returns>
    public static string ToDisplayText(string? text) => CoreColorString.ToDisplayText(text);

    /// <summary>任意の色文字列を <c>#AARRGGBB</c>（保存値の形）へ正規化する。</summary>
    /// <param name="text">入力（例: <c>Red</c>）。</param>
    /// <returns><c>#AARRGGBB</c> の保存値。 解析できない場合は入力をそのまま返す。</returns>
    public static string ToArgbHex(string? text) => CoreColorString.ToArgbHex(text);

    /// <summary>色を <c>#AARRGGBB</c> の形へ変換する。</summary>
    /// <param name="color">対象の色。</param>
    /// <returns><c>#AARRGGBB</c> の文字列（例: <c>#80FF0000</c>）。</returns>
    public static string ToArgbHex(Color color) => CoreColorString.ToArgbHex(ToDrawing(color));

    /// <summary>容量制限付きのキャッシュへ値を登録する（超過時は全クリアしてから登録）。</summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="cache">対象のキャッシュ。</param>
    /// <param name="capacity">最大保持数。</param>
    /// <param name="key">キー。</param>
    /// <param name="value">値。</param>
    /// <remarks>
    /// 容量管理の規則（上限に達したら全クリア）は
    /// <see cref="EsUtil.Platform.Core.Caching.CapacityLimitedCache{TKey, TValue}"/> と同じである
    /// （本メソッドは既存の <see cref="ConcurrentDictionary{TKey, TValue}"/> を使う呼び出し側のための入口）。
    /// </remarks>
    internal static void AddWithCapacityLimit<T>(
        ConcurrentDictionary<string, T> cache, int capacity, string key, T value)
    {
        if (cache.Count >= capacity)
        {
            cache.Clear();
        }

        cache[key] = value;
    }

    /// <summary>Avalonia の色をコアの色（<see cref="DrawingColor"/>）へ写す。</summary>
    /// <param name="color">Avalonia の色。</param>
    /// <returns>同じ ARGB を持つ色。</returns>
    private static DrawingColor ToDrawing(Color color) => DrawingColor.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary>コアの色を Avalonia の色へ写す。</summary>
    /// <param name="color">コアの色。</param>
    /// <returns>同じ ARGB を持つ色。</returns>
    private static Color ToAvalonia(DrawingColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);
}