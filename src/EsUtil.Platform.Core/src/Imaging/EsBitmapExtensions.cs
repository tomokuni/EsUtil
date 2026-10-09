using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像を<b>変更しない</b>操作（切り出し）の拡張メソッド。</summary>
/// <remarks>
/// <b>なぜ拡張メソッドなのか</b>: <see cref="Crop(IEsBitmap, EsRectInt)"/> は「自分を変更せず、新しい画像を作る」操作であり、
/// <see cref="IEsBitmap"/> を実装する任意の画像（借用した読み取り専用の画像を含む）に対して使える必要がある。
/// 継承階層の型（<see cref="EsBitmapBase"/>）のメソッドにすると、本インターフェイスを直接実装する画像
/// （表示アダプタ等）から呼べない（実測: ルーペと切り出しの両方が使う）。<br/>
/// <b>戻り値の権利</b>: 新しく作った画像は<b>書き換え可能</b>（<see cref="EsWritableBitmap"/>）で返す
/// （生成した側が所有し、破棄する責任を持つ。 読み取り専用で足りる場合は <see cref="EsBitmap.Clone"/> を使う）。<br/>
/// <b>色の取得はここに置かない</b>: 「座標のピクセルを色として読む」は画像の読み取りそのものであり、
/// 読み取りの面（<see cref="IEsBitmap.GetPixel(EsPointInt)"/>）が実装を持つ（追加の画像を作らないため、拡張メソッドである理由が無い）。
/// </remarks>
public static class EsBitmapExtensions
{
    /// <summary>画像から指定された矩形領域を切り出す。</summary>
    /// <param name="source">切り出し元の画像。 借用であり破棄しない。</param>
    /// <param name="x">切り出し領域の左上X座標（画像ローカルピクセル）。 負値の場合はソース左端より外側が透明パディングとなる。</param>
    /// <param name="y">切り出し領域の左上Y座標（画像ローカルピクセル）。 負値の場合はソース上端より外側が透明パディングとなる。</param>
    /// <param name="width">切り出し領域の幅（ピクセル）。 正の値。</param>
    /// <param name="height">切り出し領域の高さ（ピクセル）。 正の値。</param>
    /// <returns>切り出された新しい画像（要求サイズ。 ソース範囲外は透明パディング）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> / <paramref name="height"/> が 0 以下の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    /// <remarks>
    /// デスティネーションのコピーで覆われない領域は透明にするため、ソース範囲外は透明パディングとなる。<br/>
    /// 左上がソースの外（負）の場合も、要求矩形とソースの位置関係を保ってコピーする
    /// （内容を左上へ詰めない。 画像外の領域が透明のまま残る）。<br/>
    /// 矩形がソースと交差しない場合は全ピクセル透明の画像を返す。<br/>
    /// コピーされないパディングだけを透明にする（0 埋めは行わない）。
    /// </remarks>
    public static EsWritableBitmap Crop(this IEsBitmap source, int x, int y, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "切り出し領域の幅は正の値である必要があります。");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "切り出し領域の高さは正の値である必要があります。");
        }

        var srcW = source.Width;
        var srcH = source.Height;

        // ソース左端・上端より外側はデスティネーション側でも透明のまま残す（コピー開始位置をずらす）
        var dstOffsetX = Math.Max(0, -x);
        var dstOffsetY = Math.Max(0, -y);

        // ソース範囲内にクランプ（交差しない場合はコピー列数・行数が 0 以下となる）
        var cropX = Math.Max(0, x);
        var cropY = Math.Max(0, y);
        var cropW = Math.Min(width - dstOffsetX, srcW - cropX);
        var cropH = Math.Min(height - dstOffsetY, srcH - cropY);

        // ソースと交差しない場合は全ピクセル透明を返す（生成時の 0 埋めをそのまま使う）
        if (cropW <= 0 || cropH <= 0)
        {
            return EsWritableBitmap.Create(width, height);
        }

        var result = EsWritableBitmap.CreateUninitialized(width, height);
        var src = source.Pixels;
        var dst = result.WritablePixels;
        var srcStride = source.Stride;
        var dstStride = result.Stride;

        // コピーする 1 行のバイト数（cropW は「デスティネーションの幅 − 左パディング」以下にクランプ済み）
        var copyBytes = cropW * IEsBitmap.BytesPerPixel;

        // 上端・下端のパディング行は連続しているため、行ごとではなく 1 回で消す
        var bottomRows = height - dstOffsetY - cropH;
        if (dstOffsetY > 0)
        {
            dst[..(dstStride * dstOffsetY)].Clear();
        }

        if (bottomRows > 0)
        {
            dst.Slice(dstStride * (dstOffsetY + cropH), dstStride * bottomRows).Clear();
        }

        // 中間行の左右のパディングを消す（左右の幅は行ごとに同じ）
        var leftBytes = dstOffsetX * IEsBitmap.BytesPerPixel;
        var rightBytes = (width - dstOffsetX - cropW) * IEsBitmap.BytesPerPixel;
        if (leftBytes > 0 || rightBytes > 0)
        {
            for (var row = 0; row < cropH; row++)
            {
                var pRow = dstStride * (dstOffsetY + row);
                if (leftBytes > 0)
                {
                    dst.Slice(pRow, leftBytes).Clear();
                }

                if (rightBytes > 0)
                {
                    dst.Slice(pRow + leftBytes + copyBytes, rightBytes).Clear();
                }
            }
        }

        // 行単位でコピーする（行の先頭は乗算 1 回で求める）
        for (var row = 0; row < cropH; row++)
        {
            src.Slice(srcStride * (cropY + row) + cropX * IEsBitmap.BytesPerPixel, copyBytes)
                .CopyTo(dst.Slice(dstStride * (dstOffsetY + row) + leftBytes, copyBytes));
        }

        return result;
    }

    /// <summary>画像から指定された矩形領域を切り出す。</summary>
    /// <param name="source">切り出し元の画像。 借用であり破棄しない。</param>
    /// <param name="rect">切り出し領域（画像ローカルピクセル）。</param>
    /// <returns>切り出された新しい画像（要求サイズ。 ソース範囲外は透明パディング）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rect"/> の幅・高さが 0 以下の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    public static EsWritableBitmap Crop(this IEsBitmap source, EsRectInt rect)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Crop(source, rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>長辺を指定サイズへ収めた画像を生成する（収まっている場合は等倍の複製）。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 正の値。</param>
    /// <returns>生成された画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>用途</b>: 大きな画像を小さく扱う場面（一覧のサムネイル・プレビュー等）。 元の寸法の複製を保持すると
    /// 4K 1 枚で 32 MB になるため、表示や転送の前に本メソッドで縮小する（実測: 4K から 96x54 で 17 µs / 20 KB）。<br/>
    /// <b>画質</b>: <b>最近傍で標本化する</b>（縮小専用の高速経路）。 高品質な縮小が必要な場面（保存・印刷等）では
    /// 本メソッドを使わない。<br/>
    /// <b>縦横比</b>: 切り捨てで丸めるため、<b>1 px 未満の誤差が出うる</b>（座標の対応付けが必要な場面では使わない）。<br/>
    /// <b>収まっている場合</b>は縮小せず等倍の複製を返す（<see cref="Crop(IEsBitmap, EsRectInt)"/> へ委譲する。
    /// 元の画像をそのまま返さないのは、戻り値の所有権を呼び出し側へ統一するため）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSize"/> が 0 以下の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    public static EsWritableBitmap Resize(this IEsBitmap source, int maxSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (maxSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "長辺の上限は正の値である必要があります。");
        }

        var srcWidth = source.Width;
        var srcHeight = source.Height;
        if (srcWidth <= maxSize && srcHeight <= maxSize)
        {
            // 収まっている場合は縮小しない（等倍の複製）
            return Crop(source, 0, 0, srcWidth, srcHeight);
        }

        var scale = Math.Min((double)maxSize / srcWidth, (double)maxSize / srcHeight);
        var dstWidth = Math.Max(1, (int)(srcWidth * scale));
        var dstHeight = Math.Max(1, (int)(srcHeight * scale));

        // 全ピクセルを書くため、生成時の 0 埋めは省く
        var result = EsWritableBitmap.CreateUninitialized(dstWidth, dstHeight);
        var src = source.Pixels;
        var srcStride = source.Stride;
        var dst = result.WritablePixels;
        var dstStride = result.Stride;
        var bytesPerPixel = IEsBitmap.BytesPerPixel;

        // 列の対応（最近傍）を先に求め、内側のループでは割り算をしない
        var colMap = new int[dstWidth];
        for (var x = 0; x < dstWidth; x++)
        {
            colMap[x] = Math.Min(srcWidth - 1, (int)((long)x * srcWidth / dstWidth));
        }

        for (var y = 0; y < dstHeight; y++)
        {
            var srcRow = src[(Math.Min(srcHeight - 1, (int)((long)y * srcHeight / dstHeight)) * srcStride)..];
            var dstRow = dst[(y * dstStride)..];
            for (var x = 0; x < dstWidth; x++)
            {
                srcRow.Slice(colMap[x] * bytesPerPixel, bytesPerPixel).CopyTo(dstRow.Slice(x * bytesPerPixel, bytesPerPixel));
            }
        }

        return result;
    }
}
