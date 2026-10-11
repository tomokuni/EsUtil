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
/// <b>読み取り専用の画像との関係</b>: 本型は <see cref="EsReadOnlyBitmap"/> の<b>派生</b>であり、
/// 読み取りの面（<see cref="EsReadOnlyBitmap.ReadOnlyPixels"/> / <see cref="EsReadOnlyBitmap.GetPixel(int, int)"/> 等）を
/// 受け継いだうえで書き込みの入口を足す。 したがって<b>本型は読み取り専用の画像としてそのまま渡せる</b>
/// （キャストも変換も要らない）。 逆に読み取り専用の画像から書き換え可能な画像を作るには
/// <see cref="Create(EsReadOnlyBitmap)"/>（<b>複製</b>）を使う。<br/>
/// <b>凍結</b>: <see cref="EsReadOnlyBitmap.SetReadOnly"/> の後は、本型の書き込みの入口が
/// <see cref="InvalidOperationException"/> を投げる（共有した読み取り専用の画像の内容を後から変えないため）。<br/>
/// <b>借用</b>: <see cref="InstallPixels"/> は呼び出し側が確保した画素を複製せずに扱う（4K の複製は 31 MiB と 5.7 ms）。<br/>
/// <b>解放</b>: 解放の実装は <see cref="SafeAlignedBuffer"/>（確保した場合）と <see cref="SafeBorrowedBuffer"/>（借用した場合）が
/// 単一所有するため、破棄を忘れても GC が解放する（大きな画像は <c>using</c> で早く解放すること）。<br/>
/// <b>画素形式</b>: BGRA8888（アルファ乗算済み）。 ストライドは <c>幅 × <see cref="EsReadOnlyBitmap.BytesPerPixel"/></c>（行パディングなし）。
/// </remarks>
public sealed class EsBitmap : EsReadOnlyBitmap
{
    /// <summary>サイズを指定して画素バッファを確保する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="clear">true の場合は全領域を 0（透明）で初期化する。 直後に全領域を書く場合は false。</param>
    private EsBitmap(int width, int height, bool clear)
        : base(EsBitmapBuffer.Allocate(width, height, clear))
    {
    }

    /// <summary>画素バッファを受け取る（生成は本クラスの静的メソッドが行う）。</summary>
    /// <param name="buffer">画素バッファ。 破棄の責任は本インスタンスが持つ。</param>
    private EsBitmap(EsBitmapBuffer buffer)
        : base(buffer)
    {
    }

    /// <summary>ピクセル データの<b>書き込み可能なビュー</b>（<see cref="EsReadOnlyBitmap.ReadOnlyPixels"/> と同じ領域を指す。 コピーしない）。</summary>
    /// <value>先頭から <c><see cref="EsReadOnlyBitmap.Stride"/> × <see cref="EsReadOnlyBitmap.Height"/></c> バイト。 ピクセルを持たない場合は空。</value>
    /// <remarks>
    /// <b>無印の <c>Pixels</c> を書き込み可能なビューにする理由</b>: 書き換え可能な型では書き込みが本題であるため
    /// （<c>Memory&lt;T&gt;</c> の書き込み可能なビューが <c>Span</c> であるのと同じ関係）。 読み取り専用のビューは
    /// 基底の <c>ReadOnlyPixels</c> であり、<b>どちらの型でも名前が返る型と一致する</b>。<br/>
    /// <b>生ポインタが要る場合</b>は <c>fixed</c> で取る（領域はアンマネージドのため、生存中はアドレスが動かない）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">凍結済み（読み取り専用）の場合。</exception>
    public Span<byte> Pixels => Buffer.Pixels;

    /// <summary>凍結済みかどうか（<see cref="EsReadOnlyBitmap.SetReadOnly"/> の後は true。 書き込みの入口が例外を投げる）。</summary>
    /// <remarks>本型は書き込みの入口を持つため、基底（常に true）と違い凍結の状態を返す。</remarks>
    public override bool IsReadOnly => Buffer.IsReadOnly;

    /// <summary>保存に使える拡張子（値は <see cref="EsReadOnlyBitmap.SupportedFileExtensions"/> と同一）。</summary>
    /// <remarks>読み取り専用の画像からも保存できるため、基底と同じ値を本型でも公開する
    /// （<c>new</c> は「基底と同じ名前で、より具体的な型の側からも見えるようにする」の印である。 値は同一）。</remarks>
    [SupportedOSPlatform("windows")]
    public static new IReadOnlyList<string> SupportedFileExtensions => WinGdiPlusImageCodec.SupportedExtensions;

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
    /// <b>基底と同名で戻り値だけが違う</b>（<c>new</c> はその印である。 読み取り専用の画像を作る場合は基底の
    /// <see cref="EsReadOnlyBitmap.CreateFilled"/> を使う）。 0 埋めを省き、<paramref name="fill"/> が例外を投げた場合は確保した領域を解放する。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="fill"/> が null の場合。</exception>
    public static new EsBitmap CreateFilled(int width, int height, Action<Span<byte>> fill)
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

    /// <summary>読み取り専用の画像を<b>複製して</b>書き換え可能な画像を生成する（別のオブジェクトからの生成）。</summary>
    /// <param name="source">複製元の画像。 借用であり破棄しない。</param>
    /// <returns>複製した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>複製する理由</b>: 元の画像は読み取り専用であり、共有したまま書き換えると元の画素が変わってしまうため
    /// （読み取り専用 → 書き換え可能 の唯一の入口である）。 4K の複製は 31 MiB と 5.7 ms を要する。<br/>
    /// <b><see cref="Clone"/> との違い</b>: <see cref="Create(EsReadOnlyBitmap)"/> は<b>別のオブジェクトからの生成</b>、
    /// <see cref="Clone"/> は<b>自身の複製</b>である。 実装が同じでも意味が異なるため、片方へ寄せない。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    public static EsBitmap Create(EsReadOnlyBitmap source)
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
    /// <b>行パディングがある領域は借用できない</b>: 画素は連続した 1 本の領域として公開するため、ストライドは
    /// <c>幅 × <see cref="EsReadOnlyBitmap.BytesPerPixel"/></c> に限る（それ以外は <see cref="ArgumentException"/>）。<br/>
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
    /// <b>基底と同名で戻り値だけが違う</b>（<c>new</c> はその印である。 書き換えが要らない場合は基底の
    /// <see cref="EsReadOnlyBitmap.Load"/> を使う）。 中間バッファを作らず、0 埋めも省く。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合。</exception>
    [SupportedOSPlatform("windows")]
    public static new EsBitmap Load(string filePath)
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

    /// <summary>本画像の複製を生成する（<b>独立した画素</b>を持ち、書き換えても元へ影響しない）。</summary>
    /// <returns>独立した複製。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>基底（共有）を上書きする理由</b>: 書き換え可能な画像同士が画素を共有すると、片方の書き込みが
    /// もう片方の画素を変えてしまうため（基底の <see cref="EsReadOnlyBitmap.Clone"/> は読み取り専用同士であるため共有する）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public override EsBitmap Clone()
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

    /// <summary>本画像を凍結し、読み取り専用の見方として返す（相手へ渡すときの<b>引き渡し</b>）。</summary>
    /// <returns>同じ画素を指す読み取り専用の見方（<b>コピーしない</b>。 同じ実体を返す）。</returns>
    /// <remarks>
    /// <b>型を分けた意味を「注意」に頼らせないための入口</b>です。 <see cref="EsReadOnlyBitmap"/> は
    /// 「この型では書けない」しか保証しないため、書き換え可能な実体をそのまま渡すと、渡した相手の画素が
    /// 後から変わります（渡した側が書き続ければ）。 本メソッドは<b>凍結</b>（<see cref="EsReadOnlyBitmap.SetReadOnly"/>）
    /// してから返すため、<b>渡した相手は画素が変わらないことを前提にできます</b>（凍結は画素領域の共有状態に
    /// 効くので、書き換え可能な側の実体も書けなくなります）。<br/>
    /// <b>呼んだ後に書き続けられません</b>（書く必要がある場合は、渡す前に <see cref="Clone"/> で複製を
    /// 作って渡すか、書き終えてから本メソッドを呼びます）。<br/>
    /// <b>コピーしない理由</b>: 4K の複製は 31 MiB と 5.7 ms を要するため、公開（表示・履歴への登録）の
    /// たびに複製しないようにします。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public EsReadOnlyBitmap AsReadOnly()
    {
        SetReadOnly();
        return this;
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
    public void SetPixel(int x, int y, byte b, byte g, byte r, byte a) => Buffer.SetPixel(x, y, b, g, r, a);

    /// <summary>指定座標のピクセルを色として設定する（座標型を受け取る版）。</summary>
    /// <param name="point">座標（ピクセル）。</param>
    /// <param name="color">設定する色。</param>
    /// <remarks>座標を <see cref="EsPointInt"/> で持つ呼び出し側のための入口（読み取りの <see cref="EsReadOnlyBitmap.GetPixel(EsPointInt)"/> と対になる形）。
    /// 色の成分はそのまま書き込む（乗算済みへの変換は行わない）。</remarks>
    /// <exception cref="ArgumentOutOfRangeException">座標が画像の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">凍結済み（読み取り専用）の場合。</exception>
    public void SetPixel(EsPointInt point, Color color) => SetPixel(point.X, point.Y, color.B, color.G, color.R, color.A);

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
            ThrowIfDisposed();
            return false;
        }

        SetPixel(x, y, color.B, color.G, color.R, color.A);
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
    public void Clear(byte b, byte g, byte r, byte a) => Buffer.Clear(b, g, r, a);

    /// <summary>全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="color">塗りつぶし色。</param>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">凍結済み（読み取り専用）の場合。</exception>
    public void Clear(Color color) => Clear(color.B, color.G, color.R, color.A);
}
