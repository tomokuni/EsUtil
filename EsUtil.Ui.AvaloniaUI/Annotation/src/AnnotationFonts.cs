using Avalonia.Media;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>本文の書式で選べるフォントの一覧（Avalonia UI 版）。</summary>
/// <remarks>
/// <b>並べ方はコアが単一所有する</b>（`AnnotationFontCatalog`）。 本クラスは環境のフォントの列挙だけを行う
/// （列挙は UI フレームワークに依存する）。 一覧は起動時に 1 度だけ作る（列挙は重い）。
/// </remarks>
public static class AnnotationFonts
{
    /// <summary>選べるフォント名の一覧を取得する。</summary>
    public static IReadOnlyList<string> All { get; } =
        AnnotationFontCatalog.Order(FontManager.Current.SystemFonts.Select(font => font.Name));
}