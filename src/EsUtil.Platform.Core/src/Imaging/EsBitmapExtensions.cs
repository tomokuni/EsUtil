using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像を<b>変更しない</b>操作（切り出し・縮小）の拡張メソッド。</summary>
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
    /// <summary>面積平均の縮小を並列化するかどうかの境目（元画像の画素数）。</summary>
    /// <remarks>
    /// <b>実測で決めた値</b>: 6 千画素（100x60）でも並列が速い（逐次 57 µs / 並列 8.6 µs、120 千画素で 948 → 56 µs）が、
    /// 絶対時間が小さい場面でスレッドプールを触らないようにするため 32,768 画素（181x181 相当）を境にする。
    /// スクリーンショット（1080p で 200 万画素、4K で 830 万画素）は常に並列になる。
    /// </remarks>
    private const long ParallelSourceThresholdPixels = 32_768;

    /// <summary>双線形の縮小を並列化するかどうかの境目（<b>縮小後</b>の画素数）。</summary>
    /// <remarks>
    /// <b>実測で決めた値</b>: 1 行あたりの仕事が面積平均より小さいため、元画像ではなく縮小後の画素数で判定する。
    /// 32,768 画素（240x135）で 逐次 700 µs → 並列 247 µs、57,600 画素（320x180）で 1,236 → 349 µs。
    /// 14,400 画素（160x90）では両者が同程度（304 / 248 µs）になり、5,184 画素（96x54）で元画像が 4K の場合は
    /// 並列が遅い（122 → 146 µs。 行がキャッシュに乗らない大きさのため）。
    /// </remarks>
    private const long ParallelTargetThresholdPixels = 32_768;

    /// <summary>最近傍の縮小を並列化するかどうかの境目（<b>縮小後</b>の画素数）。</summary>
    /// <remarks>
    /// <b>実測で決めた値</b>: 1 画素あたりの仕事が最も小さい（4 バイトのコピー 1 回）ため、並列化の損益分岐が双線形より
    /// 大きい。 57,600 画素（320x180）では逐次 134 µs に対し並列 194 µs と遅く、129,600 画素（480x270）で逐次 286 µs に対し
    /// 並列 253 µs、230,400 画素（640x360）で逐次 488 µs に対し並列 258 µs と逆転する。 その間の 65,536 画素を境にする。
    /// </remarks>
    private const long ParallelNearestThresholdPixels = 65_536;

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
    /// コピーされないパディングだけを透明にする（0 埋めは行わない）。<br/>
    /// <b>コピーは 1 回で済む場合を分ける</b>: 左右のパディングが無く、ソースとデスティネーションの行幅が等しいときは
    /// 連続領域として全領域を 1 回でコピーする（実測: 4K 全域の切り出しで 8.3 ms → 6.6 ms）。
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
        if (leftBytes == 0 && rightBytes == 0 && cropX == 0 && srcStride == dstStride)
        {
            // 左右のパディングが無く、コピー元とコピー先の行幅も同じ場合は連続領域になる（1 回でコピーする）
            // （全域の切り出しと全幅の切り出しがここに当たる。 実測: 4K 全域で 8.3 ms → 6.6 ms）
            src.Slice(srcStride * cropY, dstStride * cropH)
                .CopyTo(dst.Slice(dstStride * dstOffsetY, dstStride * cropH));
        }
        else
        {
            for (var row = 0; row < cropH; row++)
            {
                src.Slice(srcStride * (cropY + row) + (cropX * IEsBitmap.BytesPerPixel), copyBytes)
                    .CopyTo(dst.Slice(dstStride * (dstOffsetY + row) + leftBytes, copyBytes));
            }
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

    /// <summary>長辺を上限へ収めた画像を生成する（アルゴリズムは引数で選ぶ）。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 正の値で、長辺以下。</param>
    /// <param name="algorithm">縮小のアルゴリズム。 既定は <see cref="DownscaleAlgorithm.Bilinear"/>。</param>
    /// <returns>生成された画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSize"/> が 0 以下の場合、または
    /// <paramref name="algorithm"/> が定義されていない値の場合。</exception>
    /// <exception cref="NotSupportedException"><paramref name="maxSize"/> が長辺より大きい（＝拡大が必要な）場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    /// <remarks>
    /// <b>用途</b>: 大きな画像を小さく扱う場面（一覧のサムネイル・プレビュー等）。 元の寸法の複製を保持すると
    /// 4K 1 枚で 32 MB になるため、表示や転送の前に本メソッドで縮小する。<br/>
    /// <b>名前が <c>Resize</c> である理由</b>: 将来の拡大対応を見込んで双方向の名前を残している（現在の実装は縮小だけを
    /// 行う）。 拡大が必要な要求は黙って等倍を返さず <see cref="NotSupportedException"/> にする（「拡大された」と
    /// 誤解されるため）。<br/>
    /// <b>アルゴリズムの選び方</b>: 速度と画質の目安は <see cref="DownscaleAlgorithm"/> を参照（既定の双線形は
    /// 「速い・細線は薄れる」、<see cref="DownscaleAlgorithm.Box"/> は「遅い・細線が残る」）。<br/>
    /// <b>並列化</b>: どのアルゴリズムも<b>縮小先の行を範囲へ分けて並列実行する</b>（基準は
    /// <see cref="RunRowRanges"/> を参照。 小さい画像では逐次のままにする）。<br/>
    /// <b>返す寸法の丸め</b>: 切り捨てで丸めるため<b>1 px 未満の誤差が出うる</b>（座標の対応付けが必要な場面では使わない）。
    /// 長辺と上限が一致する場合は縮小不要のため<b>等倍の複製</b>を返す。
    /// </remarks>
    public static EsWritableBitmap Resize(
        this IEsBitmap source, int maxSize, DownscaleAlgorithm algorithm = DownscaleAlgorithm.Bilinear)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (maxSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "長辺の上限は正の値である必要があります。");
        }

        var width = source.Width;
        var height = source.Height;
        var longSide = Math.Max(width, height);
        if (maxSize > longSide)
        {
            // 拡大は未サポート（等倍を返すと「拡大された」と誤解されるため、黙って通さない）
            throw new NotSupportedException(
                $"拡大（{width}x{height} → 長辺 {maxSize}）は未サポートです。 長辺以下の上限を渡してください。");
        }

        if (maxSize == longSide)
        {
            // 縮小が不要（列挙されていない値でも寸法が変わらないため先に返す）
            return Crop(source, 0, 0, width, height);
        }

        // 長辺を上限へ収める倍率（縦横比を保つ。 短辺は最低 1 px）
        var ratio = (double)maxSize / longSide;
        var target = new EsSizeInt(Math.Max(1, (int)(width * ratio)), Math.Max(1, (int)(height * ratio)));

        return algorithm switch
        {
            DownscaleAlgorithm.Nearest => ScaleNearest(source, target),
            DownscaleAlgorithm.Bilinear => ScaleBilinear(source, target),
            DownscaleAlgorithm.Box => ScaleBox(source, target),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "定義されていない縮小のアルゴリズムです。"),
        };
    }

    /// <summary>最近傍補間で縮小する（<see cref="DownscaleAlgorithm.Nearest"/> の実装）。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="target">縮小後の寸法（元より小さい）。</param>
    /// <returns>縮小した画像。</returns>
    private static EsWritableBitmap ScaleNearest(IEsBitmap source, EsSizeInt target)
    {
        var result = EsWritableBitmap.CreateUninitialized(target.Width, target.Height);
        var srcWidth = source.Width;

        // 列の対応（最近傍）を先に求め、内側のループでは割り算をしない
        var colMap = new int[target.Width];
        for (var x = 0; x < target.Width; x++)
        {
            colMap[x] = Math.Min(srcWidth - 1, (int)((long)x * srcWidth / target.Width));
        }

        // 1 行あたりの仕事が小さいため、縮小後の画素数で並列化を判定する（最近傍は最も大きいしきい値を使う）
        if ((long)target.Width * target.Height >= ParallelNearestThresholdPixels)
        {
            RunRowRanges(target.Height, (from, to) => NearestRows(source, result, colMap, from, to));
        }
        else
        {
            NearestRows(source, result, colMap, 0, target.Height);
        }

        return result;
    }

    /// <summary>縮小先の行範囲を最近傍で求め、書き込む。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="result">書き込み先（<paramref name="yStart"/>〜<paramref name="yEnd"/> の行を書く）。</param>
    /// <param name="colMap">縮小先の列に対応する元の列（事前に求めた対応表）。</param>
    /// <param name="yStart">開始行（含む）。</param>
    /// <param name="yEnd">終了行（含まない）。</param>
    private static void NearestRows(
        IEsBitmap source, EsWritableBitmap result, int[] colMap, int yStart, int yEnd)
    {
        var srcHeight = source.Height;
        var src = source.Pixels;
        var srcStride = source.Stride;
        var dst = result.WritablePixels;
        var dstStride = result.Stride;
        var bytesPerPixel = IEsBitmap.BytesPerPixel;
        var targetHeight = result.Height;
        var targetWidth = result.Width;

        for (var y = yStart; y < yEnd; y++)
        {
            var srcRowStart = Math.Min(srcHeight - 1, (int)((long)y * srcHeight / targetHeight)) * srcStride;
            var srcRow = src[srcRowStart..];
            var dstRow = dst[(y * dstStride)..];
            for (var x = 0; x < targetWidth; x++)
            {
                srcRow.Slice(colMap[x] * bytesPerPixel, bytesPerPixel).CopyTo(dstRow.Slice(x * bytesPerPixel, bytesPerPixel));
            }
        }
    }
    /// <summary>双線形補間で縮小する（<see cref="DownscaleAlgorithm.Bilinear"/> の実装）。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="target">縮小後の寸法（元より小さい）。</param>
    /// <returns>縮小した画像。</returns>
    private static EsWritableBitmap ScaleBilinear(IEsBitmap source, EsSizeInt target)
    {
        var result = EsWritableBitmap.CreateUninitialized(target.Width, target.Height);

        // 1 行あたりの仕事が小さいため、縮小後の画素数で並列化を判定する
        if ((long)target.Width * target.Height >= ParallelTargetThresholdPixels)
        {
            RunRowRanges(target.Height, (from, to) => BilinearRows(source, result, from, to, target));
        }
        else
        {
            BilinearRows(source, result, 0, target.Height, target);
        }

        return result;
    }

    /// <summary>縮小先の行範囲を双線形補間で求め、書き込む。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="result">書き込み先（<paramref name="yStart"/>〜<paramref name="yEnd"/> の行を書く）。</param>
    /// <param name="yStart">開始行（含む）。</param>
    /// <param name="yEnd">終了行（含まない）。</param>
    /// <param name="target">縮小後の寸法。</param>
    /// <remarks>
    /// <b>行を範囲で受ける理由</b>: 1 行ずつメソッドを呼ぶと、行ごとに <see cref="IEsBitmap.Pixels"/> を取り直すうえ、
    /// メソッド境界を越えることで<b>境界チェックの除去とベクター化の最適化が失われる</b>（実測: 4K → 96x54 で 116 → 229 µs）。
    /// 範囲を受け取れば、逐次（1 回で全行）と並列（範囲ごと）が<b>同じ実装</b>を使いながら最適化を保てます。
    /// </remarks>
    private static void BilinearRows(IEsBitmap source, EsWritableBitmap result, int yStart, int yEnd, EsSizeInt target)
    {
        var srcWidth = source.Width;
        var srcHeight = source.Height;
        var src = source.Pixels;
        var srcStride = source.Stride;
        var dst = result.WritablePixels;
        var dstStride = result.Stride;
        var bytesPerPixel = IEsBitmap.BytesPerPixel;

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
                var sourceX = (((x + 0.5) * srcWidth) / target.Width) - 0.5;
                var x0 = Math.Clamp((int)Math.Floor(sourceX), 0, srcWidth - 1);
                var x1 = Math.Min(x0 + 1, srcWidth - 1);
                var fx = Math.Clamp(sourceX - x0, 0.0, 1.0);
                var left = x0 * bytesPerPixel;
                var right = x1 * bytesPerPixel;
                var offset = x * bytesPerPixel;

                for (var c = 0; c < bytesPerPixel; c++)
                {
                    var top = (src[row0 + left + c] * (1 - fx)) + (src[row0 + right + c] * fx);
                    var bottom = (src[row1 + left + c] * (1 - fx)) + (src[row1 + right + c] * fx);
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
    /// メソッド境界を越えることで<b>境界チェックの除去などの最適化が失われる</b>
    /// （実測: 4K → 96x54 の双線形で 116 µs → 229 µs）。 範囲を受け取れば、逐次（1 回で全行）と
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

    /// <summary>面積平均（box）で縮小する（<see cref="DownscaleAlgorithm.Box"/> の実装）。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="target">縮小後の寸法（元より小さい）。</param>
    /// <returns>縮小した画像。</returns>
    /// <remarks>
    /// <b>1 パスで面積平均を求める</b>: 縮小先の 1 画素ごとに、対応する元画像の矩形（全画素）を合計して割る。
    /// <b>中間画像を作らない</b>ため、横 → 縦 の 2 パスで生じる丸めの重なりが無く、速度も速い（実測: 4K → 96x54 で
    /// 2 パスの 18 ms に対し 1 パスで 1.6 ms）。<br/>
    /// <b>行ごとに独立しているため並列化する</b>（実測: 4K → 96x54 で 逐次 18 ms → 並列 1.6 ms。 4K → 320x180 で 27 ms → 1.8 ms）。
    /// 並列化するのは元画像が <see cref="ParallelSourceThresholdPixels"/> 以上のときだけである（小さい画像では
    /// スレッドの起動が相対的に重くなるため。 実測: 100x60 で逐次 57 µs / 並列 8.6 µs、400x300 で 948 → 56 µs と
    /// 小さい画像でも並列が速いが、絶対時間が小さい場面でスレッドプールを触らないようにする）。<br/>
    /// <b>SIMD は使わない</b>: 面積平均をベクター化すると<b>縦の合計を保持する作業行が必要になり、
    /// 縮小先の行ごとに全体（4K で 15 KB）を消すコストが加わる</b>。 実測でも遅くなる
    /// （4K → 96x54 で 逐次 2.8 ms 対 現行 2.1 ms、4K → 960x540 で 14 ms 対 2.4 ms）。
    /// 現行は元画像の各画素を 1 回だけ読んで合計するため、読み出しの回数は既に最小である。
    /// </remarks>
    private static EsWritableBitmap ScaleBox(IEsBitmap source, EsSizeInt target)
    {
        var result = EsWritableBitmap.CreateUninitialized(target.Width, target.Height);
        var srcWidth = source.Width;
        var srcHeight = source.Height;
        var targetWidth = target.Width;
        var targetHeight = target.Height;

        if ((long)srcWidth * srcHeight >= ParallelSourceThresholdPixels)
        {
            RunRowRanges(targetHeight, (from, to) => AverageBlockRows(
                source, result, from, to, srcWidth, srcHeight, targetWidth, targetHeight));
        }
        else
        {
            AverageBlockRows(source, result, 0, targetHeight, srcWidth, srcHeight, targetWidth, targetHeight);
        }

        return result;
    }

    /// <summary>縮小先の行範囲を面積平均で求める（行ごとに独立しているため並列実行できる）。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="result">書き込み先（<paramref name="yStart"/>〜<paramref name="yEnd"/> の行を書く）。</param>
    /// <param name="yStart">開始行（含む）。</param>
    /// <param name="yEnd">終了行（含まない）。</param>
    /// <param name="srcWidth">元の幅。</param>
    /// <param name="srcHeight">元の高さ。</param>
    /// <param name="targetWidth">縮小後の幅。</param>
    /// <param name="targetHeight">縮小後の高さ。</param>
    private static void AverageBlockRows(
        IEsBitmap source, EsWritableBitmap result, int yStart, int yEnd,
        int srcWidth, int srcHeight, int targetWidth, int targetHeight)
    {
        var src = source.Pixels;
        var srcStride = source.Stride;
        var dst = result.WritablePixels;
        var dstStride = result.Stride;
        var bytesPerPixel = IEsBitmap.BytesPerPixel;

        // 合計は画素数の最大（4K 全体で 830 万）に 255 を掛けても int に収まらないため long で持つ
        Span<long> sums = stackalloc long[bytesPerPixel];

        for (var y = yStart; y < yEnd; y++)
        {
            // 縮小先の 1 画素が受け持つ元の範囲（最低 1 画素）
            var y0 = (int)((long)y * srcHeight / targetHeight);
            var y1 = Math.Max(y0 + 1, (int)((long)(y + 1) * srcHeight / targetHeight));
            var dstRow = dst[(y * dstStride)..];

            for (var x = 0; x < targetWidth; x++)
            {
                var x0 = (int)((long)x * srcWidth / targetWidth);
                var x1 = Math.Max(x0 + 1, (int)((long)(x + 1) * srcWidth / targetWidth));

                sums.Clear();
                for (var sy = y0; sy < y1; sy++)
                {
                    var row = sy * srcStride;
                    for (var sx = x0; sx < x1; sx++)
                    {
                        var index = row + (sx * bytesPerPixel);
                        for (var c = 0; c < bytesPerPixel; c++)
                        {
                            sums[c] += src[index + c];
                        }
                    }
                }

                var count = (y1 - y0) * (x1 - x0);
                var offset = x * bytesPerPixel;
                for (var c = 0; c < bytesPerPixel; c++)
                {
                    dstRow[offset + c] = (byte)(sums[c] / count);
                }
            }
        }
    }
}
