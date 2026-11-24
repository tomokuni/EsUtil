using Avalonia.Media;

namespace EsUtil.Ui.AvaloniaUI.StyledText;

/// <summary>本文の書式で選べるフォントの一覧（Avalonia UI 版）。</summary>
/// <remarks>
/// <b>並べ方は計算層が単一所有する</b>（<see cref="TextFontCatalog"/>）。 本クラスは環境のフォントの列挙だけを
/// 行う（列挙は UI フレームワークに依存する）。 一覧は起動時に 1 度だけ作る（列挙は重い）。
/// </remarks>
public static class StyledTextFonts
{
    /// <summary>選べるフォント名の一覧を取得する。</summary>
    public static IReadOnlyList<string> All { get; } =
        TextFontCatalog.Order(FontManager.Current.SystemFonts.Select(font => font.Name));
}