using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace EsUtil.Ui.WinUI3.Loupe.Internal;

/// <summary><see cref="LoupeRectList"/> を XAML の矩形へ反映する層。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く内容（どの矩形をどの色で、どの順に）は呼び出し側が単一所有する。<br/>
/// 本クラスは受け取った矩形を <see cref="Rectangle"/> として反映するだけで、計算・判断を持たない。<br/>
/// <br/>
/// <b>単位</b>: 矩形は<b>物理ピクセル</b>で受け取り、<c>pixelToDip</c> の係数で DIP へ換算して配置する<br/>
/// （描画計画が物理ピクセルで解決されているため、換算は 1 か所に閉じる）。<br/>
/// <br/>
/// <b>要素を使い回す</b>: 反映のたびに要素を作ると、パンのたびに数百の要素が割り当てられる。<br/>
/// 件数が足りないときだけ追加し、余った要素は隠す。
/// <para/>
/// <b>複製元: EsUtil.SaveEvidence.Platform.WinUI3.Drawing.RectShapeLayer</b>（単独パッケージとして配布するための意図的な複製。 依存関係 §4.1）。
/// </remarks>
internal sealed partial class LoupeShapeLayer : Canvas
{
    /// <summary>矩形を表す要素（追加順に使い回す）。</summary>
    private readonly List<Rectangle> _rectangles = [];

    /// <summary>反映した矩形の件数を取得する。</summary>
    /// <value>表示中の矩形の件数。</value>
    public int AppliedCount { get; private set; }

    /// <summary><see cref="LoupeRectList"/> の内容を反映する。</summary>
    /// <param name="list">反映する描画リスト。 null 不可。</param>
    /// <param name="pixelToDip">物理ピクセル → DIP の係数（1 / スケーリング係数）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> が null の場合。</exception>
    public void Apply(LoupeRectList list, double pixelToDip)
    {
        ArgumentNullException.ThrowIfNull(list);

        var rects = list.Rects;
        while (_rectangles.Count < rects.Count)
        {
            var rectangle = new Rectangle { IsHitTestVisible = false };
            _rectangles.Add(rectangle);
            Children.Add(rectangle);
        }

        for (var i = 0; i < _rectangles.Count; i++)
        {
            var rectangle = _rectangles[i];
            if (i >= rects.Count)
            {
                rectangle.Visibility = Visibility.Collapsed;
                continue;
            }

            var item = rects[i];
            SetLeft(rectangle, item.X * pixelToDip);
            SetTop(rectangle, item.Y * pixelToDip);
            rectangle.Width = item.Width * pixelToDip;
            rectangle.Height = item.Height * pixelToDip;
            rectangle.Fill = new SolidColorBrush(ToColor(item.ColorArgb));
            rectangle.Visibility = Visibility.Visible;
        }

        AppliedCount = rects.Count;
    }

    /// <summary>表示中の矩形をすべて隠す。</summary>
    public void Clear()
    {
        foreach (var rectangle in _rectangles)
        {
            rectangle.Visibility = Visibility.Collapsed;
        }

        AppliedCount = 0;
    }

    /// <summary>色を WinUI の色へ変換する。</summary>
    /// <param name="argb">色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>変換した色。</returns>
    private static Color ToColor(uint argb) => Color.FromArgb(
        (byte)((argb >> 24) & 0xFF),
        (byte)((argb >> 16) & 0xFF),
        (byte)((argb >> 8) & 0xFF),
        (byte)(argb & 0xFF));
}
