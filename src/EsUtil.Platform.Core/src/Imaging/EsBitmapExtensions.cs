using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像を<b>変更しない</b>操作（切り出し・縮小・減色）の拡張メソッド。</summary>
/// <remarks>
/// <b>なぜ拡張メソッドなのか</b>: <see cref="Crop(EsReadOnlyBitmap, EsRectInt)"/> は「自分を変更せず、新しい画像を作る」操作であり、
/// <see cref="EsReadOnlyBitmap"/> を実装する任意の画像（借用した読み取り専用の画像を含む）に対して使える必要がある。
/// 継承階層の型（<see cref="EsReadOnlyBitmap"/>）のメソッドにすると、本インターフェイスを直接実装する画像
/// （表示アダプタ等）から呼べない（実測: ルーペと切り出しの両方が使う）。<br/>
/// <b>戻り値の権利</b>: 新しく作った画像は<b>書き換え可能</b>（<see cref="EsBitmap"/>）で返す
/// （生成した側が所有し、破棄する責任を持つ。 複製が要る場合は <see cref="EsReadOnlyBitmap.Copy"/>、0 コピーの
/// 読み取りビューが要る場合は <see cref="EsReadOnlyBitmap.ToPixmap"/> を使う）。<br/>
/// <b>色の取得はここに置かない</b>: 「座標のピクセルを色として読む」は画像の読み取りそのものであり、
/// 読み取りの面（<see cref="EsReadOnlyBitmap.GetPixel(EsPointInt)"/>）が実装を持つ（追加の画像を作らないため、拡張メソッドである理由が無い）。<br/>
/// <b>減色の計算もここに置く</b>: 減色は「新しい画像を作る」操作として公開し（<see cref="ReduceColors"/>）、
/// 色番号とパレットを返す内部の計算（<see cref="ReduceToIndexed"/>）は保存の実装（8bpp のインデックス カラー）と共有する
/// （2 箇所に書くと、同じ色数でも保存経路によって結果が変わる）。
/// </remarks>
public static class EsBitmapExtensions
{
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

    /// <summary>減色（パレット化）で残せる最小の色数。</summary>
    internal const int MinimumColorCount = 2;

    /// <summary>減色（パレット化）で残せる最大の色数（1 画素 1 バイトで表せる上限）。</summary>
    internal const int MaximumColorCount = 256;

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

    /// <summary>本画像をファイルへ保存する（形式は拡張子が決める）。</summary>
    /// <param name="source">保存する画像。 借用であり破棄しない。</param>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> または <paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="EsReadOnlyBitmap.SupportedFileExtensions"/>）。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合。</exception>
    /// <remarks>
    /// <b>面（<see cref="EsReadOnlyBitmap"/>）の拡張メソッドである理由</b>: 保存は<b>画素を読むだけ</b>であり、
    /// 書き換えの権利も実体の型も要らない。 面の拡張にすると<b>受け取った画像をそのまま保存できる</b>
    /// （表示アダプタ等の外部実装の画像でもキャストが要らない）。<br/>
    /// <b>ピクセルは複製しない</b>: <see cref="EsReadOnlyBitmap.ReadOnlyPixels"/> のビューをそのままエンコードへ渡す
    /// （一時バッファを確保しない）。<br/>
    /// <b>Windows 専用</b>: OS の GDI+ を呼ぶため <see cref="SupportedOSPlatformAttribute"/> で限定する。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static void Save(this EsReadOnlyBitmap source, string filePath)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        ObjectDisposedException.ThrowIf(source.IsDisposed, source);
        WinGdiPlusImageCodec.EncodeToFile(source.ReadOnlyPixels, source.Width, source.Height, filePath);
    }

    /// <summary>本画像をファイルへ保存する（形式は拡張子が決める。保存の形式とオプションを指定できる）。</summary>
    /// <param name="source">保存する画像。 借用であり破棄しない。</param>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める。 <paramref name="options"/> で形式を指定した場合に拡張子が無ければ補完する）。</param>
    /// <param name="options">保存のオプション（形式ごとに使える項目が異なる）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> または <paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、または
    /// <paramref name="filePath"/> の拡張子と <see cref="EsImageSaveOptions.Format"/> が食い違い
    /// <see cref="EsImageSaveOptions.IgnoreFormatMismatch"/> が false の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の値が許容範囲外の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="EsReadOnlyBitmap.SupportedFileExtensions"/>）、または
    /// <paramref name="options"/> にその形式で使えない項目が設定されている場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合。</exception>
    /// <remarks>
    /// <b>形式の決め方</b>: <see cref="EsImageSaveOptions.Format"/> を指定しない場合は<b>拡張子が形式を決める</b>。
    /// 指定した場合は<b>形式が優先</b>され、拡張子が無ければ補完し（<c>shot</c> + <see cref="EsImageFileFormat.Png"/> →
    /// <c>shot.png</c>）、拡張子と食い違う場合は <see cref="EsImageSaveOptions.IgnoreFormatMismatch"/> が true なら
    /// 与えられたファイル名のまま保存し、false なら例外にする。<br/>
    /// <b>どのオプションが効くかは形式が決める</b>: 実測で効果が確認できたのは JPEG の品質
    /// （<see cref="EsImageSaveOptions.JpegQuality"/>）と減色（<see cref="EsImageSaveOptions.ColorCount"/>）だけであり、
    /// 他の形式へ設定すると例外になる（実測値と理由は <see cref="EsImageSaveOptions"/> を参照）。<br/>
    /// <b>オプションを省略した場合との違い</b>: 本オーバーロードは明示された値だけをエンコーダーへ渡すため、
    /// 既定値は GDI+ の既定と一致しない（実測: 1280x720 の不規則な画像で既定 826 KB に対し品質 75 は 554 KB）。
    /// 調整しない場合は <see cref="Save(EsReadOnlyBitmap, string)"/> を使う。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static void Save(this EsReadOnlyBitmap source, string filePath, EsImageSaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        ObjectDisposedException.ThrowIf(source.IsDisposed, source);
        WinGdiPlusImageCodec.EncodeToFile(source.ReadOnlyPixels, source.Width, source.Height, filePath, options);
    }

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
    public static EsBitmap Crop(this EsReadOnlyBitmap source, int x, int y, int width, int height)
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
            return EsBitmap.Create(width, height);
        }

        var result = EsBitmap.CreateUninitialized(width, height);
        var src = source.ReadOnlyPixels;
        var dst = result.Pixels;
        var srcStride = source.Stride;
        var dstStride = result.Stride;

        // コピーする 1 行のバイト数（cropW は「デスティネーションの幅 − 左パディング」以下にクランプ済み）
        var copyBytes = cropW * EsReadOnlyBitmap.BytesPerPixel;

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
        var leftBytes = dstOffsetX * EsReadOnlyBitmap.BytesPerPixel;
        var rightBytes = (width - dstOffsetX - cropW) * EsReadOnlyBitmap.BytesPerPixel;
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
                src.Slice(srcStride * (cropY + row) + (cropX * EsReadOnlyBitmap.BytesPerPixel), copyBytes)
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
    public static EsBitmap Crop(this EsReadOnlyBitmap source, EsRectInt rect)
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
    public static EsBitmap Resize(
        this EsReadOnlyBitmap source, int maxSize, DownscaleAlgorithm algorithm = DownscaleAlgorithm.Bilinear)
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

    /// <summary>画像を指定の色数へ減色する（パレット化。 画素数と寸法は変えない）。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="colorCount">残す色数（2〜256）。</param>
    /// <returns>減色した新しい画像（使われる色は <paramref name="colorCount"/> 種類以下）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="colorCount"/> が 2〜256 の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    /// <remarks>
    /// <b>不可逆である</b>: 元の画素は戻らない（減色後の色数は <paramref name="colorCount"/> 以下になる）。<br/>
    /// <b>透過は保たれる（実測）</b>: アルファも減色の対象であり、透明な画素は透明なパレットの色へ寄る。
    /// 保存した PNG には <c>tRNS</c>（インデックス透過）、GIF には透明の色番号が書かれる。<br/>
    /// <b>用途</b>: 保存のファイル サイズを小さくする（<see cref="EsImageSaveOptions.ColorCount"/> を指定した保存は
    /// 本処理を行う）。 切り出し・縮小と同じく<b>新しい画像を作る</b>ため、元の画像は変更しない。<br/>
    /// <b>アルゴリズム（実測で選んだ）</b>: 5 ビットのヒストグラム（32³ = 32,768 箱）を作り、
    /// <b>中央値分割</b>で箱を色数まで分け、箱ごとの平均色をパレットにする。 画素の割り当ては
    /// <b>箱ごとに 1 度だけ最近傍を求めた参照表</b>を引く（画素ごとに 256 色と比較しない）。
    /// 実測（1920x1080・256 色）: <b>30 ms</b>（16 色で 8 ms）。<br/>
    /// <b>色数はアルファの段階も数える</b>: アルファは 4 段階（0〜63 / 64〜127 / 128〜191 / 192〜255）に
    /// 量子化されるため、同じ RGB でもアルファが違えば別の色としてパレットを消費する
    /// （不透明な画像ではアルファが 1 段階のため、従来と同じ結果になる）。<br/>
    /// <b>ディザを行わない理由（実測）</b>: ディザは見た目を滑らかにするが、パレットの色を交互に置くため
    /// <b>保存のファイルが大きくなる</b>（実測: Magick.NET の 256 色 PNG で 645,647 B → FloydSteinberg で 803,118 B
    /// ＝ <b>+24%</b>）。 本ライブラリはファイル サイズを下げる目的で減色するため、行わない。<br/>
    /// <b>外部ライブラリとの比較（実測）</b>: 同じ 1920x1080・256 色で、本処理は 30 ms、
    /// SixLabors.ImageSharp の Wu 量子化は 219 ms、Magick.NET は 553 ms である
    /// （サイズも本処理の 173,684 B に対し 111,764 B / 117,156 B と、写真では外部の方が小さい。
    /// 減色の品質は pngquant が最も高いが、実行ファイルの同梱と 1 枚 700 ms を要する）。
    /// </remarks>
    public static EsBitmap ReduceColors(this EsReadOnlyBitmap source, int colorCount)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (colorCount < MinimumColorCount || colorCount > MaximumColorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(colorCount), colorCount, $"残す色数は {MinimumColorCount}〜{MaximumColorCount} の範囲で指定します。");
        }

        var width = source.Width;
        var height = source.Height;
        var (indices, palette) = ReduceToIndexed(source.ReadOnlyPixels, width, height, colorCount);

        var result = EsBitmap.CreateUninitialized(width, height);
        var destination = result.Pixels;
        for (var i = 0; i < indices.Length; i++)
        {
            // パレットは ARGB、画素は BGRA の並びである
            var color = palette[indices[i]];
            var offset = i * EsReadOnlyBitmap.BytesPerPixel;
            destination[offset] = (byte)color;
            destination[offset + 1] = (byte)(color >> 8);
            destination[offset + 2] = (byte)(color >> 16);
            destination[offset + 3] = (byte)(color >> 24);
        }

        return result;
    }

    /// <summary>画素を指定の色数へ減色し、色番号とパレットで返す（保存の実装と共有する）。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="colorCount">残す色数（2〜256。 呼び出し側が検証済みであること）。</param>
    /// <returns>色番号（1 画素 1 バイト）とパレット（ARGB）。</returns>
    /// <remarks>
    /// <b>色番号で返す理由</b>: 8bpp のインデックス カラーとしてファイルへ書く経路（<c>GdipSetImagePalette</c>）が
    /// 必要とする形であり、画素（BGRA）へ戻すのは <see cref="ReduceColors"/> が行う。<br/>
    /// <b>アルファも減色の対象である（実測）</b>: アルファを 4 段階に量子化して色と一緒に分けるため、
    /// パレットの色はアルファを持ち、<b>GDI+ が PNG へ <c>tRNS</c>（インデックス透過）を書き、GIF へ透明の
    /// 色番号を書く</b>（実測: 完全透明の 1,408 画素がどちらも保たれた）。 アルファを無視すると
    /// <b>透明な領域が不透明になる</b>（実測: パレットのアルファが 255 になった）。<br/>
    /// <b>RGB は保持している値（アルファ乗算済み）を平均する</b>: 半透明の画素は乗算済みの値がそのまま
    /// パレットへ入る（PNG の <c>tRNS</c> は乗算前の色として解釈されるため、半透明の色はやや暗くなる。
    /// 透明（アルファ 0）と不透明（アルファ 255）は完全に一致する）。
    /// </remarks>
    internal static (byte[] Indices, uint[] Palette) ReduceToIndexed(
        ReadOnlySpan<byte> pixels, int width, int height, int colorCount)
    {
        var counts = new int[HistogramSize];
        var sums = new long[HistogramSize * 4];
        CountColors(pixels, counts, sums);

        var boxes = SplitBoxes(counts, colorCount);
        var palette = new uint[boxes.Count];
        for (var i = 0; i < boxes.Count; i++)
        {
            palette[i] = AverageColor(boxes[i], counts, sums);
        }

        // 箱 → 色番号の参照表（画素ごとに最近傍を求めない）
        var lookup = new byte[HistogramSize];
        for (var key = 0; key < HistogramSize; key++)
        {
            lookup[key] = (byte)NearestColor(
                palette, HistogramRed(key), HistogramGreen(key), HistogramBlue(key), HistogramAlpha(key));
        }

        var indices = new byte[width * height];
        for (var i = 0; i < indices.Length; i++)
        {
            var offset = i * EsReadOnlyBitmap.BytesPerPixel;
            indices[i] = lookup[HistogramKey(
                pixels[offset + 2], pixels[offset + 1], pixels[offset], pixels[offset + 3])];
        }

        return (indices, palette);
    }

    /// <summary>画素をヒストグラムへ数える（箱ごとの画素数と成分の合計）。</summary>
    /// <param name="pixels">元の画素（BGRA8888）。</param>
    /// <param name="counts">箱ごとの画素数の書き先。</param>
    /// <param name="sums">箱ごとの成分の合計の書き先（箱 × 4。 最後の 1 つがアルファ）。</param>
    private static void CountColors(ReadOnlySpan<byte> pixels, int[] counts, long[] sums)
    {
        for (var i = 0; i < pixels.Length; i += EsReadOnlyBitmap.BytesPerPixel)
        {
            var key = HistogramKey(pixels[i + 2], pixels[i + 1], pixels[i], pixels[i + 3]);
            counts[key]++;
            sums[(key * 4) + 0] += pixels[i + 2];
            sums[(key * 4) + 1] += pixels[i + 1];
            sums[(key * 4) + 2] += pixels[i];
            sums[(key * 4) + 3] += pixels[i + 3];
        }
    }

    /// <summary>使われている箱を中央値分割で指定の個数まで分ける。</summary>
    /// <param name="counts">箱ごとの画素数。</param>
    /// <param name="colorCount">分ける個数（色数）。</param>
    /// <returns>箱の集合（各要素は使われている箱の番号の並び）。</returns>
    /// <remarks>
    /// <b>分割する箱の選び方</b>: 「成分の範囲の長さ × 画素数」が最も大きい箱を選ぶ（範囲が広く画素が多い箱ほど、
    /// 分けたときの誤差が減る）。<b>切る位置</b>は画素数が半分になる位置である（中央値分割。 色の分布が偏っていても
    /// 各箱の画素数が揃う）。
    /// </remarks>
    private static List<List<int>> SplitBoxes(int[] counts, int colorCount)
    {
        var used = new List<int>(1024);
        for (var key = 0; key < HistogramSize; key++)
        {
            if (counts[key] != 0)
            {
                used.Add(key);
            }
        }

        var boxes = new List<List<int>> { used };
        while (boxes.Count < colorCount)
        {
            var target = FindWidestBox(boxes, counts);
            if (target < 0)
            {
                // どの箱も分けられない（使われている色が指定より少ない）
                break;
            }

            var box = boxes[target];
            var axis = LongestAxis(box);
            box.Sort((left, right) => HistogramComponent(left, axis).CompareTo(HistogramComponent(right, axis)));

            var half = CountPixels(box, counts) / 2;
            var running = 0;
            var cut = 1;
            for (var i = 0; i < box.Count - 1; i++)
            {
                running += counts[box[i]];
                if (running >= half)
                {
                    cut = i + 1;
                    break;
                }
            }

            boxes[target] = box.GetRange(0, cut);
            boxes.Add(box.GetRange(cut, box.Count - cut));
        }

        return boxes;
    }

    /// <summary>アルファの広がりを RGB の広がりと比較するための倍率。</summary>
    /// <remarks>
    /// アルファは 4 段階（0〜3）であり、RGB は 32 段階（0〜31）である。 そのまま比較するとアルファの
    /// 広がりが最大 3 にしかならず、<b>RGB が同じでアルファだけが違う画像では分割が止まる</b>
    /// （実測: 同じ RGB で 3 段階のアルファを持つ画像が 1 色にまとまり、すべてアルファ 127 になった）。
    /// 4 段階 × 8 = 24 として RGB の最大 31 と同程度に扱う。
    /// </remarks>
    private const int HistogramAlphaScale = 8;

    /// <summary>分ける価値が最も高い箱を探す。</summary>
    /// <param name="boxes">箱の集合。</param>
    /// <param name="counts">箱ごとの画素数。</param>
    /// <returns>箱の位置（分けられる箱が無い場合は -1）。</returns>
    private static int FindWidestBox(List<List<int>> boxes, int[] counts)
    {
        var best = -1;
        var widest = 0;
        for (var i = 0; i < boxes.Count; i++)
        {
            if (boxes[i].Count < 2)
            {
                continue;
            }

            var volume = LongestAxisLength(boxes[i]) * CountPixels(boxes[i], counts);
            if (volume > widest)
            {
                widest = volume;
                best = i;
            }
        }

        return best;
    }

    /// <summary>箱の成分の範囲が最も長い軸を求める。</summary>
    /// <param name="box">箱。</param>
    /// <returns>軸（0 = 赤 / 1 = 緑 / 2 = 青 / 3 = アルファ）。</returns>
    private static int LongestAxis(List<int> box)
    {
        var ranges = ColorRanges(box);
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
    /// <param name="box">箱。</param>
    /// <returns>長さ。</returns>
    private static int LongestAxisLength(List<int> box)
    {
        var ranges = ColorRanges(box);
        return Math.Max(
            ranges.Alpha * HistogramAlphaScale,
            Math.Max(ranges.Red, Math.Max(ranges.Green, ranges.Blue)));
    }

    /// <summary>箱の成分ごとの範囲（最大 − 最小）を求める。</summary>
    /// <param name="box">箱。</param>
    /// <returns>成分ごとの範囲（RGB は 0〜31、アルファは 0〜3）。</returns>
    private static (int Red, int Green, int Blue, int Alpha) ColorRanges(List<int> box)
    {
        var minRed = HistogramSize;
        var maxRed = 0;
        var minGreen = HistogramSize;
        var maxGreen = 0;
        var minBlue = HistogramSize;
        var maxBlue = 0;
        var minAlpha = HistogramSize;
        var maxAlpha = 0;

        foreach (var key in box)
        {
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
    /// <param name="box">箱。</param>
    /// <param name="counts">箱ごとの画素数。</param>
    /// <returns>画素数。</returns>
    private static int CountPixels(List<int> box, int[] counts)
    {
        var sum = 0;
        foreach (var key in box)
        {
            sum += counts[key];
        }

        return sum;
    }

    /// <summary>箱の平均色を求める（ARGB。 アルファも平均する）。</summary>
    /// <param name="box">箱。</param>
    /// <param name="counts">箱ごとの画素数。</param>
    /// <param name="sums">箱ごとの成分の合計（箱 × 4）。</param>
    /// <returns>ARGB の色。</returns>
    private static uint AverageColor(List<int> box, int[] counts, long[] sums)
    {
        long red = 0;
        long green = 0;
        long blue = 0;
        long alpha = 0;
        long total = 0;
        foreach (var key in box)
        {
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

        return ((uint)(alpha / total) << 24)
            | ((uint)(red / total) << 16)
            | ((uint)(green / total) << 8)
            | (uint)(blue / total);
    }

    /// <summary>最も近いパレットの色番号を求める（成分とアルファの二乗距離）。</summary>
    /// <param name="palette">パレット（ARGB）。</param>
    /// <param name="red">赤（0〜31）。</param>
    /// <param name="green">緑（0〜31）。</param>
    /// <param name="blue">青（0〜31）。</param>
    /// <param name="alpha">アルファ（0〜3）。</param>
    /// <returns>色番号。</returns>
    /// <remarks>
    /// <b>アルファを重く見る理由</b>: アルファは最も粗く量子化される（4 段階）ため、同じ重みにすると
    /// 透明な画素が不透明な色へ寄って<b>透過が失われる</b>。 アルファの差を 16 倍して距離に加えることで、
    /// アルファの段階が違う色は RGB が近くても選ばれない（同じ段階の色が必ず近くなる）。
    /// </remarks>
    private static int NearestColor(uint[] palette, int red, int green, int blue, int alpha)
    {
        var best = 0;
        var bestDistance = long.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var color = palette[i];
            var deltaRed = ((int)((color >> 16) & 0xFF) >> (8 - HistogramBits)) - red;
            var deltaGreen = ((int)((color >> 8) & 0xFF) >> (8 - HistogramBits)) - green;
            var deltaBlue = ((int)(color & 0xFF) >> (8 - HistogramBits)) - blue;
            var deltaAlpha = ((int)((color >> 24) & 0xFF) >> (8 - HistogramAlphaBits)) - alpha;
            var distance = (long)(deltaRed * deltaRed) + (deltaGreen * deltaGreen) + (deltaBlue * deltaBlue)
                + (16L * deltaAlpha * deltaAlpha);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
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

    /// <summary>最近傍補間で縮小する（<see cref="DownscaleAlgorithm.Nearest"/> の実装）。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="target">縮小後の寸法（元より小さい）。</param>
    /// <returns>縮小した画像。</returns>
    private static EsBitmap ScaleNearest(EsReadOnlyBitmap source, EsSizeInt target)
    {
        var result = EsBitmap.CreateUninitialized(target.Width, target.Height);
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
        EsReadOnlyBitmap source, EsBitmap result, int[] colMap, int yStart, int yEnd)
    {
        var srcHeight = source.Height;
        var srcWidth = source.Width;
        var srcStrideUInt = source.Stride / sizeof(uint);
        var dstStrideUInt = result.Stride / sizeof(uint);
        var src = MemoryMarshal.Cast<byte, uint>(source.ReadOnlyPixels);
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
    /// <summary>双線形補間で縮小する（<see cref="DownscaleAlgorithm.Bilinear"/> の実装）。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="target">縮小後の寸法（元より小さい）。</param>
    /// <returns>縮小した画像。</returns>
    private static EsBitmap ScaleBilinear(EsReadOnlyBitmap source, EsSizeInt target)
    {
        var result = EsBitmap.CreateUninitialized(target.Width, target.Height);

        // 列の対応（元の 2 列の位置と重み）は行に依存しないため、1 度だけ求めて表にする
        // （実測: 4K → 1920x1080 で 6.2 ms → 4.0 ms。 画素ごとに除算・床・クランプを行わない）
        var leftX = new int[target.Width];
        var rightX = new int[target.Width];
        var fx = new double[target.Width];
        for (var x = 0; x < target.Width; x++)
        {
            var sourceX = (((x + 0.5) * source.Width) / target.Width) - 0.5;
            var x0 = Math.Clamp((int)Math.Floor(sourceX), 0, source.Width - 1);
            leftX[x] = x0 * EsReadOnlyBitmap.BytesPerPixel;
            rightX[x] = Math.Min(x0 + 1, source.Width - 1) * EsReadOnlyBitmap.BytesPerPixel;
            fx[x] = Math.Clamp(sourceX - x0, 0.0, 1.0);
        }

        // 1 行あたりの仕事が小さいため、縮小後の画素数で並列化を判定する
        if ((long)target.Width * target.Height >= ParallelTargetThresholdPixels)
        {
            RunRowRanges(target.Height, (from, to) => BilinearRows(source, result, from, to, target, leftX, rightX, fx));
        }
        else
        {
            BilinearRows(source, result, 0, target.Height, target, leftX, rightX, fx);
        }

        return result;
    }

    /// <summary>縮小先の行範囲を双線形補間で求め、書き込む。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="result">書き込み先（<paramref name="yStart"/>〜<paramref name="yEnd"/> の行を書く）。</param>
    /// <param name="yStart">開始行（含む）。</param>
    /// <param name="yEnd">終了行（含まない）。</param>
    /// <param name="target">縮小後の寸法。</param>
    /// <param name="leftX">縮小先の列が参照する元の左の列のバイト オフセット（事前計算）。</param>
    /// <param name="rightX">縮小先の列が参照する元の右の列のバイト オフセット（事前計算）。</param>
    /// <param name="fx">左の列と右の列の重み（0〜1。 事前計算）。</param>
    /// <remarks>
    /// <b>行を範囲で受ける理由</b>: 1 行ずつメソッドを呼ぶと、行ごとに <see cref="EsReadOnlyBitmap.ReadOnlyPixels"/> を取り直すうえ、
    /// メソッド境界を越えることで<b>境界チェックの除去とベクター化の最適化が失われる</b>（実測: 4K → 96x54 で 116 → 229 µs）。
    /// 範囲を受け取れば、逐次（1 回で全行）と並列（範囲ごと）が<b>同じ実装</b>を使いながら最適化を保てます。<br/>
    /// <b>列の対応を引数で受ける理由</b>: 列の対応（元の 2 列と重み）は行に依存しないため、行ごとに
    /// 除算・床・クランプをやり直さない（<see cref="ScaleBilinear"/> が 1 度だけ求める）。
    /// </remarks>
    private static void BilinearRows(
        EsReadOnlyBitmap source, EsBitmap result, int yStart, int yEnd, EsSizeInt target,
        int[] leftX, int[] rightX, double[] fx)
    {
        var srcHeight = source.Height;
        var src = source.ReadOnlyPixels;
        var srcStride = source.Stride;
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

    /// <summary>面積平均（box）で縮小する（<see cref="DownscaleAlgorithm.Box"/> の実装）。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
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
    private static EsBitmap ScaleBox(EsReadOnlyBitmap source, EsSizeInt target)
    {
        var result = EsBitmap.CreateUninitialized(target.Width, target.Height);
        var srcWidth = source.Width;
        var srcHeight = source.Height;
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
                source, result, from, to, srcHeight, target.Height, targetWidth, x0s, x1s));
        }
        else
        {
            AverageBlockRows(source, result, 0, target.Height, srcHeight, target.Height, targetWidth, x0s, x1s);
        }

        return result;
    }

    /// <summary>縮小先の行範囲を面積平均で求める（行ごとに独立しているため並列実行できる）。</summary>
    /// <param name="source">対象の画像。 借用であり破棄しない。</param>
    /// <param name="result">書き込み先（<paramref name="yStart"/>〜<paramref name="yEnd"/> の行を書く）。</param>
    /// <param name="yStart">開始行（含む）。</param>
    /// <param name="yEnd">終了行（含まない）。</param>
    /// <param name="srcHeight">元の高さ。</param>
    /// <param name="targetHeight">縮小後の高さ。</param>
    /// <param name="targetWidth">縮小後の幅。</param>
    /// <param name="x0s">縮小先の列が受け持つ元の範囲の開始列（事前計算）。</param>
    /// <param name="x1s">縮小先の列が受け持つ元の範囲の終了列（含まない。 事前計算）。</param>
    /// <remarks>
    /// <b>合計をローカル変数で持つ理由</b>: 成分ごとの合計を <c>stackalloc</c> の領域で持つと、
    /// 縮小先の画素ごとに領域を消す書き込みが加わる（実測: 4K → 1920x1080 で 5.4 ms → 3.7 ms）。<br/>
    /// <b>列の範囲を引数で受ける理由</b>: 列の範囲は行に依存しないため、画素ごとに 64 ビット除算を行わない
    /// （<see cref="ScaleBox"/> が 1 度だけ求める）。
    /// </remarks>
    private static void AverageBlockRows(
        EsReadOnlyBitmap source, EsBitmap result, int yStart, int yEnd,
        int srcHeight, int targetHeight, int targetWidth, int[] x0s, int[] x1s)
    {
        var src = source.ReadOnlyPixels;
        var srcStride = source.Stride;
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
