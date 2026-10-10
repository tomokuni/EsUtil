using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using Color = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>読み取り専用の画像（借用して読む側へ渡す型）。</summary>
/// <remarks>
/// <b>目的</b>: 「この画像は読み取るだけで、書き換えもされない」ことを<b>型で表す</b>。
/// 生成した側は本型で保持し、利用者へは本型（または <see cref="IEsBitmap"/>）で渡す。<br/>
/// <br/>
/// <b>書き換えの入口を持たない</b>: <c>SetPixel</c> / <c>Clear</c> / <c>WritablePixels</c> を公開しない
/// （書き換えが要る場合は <see cref="EsWritableBitmap"/> を使う）。 取り違えは <c>as EsWritableBitmap</c> で検出できる
/// （本型の実体は <see cref="EsWritableBitmap"/> へ変換できない。 兄弟であり互いの継承関係が無いため）。<br/>
/// <b>破棄</b>: <see cref="IDisposable"/> を実装する（大きな画像を早く手放せるようにするため）。
/// ただし<b>借用した画像を破棄してはならない</b>（破棄するのは生成した側である）。
/// 解放の実装は <see cref="SafeAlignedBuffer"/> が単一所有するため、破棄を忘れても GC が解放する。<br/>
/// <b>複製</b>: <see cref="Clone"/> は本型（読み取り専用）を返す。 書き換え可能な複製が要る場合は
/// <see cref="EsWritableBitmap.Create(EsBitmap)"/> を使う。
/// </remarks>
public sealed class EsBitmap : EsBitmapBase, IDisposable
{
    /// <summary>指定サイズの空の画像（BGRA8888・全ピクセル透明）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    public static EsBitmap Create(int width, int height)
        => new(width, height);

    /// <summary>指定矩形サイズの空の画像（BGRA8888・全ピクセル透明）を生成する。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    public static EsBitmap Create(EsSizeInt size)
        => Create(size.Width, size.Height);

    /// <summary>指定サイズの画像（BGRA8888）を生成し、全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="color">塗りつぶし色。</param>
    /// <returns>生成された画像（全ピクセルが指定色）。 所有権は呼び出し側へ移る。</returns>
    public static EsBitmap Create(int width, int height, Color color)
    {
        // 直後に全領域を塗るため、生成時の 0 埋めは行わない
        var bitmap = new EsBitmap(width, height, clear: false);
        bitmap.ClearCore(color.B, color.G, color.R, color.A);
        return bitmap;
    }

    /// <summary>指定矩形サイズの画像（BGRA8888）を生成し、全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="size">サイズ（幅・高さを使用）。</param>
    /// <param name="color">塗りつぶし色。</param>
    /// <returns>生成された画像（全ピクセルが指定色）。 所有権は呼び出し側へ移る。</returns>
    public static EsBitmap Create(EsSizeInt size, Color color)
        => Create(size.Width, size.Height, color);

    /// <summary>借用した画像を複製し、読み取り専用の画像を生成する。</summary>
    /// <param name="source">複製元の画像。 借用であり破棄しない。</param>
    /// <returns>独立した複製（読み取り専用）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>書き換え可能な複製が要る場合</b>は <see cref="EsWritableBitmap.Create(EsBitmap)"/>（権利を変える複製）を使う。<br/>
    /// <b>本メソッドが要る理由</b>: 本型は書き換えの入口を公開しないため、外部（相互変換の拡張メソッド等）が
    /// 生のピクセルから読み取り専用の画像を組み立てられない。 複製の手順を本型が単一所有する
    /// （ピクセルをコピーするため、生成元が書き換え中でないことを呼び出し側が保証すること）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    public static EsBitmap Create(IEsBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(source.IsDisposed, source);

        // 全領域を 1 回のコピーで覆うため、生成時の 0 埋めは行わない
        var result = new EsBitmap(source.Width, source.Height, clear: false);
        source.Pixels.CopyTo(result.WritableBuffer);
        return result;
    }

    /// <summary>ファイルから読み取り専用の画像を読み込む（形式は内容から判別される）。</summary>
    /// <param name="filePath">読み込むファイルのパス。 null 不可。</param>
    /// <returns>読み込んだ画像（読み取り専用）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合。</exception>
    /// <remarks>
    /// <b>書き換えが要る場合は <see cref="EsWritableBitmap.Load(string)"/> を使う</b>
    /// （本メソッドで読み込んでから <see cref="EsWritableBitmap.Create(EsBitmap)"/> で複製すると、
    /// 2 回目の全域コピー（実測: 4K で 5.7 ms / 31 MiB）が無駄になる）。<br/>
    /// <b>縮小読み込みはしない</b>（元の寸法でデコードする。 小さく使う場合は <see cref="EsBitmapExtensions.Resize"/> で縮小する）。<br/>
    /// <b>形式は内容から判別する</b>（拡張子と内容が一致しない場合は内容に従う）。<br/>
    /// <b>ピクセルはアルファ乗算済みの BGRA8888 へ変換して取り込む</b>（元の形式が異なっても GDI+ が変換する）。<br/>
    /// <b>Windows 専用</b>: OS の GDI+ を呼ぶため <see cref="SupportedOSPlatformAttribute"/> で限定する。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static EsBitmap Load(string filePath)
        => LoadCore(filePath, static (width, height) => new EsBitmap(width, height, clear: false));

    /// <summary>指定サイズのピクセル バッファを確保する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="clear">true の場合は全領域を 0（透明）で初期化する。 直後に全領域を書く場合は false。</param>
    private EsBitmap(int width, int height, bool clear = true)
        : base(width, height, clear)
    {
    }

    /// <summary>確保と充填を 1 回で行う（書き換え可能な側と同じ入口の形にする）。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="fill">確保したバッファ（全領域）を充填する処理。 生成の直後に 1 回だけ呼ぶ。</param>
    /// <returns>生成された画像（読み取り専用）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>2 回のコピーを 1 回にするための入口</b>: 外部（相互変換）が「書き換え可能な画像を作ってから
    /// <see cref="Create(IEsBitmap)"/> で読み取り専用へ複製する」と、同じ寸法のバッファを 2 つ作って
    /// <b>全領域を 2 回コピーする</b>（実測: 4K で 13.6 ms 対 6.7 ms。 1 回のコピーで約半分になる）。
    /// 本メソッドは確保したバッファをそのまま埋めさせるため、中間の画像を作らない。<br/>
    /// <b>書き込みのビューは外へ出ない</b>: <paramref name="fill"/> へ渡すスパンは本メソッドの外へ持ち出せない
    /// （<see cref="Span{T}"/> は <c>ref struct</c> のため、ラムダが捕獲できない）。
    /// したがって「読み取り専用の実体に書き込みの入口を作らない」という不変条件は保たれる。<br/>
    /// <b><see cref="EsWritableBitmap.CreateFilled"/> と対にする理由</b>: 同じ手順（外部のピクセルをコアの画像へ写す）が
    /// 権利の違いだけで 2 通りの読み方になるのを避けるためです。 相互変換（System.Drawing / Avalonia / Skia）は
    /// どちらの権利の画像も作るため、<b>同じ名前・同じ引数の形の入口があると変換の実装が同じ形で読めます</b>。<br/>
    /// <b>書かない領域を残さないこと</b>: 生成時の 0 埋めは行わない（透明が必要な領域は <paramref name="fill"/> が消す）。
    /// </remarks>
    public static EsBitmap CreateFilled(int width, int height, Action<Span<byte>> fill)
    {
        ArgumentNullException.ThrowIfNull(fill);

        // 全領域を充填する前提で確保するため、生成時の 0 埋めは行わない
        var result = new EsBitmap(width, height, clear: false);
        fill(result.WritableBuffer);
        return result;
    }

    /// <summary>本画像の複製を生成する（読み取り専用の複製）。</summary>
    /// <returns>独立した複製。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>書き換え可能な複製が要る場合は <see cref="EsWritableBitmap.Create(EsBitmap)"/> を使う。</remarks>
    public override EsBitmap Clone()
    {
        ThrowIfDisposed();

        // 全領域を 1 回のコピーで覆うため、生成時の 0 埋めは行わない
        var result = new EsBitmap(Width, Height, clear: false);
        CopyPixels(this, result);
        return result;
    }

    /// <summary>ピクセル バッファを解放する（冪等）。</summary>
    /// <remarks>解放の実装は <see cref="SafeAlignedBuffer.ReleaseHandle"/> が単一所有する。</remarks>
    public void Dispose() => ReleaseBuffer();
}
