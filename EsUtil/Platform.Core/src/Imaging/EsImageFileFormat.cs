namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像のファイル形式（保存する形式を明示したい場合に使う）。</summary>
/// <remarks>
/// <b>通常は不要</b>: 保存する形式は<b>保存先の拡張子が決める</b>（<see cref="EsReadOnlyBitmap.Save(string)"/>）。
/// 本 enum は<b>拡張子からは決まらない場合</b>にだけ使う（拡張子の無いパスへ保存する場合や、
/// 拡張子と異なる形式で書く必要がある場合）。<br/>
/// <b>拡張子との対応</b>: 各値は 1 つの拡張子を持つ（<c>.bmp</c> / <c>.png</c> / <c>.jpg</c> / <c>.gif</c> /
/// <c>.tif</c> / <c>.jxr</c> / <c>.heic</c> / <c>.jxl</c>）。 別名（<c>.jpeg</c> / <c>.tiff</c> / <c>.wdp</c> /
/// <c>.heif</c>）も受け付けるが、<b>補完に使うのは別名ではない方</b>である（保存先の一覧は
/// <see cref="EsReadOnlyBitmap.SupportedFileExtensions"/>）。<br/>
/// <b>WIC（Windows Imaging Component）のエンコーダーと 1 対 1 に対応する</b>。 次の 2 群に分かれる。
///
/// <list type="table">
/// <item>
///   <term>Windows 標準（常に使える）</term>
///   <description><see cref="Bmp"/> / <see cref="Png"/> / <see cref="Jpeg"/> / <see cref="Gif"/> /
///   <see cref="Tiff"/> / <see cref="JpegXr"/>。</description>
/// </item>
/// <item>
///   <term>コーデック拡張が必要</term>
///   <description><see cref="Heif"/> / <see cref="JpegXl"/>。 <b>Microsoft Store のコーデック拡張</b>が入っている
///   環境だけ書ける。 入っていない環境では保存時に <see cref="NotSupportedException"/> になる
///   （<see cref="EsReadOnlyBitmap.SupportedFileExtensions"/> からは外れるため、拡張子で保存する場合は
///   一覧に現れない。 <b>本 enum で形式を明示した場合は、その環境でも例外になる</b>）。</description>
/// </item>
/// </list>
///
/// DDS は WIC が扱えるが本ライブラリでは公開しない（理由は <see cref="EsReadOnlyBitmap.Save(string)"/> の備考を参照）。
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

    /// <summary>JPEG XR（<c>.jxr</c> / <c>.wdp</c>。 可逆と不可逆を選べる。 品質は <see cref="EsImageSaveOptions.JpegQuality"/>）。</summary>
    JpegXr,

    /// <summary>HEIF（<c>.heic</c> / <c>.heif</c>。 <b>コーデック拡張が必要</b>。 8x8 未満は書けない）。</summary>
    /// <remarks>
    /// <b>8x8 未満の画像は書けない</b>（HEVC の最小の符号化単位であり、実測で 8x7 以下は読み戻せなかった）。
    /// <b>透過は保存できない</b>（本環境のエンコーダーがアルファ無しの <c>32bppBGR</c> を要求する）。
    /// </remarks>
    Heif,

    /// <summary>JPEG XL（<c>.jxl</c>。 <b>コーデック拡張が必要</b>）。</summary>
    JpegXl,
}