using System.Runtime.InteropServices.WindowsRuntime;
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
    /// <summary>変換結果のキャッシュ（画像で一意）。</summary>
    private readonly Dictionary<EsReadOnlyBitmap, WriteableBitmap> _cache = [];

    /// <summary>画像に対応する表示用の画像を取得する（未変換なら変換する）。</summary>
    /// <param name="image">元の画像。</param>
    /// <returns>表示用の画像。</returns>
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
        _cache[image] = bitmap;
        return bitmap;
    }
}
