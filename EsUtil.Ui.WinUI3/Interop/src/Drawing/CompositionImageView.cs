using System.Numerics;
using EsUtil.Platform.Core.Imaging;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace EsUtil.Ui.WinUI3.Interop.Drawing;

/// <summary><see cref="EsReadOnlyBitmap"/> を Composition のサーフェスとして表示するビュー。</summary>
/// <remarks>
/// <b>描画を GPU に任せる</b>: 拡大・縮小とクリップは Composition が GPU で行う。 画素処理のコードを持たず、<br/>
/// 表示のたびに画素を書き換えない（実測: 4096×2304 の取り込みが 41ms、0.25〜12 倍の拡大とパンで 4 秒 241 フレーム）。<br/>
/// <br/>
/// <b>元画像の取り込み</b>: 画素を並べた無圧縮 BMP（<see cref="BmpStreamBuilder"/>）を <see cref="LoadedImageSurface"/> へ渡す。<br/>
/// 符号化と復号の往復を伴わない（WIC も使わない）。<br/>
/// <br/>
/// <b>補間</b>: <see cref="CompositionBitmapInterpolationMode.NearestNeighbor"/> を使う。 拡大しても画素がぼやけず、<br/>
/// 撮影画像の確認という用途に合う（画素がそのまま見える）。<br/>
/// <br/>
/// <b>XAML 要素との重なり</b>: 本ビューは子要素を持たない透明な面であり、同じ親の中で先に置かれた要素の背面に合成される。<br/>
/// 図形や文字は本ビューの兄弟として<b>後</b>に置く（前面に来る）。<br/>
/// <br/>
/// <b>影</b>: <see cref="Shadow"/> に <c>Compositor</c> の <c>DropShadow</c> を設定すると、画像の輪郭へぼかした影が付く。
/// </remarks>
public sealed partial class CompositionImageView : Grid
{
    /// <summary>画像を載せるスプライト。</summary>
    private readonly SpriteVisual _sprite;

    /// <summary>サーフェスのブラシ（画像を差し替えるたびに作り直す）。</summary>
    private CompositionSurfaceBrush? _brush;

    /// <summary>現在のサーフェス（差し替え時に破棄する）。</summary>
    private LoadedImageSurface? _loadedImage;

    /// <summary>拡大の補間に最近傍を使うかどうか。</summary>
    private bool _useNearestNeighbor = true;

    /// <summary>影のぼかしの半径（DIP。 影を付けない場合は未使用）。</summary>
    private double _shadowBlurRadius;

    /// <summary>影の色（<c>0xAARRGGBB</c>。 影を付けない場合は null）。</summary>
    private uint? _shadowArgb;

    /// <summary>Composition の画像ビューを初期化する。</summary>
    /// <remarks>画像が無い間はスプライトを不可視にする（親の背景がそのまま見える）。</remarks>
    public CompositionImageView()
    {
        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _sprite = compositor.CreateSpriteVisual();
        _sprite.IsVisible = false;
        ElementCompositionPreview.SetElementChildVisual(this, _sprite);

        // スプライトは自動では大きさを持たない（0x0 のままでは何も表示されない）。
        // 割り当てられた大きさへ常に追従させる（SetBounds を呼ばずに領域いっぱいへ敷く使い方を許す）
        SizeChanged += (_, e) => ApplySize(e.NewSize.Width, e.NewSize.Height);

        // スプライトは XAML のツリーへ載る前でも設定できるが、**載る前の子ビジュアルは合成されない**
        // ことがあるため、読み込み時にも大きさを設定し直す（実測 2026-10-25: オーバーレイの背景画像が出なかった）
        Loaded += (_, _) => ApplySize(ActualWidth, ActualHeight);
    }

    /// <summary>表示する画像を取得または設定する。</summary>
    /// <value>表示する画像。 null または破棄済みの場合は何も表示しない。</value>
    /// <remarks>
    /// 設定すると無圧縮 BMP を組み立ててサーフェスへ渡す。 4K で約 41ms かかるため、<br/>
    /// 連続して差し替える用途（ドラッグ中の更新）には使わない。
    /// </remarks>
    public EsReadOnlyBitmap? Source
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            ApplySource();
        }
    }

    /// <summary>拡大の補間に最近傍を使うかどうかを取得または設定する。</summary>
    /// <value>true（既定）の場合は最近傍。 false の場合は線形補間。</value>
    public bool UseNearestNeighbor
    {
        get => _useNearestNeighbor;
        set
        {
            _useNearestNeighbor = value;
            if (_brush is not null)
            {
                _brush.BitmapInterpolationMode = value
                    ? CompositionBitmapInterpolationMode.NearestNeighbor
                    : CompositionBitmapInterpolationMode.Linear;
            }
        }
    }

    /// <summary>画像の周囲へ付ける影を取得または設定する。</summary>
    /// <value>影。 null の場合は影を付けない（既定）。</value>
    /// <remarks>
    /// <c>Compositor.CreateDropShadow()</c> で作った影を渡す。 通常は <see cref="SetShadow"/> を使う<br/>
    /// （Compositor の取得を呼び出し側へ求めない）。
    /// </remarks>
    public new DropShadow? Shadow
    {
        get;
        set
        {
            field = value;
            _sprite.Shadow = value;
        }
    }

    /// <summary>画像の周囲へぼかした影を付ける。</summary>
    /// <param name="blurRadius">ぼかしの半径（DIP）。</param>
    /// <param name="argb">影の色（<c>0xAARRGGBB</c>）。 透過度が濃さになる。</param>
    /// <remarks>
    /// 影は Composition の<br/>
    /// <c>DropShadow</c> を使う（実際のぼかしになり、要素も増えない）。<br/>
    /// <b>画像より先に呼んでよい</b>: 影の外形は画像のサーフェス（ブラシ）に依存するため、画像を設定した時点で付け直す。
    /// </remarks>
    public void SetShadow(double blurRadius, uint argb)
    {
        _shadowBlurRadius = blurRadius;
        _shadowArgb = argb;
        ApplyShadow();
    }

    /// <summary>影を外す。</summary>
    public void ClearShadow()
    {
        _shadowArgb = null;
        Shadow = null;
    }

    /// <summary>影を画像のサーフェスへ結び付ける。</summary>
    /// <remarks>
    /// <c>DropShadow.Mask</c> はブラシを要求するため、ブラシを作った後に呼ぶ。<br/>
    /// ブラシが無い（画像が未設定の）状態では影を付けない（付けると画像の無い矩形へ影だけが残る）。
    /// </remarks>
    private void ApplyShadow()
    {
        if (_shadowArgb is not { } argb || _brush is null)
        {
            Shadow = null;
            return;
        }

        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        var shadow = compositor.CreateDropShadow();
        shadow.BlurRadius = (float)_shadowBlurRadius;
        shadow.Color = Windows.UI.Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
        shadow.Mask = _brush;
        Shadow = shadow;
    }


    /// <summary>画像を表示する領域（DIP）を設定する。</summary>
    /// <param name="bounds">表示する矩形（本ビューの座標）。</param>
    /// <remarks>画像の縦横比を保つかは呼び出し側が決める（本クラスは与えられた矩形いっぱいへ広げる）。</remarks>
    public void SetBounds(Rect bounds)
    {
        Width = bounds.Width;
        Height = bounds.Height;
        Margin = new Thickness(bounds.X, bounds.Y, 0, 0);

        // 親が Canvas でない場合にも位置を反映できるよう、配置の基準を合わせる
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;

        ApplySize(bounds.Width, bounds.Height);
    }

    /// <summary>スプライトの大きさを設定する（表示領域と一致させる）。</summary>
    /// <param name="width">幅（DIP）。</param>
    /// <param name="height">高さ（DIP）。</param>
    /// <remarks>大きさが変わらない場合は何もしない（毎フレームの再設定を避ける）。</remarks>
    private void ApplySize(double width, double height)
    {
        var size = new Vector2((float)Math.Max(0, width), (float)Math.Max(0, height));
        if (_sprite.Size != size)
        {
            _sprite.Size = size;
        }
    }

    /// <summary>画像をスプライトへ反映する。</summary>
    private void ApplySource()
    {
        if (Source is not { IsDisposed: false } image)
        {
            _sprite.IsVisible = false;
            return;
        }

        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(BmpStreamBuilder.Build(image));
            writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            writer.DetachStream();
        }

        stream.Seek(0);

        // 以前のサーフェスを破棄してから差し替える（破棄しないと画素が解放されない）
        _loadedImage?.Dispose();
        _loadedImage = LoadedImageSurface.StartLoadFromStream(stream);
        _brush = compositor.CreateSurfaceBrush(_loadedImage);
        _brush.Stretch = CompositionStretch.Fill;
        _brush.BitmapInterpolationMode = UseNearestNeighbor
            ? CompositionBitmapInterpolationMode.NearestNeighbor
            : CompositionBitmapInterpolationMode.Linear;

        _sprite.Brush = _brush;
        _sprite.IsVisible = true;
    }
}
