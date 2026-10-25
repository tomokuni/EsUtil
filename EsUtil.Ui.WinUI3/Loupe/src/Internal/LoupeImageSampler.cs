using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Ui.WinUI3.Loupe.Internal;

/// <summary>拡大画像（ルーペの 1 面）を最近傍で切り出して BGRA8888 の画素を作る。</summary>
/// <remarks>
/// <b>画素を CPU で作る理由</b>: ルーペの面は 1 辺 160 DIP（125% で 200 画素）程度であり、1 回の更新で
/// 走査する画素は 4 万件ほどである。 GPU の画像変換（Composition のスプライト）に任せるより、
/// <b>入力の画素から直接サンプリングした方が確実で検証できる</b>（スプライトは
/// <c>LoadedImageSurface</c> の非同期読み込みに依存し、読み込みが終わらないと何も表示されない）。
/// <para/>
/// <b>本クラスは WinUI に依存しない</b>: 画素の計算だけを持つため、ウィンドウ無しで検証できる。
/// </remarks>
internal static class LoupeImageSampler
{
    /// <summary>画素 1 個のバイト数（BGRA8888）。</summary>
    public const int BytesPerPixel = 4;

    /// <summary>拡大画像を指定の面へ描く画素を作る。</summary>
    /// <param name="source">拡大元の画像（読み取りのみ）。</param>
    /// <param name="cropSourceRect">切り出す領域（拡大元の画素座標。 画像の外へ出てよい）。</param>
    /// <param name="drawClientPixelRect">描画先の矩形（面の画素座標）。</param>
    /// <param name="surfaceSize">面の一辺（画素）。</param>
    /// <param name="destination">
    /// 書き込み先（<c>surfaceSize × surfaceSize × 4</c> バイト）。 呼び出し側が確保する（毎フレーム確保しない）。
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> の長さが面の寸法と一致しない場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="source"/> が破棄済みの場合。</exception>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 描画先をすべて透明（0）で埋める（画像の外は下地 ＝ ペーストボードが見える）。<br/>
    /// 2. 描画先の各画素について、切り出し領域の対応する画素を最近傍で求める（切り捨て）。<br/>
    /// 3. 拡大元の外へ出る画素は書かない（端の外は下地が見える）。<br/>
    /// <b>アルファは変換しない</b>: 拡大元の BGRA8888（乗算済み）をそのまま写す（画面の撮影結果は不透明）。
    /// </remarks>
    public static void Sample(
        EsReadOnlyBitmap source,
        EsRectInt cropSourceRect,
        EsRect drawClientPixelRect,
        int surfaceSize,
        Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(source.IsDisposed, source);
        ArgumentOutOfRangeException.ThrowIfNegative(surfaceSize);

        var expected = surfaceSize * surfaceSize * BytesPerPixel;
        if (destination.Length != expected)
        {
            throw new ArgumentException(
                $"書き込み先の長さが面の寸法と一致しません（期待 {expected} バイト / 実際 {destination.Length} バイト）。",
                nameof(destination));
        }

        destination.Clear();
        if (surfaceSize == 0 || cropSourceRect.Width <= 0 || cropSourceRect.Height <= 0
            || drawClientPixelRect.Width <= 0 || drawClientPixelRect.Height <= 0)
        {
            return;
        }

        var pixels = source.ReadOnlyPixels;
        var sourceWidth = source.Width;
        var sourceHeight = source.Height;
        var sourceStride = sourceWidth * BytesPerPixel;

        // 面の画素 → 切り出し領域の画素の倍率（描画先の 1 画素が拡大元の何画素に当たるか）
        var stepX = cropSourceRect.Width / drawClientPixelRect.Width;
        var stepY = cropSourceRect.Height / drawClientPixelRect.Height;

        // 描画先の範囲（面の内側へ切り詰める）
        var left = (int)Math.Max(0, Math.Ceiling(drawClientPixelRect.X));
        var top = (int)Math.Max(0, Math.Ceiling(drawClientPixelRect.Y));
        var right = (int)Math.Min(surfaceSize, Math.Ceiling(drawClientPixelRect.Right));
        var bottom = (int)Math.Min(surfaceSize, Math.Ceiling(drawClientPixelRect.Bottom));
        if (right <= left || bottom <= top)
        {
            return;
        }

        for (var y = top; y < bottom; y++)
        {
            var sourceY = cropSourceRect.Y + (int)Math.Floor((y - drawClientPixelRect.Y) * stepY);
            if ((uint)sourceY >= (uint)sourceHeight)
            {
                continue;
            }

            var rowOffset = sourceY * sourceStride;
            var destinationRow = y * surfaceSize * BytesPerPixel;
            for (var x = left; x < right; x++)
            {
                var sourceX = cropSourceRect.X + (int)Math.Floor((x - drawClientPixelRect.X) * stepX);
                if ((uint)sourceX >= (uint)sourceWidth)
                {
                    continue;
                }

                var from = rowOffset + (sourceX * BytesPerPixel);
                var to = destinationRow + (x * BytesPerPixel);
                destination[to] = pixels[from];
                destination[to + 1] = pixels[from + 1];
                destination[to + 2] = pixels[from + 2];
                destination[to + 3] = pixels[from + 3];
            }
        }
    }
}
