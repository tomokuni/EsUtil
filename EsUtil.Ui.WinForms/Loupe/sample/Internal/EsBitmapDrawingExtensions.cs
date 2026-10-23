using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

// 本サンプルだけが使う複製（サンプルが単独で動くため。 .github/依存関係.md §4.1）
namespace EsUtil.Ui.WinForms.Loupe.Sample.Internal;

/// <summary>コアの画像（<see cref="EsReadOnlyBitmap"/>）を GDI+ のビットマップへ複製する。</summary>
/// <remarks>
/// <b>なぜサンプルが持つか</b>: 変換の実装は本コントロールの内部（<c>Internal</c>）にあり、<br/>
/// サンプルは別アセンブリのため参照できない（型は <c>internal</c>）。<br/>
/// 依存関係の規約は、サンプルが同封の <c>Internal</c> へ必要な変換だけを複製することを認めている<br/>
/// （<c>sample/Internal/</c>。 依存関係 §4.1）。<br/>
/// <br/>
/// <b>用途</b>: 背景のテスト画像を描くために全体を 1 度だけ複製する（描画のたびには複製しない）。<br/>
/// ピクセル形式はコアの画像と同じ BGRA8888・アルファ乗算済みに揃える（<c>Format32bppPArgb</c>）。
/// </remarks>
internal static class EsBitmapDrawingExtensions
{
    /// <summary>画像を GDI+ のビットマップへ複製する。</summary>
    /// <param name="source">コピー元の画像（読み取りのみ。 破棄しない）。 null 不可。</param>
    /// <returns>複製したビットマップ。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>行幅（<c>Stride</c>）は画像ごとに異なりうるため、行単位でコピーする。</remarks>
    public static DrawingBitmap ToDrawingBitmap(this EsReadOnlyBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var width = source.Width;
        var height = source.Height;
        var destination = new DrawingBitmap(width, height, DrawingPixelFormat.Format32bppPArgb);
        var data = destination.LockBits(
            new DrawingRectangle(0, 0, width, height), DrawingImageLockMode.WriteOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            var bytesPerPixel = EsReadOnlyBitmap.BytesPerPixel;
            var rowBytes = width * bytesPerPixel;
            var pixels = source.ReadOnlyPixels;
            var row = new byte[rowBytes];

            for (var y = 0; y < height; y++)
            {
                pixels.Slice(y * source.Stride, rowBytes).CopyTo(row);
                Marshal.Copy(row, 0, nint.Add(data.Scan0, y * data.Stride), rowBytes);
            }

            return destination;
        }
        finally
        {
            destination.UnlockBits(data);
        }
    }
}
