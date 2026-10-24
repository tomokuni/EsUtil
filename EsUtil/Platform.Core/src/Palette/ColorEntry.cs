using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core.Palette;

/// <summary>色名と色の組（ドロップダウンの選択肢 1 件）。</summary>
/// <param name="Name">色の英語名（例: <c>Red</c>）。</param>
/// <param name="Color">色（<see cref="ColorNames"/> の表の値）。</param>
/// <remarks>
/// <b>並び順の単一所有者</b>である: 色名順（<see cref="All"/>）と色相順（<see cref="AllByHue"/>）の 2 通りを作り、
/// どちらを選ぶかは <see cref="ColorSortMode"/> の値で決める（UI ごとに並べ替えを書かない）。<br/>
/// <b>色相順の規則</b>: 無彩色（白・灰・黒）を明度の昇順で先に置き、有彩色を色相の昇順（赤 → 黄 → 緑 → 青 → 紫）で
/// 続ける。 同じキーの中では色名のアルファベット順にする（並びが実行ごとに変わらない）。
/// </remarks>
public sealed record ColorEntry(string Name, DrawingColor Color)
{
    /// <summary>色名（英名）のアルファベット順の全色。</summary>
    public static IReadOnlyList<ColorEntry> All => s_byName;

    /// <summary>色相順の全色。</summary>
    public static IReadOnlyList<ColorEntry> AllByHue => s_byHue;

    private static readonly ColorEntry[] s_all =
        [.. ColorNames.ByName.Select(pair => new ColorEntry(pair.Key, pair.Value))];

    private static readonly IReadOnlyList<ColorEntry> s_byName =
        [.. s_all.OrderBy(e => e.Name, StringComparer.Ordinal)];

    private static readonly IReadOnlyList<ColorEntry> s_byHue =
        [.. s_all.OrderBy(e => GetSortKey(e.Color)).ThenBy(e => e.Name, StringComparer.Ordinal)];

    /// <summary>並び順の指定に応じた全色を返す。</summary>
    /// <param name="mode">並び順。</param>
    /// <returns>指定された順の全色。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> が定義されていない値の場合。</exception>
    public static IReadOnlyList<ColorEntry> GetSorted(ColorSortMode mode)
        => mode switch
        {
            ColorSortMode.ByName => s_byName,
            ColorSortMode.ByHue => s_byHue,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "定義されていない並び順です。"),
        };

    /// <summary>一覧に表示する名前。</summary>
    /// <returns>色名（例: <c>Red</c>）。</returns>
    public override string ToString() => Name;

    /// <summary>色相順の並べ替えキーを求める。</summary>
    /// <param name="color">対象の色。</param>
    /// <returns>無彩色は (0, 明度)、有彩色は (1, 色相 0〜360) のキー。</returns>
    private static (int Priority, float Key) GetSortKey(DrawingColor color)
    {
        var r = color.R / 255f;
        var g = color.G / 255f;
        var b = color.B / 255f;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        if (delta < 0.001f)
        {
            // 無彩色は明度の昇順（黒 → 灰 → 白）
            return (0, max);
        }

        var hue = max == r
            ? ((g - b) / delta) % 6f
            : max == g
                ? ((b - r) / delta) + 2f
                : ((r - g) / delta) + 4f;
        return (1, hue * 60f);
    }
}
