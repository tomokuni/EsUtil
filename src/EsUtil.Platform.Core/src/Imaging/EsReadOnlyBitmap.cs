using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;
using Color = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>読み取り専用の画像（書き込みの入口を持たない型。 画像を<b>受け取る</b>側の型）。</summary>
/// <remarks>
/// <b>目的</b>: 「この画像は読むだけである」ことを<b>型で示す</b>。 画素を書き換える入口を持たないため、
/// 受け取った側が誤って書き換えることがコンパイル時に決まる（実行時の判定に頼らない）。<br/>
/// <br/>
/// <b>書き換え可能な画像との関係</b>: <see cref="EsBitmap"/> は本型の<b>派生</b>であり、読み取りの面を受け継いだうえで
/// 書き込みの入口を足す（Avalonia の <c>Bitmap</c> / <c>WriteableBitmap</c> と同じ形）。 したがって
/// <b>書き換え可能な画像は本型としてそのまま渡せる</b>（変換もコピーも要らない）。 逆に本型から書き換え可能な画像を
/// 得るには <see cref="EsBitmap.Create(EsReadOnlyBitmap)"/>（<b>複製</b>）を使う。<br/>
/// <b>読み取り専用であって不変ではない</b>: 本型が保証するのは「<b>このハンドルからは書けない</b>」ことであり、
/// <b>内容が決して変わらないことではない</b>。 派生した書き換え可能な画像が同じ実体である場合や、
/// 借用した画素（<see cref="FromPixels"/>）の所有者が書く場合は、内容が変わりうる
/// （内容を固定したい場合は <see cref="SetReadOnly"/> で凍結する）。<br/>
/// <b>共有と寿命</b>: 同じ画素を指す複数の実体は<b>参照を数えて</b>解放する。 したがって
/// <b>先に元を破棄しても本型の画素は有効なまま</b>であり、最後の 1 つが解放する（<see cref="Clone"/> は複製せず共有する）。<br/>
/// <b>画素形式</b>: BGRA8888（アルファ乗算済み）。 ストライドは <c>幅 × <see cref="BytesPerPixel"/></c>（行パディングなし）。<br/>
/// <b>生ポインタを公開しない</b>: 渡すと「読み取り専用」の意図が消えるため、画素は <see cref="ReadOnlyPixels"/> で読む
/// （生ポインタが要る場合は <c>fixed</c> で取る。 領域はアンマネージドのため生存中はアドレスが動かない）。<br/>
/// <b>派生できる範囲</b>: コンストラクターが <c>private protected</c> のため、派生は本アセンブリの <see cref="EsBitmap"/> だけである。<br/>
/// <b>破棄済みの扱い</b>: 破棄済みの場合、画素の取得は <see cref="ObjectDisposedException"/> を投げる
/// （無効な画像の値を正常値として渡さない）。
/// </remarks>
public class EsReadOnlyBitmap : IDisposable
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    /// <value>4。 ストライドは常に <c>幅 × 本値</c> である。</value>
    /// <remarks>リテラルの定義は <see cref="EsBitmapBuffer"/> が単一所有し、本定数はその転送である
    /// （相互変換がストライドを計算するために使う）。</remarks>
    public const int BytesPerPixel = EsBitmapBuffer.BytesPerPixel;

    /// <summary>画素バッファ（共有した場合は参照を 1 つ保持する）。</summary>
    private readonly EsBitmapBuffer _buffer;

    /// <summary>画素バッファを受け取る（生成は本クラスの静的メソッドが行う）。</summary>
    /// <param name="buffer">画素バッファ。 破棄の責任は本インスタンスが持つ（共有した場合は自分の参照だけを返す）。</param>
    /// <remarks><c>private protected</c> にするのは、派生を本アセンブリの <see cref="EsBitmap"/> だけに限るためである
    /// （外部が独自の派生を作ると、書き込みの入口の有無を型で保証できなくなる。 外部が独自の画像を作る場合は
    /// 本型を継承せず、必要な機能を持つ独自の型を作る）。</remarks>
    private protected EsReadOnlyBitmap(EsBitmapBuffer buffer) => _buffer = buffer;

    /// <summary>画素バッファ（派生だけが使う。 公開しない）。</summary>
    /// <remarks>派生（<see cref="EsBitmap"/>）が書き込みの入口を実装するために使う。</remarks>
    private protected EsBitmapBuffer Buffer => _buffer;

    /// <summary>破棄済みかどうか。</summary>
    /// <remarks>破棄の<b>権利</b>は生成した側にあり、本値は「まだ使えるか」の情報である。</remarks>
    public bool IsDisposed => _buffer.IsDisposed;

    /// <summary>この実体が読み取り専用かどうか（本型は書き込みの入口を持たないため常に true）。</summary>
    /// <remarks>
    /// <b>本型では常に true</b>: 書き込みの入口を持たないため、実行時の状態に関わらず「書けない」。
    /// 派生（<see cref="EsBitmap"/>）は本値を上書きし、凍結の状態（<see cref="SetReadOnly"/> の後かどうか）を返す
    /// （書き込みの入口を持ちながら凍結されている場合があるため）。
    /// </remarks>
    public virtual bool IsReadOnly => true;

    /// <summary>幅（ピクセル）。</summary>
    public int Width => _buffer.Width;

    /// <summary>高さ（ピクセル）。</summary>
    public int Height => _buffer.Height;

    /// <summary>幅・高さ（ピクセル）。</summary>
    public EsSizeInt Size => _buffer.Size;

    /// <summary>1 ラインあたりのバイト数（パディングなし。 常に <c>幅 × <see cref="BytesPerPixel"/></c>）。</summary>
    public int Stride => _buffer.Stride;

    /// <summary>ピクセル データ（BGRA8888・アルファ乗算済み）の<b>読み取り専用ビュー</b>。</summary>
    /// <value>先頭から <c><see cref="Stride"/> × <see cref="Height"/></c> バイト。 ピクセルを持たない場合は空。</value>
    /// <remarks>コピーしない（同じ領域を指すビュー）。 生ポインタが要る場合は <c>fixed</c> で取る。</remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public ReadOnlySpan<byte> ReadOnlyPixels => _buffer.ReadOnlyPixels;

    /// <summary>保存に使える拡張子（小文字・先頭のドットを含む）。</summary>
    /// <value><c>.bmp</c> / <c>.png</c> / <c>.jpg</c> / <c>.jpeg</c> / <c>.gif</c> / <c>.tif</c> / <c>.tiff</c>。</value>
    /// <remarks>
    /// <b>Windows 専用の値</b>（実装は GDI+ のエンコーダーであり、<see cref="EsBitmapExtensions.Save(EsReadOnlyBitmap, string)"/> と同じ制約）。
    /// 形式の判定（拡張子 → エンコーダー）の単一所有は本一覧である。 <see cref="EsBitmap"/> も同じ値を公開する（どちらの権利の画像も保存できる）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<string> SupportedFileExtensions => WinGdiPlusImageCodec.SupportedExtensions;

    /// <summary>ファイルを読み込んで読み取り専用の画像を生成する。</summary>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。 null 不可。</param>
    /// <returns>読み込んだ画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>中間バッファを作らない</b>: デコードは確保した画素領域へ直接書く（実測: 4K PNG の読み込みで 29.9 ms。
    /// 中間バッファを挟むと +31 MiB と +2.7 ms ＝ +8%）。 0 埋めも省く（直後に全領域を書くため）。<br/>
    /// <b>書き換えが要る場合</b>は <see cref="EsBitmap.Load"/> を使う（本メソッドの結果から書き換え可能な画像を作るには複製が要る）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合。</exception>
    [SupportedOSPlatform("windows")]
    public static EsReadOnlyBitmap Load(string filePath)
        => EsBitmapLoader.Load(filePath, static buffer => new EsReadOnlyBitmap(buffer));

    /// <summary>指定サイズの画素を確保し、充填する処理を 1 回で行う（相互変換が使う入口）。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="fill">確保した画素（<c>幅 × 4 × 高さ</c> バイト）へ書く処理。 <b>全領域を書く責任を持つ</b>。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>0 埋めを省く</b>: 直後に全領域を書く前提のため、未初期化のまま確保する（画像サイズ分の書き込みを削減する）。<br/>
    /// <b><paramref name="fill"/> が例外を投げた場合は確保した領域を解放する</b>（呼び出し側に解放の機会が無いため）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="fill"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    public static EsReadOnlyBitmap CreateFilled(int width, int height, Action<Span<byte>> fill)
    {
        ArgumentNullException.ThrowIfNull(fill);

        var buffer = EsBitmapBuffer.Allocate(width, height, clear: false);
        try
        {
            fill(buffer.Pixels);
            return new EsReadOnlyBitmap(buffer);
        }
        catch
        {
            // 確保した領域を解放してから例外を伝える（呼び出し側に解放の機会が無い）
            buffer.Dispose();
            throw;
        }
    }

    /// <summary>呼び出し側が確保した画素を、複製せずに読み取り専用の画像として扱う（借用）。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="pixels">画素の先頭アドレス（BGRA8888。 <c>幅 × 4 × 高さ</c> バイト以上）。</param>
    /// <param name="release">解放時に 1 回だけ呼ぶ処理（アドレスを受け取る）。 解放が不要な場合は null。</param>
    /// <returns>借用した画像。 所有権は呼び出し側に残る（<paramref name="release"/> を渡した場合は本画像の破棄時に呼ばれる）。</returns>
    /// <remarks>
    /// <b>複製しない</b>: 4K の画像を複製すると 31 MiB と 5.7 ms を要するため、既に確保済みの画素を読むだけの場面で使う。<br/>
    /// <b>行パディングがある領域は借用できない</b>: 画素は連続した 1 本の領域として公開するため、ストライドは
    /// <c>幅 × <see cref="BytesPerPixel"/></c> に限る（それ以外は <see cref="ArgumentException"/>）。<br/>
    /// <b>借用中は呼び出し側が書き込んではいけない</b>（本ライブラリは内容の不変を保証しない。 借用は「その時点の画素を読む」入口である）。<br/>
    /// <b>複製する場合</b>は <see cref="FromPixelCopy"/> を使う（名前で区別する）。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> が 0 の場合（画素がある場合）。</exception>
    public static EsReadOnlyBitmap FromPixels(int width, int height, nint pixels, Action<nint>? release = null)
        => new(EsBitmapBuffer.Borrow(width, height, pixels, release));

    /// <summary>呼び出し側の画素を<b>複製して</b>読み取り専用の画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="pixels">複製元の画素（BGRA8888。 <c>幅 × 4 × 高さ</c> バイト）。</param>
    /// <returns>複製した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>複製した後に元の画素が変わっても本画像は変わらない（借用の <see cref="FromPixels"/> との違い）。</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> の長さが <c>幅 × 4 × 高さ</c> と一致しない場合。</exception>
    public static EsReadOnlyBitmap FromPixelCopy(int width, int height, ReadOnlySpan<byte> pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        var byteCount = (long)width * BytesPerPixel * height;
        if (pixels.Length != byteCount)
        {
            throw new ArgumentException($"画素の長さが寸法と一致しません（期待 {byteCount} バイト / 実際 {pixels.Length} バイト）。", nameof(pixels));
        }

        var buffer = EsBitmapBuffer.Allocate(width, height, clear: false);
        try
        {
            pixels.CopyTo(buffer.Pixels);
            return new EsReadOnlyBitmap(buffer);
        }
        catch
        {
            // 確保した領域を解放してから例外を伝える（呼び出し側に解放の機会が無い）
            buffer.Dispose();
            throw;
        }
    }

    /// <summary>本画像の複製を生成する（読み取り専用同士は互いに書き換えないため、<b>複製せず同じ画素を共有する</b>）。</summary>
    /// <returns>同じ画素を指す別の画像。 所有権は呼び出し側へ移る（先に元を破棄しても画素は有効なまま）。</returns>
    /// <remarks>派生（<see cref="EsBitmap"/>）は本メソッドを上書きし、<b>独立した画素を持つ複製</b>を返す
    /// （書き換え可能な画像同士が共有すると、片方の書き込みがもう片方の画素を変えるため）。</remarks>
    public virtual EsReadOnlyBitmap Clone() => new(_buffer.Share());

    /// <summary>本画像を凍結し、これ以降の書き込みを拒否する。</summary>
    /// <remarks>
    /// <b>凍結する理由</b>: 派生した書き換え可能な画像が同じ実体である場合や、本画像を複数の利用者が読む場合に、
    /// 後からの書き込みで読み手の画素が黙って変わることを防ぐ（<b>沈黙ではなく例外</b>にする）。<br/>
    /// <b>本型でも凍結できる理由</b>: 読み取り専用の画像をさらに読み取り専用にしても、与える権利は増えない
    /// （減る方向の操作であるため、型が違っても約束は破れない）。<br/>
    /// <b>画素そのものは不変にならない</b>: 同じ実体を持つ他のハンドル（派生した書き換え可能な画像など）からは
    /// 書き込める（本メソッドが止めるのは<see cref="IsReadOnly"/> を見る書き込みの入口だけである）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public void SetReadOnly()
    {
        ThrowIfDisposed();
        _buffer.SetReadOnly();
    }

    /// <summary>指定座標のピクセルを BGRA で取得する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <returns>(B, G, R, A) のタプル（アルファ乗算済み）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">座標が画像の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public (byte B, byte G, byte R, byte A) GetPixel(int x, int y) => _buffer.GetPixel(x, y);

    /// <summary>指定座標のピクセルを色として取得する（座標型を受け取る版）。</summary>
    /// <param name="point">座標（ピクセル）。</param>
    /// <returns>取得した色（<see cref="Color"/>。 成分はアルファ乗算済み）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">座標が画像の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public Color GetPixel(EsPointInt point)
    {
        var (b, g, r, a) = _buffer.GetPixel(point.X, point.Y);
        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>画像の寸法を「幅x高さ」形式の文字列で返す（ログ等への記録用）。</summary>
    /// <returns>寸法の文字列（例: <c>1920x1080</c>）。 ピクセルを持たない場合は <c>0x0</c>。</returns>
    /// <remarks>書式を本面が単一所有する（実装ごとに書式を持つと、同じ画像が経路によって違う文字列で記録される）。
    /// 未設定（null）の扱いは書式ではないため面は持ちず、記録する側が決める（例: <c>image?.ToSizeText() ?? "null"</c>）。</remarks>
    public string ToSizeText() => $"{Width}x{Height}";

    /// <summary>破棄済みなら <see cref="ObjectDisposedException"/> を送出する。</summary>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public void ThrowIfDisposed() => _buffer.ThrowIfDisposed();

    /// <summary>画素領域を解放する（冪等）。</summary>
    /// <remarks>
    /// <b>共有している場合は自分の参照だけを返す</b>: 実体の解放は最後の 1 つが行う。
    /// 解放の実装は <see cref="SafeAlignedBuffer"/>（確保した場合）と <see cref="SafeBorrowedBuffer"/>（借用した場合）が単一所有する。
    /// </remarks>
    public void Dispose()
    {
        _buffer.Dispose();
        GC.SuppressFinalize(this);
    }
}
