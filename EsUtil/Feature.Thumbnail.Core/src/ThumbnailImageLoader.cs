using System.Runtime.Versioning;
using EsUtil.Platform.Core.Caching;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core;

namespace EsUtil.Feature.Thumbnail;

/// <summary>画像ファイルを縮小して一覧用のサムネイルとして読み込むローダー。</summary>
/// <remarks>
/// <b>縮小はデコード時に行う</b>: コアの <see cref="EsReadOnlyBitmap.Load(string, int)"/> へ長辺の上限を渡し、
/// WIC のスケーラーで<b>デコードしながら縮小する</b>（原寸のバッファを確保しない）。 4K の画像を多数並べても
/// 使用量が増えない。<br/>
/// <b>画質は 1 つに揃える</b>: 補間は Fant（画素の面積平均に近い）であり、UI 版ごとに補間が違うことがない
/// （過去は GDI+ のバイキュービック・Skia の縮小と混在していた）。<br/>
/// <b>EXIF の向きを適用する</b>: 回転した JPEG も正しい向きで読める（コアの <see cref="EsReadOnlyBitmap.Load(string, int)"/>
/// が回転してから縮小する）。<br/>
/// <b>キャッシュ</b>: 同一ファイル・同一サイズの結果を共有する。 ファイルの更新日時をキーに含めるため、
/// 内容が変われば再デコードされる。 容量管理は <see cref="CapacityLimitedCache{TKey, TValue}"/> が単一所有し、
/// 上限を超えると <see cref="EsReadOnlyBitmap"/> が破棄されてから消える
/// （そのため上限は呼び出し側が表示する件数を渡す）。<br/>
/// <b>失敗時</b>: 存在しない・非画像・破損などの場合は null を返す（例外にしない。 一覧を壊さない）。<br/>
/// <b>スレッド</b>: 背景の生成からも呼べる（キャッシュの上限判定・登録は直列化される。 デコードは並列のまま）。<br/>
/// <b>本層に置く理由</b>: 画素の型（<see cref="EsReadOnlyBitmap"/>）だけで完結して UI に依存せず、
/// 「画像でなければ null」「上限は表示件数」という<b>一覧の意味論</b>を持つ（汎用の画像 API へ混ぜると意味が広がりすぎる）。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public static class ThumbnailImageLoader
{
    /// <summary>デコード結果のキャッシュ（キー: パス・要求サイズ・更新日時）。</summary>
    private static readonly CapacityLimitedCache<(string Path, int Size, DateTime LastWrite), EsReadOnlyBitmap?> s_cache =
        new(static key => Decode(key.Path, key.Size));

    /// <summary>画像ファイルを読み込み、長辺が指定サイズを超えないサムネイルを返す。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="maxSize">サムネイルの長辺の上限（ピクセル）。 0 以下は 1 として扱う。</param>
    /// <param name="capacity">キャッシュの最大保持数（一覧が表示する件数を渡す）。 1 未満は 1 として扱う。</param>
    /// <returns>サムネイル画像。 読み込めない場合は null。</returns>
    /// <remarks>
    /// 1. 画像の拡張子でなければ読み込まずに null を返す。<br/>
    /// 2. キャッシュにあればそれを返す（キャッシュの値は本クラスが所有し、呼び出し側は破棄しない）。<br/>
    /// 3. 無ければ縮小してキャッシュへ登録する。<br/>
    /// <b>保持数を呼び出し側が渡す理由</b>: 一覧が表示する件数より小さい上限だと、生成の途中で表示中の
    /// サムネイルが破棄される（上限超過時はキャッシュが保持する値を破棄するため、描画が破棄済みの
    /// バッファーへ触れる）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    public static EsReadOnlyBitmap? Load(string filePath, int maxSize, int capacity)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!EsImageFileExtensions.IsImageFile(filePath))
        {
            return null;
        }

        var size = Math.Max(1, maxSize);
        s_cache.Capacity = Math.Max(1, capacity);

        try
        {
            var key = (Path: filePath, Size: size, LastWrite: File.GetLastWriteTimeUtc(filePath));
            return s_cache.GetOrAdd(key);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // アクセス拒否・削除競合など、ファイルの状態による失敗は「表示できない」として扱う
            return null;
        }
    }

    /// <summary>画像ファイルを縮小してデコードする。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 1 以上。</param>
    /// <returns>デコードした画像。 デコードできない場合は null。</returns>
    /// <remarks>
    /// <b>失敗は例外にしない</b>: 非画像・破損・削除競合は「表示できない」として null を返す。
    /// </remarks>
    private static EsReadOnlyBitmap? Decode(string filePath, int maxSize)
    {
        try
        {
            return EsReadOnlyBitmap.Load(filePath, maxSize);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 非画像・破損・削除競合は「表示できない」として扱う（一覧を壊さない）
            return null;
        }
    }
}
