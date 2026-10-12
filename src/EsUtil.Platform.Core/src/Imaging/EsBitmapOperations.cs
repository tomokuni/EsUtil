using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;

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
/// <b>減色の計算もここに置く</b>: 減色は「新しい画像を作る」操作として公開し（<see cref="ReduceColors"/>）、
/// 色番号とパレットを返す計算（<see cref="ReduceToIndexed"/>）は保存の実装（8bpp のインデックス カラー）と
/// <b>同じ共通部分</b>（<see cref="BuildPalette"/>）を使います
/// （2 箇所に書くと、同じ色数でも保存経路によって結果が変わります）。
/// </remarks>
internal static class EsBitmapOperations
{
    /// <summary>減色（パレット化）で残せる最小の色数。</summary>
    internal const int MinimumColorCount = 2;

    /// <summary>減色（パレット化）で残せる最大の色数（1 画素 1 バイトで表せる上限）。</summary>
    internal const int MaximumColorCount = 256;

    /// <summary>面積平均の縮小を並列化するかどうかの境目（元画像の画素数）。</summary>
    /// <remarks>
    /// <b>実測で決めた値</b>: 内側のループから画素ごとの 64 ビット除算と合計領域の消去を無くした後は、
    /// 元画像 65,536 画素（256x256 → 64x64）で逐次 0.089 ms に対し並列 0.107 ms と逐次が速く、
    /// 131,044 画素（362x362 → 90x90）で逐次 0.182 ms に対し並列 0.104 ms と逆転する。
    /// その間の 98,304 画素（313x313 相当）を境にする。 スクリーンショット（1080p で 200 万画素、4K で 830 万画素）は常に並列になる。
    /// </remarks>
    private const long ParallelSourceThresholdPixels = 98_304;

    /// <summary>双線形の縮小を並列化するかどうかの境目（<b>縮小後</b>の画素数）。</summary>
    /// <remarks>
    /// <b>実測で決めた値</b>: 列の対応を事前計算してからは、縮小後の 14,400 画素（160x90）で逐次 0.148 ms に対し
    /// 並列 0.167 ms と逐次が速く、20,736 画素（192x108）で逐次 0.223 ms に対し並列 0.146 ms と逆転する。
    /// その間の 20,480 画素（191x107 相当）を境にする（この値では 57,600 画素で逐次 0.610 ms に対し並列 0.211 ms）。
    /// </remarks>
    private const long ParallelTargetThresholdPixels = 20_480;

    /// <summary>最近傍の縮小を並列化するかどうかの境目（<b>縮小後</b>の画素数）。</summary>
    /// <remarks>
    /// <b>実測で決めた値</b>: 1 画素あたりの仕事が最も小さい（4 バイトのコピー 1 回）ため、並列化の損益分岐が
    /// 最も大きい。 57,600 画素（320x180）では逐次 0.062 ms に対し並列 0.065 ms とまだ並列がわずかに遅く、
    /// 82,944 画素（384x216）で逐次 0.089 ms に対し並列 0.082 ms と逆転する。 その間の 65,536 画素（341x192 相当）を境にする。
    /// </remarks>
    private const long ParallelNearestThresholdPixels = 65_536;

    /// <summary>ヒストグラムの 1 チャネルあたりのビット数（5 = 32 段階。 32³ = 32,768 の箱）。</summary>
    /// <remarks>
    /// <b>5 ビットにする理由（実測）</b>: 減色の重い部分は「色を箱へ数えること」ではなく「箱ごとにパレットの
    /// 最近傍を求めること」である。 画素ごとに最近傍を求めると 200 万画素 × 256 色の距離計算になるため、
    /// <b>箱ごとに 1 度だけ求めて参照表にする</b>（箱の数 × パレット長）。 箱を増やすほど色の分解能は上がるが、
    /// 8 ビット（16,777,216 箱）では表が 16 MB になり参照表の利点が消える。
    /// </remarks>
    private const int HistogramBits = 5;

    /// <summary>ヒストグラムのアルファのビット数（2 = 4 段階。 0〜63 / 64〜127 / 128〜191 / 192〜255）。</summary>
    /// <remarks>
    /// <b>アルファを箱に含める理由（実測）</b>: アルファを無視して減色すると<b>透明な領域が不透明になる</b>
    /// （実測: 完全透明 1,408 画素がすべて不透明になり、パレットのアルファは 255 になった）。
    /// アルファを箱の一部にすると、パレットの色がアルファを持ち、<b>GDI+ が PNG へ <c>tRNS</c>（インデックス透過）を
    /// 書き、GIF へ透明の色番号を書く</b>（実測: いずれも完全透明の 1,408 画素が保たれた）。<br/>
    /// <b>2 ビットにする理由</b>: 用途はスクリーンショットであり、アルファはほぼ「透明（切り出しのパディング）か
    /// 不透明か」の 2 値である。 段階を増やすと<b>同じ RGB の色がアルファ別にパレットを消費する</b>
    /// （4 段階で 256 色なら RGB に使えるのは実質 64 色）。 不透明な画像（アルファが 1 段階）では
    /// 箱の分け方が従来と同一になるため、結果は変わらない。
    /// </remarks>
    private const int HistogramAlphaBits = 2;

    /// <summary>ヒストグラムの箱の数（色 32³ × アルファ 4 段階）。</summary>
    private const int HistogramSize = 1 << ((HistogramBits * 3) + HistogramAlphaBits);

    /// <summary>アルファの広がりを RGB の広がりと比較するための倍率。</summary>
    /// <remarks>
    /// アルファは 4 段階（0〜3）であり、RGB は 32 段階（0〜31）である。 そのまま比較するとアルファの
    /// 広がりが最大 3 にしかならず、<b>RGB が同じでアルファだけが違う画像では分割が止まる</b>
    /// （実測: 同じ RGB で 3 段階のアルファを持つ画像が 1 色にまとまり、すべてアルファ 127 になった）。
    /// 4 段階 × 8 = 24 として RGB の最大 31 と同程度に扱う。
    /// </remarks>
    private const int HistogramAlphaScale = 8;

    /// <summary>距離でアルファの段階（0〜3）の違いへ与える重み（144 = 12²）。</summary>
    /// <remarks>
    /// <b>校正の仕方</b>: 画素のアルファは減色で 4 段階に量子化されるため、段階が違う色は RGB が近くても
    /// 選ばせません（選ぶと<b>透過が失われます</b>）。 1 段階の違いを <b>RGB の 12.5%（≒ 12 ΔE）</b>と
    /// 釣り合わせます（以前の式は 5 ビットの RGB 差 4 / 31 ≈ 12.5% に相当した。 その関係を Lab の尺度へ移した）。
    /// 12 ΔE は「はっきり分かる色差」であり、<b>同じ RGB で段階だけが違う色は必ず負けます</b>。
    /// </remarks>
    private const int AlphaTierDeltaE = 12;

    /// <summary>Lab を固定小数点で持つときの倍率（1 / 64 ΔE まで表せる）。</summary>
    /// <remarks>
    /// <b>整数で持つ理由（実測）</b>: <c>double</c> の Lab で距離を測ると、既定の品質（<see cref="EsImageQuality.Mid"/>）の
    /// 減色が 2 倍遅くなりました（1024x768 相当の写真風・256 色で 12.5 ms → 25.9 ms。 詰め直しが
    /// 「箱の数 × 色数 × 反復回数」回だけ距離を呼ぶため、1 回の費用がそのまま効く）。 1 / 64 ΔE は
    /// <b>人が区別できない</b>精度であり、整数の二乗距離にすると費用が RGB の二乗距離と同程度へ戻ります。
    /// </remarks>
    private const int LabScale = 64;

    /// <summary>白色点 D65 の X（XYZ の正規化に使う）。</summary>
    private const double WhiteX = 0.95047;

    /// <summary>白色点 D65 の Z（XYZ の正規化に使う）。</summary>
    private const double WhiteZ = 1.08883;

    /// <summary>中央値分割の結果を詰め直す反復の上限（<see cref="RefinePalette"/>）。</summary>
    /// <remarks>
    /// <b>実測で決めた値</b>: 1024x768 のグラデーション・写真風・スクリーンショット風で、
    /// 色数 16 / 64 / 256 のいずれも色の変化は数回で止まる（終了は目的関数が改善しなくなった時点で行うため、
    /// この値は上限としてのみ働く）。
    /// </remarks>
    private const int MaximumRefinementIterations = 8;

    /// <summary>詰め直しの反復に使ってよい距離計算の回数（1 回の減色あたり）。</summary>
    /// <remarks>
    /// <b>上限を設ける理由（実測）</b>: 1 回の反復の費用は「使われている箱の数 × 色数」に比例する。
    /// 箱が 32,768 個あるノイズの画像（1920x1080）・256 色では 1 回の反復が約 10 ms であり、
    /// 上限が無ければ減色全体が 137 ms になる（<see cref="EsImageQuality.High"/> の実測）一方、画質の改善は小さい。
    /// 写真風（箱 2,873 個）は 1 回が約 2 ms である。
    /// そこで<b>効果の小さい画像だけ回数を減らす</b>（この値では写真風は 8 回、ノイズは 1 回になる）。<br/>
    /// <b>値の根拠</b>: 距離を Lab（<see cref="Distance"/>）へ替えて 1 回あたりの費用が約 2.9 倍になったため、
    /// <b>従来と同じ所要に収まる</b>ように上限を 1/3 にしました（実測: 1920x1080 の写真風・256 色の
    /// <see cref="EsImageQuality.Mid"/> で 12.5 ms（RGB 距離・上限 8,000,000）に対し 24 ms（Lab 距離・上限
    /// 8,000,000）→ 上限 2,800,000 で約 15 ms）。 反復が減っても<b>Lab 化の改善（7〜14%）の方が大きい</b>ため、
    /// 画質は従来より良くなります。
    /// </remarks>
    private const long RefinementWorkBudget = 2_800_000;

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
    /// 規則と実測値は公開面が単一所有する（<see cref="EsReadOnlyBitmap.Crop(int, int, int, int)"/> の備考）。<br/>
    /// <b>行の余白を混ぜません</b>: 読み書きとも<b>行単位</b>であり（連続コピーへ寄せる条件にも行幅の一致を含む）、
    /// 余白付きのソース（<see cref="EsPixmap.ReadOnlyPixels"/>）を渡しても余白の画素は結果へ入りません。<br/>
    /// デスティネーションのコピーで覆われない領域は透明にするため、ソース範囲外は透明パディングとなる。<br/>
    /// 左上がソースの外（負）の場合も、要求矩形とソースの位置関係を保ってコピーする
    /// （内容を左上へ詰めない。 画像外の領域が透明のまま残る）。<br/>
    /// 矩形がソースと交差しない場合は全ピクセル透明の画像を返す。<br/>
    /// コピーされないパディングだけを透明にする（0 埋めは行わない）。<br/>
    /// <b>コピーは 1 回で済む場合を分ける</b>: 左右のパディングが無く、ソースとデスティネーションの行幅が等しいときは
    /// 連続領域として全領域を 1 回でコピーする（実測: 4K 全域の切り出しで 8.3 ms → 6.6 ms）。
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
            // （全域の切り出しと全幅の切り出しがここに当たる。 実測: 4K 全域で 8.3 ms → 6.6 ms）
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
    /// 規則と実測値は公開面が単一所有する（<see cref="EsReadOnlyBitmap.Resize(int, EsImageQuality)"/> の備考）。<br/>
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
    /// 「速度と画質のどちらを取るか」であり、フィルターの実装名は差し替えの自由を奪う（本実装は以前
    /// <c>Nearest</c> / <c>Bilinear</c> / <c>Box</c> の列挙体を公開していた）。<br/>
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
    /// <param name="pixels">対象の画素（BGRA8888。 <b>行の余白を含んでよい</b>）。 借用であり破棄しない。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数（<c>sourceWidth × 4</c> 以上）。</param>
    /// <param name="colorCount">残す色数（2〜256）。</param>
    /// <param name="quality">減色の品質。 既定は <see cref="EsImageQuality.Mid"/>。</param>
    /// <param name="dither">ディザの指定。 既定は <see cref="EsDitherMode.None"/>。</param>
    /// <returns>減色した新しい画像（使われる色は <paramref name="colorCount"/> 種類以下）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="colorCount"/> が 2〜256 の範囲外の場合、
    /// または <paramref name="quality"/> / <paramref name="dither"/> が定義されていない値の場合。</exception>
    /// <remarks>
    /// 規則と実測値は公開面が単一所有する（<see cref="EsReadOnlyBitmap.ReduceColors(int, EsImageQuality, EsDitherMode)"/> の備考）。<br/>
    /// <b>行の余白を色として数えません</b>: ヒストグラム（<see cref="CountColors"/>）は<b>行単位</b>に数えるため、
    /// 余白付きのソース（<see cref="EsPixmap.ReadOnlyPixels"/>）を渡しても結果は変わりません。<br/>
    /// <b>不可逆である</b>: 元の画素は戻らない（減色後の色数は <paramref name="colorCount"/> 以下になる）。<br/>
    /// <b>透過は保たれる（実測）</b>: アルファも減色の対象であり、透明な画素は透明なパレットの色へ寄る。
    /// 保存した PNG には <c>tRNS</c>（インデックス透過）、GIF には透明の色番号が書かれる。<br/>
    /// <b>アルゴリズム（実測で選んだ）</b>: 5 ビットのヒストグラム（32³ = 32,768 箱）を作り、
    /// <b>中央値分割</b>で箱を色数まで分け、箱ごとの平均色をパレットにする。 画素の割り当ては
    /// <b>箱ごとに 1 度だけ最近傍を求めた参照表</b>を引く（画素ごとに 256 色と比較しない）。
    /// 品質は<b>この段階の詰め直し（<see cref="RefinePalette"/>）の有無と回数</b>で変える
    /// （ディザを指定した場合は参照表を遅延で埋める。 誤差を加えた色は使用済みの箱へ落ちないため）。<br/>
    /// <b>色数はアルファの段階も数える</b>: アルファは 4 段階（0〜63 / 64〜127 / 128〜191 / 192〜255）に
    /// 量子化されるため、同じ RGB でもアルファが違えば別の色としてパレットを消費する
    /// （不透明な画像ではアルファが 1 段階のため、従来と同じ結果になる）。 <b>アルファの段階より色数が
    /// 少ないときは段階が統合される</b>（<see cref="EsReadOnlyBitmap.ReduceColors"/> の備考を参照）。
    /// </remarks>
    internal static EsBitmap ReduceColors(
        ReadOnlySpan<byte> pixels, int sourceWidth, int sourceHeight, int stride,
        int colorCount, EsImageQuality quality = EsImageQuality.Mid, EsDitherMode dither = EsDitherMode.None)
    {
        if (colorCount < MinimumColorCount || colorCount > MaximumColorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(colorCount), colorCount, $"残す色数は {MinimumColorCount}〜{MaximumColorCount} の範囲で指定します。");
        }

        if (quality is not (EsImageQuality.Low or EsImageQuality.Mid or EsImageQuality.High))
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "定義されていない減色の品質です。");
        }

        if (dither is not (EsDitherMode.None or EsDitherMode.FloydSteinberg))
        {
            throw new ArgumentOutOfRangeException(nameof(dither), dither, "定義されていないディザの指定です。");
        }

        var width = sourceWidth;
        var height = sourceHeight;

        // 参照表（箱 → 色番号）は常に作る（ディザの場合も、誤差を加えない素の色の割り当てには使わないが、
        // 費用は「使われている箱の数 × 色数」であり小さい。 ディザは遅延で埋める別の表を使う）
        var palette = BuildPalette(pixels, width, height, stride, colorCount, quality, out var lookup);

        var result = EsBitmap.CreateUninitialized(width, height);
        var destination = result.Pixels;

        if (dither != EsDitherMode.None)
        {
            var indices = new byte[width * height];
            AssignDither(pixels, width, height, stride, palette, indices);
            for (var y = 0; y < height; y++)
            {
                var destinationRow = destination.Slice(y * result.Stride, width * EsReadOnlyBitmap.BytesPerPixel);
                var rowStart = y * width;
                for (var x = 0; x < width; x++)
                {
                    WriteColor(destinationRow, x * EsReadOnlyBitmap.BytesPerPixel, palette[indices[rowStart + x]]);
                }
            }

            return result;
        }

        // 色番号の並び（1 画素 1 バイト）を作らず、変換先へ直接書く（実測: 1920x1080 で中間の 2 MB の確保と
        // もう 1 回の全画素走査が消える。 確保は 4.9 MB → 0.14 MB）
        for (var y = 0; y < height; y++)
        {
            // 行単位で読む（余白の画素を読まない）
            var row = pixels.Slice(y * stride, width * EsReadOnlyBitmap.BytesPerPixel);
            var destinationRow = destination.Slice(y * result.Stride, width * EsReadOnlyBitmap.BytesPerPixel);
            for (var x = 0; x < width; x++)
            {
                var offset = x * EsReadOnlyBitmap.BytesPerPixel;
                WriteColor(
                    destinationRow,
                    offset,
                    palette[lookup[HistogramKey(row[offset + 2], row[offset + 1], row[offset], row[offset + 3])]]);
            }
        }

        return result;
    }

    /// <summary>1 画素ぶんの色（ARGB）を行の書き込み先へ書く（並びは BGRA）。</summary>
    /// <param name="row">書き込み先の行。</param>
    /// <param name="offset">行の先頭からのバイト位置。</param>
    /// <param name="color">書く色（ARGB）。</param>
    private static void WriteColor(Span<byte> row, int offset, uint color)
    {
        row[offset] = (byte)color;
        row[offset + 1] = (byte)(color >> 8);
        row[offset + 2] = (byte)(color >> 16);
        row[offset + 3] = (byte)(color >> 24);
    }

    /// <summary>画素を指定の色数へ減色し、色番号とパレットで返す（保存の実装と共有する）。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="colorCount">残す色数（2〜256。 呼び出し側が検証済みであること）。</param>
    /// <param name="quality">減色の品質。 呼び出し側が検証済みであること。</param>
    /// <param name="dither">ディザの指定。 呼び出し側が検証済みであること。</param>
    /// <returns>色番号（1 画素 1 バイト）とパレット（ARGB）。</returns>
    /// <remarks>
    /// <b>色番号で返す理由</b>: 8bpp のインデックス カラーとしてファイルへ書く経路（<c>GdipSetImagePalette</c>）が
    /// 必要とする形であり、画素（BGRA）へ戻すのは <see cref="ReduceColors"/> が行う。<br/>
    /// <b>行の余白は数えない・読まない</b>: 数える側（<see cref="CountColors"/>）も割り当てる側も<b>行単位</b>です
    /// （余白付きのソースを渡しても結果が変わりません）。<br/>
    /// <b>アルファも減色の対象である（実測）</b>: アルファを 4 段階に量子化して色と一緒に分けるため、
    /// パレットの色はアルファを持ち、<b>GDI+ が PNG へ <c>tRNS</c>（インデックス透過）を書き、GIF へ透明の
    /// 色番号を書く</b>（実測: 完全透明の 1,408 画素がどちらも保たれた）。 アルファを無視すると
    /// <b>透明な領域が不透明になる</b>（実測: パレットのアルファが 255 になった）。<br/>
    /// <b>RGB は保持している値（アルファ乗算済み）を平均する</b>: 半透明の画素は乗算済みの値がそのまま
    /// パレットへ入る（PNG の <c>tRNS</c> は乗算前の色として解釈されるため、半透明の色はやや暗くなる。
    /// 透明（アルファ 0）と不透明（アルファ 255）は完全に一致する）。
    /// </remarks>
    internal static (byte[] Indices, uint[] Palette) ReduceToIndexed(
        ReadOnlySpan<byte> pixels, int width, int height, int stride, int colorCount,
        EsImageQuality quality = EsImageQuality.Mid, EsDitherMode dither = EsDitherMode.None)
    {
        var palette = BuildPalette(pixels, width, height, stride, colorCount, quality, out var lookup);

        var indices = new byte[width * height];
        if (dither != EsDitherMode.None)
        {
            AssignDither(pixels, width, height, stride, palette, indices);
            return (indices, palette);
        }

        for (var y = 0; y < height; y++)
        {
            // 行単位で読む（余白の画素を読まない）
            var row = pixels.Slice(y * stride, width * EsReadOnlyBitmap.BytesPerPixel);
            var rowStart = y * width;
            for (var x = 0; x < width; x++)
            {
                var offset = x * EsReadOnlyBitmap.BytesPerPixel;
                indices[rowStart + x] = lookup[HistogramKey(
                    row[offset + 2], row[offset + 1], row[offset], row[offset + 3])];
            }
        }

        return (indices, palette);
    }

    /// <summary>画素をパレットの色番号へ割り当てる（Floyd–Steinberg の誤差拡散を行う）。</summary>
    /// <param name="pixels">元の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="palette">パレット（ARGB）。</param>
    /// <param name="indices">割り当てた色番号の書き先（<c>幅 × 高さ</c> 要素）。</param>
    /// <remarks>
    /// <b>誤差の配り方</b>: 画素を置き換えた差（赤・緑・青）を、右（7/16）・左下（3/16）・下（5/16）・
    /// 右下（1/16）の 4 画素へ配ります（Floyd–Steinberg 法）。 誤差は<b>行ごとの 2 本のバッファ</b>で持ちます
    /// （現在の行と次の行。 画像全体の分は持ちません）。<br/>
    /// <b>アルファへ誤差を配らない理由</b>: アルファは減色で 4 段階に量子化されており、にじませると
    /// 透明と不透明の境目が崩れます（<see cref="EsDitherMode"/> の備考）。<br/>
    /// <b>近傍の探索に参照表を使えない理由</b>: 誤差を加えた色は<b>画素が無かった色</b>になりうるため、
    /// 使われている箱だけを埋めた表（<see cref="BuildPalette"/>）では引けません。 そこで<b>全域の表を要求された
    /// 箱から遅延で埋めます</b>（箱は 131,072 個あり、埋める費用は「実際に現れた色の数 × 色数」。
    /// 平坦なスクリーンショットでは数十個しか現れません）。<br/>
    /// <b>並列化しない理由</b>: 誤差がとなりへ伝わるため、画素の走査に順序が必要です。
    /// </remarks>
    private static void AssignDither(
        ReadOnlySpan<byte> pixels, int width, int height, int stride, uint[] palette, byte[] indices)
    {
        // 0xFF は「まだ求めていない」を表す（パレットの色番号は 255 以下）
        var table = new byte[HistogramSize];
        table.AsSpan().Fill(byte.MaxValue);

        // パレットの Lab は 1 度だけ求める（誤差を加えた色は画素ごとに違いうるため、色ごとに 1 度だけ引く）
        var (paletteLab, paletteAlphaTiers) = PreparePalette(palette);

        // 誤差は行ごとに持ち回る（現在の行と次の行。 成分ごとに 1 つ）
        var current = new int[width * 3];
        var next = new int[width * 3];

        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, width * EsReadOnlyBitmap.BytesPerPixel);
            var rowStart = y * width;
            Array.Clear(next);

            for (var x = 0; x < width; x++)
            {
                var offset = x * EsReadOnlyBitmap.BytesPerPixel;
                var blue = Clamp(row[offset] + current[(x * 3) + 2]);
                var green = Clamp(row[offset + 1] + current[(x * 3) + 1]);
                var red = Clamp(row[offset + 2] + current[(x * 3) + 0]);
                var alpha = row[offset + 3];

                var key = HistogramKey(red, green, blue, alpha);
                var index = table[key];
                if (index == byte.MaxValue)
                {
                    index = table[key] = (byte)NearestColor(
                        paletteLab, paletteAlphaTiers, ToLab(red, green, blue), AlphaTier(alpha));
                }

                indices[rowStart + x] = index;

                // 置き換えた差をとなりへ配る（重みは Floyd–Steinberg 法。 右 7/16・左下 3/16・下 5/16・右下 1/16）
                var color = palette[index];
                var errorBlue = blue - (int)(color & 0xFF);
                var errorGreen = green - (int)((color >> 8) & 0xFF);
                var errorRed = red - (int)((color >> 16) & 0xFF);

                if (x + 1 < width)
                {
                    // 右（同じ行）
                    SpreadError(current, x + 1, errorRed, errorGreen, errorBlue, 7);

                    // 右下（次の行）
                    SpreadError(next, x + 1, errorRed, errorGreen, errorBlue, 1);
                }

                // 下（次の行）
                SpreadError(next, x, errorRed, errorGreen, errorBlue, 5);

                if (x > 0)
                {
                    // 左下（次の行）
                    SpreadError(next, x - 1, errorRed, errorGreen, errorBlue, 3);
                }
            }

            (current, next) = (next, current);
        }
    }

    /// <summary>誤差を次の行の画素へ配る（Floyd–Steinberg 法の重み）。</summary>
    /// <param name="row">次の行の誤差。</param>
    /// <param name="x">配る先の列。</param>
    /// <param name="red">赤の誤差。</param>
    /// <param name="green">緑の誤差。</param>
    /// <param name="blue">青の誤差。</param>
    /// <param name="weight">重み（7 / 3 / 1 のいずれか。 16 で割る）。</param>
    private static void SpreadError(int[] row, int x, int red, int green, int blue, int weight)
    {
        row[(x * 3) + 0] += (red * weight) >> 4;
        row[(x * 3) + 1] += (green * weight) >> 4;
        row[(x * 3) + 2] += (blue * weight) >> 4;
    }

    /// <summary>0〜255 へ丸める（誤差を加えた色）。</summary>
    /// <param name="value">丸める値。</param>
    /// <returns>0〜255 の値。</returns>
    private static byte Clamp(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);

    /// <summary>画素を数えてパレットと「箱 → 色番号」の参照表を作る（減色の共通部分）。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="colorCount">残す色数（2〜256。 呼び出し側が検証済みであること）。</param>
    /// <param name="quality">減色の品質（詰め直しの回数を決める。 呼び出し側が検証済みであること）。</param>
    /// <param name="lookup">箱（ヒストグラムのキー）→ 色番号の参照表の書き先。</param>
    /// <returns>パレット（ARGB。 使われる色数以下）。</returns>
    /// <remarks>
    /// <b>2 つの入口で共有する理由</b>: 画像を返す経路（<see cref="ReduceColors"/>）と色番号を返す経路
    /// （<see cref="ReduceToIndexed"/>）は<b>同じ計算でなければならない</b>（同じ色数を指定すれば同じ絵になる）。
    /// 数え上げ・中央値分割・詰め直し（結果を決める部分）をここへ 1 回だけ書き、走査の仕方だけを各経路が持ちます。<br/>
    /// <b>順序</b>: 数え上げ（<see cref="CountColors"/>）→ 中央値分割（<see cref="SplitBoxes"/>）→
    /// 詰め直し（<see cref="RefinePalette"/>。 品質で回数が変わる）→ 箱 → 色番号の参照表（<see cref="NearestColor"/>）。<br/>
    /// <b>一時配列はプールから借りる（実測）</b>: 数え上げと作業場所の配列は合わせて 5.0 MB あり（<c>int</c>
    /// 131,072 要素 ×2 と <c>long</c> 524,288 要素）、毎回確保すると LOH に入って Gen2 の GC を誘発します。
    /// 借りて返すため、毎回の確保は参照表（131,072 バイト）と詰め直しの作業場所（色数ぶんと箱の数ぶんの
    /// 小さな配列）だけになります
    /// （実測: 1920x1080 のノイズ画像・256 色で、確保は 2.40 MB → 0.31 MB）。<br/>
    /// <b>参照表は使われている箱だけ埋める（実測）</b>: 画素が 1 つも無い箱は走査で読まれないため、最近傍を
    /// 求める必要がありません。 全部の箱（131,072）を埋めると費用が<b>箱の数 × 色数</b>に比例して増えます
    /// （実測: 1920x1080・256 色で、箱が 64 個の画像は 29.8 ms → 13.0 ms、箱が 32,768 個のノイズ画像は
    /// 88.1 ms → 42.2 ms。 いずれも分割の改善を含まない形で測った値です）。<br/>
    /// <b>割り当ては箱の代表色（平均色）で測る</b>: 箱の中心（量子化した座標）で測るより、その箱に入っている
    /// 画素の平均色で測る方が実際の誤差に近い（<see cref="Distance"/> を参照。 費用は同じ）。<br/>
    /// <b>画素そのものから最近傍を求めない理由（実測）</b>: 参照表を引く形にせず画素ごとに最近傍を求めると
    /// 知覚誤差は 8〜11% 減りますが、<b>費用が「画素の数 × 色数」で決まります</b>（実測: 距離の評価は 1 回
    /// あたり 1.5 ns。 1920x1080 の全画素・256 色で<b>約 775 ms</b>、16 色でも約 48 ms で、現在の
    /// <see cref="EsImageQuality.High"/> の 24 ms の 32 倍）。 詳細は
    /// <see cref="EsImageQuality"/> の備考を参照してください。
    /// </remarks>
    private static uint[] BuildPalette(
        ReadOnlySpan<byte> pixels, int width, int height, int stride, int colorCount,
        EsImageQuality quality, out byte[] lookup)
    {
        var counts = ArrayPool<int>.Shared.Rent(HistogramSize);
        var sums = ArrayPool<long>.Shared.Rent(HistogramSize * 4);
        var bins = ArrayPool<int>.Shared.Rent(HistogramSize);
        try
        {
            // 借りた配列の内容は不定である（ゼロから数え上げるため必ず消す）
            Array.Clear(counts, 0, HistogramSize);
            Array.Clear(sums, 0, HistogramSize * 4);
            CountColors(pixels, width, height, stride, counts, sums);

            var usedCount = CollectUsedBins(counts, bins);
            var palette = SplitBoxes(bins, usedCount, counts, sums, colorCount);

            // 箱の代表色（平均色）の Lab は 1 度だけ求める（詰め直しと参照表の両方が同じ値を要する）
            var binLab = new LabColor[usedCount];
            var binAlphaTier = new byte[usedCount];
            for (var i = 0; i < usedCount; i++)
            {
                var centroid = BinCentroid(bins[i], counts, sums);
                binLab[i] = ToLab(centroid.Red, centroid.Green, centroid.Blue);
                binAlphaTier[i] = (byte)AlphaTier(centroid.Alpha);
            }

            RefinePalette(bins, usedCount, counts, sums, palette, quality, binLab, binAlphaTier);

            // 箱 → 色番号の参照表（画素ごとに最近傍を求めない）。 埋めない箱は 0 のままである
            var (paletteLab, paletteAlphaTiers) = PreparePalette(palette);
            lookup = new byte[HistogramSize];
            for (var i = 0; i < usedCount; i++)
            {
                lookup[bins[i]] = (byte)NearestColor(
                    paletteLab, paletteAlphaTiers, binLab[i], binAlphaTier[i]);
            }

            return palette;
        }
        finally
        {
            // 内容は次の借り手が消す（返すときに消すと、その費用を毎回払う）
            ArrayPool<int>.Shared.Return(bins);
            ArrayPool<int>.Shared.Return(counts);
            ArrayPool<long>.Shared.Return(sums);
        }
    }

    /// <summary>箱に入っている画素の平均色（代表色）を求める。</summary>
    /// <param name="key">箱。</param>
    /// <param name="counts">箱ごとの画素数。</param>
    /// <param name="sums">箱ごとの成分の合計（箱 × 4。 RGB は 8 ビットの値、最後の 1 つがアルファ）。</param>
    /// <returns>平均色（赤・緑・青・アルファ。 いずれも 0〜255）。</returns>
    /// <remarks>
    /// <b>箱の中心ではなく平均を使う理由</b>: 箱は 32 段階に量子化した立方体であり、その中心は実際の画素の
    /// 位置を表しません（画素が箱の片隅に偏れば最大 8 のずれになる）。 平均は画素の実際の位置を表すため、
    /// <b>どのパレットの色を選ぶかが正確になる</b>（費用は同じ。 画素数で割るだけ）。<br/>
    /// <b>sRGB の値のまま平均する理由</b>: 線形（相対輝度の尺度）で平均する案は実測でわずかに悪化します
    /// （<see cref="CountColors"/> の備考）。
    /// </remarks>
    private static (int Red, int Green, int Blue, int Alpha) BinCentroid(int key, int[] counts, long[] sums)
    {
        var total = counts[key];
        return (
            (int)(sums[(key * 4) + 0] / total),
            (int)(sums[(key * 4) + 1] / total),
            (int)(sums[(key * 4) + 2] / total),
            (int)(sums[(key * 4) + 3] / total));
    }

    /// <summary>画素をヒストグラムへ数える（箱ごとの画素数と成分の合計）。</summary>
    /// <param name="pixels">元の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="counts">箱ごとの画素数の書き先。</param>
    /// <param name="sums">箱ごとの成分の合計の書き先（箱 × 4。 最後の 1 つがアルファ）。</param>
    /// <remarks>
    /// <b>行単位で数える</b>: 余白の画素を数えないため（平坦に走査すると余白が色として混ざる）。<br/>
    /// <b>RGB は sRGB の値のまま足す（実測で決めた）</b>: 平均を線形（相対輝度の尺度）で取る案は、
    /// <b>わずかに悪化します</b>（実測: 1024x768 のなだらかな画像 16 色で 6.81 → 6.99、写真風 16 色で 8.31 → 8.36。
    /// 知覚誤差 = CIEDE2000 の平均）。 距離を Lab で測る場合、パレットの色は「その箱の画素を代表する色」で
    /// あればよく、線形の平均は<b>かえって暗部へ寄る</b>ためです（距離の尺度は Lab が担います）。<br/>
    /// <b>アルファは 8 ビットのまま足す</b>: アルファは 4 段階に量子化する対象であり、平均も 8 ビットの尺度で行います。
    /// </remarks>
    private static void CountColors(
        ReadOnlySpan<byte> pixels, int width, int height, int stride, int[] counts, long[] sums)
    {
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, width * EsReadOnlyBitmap.BytesPerPixel);
            for (var x = 0; x < width; x++)
            {
                var i = x * EsReadOnlyBitmap.BytesPerPixel;
                var key = HistogramKey(row[i + 2], row[i + 1], row[i], row[i + 3]);
                counts[key]++;
                sums[(key * 4) + 0] += row[i + 2];
                sums[(key * 4) + 1] += row[i + 1];
                sums[(key * 4) + 2] += row[i];
                sums[(key * 4) + 3] += row[i + 3];
            }
        }
    }

    /// <summary>使われている箱（画素が 1 つ以上ある箱）の番号を小さい順に配列へ集める。</summary>
    /// <param name="counts">箱ごとの画素数。</param>
    /// <param name="bins">箱の番号の書き先（<see cref="HistogramSize"/> 要素以上）。</param>
    /// <returns>書き込んだ箱の数。</returns>
    /// <remarks>
    /// <b>小さい順に集める理由</b>: 減色の結果はこの順序に依存する（同じ画像・同じ色数なら同じ絵にするため、
    /// 走査の順序を固定する）。 集めた配列は以降<b>分割の作業場所</b>として並べ替えられる。
    /// </remarks>
    private static int CollectUsedBins(int[] counts, int[] bins)
    {
        var used = 0;
        for (var key = 0; key < HistogramSize; key++)
        {
            if (counts[key] != 0)
            {
                bins[used++] = key;
            }
        }

        return used;
    }

    /// <summary>使われている箱を中央値分割で指定の個数まで分け、箱ごとの平均色（パレット）を返す。</summary>
    /// <param name="bins">箱の番号の並び（<b>この配列を並べ替えながら分割する</b>）。</param>
    /// <param name="binCount"><paramref name="bins"/> に有効な箱の数。</param>
    /// <param name="counts">箱ごとの画素数。</param>
    /// <param name="sums">箱ごとの成分の合計（箱 × 4。 最後の 1 つがアルファ）。</param>
    /// <param name="colorCount">分ける個数（色数）。</param>
    /// <returns>パレット（ARGB。 箱の数だけ。 使われている色が指定より少ない場合は短い）。</returns>
    /// <remarks>
    /// <b>分割する箱の選び方</b>: 「成分の範囲の長さ × 画素数」が最も大きい箱を選ぶ（範囲が広く画素が多い箱ほど、
    /// 分けたときの誤差が減る）。<b>切る位置</b>は画素数が半分になる位置である（中央値分割。 色の分布が偏っていても
    /// 各箱の画素数が揃う）。<br/>
    /// <b>箱を「区間」で表す理由</b>: 以前は箱ごとに <c>List&lt;int&gt;</c> を持ち、分割のたびに 2 つの
    /// <c>List</c> を作って全要素を写していた（<c>GetRange</c>。 256 色で最大 510 個の <c>List</c> ができる）。
    /// 今は 1 本の配列の区間で表す。 並べ替えた範囲の前半と後半は<b>そのまま隣り合う</b>ため、
    /// <b>要素を写さずに 2 つの箱へ分けられる</b>。<br/>
    /// <b>指標を持ち回る理由（実測）</b>: 以前は分ける箱を選ぶたびに<b>すべての箱</b>の「成分の範囲」と画素数を
    /// 数え直していた（1 回あたりの費用が<b>使われている箱の数</b>に比例する）。 今は分割した箱の 2 つ分だけを
    /// 数え直す（実測: 1920x1080 のノイズ画像（箱 32,768 個）で 256 色が 105.2 ms → 88.1 ms。 参照表の改善を
    /// 合わせると 42.2 ms）。
    /// </remarks>
    private static uint[] SplitBoxes(int[] bins, int binCount, int[] counts, long[] sums, int colorCount)
    {
        // 箱は「bins の開始位置 + 長さ」で表す（最大 colorCount 個。 colorCount は 256 以下なので小さい）
        var starts = new int[colorCount];
        var lengths = new int[colorCount];
        var pixels = new long[colorCount];
        var axisLengths = new int[colorCount];

        var boxCount = 1;
        lengths[0] = binCount;
        pixels[0] = CountPixels(bins, 0, binCount, counts);
        axisLengths[0] = LongestAxisLength(bins, 0, binCount);

        while (boxCount < colorCount)
        {
            var target = FindWidestBox(pixels, axisLengths, lengths, boxCount);
            if (target < 0)
            {
                // どの箱も分けられない（使われている色が指定より少ない）
                break;
            }

            var start = starts[target];
            var length = lengths[target];
            SortByAxis(bins, start, length, LongestAxis(bins, start, length));

            var half = pixels[target] / 2;
            var running = 0L;
            var cut = 1;
            for (var i = 0; i < length - 1; i++)
            {
                running += counts[bins[start + i]];
                if (running >= half)
                {
                    cut = i + 1;
                    break;
                }
            }

            // 前半と後半は並べ替えた範囲の中で隣り合っている（要素を写す必要が無い）
            var tail = start + cut;
            var tailLength = length - cut;

            starts[target] = start;
            lengths[target] = cut;
            pixels[target] = CountPixels(bins, start, cut, counts);
            axisLengths[target] = LongestAxisLength(bins, start, cut);

            starts[boxCount] = tail;
            lengths[boxCount] = tailLength;
            pixels[boxCount] = CountPixels(bins, tail, tailLength, counts);
            axisLengths[boxCount] = LongestAxisLength(bins, tail, tailLength);
            boxCount++;
        }

        var palette = new uint[boxCount];
        for (var i = 0; i < boxCount; i++)
        {
            palette[i] = AverageColor(bins, starts[i], lengths[i], counts, sums);
        }

        return palette;
    }

    /// <summary>中央値分割で作ったパレットを k-means（Lloyd）の反復で詰め直す。</summary>
    /// <param name="bins">使われている箱の番号の並び。</param>
    /// <param name="binCount">使われている箱の数。</param>
    /// <param name="counts">箱ごとの画素数。</param>
    /// <param name="sums">箱ごとの成分の合計（箱 × 4）。</param>
    /// <param name="palette"><b>その場で</b>詰め直すパレット（改善しなければ変更しない）。</param>
    /// <param name="quality">減色の品質（反復の上限を決める）。</param>
    /// <param name="binLab">箱の代表色の Lab（<see cref="BuildPalette"/> が 1 度だけ求めた値）。</param>
    /// <param name="binAlphaTier">箱の代表色のアルファの段階（0〜3）。</param>
    /// <remarks>
    /// <b>詰め直す理由（実測）</b>: 中央値分割は「その場で最も広い箱を切る」だけであり、切った後の色を
    /// 動かしません（反復が無いため、分けた境界に対して色が最適になりません）。 「箱を最も近い色へ割り当て直し、
    /// 割り当てられた箱の画素の平均を新しい色にする」操作を繰り返すと、色差（1 チャネル平均絶対誤差）が
    /// 実測で 6〜11% 減ります（1024x768 の写真風で 16 色 14.35 → 12.73、256 色 3.41 → 3.19）。<br/>
    /// <b>品質ごとの反復の上限</b>: <see cref="EsImageQuality.Low"/> は詰め直しを行いません（最速を優先）。
    /// <see cref="EsImageQuality.Mid"/> は「使われている箱の数 × 色数」で決まる<b>作業量の上限</b>で回数を絞り、
    /// <see cref="EsImageQuality.High"/> は上限を外して収束まで回します（<see cref="MaximumRefinementIterations"/> が上限）。<br/>
    /// <b>悪化させない</b>: 反復のたびに目的関数（画素数の重み付き二乗距離の合計）を求め、改善しなければ
    /// 直前のパレットを残して止めます（同じ値で振動している場合も止まります）。 このため結果が詰め直す前より
    /// 悪くなることはありません。<br/>
    /// <b>使われている箱だけを回す理由</b>: 画素が 1 つも無い箱は割り当てにも平均にも寄与しません。
    /// <b>箱が色数以下なら何もしない</b>: 使われている箱をすべて別の色にできるためです
    /// （スクリーンショットの平坦な面がこれに当たる）。
    /// </remarks>
    private static void RefinePalette(
        int[] bins, int binCount, int[] counts, long[] sums, uint[] palette, EsImageQuality quality,
        LabColor[] binLab, byte[] binAlphaTier)
    {
        if (quality == EsImageQuality.Low || binCount <= palette.Length)
        {
            return;
        }

        var (paletteLab, paletteAlphaTiers) = PreparePalette(palette);
        var assignment = new byte[binCount];
        var objective = AssignBins(
            bins, binLab, binAlphaTier, counts, paletteLab, paletteAlphaTiers, assignment);
        var clusterCounts = new long[palette.Length];
        var clusterSums = new long[palette.Length * 4];

        // 1 回の反復の費用は「箱の数 × 色数」に比例するため、既定の品質では効果の小さい画像の回数を減らす
        var iterations = quality == EsImageQuality.High
            ? MaximumRefinementIterations
            : Math.Clamp(
                RefinementWorkBudget / Math.Max(1, (long)binCount * palette.Length),
                1,
                MaximumRefinementIterations);

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            Array.Clear(clusterCounts);
            Array.Clear(clusterSums);
            for (var i = 0; i < binCount; i++)
            {
                var key = bins[i];
                var index = assignment[i];
                clusterCounts[index] += counts[key];
                clusterSums[(index * 4) + 0] += sums[(key * 4) + 0];
                clusterSums[(index * 4) + 1] += sums[(key * 4) + 1];
                clusterSums[(index * 4) + 2] += sums[(key * 4) + 2];
                clusterSums[(index * 4) + 3] += sums[(key * 4) + 3];
            }

            var candidate = new uint[palette.Length];
            for (var i = 0; i < candidate.Length; i++)
            {
                candidate[i] = clusterCounts[i] == 0
                    ? palette[i]
                    : PackColor(
                        clusterSums[(i * 4) + 0],
                        clusterSums[(i * 4) + 1],
                        clusterSums[(i * 4) + 2],
                        clusterSums[(i * 4) + 3],
                        clusterCounts[i]);
            }

            // 新しい色の Lab も 1 度だけ求める（距離の計算は Lab どうしで行う）
            var (candidateLab, candidateAlphaTiers) = PreparePalette(candidate);
            var candidateObjective = AssignBins(
                bins, binLab, binAlphaTier, counts, candidateLab, candidateAlphaTiers, assignment);
            if (candidateObjective >= objective)
            {
                return;
            }

            objective = candidateObjective;
            Array.Copy(candidate, palette, palette.Length);
        }
    }

    /// <summary>箱を最も近いパレットの色へ割り当て、その割り当ての目的関数（画素数の重み付き距離の合計）を返す。</summary>
    /// <param name="bins">使われている箱の番号の並び（<paramref name="counts"/> を引くために要る）。</param>
    /// <param name="binLab">箱の代表色の Lab（<paramref name="bins"/> と同じ並び）。</param>
    /// <param name="binAlphaTier">箱の代表色のアルファの段階（0〜3）。</param>
    /// <param name="counts">箱ごとの画素数。</param>
    /// <param name="paletteLab">パレットの Lab。</param>
    /// <param name="paletteAlphaTiers">パレットのアルファの段階（0〜3）。</param>
    /// <param name="assignment">割り当てた色番号の書き先（<paramref name="binLab"/> と同じ長さ以上）。</param>
    /// <returns>目的関数の値。</returns>
    /// <remarks>
    /// <b>箱の代表色（平均色）で測る</b>: 尺度は <see cref="Distance"/> が単一所有します（参照表を作るときと同じです）。
    /// 箱の中心ではなく平均を使う理由は <see cref="BinCentroid"/> を参照。<br/>
    /// <b>Lab は呼び出し側が渡す</b>: 箱の代表色は反復で変わらないため、1 度求めた値を再利用します
    /// （色ごとに立方根と冪を計算する費用を反復回数ぶん繰り返さない）。
    /// </remarks>
    private static long AssignBins(
        int[] bins, LabColor[] binLab, byte[] binAlphaTier, int[] counts,
        LabColor[] paletteLab, byte[] paletteAlphaTiers, byte[] assignment)
    {
        var objective = 0L;
        for (var i = 0; i < binLab.Length; i++)
        {
            var target = binLab[i];
            var targetTier = binAlphaTier[i];

            // 内側のループは 1 回の減色で最も多く実行される（箱の数 × 色数 × 反復回数）。
            // 距離の計算をここへ直接書き、int の演算だけで済ませる（メソッド呼び出しと long の演算を避ける）
            var best = 0;
            var bestDistance = int.MaxValue;
            for (var c = 0; c < paletteLab.Length; c++)
            {
                var color = paletteLab[c];
                var deltaL = color.L - target.L;
                var deltaA = color.A - target.A;
                var deltaB = color.B - target.B;
                var deltaTier = paletteAlphaTiers[c] - targetTier;
                var distance = (deltaL * deltaL) + (deltaA * deltaA) + (deltaB * deltaB)
                    + (TierPenalty * deltaTier * deltaTier);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = c;
                }
            }

            assignment[i] = (byte)best;
            objective += counts[bins[i]] * (long)bestDistance;
        }

        return objective;
    }

    /// <summary>箱の要素を指定の軸の成分の順に<b>その場で</b>並べ替える。</summary>
    /// <param name="bins">箱の番号の並び。</param>
    /// <param name="start">箱の開始位置。</param>
    /// <param name="length">箱の長さ。</param>
    /// <param name="axis">軸（0 = 赤 / 1 = 緑 / 2 = 青 / 3 = アルファ）。</param>
    /// <remarks>
    /// <b>範囲を渡して並べる理由（実測）</b>: 以前は箱の複製を並べ替えてから書き戻していた（複製と書き戻しで
    /// 箱の全要素を 3 回写す）。 範囲をその場で並べ替えると写しは 1 回になる。<br/>
    /// <b>比較関数を作る理由</b>: 並べ替えの鍵は<b>軸の成分</b>であり、箱ごとに違う。 <c>Span&lt;int&gt;</c> の
    /// 並べ替えは比較関数を取るため、軸を捉えた小さな関数が 1 回の分割につき 1 つできる（箱の複製より小さい）。
    /// </remarks>
    private static void SortByAxis(int[] bins, int start, int length, int axis)
        => bins.AsSpan(start, length).Sort(
            (left, right) => HistogramComponent(left, axis).CompareTo(HistogramComponent(right, axis)));

    /// <summary>分ける価値が最も高い箱を探す。</summary>
    /// <param name="pixels">箱ごとの画素数。</param>
    /// <param name="axisLengths">箱ごとの最も長い軸の長さ。</param>
    /// <param name="lengths">箱ごとの長さ。</param>
    /// <param name="boxCount">箱の数。</param>
    /// <returns>箱の位置（分けられる箱が無い場合は -1）。</returns>
    /// <remarks>
    /// <b>数え直さない理由</b>: 分ける価値（最も長い軸の長さ × 画素数）が変わるのは<b>分割した箱の 2 つだけ</b>で
    /// ある（呼び出し側が指標を持ち回る）。 そのためここは箱の数（最大 256）だけを見ればよい。 価値の比較は
    /// <c>long</c> で行う（画素数 × 31 が <c>int</c> の範囲を超える大きさの画像でも順序が壊れない）。
    /// </remarks>
    private static int FindWidestBox(long[] pixels, int[] axisLengths, int[] lengths, int boxCount)
    {
        var best = -1;
        var widest = 0L;
        for (var i = 0; i < boxCount; i++)
        {
            if (lengths[i] < 2)
            {
                continue;
            }

            var volume = axisLengths[i] * pixels[i];
            if (volume > widest)
            {
                widest = volume;
                best = i;
            }
        }

        return best;
    }

    /// <summary>箱の成分の範囲が最も長い軸を求める。</summary>
    /// <param name="bins">箱の番号の並び。</param>
    /// <param name="start">箱の開始位置。</param>
    /// <param name="length">箱の長さ。</param>
    /// <returns>軸（0 = 赤 / 1 = 緑 / 2 = 青 / 3 = アルファ）。</returns>
    private static int LongestAxis(int[] bins, int start, int length)
    {
        var ranges = ColorRanges(bins, start, length);
        var alpha = ranges.Alpha * HistogramAlphaScale;
        if (alpha >= ranges.Red && alpha >= ranges.Green && alpha >= ranges.Blue)
        {
            return 3;
        }

        return ranges.Red >= ranges.Green && ranges.Red >= ranges.Blue
            ? 0
            : ranges.Green >= ranges.Blue ? 1 : 2;
    }

    /// <summary>箱の成分の範囲が最も長い長さを求める（アルファは RGB と同じ尺度へ揃える）。</summary>
    /// <param name="bins">箱の番号の並び。</param>
    /// <param name="start">箱の開始位置。</param>
    /// <param name="length">箱の長さ。</param>
    /// <returns>長さ。</returns>
    private static int LongestAxisLength(int[] bins, int start, int length)
    {
        var ranges = ColorRanges(bins, start, length);
        return Math.Max(
            ranges.Alpha * HistogramAlphaScale,
            Math.Max(ranges.Red, Math.Max(ranges.Green, ranges.Blue)));
    }

    /// <summary>箱の成分ごとの範囲（最大 − 最小）を求める。</summary>
    /// <param name="bins">箱の番号の並び。</param>
    /// <param name="start">箱の開始位置。</param>
    /// <param name="length">箱の長さ。</param>
    /// <returns>成分ごとの範囲（RGB は 0〜31、アルファは 0〜3）。</returns>
    private static (int Red, int Green, int Blue, int Alpha) ColorRanges(int[] bins, int start, int length)
    {
        var minRed = HistogramSize;
        var maxRed = 0;
        var minGreen = HistogramSize;
        var maxGreen = 0;
        var minBlue = HistogramSize;
        var maxBlue = 0;
        var minAlpha = HistogramSize;
        var maxAlpha = 0;

        var end = start + length;
        for (var i = start; i < end; i++)
        {
            var key = bins[i];
            var red = HistogramRed(key);
            var green = HistogramGreen(key);
            var blue = HistogramBlue(key);
            var alpha = HistogramAlpha(key);
            if (red < minRed) { minRed = red; }
            if (red > maxRed) { maxRed = red; }
            if (green < minGreen) { minGreen = green; }
            if (green > maxGreen) { maxGreen = green; }
            if (blue < minBlue) { minBlue = blue; }
            if (blue > maxBlue) { maxBlue = blue; }
            if (alpha < minAlpha) { minAlpha = alpha; }
            if (alpha > maxAlpha) { maxAlpha = alpha; }
        }

        return (maxRed - minRed, maxGreen - minGreen, maxBlue - minBlue, maxAlpha - minAlpha);
    }

    /// <summary>箱に入っている画素の数を求める。</summary>
    /// <param name="bins">箱の番号の並び。</param>
    /// <param name="start">箱の開始位置。</param>
    /// <param name="length">箱の長さ。</param>
    /// <param name="counts">箱ごとの画素数。</param>
    /// <returns>画素数。</returns>
    private static long CountPixels(int[] bins, int start, int length, int[] counts)
    {
        var sum = 0L;
        var end = start + length;
        for (var i = start; i < end; i++)
        {
            sum += counts[bins[i]];
        }

        return sum;
    }

    /// <summary>箱の平均色を求める（ARGB。 アルファも平均する）。</summary>
    /// <param name="bins">箱の番号の並び。</param>
    /// <param name="start">箱の開始位置。</param>
    /// <param name="length">箱の長さ。</param>
    /// <param name="counts">箱ごとの画素数。</param>
    /// <param name="sums">箱ごとの成分の合計（箱 × 4）。</param>
    /// <returns>ARGB の色。</returns>
    private static uint AverageColor(int[] bins, int start, int length, int[] counts, long[] sums)
    {
        long red = 0;
        long green = 0;
        long blue = 0;
        long alpha = 0;
        long total = 0;
        var end = start + length;
        for (var i = start; i < end; i++)
        {
            var key = bins[i];
            red += sums[(key * 4) + 0];
            green += sums[(key * 4) + 1];
            blue += sums[(key * 4) + 2];
            alpha += sums[(key * 4) + 3];
            total += counts[key];
        }

        if (total == 0)
        {
            return 0;
        }

        return PackColor(red, green, blue, alpha, total);
    }

    /// <summary>成分の合計と画素数から ARGB の色を求める。</summary>
    /// <param name="red">赤の合計（8 ビットの値）。</param>
    /// <param name="green">緑の合計（8 ビットの値）。</param>
    /// <param name="blue">青の合計（8 ビットの値）。</param>
    /// <param name="alpha">アルファの合計（8 ビットの値）。</param>
    /// <param name="total">画素数（正の値）。</param>
    /// <returns>ARGB の色。</returns>
    /// <remarks>sRGB の値のまま平均する理由と実測は <see cref="CountColors"/> を参照。</remarks>
    private static uint PackColor(long red, long green, long blue, long alpha, long total)
        => ((uint)(alpha / total) << 24)
            | ((uint)(red / total) << 16)
            | ((uint)(green / total) << 8)
            | (uint)(blue / total);

    /// <summary>最も近いパレットの色番号を求める（知覚の距離。 尺度は <see cref="Distance"/> が単一所有する）。</summary>
    /// <param name="paletteLab">パレットの Lab（<paramref name="paletteAlphaTiers"/> と同じ並び）。</param>
    /// <param name="paletteAlphaTiers">パレットのアルファの段階（0〜3）。</param>
    /// <param name="lab">対象の色の Lab。</param>
    /// <param name="alphaTier">対象の色のアルファの段階（0〜3）。</param>
    /// <returns>色番号。</returns>
    /// <remarks>尺度は <see cref="Distance"/> が単一所有します（詰め直しと同じ尺度です）。</remarks>
    private static int NearestColor(
        LabColor[] paletteLab, byte[] paletteAlphaTiers, in LabColor lab, int alphaTier)
    {
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < paletteLab.Length; i++)
        {
            var color = paletteLab[i];
            var deltaL = color.L - lab.L;
            var deltaA = color.A - lab.A;
            var deltaB = color.B - lab.B;
            var deltaTier = paletteAlphaTiers[i] - alphaTier;
            var distance = (deltaL * deltaL) + (deltaA * deltaA) + (deltaB * deltaB)
                + (TierPenalty * deltaTier * deltaTier);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>色と画素の距離を求める（パレットの色を選ぶときの尺度）。</summary>
    /// <param name="color">パレットの色の Lab。</param>
    /// <param name="target">対象の色の Lab。</param>
    /// <param name="colorAlphaTier">パレットの色のアルファの段階（0〜3）。</param>
    /// <param name="targetAlphaTier">対象の色のアルファの段階（0〜3）。</param>
    /// <returns>距離（小さいほど近い。 単位は ΔE とアルファの段階の合成）。</returns>
    /// <remarks>
    /// <b>知覚の尺度（Lab）を使う理由（実測）</b>: 素の RGB の二乗距離は<b>人の目の感度と一致しません</b>
    /// （暗部の差を過小に、明部や彩度の差を過大に見積もる）。 CIELAB の二乗距離（CIE76）に替えると、
    /// 同じパレットでも選ぶ色が変わり、出力の知覚誤差（CIEDE2000 の平均）が 1024x768 の画像で
    /// <b>7〜14% 減ります</b>（実測: グラデーション 16 色で 7.88 → 6.81、写真風 256 色で 2.30 → 2.07。
    /// <b>平坦な色の画像は 0.00 のまま変わらない</b>）。 <b>費用は画素ごとに増えません</b>（画素の走査は参照表を
    /// 引くだけで、Lab への変換は「使われている箱」とパレットの色について 1 度だけ行います）。<br/>
    /// <b>CIEDE2000 を使わない理由（実測）</b>: さらに 3〜9 ポイント良くなりますが（6.81 → 6.25）、
    /// 距離 1 回ごとに立方根・冪・三角関数が要り、詰め直しの「箱の数 × 色数 × 反復回数」の回数だけ呼ばれるため
    /// 現実的ではありません。<br/>
    /// <b>輝度の重み（Rec.709 等）を使わない理由（実測）</b>: 素の RGB に輝度の重みを付ける案は
    /// <b>かえって悪化します</b>（グラデーション 16 色で 7.88 → 8.10）。 RGB の値は既に sRGB の
    /// 符号化（ガンマ）を含んでおり、そこへ重みを掛けると暗部の差が過小になるためです。<br/>
    /// <b>アルファの段階の重み</b>: 画素のアルファは減色で 4 段階に量子化されるため、段階が違う色は RGB が
    /// 近くても選びません（選ぶと<b>透過が失われます</b>。 実測: アルファを無視すると完全透明 1,408 画素が
    /// 不透明になった）。 1 段階の違いを RGB の 12.5%（≒ 12 ΔE）と釣り合わせます（<see cref="AlphaTierDeltaE"/>）。<br/>
    /// <b>箱の中心ではなく代表色（平均色）で測る</b>: 呼び出し側（<see cref="BinCentroid"/>）が平均色を渡します。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Distance(
        in LabColor color, in LabColor target, int colorAlphaTier, int targetAlphaTier)
    {
        var deltaL = color.L - target.L;
        var deltaA = color.A - target.A;
        var deltaB = color.B - target.B;
        var deltaTier = colorAlphaTier - targetAlphaTier;
        return ((long)deltaL * deltaL) + ((long)deltaA * deltaA) + ((long)deltaB * deltaB)
            + s_tierPenalties[deltaTier + MaximumAlphaTier];
    }

    /// <summary>アルファの段階の差 1 つ分の距離の加算値（<see cref="AlphaTierDeltaE"/> の 2 乗を固定小数点の尺度へ移した値）。</summary>
    /// <remarks>
    /// <b>掛け算を減らす理由</b>: 距離は「箱の数 × 色数 × 反復回数」回だけ呼ばれるため、定数を表にせず
    /// 直接掛けます（<c>deltaTier</c> は −3〜3 の小さな整数であり、<b>表を引くより掛けた方が速い</b>）。
    /// </remarks>
    private const int TierPenalty = AlphaTierDeltaE * LabScale * AlphaTierDeltaE * LabScale;

    /// <summary>色（8 ビットの RGB）を Lab（CIE L*a*b*・D65）へ変換する。</summary>
    /// <param name="red">赤（0〜255）。</param>
    /// <param name="green">緑（0〜255）。</param>
    /// <param name="blue">青（0〜255）。</param>
    /// <returns>Lab（1 / <see cref="LabScale"/> ΔE の固定小数点）。</returns>
    /// <remarks>
    /// <b>sRGB → 線形 → XYZ → Lab の順に変換する</b>: sRGB の値は既にガンマ符号化されているため、
    /// 線形へ戻してから XYZ へ進みます（線形のまま測ると暗部の差が過小になります）。<br/>
    /// <b>1 色あたり 1 度だけ呼ぶ</b>: 画素ごとではなく、使われている箱（数千）とパレットの色（256 以下）に
    /// ついてだけ呼びます。 距離の計算は整数（<see cref="LabColor"/>）で行います。
    /// </remarks>
    private static LabColor ToLab(int red, int green, int blue)
    {
        var linearRed = ToLinear(red);
        var linearGreen = ToLinear(green);
        var linearBlue = ToLinear(blue);

        // sRGB（D65）→ XYZ（0〜1 の相対値）
        var x = ((0.4124 * linearRed) + (0.3576 * linearGreen) + (0.1805 * linearBlue)) / WhiteX;
        var y = (0.2126 * linearRed) + (0.7152 * linearGreen) + (0.0722 * linearBlue);
        var z = ((0.0193 * linearRed) + (0.1192 * linearGreen) + (0.9505 * linearBlue)) / WhiteZ;

        var fx = LabF(x);
        var fy = LabF(y);
        var fz = LabF(z);
        return new LabColor(
            (int)Math.Round(((116 * fy) - 16) * LabScale),
            (int)Math.Round(500 * (fx - fy) * LabScale),
            (int)Math.Round(200 * (fy - fz) * LabScale));
    }

    /// <summary>sRGB の 1 成分（0〜255）を線形値（0〜1）へ変換する。</summary>
    /// <param name="value">成分（0〜255）。</param>
    /// <returns>線形値。</returns>
    /// <remarks>Lab への変換（<see cref="ToLab"/>）だけが使います（画素ごとには呼びません）。</remarks>
    private static double ToLinear(int value)
    {
        var v = value / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    /// <summary>CIELAB の変換関数 f（立方根の区分的な形）。</summary>
    /// <param name="value">XYZ の成分（白色点で正規化済み）。</param>
    /// <returns>f の値。</returns>
    private static double LabF(double value)
        => value > 216.0 / 24389.0 ? Math.Cbrt(value) : ((24389.0 / 27.0 * value) + 16) / 116;

    /// <summary>アルファの段階（0〜3）を取り出す（減色で量子化した 4 段階）。</summary>
    /// <param name="alpha">アルファ（0〜255）。</param>
    /// <returns>段階（0〜3）。</returns>
    private static int AlphaTier(int alpha) => alpha >> (8 - HistogramAlphaBits);

    /// <summary>パレットから Lab とアルファの段階の並びを求める（距離の計算の前準備）。</summary>
    /// <param name="palette">パレット（ARGB）。</param>
    /// <returns>Lab の並びとアルファの段階の並び（同じ添字が同じ色）。</returns>
    private static (LabColor[] Lab, byte[] AlphaTiers) PreparePalette(uint[] palette)
    {
        var lab = new LabColor[palette.Length];
        var tiers = new byte[palette.Length];
        for (var i = 0; i < palette.Length; i++)
        {
            var color = palette[i];
            lab[i] = ToLab((byte)(color >> 16), (byte)(color >> 8), (byte)color);
            tiers[i] = (byte)AlphaTier((byte)(color >> 24));
        }

        return (lab, tiers);
    }

    /// <summary>Lab の色（CIE L*a*b*。 各成分は 1 / <see cref="LabScale"/> ΔE の固定小数点）。</summary>
    /// <param name="L">明度（0〜100 × <see cref="LabScale"/>）。</param>
    /// <param name="A">緑〜赤（およそ −128〜127 × <see cref="LabScale"/>）。</param>
    /// <param name="B">青〜黄（およそ −128〜127 × <see cref="LabScale"/>）。</param>
    private readonly record struct LabColor(int L, int A, int B);

    /// <summary>アルファの段階の差（−3〜3）ごとの距離の加算値（<see cref="AlphaTierDeltaE"/> の 2 乗）。</summary>
    /// <remarks>
    /// <b>この表を使う箇所</b>: ループの外側（<see cref="NearestColor"/> を呼ぶ経路）だけです。
    /// 最も多く実行される内側のループ（<see cref="AssignBins"/>）は <see cref="TierPenalty"/> を直接掛けます。
    /// </remarks>
    private static readonly long[] s_tierPenalties = CreateTierPenalties();

    /// <summary>アルファの段階の差の最大（段階は 0〜3）。</summary>
    private const int MaximumAlphaTier = 3;

    /// <summary>アルファの段階の差ごとの距離の加算値を作る。</summary>
    /// <returns>差 −3〜3 の加算値（添字は差 + <see cref="MaximumAlphaTier"/>）。</returns>
    private static long[] CreateTierPenalties()
    {
        var penalties = new long[(MaximumAlphaTier * 2) + 1];
        for (var i = 0; i < penalties.Length; i++)
        {
            var deltaTier = i - MaximumAlphaTier;
            penalties[i] = (long)TierPenalty * deltaTier * deltaTier;
        }

        return penalties;
    }

    /// <summary>箱を指定の軸で並べ替えるための成分を取り出す。</summary>
    /// <param name="key">箱。</param>
    /// <param name="axis">軸（0 = 赤 / 1 = 緑 / 2 = 青 / 3 = アルファ）。</param>
    /// <returns>成分（RGB は 0〜31、アルファは 0〜3）。</returns>
    private static int HistogramComponent(int key, int axis)
        => axis switch
        {
            0 => HistogramRed(key),
            1 => HistogramGreen(key),
            2 => HistogramBlue(key),
            _ => HistogramAlpha(key),
        };

    /// <summary>色からヒストグラムの箱の番号を求める。</summary>
    /// <param name="red">赤（0〜255）。</param>
    /// <param name="green">緑（0〜255）。</param>
    /// <param name="blue">青（0〜255）。</param>
    /// <param name="alpha">アルファ（0〜255）。</param>
    /// <returns>箱の番号。</returns>
    private static int HistogramKey(int red, int green, int blue, int alpha)
        => ((alpha >> (8 - HistogramAlphaBits)) << (HistogramBits * 3))
            | ((red >> (8 - HistogramBits)) << (HistogramBits * 2))
            | ((green >> (8 - HistogramBits)) << HistogramBits)
            | (blue >> (8 - HistogramBits));

    /// <summary>箱のアルファの段階を取り出す。</summary>
    /// <param name="key">箱。</param>
    /// <returns>アルファの段階（0〜3）。</returns>
    private static int HistogramAlpha(int key) => key >> (HistogramBits * 3);

    /// <summary>箱の赤の成分を取り出す。</summary>
    /// <param name="key">箱。</param>
    /// <returns>赤（0〜31）。</returns>
    private static int HistogramRed(int key) => (key >> (HistogramBits * 2)) & ((1 << HistogramBits) - 1);

    /// <summary>箱の緑の成分を取り出す。</summary>
    /// <param name="key">箱。</param>
    /// <returns>緑（0〜31）。</returns>
    private static int HistogramGreen(int key) => (key >> HistogramBits) & ((1 << HistogramBits) - 1);

    /// <summary>箱の青の成分を取り出す。</summary>
    /// <param name="key">箱。</param>
    /// <returns>青（0〜31）。</returns>
    private static int HistogramBlue(int key) => key & ((1 << HistogramBits) - 1);

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
        var colMap = new int[target.Width];
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
        // （実測: 4K → 1920x1080 で 6.2 ms → 4.0 ms。 画素ごとに除算・床・クランプを行わない）
        var leftX = new int[target.Width];
        var rightX = new int[target.Width];
        var fx = new double[target.Width];
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
    /// メソッド境界を越えることで<b>境界チェックの除去とベクター化の最適化が失われる</b>（実測: 4K → 96x54 で 116 → 229 µs）。
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

    /// <summary>面積平均（box）で縮小する（<see cref="EsImageQuality.High"/> の実装）。</summary>
    /// <param name="pixels">対象の画素（BGRA8888。 行の余白を含んでよい）。</param>
    /// <param name="sourceWidth">元の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元の高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="target">縮小後の寸法（元より小さい）。</param>
    /// <returns>縮小した画像。</returns>
    /// <remarks>
    /// <b>1 パスで面積平均を求める</b>: 縮小先の 1 画素ごとに、対応する元画像の矩形（全画素）を合計して割る。
    /// <b>中間画像を作らない</b>ため、横 → 縦 の 2 パスで生じる丸めの重なりが無く、速度も速い（実測: 4K → 96x54 で
    /// 2 パスの 18 ms に対し 1 パスで 1.5 ms）。<br/>
    /// <b>列の範囲は行に依存しない</b>ため、1 度だけ求めて表にする（画素ごとの 64 ビット除算を無くす。
    /// 実測: 4K → 1920x1080 で 5.4 ms → 3.7 ms）。 成分ごとの合計は <c>stackalloc</c> の領域ではなく
    /// ローカル変数で持つ（画素ごとに領域を消す書き込みを無くす。 同じ実測に含む）。<br/>
    /// <b>行ごとに独立しているため並列化する</b>（実測: 4K → 96x54 で 逐次 18 ms → 並列 1.5 ms。 4K → 320x180 で 27 ms → 1.6 ms）。
    /// 並列化するのは元画像が <see cref="ParallelSourceThresholdPixels"/> 以上のときだけである（実測: 元 65,536 画素では逐次が速く、
    /// 131,044 画素で並列が逆転する）。<br/>
    /// <b>SIMD は使わない</b>: 面積平均をベクター化すると<b>縦の合計を保持する作業行が必要になり、
    /// 縮小先の行ごとに全体（4K で 15 KB）を消すコストが加わる</b>。 実測でも遅くなる
    /// （4K → 96x54 で 逐次 2.8 ms 対 現行 1.5 ms、4K → 960x540 で 14 ms 対 2.4 ms）。
    /// 現行は元画像の各画素を 1 回だけ読んで合計するため、読み出しの回数は既に最小である。
    /// </remarks>
    private static unsafe EsBitmap ScaleBox(
        byte* pixels, int sourceWidth, int sourceHeight, int stride, EsSizeInt target)
    {
        var result = EsBitmap.CreateUninitialized(target.Width, target.Height);
        var srcWidth = sourceWidth;
        var srcHeight = sourceHeight;
        var targetWidth = target.Width;

        // 列の範囲は行に依存しないため、1 度だけ求めて表にする
        // （実測: 4K → 1920x1080 で 5.4 ms → 3.7 ms。 画素ごとに 64 ビット除算を行わない）
        var x0s = new int[targetWidth];
        var x1s = new int[targetWidth];
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
    /// 縮小先の画素ごとに領域を消す書き込みが加わる（実測: 4K → 1920x1080 で 5.4 ms → 3.7 ms）。<br/>
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
