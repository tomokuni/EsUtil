namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像ファイルの拡張子の判定（一覧に画像を含めるかの判断）。</summary>
/// <remarks>
/// <b>拡張子は大文字・小文字を区別しない</b>（<c>.PNG</c> も画像として扱う）。<br/>
/// <b>保存できる形式とは目的が違う</b>: ここは「一覧に並べる対象か」であり、
/// <see cref="EsReadOnlyBitmap.SupportedFileExtensions"/>（保存できる形式）より広い
/// （<c>.webp</c> は一覧には並べるが、本ライブラリでは保存しない）。<br/>
/// <b>4 つの UI 版で同じ判定を使う</b>ため最下層が単一所有する（UI ごとに判定がずれると、
/// 同じフォルダーを開いても並ぶファイルが変わる）。
/// </remarks>
public static class EsImageFileExtensions
{
    /// <summary>一覧に含める画像ファイルの拡張子（先頭のドットを含む）。</summary>
    /// <remarks>
    /// <b>拡張子の判定は OS のデコーダーの有無に依らない</b>（一覧に並べる判断と、読み書きできるかの判断は別である）。
    /// <c>.webp</c> や <c>.heic</c> は、コーデック拡張が入っていない環境では読み込みに失敗するが、
    /// <b>一覧には並べる</b>（ユーザーが「画像が 1 枚も無い」と誤解しないため）。
    /// </remarks>
    private static readonly HashSet<string> s_all = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".webp",
        ".jxr", ".wdp", ".heic", ".heif", ".jxl",
    };

    /// <summary>一覧に含める画像ファイルの拡張子を取得する。</summary>
    /// <value>先頭のドットを含む拡張子の集合（読み取り専用のビュー）。</value>
    public static IReadOnlyCollection<string> All => s_all;

    /// <summary>指定されたパスが画像ファイルかどうかを判定する。</summary>
    /// <param name="path">判定するパス（拡張子だけを使う）。</param>
    /// <returns>一覧に含める画像の場合は true。 拡張子が無い・対象外の場合は false。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> が null の場合。</exception>
    public static bool IsImageFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return s_all.Contains(Path.GetExtension(path));
    }
}
