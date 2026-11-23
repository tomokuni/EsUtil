using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Annotation.Presenter.Editor;

/// <summary>注釈の色選択で使う代表色の定義（単一所有）。</summary>
/// <remarks>
/// <b>定義は 1 か所</b>: 書式バー・右クリックのフライアウト・設定ペインが同じ並びを使う。
/// 4 つの UI 版がそれぞれ色名と値を持つと、版ごとに色が違う危険がある
/// （実測 2026-11-21: 同じ 40 色を 4 か所で重複して持つ）。<br/>
/// <b>UI はタブの見た目だけを作る</b>: 色選択コントロールの型は UI ごとに違うため、
/// タブを組み立てるのは各 UI が行い、色名と値は本クラスから取ります。
/// </remarks>
public static class AnnotationPaletteCatalog
{
    /// <summary>代表色の既定の列数（40 色なら 8 列 × 5 行に収まり、スクロールせずに選べる）。</summary>
    public const int DefaultPaletteColumns = 8;

    /// <summary>代表色のタブの題名。</summary>
    /// <remarks>
    /// <b>タブは 2 つだけ</b>（「代表色」と「ColorPicker」）: ■ の格子と色名の一覧を別のタブに分けると、
    /// 目的の色へ届くまでの手数が増えるだけで、同じ 40 色を 2 通りに見せる意味が薄い（実測 2026-11-22）。
    /// 格子の各 ■ の色名は入力欄へ出るため、一覧が無くても色名は分かる。
    /// </remarks>
    public const string PaletteTabTitle = "代表色";

    /// <summary>代表色の並び（色名と保存値 <c>#AARRGGBB</c>）。</summary>
    /// <remarks>
    /// 無彩色 → 赤系 → 橙・黄系 → 緑系 → 青系 → 紫・桃系の順に並べる（同じ系統の色を続けて選びやすくする）。
    /// 値は 40 色すべて異なる。
    /// </remarks>
    public static IReadOnlyList<AnnotationPaletteColor> RepresentativeColors { get; } =
    [
        // 無彩色
        new("黒", "#FF000000"),
        new("濃い灰色", "#FF404040"),
        new("灰色", "#FF808080"),
        new("薄い灰色", "#FFC0C0C0"),
        new("白", "#FFFFFFFF"),
        new("墨", "#FF1F2937"),
        new("銀", "#FF9CA3AF"),

        // 赤系
        new("赤", "#FFDC2626"),
        new("茜", "#FFB91C1C"),
        new("紅", "#FFE11D48"),
        new("躑躅", "#FFBE123C"),
        new("珊瑚", "#FFF87171"),

        // 橙・黄系
        new("朱", "#FFEA580C"),
        new("橙", "#FFF97316"),
        new("琥珀", "#FFB45309"),
        new("杏", "#FFFDBA74"),
        new("黄", "#FFEAB308"),
        new("山吹", "#FFFBBF24"),
        new("檸檬", "#FFFDE047"),
        new("黄土", "#FFCA8A04"),
        new("茶", "#FF92400E"),

        // 緑系
        new("黄緑", "#FF65A30D"),
        new("若草", "#FF84CC16"),
        new("鶯", "#FF4D7C0F"),
        new("緑", "#FF16A34A"),
        new("常磐", "#FF047857"),
        new("青緑", "#FF0D9488"),

        // 青系
        new("水色", "#FF0EA5E9"),
        new("空色", "#FF38BDF8"),
        new("青磁", "#FF22D3EE"),
        new("浅葱", "#FF06B6D4"),
        new("青", "#FF2563EB"),
        new("瑠璃", "#FF3B82F6"),
        new("群青", "#FF1D4ED8"),
        new("藍", "#FF312E81"),

        // 紫・桃系
        new("紫", "#FF7C3AED"),
        new("菫", "#FFA78BFA"),
        new("藤", "#FFC4B5FD"),
        new("桃", "#FFDB2777"),
        new("桜", "#FFF9A8D4"),
    ];

    /// <summary>代表色を <c>#AARRGGBB</c> の文字列として取得する。</summary>
    /// <param name="name">色名。</param>
    /// <returns>保存値の文字列。 見つからない場合は <see langword="null"/>。</returns>
    public static string? GetHex(string name)
    {
        foreach (var color in RepresentativeColors)
        {
            if (string.Equals(color.Name, name, StringComparison.Ordinal))
            {
                return color.Value;
            }
        }

        return null;
    }

    /// <summary>代表色を色として取得する。</summary>
    /// <param name="name">色名。</param>
    /// <param name="color">色。</param>
    /// <returns>見つかった場合は <see langword="true"/>。</returns>
    public static bool TryGetColor(string name, out DrawingColor color)
    {
        if (GetHex(name) is { Length: > 0 } hex && DrawingColorExtensions.TryParseHexText(hex, out var parsed))
        {
            color = parsed;
            return true;
        }

        color = default;
        return false;
    }
}
