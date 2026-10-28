using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win.Wic;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像ファイルの内容を 1 回だけ読み込み、寸法の取得と復号を同じ内容から行う。</summary>
/// <remarks>
/// <b>同じファイルを 2 回読まない</b>: 確保の前に寸法を知りたい場面（縮小して読む大きさを決める等）では、
/// 寸法の取得と復号へ同じ内容を渡せます（読み込みは <see cref="Open"/> の 1 回だけ）。<br/>
/// <b>開いたままにしない</b>: 内容は <see cref="Open"/> でメモリへ取り込むため、ファイルは直ちに解放されます
/// （他のアプリが開いているファイルも読めます）。<br/>
/// <b>復号の結果は呼び出し側のもの</b>です（<see cref="Dispose"/> が手放すのは読み込んだ内容だけです）。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public sealed class EsImageFile : IDisposable
{
    /// <summary>読み込んだファイルの内容（<see cref="Dispose"/> で手放す）。</summary>
    private byte[]? _bytes;

    /// <summary>読み込んだ内容を保持するインスタンスを作る。</summary>
    /// <param name="bytes">ファイルの内容。</param>
    private EsImageFile(byte[] bytes) => _bytes = bytes;

    /// <summary>読み込んだファイルの内容。</summary>
    /// <exception cref="ObjectDisposedException"><see cref="Dispose"/> の後に読んだ場合。</exception>
    private ReadOnlySpan<byte> Bytes => _bytes ?? throw new ObjectDisposedException(nameof(EsImageFile));

    /// <summary>画像ファイルを読み込む（形式は内容から判別する）。</summary>
    /// <param name="filePath">読み込むファイルのパス。</param>
    /// <returns>読み込んだ内容を持つインスタンス（呼び出し側が <see cref="Dispose"/> する）。</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、またはファイルが存在しない場合。</exception>
    /// <remarks><b>この時点では復号しません</b>（寸法も画素も <see cref="GetSize"/> / <see cref="Decode(int)"/> で求めます）。</remarks>
    public static EsImageFile Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return new EsImageFile(WinWicImageCodec.ReadAllBytes(filePath));
    }

    /// <summary>画素を読まずに寸法だけを求める。</summary>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は元の寸法（拡大はしない）。</param>
    /// <returns>復号したときの寸法（EXIF の向きと縮小を反映する）。</returns>
    /// <exception cref="ArgumentException">画像として解釈できない場合。</exception>
    /// <exception cref="ObjectDisposedException"><see cref="Dispose"/> の後に呼んだ場合。</exception>
    /// <remarks>画素を複製しません（デコーダーから寸法だけを読みます）。</remarks>
    public EsSizeInt GetSize(int maxSize = 0) => EsImageCodec.ReadSize(Bytes, maxSize);

    /// <summary>復号した画素（BGRA8888・アルファ乗算済み）と寸法を返す。</summary>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は元の寸法（拡大はしない）。</param>
    /// <returns>復号した画素（密な並び）とその寸法。</returns>
    /// <exception cref="ArgumentException">画像として解釈できない場合。</exception>
    /// <exception cref="ObjectDisposedException"><see cref="Dispose"/> の後に呼んだ場合。</exception>
    public (byte[] Pixels, EsSizeInt Size) Decode(int maxSize = 0) => EsImageCodec.Decode(Bytes, maxSize);

    /// <summary>復号した画素を呼び出し側の領域へ書く（画素を複製しない）。</summary>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は元の寸法（拡大はしない）。</param>
    /// <param name="destination">書き込み先（<c>寸法の幅 × 4 × 高さ</c> バイト以上。 行は密に書く）。</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> が足りない場合、または画像として解釈できない場合。</exception>
    /// <exception cref="ObjectDisposedException"><see cref="Dispose"/> の後に呼んだ場合。</exception>
    public void Decode(int maxSize, Span<byte> destination) => EsImageCodec.Decode(Bytes, maxSize, destination);

    /// <summary>読み込んだ内容を手放す。</summary>
    /// <remarks>二重に呼んでも安全です（2 回目は何もしません）。</remarks>
    public void Dispose() => _bytes = null;
}
