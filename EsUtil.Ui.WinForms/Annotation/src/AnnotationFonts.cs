using System.Drawing.Text;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>本文の書式で選べるフォントの一覧（WinForms 版）。</summary>
/// <remarks>
/// <b>並べ方はコアが単一所有する</b>（`AnnotationFontCatalog`）。 本クラスは環境のフォントの列挙だけを行う
/// （列挙は OS の API に依存する）。 一覧は起動時に 1 度だけ作る（列挙は重い）。
/// </remarks>
public static class AnnotationFonts
{
    /// <summary>選べるフォント名の一覧を取得する。</summary>
    public static IReadOnlyList<string> All { get; } = Build();

    /// <summary>環境のフォントを列挙して並べ替える。</summary>
    /// <returns>選べるフォント名の一覧。</returns>
    private static IReadOnlyList<string> Build()
    {
        using var installed = new InstalledFontCollection();
        return AnnotationFontCatalog.Order(installed.Families.Select(family => family.Name));
    }
}