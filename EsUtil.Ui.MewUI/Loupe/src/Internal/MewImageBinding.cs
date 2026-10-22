using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Ui.MewUI.Loupe.Internal;

/// <summary>コアの画像（<see cref="EsReadOnlyBitmap"/>）を MewUI の画像（<see cref="IImage"/>）として保持する。</summary>
/// <remarks>
/// <b>変換は画像が変わったときだけ</b>: 毎フレーム変換すると画素数分の費用が描画に乗る。<br/>
/// <b>所有権</b>: 変換した <see cref="ImageSource"/> と <see cref="IImage"/> は本クラスが所有し、
/// 差し替え時に解放する。
/// </remarks>
internal sealed class MewImageBinding
{
    /// <summary>変換済みの元画像（同一性の判定に使う）。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>変換した画像の元（画素を保持する）。</summary>
    private ImageSource? _decoded;

    /// <summary>描画に渡す画像。</summary>
    private IImage? _image;

    /// <summary>表示する画像に対応する MewUI の画像を返す（必要なら変換する）。</summary>
    /// <param name="source">表示する画像。 null・破棄済みの場合は null を返す。</param>
    /// <returns>描画に渡す画像。 変換できない場合は null。</returns>
    public IImage? Resolve(EsReadOnlyBitmap? source)
    {
        if (ReferenceEquals(_source, source))
        {
            return _source is { IsDisposed: false } ? _image : null;
        }

        Release();
        _source = source;

        if (source is not { IsDisposed: false } bitmap)
        {
            return null;
        }

        _decoded = ImageSource.FromBgraPixels(bitmap.Width, bitmap.Height, bitmap.ReadOnlyPixels, hasAlpha: true);
        _image = _decoded.CreateImage(Application.DefaultGraphicsFactory);
        return _image;
    }

    /// <summary>保持している画像を解放する。</summary>
    public void Release()
    {
        _image?.Dispose();
        _image = null;
        _decoded?.Dispose();
        _decoded = null;
        _source = null;
    }

    /// <summary>指定の矩形へ画像の全面を描画する。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="source">描画する画像。 null・破棄済みの場合は何もしない。</param>
    /// <param name="crop">元画像の切り出し矩形（ピクセル）。</param>
    /// <param name="destination">描画先の矩形。</param>
    public void Draw(IGraphicsContext context, EsReadOnlyBitmap? source, EsRectInt crop, EsRect destination)
    {
        if (Resolve(source) is not { } image)
        {
            return;
        }

        context.ImageScaleQuality = ImageScaleQuality.Fast;
        context.DrawImage(
            image,
            new Rect(crop.X, crop.Y, crop.Width, crop.Height),
            destination.ToMewUIRect());
    }
}
