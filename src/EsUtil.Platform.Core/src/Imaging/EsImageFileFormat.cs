namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像のファイル形式（保存する形式を明示したい場合に使う）。</summary>
/// <remarks>
/// <b>通常は不要</b>: 保存する形式は<b>保存先の拡張子が決める</b>（<see cref="EsBitmapExtensions.Save(IEsBitmap, string)"/>）。
/// 本 enum は<b>拡張子からは決まらない場合</b>にだけ使う（拡張子の無いパスへ保存する場合や、
/// 拡張子と異なる形式で書く必要がある場合）。<br/>
/// <b>拡張子との対応</b>: 各値は 1 つの拡張子を持つ（<c>.bmp</c> / <c>.png</c> / <c>.jpg</c> / <c>.gif</c> / <c>.tif</c>）。
/// 別名（<c>.jpeg</c> / <c>.tiff</c>）も受け付けるが、<b>補完に使うのは別名ではない方</b>である
/// （保存先の一覧は <see cref="EsBitmapBase.SupportedFileExtensions"/>）。<br/>
/// <b>GDI+ のエンコーダーと 1 対 1 に対応する</b>（5 形式。 WebP / HEIF を持たない理由は
/// <see cref="EsBitmapExtensions.Save(IEsBitmap, string)"/> の備考を参照）。
/// </remarks>
public enum EsImageFileFormat
{
    /// <summary>Windows Bitmap（<c>.bmp</c>。 無圧縮）。</summary>
    Bmp,

    /// <summary>PNG（<c>.png</c>。 可逆圧縮）。</summary>
    Png,

    /// <summary>JPEG（<c>.jpg</c>。 不可逆圧縮。 品質は <see cref="EsImageSaveOptions.JpegQuality"/> で指定する）。</summary>
    Jpeg,

    /// <summary>GIF（<c>.gif</c>。 256 色。 画素は減色される）。</summary>
    Gif,

    /// <summary>TIFF（<c>.tif</c>。 可逆圧縮）。</summary>
    Tiff,
}
