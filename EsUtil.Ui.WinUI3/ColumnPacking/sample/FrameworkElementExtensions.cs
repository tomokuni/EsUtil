using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace EsUtil.Ui.WinUI3.ColumnPacking.Sample;

/// <summary>要素の内側の大きさを求める拡張メソッド（サンプルのデバッグ表示で使う）。</summary>
internal static class FrameworkElementExtensions
{
    /// <summary>要素の内側の大きさ（余白と枠線を除いた大きさ）を求める。</summary>
    /// <param name="element">対象の要素。</param>
    /// <returns>内側の大きさ。</returns>
    public static Size GetInnerSize(this FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var padding = GetPadding(element);
        var border = GetBorderThickness(element);
        return new Size(
            element.ActualWidth - (padding.Left + padding.Right + border.Left + border.Right),
            element.ActualHeight - (padding.Top + padding.Bottom + border.Top + border.Bottom));
    }

    /// <summary>枠線の太さを求める。</summary>
    /// <param name="element">対象の要素。</param>
    /// <returns>枠線の太さ（枠線を持たない要素は 0）。</returns>
    public static Thickness GetBorderThickness(this FrameworkElement element)
        => element switch
        {
            Border border => border.BorderThickness,
            _ => default,
        };

    /// <summary>余白を求める。</summary>
    /// <param name="element">対象の要素。</param>
    /// <returns>余白（余白を持たない要素は 0）。</returns>
    public static Thickness GetPadding(this FrameworkElement element)
        => element switch
        {
            Control control => control.Padding,
            Border border => border.Padding,
            _ => default,
        };

    /// <summary>余白を表示用の文字列へ整形する。</summary>
    /// <param name="thickness">整形する余白。</param>
    /// <returns>4 辺が同じなら 1 つの値、上下と左右が同じなら 2 つの値、それ以外は 4 辺の値。</returns>
    public static string Format(this Thickness thickness)
        => thickness is { Left: var left, Top: var top, Right: var right, Bottom: var bottom }
            && left == top && right == bottom && left == right
                ? $"{left:F1}"
                : left == right && top == bottom
                    ? $"{left:F1}, {top:F1}"
                    : $"L:{left:F1}, T:{top:F1}, R:{right:F1}, B:{bottom:F1}";

    /// <summary>大きさを表示用の文字列へ整形する。</summary>
    /// <param name="size">整形する大きさ。</param>
    /// <param name="separator">幅と高さの区切り。</param>
    /// <returns>幅と高さの文字列（未確定の値は <c>Auto</c>）。</returns>
    public static string Format(this Size size, string separator = ", ")
        => $"{(double.IsNaN(size.Width) ? "Auto" : $"{size.Width:F1}")}{separator}{(double.IsNaN(size.Height) ? "Auto" : $"{size.Height:F1}")}";
}
