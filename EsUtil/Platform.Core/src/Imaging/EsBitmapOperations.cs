using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win.Wic;
using Windows.Win32.Graphics.Imaging;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像を<b>変更せずに</b>新しい画像を作る操作（切り出し・縮小・減色）の内部実装。</summary>
/// <remarks>
/// <b>公開しない理由</b>: どの操作も必要なのは<b>画素とその形（幅・高さ・行幅）</b>だけであり、
/// 公開する 2 型（<see cref="EsReadOnlyBitmap"/> / <see cref="EsBitmap"/>）とビュー（<see cref="EsPixmap"/>）が
/// <b>同じ名前のメンバー</b>として同じ手順を公開します（<see cref="EsReadOnlyBitmap.Crop(EsRectInt)"/> 等）。
/// 1 回書けば足りる手順を公開面へ 3 組並べないためです（拡張メソッドにすると、同じ操作が型の外へ並びます）。<br/>
/// <b>受け取るのが「画素 + 幅 + 高さ + 行幅」である理由</b>: どの操作も<b>画素の読み取りだけ</b>を行い、
/// 書き込みの権利を要しません（読み取り専用の画像・書き換え可能な画像・ビューのいずれも、渡すのは同じ形です）。
/// <b>行幅を引数にする理由</b>: ビューの画素は元画像の行幅を保つため（<see cref="EsPixmap.Stride"/>）、
/// <b>行の余白を含みます</b>。 全メソッドが<b>行単位</b>に読み書きするため、余白の画素は結果へ混ざりません。<br/>
/// <b>戻り値の権利</b>: 新しく作った画像は<b>書き換え可能</b>（<see cref="EsBitmap"/>）で返します
/// （生成した側が所有し、破棄する責任を持つ）。 複製が要る場合は <see cref="EsReadOnlyBitmap.Copy"/>、0 コピーの
/// 読み取りビューが要る場合は <see cref="EsReadOnlyBitmap.ToPixmap"/> を使います。<br/>
/// <b>切り出しだけが本ライブラリの計算です</b>: 縮小（<see cref="ScaleInto"/>）と減色（<see cref="CreateReduced"/>）の
/// 計算は<b>WIC が単一所有</b>します（保存・読み込みと同じ実装を使うため、経路によって画素が変わりません）。
/// 切り出しは画素のコピーだけであり、WIC の機能（<c>IWICBitmapClipper</c>）を使うと同じだけの写しが増えるため、
/// 行単位のコピーで行います。
/// </remarks>
internal static class EsBitmapOperations
{
    /// <summary>減色（パレット化）で残せる最小の色数。</summary>
    internal const int MinimumColorCount = 2;

    /// <summary>減色（パレット化）で残せる最大の色数（1 画素 1 バイトで表せる上限）。</summary>
    internal const int MaximumColorCount = 256;

    /// <summary>画素から指定された矩形領域を切り出す（公開面のメンバーの実装）。</summary>
    /// <param name="pixels">切り出し元の画素（BGRA8888。 <b>行の余白を含んでよい</b>）。 借用であり破棄しない。</param>
    /// <param name="sourceWidth">切り出し元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">切り出し元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数（<c>sourceWidth × 4</c> 以上）。</param>
    /// <param name="x">切り出し領域の左上X座標（画像ローカルピクセル）。 負値の場合はソース左端より外側が透明パディングとなる。</param>
    /// <param name="y">切り出し領域の左上Y座標（画像ローカルピクセル）。 負値の場合はソース上端より外側が透明パディングとなる。</param>
    /// <param name="cropWidth">切り出し領域の幅（ピクセル）。 正の値。</param>
    /// <param name="cropHeight">切り出し領域の高さ（ピクセル）。 正の値。</param>
    /// <returns>切り出された新しい画像（要求サイズ。 ソース範囲外は透明パディング）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// 規則は公開面が単一所有する（<see cref="EsReadOnlyBitmap.Crop(int, int, int, int)"/> の備考）。<br/>
    /// <b>行の余白を混ぜません</b>: 読み書きとも<b>行単位</b>であり（連続コピーへ寄せる条件にも行幅の一致を含む）、
    /// 余白付きのソース（<see cref="EsPixmap.ReadOnlyPixels"/>）を渡しても余白の画素は結果へ入りません。<br/>
    /// デスティネーションのコピーで覆われない領域は透明にするため、ソース範囲外は透明パディングとなる。<br/>
    /// 左上がソースの外（負）の場合も、要求矩形とソースの位置関係を保ってコピーする
    /// （内容を左上へ詰めない。 画像外の領域が透明のまま残る）。<br/>
    /// 矩形がソースと交差しない場合は全ピクセル透明の画像を返す。<br/>
    /// コピーされないパディングだけを透明にする（0 埋めは行わない）。<br/>
    /// <b>コピーは 1 回で済む場合を分ける</b>: 左右のパディングが無く、ソースとデスティネーションの行幅が等しいときは
    /// 連続領域として全領域を 1 回でコピーする（行ごとの写しの呼び出しが消える）。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cropWidth"/> / <paramref name="cropHeight"/> が 0 以下の場合。</exception>
    internal static EsBitmap Crop(
        ReadOnlySpan<byte> pixels, int sourceWidth, int sourceHeight, int stride,
        int x, int y, int cropWidth, int cropHeight)
    {
        if (cropWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cropWidth), cropWidth, "切り出し領域の幅は正の値である必要があります。");
        }

        if (cropHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cropHeight), cropHeight, "切り出し領域の高さは正の値である必要があります。");
        }

        var srcW = sourceWidth;
        var srcH = sourceHeight;

        // ソース左端・上端より外側はデスティネーション側でも透明のまま残す（コピー開始位置をずらす）
        var dstOffsetX = Math.Max(0, -x);
        var dstOffsetY = Math.Max(0, -y);

        // ソース範囲内にクランプ（交差しない場合はコピー列数・行数が 0 以下となる）
        var cropX = Math.Max(0, x);
        var cropY = Math.Max(0, y);
        var copyW = Math.Min(cropWidth - dstOffsetX, srcW - cropX);
        var copyH = Math.Min(cropHeight - dstOffsetY, srcH - cropY);

        // ソースと交差しない場合は全ピクセル透明を返す（生成時の 0 埋めをそのまま使う）
        if (copyW <= 0 || copyH <= 0)
        {
            return EsBitmap.Create(cropWidth, cropHeight);
        }

        var result = EsBitmap.CreateUninitialized(cropWidth, cropHeight);
        var src = pixels;
        var dst = result.Pixels;
        var srcStride = stride;
        var dstStride = result.Stride;

        // コピーする 1 行のバイト数（copyW は「デスティネーションの幅 − 左パディング」以下にクランプ済み）
        var copyBytes = copyW * EsReadOnlyBitmap.BytesPerPixel;

        // 上端・下端のパディング行は連続しているため、行ごとではなく 1 回で消す
        var bottomRows = cropHeight - dstOffsetY - copyH;
        if (dstOffsetY > 0)
        {
            dst[..(dstStride * dstOffsetY)].Clear();
        }

        if (bottomRows > 0)
        {
            dst.Slice(dstStride * (dstOffsetY + copyH), dstStride * bottomRows).Clear();
        }

        // 中間行の左右のパディングを消す（左右の幅は行ごとに同じ）
        var leftBytes = dstOffsetX * EsReadOnlyBitmap.BytesPerPixel;
        var rightBytes = (cropWidth - dstOffsetX - copyW) * EsReadOnlyBitmap.BytesPerPixel;
        if (leftBytes > 0 || rightBytes > 0)
        {
            for (var row = 0; row < copyH; row++)
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
        if (leftBytes == 0 && rightBytes == 0 && cropX == 0 && srcStride == dstStride)
        {
            // 左右のパディングが無く、コピー元とコピー先の行幅も同じ場合は連続領域になる（1 回でコピーする）
            // （全域の切り出しと全幅の切り出しがここに当たる）
            src.Slice(srcStride * cropY, dstStride * copyH)
                .CopyTo(dst.Slice(dstStride * dstOffsetY, dstStride * copyH));
        }
        else
        {
            for (var row = 0; row < copyH; row++)
            {
                src.Slice(srcStride * (cropY + row) + (cropX * EsReadOnlyBitmap.BytesPerPixel), copyBytes)
                    .CopyTo(dst.Slice(dstStride * (dstOffsetY + row) + leftBytes, copyBytes));
            }
        }

        return result;
    }

    /// <summary>画素から指定された矩形領域を切り出す（公開面のメンバーの実装）。</summary>
    /// <param name="pixels">切り出し元の画素（BGRA8888。 行の余白を含んでよい）。 借用であり破棄しない。</param>
    /// <param name="sourceWidth">切り出し元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">切り出し元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="rect">切り出し領域（画像ローカルピクセル）。</param>
    /// <returns>切り出された新しい画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>規則は公開面が単一所有する（<see cref="EsReadOnlyBitmap.Crop(EsRectInt)"/>）。</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rect"/> の幅・高さが 0 以下の場合。</exception>
    internal static EsBitmap Crop(
        ReadOnlySpan<byte> pixels, int sourceWidth, int sourceHeight, int stride, in EsRectInt rect)
        => Crop(pixels, sourceWidth, sourceHeight, stride, rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>縮小した画像を生成する（寸法の判断と縮小の規則は <see cref="EsImageCodec"/> が持つ）。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 元の長辺以下。</param>
    /// <param name="quality">縮小の品質（補間の選び方）。</param>
    /// <returns>縮小した新しい画像（所有権は呼び出し側へ移る）。</returns>
    /// <remarks><b>中間画像を作りません</b>（縮小先の画像が持つ領域へ直接書きます）。</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSize"/> または <paramref name="quality"/> が許容範囲外の場合。</exception>
    /// <exception cref="NotSupportedException"><paramref name="maxSize"/> が長辺より大きい（拡大が必要な）場合。</exception>
    [SupportedOSPlatform("windows10.0.17763")]
    internal static EsBitmap CreateScaled(
        ReadOnlySpan<byte> pixels, int sourceWidth, int sourceHeight, int stride, int maxSize, EsImageQuality quality)
    {
        var size = new EsSizeInt(sourceWidth, sourceHeight);
        var target = EsImageCodec.GetResizedSize(size, maxSize);
        var result = EsBitmap.CreateUninitialized(target);
        EsImageCodec.ScaleInto(pixels, size, stride, target, quality, result.Pixels);
        return result;
    }

    /// <summary>減色した画像を生成する（減色の計算は WIC が単一所有する）。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="colorCount">残す色数（2〜256）。</param>
    /// <param name="dither">ディザの指定。</param>
    /// <returns>減色した新しい画像（寸法は変わらない）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="colorCount"/> または <paramref name="dither"/> が許容範囲外の場合。</exception>
    [SupportedOSPlatform("windows10.0.17763")]
    internal static EsBitmap CreateReduced(
        ReadOnlySpan<byte> pixels, int sourceWidth, int sourceHeight, int stride, int colorCount, EsDitherMode dither)
    {
        var size = new EsSizeInt(sourceWidth, sourceHeight);
        var result = EsBitmap.CreateUninitialized(size);
        EsImageCodec.ReduceInto(pixels, size, stride, colorCount, dither, result.Pixels);
        return result;
    }
    /// <summary>縮小した画素を呼び出し側の領域へ書き込む（縮小の計算は WIC が単一所有する）。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="target">縮小後の寸法（元より小さいこと）。</param>
    /// <param name="quality">縮小の品質（補間の選び方）。</param>
    /// <param name="destination">書き込み先（<c>target.Width × 4 × target.Height</c> バイト以上。 行は密に書く）。</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> が足りない場合。</exception>
    /// <remarks>
    /// <b>画素を返さない理由</b>: 公開面（<c>EsImageCodec</c>）が呼び出し側の領域へ直接書けるようにするためです。
    /// 画像型は自分が確保した領域を渡すため、<b>縮小のためだけの中間画像を作りません</b>。<br/>
    /// <b>補間は WIC に任せます</b>: デコード時の縮小（<c>IWICBitmapScaler</c>）と同じ実装を使うため、
    /// 読み込み経路と表示経路で画質が変わりません。 本ライブラリは縮小の計算を持ちません。
    /// </remarks>
    [SupportedOSPlatform("windows10.0.17763")]
    internal static void ScaleInto(
        ReadOnlySpan<byte> pixels, int sourceWidth, int sourceHeight, int stride,
        EsSizeInt target, EsImageQuality quality, Span<byte> destination)
    {
        if (target == new EsSizeInt(sourceWidth, sourceHeight))
        {
            var rowBytes = sourceWidth * EsReadOnlyBitmap.BytesPerPixel;
            for (var y = 0; y < sourceHeight; y++)
            {
                pixels.Slice(y * stride, rowBytes).CopyTo(destination[(y * rowBytes)..]);
            }

            return;
        }

        WinWicImageCodec.Resize(
            pixels, sourceWidth, sourceHeight, stride, target.Width, target.Height,
            ToInterpolationMode(quality), destination);
    }

    /// <summary>品質の指定を WIC の補間の指定へ写す。</summary>
    /// <param name="quality">縮小の品質。</param>
    /// <returns>WIC の補間の指定。</returns>
    /// <remarks>
    /// <b>段階と補間の対応</b>: <see cref="EsImageQuality.Low"/> は最近傍（最も速い）、
    /// <see cref="EsImageQuality.Mid"/> は双線形、<see cref="EsImageQuality.High"/> は Fant
    /// （画素の面積平均に近く、細線が落ちにくい）。 <b>デコード時の縮小も Fant</b> であるため、
    /// <see cref="EsImageQuality.High"/> は読み込み経路と画素が一致します。<br/>
    /// <b>立方畳み込み（<c>HighQualityCubic</c>）を使わない理由</b>: 縮小でリンギング（細線の縁の白黒の縞）が
    /// 出やすく、画面キャプチャの文字が読みにくくなります（<see cref="EsReadOnlyBitmap.Resize(int, EsImageQuality)"/> の備考）。
    /// </remarks>
    private static WICBitmapInterpolationMode ToInterpolationMode(EsImageQuality quality)
        => quality switch
        {
            EsImageQuality.Low => WICBitmapInterpolationMode.WICBitmapInterpolationModeNearestNeighbor,
            EsImageQuality.High => WICBitmapInterpolationMode.WICBitmapInterpolationModeFant,
            EsImageQuality.Mid => WICBitmapInterpolationMode.WICBitmapInterpolationModeLinear,
            var other => throw new ArgumentOutOfRangeException(nameof(quality), other, "定義されていない品質の指定です。"),
        };
}