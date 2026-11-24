namespace EsUtil.Ui.WinUI3.StyledText;

/// <summary>本文の書式で選べるフォントの一覧（WinUI 3 版）。</summary>
/// <remarks>
/// <b>並べ方は計算層が単一所有する</b>（<c>TextFontCatalog</c>）。 WinUI 3 はインストール済みフォントの
/// 列挙を公開していないため、代表的な一覧（<c>TextFontCatalog.FallbackNames</c>）を渡す。
/// </remarks>
public static class StyledTextFonts
{
    /// <summary>選べるフォント名の一覧を取得する。</summary>
    public static IReadOnlyList<string> All { get; } = TextFontCatalog.Order(TextFontCatalog.FallbackNames);
}