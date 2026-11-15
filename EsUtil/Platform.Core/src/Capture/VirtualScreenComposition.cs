using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Platform.Core.Capture;

/// <summary>モニター単位に撮影した画像を、仮想スクリーン 1 枚の画像へ合成する。</summary>
/// <remarks>
/// <b>なぜモニター単位か</b>: Windows.Graphics.Capture はモニター単位でしか撮れず、GDI の <c>BitBlt</c> も
/// モニターを跨ぐ領域を 1 回で撮ると、モニターごとの DPI・色空間の違いを吸収できない。<br/>
/// <b>撮影の実装を問わない</b>: 撮影そのものは <c>capture</c> 引数（モニターの矩形 → 画像）へ委ねるため、
/// GDI と Windows.Graphics.Capture のどちらでも同じ手順になる（両者の違いは 1 モニターを撮る手段だけ）。<br/>
/// <b>合成は隙間を透明のまま残す</b>: モニターが矩形状に並んでいない場合、モニターの無い領域は
/// <see cref="EsBitmap.Create(int, int)"/> が作る透明の画素のままになる。
/// </remarks>
internal static class VirtualScreenComposition
{
    /// <summary>全モニターを撮影して 1 枚の画像へ合成する。</summary>
    /// <param name="monitors">モニターの矩形（画面物理ピクセル）。</param>
    /// <param name="capture">1 モニターを撮影する処理（引数の矩形は画面物理ピクセル。 返した画像は破棄する）。</param>
    /// <param name="origin">合成した画像の原点（全モニターを包含する矩形の左上、画面物理ピクセル）。</param>
    /// <returns>合成した画像。 モニターが無い場合・矩形が空の場合は null。 破棄は呼び出し側。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="monitors"/> または <paramref name="capture"/> が null の場合。</exception>
    /// <remarks>
    /// 1. 全モニターを包含する矩形を求める（各撮影画像の貼り付け先）。<br/>
    /// 2. モニターごとに撮影し、包含矩形からの相対位置へ画素を貼り付ける。<br/>
    /// 3. 途中で失敗した場合は合成中の画像を破棄してから例外を伝える（確保した資源を残さない）。
    /// </remarks>
    public static EsBitmap? Compose(IReadOnlyList<EsRectInt> monitors, Func<EsRectInt, EsBitmap> capture, out EsPointInt origin)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        ArgumentNullException.ThrowIfNull(capture);

        origin = default;
        if (monitors.Count == 0)
        {
            return null;
        }

        var canvas = monitors[0];
        for (var i = 1; i < monitors.Count; i++)
        {
            canvas = canvas.Union(monitors[i]);
        }

        if (canvas.Width <= 0 || canvas.Height <= 0)
        {
            return null;
        }

        origin = new EsPointInt(canvas.X, canvas.Y);
        var image = EsBitmap.Create(canvas.Width, canvas.Height);
        try
        {
            for (var i = 0; i < monitors.Count; i++)
            {
                var monitor = monitors[i];
                using var captured = capture(monitor);
                CopyInto(captured, image, monitor.X - canvas.X, monitor.Y - canvas.Y);
            }

            return image;
        }
        catch
        {
            // 合成の途中で失敗したら、確保した画像を破棄してから呼び出し側へ伝える（本層はログを出力しない）
            image.Dispose();
            throw;
        }
    }

    /// <summary>画像を貼り付け先の位置へ画素単位でコピーする（はみ出す部分は切り捨てる）。</summary>
    /// <param name="source">コピー元の画像。</param>
    /// <param name="target">コピー先の画像。</param>
    /// <param name="x">コピー先の左上 X（画素）。</param>
    /// <param name="y">コピー先の左上 Y（画素）。</param>
    /// <remarks>画素の並び（BGRA・上から下）は両方の画像で同じため、行ごとにまとめてコピーする。</remarks>
    private static void CopyInto(EsBitmap source, EsBitmap target, int x, int y)
    {
        var left = Math.Max(0, x);
        var top = Math.Max(0, y);
        var right = Math.Min(target.Width, x + source.Width);
        var bottom = Math.Min(target.Height, y + source.Height);
        if (right <= left || bottom <= top)
        {
            return;
        }

        var sourcePixels = source.Pixels;
        var targetPixels = target.Pixels;
        var bytesPerPixel = EsBitmap.BytesPerPixel;
        var rowBytes = (right - left) * bytesPerPixel;
        for (var row = top; row < bottom; row++)
        {
            var sourceStart = (((row - y) * source.Stride) + ((left - x) * bytesPerPixel));
            var targetStart = (row * target.Stride) + (left * bytesPerPixel);
            sourcePixels.Slice(sourceStart, rowBytes).CopyTo(targetPixels.Slice(targetStart, rowBytes));
        }
    }
}
