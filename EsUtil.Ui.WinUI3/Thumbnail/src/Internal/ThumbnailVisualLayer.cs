using EsUtil.Platform.Core.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace EsUtil.Ui.WinUI3.Thumbnail.Internal;

/// <summary>描画計画（<see cref="ThumbnailDrawPlan"/>）の背景・影・画像を XAML の要素で表す層。</summary>
/// <remarks>
/// <b>位置付け</b>: 矩形・色・寸法は計画に解決済みである。 本クラスは「背景 → 影 → 画像」の順に要素を置くだけで、<br/>
/// 計算・判断を持たない（順序は <see cref="ThumbnailDrawPlan.Layers"/> が単一所有する）。<br/>
/// <br/>
/// <b>影は 1 画素の帯で塗る</b>: 影の計画（<see cref="IThumbnailShadowPlan.BuildDeviceBands"/>）が返す帯を<br/>
/// 矩形として置く（グラデーション ブラシの端の扱いに依存せず、他の UI 版と同じ結果になる）。<br/>
/// <br/>
/// <b>要素を使い回す</b>: 一覧は項目ごとに本クラスを持ち、スクロールのたびに作り直されるため、<br/>
/// 反映のたびに要素を作らないよう件数が足りないときだけ追加する。
/// </remarks>
internal sealed partial class ThumbnailVisualLayer : Canvas
{
    /// <summary>背景の矩形。</summary>
    private readonly Rectangle _background = new() { IsHitTestVisible = false };

    /// <summary>画像を表示する要素。</summary>
    private readonly Image _image = new() { IsHitTestVisible = false, Stretch = Stretch.Fill };

    /// <summary>影の帯を表す要素（件数が変わらなければ使い回す）。</summary>
    private readonly List<Rectangle> _shadowBands = [];

    /// <summary>現在表示している画像（同じ画像を渡し直さないための保持値）。</summary>
    private EsReadOnlyBitmapCache? _cache;

    /// <summary>表示の層を初期化する。</summary>
    /// <remarks>子の順序は「背景 → 影の帯 → 画像」である。</remarks>
    public ThumbnailVisualLayer()
    {
        IsHitTestVisible = false;
        Children.Add(_background);
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
            : new SolidColorBrush(ToColor(plan.BackgroundArgb));

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

    /// <summary>影の帯を反映する。</summary>
    /// <param name="plan">描画計画。</param>
    /// <param name="rect">画像の矩形（DIP。 項目の原点からの相対位置）。</param>
    /// <param name="pixelToDip">物理ピクセル → DIP の係数。</param>
    private void ApplyShadow(ThumbnailDrawPlan plan, Rect rect, double pixelToDip)
    {
        if (plan.Shadow is not { } shadow || rect.Width <= 0 || rect.Height <= 0)
        {
            HideShadowBands(0);
            return;
        }

        var scaling = pixelToDip > 0 ? 1 / pixelToDip : 1;

        // BuildDeviceBands の入力は DIP、出力は物理ピクセルである（IThumbnailShadowPlan の契約）。
        // 入力へ倍率を掛けると影だけが画面外へ飛ぶ（実測 2026-10-25: 一覧の影が大きな四角として
        // サムネイルの左上へずれて表示された）
        var bands = shadow.BuildDeviceBands(rect.X, rect.Y, rect.Width, rect.Height, scaling);

        EnsureShadowBands(bands.Count);
        for (var i = 0; i < _shadowBands.Count; i++)
        {
            var element = _shadowBands[i];
            if (i >= bands.Count)
            {
                element.Visibility = Visibility.Collapsed;
                continue;
            }

            var band = bands[i];
            SetLeft(element, band.X * pixelToDip);
            SetTop(element, band.Y * pixelToDip);
            element.Width = band.Width * pixelToDip;
            element.Height = band.Height * pixelToDip;
            element.Fill = new SolidColorBrush(ToColor(band.Argb));
            element.Visibility = Visibility.Visible;
        }
    }

    /// <summary>影の帯の要素を必要な数まで用意する。</summary>
    /// <param name="count">必要な件数。</param>
    /// <remarks>影は画像より背面に置く（追加順で画像より先にする）。</remarks>
    private void EnsureShadowBands(int count)
    {
        while (_shadowBands.Count < count)
        {
            var element = new Rectangle { IsHitTestVisible = false };
            _shadowBands.Add(element);
            Children.Insert(Math.Min(Children.Count - 1, 1 + _shadowBands.Count), element);
        }
    }

    /// <summary>影の帯を隠す。</summary>
    /// <param name="count">表示する件数。</param>
    private void HideShadowBands(int count)
    {
        for (var i = count; i < _shadowBands.Count; i++)
        {
            _shadowBands[i].Visibility = Visibility.Collapsed;
        }
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
