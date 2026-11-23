namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>本文の書式で選べるフォントの一覧（MewUI 版）。</summary>
/// <remarks>
/// <b>並べ方はコアが単一所有する</b>（`AnnotationFontCatalog`）。 MewUI はインストール済みフォントの
/// 列挙を公開していないため、代表的な一覧（`AnnotationFontCatalog.FallbackNames`）を渡す。
/// </remarks>
public static class AnnotationFonts
{
    /// <summary>選べるフォント名の一覧を取得する。</summary>
    public static IReadOnlyList<string> All { get; } =
        AnnotationFontCatalog.Order(AnnotationFontCatalog.FallbackNames);
}