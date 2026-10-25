using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace EsUtil.Ui.WinUI3.ColumnPacking.Sample;

/// <summary>デバッグ表示の要素を画面へ取り付けるための補助。</summary>
/// <remarks>
/// サンプルだけが使う補助であり、コントロール本体は持ちません（要素の重ね方はホストが決めます）。
/// </remarks>
internal static class Helper
{
    /// <summary>指定した要素の祖先のうち、最初に見つかった <see cref="Panel"/> へ要素を追加する。</summary>
    /// <param name="element">基準となる要素。</param>
    /// <param name="elementToAdd">追加する要素。 null の場合は何もしない。</param>
    /// <param name="zIndex">重なり順（既定は 1000）。</param>
    public static void AttachElementToAncestor(DependencyObject element, UIElement? elementToAdd, int zIndex = 1000)
    {
        if (elementToAdd is null)
        {
            return;
        }

        var parentPanel = FindAncestor<Panel>(element);
        if (parentPanel is not null && !parentPanel.Children.Contains(elementToAdd))
        {
            parentPanel.Children.Add(elementToAdd);
            Canvas.SetZIndex(elementToAdd, zIndex);
        }
    }

    /// <summary>指定した型の祖先を、ビジュアル ツリーを上へ辿って探す。</summary>
    /// <typeparam name="T">探す祖先の型。</typeparam>
    /// <param name="current">探索を始める要素。</param>
    /// <returns>最初に見つかった祖先。 見つからない場合は null。</returns>
    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is T matched)
            {
                return matched;
            }
        }

        return null;
    }
}
