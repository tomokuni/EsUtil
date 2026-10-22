using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

// 複製元: EsUtil.Platform.Core.Windows.Imaging（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Ui.WinForms.Loupe.Internal;

/// <summary>コアの画像（<see cref="EsReadOnlyBitmap"/>）から GDI+ の <see cref="DrawingBitmap"/> を作る。</summary>
/// <remarks>
/// <b>なぜ本プロジェクトが持つか</b>: System.Drawing は Windows 専用の外部パッケージであるため、<br/>
/// 「外部パッケージへ依存しない」を判定基準とする <c>EsUtil.Platform.Core</c> へは置けない。<br/>
/// 複製元は System.Drawing との相互変換（<c>ToDrawingBitmap</c> とロック済みバッファの行コピー）であり、<br/>
/// 本プロジェクトは<b>ルーペが必要とする向き（コアの画像 → GDI+）と、切り出す領域だけを複製する入口</b>に絞って持つ。<br/>
/// <br/>
/// <b>ピクセル形式</b>: コアの画像は BGRA8888・アルファ乗算済みであるため、GDI+ の <c>Format32bppPArgb</c>（同じ並び・同じ乗算）を使う<br/>
/// （形式が一致しないと成分の順序が入れ替わる）。<br/>
/// <br/>
/// <b>所有権</b>: 返す <see cref="DrawingBitmap"/> は複製であり、呼び出し側が所有する（破棄も呼び出し側）。<br/>
/// 引数の <see cref="EsReadOnlyBitmap"/> の所有権は移動しない（読み取るだけであり、破棄もしない）。<br/>
/// <br/>
/// <b>描画のたびの複製を小さく保つ</b>: 描画面へ描くのは拡大画像の切り出し領域だけであるため、<br/>
/// 画像全体ではなく<b>必要な領域だけ</b>を複製する（表示サイズ ÷ 拡大率 の画素数のため、画像全体より小さい）。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class EsBitmapDrawingInterop
{
    /// <summary>画像の一部を GDI+ のビットマップへ複製する。</summary>
    /// <param name="source">コピー元の画像（読み取りのみ。 破棄しない）。 null 不可。</param>
    /// <param name="x">複製する領域の左上 X（画像のピクセル座標）。 領域は画像の内側であること（呼び出し側が切り詰める）。</param>
    /// <param name="y">複製する領域の左上 Y（画像のピクセル座標）。</param>
    /// <param name="width">複製する領域の幅（ピクセル）。 1 以上。</param>
    /// <param name="height">複製する領域の高さ（ピクセル）。 1 以上。</param>
    /// <returns>複製したビットマップ（<c>Format32bppPArgb</c>）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException">領域が不正（幅・高さが 1 未満、または画像の外へ出る）な場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    /// <remarks>
    /// 行幅（<c>Stride</c>）は画像ごとに異なりうるため、行単位でコピーする<br/>
    /// （画像の行幅とビットマップの行幅が一致する場合も同じ経路を使う。 切り出しが小さいため差は小さい）。
    /// </remarks>
    public static DrawingBitmap CopyRegion(EsReadOnlyBitmap source, int x, int y, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (width < 1 || height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "複製する領域は 1 ピクセル以上である必要があります。");
        }

        if (x < 0 || y < 0 || x + width > source.Width || y + height > source.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "複製する領域が画像の外へ出ています。");
        }

        var destination = new DrawingBitmap(width, height, DrawingPixelFormat.Format32bppPArgb);
        var data = destination.LockBits(
            new DrawingRectangle(0, 0, width, height), DrawingImageLockMode.WriteOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            CopyRows(source, x, y, width, height, data);
            return destination;
        }
        finally
        {
            destination.UnlockBits(data);
        }
    }

    /// <summary>画像の指定領域の行を、ロック済みのビットマップへコピーする。</summary>
    /// <param name="source">コピー元の画像。</param>
    /// <param name="x">領域の左上 X（画像のピクセル座標）。</param>
    /// <param name="y">領域の左上 Y（画像のピクセル座標）。</param>
    /// <param name="width">領域の幅（ピクセル）。</param>
    /// <param name="height">領域の高さ（ピクセル）。</param>
    /// <param name="data">ロック済みのコピー先（コピー先の行幅は <c>Stride</c>）。</param>
    /// <remarks>
    /// <b>1 行ずつ配列を経由する理由</b>: コピー元はアンマネージド メモリの span、コピー先は GDI+ が確保した<br/>
    /// ポインタであり、両者を直接つなぐ API が無い（<c>unsafe</c> を使わない方針のため、行ごとに
    /// <see cref="Marshal.Copy(byte[], int, nint, int)"/> で書き込む）。 切り出しは表示サイズ ÷ 拡大率の画素数であり、<br/>
    /// 1 行分の一時配列は小さい（例: 240 ピクセルの切り出しで約 1 KB）。
    /// </remarks>
    private static void CopyRows(
        EsReadOnlyBitmap source, int x, int y, int width, int height, BitmapData data)
    {
        var bytesPerPixel = EsReadOnlyBitmap.BytesPerPixel;
        var rowBytes = width * bytesPerPixel;
        var pixels = source.ReadOnlyPixels;
        var row = new byte[rowBytes];

        for (var offset = 0; offset < height; offset++)
        {
            pixels.Slice(((y + offset) * source.Stride) + (x * bytesPerPixel), rowBytes).CopyTo(row);
            Marshal.Copy(row, 0, nint.Add(data.Scan0, offset * data.Stride), rowBytes);
        }
    }
}
