using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;

namespace EsUtil.Ui.MewUI.Interop.Imaging;

/// <summary><see cref="EsReadOnlyBitmap"/> を MewUI の画像（<see cref="IImage"/>）として保持する結び付け。</summary>
/// <remarks>
/// <b>適用パターン</b>: Adapter + キャッシュ（コアの画素を MewUI の画像へ 1 回だけ変換する）。<br/>
/// <b>変換の費用</b>: <c>ImageSource.FromBgraPixels</c> は画素を渡して画像を作る（画素の並びはそのまま使える）。
/// 毎フレーム変換するとフレーム時間に画素数分の費用が乗るため、<b>画像が変わったときだけ変換</b>する。<br/>
/// <b>所有権</b>: 変換した <see cref="ImageSource"/> と <see cref="IImage"/> は本クラスが所有し、
/// 差し替え時と <see cref="Dispose"/> で解放する。<br/>
/// <b>派生クラス（<see cref="Aprillz.MewUI.Controls.Control"/>）の寿命</b>: MewUI の要素は破棄の通知を持たないため、
/// 表示している間は本クラスが保持し続ける（プロセス終了時に OS が回収する）。
/// </remarks>
internal sealed class ImageBinding : IDisposable
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
    /// <remarks>
    /// 同じ <paramref name="source"/> を渡し続ける限り変換しない（参照の同一性で判定する）。
    /// 画像が差し替わった場合だけ古い画像を解放して作り直す。
    /// </remarks>
    public IImage? Resolve(EsReadOnlyBitmap? source)
    {
        if (ReferenceEquals(_source, source))
        {
            return _source is { IsDisposed: false } ? _image : null;
        }

        ReleaseSource();
        _source = source;

        if (source is not { IsDisposed: false } bitmap)
        {
            return null;
        }

        // コアの画素は BGRA8888（アルファ乗算済み）であり、そのまま MewUI の画像へ渡せる
        _decoded = ImageSource.FromBgraPixels(bitmap.Width, bitmap.Height, bitmap.ReadOnlyPixels, hasAlpha: true);
        _image = _decoded.CreateImage(Application.DefaultGraphicsFactory);
        return _image;
    }

    /// <summary>保持している画像を解放する。</summary>
    public void Dispose() => ReleaseSource();

    /// <summary>変換した画像を解放し、元画像の参照を切る。</summary>
    private void ReleaseSource()
    {
        _image?.Dispose();
        _image = null;
        _decoded?.Dispose();
        _decoded = null;
        _source = null;
    }
}
