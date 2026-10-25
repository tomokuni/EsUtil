using EsUtil.Ui.WinUI3.Interop.Drawing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

namespace EsUtil.Ui.WinUI3.Marquee.Internal;

/// <summary><see cref="DrawList"/> を XAML の矩形へ反映する層。</summary>
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
/// <b>本コントロールだけが使う複製</b>（独立パッケージのため。 .github/依存関係.md §4.1）。
/// </remarks>
internal sealed partial class MarqueeShapeLayer : Canvas
{
    /// <summary>矩形を表す要素（追加順に使い回す）。</summary>
    private readonly List<Rectangle> _rectangles = [];

    /// <summary>反映した矩形の件数を取得する。</summary>
    /// <value>表示中の矩形の件数。</value>
    public int AppliedCount { get; private set; }

    /// <summary><see cref="DrawList"/> の内容を反映する。</summary>
    /// <param name="list">反映する描画リスト。 null 不可。</param>
    /// <param name="pixelToDip">物理ピクセル → DIP の係数（1 / スケーリング係数）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> が null の場合。</exception>
    /// <remarks>
    /// <b>値が変わった項目だけを書く</b>: 要素のプロパティを書くたびに XAML の測定・配置が
    /// 無効になり、ドラッグ中は毎フレーム数十個の要素へ 4 つの値を書いていた（実測 2026-10-25）。
    /// 前フレームと同じ値なら書かない（位置だけ動く項目・大きさだけ変わる項目をそれぞれ最小の更新にする）。
    /// </remarks>
    public void Apply(DrawList list, double pixelToDip)
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
                // Visibility の書き込みも測定を無効にするため、既に隠れている場合は書かない
                if (rectangle.Visibility != Visibility.Collapsed)
                {
                    rectangle.Visibility = Visibility.Collapsed;
                }

                continue;
            }

            var item = rects[i];
            var x = item.Bounds.X * pixelToDip;
            var y = item.Bounds.Y * pixelToDip;
            var width = item.Bounds.Width * pixelToDip;
            var height = item.Bounds.Height * pixelToDip;
            if (GetLeft(rectangle) != x)
            {
                SetLeft(rectangle, x);
            }

            if (GetTop(rectangle) != y)
            {
                SetTop(rectangle, y);
            }

            if (rectangle.Width != width)
            {
                rectangle.Width = width;
            }

            if (rectangle.Height != height)
            {
                rectangle.Height = height;
            }

            // 色は ARGB ごとに 1 つだけ作る（毎フレーム作ると WinRT の割り当てが積み上がり、
            // ドラッグ中の描画が重くなる。 実測 2026-10-25）
            var brush = SolidBrushCache.Get(item.Color);
            if (!ReferenceEquals(rectangle.Fill, brush))
            {
                rectangle.Fill = brush;
            }

            if (rectangle.Visibility != Visibility.Visible)
            {
                rectangle.Visibility = Visibility.Visible;
            }
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

}
