using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using Color = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>書き換え可能な画像（生成した側が所有し、ピクセルを編集する型）。</summary>
/// <remarks>
/// <b>目的</b>: 「この画像は書き換えてよく、破棄する責任が自分にある」ことを<b>型で表す</b>。
/// 生成（<c>Create</c>）と相互変換の結果が本型になり、<c>using</c> で決定的に解放できる。<br/>
/// <br/>
/// <b>読み取り専用の面も持つ</b>: <see cref="EsBitmapBase"/> を継承し <see cref="IEsWritableBitmap"/> を実装するため、
/// 読み取りだけの関数（<see cref="IEsBitmap"/> を受ける）へもそのまま渡せる。<br/>
/// <b>兄弟との関係</b>: <see cref="EsBitmap"/>（読み取り専用）とは<b>兄弟</b>であり、互いに変換できない
/// （同じバッファを 2 つの型で見せると、書き換えの可否が静かに食い違うため）。
/// 読み取り専用の画像から書き換え可能な画像を作る場合は <see cref="Create(IEsBitmap)"/> を使う。<br/>
/// <b>解放</b>: <see cref="IDisposable"/> を実装する。 解放の実装は <see cref="SafeAlignedBuffer"/> が単一所有するため、
/// 破棄を忘れても GC が解放する（大きな画像は <c>using</c> で早く解放すること）。
/// </remarks>
public sealed class EsWritableBitmap : EsBitmapBase, IEsWritableBitmap, IDisposable
{
    /// <summary>指定サイズのピクセル バッファを確保する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="clear">true の場合は全領域を 0（透明）で初期化する。 直後に全領域を書く場合は false。</param>
    private EsWritableBitmap(int width, int height, bool clear = true)
        : base(width, height, clear)
    {
    }

    /// <summary>読み取り専用の画像のバッファを共有する（最初の書き込みで複製する = 遅延コピー）。</summary>
    /// <param name="source">共有する元の画像（読み取り専用であること）。</param>
    private EsWritableBitmap(EsBitmap source)
        : base(source)
    {
    }

    /// <summary>ピクセル バッファを解放する（冪等）。</summary>
    /// <remarks>解放の実装は <see cref="SafeAlignedBuffer.ReleaseHandle"/> が単一所有する。</remarks>
    public void Dispose() => ReleaseBuffer();

    /// <summary>指定サイズの空の画像（BGRA8888・全ピクセル透明）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    public static EsWritableBitmap Create(int width, int height)
        => new(width, height);

    /// <summary>指定矩形サイズの空の画像（BGRA8888・全ピクセル透明）を生成する。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    public static EsWritableBitmap Create(EsSizeInt size)
        => Create(size.Width, size.Height);

    /// <summary>指定サイズの画像（BGRA8888）を生成し、全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="color">塗りつぶし色。</param>
    /// <returns>生成された画像（全ピクセルが指定色）。 所有権は呼び出し側へ移る。</returns>
    public static EsWritableBitmap Create(int width, int height, Color color)
    {
        // 直後に全領域を塗るため、生成時の 0 埋めは行わない
        var bitmap = CreateUninitialized(width, height);
        bitmap.ClearCore(color.B, color.G, color.R, color.A);
        return bitmap;
    }

    /// <summary>指定矩形サイズの画像（BGRA8888）を生成し、全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <param name="color">塗りつぶし色。</param>
    /// <returns>生成された画像（全ピクセルが指定色）。 所有権は呼び出し側へ移る。</returns>
    public static EsWritableBitmap Create(EsSizeInt size, Color color)
        => Create(size.Width, size.Height, color);

    /// <summary>別のオブジェクト（画像）から書き換え可能な画像を生成する。</summary>
    /// <param name="source">生成元の画像。 借用であり破棄しない。</param>
    /// <returns>書き換え可能な画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>元が読み取り専用の画像（<see cref="EsBitmap"/>）なら共有し、最初の書き込みで複製する（遅延コピー）</b>:
    /// 読み取り専用の画像は以後変わらないため、書き込むまで複製を遅らせても<b>見え方は同じ</b>である
    /// （書き込みの瞬間に複製するので、元の画素は変わらない。 実測で確認）。
    /// したがって<b>書き込まない場合は全域コピー（実測: 4K で 5.7 ms / 31 MiB）を払わない</b>。<br/>
    /// <b>それ以外（書き換え可能な画像・外部実装の画像）からは複製する</b>: 生成元がその後に変わりうるため
    /// （書き換え可能な画像は持ち主が書き込める。 外部実装の画像はバッファを共有できる手段が無い）。<br/>
    /// <b>本メソッドは「別のオブジェクトからの生成」である</b>: 受け取った画像から書き換え可能な実体を組み立てる。
    /// <b>自身（書き換え可能な画像）の複製は <see cref="Clone"/></b> であり意味が異なる
    /// （実装が同じでも片方へ寄せない。 <c>Clone</c> は即時に複製する）。<br/>
    /// <b>引数が面（<see cref="IEsBitmap"/>）である理由</b>: <see cref="EsBitmap.Create(IEsBitmap)"/> と同じであり、
    /// <b>受け取る実体を本型の派生に限らない</b>ためである（外部が実装する画像（UI の表示アダプタ等）からも
    /// 書き換え可能な実体を組み立てられる。 同じ型（<see cref="EsWritableBitmap"/>）を渡すこともできるが、
    /// 自身の複製が目的なら <see cref="Clone"/> を使う）。<br/>
    /// <b>ファイルから書き換え可能な画像が要る場合は本メソッドを使わない</b>
    /// （<see cref="Load(string)"/> がデコードと同時に書き換え可能な型を作るため、複製が 1 回で済む）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    public static EsWritableBitmap Create(IEsBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(source.IsDisposed, source);

        // 読み取り専用の画像は以後変わらないため、バッファを共有して書き込み時に複製する
        return source is EsBitmap readOnly ? new EsWritableBitmap(readOnly) : CopyFrom(source);
    }

    /// <summary>直後に全領域を書く前提でピクセル バッファを確保する（初期化しない）。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <returns>生成された画像（ピクセルは不定値）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// 直後に全領域を書く手順（切り出し・縮小・複製・画面キャプチャ）から使う（大きな画像の 0 埋めを丸ごと省ける）。<br/>
    /// <b>1 回の充填で全体を書く相互変換（System.Drawing / Avalonia / Skia）は <see cref="CreateFilled"/> を使う</b>
    /// （呼び出し側へ書き込みのビューを渡さないため）。<br/>
    /// <b>書かない領域が残る場合は <see cref="Create(int, int)"/> を使うこと</b>（不定値が描画へ回り、
    /// 透明ではないピクセルが見える）。 <b>書く責任は呼び出し側が持つ</b>。
    /// </remarks>
    public static EsWritableBitmap CreateUninitialized(int width, int height)
        => new(width, height, clear: false);

    /// <summary>ピクセルを複製して書き換え可能な画像を生成する（外部実装の画像・書き換え可能な画像から作る場合）。</summary>
    /// <param name="source">生成元の画像。 借用であり破棄しない。</param>
    /// <returns>独立した画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>複製が要る理由</b>: 生成元が<b>その後に変わりうる</b>ため（外部実装の画像は本ライブラリが関与できない。
    /// 書き換え可能な画像は持ち主が書き込める）。 読み取り専用の画像（<see cref="EsBitmap"/>）から作る場合は
    /// <see cref="Create(IEsBitmap)"/> が共有するため、本メソッドは通らない。
    /// </remarks>
    private static EsWritableBitmap CopyFrom(IEsBitmap source)
    {
        var result = CreateUninitialized(source.Width, source.Height);
        source.Pixels.CopyTo(result.WritableBuffer);
        return result;
    }

    /// <summary>直後に全領域を書く前提でピクセル バッファを確保する（初期化しない）。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <returns>生成された画像（ピクセルは不定値）。 所有権は呼び出し側へ移る。</returns>
    public static EsWritableBitmap CreateUninitialized(EsSizeInt size)
        => CreateUninitialized(size.Width, size.Height);

    /// <summary>確保と充填を 1 回で行う（読み取りの面と同じ入口の形にする）。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="fill">確保したバッファ（全領域）を充填する処理。 生成の直後に 1 回だけ呼ぶ。</param>
    /// <returns>生成された画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b><see cref="EsBitmap.CreateFilled"/> と対にする理由</b>: 同じ手順（外部のピクセルをコアの画像へ写す）が
    /// 権利の違いだけで 2 通りの読み方になるのを避けるためです。 相互変換（System.Drawing / Avalonia / Skia）は
    /// どちらの権利の画像も作るため、<b>同じ名前・同じ引数の形の入口があると変換の実装が同じ形で読めます</b>。<br/>
    /// <b><see cref="CreateUninitialized(int, int)"/> との関係</b>: 本メソッドはそれを呼んで充填するだけです
    /// （同じ確保手順を 2 度書かず、0 埋めを省く場所も 1 か所に保ちます）。
    /// <b>読み取り専用の実体（<see cref="EsBitmap"/>）に本メソッドしか無い理由</b>: 書き込みの入口を公開しないためです
    /// （同名の確保入口は <c>private</c> に持ち、外へ出す入口を確保と充填の 1 回へ閉じます）。
    /// 使い分けは書く形で決まります。 <b>1 回の充填で全体を書く</b>手順（相互変換）は本メソッド、
    /// それ以外で<b>直後に全領域を書く</b>手順（切り出し・縮小・複製・画面キャプチャ）は
    /// <see cref="CreateUninitialized(int, int)"/> を使います。<br/>
    /// <b>書かない領域を残さないこと</b>: 生成時の 0 埋めは行わない（透明が必要な領域は <paramref name="fill"/> が消す）。
    /// </remarks>
    public static EsWritableBitmap CreateFilled(int width, int height, Action<Span<byte>> fill)
    {
        ArgumentNullException.ThrowIfNull(fill);

        // 全領域を充填する前提で確保するため、生成時の 0 埋めは行わない
        var result = CreateUninitialized(width, height);
        fill(result.WritableBuffer);
        return result;
    }

    /// <summary>ファイルから書き換え可能な画像を読み込む（形式は内容から判別される）。</summary>
    /// <param name="filePath">読み込むファイルのパス。 null 不可。</param>
    /// <returns>読み込んだ画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合。</exception>
    /// <remarks>
    /// <b>書き換えが要らない場合は <see cref="EsBitmap.Load(string)"/> を使う</b>
    /// （本型は <see cref="IEsBitmap"/> も実装するため読み取り専用としても渡せるが、
    /// 読み取るだけなら書き換えの入口を公開しない型で持つ方が意図が明確になる）。<br/>
    /// <b>読み込みの規則は <see cref="EsBitmap.Load(string)"/> と同じ</b>: 縮小しない・内容から形式を判別する・
    /// アルファ乗算済みの BGRA8888 へ変換する。<br/>
    /// <b>Windows 専用</b>: OS の GDI+ を呼ぶため <see cref="SupportedOSPlatformAttribute"/> で限定する。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static EsWritableBitmap Load(string filePath)
        => LoadCore(filePath, static (width, height) => CreateUninitialized(width, height));

    /// <inheritdoc/>
    /// <remarks>本型が公開するのは書き換えを認められた画像であるため、基底のビューをそのまま公開する。</remarks>
    public Span<byte> WritablePixels => WritableBuffer;

    /// <inheritdoc/>
    /// <remarks>
    /// <b>書き換えてよいのは自分が生成した（または引き取った）画像だけ</b>である。
    /// 借用した画像を書き換えると、その画像を共有する他の表示が壊れる。
    /// 受け取る側は読み取り専用の面（<see cref="IEsBitmap"/>）だけを受け取ること。<br/>
    /// <b>4 バイトを 1 回の書き込みにする</b>: BGRA の並びはメモリ上の <c>uint</c> と同じ（リトルエンディアン）ため、
    /// 成分ごとに 4 回書かずに 1 回で書く（実測: 100 万回で 2.0 ms → 1.5 ms）。
    /// </remarks>
    public unsafe void SetPixel(int x, int y, byte b, byte g, byte r, byte a)
    {
        ThrowIfDisposed();
        EnsureOwnBuffer();
        var p = AddressCore + (y * Stride) + (x * BytesPerPixel);
        *(uint*)p = (uint)(b | (g << 8) | (r << 16) | (a << 24));
    }

    /// <summary>指定座標のピクセルを設定する。</summary>
    /// <param name="point">座標（ピクセル）。</param>
    /// <param name="color">設定する色。</param>
    /// <exception cref="IndexOutOfRangeException">座標が画像の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public void SetPixel(EsPointInt point, Color color)
        => SetPixel(point.X, point.Y, color.B, color.G, color.R, color.A);

    /// <inheritdoc/>
    public bool TrySetPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return false;
        }

        SetPixel(x, y, color.B, color.G, color.R, color.A);
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>範囲の判定と破棄済みの検証は <see cref="TrySetPixel(int, int, Color)"/> が単一所有する（本メソッドは成分へ分解するだけ）。</remarks>
    public bool TrySetPixel(EsPointInt point, Color color) => TrySetPixel(point.X, point.Y, color);

    /// <inheritdoc/>
    public void Clear(byte b, byte g, byte r, byte a) => ClearCore(b, g, r, a);

    /// <inheritdoc/>
    public void Clear(Color color) => ClearCore(color.B, color.G, color.R, color.A);

    /// <summary>本画像（自身）の複製を生成する（書き換え可能な複製）。</summary>
    /// <returns>独立した複製。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>別のオブジェクトから書き換え可能な実体を生成するのは <see cref="Create(IEsBitmap)"/> であり、
    /// 意味が異なる（実装が同じでも片方へ寄せない）。</remarks>
    public override EsWritableBitmap Clone()
    {
        ThrowIfDisposed();

        // 全領域を 1 回のコピーで覆うため、生成時の 0 埋めは行わない
        var result = CreateUninitialized(Width, Height);
        CopyPixels(this, result);
        return result;
    }
}
