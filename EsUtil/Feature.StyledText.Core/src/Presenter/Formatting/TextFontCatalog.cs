using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Feature.StyledText.Presenter.Formatting;

/// <summary>書式で選べるフォントの並べ方（単一所有）。</summary>
/// <remarks>
/// <b>優先フォントを先頭に置く</b>: <see cref="TextDefaults.PreferredFontFamilies"/>（<c>Yu Gothic UI</c> →
/// <c>Segoe UI</c>）を先頭へ並べ、その後に環境のフォントを続ける。 4 つの UI 版がそれぞれ並べると、
/// 版によって一覧の先頭が変わる。<br/>
/// <b>列挙は UI が行う</b>: インストール済みフォントの列挙は UI フレームワーク（または OS の API）に依存するため、
/// 本クラスは「渡された名前をどう並べるか」だけを決める。 列挙を持たない UI は
/// <see cref="FallbackNames"/> を渡す。
/// </remarks>
public static class TextFontCatalog
{
    /// <summary>列挙を持たない UI が使う代表的なフォント名。</summary>
    /// <value>優先フォントの後に日本語・欧文の代表的なフォントを並べたもの。</value>
    public static IReadOnlyList<string> FallbackNames { get; } =
    [
        .. TextDefaults.PreferredFontFamilies,
        "メイリオ",
        "Meiryo UI",
        "游ゴシック",
        "游明朝",
        "MS UI Gothic",
        "MS Gothic",
        "BIZ UDPGothic",
        "Noto Sans JP",
        "Arial",
        "Calibri",
        "Consolas",
    ];

    /// <summary>フォント名を並べ替える（優先フォントを先頭へ、残りは名前順）。</summary>
    /// <param name="installed">環境にあるフォント名。</param>
    /// <returns>並べ替えたフォント名。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="installed"/> が null の場合。</exception>
    public static IReadOnlyList<string> Order(IEnumerable<string> installed)
    {
        ArgumentNullException.ThrowIfNull(installed);

        var names = installed
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCulture)
            .ToList();

        foreach (var preferred in TextDefaults.PreferredFontFamilies.Reverse())
        {
            var index = names.FindIndex(name => string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                names.RemoveAt(index);
            }

            names.Insert(0, preferred);
        }

        return names;
    }
}