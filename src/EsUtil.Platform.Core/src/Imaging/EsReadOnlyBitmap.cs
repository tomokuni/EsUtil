using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;
using Color = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>読み取り専用の画像（画素を書き換える入口を持たない型。 画像を<b>受け取る・保持する</b>側の型）。</summary>
/// <remarks>
/// <b>目的</b>: 「この画像は読むだけである」ことを<b>型で示す</b>。 画素を書き換える入口を持たないため、
/// 受け取った側が誤って書き換えることがコンパイル時に決まる（実行時の判定に頼らない）。<br/>
/// <br/>
/// <b>書き換え可能な画像（<see cref="EsBitmap"/>）とは兄弟です</b>: 互いの派生ではなく、<b>互いに変換できません</b>。
/// したがって<b>未凍結の画素を読み取り専用として渡すことはできません</b>（この型は「画素が変わらない」ことを前提に使える）。
/// 書き換え可能な画像から本型を得る唯一の入口は <see cref="FromBitmap"/> であり、<b>凍結済みなら 0 コピーで共有・
/// 未凍結なら複製</b>します（実測: Skia の <c>SKImage.FromBitmap</c> と同じ規則）。<br/>
/// <b>借用は「この実体から書けない」ことだけを約束します</b>: 呼び出し側が確保したメモリを <see cref="InstallPixels"/> で
/// 借用した場合、所有者がそのメモリを書き換えれば内容は変わります（借用は「その時点の画素を読む入口」です）。<br/>
/// <b>寿命と共有</b>: 同じ画素を指す複数の実体は<b>参照を数えて</b>解放します。 したがって
/// <b>先に元を破棄しても画素は有効なまま</b>であり、最後の 1 つが解放します（画素を共有する入口は
/// <see cref="FromBitmap"/>（凍結済みの画像）と <see cref="ToPixmap"/>（読み取りビュー）です）。<br/>
/// <b>画素形式</b>: BGRA8888（アルファ乗算済み）。 ストライドは <c>幅 × <see cref="BytesPerPixel"/></c>（行パディングなし）。<br/>
/// <b>生ポインタを公開しない</b>: 渡すと「読み取り専用」の意図が消えるため、画素は <see cref="ReadOnlyPixels"/> で読む
/// （生ポインタが要る場合は <c>fixed</c> で取る。 領域はアンマネージドのため生存中はアドレスが動かない）。<br/>
/// <b>破棄済みの扱い</b>: 破棄済みの場合、画素の取得は <see cref="ObjectDisposedException"/> を投げる
/// （無効な画像の値を正常値として渡さない）。
/// </remarks>
public sealed class EsReadOnlyBitmap : IDisposable
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    /// <value>4。 ストライドは常に <c>幅 × 本値</c> である。</value>
    /// <remarks>リテラルの定義は内部のバッファ型（<see cref="EsBitmapBuffer"/>）が単一所有し、本定数はその転送である。</remarks>
    public const int BytesPerPixel = EsBitmapBuffer.BytesPerPixel;

    /// <summary>画素バッファ（確保・借用・共有・解放を単一所有する内部型）。</summary>
    /// <remarks>
    /// <b>継承ではなく保有にする理由</b>: 公開型は内部の型から派生できません（<c>CS0060</c>。 基底が非公開だと
    /// 派生の公開面が保てない）。 読み取り専用と書き換え可能は<b>兄弟</b>であり、実装の共有は<b>内部のバッファ型</b>が担います。
    /// </remarks>
    private readonly EsBitmapBuffer _buffer;

    /// <summary>画素バッファを受け取る（生成は本クラスの静的メソッドと <see cref="EsBitmap.Load"/> 系が行う）。</summary>
    /// <param name="buffer">画素バッファ。 破棄の責任は本インスタンスが持つ。</param>
    /// <remarks><c>internal</c> にするのは、生成を本アセンブリへ限るためである
    /// （外部が任意のバッファで実体を作れると、画素の整合を保証できない）。</remarks>
    internal EsReadOnlyBitmap(EsBitmapBuffer buffer) => _buffer = buffer;

    /// <summary>破棄済みかどうか。</summary>
    /// <remarks>破棄の<b>権利</b>は生成した側にあり、本値は「まだ使えるか」の情報である。</remarks>
    public bool IsDisposed => _buffer.IsDisposed;

    /// <summary>この実体が読み取り専用かどうか（書き込みの入口を持たないため常に true）。</summary>
    /// <remarks>書き換え可能な画像（<see cref="EsBitmap"/>）は凍結の状態を返すため、<b>両方の型で同じ名前が使えます</b>
    /// （ログや共通処理が型を区別せずに「凍結されているか」を扱える）。</remarks>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "書き換え可能な画像（EsBitmap）と同じ名前のインスタンス メンバーとして公開する（型を区別せずに扱えるようにするため）。")]
    public bool IsReadOnly => true;

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
    /// 形式の判定（拡張子 → エンコーダー）の単一所有は本一覧である。 <see cref="EsBitmap"/> も同じ値を公開する。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<string> SupportedFileExtensions => WinGdiPlusImageCodec.SupportedExtensions;

    /// <summary>ファイルを読み込んで読み取り専用の画像を生成する。</summary>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。 null 不可。</param>
    /// <returns>読み込んだ画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>中間バッファを作らない</b>: デコードは確保した画素領域へ直接書く（実測: 4K PNG の読み込みで 29.9 ms。
    /// 中間バッファを挟むと +31 MiB と +2.7 ms ＝ +8%）。 0 埋めも省く（直後に全領域を書くため）。<br/>
    /// <b>書き換えが要る場合</b>は <see cref="EsBitmap.Load"/> を使う。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合。</exception>
    [SupportedOSPlatform("windows")]
    public static EsReadOnlyBitmap Load(string filePath)
        => EsBitmapLoader.Load(filePath, static buffer => new EsReadOnlyBitmap(buffer));

    /// <summary>指定サイズの空の画像（BGRA8888・全ピクセル透明）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <returns>生成された画像（全ピクセル透明）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>書き込みの入口が無い型でも生成できるようにしています</b>（<see cref="EsBitmap"/> と入口を揃えるため。
    /// 片方だけ別の書き方になると、変換を読むたびに「なぜ違うのか」を調べることになります）。<br/>
    /// <b>用途</b>: 透明のまま置く画像（プレースホルダ）や、<see cref="CreateFilled"/> と組み合わせた生成です。
    /// 生成した画像へ後から書き込む場合は <see cref="EsBitmap.Create(int, int)"/> を使います。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    public static EsReadOnlyBitmap Create(int width, int height)
    {
        var buffer = EsBitmapBuffer.Allocate(width, height, clear: true);
        return new EsReadOnlyBitmap(buffer);
    }

    /// <summary>指定サイズの空の画像（全ピクセル透明）を生成する（サイズ型を受け取る版）。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    public static EsReadOnlyBitmap Create(EsSizeInt size) => Create(size.Width, size.Height);

    /// <summary>指定サイズの画像を生成し、全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="color">塗りつぶし色。</param>
    /// <returns>生成された画像（全ピクセルが指定色）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>色の成分はそのまま書き込む（乗算済みへの変換は行わない）。 0 埋めは行わない（全領域を書くため）。</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    public static EsReadOnlyBitmap Create(int width, int height, Color color)
    {
        var buffer = EsBitmapBuffer.Allocate(width, height, clear: false);
        try
        {
            buffer.Clear(color.B, color.G, color.R, color.A);
            return new EsReadOnlyBitmap(buffer);
        }
        catch
        {
            // 確保した領域を解放してから例外を伝える（呼び出し側に解放の機会が無い）
            buffer.Dispose();
            throw;
        }
    }

    /// <summary>指定サイズの画像を生成し、全ピクセルを指定色で塗りつぶす（サイズ型を受け取る版）。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <param name="color">塗りつぶし色。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    public static EsReadOnlyBitmap Create(EsSizeInt size, Color color) => Create(size.Width, size.Height, color);

    /// <summary>指定サイズの画素を確保し、充填する処理を 1 回で行う（相互変換が使う入口）。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="fill">確保した画素（<c>幅 × 4 × 高さ</c> バイト）へ書く処理。 <b>全領域を書く責任を持つ</b>。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>0 埋めを省く</b>: 直後に全領域を書く前提のため、未初期化のまま確保する（画像サイズ分の書き込みを削減する）。<br/>
    /// <b><paramref name="fill"/> が例外を投げた場合は確保した領域を解放する</b>（呼び出し側に解放の機会が無いため）。<br/>
    /// <b><see cref="EsBitmap"/> にも同じ入口がある</b>（相互変換はどちらの権利の画像も作るため。 対にして覚える負担を減らす）。
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
    /// <b>内容の不変は保証しない</b>: 所有者がそのメモリを書き換えれば内容は変わります（借用は「その時点の画素を読む入口」です）。
    /// <b>本ライブラリの画像のアドレスを渡し直さないこと</b>（凍結や読み取り専用の約束を迂回できてしまいます）。<br/>
    /// <b>複製する場合</b>は <see cref="FromPixelCopy"/> を使う（名前で区別する）。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> が 0 の場合（画素がある場合）。</exception>
    public static EsReadOnlyBitmap InstallPixels(int width, int height, nint pixels, Action<nint>? release = null)
        => new(EsBitmapBuffer.Borrow(width, height, pixels, release));

    /// <summary>呼び出し側の画素を<b>複製して</b>読み取り専用の画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="pixels">複製元の画素（BGRA8888。 <c>幅 × 4 × 高さ</c> バイト）。</param>
    /// <returns>複製した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>複製した後に元の画素が変わっても本画像は変わらない（借用の <see cref="InstallPixels"/> との違い）。</remarks>
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

    /// <summary>0 コピーの読み取りビューから画像を生成する（<b>常に複製</b>。 行幅を考慮する）。</summary>
    /// <param name="source">複製元のビュー。 借用であり破棄しない。</param>
    /// <returns>複製した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>ビューの行幅（<see cref="EsPixmap.Stride"/>）を考慮して行ごとに写します</b>: ビューは元画像のストライドを
    /// 保つため（<c>幅 × 4</c> より大きい場合があります）、領域全体を一度に写すと行がずれます（
    /// <see cref="FromPixelCopy"/> にビューの <c>ReadOnlyPixels</c> をそのまま渡すと長さが合わず
    /// <see cref="ArgumentException"/> になります）。<br/>
    /// <b>0 コピーで共有する方法はありません</b>: ビューの行幅は元画像のそれであり、本ライブラリの画像は
    /// 行幅が <c>幅 × 4</c> に固定されているためです（連続した 1 本の領域として扱う前提を保つため）。
    /// ビューのまま読む（<see cref="EsPixmap.GetPixel(int, int)"/>）か、複製して画像にします。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    public static EsReadOnlyBitmap FromPixmap(EsPixmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.ThrowIfDisposed();

        var buffer = EsBitmapBuffer.Allocate(source.Width, source.Height, clear: false);
        try
        {
            source.CopyTo(buffer);
            return new EsReadOnlyBitmap(buffer);
        }
        catch
        {
            // 確保した領域を解放してから例外を伝える（呼び出し側に解放の機会が無い）
            buffer.Dispose();
            throw;
        }
    }

    /// <summary>書き換え可能な画像から読み取り専用の画像を生成する（<b>凍結済みなら 0 コピー、未凍結なら複製</b>）。</summary>
    /// <param name="source">元の画像。 借用であり破棄しない。</param>
    /// <returns>読み取り専用の画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>書き換え可能な画像から本型を得る唯一の入口</b>です（実測: Skia の <c>SKImage.FromBitmap</c> と同じ規則。
    /// 凍結していない <c>SKBitmap</c> からは複製し、凍結済みなら共有します）。<br/>
    /// <b>一覧</b>:
    /// <list type="bullet">
    /// <item><description><paramref name="source"/> が凍結済み（<see cref="EsBitmap.IsReadOnly"/> が true）: <b>0 コピーで共有</b>
    /// （誰も書けないため、内容は変わらない）。</description></item>
    /// <item><description>未凍結: <b>複製</b>した読み取り専用の画像を返す（元は書き換え可能なまま影響を受けず、返した画像も
    /// 元の書き込みで変わらない）。</description></item>
    /// </list>
    /// <b>複製の費用</b>: 実測で 4K 1 枚あたり 31 MiB と 5.7 ms です。 払いたくない場合は、書き終えた時点で
    /// 書き換え可能な型の <c>SetReadOnly</c> を呼んでから本メソッドを呼びます。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    public static EsReadOnlyBitmap FromBitmap(EsBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.ThrowIfDisposed();

        // 凍結済みなら共有（0 コピー）、未凍結なら複製する（読み手の画素が後から変わらないようにする）
        return source.IsReadOnly
            ? new EsReadOnlyBitmap(source.Buffer.Share())
            : FromPixelCopy(source.Width, source.Height, source.ReadOnlyPixels);
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

    /// <summary>本画像の複製（<b>独立した画素</b>）を生成する。</summary>
    /// <returns>独立した複製。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>常に複製します</b>（読み取り専用同士でも画素を共有しません）。 実測: 4K で 31 MiB と 5.7 ms。<br/>
    /// <b>0 コピーが要る場合</b>は複製ではなく <see cref="ToPixmap"/>（読み取りビュー。 元の寿命に依存）を、
    /// 書き換え可能な画像を凍結して共有したい場合は <see cref="FromBitmap"/> を使います。<br/>
    /// <b>名前を <c>Clone</c> にしない理由</b>: <c>Clone</c> は「安い複製」を期待させますが、本メソッドは全域コピーです。
    /// 複製は費用が名前から読める <c>Copy</c>、共有は別名（<see cref="ToPixmap"/> / <see cref="FromBitmap"/>）に分けます
    /// （実測: Skia も <c>SKBitmap.Copy</c> が複製、共有は <c>SKImage.FromBitmap</c> と名前が別です）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public EsReadOnlyBitmap Copy() => FromPixelCopy(Width, Height, ReadOnlyPixels);

    /// <summary>本画像の一部を指す<b>0 コピーの読み取りビュー</b>を生成する（切り出しの複製を避ける）。</summary>
    /// <param name="rect">ビューの範囲（元画像の座標。 <b>画像の内側であること</b>）。</param>
    /// <returns>ビュー。 所有権は呼び出し側へ移る（破棄すると元画像への参照を返す。 画素は破棄しない）。</returns>
    /// <remarks>
    /// <b>複製しません</b>: 実測では 4K から 1000x800 の切り出しが、複製の <c>Crop</c> で 0.72 ms / 4 MB に対し、
    /// ビューではアドレス計算だけです（Skia の <c>SKBitmap.ExtractSubset</c> は 0.000 ms で、元と双方向に
    /// 書き込みが伝わるビューであることを実測で確認）。<br/>
    /// <b>行幅（<see cref="EsPixmap.Stride"/>）は元画像のストライドのまま</b>です（実測: Skia のビューも
    /// 元の行幅を保つため、行ごとに余白を持ちます）。 したがって <see cref="EsPixmap.ReadOnlyPixels"/> は
    /// <b>行の余白を含む</b>連続したビューであり、画素を読む場合は <see cref="EsPixmap.GetPixel(int, int)"/> を使います。<br/>
    /// <b>範囲は画像の内側に限ります</b>: ビューは透明で埋めることができないためです（画像の外側を含む切り出しが
    /// 必要な場合は、透明で埋める <see cref="EsBitmapExtensions.Crop(EsReadOnlyBitmap, EsRectInt)"/> を使います）。<br/>
    /// <b>元画像の寿命に依存しません</b>: ビューは元の画素の参照を 1 つ持つため、<b>先に元を破棄してもビューは読めます</b>
    /// （画素の解放は最後の 1 つが行います）。 ただし<b>元の内容の変化は見えます</b>（ビューは同じ画素を指すため）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rect"/> が空、または画像の範囲外に出る場合。</exception>
    public EsPixmap ToPixmap(EsRectInt rect)
    {
        ThrowIfDisposed();
        return _buffer.ToPixmap(rect);
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
