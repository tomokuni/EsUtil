using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EsUtil.Platform.Core.Imaging;
using AvaloniaColor = Avalonia.Media.Color;
using Color = System.Drawing.Color;

// 複製元: EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging（サンプルが単独で動くための意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Marquee.AvaloniaUI.Sample.Internal;

/// <summary><see cref="EsReadOnlyBitmap"/> / <see cref="EsBitmap"/> の Avalonia 向け生成・変換拡張。</summary>
/// <remarks>
/// <b>依存分離</b>: 本クラスは Avalonia との変換を担い、<see cref="EsReadOnlyBitmap"/> 本体（コア）の依存を閉じる。<br/>
/// <br/>
/// <b>権利の向き</b>: 生成（<c>ToEsBitmap</c> / <c>CreateAvaloniaWriteableBitmap</c>）は書き換え可能な画像を作る。
/// 変換（<c>ToAvaloniaBitmap</c> / <c>ToAvaloniaWriteableBitmap</c>）は読み取り専用の面（<see cref="EsReadOnlyBitmap"/>）を受け取る
/// （複製を返すため入力の書き換え権限は要らない）。<br/>
/// <b>所有権</b>: 返された Avalonia の画像は呼び出し側が所有する（破棄する）。
/// </remarks>
internal static class EsBitmapAvaloniaExtensions
{
    // ====================================================================
    // 生成・変換 (Avalonia)
    // ====================================================================

    /// <summary>単色の Avalonia WriteableBitmap を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 正の値。</param>
    /// <param name="height">高さ（ピクセル）。 正の値。</param>
    /// <param name="color">塗りつぶし色（Avalonia Color。 アルファ乗算して書き込む）。</param>
    /// <returns>単色の <see cref="WriteableBitmap"/>（所有権は呼び出し側）。</returns>
    /// <remarks>
    /// テスト・サンプルで重複していた <c>new WriteableBitmap(...)</c> + 色塗りループを一元化する。<br/>
    /// <see cref="EsBitmap.Create(int, int, Color)"/> + <see cref="ToAvaloniaWriteableBitmap"/> で構築する。
    /// 生成に Skia（Avalonia ヘッドレス）が必要。<br/>
    /// <b>命名の理由</b>: コアの <c>Create</c>（<see cref="EsBitmap"/> を返す）と区別するため、
    /// 表示境界で <see cref="WriteableBitmap"/> を返す本メソッドは <c>CreateAvaloniaWriteableBitmap</c> とする。
    /// </remarks>
    public static WriteableBitmap CreateAvaloniaWriteableBitmap(int width, int height, AvaloniaColor color)
    {
        // Avalonia Color を System.Drawing.Color へ変換してコアの生成へ委譲する
        var colorArgb = Color.FromArgb(color.A, color.R, color.G, color.B);
        using var bitmap = EsBitmap.Create(width, height, colorArgb);
        // その場で読むだけ（読み取り専用の面へ引き渡す）: 凍結して 0 コピーで共有する
        bitmap.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bitmap);
        return read.ToAvaloniaWriteableBitmap();
    }

    /// <summary>単色の Avalonia 画像（表示境界の <see cref="IImage"/>）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 正の値。</param>
    /// <param name="height">高さ（ピクセル）。 正の値。</param>
    /// <param name="color">塗りつぶし色（Avalonia Color。 アルファ乗算して書き込む）。</param>
    /// <returns>単色の画像を包むアダプタ（<b>破棄は呼び出し側</b>）。</returns>
    /// <remarks>
    /// テスト・サンプルで多用する単色画像を、表示境界の <see cref="IImage"/> として簡便に生成する。<br/>
    /// 画像はコアの <see cref="EsBitmap"/> で作り、アダプタが所有する（<see cref="EsBitmapAvaloniaAdapter.Adopt"/>）。<br/>
    /// 表示用の変換は<b>初回の描画で 1 回だけ</b>行う（ピクセルの複製は 1 枚につき 1 回）。
    /// </remarks>
    public static EsBitmapAvaloniaAdapter CreateAvaloniaImage(int width, int height, AvaloniaColor color)
    {
        var colorArgb = Color.FromArgb(color.A, color.R, color.G, color.B);
        // 新しく生成した画像を解放する人がいないため、所有する入口（Adopt）で包む
        return EsBitmapAvaloniaAdapter.Adopt(EsBitmap.Create(width, height, colorArgb));
    }

    /// <summary>Avalonia Bitmap からラッパーを生成する（複製するため元は変更されない）。</summary>
    /// <param name="bitmap">変換元の Avalonia Bitmap。</param>
    /// <returns>生成された画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <c>Bitmap.CopyPixels</c>（ネイティブ memcpy 相当）で、コアのピクセル バッファへ直接コピーする。<br/>
    /// 中間バッファ（byte[]）を介さないため高速・低アロケーション。 PNG エンコード/デコードや System.Drawing を経由しない（可逆）。
    /// </remarks>
    public static unsafe EsBitmap ToEsBitmap(this Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var w = bitmap.PixelSize.Width;
        var h = bitmap.PixelSize.Height;

        // CopyPixels は矩形の全行を埋めるため、0 埋めは行わない
        return EsBitmap.CreateFilled(w, h, dst =>
        {
            var dstStride = w * EsReadOnlyBitmap.BytesPerPixel;
            fixed (byte* p = dst)
            {
                // CopyPixels は宛先バッファを stride のピッチで埋める。 コアのバッファを直接宛先にし、中間 byte[] を排除する
                bitmap.CopyPixels(
                    new PixelRect(0, 0, w, h),
                    (nint)p,
                    checked(dstStride * h),
                    dstStride);
            }
        });
    }

    /// <summary>Avalonia WriteableBitmap を返す（複製を所有するため安全に使用できる）。</summary>
    /// <param name="bitmap">対象の画像。 借用であり破棄しない。</param>
    /// <returns>変換された Avalonia WriteableBitmap。 所有権は呼び出し側。</returns>
    /// <remarks>
    /// コアのピクセルを複製して Avalonia へ渡すため、返された Bitmap の破棄は呼び出し側が所有する。
    /// </remarks>
    public static unsafe WriteableBitmap ToAvaloniaWriteableBitmap(this EsReadOnlyBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        // 寸法を一度だけ取得し、PixelSize 生成と行ループの両方で共有する（値型の生成を削減）
        var size = bitmap.Size;
        var height = size.Height;
        var pixelSize = new PixelSize(size.Width, height);
        var vector = new Vector(96, 96);
        var wb = new WriteableBitmap(pixelSize, vector, PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = wb.Lock();
        var dstStride = fb.RowBytes;
        var srcStride = bitmap.Stride;
        var rowBytes = Math.Min(srcStride, dstStride);
        fixed (byte* src = bitmap.ReadOnlyPixels)
        {
            var dst = (byte*)fb.Address;
            if (srcStride == dstStride)
            {
                // ストライドが同一の場合は連続領域なので、行単位ではなく全領域を 1 回でコピーする（高速化）
                var totalBytes = rowBytes * height;
                Buffer.MemoryCopy(src, dst, totalBytes, totalBytes);
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    Buffer.MemoryCopy(src + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
                }
            }
        }

        return wb;
    }

    /// <summary>公開コンストラクタで Avalonia の標準 <see cref="Bitmap"/> を生成する。</summary>
    /// <param name="bitmap">対象の画像。 借用であり破棄しない。</param>
    /// <returns>標準描画経路（<c>IImage</c>）で確実に描画される <see cref="Bitmap"/>。 所有権は呼び出し側。</returns>
    /// <remarks>
    /// <see cref="Bitmap"/> の公開コンストラクタへコアのピクセル バッファを直接渡して生成する。<br/>
    /// Skia の内部実装がピクセルを複製するためコピーは発生するが、<c>WriteableBitmap</c> の <c>Lock()</c> オーバーヘッドがなく、
    /// <c>Bitmap</c> として標準経路で確実に描画される。<br/>
    /// <b>注意</b>: 変換時にピクセルを<b>複製する</b>ため、その後コアの画像を書き換えても本 <see cref="Bitmap"/> には反映されない（スナップショット）。
    /// </remarks>
    public static unsafe Bitmap ToAvaloniaBitmap(this EsReadOnlyBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        fixed (byte* src = bitmap.ReadOnlyPixels)
        {
            return new Bitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Premul,
                (nint)src,
                new PixelSize(bitmap.Width, bitmap.Height),
                new Vector(96, 96),
                bitmap.Stride);
        }
    }

    /// <summary>指定座標（Avalonia PixelPoint）へ Avalonia Color を書き込む。</summary>
    /// <param name="bitmap">対象の画像（<b>書き換えるため <see cref="EsBitmap"/> を受ける</b>）。</param>
    /// <param name="point">座標（ピクセル）。</param>
    /// <param name="color">設定する Avalonia Color。 アルファ乗算して書き込む。</param>
    public static void SetPixel(this EsBitmap bitmap, PixelPoint point, AvaloniaColor color)
        => bitmap.SetPixel(point.X, point.Y, color.B, color.G, color.R, color.A);

    /// <summary>0 コピーのビューから Avalonia の標準 <see cref="Bitmap"/> を生成する（<b>切り出しの複製を作らない</b>）。</summary>
    /// <param name="view">対象のビュー（切り出す領域を指す）。 借用であり破棄しない。</param>
    /// <returns>標準描画経路（<c>IImage</c>）で確実に描画される <see cref="Bitmap"/>。 所有権は呼び出し側。</returns>
    /// <remarks>
    /// <b>行幅（<see cref="EsPixmap.Stride"/>）をそのまま渡します</b>: <see cref="EsPixmap.ReadOnlyPixels"/> は
    /// <b>行の余白を含む</b>連続ビューであるため、<c>幅 × 4</c> を渡すと行がずれます（元画像の幅で切り出した場合だけ一致します）。<br/>
    /// <b>用途</b>: 一部だけを表示用へ変換する場面。 先に <see cref="EsReadOnlyBitmap.Crop(EsUtil.Platform.Core.Geometry.EsRectInt)"/> で切り出すと、
    /// 表示用の変換がピクセルを複製する前に<b>切り出しの複製</b>を払うことになります。<br/>
    /// 生成した <see cref="Bitmap"/> の座標はビューの左上を原点とします（元画像の座標ではありません）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="view"/> が破棄済みの場合。</exception>
    public static unsafe Bitmap ToAvaloniaBitmap(this EsPixmap view)
    {
        ArgumentNullException.ThrowIfNull(view);

        fixed (byte* src = view.ReadOnlyPixels)
        {
            return new Bitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Premul,
                (nint)src,
                new PixelSize(view.Width, view.Height),
                new Vector(96, 96),
                view.Stride);
        }
    }
}