using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

namespace EsUtil.Ui.WinForms.Interop.Imaging;

/// <summary>コアの画像（<see cref="EsReadOnlyBitmap"/>）から GDI+ の <see cref="DrawingBitmap"/> を作る。</summary>
/// <remarks>
/// <b>なぜ本プロジェクトが持つか</b>: <c>System.Drawing</c> は Windows 専用の外部パッケージであるため、<br/>
/// 「外部パッケージへ依存しない」を判定基準とする <c>EsUtil.Platform.Core</c> へは置けない。<br/>
/// GDI+ で描く層（本プロジェクトの表示ビューと、それを参照する UI 実装）が共用する入口を本クラスが単一所有する。<br/>
/// <br/>
/// <b>ピクセル形式</b>: コアの画像は BGRA8888・アルファ乗算済みであるため、GDI+ の <c>Format32bppPArgb</c>（同じ並び・同じ乗算）を使う<br/>
/// （形式が一致しないと成分の順序が入れ替わる）。<br/>
/// <br/>
/// <b>所有権</b>: 返す <see cref="DrawingBitmap"/> は複製であり、呼び出し側が所有する（破棄も呼び出し側）。<br/>
/// 引数の <see cref="EsReadOnlyBitmap"/> の所有権は移動しない（読み取るだけであり、破棄もしない）。<br/>
/// <b>複製は高価</b>: 1 回の呼び出しが全面の画素を複製するため、描画のたびに呼ばず、<br/>
/// 画像が差し替わった時点で 1 度だけ作って保持する（保持と破棄は呼び出し側）。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class EsBitmapDrawingInterop
{
    /// <summary>コアの画像を GDI+ のビットマップへ複製する。</summary>
    /// <param name="source">コピー元の画像（読み取りのみ。 破棄しない）。 null 不可。</param>
    /// <returns>複製したビットマップ（<c>Format32bppPArgb</c>）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    /// <remarks>
    /// 行幅（<c>Stride</c>）は画像ごとに異なりうるため、行単位でコピーする。
    /// </remarks>
    public static DrawingBitmap ToDrawingBitmap(EsReadOnlyBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var destination = new DrawingBitmap(source.Width, source.Height, DrawingPixelFormat.Format32bppPArgb);
        var data = destination.LockBits(
            new DrawingRectangle(0, 0, source.Width, source.Height),
            DrawingImageLockMode.WriteOnly,
            DrawingPixelFormat.Format32bppPArgb);
        try
        {
            CopyRows(source, data);
            return destination;
        }
        finally
        {
            destination.UnlockBits(data);
        }
    }

    /// <summary>画像の全行を、ロック済みのビットマップへコピーする。</summary>
    /// <param name="source">コピー元の画像。</param>
    /// <param name="data">ロック済みのコピー先（コピー先の行幅は <c>Stride</c>）。</param>
    /// <remarks>
    /// <b>1 行ずつ配列を経由する理由</b>: コピー元はアンマネージド メモリの span、コピー先は GDI+ が確保した<br/>
    /// ポインタであり、両者を直接つなぐ API が無い（<c>unsafe</c> を使わない方針のため、行ごとに<br/>
    /// <see cref="Marshal.Copy(byte[], int, nint, int)"/> で書き込む）。
    /// </remarks>
    private static void CopyRows(EsReadOnlyBitmap source, BitmapData data)
    {
        var bytesPerPixel = EsReadOnlyBitmap.BytesPerPixel;
        var rowBytes = source.Width * bytesPerPixel;
        var pixels = source.ReadOnlyPixels;
        var row = new byte[rowBytes];

        for (var offset = 0; offset < source.Height; offset++)
        {
            pixels.Slice((offset * source.Stride), rowBytes).CopyTo(row);
            Marshal.Copy(row, 0, nint.Add(data.Scan0, offset * data.Stride), rowBytes);
        }
    }
}
