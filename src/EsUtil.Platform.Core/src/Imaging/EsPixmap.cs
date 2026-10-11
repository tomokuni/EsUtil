using EsUtil.Platform.Core.Geometry;
using Color = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像の一部を指す<b>0 コピーの読み取りビュー</b>（行幅を持つ）。</summary>
/// <remarks>
/// <b>目的</b>: 切り出しの<b>複製を避ける</b>ためです。 実測では 4K から 1000x800 の切り出しが、複製の
/// <see cref="EsReadOnlyBitmap.Crop(EsRectInt)"/> で 0.72 ms / 4 MB に対し、ビューではアドレス計算だけです
/// （実測: Skia の <c>SKBitmap.ExtractSubset</c> は 0.000 ms で、元と双方向に書き込みが伝わるビューでした）。<br/>
/// <b>生成</b>: <see cref="EsReadOnlyBitmap.ToPixmap"/> が唯一の入口です（ビューは画像の一部を指すため、
/// 画素を所有する型から作ります）。<br/>
/// <b>行幅（<see cref="Stride"/>）は元画像のストライドのまま</b>です（実測: Skia のビューも元の行幅を保ち、
/// 行ごとに余白を持ちます）。 したがって <see cref="ReadOnlyPixels"/> は<b>行の余白を含む</b>連続した
/// ビューであり、画素を読む場合は <see cref="GetPixel(int, int)"/> を使います。<br/>
/// <b>読み取り専用です</b>: 書き込みの入口を持ちません（元の画像を書き換える場合は
/// `EsBitmap.FromReadOnlyBitmap` で複製するか、元の書き換え可能な画像を使います）。<br/>
/// <b>寿命</b>: ビューは元の画素の参照を 1 つ持つため、<b>先に元を破棄しても読めます</b>（画素の解放は最後の 1 つ）。
/// ただし<b>元の内容の変化は見えます</b>（同じ画素を指すため）。<br/>
/// <b>範囲は元画像の内側に限ります</b>: ビューは透明で埋められないためです（外側を含む切り出しは
/// <see cref="EsReadOnlyBitmap.Crop(EsRectInt)"/> を使います）。
/// </remarks>
public sealed class EsPixmap : IDisposable
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    /// <remarks>リテラルの定義は内部のバッファ型（<see cref="EsBitmapBuffer"/>）が単一所有し、本定数はその転送である。</remarks>
    public const int BytesPerPixel = EsBitmapBuffer.BytesPerPixel;

    /// <summary>元の画素の参照（ビューが読めるのはこの参照がある間）。</summary>
    private readonly EsBitmapBuffer _buffer;

    /// <summary>ビューの先頭の、元の画素の先頭からのバイト数。</summary>
    private readonly long _offsetBytes;

    /// <summary>ビューの先頭アドレス（生成時に 1 度だけ求める）。</summary>
    /// <remarks>
    /// <b>保持する理由（実測）</b>: <see cref="GetPixel(int, int)"/> は画素ごとに呼ばれるため、そのたびに
    /// 元のバッファのアドレスを求めてオフセットを足すと費用がかさみます（実測: 10 万画素の読み取りで 1.40 ms
    /// → 1.29 ms）。
    /// </remarks>
    private readonly nint _address;

    /// <summary>ビューの全バイト数（<c>Stride × Height</c>。 生成時に 1 度だけ求める）。</summary>
    private readonly int _byteCount;

    private bool _disposed;

    /// <summary>ビューを生成する（生成の入口は <see cref="EsReadOnlyBitmap.ToPixmap"/>）。</summary>
    /// <param name="buffer">元の画素のバッファ（<b>共有した参照</b>を渡す。 破棄は本ビューが行う）。</param>
    /// <param name="offsetBytes">ビューの先頭の、元の画素の先頭からのバイト数。</param>
    /// <param name="width">幅（ピクセル）。 1 以上。</param>
    /// <param name="height">高さ（ピクセル）。 1 以上。</param>
    /// <param name="rowBytes">1 ラインあたりのバイト数（元画像のストライド）。</param>
    internal EsPixmap(EsBitmapBuffer buffer, long offsetBytes, int width, int height, int rowBytes)
    {
        _buffer = buffer;
        _offsetBytes = offsetBytes;
        _address = buffer.Address + (nint)offsetBytes;
        _byteCount = rowBytes * height;
        Width = width;
        Height = height;
        Stride = rowBytes;
    }

    /// <summary>破棄済みかどうか。</summary>
    public bool IsDisposed => _disposed;

    /// <summary>幅（ピクセル）。</summary>
    public int Width { get; }

    /// <summary>高さ（ピクセル）。</summary>
    public int Height { get; }

    /// <summary>幅・高さ（ピクセル）。</summary>
    public EsSizeInt Size => new(Width, Height);

    /// <summary>1 ラインあたりのバイト数（<b>元画像のストライドのまま</b>。 <c>幅 × 4</c> より大きい場合があります）。</summary>
    /// <remarks><b>名前を画像と同じ <c>Stride</c> にする理由</b>: 「1 ラインあたりのバイト数」は同じ概念であり、語彙を 2 つに増やさないためです（実測: Skia は画像とビューの両方で同じ名前 <c>RowBytes</c> を使っています）。 ビューは元の行幅を保つため、行ごとに余白を持ちます。</remarks>
    public int Stride { get; }

    /// <summary>ピクセル データの読み取り専用ビュー（<b>行の余白を含む</b>）。</summary>
    /// <value>先頭から <c><see cref="Stride"/> × <see cref="Height"/></c> バイト。</value>
    /// <remarks>
    /// <b>行の余白を含みます</b>（<see cref="Stride"/> が <c><see cref="Width"/> × 4</c> より大きいため）。
    /// 画素を読む場合は <see cref="GetPixel(int, int)"/> を使います（Skia の <c>SKPixmap</c> と同じ性質です）。<br/>
    /// 位置関係の情報が要る場合（他フレームワークの非所有ビューへ渡す等）は、本ビューと <see cref="Stride"/> を
    /// そのまま渡します。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public unsafe ReadOnlySpan<byte> ReadOnlyPixels
    {
        get
        {
            ThrowIfDisposed();
            var address = Address;
            return address == 0 ? default : new ReadOnlySpan<byte>((void*)address, _byteCount);
        }
    }

    /// <summary>指定座標のピクセルを BGRA で取得する（<b>行幅を考慮する</b>）。</summary>
    /// <param name="x">X 座標（ピクセル。 ビューの左上が 0）。</param>
    /// <param name="y">Y 座標（ピクセル。 ビューの左上が 0）。</param>
    /// <returns>(B, G, R, A) のタプル（アルファ乗算済み）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">座標がビューの範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public unsafe (byte B, byte G, byte R, byte A) GetPixel(int x, int y)
    {
        ThrowIfDisposed();
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, $"X 座標がビューの範囲外です（幅 {Width}）。");
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Y 座標がビューの範囲外です（高さ {Height}）。");
        }

        var p = (byte*)Address + (y * Stride) + (x * BytesPerPixel);
        return (p[0], p[1], p[2], p[3]);
    }

    /// <summary>指定座標のピクセルを色として取得する（座標型を受け取る版）。</summary>
    /// <param name="point">座標（ピクセル。 ビューの左上が 0）。</param>
    /// <returns>取得した色（<see cref="Color"/>。 成分はアルファ乗算済み）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">座標がビューの範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public Color GetPixel(EsPointInt point)
    {
        var (b, g, r, a) = GetPixel(point.X, point.Y);
        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>本ビューを長辺が上限に収まるよう縮小した画像を生成する（<b>切り出しの実体化を挟まない</b>）。</summary>
    /// <param name="maxSize">長辺の上限（ピクセル）。 正の値で、長辺以下。</param>
    /// <param name="algorithm">縮小のアルゴリズム。 既定は <see cref="DownscaleAlgorithm.Bilinear"/>。</param>
    /// <returns>縮小した画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>用途</b>: 大きな画像の一部だけを縮小したい場面（一覧のサムネイル・プレビューの一部）。 画像型の
    /// <see cref="EsReadOnlyBitmap.Resize(int, DownscaleAlgorithm)"/> を先に呼ぶと、<b>切り出しの複製
    /// （実測: 4K から 1000x800 で 0.85 ms / 4 MB）を払ってから縮小する</b>ことになります。 本メソッドは
    /// ビュー（0 コピー）をそのまま読むため、<b>その 0.85 ms / 4 MB が要りません</b>
    /// （実測: 1000x800 の切り出し → 長辺 320 の面積平均で 1.195 ms に対し、本メソッド相当で 0.363 ms ＝ <b>3.3 倍</b>）。<br/>
    /// <b>代金</b>: ビューの行幅（<see cref="Stride"/>。 元画像のストライド）のまま読むため、<b>行ごとに余白を飛ばす</b>
    /// 分だけ余計にアドレスを計算します（密な画像から切り出した画像を縮小する場合との差）。
    /// アルゴリズムの選び方と規則は画像型と同じです（<see cref="EsReadOnlyBitmap.Resize(int, DownscaleAlgorithm)"/>）。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSize"/> が 0 以下の場合、または
    /// <paramref name="algorithm"/> が定義されていない値の場合。</exception>
    /// <exception cref="NotSupportedException"><paramref name="maxSize"/> が長辺より大きい（＝拡大が必要な）場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public EsBitmap Resize(int maxSize, DownscaleAlgorithm algorithm = DownscaleAlgorithm.Bilinear)
    {
        ThrowIfDisposed();
        return EsBitmapOperations.Resize(ReadOnlyPixels, Width, Height, Stride, maxSize, algorithm);
    }

    /// <summary>本ビューを指定の色数へ減色した画像を生成する（<b>切り出しの実体化を挟まない</b>）。</summary>
    /// <param name="colorCount">残す色数（2〜256）。</param>
    /// <returns>減色した新しい画像（使われる色は <paramref name="colorCount"/> 種類以下）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>用途</b>: 大きな画像の一部だけを減色したい場面。 画像型の <see cref="EsReadOnlyBitmap.ReduceColors(int)"/> を
    /// 先に呼ぶと切り出しの複製を払うことになります（実測: 4K から 1000x800 で 0.85 ms / 4 MB）。
    /// 本メソッドはビューをそのまま読むため、その分が要りません（実測: 1000x800 の減色 256 色で 6.0 ms のうち
    /// 切り出しの 0.85 ms ＝ <b>約 12%</b>）。<br/>
    /// 減色の内容（アルファの扱い・ディザを行わない理由・パレットの作り方）は画像型と同じです
    /// （<see cref="EsReadOnlyBitmap.ReduceColors(int)"/> の備考）。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="colorCount"/> が 2〜256 の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public EsBitmap ReduceColors(int colorCount)
    {
        ThrowIfDisposed();
        return EsBitmapOperations.ReduceColors(ReadOnlyPixels, Width, Height, Stride, colorCount);
    }

    /// <summary>本ビューの一部を指す別のビューを生成する（さらに 0 コピーで切り出す）。</summary>
    /// <param name="rect">切り出す範囲（<b>本ビューの座標</b>。 本ビューの内側であること）。</param>
    /// <returns>切り出したビュー。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rect"/> が空、または本ビューの範囲外に出る場合。</exception>
    public EsPixmap ToPixmap(EsRectInt rect)
    {
        ThrowIfDisposed();

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rect), rect, "ビューの幅と高さは 1 以上である必要があります。");
        }

        if (rect.X < 0 || rect.Y < 0 || rect.Right > Width || rect.Bottom > Height)
        {
            throw new ArgumentOutOfRangeException(nameof(rect), rect, $"ビューの範囲が外に出ています（本ビュー {Size.Width}x{Size.Height}）。");
        }

        var offset = _offsetBytes + (rect.Y * Stride) + (rect.X * BytesPerPixel);
        return new EsPixmap(_buffer.Share(), offset, rect.Width, rect.Height, Stride);
    }

    /// <summary>ビューの寸法を「幅x高さ」形式の文字列で返す（ログ等への記録用）。</summary>
    /// <returns>寸法の文字列（例: <c>1000x800</c>）。</returns>
    public string ToSizeText() => $"{Width}x{Height}";

    /// <summary>破棄済みなら <see cref="ObjectDisposedException"/> を送出する。</summary>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, typeof(EsPixmap));

    /// <summary>元の画素への参照を返す（<b>画素は破棄しません</b>。 最後の 1 つが解放します）。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _buffer.Dispose();
    }

    /// <summary>ビューの先頭アドレス（破棄後は 0）。</summary>
    /// <remarks>
    /// 本アセンブリの中だけで使う（公開しない。 生ポインタを渡すと読み取り専用の意図が消えるため）。<br/>
    /// アドレスは生成時に 1 度だけ求める（<see cref="GetPixel(int, int)"/> を画素ごとに呼んでも再計算しない）。
    /// </remarks>
    internal nint Address => _disposed ? 0 : _address;

    /// <summary>本ビューの画素を、同じ寸法の別のバッファへ複製する（<b>行幅を考慮する</b>）。</summary>
    /// <param name="destination">複製先のバッファ（同じ寸法であること）。</param>
    /// <remarks>
    /// <b>行ごとに写す理由</b>: ビューの行幅は元画像のストライドのままであるため（<c>幅 × 4</c> より大きい場合が
    /// あります）、領域全体を一度に写すと行がずれます（余白の画素が混ざります）。<br/>
    /// <b>用途</b>: ビューから画像を作る入口（<see cref="EsBitmap.FromPixmap"/> /
    /// <see cref="EsReadOnlyBitmap.FromPixmap"/>）が使います。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> が null の場合。</exception>
    /// <exception cref="ArgumentException">寸法が一致しない場合。</exception>
    /// <exception cref="InvalidOperationException">複製先が読み取り専用の場合。</exception>
    internal void CopyTo(EsBitmapBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ThrowIfDisposed();

        if (destination.Width != Width || destination.Height != Height)
        {
            throw new ArgumentException($"複製先の寸法が一致しません（ビュー {Width}x{Height} / 先 {destination.Width}x{destination.Height}）。", nameof(destination));
        }

        var pixels = destination.Pixels;
        var rowBytes = Width * BytesPerPixel;
        for (var y = 0; y < Height; y++)
        {
            // ビューの行（先頭 y * Stride）から、複製先の行（先頭 y * destination.Stride）へ写す
            ReadOnlyPixels.Slice(y * Stride, rowBytes).CopyTo(pixels.Slice(y * destination.Stride, rowBytes));
        }
    }
}
