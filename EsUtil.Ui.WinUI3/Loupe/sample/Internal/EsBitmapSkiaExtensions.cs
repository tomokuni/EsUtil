using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

// 複製元: EsUtil.SaveEvidence.Platform.Skia（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Ui.WinUI3.Loupe.Sample.Internal;

/// <summary>コアの画像を Skia のビットマップとして読むための拡張メソッド（サンプルが使う分だけの複製）。</summary>
/// <remarks>
/// <b>なぜ複製するか</b>: サンプルは本体の <c>internal</c> 型を参照できないため、<br/>
/// 「テスト画像を Skia で描く」ために必要な変換だけを本プロジェクトの Internal へ複製する<br/>
/// （本体が公開している経路ではないため、複製は使う分に限る）。<br/>
/// <b>所有権</b>: 返す <see cref="SKBitmap"/> はコアのバッファを指す非所有ビューである（破棄してもコアのバッファは解放されない）。<br/>
/// ビューは元の画像が生存している間だけ有効である。
/// </remarks>
internal static class EsBitmapSkiaExtensions
{
    /// <summary>コアの画像を指す非所有ビューを返す。</summary>
    /// <param name="bitmap">対象の画像（読み取るだけのため <see cref="EsReadOnlyBitmap"/> を受ける）。 null 不可。</param>
    /// <returns>同じピクセル バッファを指す <see cref="SKBitmap"/>（非所有）。 呼び出し側が破棄する。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="bitmap"/> が破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">ビューの作成に失敗した場合。</exception>
    public static SKBitmap ToSKBitmap(this EsReadOnlyBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var view = new SKBitmap();

        // コアのバッファをそのまま指す（ピクセルをコピーしない非所有ビュー）。
        // バッファはアンマネージドのため、fixed で得たアドレスは生存中動かない（InstallPixels が保持しても有効）
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
