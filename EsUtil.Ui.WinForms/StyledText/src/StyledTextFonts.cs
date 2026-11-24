using System.Drawing.Text;

namespace EsUtil.Ui.WinForms.StyledText;

/// <summary>本文の書式で選べるフォントの一覧（WinForms 版）。</summary>
/// <remarks>
/// <b>並べ方は計算層が単一所有する</b>（<see cref="TextFontCatalog"/>）。 本クラスは環境のフォントの列挙だけを
/// 行う（列挙は UI フレームワークに依存する）。 一覧は起動時に 1 度だけ作る（列挙は重い）。
/// </remarks>
public static class StyledTextFonts
{
    /// <summary>選べるフォント名の一覧を取得する。</summary>
    public static IReadOnlyList<string> All { get; } = Load();

    /// <summary>環境のフォント名を列挙して並べ替える。</summary>
    /// <returns>並べ替えたフォント名。</returns>
    private static IReadOnlyList<string> Load()
    {
        using var installed = new InstalledFontCollection();
        return TextFontCatalog.Order(installed.Families.Select(family => family.Name));
    }
}