using EsUtil.Platform.Core.Imaging;
using EsUtil.Feature.Annotation.Presenter.Editor;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈の色選択で使う代表色（40 色。 WinForms 版）。</summary>
/// <remarks>
/// <b>定義はコアが単一所有する</b>（`AnnotationPaletteCatalog`）。 WinForms には色選択コントロールが
/// 無いため、代表色を<b>メニューに並べる</b>（任意の色は色ダイアログで選ぶ）。
/// </remarks>
public static class AnnotationPalette
{
    /// <summary>代表色の並び（色名と色）を取得する。</summary>
    public static IReadOnlyList<(string Name, Color Value)> RepresentativeColors { get; } = Build();

    /// <summary>コアの定義（色名と保存値）から色を作る。</summary>
    /// <returns>代表色の並び。</returns>
    private static IReadOnlyList<(string Name, Color Value)> Build()
    {
        var colors = new List<(string, Color)>(AnnotationPaletteCatalog.RepresentativeColors.Count);
        foreach (var entry in AnnotationPaletteCatalog.RepresentativeColors)
        {
            if (DrawingColorExtensions.TryParseHexText(entry.Value, out var color))
            {
                colors.Add((entry.Name, color));
            }
        }

        return colors;
    }
}
