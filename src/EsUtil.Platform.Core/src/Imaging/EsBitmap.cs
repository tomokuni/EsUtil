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

    /// <summary>指定サイズのピクセル バッファを確保する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="clear">true の場合は全領域を 0（透明）で初期化する。 直後に全領域を書く場合は false。</param>
    private EsBitmap(int width, int height, bool clear = true)
        : base(width, height, clear)
    {
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
