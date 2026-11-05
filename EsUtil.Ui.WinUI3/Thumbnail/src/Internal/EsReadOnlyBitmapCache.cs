using System.Runtime.InteropServices.WindowsRuntime;
using EsUtil.Platform.Core.Imaging;
using Microsoft.UI.Xaml.Media.Imaging;

namespace EsUtil.Ui.WinUI3.Thumbnail.Internal;

/// <summary><see cref="EsReadOnlyBitmap"/> を <see cref="WriteableBitmap"/> へ変換してキャッシュする。</summary>
/// <remarks>
/// <b>変換の理由</b>: XAML の画像要素は <see cref="WriteableBitmap"/> などの画像ソースを受け取るが、<br/>
/// コアの画像は UI 非依存の画素バッファであるため、表示の直前に変換する必要がある。<br/>
/// <br/>
/// <b>キャッシュの理由</b>: 一覧はスクロールのたびに項目を作り直すため、同じ画像を何度も変換すると<br/>
/// スクロールが重くなる。 画像ごとに 1 つだけ変換結果を保持する（画像は不変であり、破棄されると参照も消える）。<br/>
/// <br/>
/// <b>画素形式</b>: <see cref="WriteableBitmap"/> が要求する BGRA8888 はコアの内部表現と同じであるため、<br/>
/// 画素はそのまま複製できる（色の並べ替えをしない）。
/// </remarks>
internal sealed class EsReadOnlyBitmapCache
{
    /// <summary>保持する変換結果の上限（件）。</summary>
    /// <remarks>
    /// <see cref="WriteableBitmap"/> は画素の複製を保持するため、上限を置かないと
    /// 同じ項目の画像を差し替えるたびに古い画素が解放されない（4K の画像 1 枚で 33 MB）。
    /// 1 つの項目が同時に表示する画像は 1 枚であるため、少しの余裕だけを残す。
    /// </remarks>
    private const int Capacity = 4;

    /// <summary>変換結果のキャッシュ（画像で一意）。</summary>
    private readonly Dictionary<EsReadOnlyBitmap, WriteableBitmap> _cache = [];

    /// <summary>追加順（上限を超えたときに古いものから捨てる）。</summary>
    private readonly Queue<EsReadOnlyBitmap> _order = [];

    /// <summary>画像に対応する表示用の画像を取得する（未変換なら変換する）。</summary>
    /// <param name="image">元の画像。</param>
    /// <returns>表示用の画像。</returns>
    /// <remarks>UI スレッド専用（<see cref="WriteableBitmap"/> を UI スレッド以外から触れないため）。</remarks>
    public WriteableBitmap Get(EsReadOnlyBitmap image)
    {
        if (_cache.TryGetValue(image, out var cached))
        {
            return cached;
        }

        var bitmap = new WriteableBitmap(image.Width, image.Height);
        using (var target = bitmap.PixelBuffer.AsStream())
        {
            target.Write(image.ReadOnlyPixels);
        }

        bitmap.Invalidate();

        if (_cache.Count >= Capacity)
        {
            _cache.Remove(_order.Dequeue());
        }

        _cache[image] = bitmap;
        _order.Enqueue(image);
        return bitmap;
    }
}
