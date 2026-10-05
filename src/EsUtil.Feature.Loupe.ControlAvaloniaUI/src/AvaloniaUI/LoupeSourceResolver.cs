using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;

namespace EsUtil.Feature.Loupe.AvaloniaUI;

/// <summary>ルーペのソース画像（<see cref="Bitmap"/> / <see cref="EsBitmapAvaloniaImage"/>）から切り出し元の解決ロジック（純粋関数）。</summary>
/// <remarks>
/// <see cref="LoupeControl.GenerateAsync"/> のソース解決（型判定・破棄済み判定・<see cref="EsBitmap"/> 取得）を<br/>
/// UI 非依存の純粋ロジックとして提供する。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupeSourceResolver
{
    /// <summary>ソース解決の結果。</summary>
    /// <param name="Bitmap">切り出し元の EsBitmap。 未対応・破棄済み・null の場合は null。</param>
    /// <param name="IsSourceBitmap">ソースが <see cref="Bitmap"/> 由来か（複製破棄が必要か）。 false の場合は共有参照（破棄しない）。</param>
    public readonly record struct Result(EsBitmap? Bitmap, bool IsSourceBitmap);

    /// <summary>ソース画像がルーペで対応可能な型（<see cref="Bitmap"/> / <see cref="EsBitmapAvaloniaImage"/>）かどうかを判定する。</summary>
    /// <param name="source">判定するソース画像。</param>
    /// <returns>対応可能なら true。 null は「画像なし」として対応可能、未対応型は false。</returns>
    /// <remarks>
    /// <see cref="LoupeRenderPipeline"/> の未対応型判定（<see cref="NotSupportedException"/>）を単一所有する（DRY）。<br/>
    /// null は「画像なし」を表すため対応可能（市松模様のみで描画する）として扱う。
    /// </remarks>
    public static bool IsSourceSupported(IImage? source)
        => source is null or Bitmap or EsBitmapAvaloniaImage;

    /// <summary>ソース画像から切り出し元の EsBitmap を解決する。</summary>
    /// <param name="source">ルーペのソース画像（<see cref="Bitmap"/> / <see cref="EsBitmapAvaloniaImage"/>）。</param>
    /// <param name="sourceBitmap">解決した切り出し元。 未対応・破棄済み・null の場合は null。</param>
    /// <returns>破棄済み・未対応型の場合は false（市松模様のみで描画する）。 それ以外は true。</returns>
    /// <remarks>
    /// <see cref="Bitmap"/> は複製（<c>ToEsBitmap()</c>）を返し、破棄は呼び出し側が行う（<see cref="Result.IsSourceBitmap"/> = true）。<br/>
    /// <see cref="EsBitmapAvaloniaImage"/> は共有参照を返し、破棄しない（<see cref="Result.IsSourceBitmap"/> = false）。<br/>
    /// 破棄済み <see cref="Bitmap"/> は <c>PixelSize</c> 参照で検出する（<see cref="ObjectDisposedException"/> 防止）。<br/>
    /// 対応可能型（null / <see cref="Bitmap"/> / <see cref="EsBitmapAvaloniaImage"/>）以外は null を返す（市松模様のみで描画する）。
    /// </remarks>
    public static Result Resolve(IImage? source, out EsBitmap? sourceBitmap)
    {
        sourceBitmap = null;

        if (source is null)
        {
            return new Result(null, IsSourceBitmap: false);
        }

        if (source is Bitmap sourceBitmapImage)
        {
            try
            {
                _ = sourceBitmapImage.PixelSize;
            }
            catch (ObjectDisposedException)
            {
                // 破棄済み Source: 市松模様のみで描画する（前回のビットマップは維持）
                return new Result(null, IsSourceBitmap: false);
            }

            sourceBitmap = sourceBitmapImage.ToEsBitmap();
            return new Result(sourceBitmap, IsSourceBitmap: true);
        }

        if (source is EsBitmapAvaloniaImage unifiedImage)
        {
            sourceBitmap = unifiedImage.Bitmap;
            return new Result(sourceBitmap, IsSourceBitmap: false);
        }

        // 対応可能型は上で全て処理済み。 未対応型（IImage の他の実装）は切り出し元を解決せず null を返す。
        return new Result(null, IsSourceBitmap: false);
    }
}
