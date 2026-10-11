using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;
using Color = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>書き換え可能な画像（生成した側が所有し、画素を編集する型）。</summary>
/// <remarks>
/// <b>目的</b>: 「この画像は書き換えてよく、破棄する責任が自分にある」ことを<b>型で示す</b>。
/// 生成（<see cref="Create(int, int)"/>）と相互変換の結果が本型になり、<c>using</c> で決定的に解放できる。<br/>
/// <br/>
/// <b>読み取り専用の画像（<see cref="EsReadOnlyBitmap"/>）とは兄弟です</b>: 互いの派生ではなく、<b>互いに変換できません</b>。
/// 読み取り専用として渡すには <see cref="EsReadOnlyBitmap.FromBitmap"/>（<b>凍結済みなら 0 コピー・未凍結なら複製</b>）、
/// 読み取り専用の画像から書き換え可能な画像を作るには <see cref="FromReadOnlyBitmap"/>（<b>複製</b>）を使います。
/// <b>暗黙の変換はありません</b>（未凍結の画素が「変わらない」ものとして渡ることを型で防ぐため）。<br/>
/// <b>凍結（<see cref="SetReadOnly"/>）</b>: 呼ぶと以降の書き込みの入口（<see cref="Pixels"/> / <see cref="SetPixel(int, int, byte, byte, byte, byte)"/> /
/// <see cref="TrySetPixel(int, int, Color)"/> / <see cref="Clear(byte, byte, byte, byte)"/>）が <see cref="InvalidOperationException"/> を投げます。
/// 凍結は<b>画素領域</b>に効くため、<see cref="EsReadOnlyBitmap.FromBitmap"/> が 0 コピーで共有できるようになります
/// （書き終えた時点で凍結するのが規則です）。<br/>
/// <b>借用</b>: <see cref="InstallPixels"/> は呼び出し側が確保した画素を複製せずに扱います（4K の複製は 31 MiB と 5.7 ms）。
/// <b>本ライブラリの画像のアドレスを渡し直す用途には使わないこと</b>（凍結を迂回できてしまいます）。<br/>
/// <b>解放</b>: 解放の実装は <see cref="SafeAlignedBuffer"/>（確保した場合）と <see cref="SafeBorrowedBuffer"/>（借用した場合）が
/// 単一所有するため、破棄を忘れても GC が解放する（大きな画像は <c>using</c> で早く解放すること）。<br/>
/// <b>画素形式</b>: BGRA8888（アルファ乗算済み）。 ストライドは <c>幅 × <see cref="BytesPerPixel"/></c>（行パディングなし）。
/// </remarks>
public sealed class EsBitmap : IDisposable
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    /// <value>4。 ストライドは常に <c>幅 × 本値</c> である。</value>
    /// <remarks>リテラルの定義は内部のバッファ型（<see cref="EsBitmapBuffer"/>）が単一所有し、本定数はその転送である。</remarks>
    public const int BytesPerPixel = EsBitmapBuffer.BytesPerPixel;

    /// <summary>画素バッファ（確保・借用・共有・解放を単一所有する内部型）。</summary>
    /// <remarks>
    /// <b>継承ではなく保有にする理由</b>: 公開型は内部の型から派生できません（<c>CS0060</c>）。 読み取り専用と書き換え可能は
    /// <b>兄弟</b>であり、実装の共有は<b>内部のバッファ型</b>が担います。
    /// </remarks>
    private readonly EsBitmapBuffer _buffer;

    /// <summary>サイズを指定して画素バッファを確保する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="clear">true の場合は全領域を 0（透明）で初期化する。 直後に全領域を書く場合は false。</param>
    private EsBitmap(int width, int height, bool clear) => _buffer = EsBitmapBuffer.Allocate(width, height, clear);

    /// <summary>画素バッファを受け取る（生成は本クラスの静的メソッドと <see cref="EsBitmapLoader"/> が行う）。</summary>
    /// <param name="buffer">画素バッファ。 破棄の責任は本インスタンスが持つ。</param>
    internal EsBitmap(EsBitmapBuffer buffer) => _buffer = buffer;

    /// <summary>画素バッファ（同じアセンブリの <see cref="EsReadOnlyBitmap.FromBitmap"/> が 0 コピー共有に使う）。</summary>
    /// <remarks>公開しない（生のバッファを外部へ渡すと、読み取り専用の約束を作り直せてしまうため）。</remarks>
    internal EsBitmapBuffer Buffer => _buffer;

    /// <summary>破棄済みかどうか。</summary>
    public bool IsDisposed => _buffer.IsDisposed;

    /// <summary>凍結済みかどうか（<see cref="SetReadOnly"/> の後は true。 書き込みの入口が例外を投げる）。</summary>
    public bool IsReadOnly => _buffer.IsReadOnly;

    /// <summary>幅（ピクセル）。</summary>
    public int Width => _buffer.Width;

    /// <summary>高さ（ピクセル）。</summary>
    public int Height => _buffer.Height;

    /// <summary>幅・高さ（ピクセル）。</summary>
    public EsSizeInt Size => _buffer.Size;

    /// <summary>1 ラインあたりのバイト数（パディングなし。 常に <c>幅 × <see cref="BytesPerPixel"/></c>）。</summary>
    public int Stride => _buffer.Stride;

    /// <summary>ピクセル データの読み取り専用ビュー（意味は <see cref="EsReadOnlyBitmap.ReadOnlyPixels"/> と同じ）。</summary>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public ReadOnlySpan<byte> ReadOnlyPixels => _buffer.ReadOnlyPixels;

    /// <summary>ピクセル データの<b>書き込み可能なビュー</b>（<see cref="ReadOnlyPixels"/> と同じ領域を指す。 コピーしない）。</summary>
    /// <value>先頭から <c><see cref="Stride"/> × <see cref="Height"/></c> バイト。 ピクセルを持たない場合は空。</value>
    /// <remarks>
    /// <b>無印の <c>Pixels</c> を書き込み可能なビューにする理由</b>: 書き換え可能な型では書き込みが本題であるため
    /// （<c>Memory&lt;T&gt;</c> の書き込み可能なビューが <c>Span</c> であるのと同じ関係）。 読み取り専用のビューは
    /// <see cref="ReadOnlyPixels"/> であり、<b>どちらの型でも名前が返る型と一致する</b>。<br/>
    /// <b>取得した後に凍結しても、取得済みのビューは書けます</b>（<c>Span</c> は raw なビューであり、取得後に
    /// 無効化できない。 Skia の <c>GetPixelSpan</c> も同じ）。 ビューを保持したまま凍結しないこと。<br/>
    /// <b>生ポインタが要る場合</b>は <c>fixed</c> で取る（領域はアンマネージドのため、生存中はアドレスが動かない）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">凍結済み（読み取り専用）の場合。</exception>
    public Span<byte> Pixels => _buffer.Pixels;

    /// <summary>保存に使える拡張子（値は <see cref="EsReadOnlyBitmap.SupportedFileExtensions"/> と同一）。</summary>
    /// <remarks>読み取り専用の画像からも保存できるため、基底と同じ値を本型でも公開する（どちらの権利の画像でも同じ一覧を使う）。</remarks>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<string> SupportedFileExtensions => WinGdiPlusImageCodec.SupportedExtensions;

    /// <summary>指定サイズの空の画像（BGRA8888・全ピクセル透明）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    public static EsBitmap Create(int width, int height) => new(width, height, clear: true);

    /// <summary>指定サイズの空の画像（全ピクセル透明）を生成する（サイズ型を受け取る版）。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    public static EsBitmap Create(EsSizeInt size) => Create(size.Width, size.Height);

    /// <summary>指定サイズの画像を生成し、全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="color">塗りつぶし色。</param>
    /// <returns>生成された画像（全ピクセルが指定色）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>色の成分はそのまま書き込む（乗算済みへの変換は行わない）。 0 埋めは行わない（全領域を書くため）。</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    public static EsBitmap Create(int width, int height, Color color)
    {
        var buffer = EsBitmapBuffer.Allocate(width, height, clear: false);
        try
        {
            buffer.Clear(color.B, color.G, color.R, color.A);
            return new EsBitmap(buffer);
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
    public static EsBitmap Create(EsSizeInt size, Color color) => Create(size.Width, size.Height, color);

    /// <summary>指定サイズの画素を確保し、充填する処理を 1 回で行う（相互変換が使う入口）。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="fill">確保した画素（<c>幅 × 4 × 高さ</c> バイト）へ書く処理。 <b>全領域を書く責任を持つ</b>。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b><see cref="EsReadOnlyBitmap"/> にも同じ入口がある</b>（相互変換はどちらの権利の画像も作るため。 対にして覚える負担を減らす）。
    /// 0 埋めを省き、<paramref name="fill"/> が例外を投げた場合は確保した領域を解放する。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="fill"/> が null の場合。</exception>
    public static EsBitmap CreateFilled(int width, int height, Action<Span<byte>> fill)
    {
        ArgumentNullException.ThrowIfNull(fill);

        var buffer = EsBitmapBuffer.Allocate(width, height, clear: false);
        try
        {
            fill(buffer.Pixels);
            return new EsBitmap(buffer);
        }
        catch
        {
            // 確保した領域を解放してから例外を伝える（呼び出し側に解放の機会が無い）
            buffer.Dispose();
            throw;
        }
    }

    /// <summary>読み取り専用の画像から書き換え可能な画像を生成する（<b>常に複製</b>）。</summary>
    /// <param name="source">複製元の画像。 借用であり破棄しない。</param>
    /// <returns>複製した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>複製する理由</b>: 兄弟であり画素を共有できないため（読み取り専用の画像の画素を書き換えると、その画像を
    /// 読んでいる相手の画素が変わる）。 4K の複製は 31 MiB と 5.7 ms を要する。<br/>
    /// <b><see cref="Copy"/> との違い</b>: <see cref="FromReadOnlyBitmap"/> は<b>別のオブジェクトからの生成</b>、
    /// <see cref="Copy"/> は<b>自身の複製</b>である。 実装が同じでも意味が異なるため、片方へ寄せない。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    public static EsBitmap FromReadOnlyBitmap(EsReadOnlyBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.ThrowIfDisposed();

        var buffer = EsBitmapBuffer.Allocate(source.Width, source.Height, clear: false);
        try
        {
            source.ReadOnlyPixels.CopyTo(buffer.Pixels);
            return new EsBitmap(buffer);
        }
        catch
        {
            // 確保した領域を解放してから例外を伝える（呼び出し側に解放の機会が無い）
            buffer.Dispose();
            throw;
        }
    }

    /// <summary>呼び出し側が確保した画素を、複製せずに書き換え可能な画像として扱う（借用）。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="pixels">画素の先頭アドレス（BGRA8888。 <c>幅 × 4 × 高さ</c> バイト以上）。</param>
    /// <param name="release">解放時に 1 回だけ呼ぶ処理（アドレスを受け取る）。 解放が不要な場合は null。</param>
    /// <returns>借用した画像。 領域の解放は呼び出し側が行う（<paramref name="release"/> を渡した場合は本画像の破棄時に呼ばれる）。</returns>
    /// <remarks>
    /// <b>外部が確保したメモリのための入口です</b>（他フレームワークのビットマップ・アンマネージドの領域）。
    /// <b>本ライブラリの画像のアドレスを渡し直さないこと</b>: 凍結を迂回して書ける別の実体ができてしまいます
    /// （凍結は実体の画素領域ごとに効くため）。<br/>
    /// <b>行パディングがある領域は借用できない</b>: 画素は連続した 1 本の領域として公開するため、ストライドは
    /// <c>幅 × <see cref="BytesPerPixel"/></c> に限る（それ以外は <see cref="ArgumentException"/>）。<br/>
    /// <b>借用中も本画像は書き換えられる</b>（領域の所有者が別にいる場合は、そちらの書き込みと競合しないように使う）。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> が 0 の場合（画素がある場合）。</exception>
    public static EsBitmap InstallPixels(int width, int height, nint pixels, Action<nint>? release = null)
        => new(EsBitmapBuffer.Borrow(width, height, pixels, release));

    /// <summary>ファイルを読み込んで書き換え可能な画像を生成する。</summary>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。 null 不可。</param>
    /// <returns>読み込んだ画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// 中間バッファを作らず、0 埋めも省く（<see cref="EsReadOnlyBitmap.Load"/> と同じ手順）。
    /// <b>書き換えが要らない場合</b>は <see cref="EsReadOnlyBitmap.Load"/> を使う。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合。</exception>
    [SupportedOSPlatform("windows")]
    public static EsBitmap Load(string filePath)
        => EsBitmapLoader.Load(filePath, static buffer => new EsBitmap(buffer));

    /// <summary>0 埋めを行わずに画素を確保する（全領域を書く責任を呼び出し側が持つ）。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <returns>確保した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>公開しない理由</b>: 書き込んでいない領域が不定値のまま残るため、型で「全領域を書く」約束を表せない。
    /// 生成の直後に全領域を書く手順（切り出し・縮小・複製・相互変換）が使う。
    /// </remarks>
    internal static EsBitmap CreateUninitialized(int width, int height) => new(width, height, clear: false);

    /// <summary>0 埋めを行わずに画素を確保する（サイズ型を受け取る版）。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <returns>確保した画像。 所有権は呼び出し側へ移る。</returns>
    internal static EsBitmap CreateUninitialized(EsSizeInt size) => CreateUninitialized(size.Width, size.Height);

    /// <summary>本画像の一部を指す<b>0 コピーの読み取りビュー</b>を生成する（切り出しの複製を避ける）。</summary>
    /// <param name="rect">ビューの範囲（元画像の座標。 <b>画像の内側であること</b>）。</param>
    /// <returns>ビュー。 所有権は呼び出し側へ移る（破棄すると元画像への参照を返す。 画素は破棄しない）。</returns>
    /// <remarks>
    /// <b>読み取り専用の画像と同じ入口です</b>（本画像が書き換え可能でも、ビューは<b>読み取り専用</b>です。 書き換える
    /// 場合は本画像を使うか <see cref="Copy"/> で複製します）。 詳細（行幅・寿命・範囲）は
    /// <see cref="EsReadOnlyBitmap.ToPixmap"/> と同じです。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rect"/> が空、または画像の範囲外に出る場合。</exception>
    public EsPixmap ToPixmap(EsRectInt rect)
    {
        ThrowIfDisposed();
        return _buffer.ToPixmap(rect);
    }

    /// <summary>呼び出し側の画素を<b>複製して</b>書き換え可能な画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="pixels">複製元の画素（BGRA8888。 <c>幅 × 4 × 高さ</c> バイト）。</param>
    /// <returns>複製した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>読み取り専用の画像と同じ入口です</b>（<see cref="EsReadOnlyBitmap.FromPixelCopy"/>）。 複製した後に元の
    /// 画素が変わっても本画像は変わりません（借用の <see cref="InstallPixels"/> との違い）。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> の長さが <c>幅 × 4 × 高さ</c> と一致しない場合。</exception>
    public static EsBitmap FromPixelCopy(int width, int height, ReadOnlySpan<byte> pixels)
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
            return new EsBitmap(buffer);
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
    public static EsBitmap FromPixmap(EsPixmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.ThrowIfDisposed();

        var buffer = EsBitmapBuffer.Allocate(source.Width, source.Height, clear: false);
        try
        {
            source.CopyTo(buffer);
            return new EsBitmap(buffer);
        }
        catch
        {
            // 確保した領域を解放してから例外を伝える（呼び出し側に解放の機会が無い）
            buffer.Dispose();
            throw;
        }
    }
    /// <summary>本画像の複製（<b>独立した画素</b>）を生成する。 書き換えても元へ影響しない。</summary>
    /// <returns>独立した複製。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>読み取り専用の画像の <see cref="EsReadOnlyBitmap.Copy"/> と同じ「複製」</b>です（どちらの型でも
    /// <c>Copy</c> は独立した画素を作ります。 費用は実測: 4K で 31 MiB / 5.7 ms）。 書き換え可能な画像では
    /// 画素を共有できないため（片方の書き込みがもう片方を変える）、複製だけを提供します。<br/>
    /// <b>名前を <c>Clone</c> にしない理由</b>は <see cref="EsReadOnlyBitmap.Copy"/> と同じです。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public EsBitmap Copy()
    {
        ThrowIfDisposed();
        var copy = EsBitmapBuffer.Allocate(Width, Height, clear: false);
        try
        {
            ReadOnlyPixels.CopyTo(copy.Pixels);
            return new EsBitmap(copy);
        }
        catch
        {
            // 確保した領域を解放してから例外を伝える（呼び出し側に解放の機会が無い）
            copy.Dispose();
            throw;
        }
    }

    /// <summary>本画像を凍結し、以降の書き込みを拒否する。</summary>
    /// <remarks>
    /// <b>凍結する理由</b>: <see cref="EsReadOnlyBitmap.FromBitmap"/> が 0 コピーで共有できるようにするためです
    /// （未凍結のまま渡すと複製になる）。 また、同じ画素を指す実体が複数ある場合に、後からの書き込みで
    /// 読み手の画素が黙って変わることを防ぎます（<b>沈黙ではなく例外</b>にする）。<br/>
    /// <b>呼ぶ時点</b>: 書き終えた時点で呼びます（呼んだ後は <see cref="Pixels"/> / <see cref="SetPixel(int, int, byte, byte, byte, byte)"/> /
    /// <see cref="TrySetPixel(int, int, Color)"/> / <see cref="Clear(byte, byte, byte, byte)"/> が <see cref="InvalidOperationException"/> を投げます）。
    /// <b>凍結の前に取得したビューは書けます</b>（<c>Span</c> は取得後に無効化できない）。<br/>
    /// <b>画素そのものは不変にならない</b>: 同じメモリを借用した別の実体（<see cref="InstallPixels"/>）からは書けます
    /// （凍結は<b>この実体の画素領域</b>に効きます）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public void SetReadOnly()
    {
        ThrowIfDisposed();
        _buffer.SetReadOnly();
    }

    /// <summary>指定座標のピクセルを BGRA で設定する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    /// <exception cref="ArgumentOutOfRangeException">座標が画像の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">凍結済み（読み取り専用）の場合。</exception>
    public void SetPixel(int x, int y, byte b, byte g, byte r, byte a) => _buffer.SetPixel(x, y, b, g, r, a);

    /// <summary>指定座標のピクセルを色として設定する（座標型を受け取る版）。</summary>
    /// <param name="point">座標（ピクセル）。</param>
    /// <param name="color">設定する色。</param>
    /// <remarks>座標を <see cref="EsPointInt"/> で持つ呼び出し側のための入口（<see cref="GetPixel(EsPointInt)"/> と対になる形）。
    /// 色の成分はそのまま書き込む（乗算済みへの変換は行わない）。</remarks>
    /// <exception cref="ArgumentOutOfRangeException">座標が画像の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">凍結済み（読み取り専用）の場合。</exception>
    public void SetPixel(EsPointInt point, Color color) => _buffer.SetPixel(point.X, point.Y, color.B, color.G, color.R, color.A);

    /// <summary>指定座標へ色を設定する（<b>範囲外は無視する</b>）。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="color">設定する色。</param>
    /// <returns>書き込めたかどうか。 範囲外の場合は false（例外にしない）。</returns>
    /// <remarks>
    /// 描画（破線・矩形オーバーレイ等）のように<b>画像の端をはみ出す矩形を描く</b>場合に使う
    /// （範囲判定を各呼び出し側へ書かない）。 破棄済みの場合は例外を投げる（範囲外とは区別する）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">凍結済み（読み取り専用）の場合。</exception>
    public bool TrySetPixel(int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            _buffer.ThrowIfDisposed();
            _buffer.ThrowIfReadOnly();
            return false;
        }

        _buffer.SetPixel(x, y, color.B, color.G, color.R, color.A);
        return true;
    }

    /// <summary>指定座標へ色を設定する（座標型を受け取る版。 範囲外は無視する）。</summary>
    /// <param name="point">座標（ピクセル）。</param>
    /// <param name="color">設定する色。</param>
    /// <returns>書き込めたかどうか。 範囲外の場合は false（例外にしない）。</returns>
    /// <remarks>範囲の判定と破棄済みの検証は <see cref="TrySetPixel(int, int, Color)"/> が単一所有する。</remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">凍結済み（読み取り専用）の場合。</exception>
    public bool TrySetPixel(EsPointInt point, Color color) => TrySetPixel(point.X, point.Y, color);

    /// <summary>全ピクセルを指定色（BGRA）で塗りつぶす。</summary>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    /// <remarks>与えられた成分をそのまま書き込む（乗算済みへの変換は行わない）。</remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">凍結済み（読み取り専用）の場合。</exception>
    public void Clear(byte b, byte g, byte r, byte a) => _buffer.Clear(b, g, r, a);

    /// <summary>全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="color">塗りつぶし色。</param>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">凍結済み（読み取り専用）の場合。</exception>
    public void Clear(Color color) => Clear(color.B, color.G, color.R, color.A);

    /// <summary>指定座標のピクセルを BGRA で取得する（意味は <see cref="EsReadOnlyBitmap.GetPixel(int, int)"/> と同じ）。</summary>
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

    /// <summary>画像の寸法を「幅x高さ」形式の文字列で返す（意味は <see cref="EsReadOnlyBitmap.ToSizeText"/> と同じ）。</summary>
    /// <returns>寸法の文字列（例: <c>1920x1080</c>）。</returns>
    public string ToSizeText() => $"{Width}x{Height}";

    /// <summary>破棄済みなら <see cref="ObjectDisposedException"/> を送出する。</summary>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public void ThrowIfDisposed() => _buffer.ThrowIfDisposed();

    /// <summary>画素領域を解放する（冪等）。</summary>
    /// <remarks>0 コピーで共有した読み取り専用の画像がある場合、実体の解放は最後の 1 つが行う。</remarks>
    public void Dispose()
    {
        _buffer.Dispose();
        GC.SuppressFinalize(this);
    }
}
