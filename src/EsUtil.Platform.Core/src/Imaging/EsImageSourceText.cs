namespace EsUtil.Platform.Core.Imaging;

/// <summary><see cref="IEsBitmap"/> の寸法を文字列へ整形する唯一の入口。</summary>
/// <remarks>
/// ログ等へ画像の寸法を出す書式（<c>幅x高さ</c>）を単一所有する。<br/>
/// <see cref="IEsBitmap"/> の実装（<see cref="EsBitmap"/> / <see cref="EsWritableBitmap"/> と UI の表示アダプタ）が増えても
/// 書式は 1 つのままになる（実装ごとに書式を持つと、同じ画像が経路によって違う文字列で記録される）。<br/>
/// <b>未設定（null）は <c>null</c></b> と表す（呼び出し側が文字列を決めない）。
/// </remarks>
public static class EsImageSourceText
{
    /// <summary>画像の寸法を「幅x高さ」形式の文字列へ整形する。</summary>
    /// <param name="source">対象の画像。 null 可。</param>
    /// <returns>寸法の文字列（例: <c>1920x1080</c>）。 <paramref name="source"/> が null の場合は <c>null</c>。</returns>
    public static string ToSizeText(IEsBitmap? source)
        => source is null ? "null" : $"{source.Width}x{source.Height}";
}
