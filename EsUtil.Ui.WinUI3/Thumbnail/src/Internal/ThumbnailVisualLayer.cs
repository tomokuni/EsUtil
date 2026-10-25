using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.WinUI3.Interop.Drawing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace EsUtil.Ui.WinUI3.Thumbnail.Internal;

/// <summary>描画計画（<see cref="ThumbnailDrawPlan"/>）の背景・影・画像を XAML の要素で表す層。</summary>
/// <remarks>
/// <b>位置付け</b>: 矩形・色・寸法は計画に解決済みである。 本クラスは「背景 → 影 → 画像」の順に要素を置くだけで、<br/>
/// 計算・判断を持たない（順序は <see cref="ThumbnailDrawPlan.Layers"/> が単一所有する）。<br/>
/// <br/>
/// <b>影はグラデーションで塗る</b>: 影の計画（<see cref="IThumbnailShadowPlan.Edges"/> /
/// <see cref="IThumbnailShadowPlan.CornerStops"/>）を<br/>
/// 4 辺の線形グラデーションと 4 隅の放射グラデーションへ写す（<see cref="ThumbnailShadowBrushes"/>）。<br/>
/// 1 画素の帯を要素にすると 1 件あたり数百個になり、一覧の初回描画が待たされる（実測 2026-10-25:<br/>
/// 31 件で 258 ms → 8 要素に減らして 36 ms）。<br/>
/// <br/>
/// <b>要素を使い回す</b>: 一覧は項目ごとに本クラスを持ち、スクロールのたびに作り直されるため、<br/>
/// 反映のたびに要素を作らない（背景 1 個・影 8 個・画像 1 個を固定で持つ）。
/// </remarks>
internal sealed partial class ThumbnailVisualLayer : Canvas
{
    /// <summary>背景の矩形。</summary>
    private readonly Rectangle _background = new() { IsHitTestVisible = false };

    /// <summary>画像を表示する要素。</summary>
    private readonly Image _image = new() { IsHitTestVisible = false, Stretch = Stretch.Fill };

    /// <summary>影の辺（上・右・下・左）の矩形。</summary>
    private readonly Rectangle[] _shadowEdges =
        [new() { IsHitTestVisible = false }, new() { IsHitTestVisible = false },
         new() { IsHitTestVisible = false }, new() { IsHitTestVisible = false }];

    /// <summary>影の隅（左上・右上・左下・右下）の矩形。</summary>
    private readonly Rectangle[] _shadowCorners =
        [new() { IsHitTestVisible = false }, new() { IsHitTestVisible = false },
         new() { IsHitTestVisible = false }, new() { IsHitTestVisible = false }];

    /// <summary>現在表示している画像（同じ画像を渡し直さないための保持値）。</summary>
    private EsReadOnlyBitmapCache? _cache;

    /// <summary>表示の層を初期化する。</summary>
    /// <remarks>子の順序は「背景 → 影 → 画像」である。</remarks>
    public ThumbnailVisualLayer()
    {
        IsHitTestVisible = false;
        Children.Add(_background);

        foreach (var element in _shadowEdges)
        {
            Children.Add(element);
        }

        foreach (var element in _shadowCorners)
        {
            Children.Add(element);
        }

        Children.Add(_image);
    }

    /// <summary>描画計画を要素へ反映する。</summary>
    /// <param name="plan">反映する計画。</param>
    /// <param name="image">表示する画像。 null の場合は画像を表示しない。</param>
    /// <param name="pixelToDip">物理ピクセル → DIP の係数（1 / スケーリング係数）。</param>
    public void Apply(ThumbnailDrawPlan plan, EsReadOnlyBitmap? image, double pixelToDip)
    {
        _background.Width = plan.Slot.Width;
        _background.Height = plan.Slot.Height;
        _background.Fill = plan.BackgroundArgb == 0
            ? null
            : SolidBrushCache.Get(plan.BackgroundArgb);

        // 画像の矩形は項目の原点（Slot）からの相対位置で置く
        var imageRect = new Rect(
            plan.Image.X - plan.Slot.X,
            plan.Image.Y - plan.Slot.Y,
            plan.Image.Width,
            plan.Image.Height);

        ApplyImage(image, imageRect);
        ApplyShadow(plan, imageRect, pixelToDip);
    }

    /// <summary>画像を反映する。</summary>
    /// <param name="image">表示する画像。</param>
    /// <param name="rect">配置する矩形（DIP）。</param>
    private void ApplyImage(EsReadOnlyBitmap? image, Rect rect)
    {
        if (image is not { IsDisposed: false } || rect.Width <= 0 || rect.Height <= 0)
        {
            _image.Source = null;
            return;
        }

        _cache ??= new EsReadOnlyBitmapCache();
        _image.Source = _cache.Get(image);
        _image.Width = rect.Width;
        _image.Height = rect.Height;
        SetLeft(_image, rect.X);
        SetTop(_image, rect.Y);
    }

    /// <summary>影を反映する。</summary>
    /// <param name="plan">描画計画。</param>
    /// <param name="rect">画像の矩形（DIP。 項目の原点からの相対位置）。</param>
    /// <param name="pixelToDip">物理ピクセル → DIP の係数。</param>
    /// <remarks>
    /// <b>要素は 8 個で足りる</b>: 4 辺は線形グラデーション、4 隅は放射グラデーションの矩形 1 個で塗る<br/>
    /// （1 画素の帯を要素にすると 1 件あたり数百個になり、一覧の初回描画が待たされる）。<br/>
    /// <b>画像の端の 1 画素まで覆う</b>: 辺の矩形を画像の内側へ 1 画素だけ延ばす。 画像が端の画素を<br/>
    /// 塗り残したときに背景（白）が 1 画素の線として見えるのを防ぐ（画像より背面なので画像が隠す）。
    /// </remarks>
    private void ApplyShadow(ThumbnailDrawPlan plan, Rect rect, double pixelToDip)
    {
        if (plan.Shadow is not { } shadow || rect.Width <= 0 || rect.Height <= 0 || pixelToDip <= 0)
        {
            HideShadow();
            return;
        }

        var brushes = ThumbnailShadowBrushes.Resolve(shadow);
        var extent = shadow.BlurRadius;
        var inner = pixelToDip;

        Place(_shadowEdges[0], rect.X, rect.Y - extent, rect.Width, extent + inner, brushes.Top);
        Place(_shadowEdges[1], rect.X + rect.Width - inner, rect.Y, extent + inner, rect.Height, brushes.Right);
        Place(_shadowEdges[2], rect.X, rect.Y + rect.Height - inner, rect.Width, extent + inner, brushes.Bottom);
        Place(_shadowEdges[3], rect.X - extent, rect.Y, extent + inner, rect.Height, brushes.Left);

        Place(_shadowCorners[0], rect.X - extent, rect.Y - extent, extent, extent, brushes.TopLeft);
        Place(_shadowCorners[1], rect.X + rect.Width, rect.Y - extent, extent, extent, brushes.TopRight);
        Place(_shadowCorners[2], rect.X - extent, rect.Y + rect.Height, extent, extent, brushes.BottomLeft);
        Place(_shadowCorners[3], rect.X + rect.Width, rect.Y + rect.Height, extent, extent, brushes.BottomRight);
    }

    /// <summary>影の要素を 1 つ配置する。</summary>
    /// <param name="element">配置する要素。</param>
    /// <param name="x">左端（DIP。 項目の原点からの相対位置）。</param>
    /// <param name="y">上端（DIP。 項目の原点からの相対位置）。</param>
    /// <param name="width">幅（DIP）。</param>
    /// <param name="height">高さ（DIP）。</param>
    /// <param name="brush">塗るブラシ。</param>
    private static void Place(Rectangle element, double x, double y, double width, double height, Brush brush)
    {
        SetLeft(element, x);
        SetTop(element, y);
        element.Width = width;
        element.Height = height;
        element.Fill = brush;
        element.Visibility = Visibility.Visible;
    }

    /// <summary>影を隠す。</summary>
    private void HideShadow()
    {
        foreach (var element in _shadowEdges)
        {
            element.Visibility = Visibility.Collapsed;
        }

        foreach (var element in _shadowCorners)
        {
            element.Visibility = Visibility.Collapsed;
        }
    }

}
