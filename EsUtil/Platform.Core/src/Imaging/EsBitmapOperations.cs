using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win.Wic;

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
/// <b>ポインターで受ける箇所がある理由</b>: 縮小は行の範囲を並列実行するため、そのラムダは <c>ref struct</c>
/// （<see cref="ReadOnlySpan{T}"/>）を取り込めません（呼び出し側の <see cref="Resize"/> が <c>fixed</c> で固定します）。<br/>
/// <b>戻り値の権利</b>: 新しく作った画像は<b>書き換え可能</b>（<see cref="EsBitmap"/>）で返します
/// （生成した側が所有し、破棄する責任を持つ）。 複製が要る場合は <see cref="EsReadOnlyBitmap.Copy"/>、0 コピーの
/// 読み取りビューが要る場合は <see cref="EsReadOnlyBitmap.ToPixmap"/> を使います。<br/>
/// <b>減色の計算はここに置きません</b>: 減色は「新しい画像を作る」操作として公開しますが
/// （<see cref="ReduceColors"/>）、その計算は<b>WIC が単一所有</b>します（保存の 8bpp のインデックス カラーと
/// 同じパレットを使うため。 2 箇所に書くと、同じ色数でも保存経路によって結果が変わります）。
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

    /// <summary>長辺を上限へ収めた画像を生成する（公開面のメンバーの実装）。</summary>
    /// <param name="pixels">対象の画素（BGRA8888。 <b>行の余白を含んでよい</b>）。 借用であり破棄しない。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数（<c>sourceWidth × 4</c> 以上）。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 正の値で、長辺以下。</param>
    /// <param name="quality">縮小の品質。 既定は <see cref="EsImageQuality.Mid"/>（双線形）。</param>
    /// <returns>生成された画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// 規則は公開面が単一所有する（<see cref="EsReadOnlyBitmap.Resize(int, EsImageQuality)"/> の備考）。<br/>
    /// <b>ポインターへ切り替えるのは縮小の内側だけです</b>: 行の範囲を並列実行する処理へ渡すラムダは
    /// <c>ReadOnlySpan</c> を取り込めない（<c>ref struct</c> は保持できない）ため、<c>fixed</c> で固定した位置から
    /// 各アルゴリズムへ渡します（余白を含む全領域を 1 つの領域として扱います）。<br/>
    /// <b>用途</b>: 大きな画像を小さく扱う場面（一覧のサムネイル・プレビュー等）。 元の寸法の複製を保持すると
    /// 4K 1 枚で 32 MB になるため、表示や転送の前に本メソッドで縮小する。<br/>
    /// <b>名前が <c>Resize</c> である理由</b>: 将来の拡大対応を見込んで双方向の名前を残している（現在の実装は縮小だけを
    /// 行う）。 拡大が必要な要求は黙って等倍を返さず <see cref="NotSupportedException"/> にする（「拡大された」と
    /// 誤解されるため）。<br/>
    /// <b>品質とアルゴリズムの対応</b>: <see cref="EsImageQuality.Low"/> → 最近傍（<see cref="ScaleNearest"/>）、
    /// <see cref="EsImageQuality.Mid"/> → 双線形（<see cref="ScaleBilinear"/>）、<see cref="EsImageQuality.High"/> →
    /// 面積平均（<see cref="ScaleBox"/>）。 <b>アルゴリズムの名前を公開面へ出さない理由</b>: 利用者が選ぶのは
    /// 「速度と画質のどちらを取るか」であり、フィルターの実装名は差し替えの自由を奪うためです。<br/>
    /// <b>並列化</b>: どの段階も<b>縮小先の行を範囲へ分けて並列実行する</b>（基準は
    /// <see cref="RunRowRanges"/> を参照。 小さい画像では逐次のままにする）。<br/>
    /// <b>返す寸法の丸め</b>: 切り捨てで丸めるため<b>1 px 未満の誤差が出うる</b>（座標の対応付けが必要な場面では使わない）。
    /// 長辺と上限が一致する場合は縮小不要のため<b>等倍の複製</b>を返す。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSize"/> が 0 以下の場合、または
    /// <paramref name="quality"/> が定義されていない値の場合。</exception>
    /// <exception cref="NotSupportedException"><paramref name="maxSize"/> が長辺より大きい（＝拡大が必要な）場合。</exception>
    internal static unsafe EsBitmap Resize(
        ReadOnlySpan<byte> pixels, int sourceWidth, int sourceHeight, int stride,
        int maxSize, EsImageQuality quality = EsImageQuality.Mid)
    {
        if (maxSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "長辺の上限は正の値である必要があります。");
        }

        if (quality is not (EsImageQuality.Low or EsImageQuality.Mid or EsImageQuality.High))
        {
            // 寸法の判定より先に見る（縮小が不要な場合でも値の誤りは黙って通さない）
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "定義されていない縮小の品質です。");
        }

        var width = sourceWidth;
        var height = sourceHeight;
        var longSide = Math.Max(width, height);
        if (maxSize > longSide)
        {
            // 拡大は未サポート（等倍を返すと「拡大された」と誤解されるため、黙って通さない）
            throw new NotSupportedException(
                $"拡大（{width}x{height} → 長辺 {maxSize}）は未サポートです。 長辺以下の上限を渡してください。");
        }

        if (maxSize == longSide)
        {
            // 縮小が不要
            return Crop(pixels, sourceWidth, sourceHeight, stride, 0, 0, width, height);
        }

        // 長辺を上限へ収める倍率（縦横比を保つ。 短辺は最低 1 px）
        var ratio = (double)maxSize / longSide;
        var target = new EsSizeInt(Math.Max(1, (int)(width * ratio)), Math.Max(1, (int)(height * ratio)));

        // 縮小は行の範囲を並列実行するため、内側はポインターで扱う（ラムダは span を取り込めない）
        fixed (byte* p = pixels)
        {
            return quality switch
            {
                EsImageQuality.Low => ScaleNearest(p, sourceWidth, sourceHeight, stride, target),
                EsImageQuality.High => ScaleBox(p, sourceWidth, sourceHeight, stride, target),
                _ => ScaleBilinear(p, sourceWidth, sourceHeight, stride, target),
            };
        }
    }

    /// <summary>画素を指定の色数へ減色する（公開面のメンバーの実装）。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="colorCount">残す色数（2〜256。 呼び出し側が検証済みであること）。</param>
    /// <param name="dither">ディザの指定。 呼び出し側が検証済みであること。</param>
    /// <returns>減色した画像。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="colorCount"/> が 2〜256 の範囲外の場合、
    /// または <paramref name="dither"/> が定義されていない値の場合。</exception>
    /// <remarks>
    /// <b>減色の実装は WIC が単一所有します</b>（<c>WinWicImageCodec.Reduce</c>）。 本クラスは引数を検証して
    /// 渡すだけです（切り出し・縮小と同じく、実装は 1 か所に置き、公開面は画像型が持ちます）。<br/>
    /// <b>パレット化の計算を自前で持たない理由</b>: 同じ「色数へ減らす」計算を 2 つ持つと、
    /// <b>保存したファイルの画素が経路によって変わる</b>ためです（保存は 8bpp のインデックスとして書き、
    /// 本メソッドは画素へ戻すため、同じパレットでなければ一致しません）。
    /// </remarks>
    [SupportedOSPlatform("windows10.0.17763")]
    internal static EsBitmap ReduceColors(
        ReadOnlySpan<byte> pixels, int width, int height, int stride, int colorCount, EsDitherMode dither)
    {
        if (colorCount < MinimumColorCount || colorCount > MaximumColorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(colorCount), colorCount, $"減色して残す色数は {MinimumColorCount}〜{MaximumColorCount} の範囲で指定します。");
        }

        if (dither is not (EsDitherMode.None or EsDitherMode.ErrorDiffusion))
        {
            throw new ArgumentOutOfRangeException(nameof(dither), dither, "定義されていないディザの指定です。");
        }

        var result = WinWicImageCodec.Reduce(pixels, width, height, stride, colorCount, dither);
        return EsBitmap.FromPixelCopy(width, height, result);
    }

    /// <summary>最近傍補間で縮小する（<see cref="EsImageQuality.Low"/> の実装）。</summary>
    /// <param name="pixels">対象の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="target">縮小後の寸法（元より小さい）。</param>
    /// <returns>縮小した画像。</returns>
    /// <remarks>
    /// <b>ポインターで受ける理由</b>: 行の範囲を並列実行する処理（<see cref="RunRowRanges"/>）へ渡すラムダは
    /// <c>ReadOnlySpan</c> を<b>取り込めない</b>（<c>ref struct</c> はラムダが保持できない）ためです
    /// （ポインターは取り込めます）。
    /// </remarks>
    private static unsafe EsBitmap ScaleNearest(
        byte* pixels, int sourceWidth, int sourceHeight, int stride, EsSizeInt target)
    {
        var result = EsBitmap.CreateUninitialized(target.Width, target.Height);
        var srcWidth = sourceWidth;

        // 列の対応（最近傍）を先に求め、内側のループでは割り算をしない
        var colMap = ArrayPool<int>.Shared.Rent(target.Width);
        try
        {
            for (var x = 0; x < target.Width; x++)
            {
                colMap[x] = Math.Min(srcWidth - 1, (int)((long)x * srcWidth / target.Width));
            }

            // 1 行あたりの仕事が小さいため、縮小後の画素数で並列化を判定する（最近傍は最も大きいしきい値を使う）
            if ((long)target.Width * target.Height >= ParallelNearestThresholdPixels)
            {
                RunRowRanges(target.Height, (from, to) => NearestRows(pixels, sourceWidth, sourceHeight, stride, result, colMap, from, to));
            }
            else
            {
                NearestRows(pixels, sourceWidth, sourceHeight, stride, result, colMap, 0, target.Height);
            }

            return result;
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
    /// <param name="result">書き込み先（<paramref name="yStart"/>〜<paramref name="yEnd"/> の行を書く）。</param>
    /// <param name="colMap">縮小先の列に対応する元の列（事前に求めた対応表）。</param>
    /// <param name="yStart">開始行（含む）。</param>
    /// <param name="yEnd">終了行（含まない）。</param>
    private static unsafe void NearestRows(
        byte* pixels, int sourceWidth, int sourceHeight, int stride,
        EsBitmap result, int[] colMap, int yStart, int yEnd)
    {
        var srcWidth = sourceWidth;
        var srcHeight = sourceHeight;
        var srcStrideUInt = stride / sizeof(uint);
        var dstStrideUInt = result.Stride / sizeof(uint);
        // 余白を含む全領域（= 行幅 × 高さ）を uint として見る
        var src = new ReadOnlySpan<uint>(pixels, checked(srcStrideUInt * srcHeight));
        var dst = MemoryMarshal.Cast<byte, uint>(result.Pixels);
        var targetHeight = result.Height;
        var targetWidth = result.Width;

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
    /// <summary>双線形補間で縮小する（<see cref="EsImageQuality.Mid"/> の実装）。</summary>
    /// <param name="pixels">対象の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="target">縮小後の寸法（元より小さい）。</param>
    /// <returns>縮小した画像。</returns>
    private static unsafe EsBitmap ScaleBilinear(
        byte* pixels, int sourceWidth, int sourceHeight, int stride, EsSizeInt target)
    {
        var result = EsBitmap.CreateUninitialized(target.Width, target.Height);

        // 列の対応（元の 2 列の位置と重み）は行に依存しないため、1 度だけ求めて表にする
        // （画素ごとに除算・床・クランプを行わない）
        var leftX = ArrayPool<int>.Shared.Rent(target.Width);
        var rightX = ArrayPool<int>.Shared.Rent(target.Width);
        var fx = ArrayPool<double>.Shared.Rent(target.Width);
        try
        {
            for (var x = 0; x < target.Width; x++)
            {
                var sourceX = (((x + 0.5) * sourceWidth) / target.Width) - 0.5;
                var x0 = Math.Clamp((int)Math.Floor(sourceX), 0, sourceWidth - 1);
                leftX[x] = x0 * EsReadOnlyBitmap.BytesPerPixel;
                rightX[x] = Math.Min(x0 + 1, sourceWidth - 1) * EsReadOnlyBitmap.BytesPerPixel;
                fx[x] = Math.Clamp(sourceX - x0, 0.0, 1.0);
            }

            // 1 行あたりの仕事が小さいため、縮小後の画素数で並列化を判定する
            if ((long)target.Width * target.Height >= ParallelTargetThresholdPixels)
            {
                RunRowRanges(target.Height, (from, to) => BilinearRows(
                    pixels, sourceHeight, stride, result, from, to, target, leftX, rightX, fx));
            }
            else
            {
                BilinearRows(pixels, sourceHeight, stride, result, 0, target.Height, target, leftX, rightX, fx);
            }

            return result;
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
    /// <param name="result">書き込み先（<paramref name="yStart"/>〜<paramref name="yEnd"/> の行を書く）。</param>
    /// <param name="yStart">開始行（含む）。</param>
    /// <param name="yEnd">終了行（含まない）。</param>
    /// <param name="target">縮小後の寸法。</param>
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
        EsBitmap result, int yStart, int yEnd, EsSizeInt target,
        int[] leftX, int[] rightX, double[] fx)
    {
        var srcHeight = sourceHeight;
        var srcStride = stride;
        var src = new ReadOnlySpan<byte>(pixels, checked(srcStride * sourceHeight));
        var dst = result.Pixels;
        var dstStride = result.Stride;
        var bytesPerPixel = EsReadOnlyBitmap.BytesPerPixel;

        for (var y = yStart; y < yEnd; y++)
        {
            // 画素の中心が対応するように元の座標を求める（端は同じ画素でクランプする）
            var sourceY = (((y + 0.5) * srcHeight) / target.Height) - 0.5;
            var y0 = Math.Clamp((int)Math.Floor(sourceY), 0, srcHeight - 1);
            var y1 = Math.Min(y0 + 1, srcHeight - 1);
            var fy = Math.Clamp(sourceY - y0, 0.0, 1.0);
            var row0 = y0 * srcStride;
            var row1 = y1 * srcStride;
            var dstRow = dst[(y * dstStride)..];

            for (var x = 0; x < target.Width; x++)
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

    /// <summary>面積平均（box）で縮小する（<see cref="EsImageQuality.High"/> の実装）。</summary>
    /// <param name="pixels">対象の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="target">縮小後の寸法（元より小さい）。</param>
    /// <returns>縮小した画像。</returns>
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
    private static unsafe EsBitmap ScaleBox(
        byte* pixels, int sourceWidth, int sourceHeight, int stride, EsSizeInt target)
    {
        var result = EsBitmap.CreateUninitialized(target.Width, target.Height);
        var srcWidth = sourceWidth;
        var srcHeight = sourceHeight;
        var targetWidth = target.Width;

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
                RunRowRanges(target.Height, (from, to) => AverageBlockRows(
                    pixels, sourceHeight, stride, result, from, to, target.Height, targetWidth, x0s, x1s));
            }
            else
            {
                AverageBlockRows(
                    pixels, sourceHeight, stride, result, 0, target.Height, target.Height, targetWidth, x0s, x1s);
            }

            return result;
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
    /// <param name="result">書き込み先（<paramref name="yStart"/>〜<paramref name="yEnd"/> の行を書く）。</param>
    /// <param name="yStart">開始行（含む）。</param>
    /// <param name="yEnd">終了行（含まない）。</param>
    /// <param name="targetHeight">縮小後の高さ。</param>
    /// <param name="targetWidth">縮小後の幅。</param>
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
        EsBitmap result, int yStart, int yEnd,
        int targetHeight, int targetWidth, int[] x0s, int[] x1s)
    {
        var srcHeight = sourceHeight;
        var srcStride = stride;
        var src = new ReadOnlySpan<byte>(pixels, checked(srcStride * sourceHeight));
        var dst = result.Pixels;
        var dstStride = result.Stride;
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
