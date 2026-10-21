using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Interop.Drawing;

/// <summary><see cref="DrawList"/> を XAML の図形へ反映する層。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く内容の判断（どの矩形をどの色で、どの順に）は <see cref="DrawList"/> を持つ呼び出し側が<br/>
/// 単一所有する。 本クラスは受け取った矩形を要素へ<see cref="Rectangle"/>／<see cref="Border"/> として反映するだけで、<br/>
/// 計算・判断を持たない。<br/>
/// <br/>
/// <b>要素を使い回す</b>: 反映のたびに要素を作ると、パンのたびに数百の要素が割り当てられる。<br/>
/// 件数が足りないときだけ追加し、余った要素は <see cref="UIElement.Visibility"/> で隠す。<br/>
/// <br/>
/// <b>座標</b>: 本クラスは <see cref="Canvas"/> を継承するため、子要素の位置は <c>Canvas.Left</c> / <c>Canvas.Top</c> で表す。<br/>
/// 矩形の単位（DIP か物理ピクセルか）は呼び出し側が決める（本クラスは変換しない）。
/// </remarks>
public sealed partial class RectShapeLayer : Canvas
{
    /// <summary>矩形を表す要素（追加順に使い回す）。</summary>
    private readonly List<Rectangle> _rectangles = [];

    /// <summary>角丸矩形を表す要素（追加順に使い回す）。</summary>
    private readonly List<Border> _borders = [];

    /// <summary><see cref="DrawList"/> の内容を反映する。</summary>
    /// <param name="list">反映する描画リスト。 null 不可。</param>
    /// <remarks>
    /// 1. 件数が足りなければ要素を追加する（追加した要素は子として登録する）。<br/>
    /// 2. 先頭から順に位置・大きさ・色を写す（追加順が描画順になる）。<br/>
    /// 3. 余った要素は隠す（消さない。 次の反映で再利用する）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> が null の場合。</exception>
    public void Apply(DrawList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        ApplyRects(list.Rects);
        ApplyRoundRects(list.RoundRects);
    }

    /// <summary>矩形を反映する。</summary>
    /// <param name="rects">反映する矩形の列。</param>
    private void ApplyRects(IReadOnlyList<DrawRect> rects)
    {
        EnsureRectangles(rects.Count);

        for (var i = 0; i < _rectangles.Count; i++)
        {
            var rectangle = _rectangles[i];
            if (i >= rects.Count)
            {
                rectangle.Visibility = Visibility.Collapsed;
                continue;
            }

            var item = rects[i];
            SetBounds(rectangle, item.Bounds);
            rectangle.Width = item.Bounds.Width;
            rectangle.Height = item.Bounds.Height;

            // 色は共有のブラシにしない（要素ごとに色が異なるため）
            rectangle.Fill = new SolidColorBrush(ToColor(item.Color));
            rectangle.Visibility = Visibility.Visible;
        }
    }

    /// <summary>角丸矩形を反映する。</summary>
    /// <param name="roundRects">反映する角丸矩形の列。</param>
    private void ApplyRoundRects(IReadOnlyList<DrawRoundRect> roundRects)
    {
        EnsureBorders(roundRects.Count);

        for (var i = 0; i < _borders.Count; i++)
        {
            var border = _borders[i];
            if (i >= roundRects.Count)
            {
                border.Visibility = Visibility.Collapsed;
                continue;
            }

            var item = roundRects[i];
            SetBounds(border, item.Bounds);
            border.Width = item.Bounds.Width;
            border.Height = item.Bounds.Height;
            border.CornerRadius = new CornerRadius(Math.Max(0, item.CornerRadius));
            border.Background = new SolidColorBrush(ToColor(item.Color));
            border.Visibility = Visibility.Visible;
        }
    }

    /// <summary>矩形の要素を必要な数まで用意する。</summary>
    /// <param name="count">必要な件数。</param>
    private void EnsureRectangles(int count)
    {
        while (_rectangles.Count < count)
        {
            var rectangle = new Rectangle { IsHitTestVisible = false };
            _rectangles.Add(rectangle);
            Children.Add(rectangle);
        }
    }

    /// <summary>角丸矩形の要素を必要な数まで用意する。</summary>
    /// <param name="count">必要な件数。</param>
    /// <remarks>角丸矩形は他の矩形より手前に描く（追加順で後になる）。</remarks>
    private void EnsureBorders(int count)
    {
        while (_borders.Count < count)
        {
            var border = new Border { IsHitTestVisible = false };
            _borders.Add(border);
            Children.Add(border);
        }
    }

    /// <summary>要素の位置を設定する。</summary>
    /// <param name="element">対象の要素。</param>
    /// <param name="bounds">矩形。</param>
    private static void SetBounds(FrameworkElement element, EsRect bounds)
    {
        SetLeft(element, bounds.X);
        SetTop(element, bounds.Y);
    }

    /// <summary>UI 非依存の色を WinUI の色へ変換する。</summary>
    /// <param name="color">変換する色。</param>
    /// <returns>変換した色。</returns>
    /// <remarks>コアの <see cref="EsRect"/> と同様、色も UI 非依存の <see cref="DrawingColor"/> で受け取る。</remarks>
    private static Windows.UI.Color ToColor(DrawingColor color)
        => Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
}
