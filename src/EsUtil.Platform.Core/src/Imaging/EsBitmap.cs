using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;
using Color = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>複数のビットマップ種別を横断して扱う軽量なユニバーサルビットマップラッパー（コア）。</summary>
/// <remarks>
/// <b>設計</b>: 本型は自前のピクセルバッファ（アンマネージド）だけを所有し、ビットマップ種別に依存しない
/// ピクセル操作のみを提供する。 外部パッケージへ依存しない（UI 非依存・可搬の判定基準を満たす）。<br/>
/// UI ごとの生成・変換（System.Drawing / Avalonia / Skia）は各プラットフォーム層の拡張が担う。<br/>
/// <br/>
/// <b>メモリ確保</b>: <see cref="NativeMemory.AlignedAlloc"/> で 64 バイト整列して確保し、確保直後に全領域を 0 で初期化する
/// （透明 = 全成分 0 が「描かれていない」の表現であるため）。 全領域を直後に書く手順（複製・色付き生成・切り出し）は
/// 0 初期化を省き、切り出しでは書かれない領域（パディング）だけを透明にする。 整列は<b>確保の基底アドレスのみ</b>に適用し、
/// <see cref="Stride"/> は <c>幅 × 4</c>（パディングなし）とする（行も整列させるとストライドが変わり、
/// ピクセル形式を共有する他の実装の期待値を壊す）。<br/>
/// <br/>
/// <b>ピクセル形式</b>: BGRA8888（アルファ乗算済み、System.Drawing の Format32bppPArgb 相当）。<br/>
/// <see cref="Clear(byte, byte, byte, byte)"/> は<b>与えられた成分をそのまま書き込む</b>（乗算済みへの変換は行わない）。<br/>
/// <b>所有権</b>: ピクセルバッファの所有権は本型が保持し、<see cref="Dispose"/> で解放する。<br/>
/// <b>スレッド安全性</b>: 静的共有状態を持たないため各インスタンスは独立して並列利用できるが、
/// 単一インスタンスへの同時ピクセル操作は直列化すること。
/// </remarks>
public sealed class EsBitmap : IEsImageSource, IDisposable
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    /// <value>4。 ストライドは常に <c>幅 × 本値</c> である。</value>
    /// <remarks>相互変換（System.Drawing / Avalonia / Skia）がストライドを計算するために使う（同じ 4 を各所へ書かない）。</remarks>
    public const int BytesPerPixel = 4;

    /// <summary>確保するバッファの整列バイト数（SIMD・GPU 転送の要件を満たす）。</summary>
    private const int AlignmentBytes = 64;

    /// <summary>ピクセルバッファの先頭アドレス（0 は未確保）。</summary>
    private nint _buffer;

    private bool _disposed;

    /// <summary>指定サイズのピクセルバッファを確保する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="clear">true の場合は全領域を 0（透明）で初期化する。 直後に全領域を書く場合は false にして 0 埋めを省く。</param>
    /// <remarks>
    /// ストライドは <c>幅 × 4</c>（パディングなし）である。 ストライドを本来の値に固定することで、<br/>
    /// ピクセル形式を共有する他の実装（Skia の <c>RowBytes</c> も <c>幅 × 4</c>）とコピー手順が一致する。<br/>
    /// <b><paramref name="clear"/> = false は全領域を直後に書く場合だけ使う</b>（書かない領域は不定値のまま残る）。
    /// </remarks>
    private unsafe EsBitmap(int width, int height, bool clear = true)
    {
        Width = width;
        Height = height;
        Size = new EsSizeInt(width, height);
        Stride = width * BytesPerPixel;

        var byteCount = (long)Stride * height;
        if (byteCount == 0)
        {
            return;
        }

        _buffer = (nint)NativeMemory.AlignedAlloc((nuint)byteCount, AlignmentBytes);
        if (clear)
        {
            // 透明（全成分 0）で初期化する（未描画領域を「描かれていない」として扱うため）
            NativeMemory.Clear((void*)_buffer, (nuint)byteCount);
        }
    }

    // ====================================================================
    // 解放
    // ====================================================================

    /// <summary>ピクセルバッファを解放する。</summary>
    public unsafe void Dispose()
    {
        if (_disposed) return;

        if (_buffer != 0)
        {
            NativeMemory.AlignedFree((void*)_buffer);
            _buffer = 0;
        }

        _disposed = true;
    }

    /// <summary>破棄済みなら例外を投げる（拡張クラス・外部アセンブリからも利用）。</summary>
    public void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    // ====================================================================
    // 生成・変換
    // ====================================================================

    /// <summary>指定サイズの空のビットマップ（BGRA8888・全ピクセル透明）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <returns>生成されたビットマップ。</returns>
    public static EsBitmap Create(int width, int height)
        => new(width, height);

    /// <summary>指定矩形サイズの空のビットマップ（BGRA8888・全ピクセル透明）を生成する。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <returns>生成されたビットマップ。</returns>
    public static EsBitmap Create(EsSizeInt size)
        => Create(size.Width, size.Height);

    /// <summary>指定サイズのビットマップ（BGRA8888）を生成し、全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="color">塗りつぶし色。</param>
    /// <returns>生成されたビットマップ（全ピクセルが指定色）。</returns>
    /// <remarks>
    /// <see cref="Create(int, int)"/> で生成後に <see cref="Clear(Color)"/> を呼ぶのと等価。<br/>
    /// 生成と一括塗りつぶしを 1 回で行う（テスト・サンプルで多用する単色 Bitmap 生成を簡便にする）。
    /// </remarks>
    public static EsBitmap Create(int width, int height, Color color)
    {
        // 直後に全領域を塗るため、生成時の 0 埋めは行わない
        var bitmap = new EsBitmap(width, height, clear: false);
        bitmap.Clear(color);
        return bitmap;
    }

    /// <summary>指定矩形サイズのビットマップ（BGRA8888）を生成し、全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <param name="color">塗りつぶし色。</param>
    /// <returns>生成されたビットマップ（全ピクセルが指定色）。</returns>
    public static EsBitmap Create(EsSizeInt size, Color color)
        => Create(size.Width, size.Height, color);

    /// <summary>直後に全領域を書き込む前提でピクセルバッファを確保する（0 埋めをしない）。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <returns>生成されたビットマップ（ピクセルは不定値）。</returns>
    /// <remarks>
    /// 全領域を必ず書く手順（全行のコピー・全ピクセルの塗りつぶし）から使う（大きな画像の 0 埋めを丸ごと省ける）。<br/>
    /// 相互変換（System.Drawing / Avalonia / Skia）は全行を書くため本メソッドを使う。<br/>
    /// <b>書かない領域が残る場合は <see cref="Create(int, int)"/> を使うこと</b>（不定値が描画へ回り、
    /// 透明ではないピクセルが見える）。<b>書く責任は呼び出し側が持つ</b>。
    /// </remarks>
    public static EsBitmap CreateUninitialized(int width, int height) => new(width, height, clear: false);

    /// <summary>直後に全領域を書き込む前提でピクセルバッファを確保する（0 埋めをしない）。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <returns>生成されたビットマップ（ピクセルは不定値）。</returns>
    public static EsBitmap CreateUninitialized(EsSizeInt size) => new(size.Width, size.Height, clear: false);

    // ====================================================================
    // 基本プロパティ
    // ====================================================================

    /// <summary>幅（ピクセル）。</summary>
    public int Width { get; init; }

    /// <summary>高さ（ピクセル）。</summary>
    public int Height { get; init; }

    /// <summary>幅・高さ（ピクセル）。</summary>
    public EsSizeInt Size { get; init; }

    /// <summary>ピクセルデータ先頭アドレス（BGRA8888）。 ピクセルを持たない場合は 0。</summary>
    /// <value>確保したバッファの先頭アドレス（バッファは <see cref="Dispose"/> で解放する）。</value>
    public nint Address => _buffer;

    /// <summary>1 ラインあたりのバイト数（パディングなし。 常に <c>幅 × 4</c>）。</summary>
    public int Stride { get; init; }

    /// <summary>破棄済みかどうか。</summary>
    public bool IsDisposed => _disposed;

    // ====================================================================
    // 画像源（IEsImageSource）
    // ====================================================================

    /// <summary>コアの画像を取得する（自分自身を返す。 借用）。</summary>
    /// <returns>自分自身。<b>借用</b>（破棄しない。 所有者は呼び出し側のまま）。</returns>
    /// <remarks>
    /// <see cref="EsBitmap"/> 自身が画像源であるため、恒等（コピーなし）で返す。<br/>
    /// <b>明示的実装</b>にして公開面を増やさない（本型を直接使う呼び出し側は変換を必要としない）。<br/>
    /// 寸法の文字列化は <see cref="EsImageSourceText.ToSizeText"/> が単一所有する。
    /// </remarks>
    EsBitmap IEsImageSource.GetEsBitmap() => this;

    // ====================================================================
    // ピクセル操作
    // ====================================================================

    /// <summary>指定座標のピクセルを BGRA で取得する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <returns>(B, G, R, A) のタプル。 範囲外は IndexOutOfRangeException。</returns>
    public unsafe (byte B, byte G, byte R, byte A) GetPixel(int x, int y)
    {
        ThrowIfDisposed();
        var p = (byte*)Address + y * Stride + x * BytesPerPixel;
        return (p[0], p[1], p[2], p[3]);
    }

    /// <summary>指定座標のピクセルを取得する。</summary>
    /// <param name="point">座標（ピクセル）。</param>
    /// <returns>取得した色（<see cref="Color"/>）。 範囲外は IndexOutOfRangeException。</returns>
    public Color GetPixel(EsPointInt point)
    {
        var (b, g, r, a) = GetPixel(point.X, point.Y);
        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>指定座標のピクセルを BGRA で設定する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    public unsafe void SetPixel(int x, int y, byte b, byte g, byte r, byte a)
    {
        ThrowIfDisposed();
        var p = (byte*)Address + y * Stride + x * BytesPerPixel;
        p[0] = b;
        p[1] = g;
        p[2] = r;
        p[3] = a;
    }

    /// <summary>指定座標のピクセルを設定する。</summary>
    /// <param name="point">座標（ピクセル）。</param>
    /// <param name="color">設定する色。</param>
    public void SetPixel(EsPointInt point, Color color)
        => SetPixel(point.X, point.Y, color.B, color.G, color.R, color.A);

    /// <summary>指定座標のピクセルを色で設定する（範囲外は無視）。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="color">設定する色。</param>
    /// <returns>書き込めたかどうか。 範囲外の場合は false。</returns>
    /// <remarks>
    /// 描画（破線・矩形オーバーレイ等）で範囲外を無視したい場合に使用する。<br/>
    /// <see cref="SetPixel(int, int, byte, byte, byte, byte)"/> は範囲外で例外のため、<br/>
    /// 範囲外チェック（<c>x</c> / <c>y</c> が画像サイズ内かどうか）を行ってから委譲する。
    /// </remarks>
    public bool TrySetPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return false;
        }

        SetPixel(x, y, color.B, color.G, color.R, color.A);
        return true;
    }

    /// <summary>全ピクセルを指定色（BGRA）で塗りつぶす。</summary>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    /// <remarks>与えられた成分をそのまま書き込む（乗算済みへの変換は行わない）。</remarks>
    public unsafe void Clear(byte b, byte g, byte r, byte a)
    {
        ThrowIfDisposed();

        if (_buffer == 0)
        {
            return;
        }

        // BGRA の並びをそのまま uint として書き込む（成分の入れ替えをしない）
        var pattern = (uint)(b | (g << 8) | (r << 16) | (a << 24));
        // 要素数は幅×高さ（ストライドは幅×4 のため、ストライド経由で数えると桁が大きくなるだけ）
        new Span<uint>((void*)_buffer, Width * Height).Fill(pattern);
    }

    /// <summary>全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="color">塗りつぶし色。</param>
    public void Clear(Color color)
        => Clear(color.B, color.G, color.R, color.A);

    /// <summary>本ビットマップの複製を生成する。</summary>
    /// <returns>独立した複製（所有権は呼び出し側）。</returns>
    public unsafe EsBitmap Clone()
    {
        ThrowIfDisposed();

        // 全領域を 1 回のコピーで覆うため、生成時の 0 埋めは行わない
        var result = new EsBitmap(Width, Height, clear: false);
        var byteCount = (long)Stride * Height;
        if (byteCount > 0)
        {
            NativeMemory.Copy((void*)_buffer, (void*)result.Address, (nuint)byteCount);
        }

        return result;
    }

    /// <summary>画像から指定された矩形領域を切り出す。</summary>
    /// <param name="x">切り出し領域の左上X座標（画像ローカルピクセル）。 負値の場合はソース左端より外側が透明パディングとなる。</param>
    /// <param name="y">切り出し領域の左上Y座標（画像ローカルピクセル）。 負値の場合はソース上端より外側が透明パディングとなる。</param>
    /// <param name="width">切り出し領域の幅（ピクセル）。 正の値。</param>
    /// <param name="height">切り出し領域の高さ（ピクセル）。 正の値。</param>
    /// <returns>切り出された新しいビットマップ（要求サイズ。 ソース範囲外は透明パディング）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> / <paramref name="height"/> が0以下。</exception>
    /// <remarks>
    /// デスティネーションのコピーで覆われない領域は透明にするため、ソース範囲外は透明パディングとなる。<br/>
    /// 左上がソースの外（負）の場合も、要求矩形とソースの位置関係を保ってコピーする<br/>
    /// （内容を左上へ詰めない。 画像外の領域が透明のまま残る）。<br/>
    /// 矩形がソースと交差しない場合は全ピクセル透明のビットマップを返す。<br/>
    /// 内部ではピクセルバッファ（<see cref="Address"/>）へ直接アクセスし、行単位でコピーする<br/>
    /// （0 埋めは行わず、コピーされないパディングだけを透明にする）。<br/>
    /// 呼び出し側で戻り値を破棄すること。
    /// </remarks>
    public unsafe EsBitmap Crop(int x, int y, int width, int height)
    {
        ThrowIfDisposed();
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "切り出し領域の幅は正の値である必要があります。");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "切り出し領域の高さは正の値である必要があります。");
        }

        var srcW = Width;
        var srcH = Height;

        // ソース左端・上端より外側はデスティネーション側でも透明のまま残す（コピー開始位置をずらす）
        var dstOffsetX = Math.Max(0, -x);
        var dstOffsetY = Math.Max(0, -y);

        // ソース範囲内にクランプ（交差しない場合はコピー列数・行数が 0 以下となる）
        var cropX = Math.Max(0, x);
        var cropY = Math.Max(0, y);
        var cropW = Math.Min(width - dstOffsetX, srcW - cropX);
        var cropH = Math.Min(height - dstOffsetY, srcH - cropY);

        // ソースと交差しない場合はゼロ初期化された全ピクセル透明を返す
        if (cropW <= 0 || cropH <= 0)
        {
            return new EsBitmap(width, height);
        }

        var result = new EsBitmap(width, height, clear: false);
        var srcStride = Stride;
        var dstStride = result.Stride;
        var dst = (byte*)result.Address;

        // コピーする 1 行のバイト数（cropW は「デスティネーションの幅 − 左パディング」以下にクランプ済み）
        var copyBytes = cropW * BytesPerPixel;

        // 上端・下端のパディング行は連続しているため、行ごとではなく 1 回で消す
        var bottomRows = height - dstOffsetY - cropH;
        if (dstOffsetY > 0)
        {
            NativeMemory.Clear(dst, (nuint)(dstStride * dstOffsetY));
        }

        if (bottomRows > 0)
        {
            NativeMemory.Clear(dst + dstStride * (dstOffsetY + cropH), (nuint)(dstStride * bottomRows));
        }

        // 中間行の左右のパディングを消す（左右の幅は行ごとに同じ）
        var leftBytes = dstOffsetX * BytesPerPixel;
        var rightBytes = (width - dstOffsetX - cropW) * BytesPerPixel;
        if (leftBytes > 0 || rightBytes > 0)
        {
            for (var row = 0; row < cropH; row++)
            {
                var pRow = dst + dstStride * (dstOffsetY + row);
                if (leftBytes > 0)
                {
                    NativeMemory.Clear(pRow, (nuint)leftBytes);
                }

                if (rightBytes > 0)
                {
                    NativeMemory.Clear(pRow + leftBytes + copyBytes, (nuint)rightBytes);
                }
            }
        }

        // 行単位でコピーする（ポインタを進めるだけで、行ごとの乗算を行わない）
        var pSrc = (byte*)Address + srcStride * cropY + cropX * BytesPerPixel;
        var pDst = dst + dstStride * dstOffsetY + dstOffsetX * BytesPerPixel;

        for (var row = 0; row < cropH; row++)
        {
            NativeMemory.Copy(pSrc, pDst, (nuint)copyBytes);
            pSrc += srcStride;
            pDst += dstStride;
        }

        return result;
    }

    /// <summary>画像から指定された矩形領域を切り出す。</summary>
    /// <param name="rect">切り出し領域（画像ローカルピクセル）。</param>
    /// <returns>切り出された新しいビットマップ（要求サイズ。 ソース範囲外は透明パディング）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rect"/> の幅・高さが0以下。</exception>
    /// <remarks>
    /// デスティネーションのコピーで覆われない領域は透明にするため、ソース範囲外は透明パディングとなる。<br/>
    /// 矩形がソースと交差しない場合は全ピクセル透明のビットマップを返す。<br/>
    /// 呼び出し側で戻り値を破棄すること。
    /// </remarks>
    public EsBitmap Crop(EsRectInt rect)
        => Crop(rect.X, rect.Y, rect.Width, rect.Height);
}
