using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

// 複製元: EsUtil.SaveEvidence.Platform.Skia（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Loupe.MewUI.Internal;

/// <summary>コアの <see cref="EsReadOnlyBitmap"/> と Skia の <see cref="SKBitmap"/> を相互変換する拡張メソッド。</summary>
/// <remarks>
/// <b>なぜ本層が持つか</b>: Skia は UI フレームワークではないが<b>外部パッケージ</b>であるため、<br/>
/// 「外部パッケージへ依存しない」を判定基準とする <c>EsUtil.Platform.Core</c> には置けない。<br/>
/// Skia への依存は本層へ閉じ込め、コアは自前のピクセルバッファだけを所有する。<br/>
/// <br/>
/// <b>所有権</b>: 双方向とも<b>コアのバッファの所有権は移動しない</b>。<br/>
/// - <see cref="ToSKBitmap"/> は<b>非所有ビュー</b>を返す（コアのバッファを指すだけ。 破棄してもコアのバッファは解放されない）。<br/>
/// - <see cref="ToEsBitmap(SKBitmap)"/> は<b>複製</b>を作る（引数の <see cref="SKBitmap"/> の所有権は呼び出し側に残る）。<br/>
/// <br/>
/// <b>ビューの寿命</b>: <see cref="ToSKBitmap"/> が返す <see cref="SKBitmap"/> は、対応する画像が<br/>
/// 生存している間だけ有効である（コアのバッファを指すため）。 画像を破棄した後にビューを使うと<br/>
/// 解放済みメモリを読む。
/// </remarks>
internal static class EsBitmapSkiaExtensions
{
    /// <summary>コアの画像を指す非所有ビューを返す。</summary>
    /// <param name="bitmap">対象の画像（読み取るだけのため <see cref="EsReadOnlyBitmap"/> を受ける）。 null 不可。</param>
    /// <returns>同じピクセル バッファを指す <see cref="SKBitmap"/>（非所有）。 <b>呼び出し側が破棄する</b>（破棄してもコアのバッファは解放されない）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> が null の場合。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="bitmap"/> が破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">ビューの作成に失敗した場合。</exception>
    /// <remarks>
    /// ビューは元の画像の生存中のみ有効である（ピクセルをコピーしないため）。<br/>
    /// <b>破棄は呼び出し側が行う</b>（本メソッドは呼び出しごとに新しい <see cref="SKBitmap"/> を作る）。
    /// 破棄しないと <see cref="SKBitmap"/> のファイナライザ キューへ積まれ、描画のたびに不要な GC 負荷になる
    /// （実測: ビュー生成は 0.3〜2 µs だが、破棄しない場合は毎回 112 B のファイナライザ対象が残る）。<br/>
    /// ビューの破棄はコアのピクセルを解放しない（非所有のため）。 コピーが必要な場合は結果へ <c>Copy()</c> を適用する。
    /// </remarks>
    public static SKBitmap ToSKBitmap(this EsReadOnlyBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var view = new SKBitmap();

        // コアのバッファをそのまま指す（ピクセルをコピーしない非所有ビュー）。
        // バッファはアンマネージドのため、fixed で得たアドレスは生存中動かない（InstallPixels が保持しても有効）
        unsafe
        {
            fixed (byte* p = bitmap.ReadOnlyPixels)
            {
                if (!view.InstallPixels(info, (nint)p, bitmap.Stride))
                {
                    view.Dispose();
                    throw new InvalidOperationException("SKBitmap のビューを作成できませんでした（ピクセル形式または寸法が不正です）。");
                }
            }
        }

        return view;
    }

    /// <summary>Skia のビットマップを複製してコアの画像を生成する。</summary>
    /// <param name="bitmap">元のビットマップ（BGRA8888・アルファ乗算済み）。 null 不可。</param>
    /// <returns>複製された画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> が null の場合。</exception>
    /// <remarks>
    /// 引数の所有権は呼び出し側に残る（本メソッドは引数を破棄しない）。 不要になった時点で呼び出し側が破棄する。<br/>
    /// 元のビットマップが BGRA8888・アルファ乗算済みでない場合は、Skia が変換せずにバイト列を写すため<br/>
    /// 成分順が一致しない（呼び出し側で形式を保証する）。<br/>
    /// 全行をコピーできる場合は 0 埋めを省く（大きな画像の書き込み量を減らす）。
    /// </remarks>
    public static EsBitmap ToEsBitmap(this SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var srcStride = bitmap.RowBytes;
        var dstStride = bitmap.Width * EsReadOnlyBitmap.BytesPerPixel;
        var rowBytes = Math.Min(srcStride, dstStride);
        var height = bitmap.Height;

        return EsBitmap.CreateFilled(bitmap.Width, height, dst =>
        {
            unsafe
            {
                var src = (byte*)bitmap.GetPixels();
                if (src is null)
                {
                    // ピクセルを持たないビットマップ（表像なし）は透明で埋める（不定値が残るのを避ける）
                    dst.Clear();
                    return;
                }

                if (srcStride < dstStride)
                {
                    // 行幅がコピー先より小さい形式では、覆われない右端を透明にする
                    dst.Clear();
                }

                for (var y = 0; y < height; y++)
                {
                    new ReadOnlySpan<byte>(src + (y * srcStride), rowBytes).CopyTo(dst.Slice(y * dstStride, rowBytes));
                }
            }
        });
    }
}
