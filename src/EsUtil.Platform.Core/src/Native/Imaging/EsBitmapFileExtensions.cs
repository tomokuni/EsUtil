using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;

namespace EsUtil.Platform.Core.Native.Imaging;

/// <summary>コアの画像のファイル保存・読み込み（<c>System.Drawing.Common</c> を使う Windows 専用）。</summary>
/// <remarks>
/// <b>依存分離</b>: ファイル形式（BMP / PNG / JPEG / GIF / TIFF）のエンコード・デコードは
/// <c>System.Drawing.Common</c>（.NET 標準）が担い、コア本体の依存を閉じる。<br/>
/// <b>形式は拡張子が決める</b>: <see cref="Save(IEsBitmap, string)"/> は保存先の拡張子から形式を選ぶ。
/// <b>未対応の拡張子は例外にする</b>（PNG 等へ黙って差し替えると、呼び出し側が意図した形式と違うファイルが
/// できて気付かない。 対応形式は <see cref="SupportedExtensions"/> が単一所有する）。<br/>
/// <b>Windows 専用</b>: <c>System.Drawing.Common</c> は Windows でのみ動作する（<see cref="SupportedOSPlatformAttribute"/> で限定）。<br/>/// <b>WebP / HEIF を対応形式へ入れない理由（実測）</b>: .NET 10 の <c>System.Drawing.Common</c> は
/// <c>ImageFormat.Webp</c> / <c>ImageFormat.Heif</c> を公開しているが、GDI+ はこれらのエンコーダーを持たない
/// （<c>ImageCodecInfo.GetImageEncoders()</c> が返すのは BMP / JPEG / GIF / TIFF / PNG のみ）。
/// 実際に <c>ImageFormat.Webp</c> を渡した <c>Bitmap.Save</c> は<b>例外を出さずに PNG を書き出す</b>ため、
/// 拡張子だけ <c>.webp</c> の PNG ファイルができる（実測: 書き出したファイルの先頭バイトが PNG のシグネチャ）。<br/>/// <b>制限</b>: JPEG / BMP はアルファ（透明度）を保存できない（形式の仕様）。 透過を保つ場合は PNG を選ぶこと。
/// </remarks>
public static class EsBitmapFileExtensions
{
    /// <summary>保存に使える拡張子（小文字・先頭のドットを含む）。</summary>
    public static readonly IReadOnlyList<string> SupportedExtensions = [".bmp", ".png", ".jpg", ".jpeg", ".gif", ".tif", ".tiff"];

    /// <summary>画像をファイルへ保存する（形式は拡張子が決める）。</summary>
    /// <param name="bitmap">保存する画像。 借用であり破棄しない。</param>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> または <paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="SupportedExtensions"/>）。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="bitmap"/> が破棄済みの場合。</exception>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合。</exception>
    /// <remarks>
    /// 保存形式は拡張子から決める（<c>.bmp</c> = Windows Bitmap / <c>.png</c> / <c>.jpg</c> / <c>.jpeg</c> /
    /// <c>.gif</c> / <c>.tif</c> / <c>.tiff</c>）。<br/>
    /// <b>JPEG / BMP はアルファを保存できない</b>（透過を保つ場合は PNG を選ぶ）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static void Save(this IEsBitmap bitmap, string filePath)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var format = ResolveFormat(filePath);
        using var drawing = bitmap.ToDrawingBitmap();
        drawing.Save(filePath, format);
    }

    /// <summary>ファイルから画像を読み込む。</summary>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。</param>
    /// <returns>読み込んだ画像（読み取り専用）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合（GDI+ の仕様でこの例外になる）。</exception>
    /// <remarks>
    /// <b>縮小読み込みはしない</b>（元の寸法でデコードする。 大きな画像を小さく使う場合は
    /// <c>Resize</c> で縮小すること。 サムネイルのようにデコード自体を小さくしたい場合は、各 UI の
    /// コーデック（縮小デコード対応）を使う方が速い）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static EsBitmap Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var drawing = new DrawingBitmap(filePath);
        return drawing.ToEsBitmap();
    }

    /// <summary>保存先の拡張子から保存形式を決める。</summary>
    /// <param name="filePath">保存先のファイルパス。</param>
    /// <returns>System.Drawing の画像形式。</returns>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合。</exception>
    [SupportedOSPlatform("windows")]
    private static DrawingImageFormat ResolveFormat(string filePath)
        => Path.GetExtension(filePath).ToUpperInvariant() switch
        {
            ".BMP" => DrawingImageFormat.Bmp,
            ".PNG" => DrawingImageFormat.Png,
            ".JPG" or ".JPEG" => DrawingImageFormat.Jpeg,
            ".GIF" => DrawingImageFormat.Gif,
            ".TIF" or ".TIFF" => DrawingImageFormat.Tiff,
            var other => throw new NotSupportedException(
                $"拡張子 '{other}' へは保存できません。 対応形式: {string.Join(" / ", SupportedExtensions)}"),
        };
}
