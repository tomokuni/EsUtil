using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRect = System.Drawing.Rectangle;

namespace EsUtil.Platform.Core.Native.Imaging;

/// <summary>
/// <see cref="EsBitmap"/> / <see cref="EsWritableBitmap"/> の System.Drawing 向け生成・変換拡張（Windows 専用）。
/// </summary>
/// <remarks>
/// <b>依存分離</b>: 本クラスは System.Drawing との変換を担い、<see cref="EsBitmap"/> 本体（コア）の依存を閉じる。<br/>
/// <br/>
/// <b>権利の向き</b>: 生成（<see cref="ToEsBitmap"/>) は書き換え可能な画像を作る（充填するため）。
/// 変換（<see cref="ToDrawingBitmap"/>) は読み取り専用の面（<see cref="IEsBitmap"/>）を受け取る（複製を返すため
/// 入力の書き換え権限は要らない）。<br/>
/// <b>所有権</b>: <see cref="ToDrawingBitmap"/> は複製を返す（呼び出し側が所有）。<br/>
/// <b>Windows 専用</b>: System.Drawing は Windows でのみ利用可能（<see cref="SupportedOSPlatformAttribute"/> で限定）。
/// </remarks>
public static class EsBitmapDrawingExtensions
{
    /// <summary>System.Drawing.Bitmap からラッパーを生成する（複製するため元は変更されない）。</summary>
    /// <param name="bitmap">変換元の System.Drawing.Bitmap。</param>
    /// <returns>生成された画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// System.Drawing は Windows 専用のため、本メソッドは Windows でのみ利用可能。<br/>
    /// 全行をコピーする場合は生成時の 0 埋めを省く（大きなキャプチャ画像の書き込み量を減らす）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static EsWritableBitmap ToEsBitmap(this DrawingBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var w = bitmap.Width;
        var h = bitmap.Height;
        var data = bitmap.LockBits(new DrawingRect(0, 0, w, h), DrawingImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            var srcStride = data.Stride;
            var dstStride = w * IEsBitmap.BytesPerPixel;
            var rowBytes = Math.Min(dstStride, srcStride);

            // コピー元のストライドがデスティネーション（幅×4）以上なら全行が覆われるため、0 埋めを省く
            var buffer = srcStride >= dstStride
                ? EsWritableBitmap.CreateUninitialized(w, h)
                : EsWritableBitmap.Create(w, h);

            var dst = buffer.WritablePixels;
            unsafe
            {
                var src = (byte*)data.Scan0;
                if (srcStride == dstStride)
                {
                    // ストライドが同一の場合は連続領域なので、行単位ではなく全領域を 1 回でコピーする（高速化）
                    new ReadOnlySpan<byte>(src, rowBytes * h).CopyTo(dst);
                }
                else
                {
                    for (var y = 0; y < h; y++)
                    {
                        new ReadOnlySpan<byte>(src + y * srcStride, rowBytes).CopyTo(dst.Slice(y * dstStride, rowBytes));
                    }
                }
            }

            return buffer;
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
            var src = bitmap.Pixels;
            var srcStride = bitmap.Stride;
            var dstStride = data.Stride;
            var rowBytes = Math.Min(srcStride, dstStride);
            unsafe
            {
                fixed (byte* p = src)
                {
                    var dst = (byte*)data.Scan0;
                    if (srcStride == dstStride)
                    {
                        // ストライドが同一の場合は連続領域なので、行単位ではなく全領域を 1 回でコピーする（高速化）
                        Buffer.MemoryCopy(p, dst, rowBytes * height, rowBytes * height);
                    }
                    else
                    {
                        for (var y = 0; y < height; y++)
                        {
                            Buffer.MemoryCopy(p + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
                        }
                    }
                }
            }

            return sd;
        }
        finally
        {
            sd.UnlockBits(data);
        }
    }
}
