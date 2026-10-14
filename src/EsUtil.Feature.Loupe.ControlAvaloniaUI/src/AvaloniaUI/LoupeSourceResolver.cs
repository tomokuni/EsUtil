using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using EsUtil.Feature.Loupe.AvaloniaUI.Internal;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.Loupe.AvaloniaUI;

/// <summary>ルーペのソース画像（<see cref="Bitmap"/> / コアの画像を内包する表示アダプタ）から切り出し元の解決ロジック（純粋関数）。</summary>
/// <remarks>
/// <see cref="LoupeControl"/> のソース解決（型判定・破棄済み判定・<see cref="EsReadOnlyBitmap"/> 取得）を<br/>
/// UI 非依存の純粋ロジックとして提供する。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupeSourceResolver
{
    /// <summary>表示アダプタがコアの画像を公開するメソッド名（複製されたアダプタ間で共有する公開契約）。</summary>
    private const string BorrowMethodName = "GetEsBitmap";

    /// <summary>型ごとの「コアの画像の取り出し手段」（<see cref="BorrowMethodName"/> を持たない型は null）。</summary>
    /// <remarks>
    /// <b>型ではなく契約で判定する理由</b>: 表示アダプタは独立パッケージとして配布できるよう、各アセンブリが<b>同じ実装を
    /// internal で複製</b>して所有する（<c>.github/依存関係.md</c> §4.1）。 ルーペは自アセンブリの複製しか参照できないため、
    /// ホストが設定する他の複製（表示アダプタを公開する層の型）を型で判定できない。<br/>
    /// そのため公開契約（<see cref="BorrowMethodName"/>）で判定し、<b>型ごとに 1 回だけ</b>解決してキャッシュする
    /// （描画ごとの再探索を避ける）。
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, Func<IImage, EsReadOnlyBitmap>?> s_borrowers = new();

    /// <summary>ソース解決の結果。</summary>
    /// <param name="Size">切り出し元の寸法（ピクセル）。 描けない場合（画像なし・未対応型・破棄済み）は null。</param>
    /// <param name="Bitmap">切り出し元のコアの画像（借用。 表示用の射影だけを持つソースでは null）。</param>
    /// <param name="IsSourceBitmap">返したコアの画像が複製かどうか（true の場合、破棄は呼び出し側が行う）。</param>
    public readonly record struct Result(EsSizeInt? Size, EsReadOnlyBitmap? Bitmap, bool IsSourceBitmap);

    /// <summary>ソース画像がルーペで対応可能な型かどうかを判定する。</summary>
    /// <param name="source">判定するソース画像。</param>
    /// <returns>対応可能なら true。 null は「画像なし」として対応可能、未対応型は false。</returns>
    /// <remarks>
    /// 対応する型は <see cref="Bitmap"/>（<b>派生の <see cref="WriteableBitmap"/> / <see cref="RenderTargetBitmap"/> を含む</b>）と
    /// <b>コアの画像を内包する表示アダプタ</b>（<see cref="BorrowMethodName"/> を公開する <see cref="IImage"/>）である
    /// （ピクセル抽出が可能な型だけを受け入れる）。<br/>
    /// 未対応型の判定（<see cref="NotSupportedException"/>）を単一所有する（DRY）。<br/>
    /// null は「画像なし」を表すため対応可能（下地のみで描画する）として扱う。<br/>
    /// <b>継承による包含に依存する</b>ため、新しい対応型を足すときは <see cref="Bitmap"/> の派生かどうかを実測する。<br/>
    /// （実測: Avalonia 12 の <see cref="WriteableBitmap"/> / <see cref="RenderTargetBitmap"/> は <see cref="Bitmap"/> の派生、<br/>
    /// <c>CroppedBitmap</c> は <c>AvaloniaObject</c> の派生で非対応）
    /// </remarks>
    public static bool IsSourceSupported(IImage? source)
        => source is null or Bitmap || TryGetBorrower(source) is not null;

    /// <summary>ソース画像がルーペで対応可能な型であることを確かめる（未対応は例外）。</summary>
    /// <param name="source">判定するソース画像。 null は「画像なし」として許す。</param>
    /// <exception cref="NotSupportedException"><paramref name="source"/> が対応していない型の場合。</exception>
    /// <remarks>
    /// 対応する型の判定（<see cref="IsSourceSupported"/>）を単一所有する（例外の送出もここに閉じる）。<br/>
    /// 例外の記録（落とさない扱い）は呼び出し側の描画経路が行う。
    /// </remarks>
    public static void EnsureSupported(IImage? source)
    {
        if (!IsSourceSupported(source))
        {
            throw new NotSupportedException(
                $"LoupeControl は {source!.GetType().FullName} をサポートしていません。Bitmap（WriteableBitmap を含む）または EsReadOnlyBitmap（表示アダプタ）を指定してください。");
        }
    }

    /// <summary>ソース画像の切り出し元（寸法と、必要ならコアの画像）を解決する。</summary>
    /// <param name="source">ルーペのソース画像（<see cref="Bitmap"/> / コアの画像を内包する表示アダプタ）。</param>
    /// <returns>切り出し元の寸法と（表示用の射影だけでは足りない場合の）コアの画像。 画像なし・未対応型・破棄済みは寸法 null。</returns>
    /// <remarks>
    /// <see cref="Bitmap"/>（派生の <see cref="WriteableBitmap"/> / <see cref="RenderTargetBitmap"/> を含む）は、
    /// <b>表示用の射影（<see cref="IImage"/>）が切り出し元と同じ座標系で使える場合（寸法が 1 DIP = 1 ピクセル）は複製を作らない</b>
    /// （<see cref="Result.Size"/> だけを返す。 表示は元画像を元矩形つきで描けるため、切り出し元の画素は要らない）。<br/>
    /// 射影をそのまま使えない場合（DPI が 96 以外の Bitmap 等）だけ複製を読み取り専用の面へ移して返す<br/>
    /// （破棄は呼び出し側が行う。 <see cref="Result.IsSourceBitmap"/> = true）。<br/>
    /// 表示アダプタ（コアの画像を内包する <see cref="IImage"/>）は借用（<see cref="BorrowMethodName"/>）を返し、破棄しない<br/>
    /// （<see cref="Result.IsSourceBitmap"/> = false）。<br/>
    /// 破棄済み（または破棄済みの真実を内包する）ソースは <c>PixelSize</c> 参照で検出する（<see cref="ObjectDisposedException"/> 防止）。<br/>
    /// 対応可能型（null / 表示アダプタ / <see cref="Bitmap"/>）以外は寸法 null を返す（下地のみで描画する）。
    /// </remarks>
    public static Result Resolve(IImage? source)
    {
        if (source is null)
        {
            return new Result(null, null, IsSourceBitmap: false);
        }

        if (source is Bitmap sourceBitmapImage)
        {
            PixelSize pixelSize;
            try
            {
                pixelSize = sourceBitmapImage.PixelSize;
            }
            catch (ObjectDisposedException)
            {
                // 破棄済み Source: 下地のみで描画する（前回のビットマップは維持）
                return new Result(null, null, IsSourceBitmap: false);
            }

            var size = new EsSizeInt(pixelSize.Width, pixelSize.Height);
            if (IsSameCoordinateSpace(sourceBitmapImage, pixelSize))
            {
                // 表示用の射影をそのまま使える: コアの画像へ複製しない
                // （複製は全画素のコピーであり、再描画のたびに画面全体のコピーを払うことになる）
                return new Result(size, null, IsSourceBitmap: false);
            }

            using (var created = sourceBitmapImage.ToEsBitmap())
            {
                // その場で読むだけ（読み取り専用の面へ渡す）: 凍結して 0 コピーで共有し、書き換え可能の実体は解放する
                created.SetReadOnly();
                return new Result(size, EsReadOnlyBitmap.FromBitmap(created), IsSourceBitmap: true);
            }
        }

        // 画像源（借用）を判定する（Bitmap はコアの画像を取り出す契約を持たないため両者は排他）
        if (TryGetBorrower(source) is { } borrow)
        {
            EsReadOnlyBitmap borrowed;
            try
            {
                borrowed = borrow(source);
            }
            catch (ObjectDisposedException)
            {
                // 破棄済みの真実を内包する Source: 下地のみで描画する（前回のビットマップは維持）
                return new Result(null, null, IsSourceBitmap: false);
            }

            var size = borrowed.Size;
            return new Result(size, borrowed, IsSourceBitmap: false);
        }

        // 対応可能型は上で全て処理済み。 未対応型（IImage の他の実装）は切り出し元を解決せず寸法 null を返す。
        return new Result(null, null, IsSourceBitmap: false);
    }

    /// <summary>表示用の射影（<see cref="IImage"/>）が切り出し元と同じ座標系（1 DIP = 1 ピクセル）で使えるかどうかを判定する。</summary>
    /// <param name="bitmap">判定する画像。</param>
    /// <param name="pixelSize">画像の寸法（ピクセル）。</param>
    /// <returns>そのまま切り出し元の座標系として使える場合は true。</returns>
    /// <remarks>
    /// 描画 API へ渡す元矩形（切り出し領域）は画像の座標系（DIP）で解釈されるため、<br/>
    /// 座標系とピクセルが一致する場合だけ元矩形をそのまま使える（解釈が一致しない場合はコアの画像から描く）。<br/>
    /// DPI が 96 以外の <see cref="Bitmap"/> は一致しない（<c>Size</c> は DIP、<c>PixelSize</c> はピクセル）。
    /// </remarks>
    private static bool IsSameCoordinateSpace(Bitmap bitmap, PixelSize pixelSize)
        => (int)bitmap.Size.Width == pixelSize.Width && (int)bitmap.Size.Height == pixelSize.Height;

    /// <summary>表示アダプタからコアの画像を取り出す手段を取得する（型ごとに 1 回だけ解決する）。</summary>
    /// <param name="source">判定するソース画像。</param>
    /// <returns>取り出し手段。 <see cref="BorrowMethodName"/> を公開しない型の場合は null。</returns>
    /// <remarks>
    /// 判定する契約は「引数なしの public な <see cref="BorrowMethodName"/> が <see cref="EsReadOnlyBitmap"/> を返す」ことである。<br/>
    /// 合わない型は「コアの画像を内包しない表示画像」として未対応のまま扱う。
    /// </remarks>
    private static Func<IImage, EsReadOnlyBitmap>? TryGetBorrower(IImage source)
        => s_borrowers.GetOrAdd(source.GetType(), static type =>
        {
            var method = type.GetMethod(
                BorrowMethodName,
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                Type.EmptyTypes,
                modifiers: null);

            if (method is null || !typeof(EsReadOnlyBitmap).IsAssignableFrom(method.ReturnType))
            {
                return null;
            }

            // 呼び出しはデリゲートへ変換する（<see cref="MethodInfo.Invoke(object, object?[])"/> は対象の例外を
            // <see cref="TargetInvocationException"/> で包むため、破棄済みの判定が呼び出し側で型により変わる）
            var image = Expression.Parameter(typeof(IImage), "image");
            return Expression.Lambda<Func<IImage, EsReadOnlyBitmap>>(
                Expression.Call(Expression.Convert(image, type), method),
                image).Compile();
        });
}
