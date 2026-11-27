using EsUtil.Feature.Annotation.Presenter.Editor;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈の色選択で使う代表色（40 色。 WinForms 版）。</summary>
/// <remarks>
/// <b>定義はコアが単一所有する</b>（`AnnotationPaletteCatalog`）。 WinForms には色選択コントロールが
/// 無いため、代表色を<b>メニューに並べる</b>（任意の色は色ダイアログで選ぶ）。
/// </remarks>
public static class AnnotationPalette
{
    /// <summary>代表色の並び（色名と保存値 <c>#AARRGGBB</c>）を取得する。</summary>
    /// <remarks>色へ変換するのは使う側（メニューの項目を作るとき）である。</remarks>
    public static IReadOnlyList<AnnotationPaletteColor> RepresentativeColors { get; } =
        AnnotationPaletteCatalog.RepresentativeColors;
}
