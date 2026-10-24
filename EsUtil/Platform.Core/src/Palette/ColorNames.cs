using System.Collections.Frozen;
using System.Drawing;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core.Palette;

/// <summary>名前付きの色（Windows 標準の色名）の一覧と、色名 ⇔ 色の相互参照。</summary>
/// <remarks>
/// <b>色名の出所は Windows 標準（<see cref="KnownColor"/>）である</b>: 色名 → 色 の表を
/// <see cref="KnownColor"/> から作り、システム色（<c>ActiveBorder</c> 等。 画面の配色に追随して
/// 値が変わる色）は除く。 <b>リフレクションを使わない</b>（列挙と <see cref="DrawingColor.FromKnownColor(KnownColor)"/>
/// だけで作るため、Native AOT でもそのまま動く）。<br/>
/// <b>色 → 色名の逆向き参照を持つ</b>: 画面表示（<c>Red</c> や <c>#336699</c>）で色名を使うために、
/// アルファを含めた完全一致を先に、無ければ RGB 一致（アルファ無視）で引く（<see cref="FindName"/>）。<br/>
/// <b>反復の順序</b>: <see cref="KnownColor"/> の宣言順（色名のアルファベット順）である。 並べ替えは
/// <see cref="ColorEntry"/> が行う。
/// </remarks>
public static class ColorNames
{
    /// <summary>色名（大文字小文字を無視）→ 色 の表。</summary>
    private static readonly FrozenDictionary<string, DrawingColor> s_byName = BuildNames();

    /// <summary>色（アルファ含む完全一致）→ 色名 の表。</summary>
    private static readonly FrozenDictionary<DrawingColor, string> s_exact = BuildReverse(s_byName);

    /// <summary>色（RGB のみ一致）→ 色名 の表。</summary>
    /// <remarks>同じ RGB に複数の色名がある場合は不透明（<c>A = 255</c>）の色名を優先する（例: 白は <c>White</c>）。</remarks>
    private static readonly FrozenDictionary<(byte R, byte G, byte B), string> s_byRgb = BuildReverseRgb(s_byName);

    /// <summary>色名 → 色 の表（キーは大文字小文字を無視する）。</summary>
    /// <value>Windows 標準の色名（システム色を除く）の表。</value>
    public static IReadOnlyDictionary<string, DrawingColor> ByName => s_byName;

    /// <summary>色名を色へ解決する。</summary>
    /// <param name="name">色名（例: <c>Red</c>）。 大文字小文字は無視する。 null 可。</param>
    /// <param name="color">解決した色。 解決できなかった場合は <see cref="DrawingColor.Empty"/>。</param>
    /// <returns>解決できた場合は <see langword="true"/>。</returns>
    public static bool TryGet(string? name, out DrawingColor color)
    {
        if (name is not null && s_byName.TryGetValue(name, out color))
        {
            return true;
        }

        color = DrawingColor.Empty;
        return false;
    }

    /// <summary>色名が定義されているかを調べる。</summary>
    /// <param name="name">色名。 大文字小文字は無視する。 null 可。</param>
    /// <returns>定義されている場合は <see langword="true"/>。</returns>
    public static bool IsDefined(string? name) => name is not null && s_byName.ContainsKey(name);

    /// <summary>色に対応する色名を探す。</summary>
    /// <param name="color">対象の色。</param>
    /// <returns>対応する色名。 見つからない場合は null。</returns>
    /// <remarks>
    /// アルファを含めた完全一致を先に探し、無ければ RGB だけで一致する色名を返す
    /// （半透明の白は <c>White</c> と表示される）。
    /// </remarks>
    public static string? FindName(DrawingColor color)
    {
        if (s_exact.TryGetValue(color, out var exact))
        {
            return exact;
        }

        return s_byRgb.TryGetValue((color.R, color.G, color.B), out var rgb) ? rgb : null;
    }

    /// <summary>色名の表を作る（システム色を除く）。</summary>
    /// <returns>色名 → 色 の表（キーは大文字小文字を無視する）。</returns>
    private static FrozenDictionary<string, DrawingColor> BuildNames()
    {
        var result = new Dictionary<string, DrawingColor>(StringComparer.OrdinalIgnoreCase);
        foreach (var known in Enum.GetValues<KnownColor>())
        {
            var color = DrawingColor.FromKnownColor(known);
            // システム色（画面の配色に追随する色）は選ばせる色ではないため除く
            if (color.IsEmpty || color.IsSystemColor)
            {
                continue;
            }

            // 既知の色のまま持つと、ARGB から作った色と等しくならない（Color の等価比較は生成元も見る）ため、
            // ARGB の値へ写してから保持する
            result[known.ToString()] = DrawingColor.FromArgb(color.A, color.R, color.G, color.B);
        }

        return result.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>色（完全一致）→ 色名 の表を作る。</summary>
    /// <param name="names">色名 → 色 の表。</param>
    /// <returns>色 → 色名 の表。</returns>
    /// <remarks>同じ色に複数の名前がある場合は宣言順で最初の名前を残す。</remarks>
    private static FrozenDictionary<DrawingColor, string> BuildReverse(IReadOnlyDictionary<string, DrawingColor> names)
    {
        var result = new Dictionary<DrawingColor, string>(names.Count);
        foreach (var (name, color) in names)
        {
            result.TryAdd(color, name);
        }

        return result.ToFrozenDictionary();
    }

    /// <summary>色（RGB のみ一致）→ 色名 の表を作る。</summary>
    /// <param name="names">色名 → 色 の表。</param>
    /// <returns>RGB → 色名 の表。</returns>
    /// <remarks>
    /// 不透明（<c>A = 255</c>）の色名を優先し、両方が同じ透過度なら先に見た名前を残す
    /// （<c>White</c> と <c>Transparent</c> は同じ RGB のため、不透明でない色には <c>White</c> を返す）。
    /// </remarks>
    private static FrozenDictionary<(byte R, byte G, byte B), string> BuildReverseRgb(IReadOnlyDictionary<string, DrawingColor> names)
    {
        var result = new Dictionary<(byte R, byte G, byte B), string>(names.Count);
        foreach (var (name, color) in names)
        {
            var key = (color.R, color.G, color.B);
            if (result.TryGetValue(key, out var existing)
                && (names[existing].A == 255 || color.A != 255))
            {
                continue;
            }

            result[key] = name;
        }

        return result.ToFrozenDictionary();
    }
}
