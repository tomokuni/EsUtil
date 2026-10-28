using System.Buffers;
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
/// <b>縮小は 2 系統あります</b>（<see cref="EsImageScalingMode"/>）: <c>Wic</c> で始まる方法は <b>WIC に任せ</b>
/// （読み込み・保存と同じ実装のため、経路によって画素が変わりません）、<c>Es</c> で始まる方法は
/// <b>本ライブラリの計算</b>です（縮小先に必要な画素だけを読むため速いが、WIC とは画素が一致しません）。
/// <b>減色の計算は WIC が単一所有</b>します（保存の 8bpp のインデックス カラーと同じパレットを使うため）。
/// 切り出しは画素のコピーだけであり、WIC の機能（<c>IWICBitmapClipper</c>）を使うと同じだけの写しが増えるため、
/// 行単位のコピーで行います。
/// </remarks>
internal static class EsBitmapOperations
{
    /// <summary>減色（パレット化）で残せる最小の色数。</summary>
    internal const int MinimumColorCount = 2;

    /// <summary>減色（パレット化）で残せる最大の色数（1 画素 1 バイトで表せる上限）。</summary>
    internal const int MaximumColorCount = 256;

    /// <summary>面積平均の縮小を並列化するかどうかの境目（元画像の画素数）。</summary>
    /// <remarks>
    /// <b>値の根拠</b>: 並列化の費用（範囲の分割と起動）は仕事の大きさに依らないため、損益分岐は
    /// 「並列で分けられる仕事」の画素数で決まる。 面積平均は全画素を 1 回読むため、その仕事は
    /// 元画像の画素数である。 実測（本値の 2 倍の余裕を見る前の損益分岐は約 5,500 画素）に
    /// <b>コア数が半分の機械でも並列が勝つ側へ留まるための 2 倍</b>を掛けた値である。<br/>
    /// 境界より大きい画像（スクリーンショットは常にこの側）は並列になる。
    /// </remarks>
    private const long ParallelSourceThresholdPixels = 11_264;

    /// <summary>双線形の縮小を並列化するかどうかの境目（<b>縮小後</b>の画素数）。</summary>
    /// <remarks>
    /// <b>値の根拠</b>: 双線形の費用は縮小先の画素数で決まる（1 画素につき元の 2x2 画素を読む）ため、
    /// 並列で分けられる仕事も縮小先の画素数である。 実測の損益分岐は約 1,500 画素であり、
    /// 面積平均より小さい画像で並列が有利になる（1 画素あたりの仕事が大きいため）。
    /// 本値は<b>コア数が半分の機械でも並列が勝つ側へ留まるための 2 倍</b>を掛けた値である。
    /// </remarks>
    private const long ParallelTargetThresholdPixels = 3_072;

    /// <summary>最近傍の縮小を並列化するかどうかの境目（<b>縮小後</b>の画素数）。</summary>
    /// <remarks>
    /// <b>値の根拠</b>: 最近傍は 1 画素あたりの仕事が最も小さい（4 バイトのコピー 1 回）ため、
    /// 並列化の損益分岐が最も高くなる（実測で約 18,000 画素。 3 つの縮小のうち最も大きい画像で
    /// 並列へ切り替わる）。 本値は<b>コア数が半分の機械でも並列が勝つ側へ留まるための 2 倍</b>を
    /// 掛けた値である。
    /// </remarks>
    private const long ParallelNearestThresholdPixels = 36_864;

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
    /// <param name="mode">縮小の方法（<see cref="EsImageScalingMode"/>）。</param>
    /// <returns>縮小した新しい画像（所有権は呼び出し側へ移る）。</returns>
    /// <remarks><b>中間画像を作りません</b>（縮小先の画像が持つ領域へ直接書きます）。</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSize"/> が許容範囲外の場合、または <paramref name="mode"/> が定義されていない値の場合。</exception>
    /// <exception cref="NotSupportedException"><paramref name="maxSize"/> が長辺より大きい（拡大が必要な）場合。</exception>
    [SupportedOSPlatform("windows10.0.17763")]
    internal static EsBitmap CreateScaled(
        ReadOnlySpan<byte> pixels, int sourceWidth, int sourceHeight, int stride, int maxSize, EsImageScalingMode mode)
    {
        var size = new EsSizeInt(sourceWidth, sourceHeight);
        var target = EsImageCodec.GetResizedSize(size, maxSize);
        var result = EsBitmap.CreateUninitialized(target);
        EsImageCodec.ScaleInto(pixels, size, stride, target, mode, result.Pixels);
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

    /// <summary>縮小した画素を呼び出し側の領域へ書き込む（指定の縮小方法で計算する）。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="target">縮小後の寸法（元より小さいこと）。</param>
    /// <param name="mode">縮小の方法（<see cref="EsImageScalingMode"/>）。</param>
    /// <param name="destination">書き込み先（<c>target.Width × 4 × target.Height</c> バイト以上。 行は密に書く）。</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> が足りない場合。</exception>
    /// <remarks>
    /// <b>画素を返さない理由</b>: 公開面（<c>EsImageCodec</c>）が呼び出し側の領域へ直接書けるようにするためです。
    /// 画像型は自分が確保した領域を渡すため、<b>縮小のためだけの中間画像を作りません</b>。<br/>
    /// <b><see cref="EsImageScalingMode.WicNearestNeighbor"/> など <c>Wic</c> で始まる方法は WIC に任せます</b>:
    /// デコード時の縮小（<c>IWICBitmapScaler</c>）と同じ実装であり、読み込み経路と画質が一致します。
    /// 元の画素をそのまま参照させ（複製せず）、結果を直接書き込みます。<br/>
    /// <b><see cref="EsImageScalingMode.EsBox"/> など <c>Es</c> で始まる方法は本ライブラリの計算です</b>:
    /// 縮小先に必要な画素だけを読むため、WIC より速い代わりに、読み込み経路（WIC）とは画素が一致しません。
    /// </remarks>
    [SupportedOSPlatform("windows10.0.17763")]
    internal static unsafe void ScaleInto(
        ReadOnlySpan<byte> pixels, int sourceWidth, int sourceHeight, int stride,
        EsSizeInt target, EsImageScalingMode mode, Span<byte> destination)
    {
        var destinationStride = target.Width * EsReadOnlyBitmap.BytesPerPixel;
        var expected = (long)destinationStride * target.Height;
        if (destination.Length < expected)
        {
            throw new ArgumentException(
                $"書き込み先が足りません（{target.Width}x{target.Height} は {expected} バイト。 実際は {destination.Length} バイト）。",
                nameof(destination));
        }

        fixed (byte* source = pixels)
        fixed (byte* sink = destination)
        {
            switch (mode)
            {
                case EsImageScalingMode.EsNearestNeighbor:
                    ScaleNearest(source, sourceWidth, sourceHeight, stride, sink, destinationStride, target.Width, target.Height);
                    break;
                case EsImageScalingMode.EsBilinear:
                    ScaleBilinear(source, sourceWidth, sourceHeight, stride, sink, destinationStride, target.Width, target.Height);
                    break;
                case EsImageScalingMode.EsBox:
                    ScaleBox(source, sourceWidth, sourceHeight, stride, sink, destinationStride, target.Width, target.Height);
                    break;
                default:
                    // WIC の 5 つの補間は、そのまま WIC の指定へ写して任せる
                    WinWicImageCodec.Resize(
                        pixels, sourceWidth, sourceHeight, stride, target.Width, target.Height,
                        ToInterpolationMode(mode), destination);
                    break;
            }
        }
    }

    /// <summary>縮小の方法を WIC の補間の指定へ写す。</summary>
    /// <param name="mode">縮小の方法。 <c>Wic</c> で始まる値であること。</param>
    /// <returns>WIC の補間の指定。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> が WIC の方法でない場合、または定義されていない値の場合。</exception>
    /// <remarks>
    /// <b>対応は 1 対 1 です</b>: <see cref="EsImageScalingMode.WicNearestNeighbor"/> =
    /// <c>WICBitmapInterpolationModeNearestNeighbor</c> / <see cref="EsImageScalingMode.WicLinear"/> =
    /// <c>WICBitmapInterpolationModeLinear</c> / <see cref="EsImageScalingMode.WicCubic"/> =
    /// <c>WICBitmapInterpolationModeCubic</c> / <see cref="EsImageScalingMode.WicFant"/> =
    /// <c>WICBitmapInterpolationModeFant</c> / <see cref="EsImageScalingMode.WicHighQualityCubic"/> =
    /// <c>WICBitmapInterpolationModeHighQualityCubic</c>。 <b>本ライブラリの計算（<c>Es</c> で始まる方法）は
    /// 写せません</b>（WIC の補間ではないため例外にします）。
    /// </remarks>
    internal static WICBitmapInterpolationMode ToInterpolationMode(EsImageScalingMode mode) => mode switch
    {
        EsImageScalingMode.WicNearestNeighbor => WICBitmapInterpolationMode.WICBitmapInterpolationModeNearestNeighbor,
        EsImageScalingMode.WicLinear => WICBitmapInterpolationMode.WICBitmapInterpolationModeLinear,
        EsImageScalingMode.WicCubic => WICBitmapInterpolationMode.WICBitmapInterpolationModeCubic,
        EsImageScalingMode.WicFant => WICBitmapInterpolationMode.WICBitmapInterpolationModeFant,
        EsImageScalingMode.WicHighQualityCubic => WICBitmapInterpolationMode.WICBitmapInterpolationModeHighQualityCubic,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "WIC の補間へ写せない縮小方法です。"),
    };
    /// <summary>最近傍補間で縮小する（<see cref="EsImageScalingMode.EsNearestNeighbor"/> の実装）。</summary>
    /// <param name="pixels">対象の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="destination">書き込み先（<c>targetWidth × 4 × targetHeight</c> バイト以上）。</param>
    /// <param name="destinationStride">書き込み先の 1 ラインあたりのバイト数。</param>
    /// <param name="targetWidth">縮小後の幅（ピクセル）。</param>
    /// <param name="targetHeight">縮小後の高さ（ピクセル）。</param>
    /// <remarks>
    /// <b>ポインターで受ける理由</b>: 行の範囲を並列実行する処理（<see cref="RunRowRanges"/>）へ渡すラムダは
    /// <c>ReadOnlySpan</c> を<b>取り込めない</b>（<c>ref struct</c> はラムダが保持できない）ためです
    /// （ポインターは取り込めます）。
    /// </remarks>
    private static unsafe void ScaleNearest(
        byte* pixels, int sourceWidth, int sourceHeight, int stride,
        byte* destination, int destinationStride, int targetWidth, int targetHeight)
    {
        var srcWidth = sourceWidth;

        // 列の対応（最近傍）を先に求め、内側のループでは割り算をしない
        var colMap = ArrayPool<int>.Shared.Rent(targetWidth);
        try
        {
            for (var x = 0; x < targetWidth; x++)
            {
                colMap[x] = Math.Min(srcWidth - 1, (int)((long)x * srcWidth / targetWidth));
            }

            // 1 行あたりの仕事が小さいため、縮小後の画素数で並列化を判定する（最近傍は最も大きいしきい値を使う）
            if ((long)targetWidth * targetHeight >= ParallelNearestThresholdPixels)
            {
                RunRowRanges(targetHeight, (from, to) => NearestRows(
                    pixels, sourceWidth, sourceHeight, stride, destination, destinationStride, targetWidth, targetHeight, colMap, from, to));
            }
            else
            {
                NearestRows(
                    pixels, sourceWidth, sourceHeight, stride, destination, destinationStride, targetWidth, targetHeight, colMap, 0, targetHeight);
            }
        }
        finally
        {
            // 内容は次の借り手が消す（返すときに消すと、その費用を毎回払う）
            ArrayPool<int>.Shared.Return(colMap);
        }
    }

    /// <summary>縮小先の行範囲を最近傍で求め、書き込む。</summary>
    /// <param name="pixels">対象の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="destination">書き込み先（<paramref name="yStart"/>〜<paramref name="yEnd"/> の行を書く）。</param>
    /// <param name="destinationStride">書き込み先の 1 ラインあたりのバイト数。</param>
    /// <param name="targetWidth">縮小後の幅。</param>
    /// <param name="targetHeight">縮小後の高さ。</param>
    /// <param name="colMap">縮小先の列に対応する元の列（事前に求めた対応表）。</param>
    /// <param name="yStart">開始行（含む）。</param>
    /// <param name="yEnd">終了行（含まない）。</param>
    private static unsafe void NearestRows(
        byte* pixels, int sourceWidth, int sourceHeight, int stride,
        byte* destination, int destinationStride, int targetWidth, int targetHeight,
        int[] colMap, int yStart, int yEnd)
    {
        var srcWidth = sourceWidth;
        var srcHeight = sourceHeight;
        var srcStrideUInt = stride / sizeof(uint);
        var dstStrideUInt = destinationStride / sizeof(uint);
        // 余白を含む全領域（= 行幅 × 高さ）を uint として見る
        var src = new ReadOnlySpan<uint>(pixels, checked(srcStrideUInt * srcHeight));
        var dst = new Span<uint>(destination, checked(dstStrideUInt * targetHeight));

        for (var y = yStart; y < yEnd; y++)
        {
            var srcRow = src.Slice(Math.Min(srcHeight - 1, (int)((long)y * srcHeight / targetHeight)) * srcStrideUInt, srcWidth);
            var dstRow = dst.Slice(y * dstStrideUInt, targetWidth);
            for (var x = 0; x < targetWidth; x++)
            {
                dstRow[x] = srcRow[colMap[x]];
            }
        }
    }

    /// <summary>双線形補間で縮小する（<see cref="EsImageScalingMode.EsBilinear"/> の実装）。</summary>
    /// <param name="pixels">対象の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="destination">書き込み先（<c>targetWidth × 4 × targetHeight</c> バイト以上）。</param>
    /// <param name="destinationStride">書き込み先の 1 ラインあたりのバイト数。</param>
    /// <param name="targetWidth">縮小後の幅（ピクセル）。</param>
    /// <param name="targetHeight">縮小後の高さ（ピクセル）。</param>
    private static unsafe void ScaleBilinear(
        byte* pixels, int sourceWidth, int sourceHeight, int stride,
        byte* destination, int destinationStride, int targetWidth, int targetHeight)
    {
        // 列の対応（元の 2 列の位置と重み）は行に依存しないため、1 度だけ求めて表にする
        // （画素ごとに除算・床・クランプを行わない）
        var leftX = ArrayPool<int>.Shared.Rent(targetWidth);
        var rightX = ArrayPool<int>.Shared.Rent(targetWidth);
        var fx = ArrayPool<double>.Shared.Rent(targetWidth);
        try
        {
            for (var x = 0; x < targetWidth; x++)
            {
                var sourceX = (((x + 0.5) * sourceWidth) / targetWidth) - 0.5;
                var x0 = Math.Clamp((int)Math.Floor(sourceX), 0, sourceWidth - 1);
                leftX[x] = x0 * EsReadOnlyBitmap.BytesPerPixel;
                rightX[x] = Math.Min(x0 + 1, sourceWidth - 1) * EsReadOnlyBitmap.BytesPerPixel;
                fx[x] = Math.Clamp(sourceX - x0, 0.0, 1.0);
            }

            // 1 行あたりの仕事が小さいため、縮小後の画素数で並列化を判定する
            if ((long)targetWidth * targetHeight >= ParallelTargetThresholdPixels)
            {
                RunRowRanges(targetHeight, (from, to) => BilinearRows(
                    pixels, sourceHeight, stride, destination, destinationStride, from, to,
                    targetWidth, targetHeight, leftX, rightX, fx));
            }
            else
            {
                BilinearRows(
                    pixels, sourceHeight, stride, destination, destinationStride, 0, targetHeight,
                    targetWidth, targetHeight, leftX, rightX, fx);
            }
        }
        finally
        {
            // 内容は次の借り手が消す（返すときに消すと、その費用を毎回払う）
            ArrayPool<double>.Shared.Return(fx);
            ArrayPool<int>.Shared.Return(rightX);
            ArrayPool<int>.Shared.Return(leftX);
        }
    }

    /// <summary>縮小先の行範囲を双線形補間で求め、書き込む。</summary>
    /// <param name="pixels">対象の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="destination">書き込み先（<paramref name="yStart"/>〜<paramref name="yEnd"/> の行を書く）。</param>
    /// <param name="destinationStride">書き込み先の 1 ラインあたりのバイト数。</param>
    /// <param name="yStart">開始行（含む）。</param>
    /// <param name="yEnd">終了行（含まない）。</param>
    /// <param name="targetWidth">縮小後の幅。</param>
    /// <param name="targetHeight">縮小後の高さ。</param>
    /// <param name="leftX">縮小先の列が参照する元の左の列のバイト オフセット（事前計算）。</param>
    /// <param name="rightX">縮小先の列が参照する元の右の列のバイト オフセット（事前計算）。</param>
    /// <param name="fx">左の列と右の列の重み（0〜1。 事前計算）。</param>
    /// <remarks>
    /// <b>行を範囲で受ける理由</b>: 1 行ずつメソッドを呼ぶと、行ごとに画素のビューを取り直すうえ、
    /// メソッド境界を越えることで<b>境界チェックの除去とベクター化の最適化が失われる</b>。
    /// 範囲を受け取れば、逐次（1 回で全行）と並列（範囲ごと）が<b>同じ実装</b>を使いながら最適化を保てます。<br/>
    /// <b>列の対応を引数で受ける理由</b>: 列の対応（元の 2 列と重み）は行に依存しないため、行ごとに
    /// 除算・床・クランプをやり直さない（<see cref="ScaleBilinear"/> が 1 度だけ求める）。<br/>
    /// <b>行の余白を含む全領域を 1 つのビューにします</b>（行の先頭は <paramref name="stride"/> で求める）。
    /// </remarks>
    private static unsafe void BilinearRows(
        byte* pixels, int sourceHeight, int stride,
        byte* destination, int destinationStride, int yStart, int yEnd,
        int targetWidth, int targetHeight,
        int[] leftX, int[] rightX, double[] fx)
    {
        var srcHeight = sourceHeight;
        var srcStride = stride;
        var src = new ReadOnlySpan<byte>(pixels, checked(srcStride * sourceHeight));
        var dst = new Span<byte>(destination, checked(destinationStride * targetHeight));
        var dstStride = destinationStride;
        var bytesPerPixel = EsReadOnlyBitmap.BytesPerPixel;

        for (var y = yStart; y < yEnd; y++)
        {
            // 画素の中心が対応するように元の座標を求める（端は同じ画素でクランプする）
            var sourceY = (((y + 0.5) * srcHeight) / targetHeight) - 0.5;
            var y0 = Math.Clamp((int)Math.Floor(sourceY), 0, srcHeight - 1);
            var y1 = Math.Min(y0 + 1, srcHeight - 1);
            var fy = Math.Clamp(sourceY - y0, 0.0, 1.0);
            var row0 = y0 * srcStride;
            var row1 = y1 * srcStride;
            var dstRow = dst[(y * dstStride)..];

            for (var x = 0; x < targetWidth; x++)
            {
                var left = leftX[x];
                var right = rightX[x];
                var f = fx[x];
                var offset = x * bytesPerPixel;

                for (var c = 0; c < bytesPerPixel; c++)
                {
                    var top = (src[row0 + left + c] * (1 - f)) + (src[row0 + right + c] * f);
                    var bottom = (src[row1 + left + c] * (1 - f)) + (src[row1 + right + c] * f);
                    dstRow[offset + c] = (byte)((top * (1 - fy)) + (bottom * fy));
                }
            }
        }
    }

    /// <summary>縮小先の行を範囲へ分け、並列実行する。</summary>
    /// <param name="height">縮小後の高さ。</param>
    /// <param name="runRange">1 つの範囲（開始行・終了行）を処理する処理。</param>
    /// <remarks>
    /// <b>行を範囲でまとめる理由</b>: 1 行ずつ処理を呼ぶと、行ごとに画素のビューを取り直すうえ、
    /// メソッド境界を越えることで<b>境界チェックの除去などの最適化が失われる</b>。
    /// 範囲を受け取れば、逐次（1 回で全行）と
    /// 並列（範囲ごと）が<b>同じ実装</b>を使いながら最適化を保てる。<br/>
    /// <b>分割数</b>: CPU 数 × 4 を目安にし、1 範囲あたり最低 1 行を確保する（行ごとの仕事が小さいアルゴリズムでも
    /// 範囲の中でループが連続するため、範囲が細かすぎるより速い）。
    /// </remarks>
    private static void RunRowRanges(int height, Action<int, int> runRange)
    {
        var chunk = Math.Max(1, height / (Environment.ProcessorCount * 4));
        var ranges = new List<(int From, int To)>();
        for (var y = 0; y < height; y += chunk)
        {
            ranges.Add((y, Math.Min(y + chunk, height)));
        }

        Parallel.ForEach(ranges, range => runRange(range.From, range.To));
    }

    /// <summary>面積平均（box）で縮小する（<see cref="EsImageScalingMode.EsBox"/> の実装）。</summary>
    /// <param name="pixels">対象の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="destination">書き込み先（<c>targetWidth × 4 × targetHeight</c> バイト以上）。</param>
    /// <param name="destinationStride">書き込み先の 1 ラインあたりのバイト数。</param>
    /// <param name="targetWidth">縮小後の幅（ピクセル）。</param>
    /// <param name="targetHeight">縮小後の高さ（ピクセル）。</param>
    /// <remarks>
    /// <b>1 パスで面積平均を求める</b>: 縮小先の 1 画素ごとに、対応する元画像の矩形（全画素）を合計して割る。
    /// <b>中間画像を作らない</b>ため、横 → 縦 の 2 パスで生じる丸めの重なりが無く、速度も速い。<br/>
    /// <b>列の範囲は行に依存しない</b>ため、1 度だけ求めて表にする（画素ごとの 64 ビット除算を無くす）。
    /// 成分ごとの合計は <c>stackalloc</c> の領域ではなくローカル変数で持つ（画素ごとに領域を消す書き込みを
    /// 無くす）。<br/>
    /// <b>行ごとに独立しているため並列化する</b>。 並列化するのは元画像が
    /// <see cref="ParallelSourceThresholdPixels"/> 以上のときだけである（小さい画像では並列化の費用が勝つ）。<br/>
    /// <b>SIMD は使わない</b>: 面積平均をベクター化すると<b>縦の合計を保持する作業行が必要になり、
    /// 縮小先の行ごとに全体を消すコストが加わる</b>ため、かえって遅くなります。 現在の実装は元画像の各画素を
    /// 1 回だけ読んで合計するため、読み出しの回数は既に最小です。
    /// </remarks>
    private static unsafe void ScaleBox(
        byte* pixels, int sourceWidth, int sourceHeight, int stride,
        byte* destination, int destinationStride, int targetWidth, int targetHeight)
    {
        var srcWidth = sourceWidth;
        var srcHeight = sourceHeight;

        // 列の範囲は行に依存しないため、1 度だけ求めて表にする（画素ごとに 64 ビット除算を行わない）
        var x0s = ArrayPool<int>.Shared.Rent(targetWidth);
        var x1s = ArrayPool<int>.Shared.Rent(targetWidth);
        try
        {
            for (var x = 0; x < targetWidth; x++)
            {
                x0s[x] = (int)((long)x * srcWidth / targetWidth);
                x1s[x] = Math.Max(x0s[x] + 1, (int)((long)(x + 1) * srcWidth / targetWidth));
            }

            if ((long)srcWidth * srcHeight >= ParallelSourceThresholdPixels)
            {
                RunRowRanges(targetHeight, (from, to) => AverageBlockRows(
                    pixels, sourceHeight, stride, destination, destinationStride, from, to,
                    targetWidth, targetHeight, x0s, x1s));
            }
            else
            {
                AverageBlockRows(
                    pixels, sourceHeight, stride, destination, destinationStride, 0, targetHeight,
                    targetWidth, targetHeight, x0s, x1s);
            }
        }
        finally
        {
            // 内容は次の借り手が消す（返すときに消すと、その費用を毎回払う）
            ArrayPool<int>.Shared.Return(x1s);
            ArrayPool<int>.Shared.Return(x0s);
        }
    }

    /// <summary>縮小先の行範囲を面積平均で求める（行ごとに独立しているため並列実行できる）。</summary>
    /// <param name="pixels">対象の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="destination">書き込み先（<paramref name="yStart"/>〜<paramref name="yEnd"/> の行を書く）。</param>
    /// <param name="destinationStride">書き込み先の 1 ラインあたりのバイト数。</param>
    /// <param name="yStart">開始行（含む）。</param>
    /// <param name="yEnd">終了行（含まない）。</param>
    /// <param name="targetWidth">縮小後の幅。</param>
    /// <param name="targetHeight">縮小後の高さ。</param>
    /// <param name="x0s">縮小先の列が受け持つ元の範囲の開始列（事前計算）。</param>
    /// <param name="x1s">縮小先の列が受け持つ元の範囲の終了列（含まない。 事前計算）。</param>
    /// <remarks>
    /// <b>合計をローカル変数で持つ理由</b>: 成分ごとの合計を <c>stackalloc</c> の領域で持つと、
    /// 縮小先の画素ごとに領域を消す書き込みが加わる。<br/>
    /// <b>列の範囲を引数で受ける理由</b>: 列の範囲は行に依存しないため、画素ごとに 64 ビット除算を行わない
    /// （<see cref="ScaleBox"/> が 1 度だけ求める）。<br/>
    /// <b>行の余白を含む全領域を 1 つのビューにします</b>（行の先頭は <paramref name="stride"/> で求める）。
    /// </remarks>
    private static unsafe void AverageBlockRows(
        byte* pixels, int sourceHeight, int stride,
        byte* destination, int destinationStride, int yStart, int yEnd,
        int targetWidth, int targetHeight, int[] x0s, int[] x1s)
    {
        var srcHeight = sourceHeight;
        var srcStride = stride;
        var src = new ReadOnlySpan<byte>(pixels, checked(srcStride * sourceHeight));
        var dst = new Span<byte>(destination, checked(destinationStride * targetHeight));
        var dstStride = destinationStride;
        var bytesPerPixel = EsReadOnlyBitmap.BytesPerPixel;

        for (var y = yStart; y < yEnd; y++)
        {
            // 縮小先の 1 画素が受け持つ元の範囲（最低 1 画素）
            var y0 = (int)((long)y * srcHeight / targetHeight);
            var y1 = Math.Max(y0 + 1, (int)((long)(y + 1) * srcHeight / targetHeight));
            var dstRow = dst[(y * dstStride)..];

            for (var x = 0; x < targetWidth; x++)
            {
                var x0 = x0s[x];
                var x1 = x1s[x];

                // 合計は画素数の最大（4K 全体で 830 万）に 255 を掛けても int に収まらないため long で持つ
                long sumB = 0;
                long sumG = 0;
                long sumR = 0;
                long sumA = 0;
                for (var sy = y0; sy < y1; sy++)
                {
                    var row = sy * srcStride;
                    for (var sx = x0; sx < x1; sx++)
                    {
                        var index = row + (sx * bytesPerPixel);
                        sumB += src[index];
                        sumG += src[index + 1];
                        sumR += src[index + 2];
                        sumA += src[index + 3];
                    }
                }

                var count = (y1 - y0) * (x1 - x0);
                var offset = x * bytesPerPixel;
                dstRow[offset] = (byte)(sumB / count);
                dstRow[offset + 1] = (byte)(sumG / count);
                dstRow[offset + 2] = (byte)(sumR / count);
                dstRow[offset + 3] = (byte)(sumA / count);
            }
        }
    }
}
