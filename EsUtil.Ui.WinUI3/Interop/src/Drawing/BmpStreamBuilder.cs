
namespace EsUtil.Ui.WinUI3.Interop.Drawing;

/// <summary>画素から無圧縮の BMP（24/32 bpp）を組み立てる。</summary>
/// <remarks>
/// <b>用途</b>: Composition の <c>LoadedImageSurface</c> は画像のストリームを受け取る。<br/>
/// PNG で符号化すると符号化と復号の往復が増えるため、<b>画素をそのまま並べた BMP</b> を渡す<br/>
/// （無圧縮であるため、受け取り側は画素を読むだけで済む）。<br/>
/// <br/>
/// <b>形式</b>: ヘッダーは <c>BITMAPFILEHEADER</c>（14 バイト）+ <c>BITMAPINFOHEADER</c>（40 バイト）の 54 バイト。 画素は BGRA8888。<br/>
/// 高さを負にして<b>上から下へ</b>並べる（BMP の既定は下から上であり、負の高さで反転を不要にする）。<br/>
/// <br/>
/// <b>UI に依存しない</b>: 純粋なバイト列の組み立てであるため、ウィンドウ無しで検証できる。
/// </remarks>
public static class BmpStreamBuilder
{
    /// <summary>ヘッダーの大きさ（14 + 40 バイト）。</summary>
    public const int HeaderSize = 54;

    /// <summary>画素から BMP のバイト列を組み立てる。</summary>
    /// <param name="width">幅（ピクセル）。 1 以上。</param>
    /// <param name="height">高さ（ピクセル）。 1 以上。</param>
    /// <param name="pixels">画素（BGRA8888。 <c>幅 × 4 × 高さ</c> バイト。 上から下の順）。</param>
    /// <returns>BMP のバイト列。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が 1 未満の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> の長さが寸法と一致しない場合。</exception>
    public static byte[] Build(int width, int height, ReadOnlySpan<byte> pixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        var stride = width * EsReadOnlyBitmap.BytesPerPixel;
        var expected = (long)stride * height;
        if (pixels.Length != expected)
        {
            throw new ArgumentException(
                $"画素の長さが寸法と一致しません（期待 {expected} バイト / 実際 {pixels.Length} バイト）。",
                nameof(pixels));
        }

        var data = new byte[HeaderSize + stride * height];
        var span = data.AsSpan();

        // BITMAPFILEHEADER
        WriteUInt16(span, 0, 0x4D42);                       // "BM"
        WriteInt32(span, 2, data.Length);                   // ファイル全体の大きさ
        WriteUInt16(span, 6, 0);                            // 予約
        WriteUInt16(span, 8, 0);                            // 予約
        WriteInt32(span, 10, HeaderSize);                   // 画素の開始位置

        // BITMAPINFOHEADER
        WriteInt32(span, 14, 40);                           // ヘッダーの大きさ
        WriteInt32(span, 18, width);
        WriteInt32(span, 22, -height);                      // 負の高さ = 上から下へ
        WriteUInt16(span, 26, 1);                           // プレーン数
        WriteUInt16(span, 28, 32);                          // 1 画素のビット数
        WriteInt32(span, 30, 0);                            // 圧縮なし
        WriteInt32(span, 34, stride * height);              // 画素の大きさ
        WriteInt32(span, 38, 2835);                         // 水平解像度（72 DPI）
        WriteInt32(span, 42, 2835);                         // 垂直解像度（72 DPI）
        WriteInt32(span, 46, 0);                            // パレット数
        WriteInt32(span, 50, 0);                            // 重要色数

        pixels.CopyTo(span[HeaderSize..]);
        return data;
    }

    /// <summary>画像から BMP のバイト列を組み立てる。</summary>
    /// <param name="image">元の画像（読み取りの面）。 null 不可。</param>
    /// <returns>BMP のバイト列。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="image"/> が破棄済みの場合。</exception>
    public static byte[] Build(EsReadOnlyBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        return Build(image.Width, image.Height, image.ReadOnlyPixels);
    }

    /// <summary>16 ビットの値を書き込む（リトルエンディアン）。</summary>
    /// <param name="span">書き込み先。</param>
    /// <param name="offset">位置。</param>
    /// <param name="value">値。</param>
    private static void WriteUInt16(Span<byte> span, int offset, int value)
    {
        span[offset] = (byte)(value & 0xFF);
        span[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    /// <summary>32 ビットの値を書き込む（リトルエンディアン）。</summary>
    /// <param name="span">書き込み先。</param>
    /// <param name="offset">位置。</param>
    /// <param name="value">値。</param>
    private static void WriteInt32(Span<byte> span, int offset, int value)
    {
        span[offset] = (byte)(value & 0xFF);
        span[offset + 1] = (byte)((value >> 8) & 0xFF);
        span[offset + 2] = (byte)((value >> 16) & 0xFF);
        span[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
