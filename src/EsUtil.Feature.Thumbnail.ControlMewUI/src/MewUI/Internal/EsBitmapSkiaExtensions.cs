using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

// 複製元: EsUtil.Feature.Loupe.ControlMewUI.Internal（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Thumbnail.MewUI.Internal;

/// <summary>コアの <see cref="EsReadOnlyBitmap"/> を Skia の <see cref="SKBitmap"/> として読む拡張メソッド。</summary>
/// <remarks>
/// <b>なぜ本層が持つか</b>: Skia は UI フレームワークではないが<b>外部パッケージ</b>であるため、<br/>
/// 「外部パッケージへ依存しない」を判定基準とする <c>EsUtil.Platform.Core</c> には置けない。<br/>
/// <b>所有権</b>: 返すビューはコアのピクセルを指すだけ（複製しない）。 <b>破棄は呼び出し側が行う</b><br/>
/// （破棄してもコアのバッファは解放されない）。
/// </remarks>
internal static class EsBitmapSkiaExtensions
{
    /// <summary>コアの画像を指す非所有ビューを返す。</summary>
    /// <param name="bitmap">対象の画像（読み取るだけのため <see cref="EsReadOnlyBitmap"/> を受ける）。 null 不可。</param>
    /// <returns>同じピクセル バッファを指す <see cref="SKBitmap"/>（非所有）。 <b>呼び出し側が破棄する</b>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> が null の場合。</exception>
    /// <exception cref="InvalidOperationException">ビューの作成に失敗した場合。</exception>
    /// <remarks>ビューは元の画像の生存中のみ有効である（ピクセルをコピーしないため）。</remarks>
    public static SKBitmap ToSKBitmap(this EsReadOnlyBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var view = new SKBitmap();

        // コアのバッファをそのまま指す（ピクセルをコピーしない非所有ビュー）。
        // バッファはアンマネージドのため、fixed で得たアドレスは生存中動かない
        unsafe
        {
            fixed (byte* p = bitmap.ReadOnlyPixels)
            {
                if (!view.InstallPixels(info, (nint)p, bitmap.Stride))
                {
                    view.Dispose();
                    throw new InvalidOperationException("SKBitmap のビューを作成できませんでした（ピクセル形式または寸法が不正です）。");
                }
            }
        }

        return view;
    }
}
