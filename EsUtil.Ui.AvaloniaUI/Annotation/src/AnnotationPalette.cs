using EsUtil.Ui.AvaloniaUI.ColorSelector;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Annotation.Presenter.Editor;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の色選択で使う代表色（色名付きの 40 色）。</summary>
/// <remarks>
/// <b>色名付きの 40 色を 1 か所で持つ</b>: 注釈の色は「枠線・内側・文字」の 3 種類しかなく、テーマの色や
/// 色名の全一覧を開くと目的の色へ届くまでに手数がかかる。 よく使う 40 色を 1 つにまとめる。<br/>
/// <b>タブは「代表色」と「ColorPicker」の 2 つ</b>である（■ の格子だけを出し、色名は入力欄に出す）。<br/>
/// <b>定義は 1 か所</b>: 書式バー・右クリックのフライアウト・設定ペインが同じ並びを使う（画面ごとに色が違わない）。
/// </remarks>
public static class AnnotationPalette
{
    /// <summary>代表色の既定の列数（40 色なら 8 列 × 5 行に収まり、スクロールせずに選べる）。</summary>
    public const int DefaultPaletteColumns = AnnotationPaletteCatalog.DefaultPaletteColumns;

    /// <summary>代表色のタブの題名。</summary>
    public const string PaletteTabTitle = AnnotationPaletteCatalog.PaletteTabTitle;

    /// <summary>代表色の並び（色名と保存値 <c>#AARRGGBB</c>）。</summary>
    /// <remarks>定義はコアが単一所有する（<see cref="AnnotationPaletteCatalog.RepresentativeColors"/>）。</remarks>
    public static IReadOnlyList<ColorSelectorCustomColor> RepresentativeColors { get; } =
        [.. AnnotationPaletteCatalog.RepresentativeColors.Select(color => new ColorSelectorCustomColor(color.Name, color.Value))];

    /// <summary>代表色のタブ（■ だけを指定した列数で並べる）を作る。</summary>
    /// <param name="columns">■ の列数（1 以上。 既定は <see cref="DefaultPaletteColumns"/>）。</param>
    /// <returns>■ だけの格子のタブ。</returns>
    public static ColorSelectorCustomTab CreatePaletteTab(int columns = DefaultPaletteColumns)
        => new(PaletteTabTitle, RepresentativeColors, Math.Max(1, columns));

    /// <summary>色の選択を注釈向けの構成にする（代表色のタブだけを出す）。</summary>
    /// <param name="selector">構成する色の選択。</param>
    /// <param name="columns">■ の列数（1 以上。 既定は <see cref="DefaultPaletteColumns"/>）。</param>
    /// <param name="showPicker">カラーピッカーのタブを出すかどうか。</param>
    /// <remarks>
    /// <b>テーマカラーと色名のタブは出さない</b>: 注釈の色は「枠線・内側・文字」の 3 種類しかなく、
    /// 色の全一覧を開くと目的の色へ届くまでに手数がかかる。<br/>
    /// 16 進や色名を直接打ち込む入力欄は残す（代表色に無い色を指定できる）。
    /// </remarks>
    internal static void Configure(
        ColorSelectorControl selector, int columns = DefaultPaletteColumns, bool showPicker = false)
    {
        selector.ShowPickerTab = showPicker;
        selector.ShowThemeColorTab = false;
        selector.ShowNamedColorTab = false;
        selector.ShowTabTitles = true;
        selector.CustomTabs = [CreatePaletteTab(columns)];
    }

    /// <summary>色の選択を「代表色と標準のタブ」の構成にする（設定画面向け）。</summary>
    /// <param name="selector">構成する色の選択。</param>
    /// <param name="columns">■ の列数（1 以上。 既定は <see cref="DefaultPaletteColumns"/>）。</param>
    /// <remarks>
    /// <b>標準のタブも出す</b>: 設定画面では任意の色を選べるほうがよい（テーマの色・色名・カラーピッカー）。
    /// </remarks>
    public static void ConfigureAll(ColorSelectorControl selector, int columns = DefaultPaletteColumns)
    {
        selector.ShowPickerTab = true;
        selector.ShowThemeColorTab = true;
        selector.ShowNamedColorTab = true;
        selector.ShowTabTitles = true;
        selector.CustomTabs = [CreatePaletteTab(columns)];
    }

    /// <summary>代表色を <c>#AARRGGBB</c> の文字列として取得する。</summary>
    /// <param name="name">色名。</param>
    /// <returns>保存値の文字列。 見つからない場合は <see langword="null"/>。</returns>
    /// <remarks>色の既定値を作るときに使う（色名と値を 1 か所で持つ）。</remarks>
    public static string? GetHex(string name)
        => RepresentativeColors.FirstOrDefault(color => string.Equals(color.Name, name, StringComparison.Ordinal))?.Value;

    /// <summary>代表色を色として取得する。</summary>
    /// <param name="name">色名。</param>
    /// <param name="color">色。</param>
    /// <returns>見つかった場合は <see langword="true"/>。</returns>
    public static bool TryGetColor(string name, out DrawingColor color)
    {
        var hex = GetHex(name);
        if (hex is not null
            && EsUtil.Platform.Core.Imaging.DrawingColorExtensions.TryParseHexText(hex, out var parsed))
        {
            color = parsed;
            return true;
        }

        color = default;
        return false;
    }
}
