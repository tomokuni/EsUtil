using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingBitmapData = System.Drawing.Imaging.BitmapData;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRect = System.Drawing.Rectangle;

namespace EsUtil.Platform.Core.Windows.Imaging;

/// <summary>
/// <see cref="EsBitmap"/> / <see cref="EsWritableBitmap"/> の System.Drawing 向け生成・変換拡張（Windows 専用）。
/// </summary>
/// <remarks>
/// <b>依存分離</b>: 本クラスは System.Drawing との変換を担い、<see cref="EsBitmap"/> 本体（コア）の依存を閉じる。<br/>
/// <br/>
/// <b>権利の向き</b>: 生成（<see cref="ToEsBitmap"/> / <see cref="ToEsWritableBitmap"/>) はコピーを作り
/// （元の <see cref="DrawingBitmap"/> は変更しない。 書き換えが要るかでどちらを使うかが決まる）、
/// 変換（<see cref="ToDrawingBitmap"/>) は読み取り専用の面（<see cref="IEsBitmap"/>）を受け取る（複製を返すため
/// 入力の書き換え権限は要らない）。<br/>
/// <b>所有権</b>: 返り値はすべて複製であり、呼び出し側が所有する（引数の所有権は呼び出し側に残る）。<br/>
/// <b>Windows 専用</b>: System.Drawing は Windows でのみ利用可能（<see cref="SupportedOSPlatformAttribute"/> で限定）。
/// </remarks>
public static class EsBitmapDrawingExtensions
{
    /// <summary>System.Drawing.Bitmap から読み取り専用の画像を生成する（複製するため元は変更されない）。</summary>
    /// <param name="bitmap">変換元の System.Drawing.Bitmap。</param>
    /// <returns>生成された画像（読み取り専用）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>書き換えが要る場合は <see cref="ToEsWritableBitmap"/> を使う</b>。<br/>
    /// <b>1 回のコピーで作る</b>: 書き換え可能な画像を経由するとバッファを 2 つ作って全領域を 2 回コピーするため、
    /// 読み取り専用の画像へ直接 1 回だけ書き込む（実測: 4K で 13.6 ms → 6.7 ms）。<br/>
    /// System.Drawing は Windows 専用のため、本メソッドは Windows でのみ利用可能。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static EsBitmap ToEsBitmap(this DrawingBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var width = bitmap.Width;
        var height = bitmap.Height;
        return EsBitmap.CreateFilled(width, height, dst => CopyFromBitmap(bitmap, dst, width * IEsBitmap.BytesPerPixel));
    }

    /// <summary>System.Drawing.Bitmap から書き換え可能な画像を生成する（複製するため元は変更されない）。</summary>
    /// <param name="bitmap">変換元の System.Drawing.Bitmap。</param>
    /// <returns>生成された画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// System.Drawing は Windows 専用のため、本メソッドは Windows でのみ利用可能。<br/>
    /// 全行をコピーする場合は生成時の 0 埋めを省く（大きなキャプチャ画像の書き込み量を減らす）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static EsWritableBitmap ToEsWritableBitmap(this DrawingBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var width = bitmap.Width;
        var height = bitmap.Height;
        var dstStride = width * IEsBitmap.BytesPerPixel;
        var data = Lock(bitmap);
        try
        {
            return EsWritableBitmap.CreateFilled(width, height, dst =>
            {
                // コピー元の行幅がコピー先より小さい場合は、覆われない右端を透明にする
                if (data.Stride < dstStride)
                {
                    dst.Clear();
                }

                CopyLocked(data, dst, dstStride, height);
            });
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    /// <summary>System.Drawing.Bitmap（Format32bppPArgb）を返す（複製を所有する）。</summary>
    /// <param name="bitmap">対象の画像。 借用であり破棄しない。</param>
    /// <returns>変換された System.Drawing.Bitmap。</returns>
    /// <remarks>System.Drawing は Windows 専用のため、本メソッドは Windows でのみ利用可能。</remarks>
    [SupportedOSPlatform("windows")]
    public static DrawingBitmap ToDrawingBitmap(this IEsBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var width = bitmap.Width;
        var height = bitmap.Height;
        var sd = new DrawingBitmap(width, height, DrawingPixelFormat.Format32bppPArgb);
        var data = sd.LockBits(new DrawingRect(0, 0, width, height), DrawingImageLockMode.WriteOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            CopyToLocked(bitmap, data, width * IEsBitmap.BytesPerPixel, height);
            return sd;
        }
        finally
        {
            sd.UnlockBits(data);
        }
    }

    /// <summary>System.Drawing.Bitmap を読み取り専用でロックする。</summary>
    /// <param name="bitmap">対象の Bitmap。</param>
    /// <returns>ロックしたピクセル データ。 使い終わったら <c>UnlockBits</c> する。</returns>
    [SupportedOSPlatform("windows")]
    private static DrawingBitmapData Lock(DrawingBitmap bitmap)
        => bitmap.LockBits(
            new DrawingRect(0, 0, bitmap.Width, bitmap.Height),
            DrawingImageLockMode.ReadOnly,
            DrawingPixelFormat.Format32bppPArgb);

    /// <summary>System.Drawing.Bitmap のピクセルをコアの画像のバッファへコピーする。</summary>
    /// <param name="bitmap">変換元の Bitmap（未ロック）。</param>
    /// <param name="destination">コピー先（<c>dstStride × 高さ</c> バイト以上）。</param>
    /// <param name="dstStride">コピー先の行幅（<c>幅 × 4</c>）。</param>
    /// <remarks>
    /// <b>上書きされない右端を透明にする</b>: 元の行幅がコピー先より小さい場合は、行ごとに不足する分を 0 にする
    /// （元の Bitmap の仕様上ありうる。 行単位のコピーでは覆われない領域が残るため）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static void CopyFromBitmap(DrawingBitmap bitmap, Span<byte> destination, int dstStride)
    {
        var data = Lock(bitmap);
        try
        {
            if (data.Stride < dstStride)
            {
                destination.Clear();
            }

            CopyLocked(data, destination, dstStride, bitmap.Height);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    /// <summary>ロック済みの System.Drawing のピクセルをコアの画像のバッファへコピーする（コピーの実体）。</summary>
    /// <param name="data">ロック済みのピクセル データ。</param>
    /// <param name="destination">コピー先（<c>dstStride × height</c> バイト以上）。</param>
    /// <param name="dstStride">コピー先の行幅（<c>幅 × 4</c>）。</param>
    /// <param name="height">コピーする行数。</param>
    /// <remarks>
    /// <b>経路は 2 つ</b>: 行幅が一致する場合は連続領域として全領域を 1 回でコピーし、一致しない場合は行単位で
    /// コピーする。 行単位の場合、行の残り（<c>dstStride − min</c>）は呼び出し側が 0 にする。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static void CopyLocked(DrawingBitmapData data, Span<byte> destination, int dstStride, int height)
    {
        var srcStride = data.Stride;
        var rowBytes = Math.Min(dstStride, srcStride);
        unsafe
        {
            var src = (byte*)data.Scan0;
            if (srcStride == dstStride)
            {
                // 行幅が同一の場合は連続領域なので、行単位ではなく全領域を 1 回でコピーする（高速化）
                new ReadOnlySpan<byte>(src, rowBytes * height).CopyTo(destination);
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    new ReadOnlySpan<byte>(src + (y * srcStride), rowBytes)
                        .CopyTo(destination.Slice(y * dstStride, rowBytes));
                }
            }
        }
    }

    /// <summary>コアの画像のピクセルを、ロック済みの System.Drawing のバッファへコピーする。</summary>
    /// <param name="bitmap">コピー元の画像。</param>
    /// <param name="data">ロック済みのコピー先。</param>
    /// <param name="srcStride">コピー元の行幅（<c>幅 × 4</c>）。</param>
    /// <param name="height">コピーする行数。</param>
    [SupportedOSPlatform("windows")]
    private static void CopyToLocked(IEsBitmap bitmap, DrawingBitmapData data, int srcStride, int height)
    {
        var src = bitmap.Pixels;
        var dstStride = data.Stride;
        var rowBytes = Math.Min(srcStride, dstStride);
        unsafe
        {
            fixed (byte* p = src)
            {
                var dst = (byte*)data.Scan0;
                if (srcStride == dstStride)
                {
                    // 行幅が同一の場合は連続領域なので、行単位ではなく全領域を 1 回でコピーする（高速化）
                    Buffer.MemoryCopy(p, dst, rowBytes * height, rowBytes * height);
                }
                else
                {
                    for (var y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(p + (y * srcStride), dst + (y * dstStride), dstStride, rowBytes);
                    }
                }
            }
        }
    }
}
