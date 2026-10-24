using EsUtil.Platform.Core.Common;

namespace EsUtil.Platform.Core.Imaging;

/// <summary><see cref="EsImageFileFormat"/> と拡張子・表示名の対応（保存形式の規則の単一所有者）。</summary>
/// <remarks>
/// <b>補完に使う拡張子は形式ごとに 1 つに固定する</b>: 同じ形式に複数の拡張子があり（<c>.jpeg</c> / <c>.jpe</c> /
/// <c>.jpg</c> など）、<b>OS が申告する並びは保証されない</b>ため、補完に使う名前は本クラスが決める。<br/>
/// <b>表示名は OS に依らない</b>: WIC のエンコーダーは <c>PNG Encoder</c> のような英語名を返すため、
/// 画面に出す名前は本クラスが持つ（実行環境で表示が変わらない）。
/// </remarks>
internal static class EsImageFileFormatExtensions
{
    /// <summary>形式の代表の拡張子を求める。</summary>
    /// <param name="format">形式。</param>
    /// <returns>代表の拡張子（小文字・先頭のドットを含む）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">定義されていない値の場合。</exception>
    internal static string ToExtension(this EsImageFileFormat format) => format switch
    {
        EsImageFileFormat.Bmp => ".bmp",
        EsImageFileFormat.Png => ".png",
        EsImageFileFormat.Jpeg => ".jpg",
        EsImageFileFormat.Gif => ".gif",
        EsImageFileFormat.Tiff => ".tif",
        EsImageFileFormat.JpegXr => ".jxr",
        EsImageFileFormat.Heif => ".heic",
        EsImageFileFormat.JpegXl => ".jxl",
        var other => throw new ArgumentOutOfRangeException(nameof(format), other, "定義されていない画像形式です。"),
    };

    /// <summary>形式の表示名を求める。</summary>
    /// <param name="format">形式。</param>
    /// <returns>画面に出す名前（例: <c>PNG</c> / <c>JPEG XR</c>）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">定義されていない値の場合。</exception>
    internal static string ToDisplayName(this EsImageFileFormat format) => format switch
    {
        EsImageFileFormat.Bmp => "BMP",
        EsImageFileFormat.Png => "PNG",
        EsImageFileFormat.Jpeg => "JPEG",
        EsImageFileFormat.Gif => "GIF",
        EsImageFileFormat.Tiff => "TIFF",
        EsImageFileFormat.JpegXr => "JPEG XR",
        EsImageFileFormat.Heif => "HEIF",
        EsImageFileFormat.JpegXl => "JPEG XL",
        var other => throw new ArgumentOutOfRangeException(nameof(format), other, "定義されていない画像形式です。"),
    };

    /// <summary>文字列から形式を解釈する。</summary>
    /// <param name="text">拡張子（<c>.png</c> / <c>png</c>。 大文字・小文字は区別しない）または形式の名前（<c>Png</c>）。</param>
    /// <param name="format">解釈した形式。 解釈できなかった場合は <see cref="EsImageFileFormat.Png"/>。</param>
    /// <returns>解釈できた場合は true。</returns>
    /// <remarks>
    /// <b>拡張子を先に見る</b>（<c>.jpg</c> は拡張子として扱う）。 数値文字列は受理しない
    /// （列挙に無い値を通さない。 判断は <see cref="EnumCanonical"/> と同じ方針）。
    /// </remarks>
    internal static bool TryParse(string? text, out EsImageFileFormat format)
    {
        format = EsImageFileFormat.Png;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var extension = trimmed.StartsWith('.') ? trimmed : string.Concat(".", trimmed);
        foreach (var candidate in Enum.GetValues<EsImageFileFormat>())
        {
            if (string.Equals(candidate.ToExtension(), extension, StringComparison.OrdinalIgnoreCase))
            {
                format = candidate;
                return true;
            }
        }

        // 拡張子で解決できなければ形式の名前として解釈する（数値は列挙に無い値を通さないため拒否する）
        if (char.IsAsciiDigit(trimmed[0]))
        {
            return false;
        }

        if (EnumCanonical.TryParse(trimmed, out EsImageFileFormat parsed))
        {
            format = parsed;
            return true;
        }

        // 失敗した場合は既定（Png）を残す（EnumCanonical は失敗時に default（Bmp）を書き戻す）
        return false;
    }
}
