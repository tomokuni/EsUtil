using System.Numerics;
using EsUtil.Platform.Core.Imaging;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace EsUtil.Ui.WinUI3.Loupe.Internal;

/// <summary><see cref="EsReadOnlyBitmap"/> の一部を拡大して表示する層（Composition のサーフェス）。</summary>
/// <remarks>
/// <b>拡大と切り出しを GPU に任せる</b>: 画素処理のコードを持たず、表示のたびに画素を書き換えない。<br/>
/// 元画像は無圧縮 BMP（<see cref="LoupeBitmapStream"/>) として 1 度だけ取り込み、拡大率と位置は<br/>
/// スプライトの拡大・移動で表す（拡大率を変えても取り込み直さない）。<br/>
/// <br/>
/// <b>最近傍で拡大する</b>: ルーペは撮影画像の画素を確認する用途であるため、補間で色を混ぜない<br/>
/// （移行前の Skia の描画と同じ見た目になる）。
/// <para/>
/// <b>複製元: EsUtil.SaveEvidence.Platform.WinUI3.Drawing.CompositionImageView</b>（単独パッケージとして配布するための意図的な複製。 依存関係 §4.1）。
/// </remarks>
internal sealed partial class LoupeImageView : Grid
{
    /// <summary>画像を載せるスプライト。</summary>
    private readonly SpriteVisual _sprite;

    /// <summary>サーフェスのブラシ（画像を差し替えるたびに作り直す）。</summary>
    private CompositionSurfaceBrush? _brush;

    /// <summary>現在のサーフェス（差し替え時に破棄する）。</summary>
    private LoadedImageSurface? _loadedImage;

    /// <summary>現在表示している画像（同じ画像を渡し直さないための保持値）。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>拡大画像の表示層を初期化する。</summary>
    /// <remarks>画像が無い間はスプライトを不可視にする（下の要素がそのまま見える）。</remarks>
    public LoupeImageView()
    {
        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _sprite = compositor.CreateSpriteVisual();
        _sprite.IsVisible = false;
        ElementCompositionPreview.SetElementChildVisual(this, _sprite);

        // スプライトは要素の外へはみ出しうるため、自分の領域で切る（切り出しは GPU が行う）
        Clip = new RectangleGeometry();
        SizeChanged += (_, e) =>
        {
            if (Clip is RectangleGeometry clip)
            {
                clip.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            }
        };
    }

    /// <summary>拡大元の画像を設定する。</summary>
    /// <param name="image">拡大元の 100% 画像。 null の場合は何も表示しない。</param>
    /// <remarks>同じ画像を渡し直しても取り込み直さない（取り込みには 4K で約 40ms かかる）。</remarks>
    public void SetSource(EsReadOnlyBitmap? image)
    {
        if (ReferenceEquals(_source, image))
        {
            return;
        }

        _source = image;
        if (image is not { IsDisposed: false })
        {
            _sprite.IsVisible = false;
            return;
        }

        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(LoupeBitmapStream.Build(image));
            writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            writer.DetachStream();
        }

        stream.Seek(0);

        // 以前のサーフェスを破棄してから差し替える（破棄しないと画素が解放されない）
        _loadedImage?.Dispose();
        _loadedImage = LoadedImageSurface.StartLoadFromStream(stream);
        _brush = compositor.CreateSurfaceBrush(_loadedImage);
        _brush.Stretch = CompositionStretch.Fill;
        _brush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;

        _sprite.Brush = _brush;
        _sprite.IsVisible = true;
    }

    /// <summary>拡大する領域と拡大率を設定する。</summary>
    /// <param name="sourceRectX">切り出す領域の左端（元画像ピクセル）。</param>
    /// <param name="sourceRectY">切り出す領域の上端（元画像ピクセル）。</param>
    /// <param name="destinationX">表示先の左端（本層の DIP）。</param>
    /// <param name="destinationY">表示先の上端（本層の DIP）。</param>
    /// <param name="zoomLevel">元画像ピクセル → 表示ピクセルの拡大率。</param>
    /// <param name="clientToPixelScaling">表示 DIP → 物理ピクセルの倍率。</param>
    /// <remarks>
    /// 元画像の 1 ピクセルは <c>zoomLevel / clientToPixelScaling</c> DIP になる。<br/>
    /// したがって「切り出す領域の左上を表示先の左上へ合わせる」ための移動量は<br/>
    /// <c>destination / scaling − source × 拡大率</c> で求まる（計算はここ 1 か所に閉じる）。
    /// </remarks>
    public void SetMagnification(
        double sourceRectX,
        double sourceRectY,
        double destinationX,
        double destinationY,
        double zoomLevel,
        double clientToPixelScaling)
    {
        if (_source is not { IsDisposed: false } image || clientToPixelScaling <= 0 || zoomLevel <= 0)
        {
            return;
        }

        var scale = zoomLevel / clientToPixelScaling;
        _sprite.Size = new Vector2(image.Width, image.Height);
        _sprite.Scale = new Vector3((float)scale, (float)scale, 1);
        _sprite.Offset = new Vector3(
            (float)((destinationX / clientToPixelScaling) - (sourceRectX * scale)),
            (float)((destinationY / clientToPixelScaling) - (sourceRectY * scale)),
            0);
        _sprite.IsVisible = true;
    }
}
