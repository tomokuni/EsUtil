using EsUtil.Ui.MewUI.ColorSelector;
using EsUtil.Feature.Annotation.Presenter.Editor;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈の色選択で使う代表色（40 色）と、色の選択をその構成にする処理。</summary>
/// <remarks>
/// <b>並びは 4 つの UI 版で同じ</b>である（AvaloniaUI 版の <c>AnnotationPalette</c> と同じ色名・同じ値）。<br/>
/// 色は保存値（<c>#AARRGGBB</c>）で持つ（入力欄・保存・描画が同じ文字列を扱う）。
/// </remarks>
public static class AnnotationPalette
{
    /// <summary>代表色の既定の列数（40 色なら 8 列 × 5 行に収まり、スクロールせずに選べる）。</summary>
    public const int DefaultPaletteColumns = AnnotationPaletteCatalog.DefaultPaletteColumns;

    /// <summary>代表色のタブの題名。</summary>
    public const string PaletteTabTitle = AnnotationPaletteCatalog.PaletteTabTitle;

    /// <summary>代表色の並び（色名と保存値 <c>#AARRGGBB</c>）。</summary>
    /// <remarks>定義はコアが単一所有する（<see cref="AnnotationPaletteCatalog.RepresentativeColors"/>）。</remarks>
    public static IReadOnlyList<AnnotationPaletteColor> RepresentativeColors { get; } =
        AnnotationPaletteCatalog.RepresentativeColors;
    /// <summary>代表色のタブ（■ だけを指定した列数で並べる）を作る。</summary>
    /// <param name="columns">■ の列数（1 以上。 既定は <see cref="DefaultPaletteColumns"/>）。</param>
    /// <returns>■ だけの格子のタブ。</returns>
    public static ColorSelectorCustomTab CreatePaletteTab(int columns = DefaultPaletteColumns)
        => new(
            PaletteTabTitle,
            [.. RepresentativeColors.Select(color => new ColorSelectorCustomColor(color.Name, color.Value))],
            Math.Max(1, columns));

    /// <summary>色の選択を注釈向けの構成にする（「代表色」と「ColorPicker」のタブだけを出す）。</summary>
    /// <param name="selector">構成する色の選択。</param>
    /// <param name="columns">■ の格子の列数。</param>
    /// <param name="showPicker">カラーピッカーのタブも出すかどうか。</param>
    /// <remarks>テーマの色・色名のタブは隠す（注釈の色は代表色から選ぶ。 設定画面は <see cref="ConfigureAll"/>）。</remarks>
    public static void Configure(
        ColorSelectorControl selector, int columns = DefaultPaletteColumns, bool showPicker = false)
    {
        ArgumentNullException.ThrowIfNull(selector);

        selector.ShowCustomTab = true;
        selector.CustomTabs = [CreatePaletteTab(columns)];
        selector.ShowThemeColorTab = false;
        selector.ShowNamedColorTab = false;
        selector.ShowPickerTab = showPicker;
    }

    /// <summary>色の選択を「代表色 + 標準のタブ」の構成にする（設定画面向け）。</summary>
    /// <param name="selector">構成する色の選択。</param>
    /// <param name="columns">■ の格子の列数。</param>
    public static void ConfigureAll(ColorSelectorControl selector, int columns = DefaultPaletteColumns)
    {
        ArgumentNullException.ThrowIfNull(selector);

        selector.ShowCustomTab = true;
        selector.CustomTabs = [CreatePaletteTab(columns)];
        selector.ShowThemeColorTab = true;
        selector.ShowNamedColorTab = true;
        selector.ShowPickerTab = true;
    }
}
