namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary>WIC（Windows Imaging Component）の呼び出しで使う定数。</summary>
/// <remarks>
/// <b>列挙値は CsWin32 が生成する型（<c>WICDecodeOptions</c> / <c>WICBitmapTransformOptions</c> など）を使う</b>ため、
/// ここに置くのは<b>WIC の型になっていない値</b>だけである。<br/>
/// <b>EXIF の方向の名前は形式で階層が違う</b>（JPEG は <c>app1/ifd</c>、TIFF は <c>ifd</c>）ため、この順に試す。
/// </remarks>
internal static class WinWicConstants
{
    /// <summary>HEIF が書ける最小の寸法（ピクセル）。</summary>
    /// <remarks>
    /// <b>8x8 未満は書けない（実測）</b>: 保存自体は成功するが、書き出したファイルを読み戻すと
    /// <c>WINCODEC_ERR_INVALIDPARAMETER</c>（<c>0x80070057</c>）になる（HEVC の最小の符号化単位）。
    /// </remarks>
    internal const int HeifMinimumSize = 8;

    /// <summary>HEIF のコンテナ形式の GUID（実測値）。</summary>
    /// <remarks>
    /// <b><c>wincodec.h</c> に無い</b>: HEIF のコーデック拡張（Store アプリ）が登録する GUID であり、
    /// 本環境の列挙（<c>CreateComponentEnumerator</c>）で得た値を持つ。 <b>この形式だけは寸法の下限がある</b>ため、
    /// 拡張子ではなくコンテナで判定できるようにここへ置く。
    /// </remarks>
    internal static readonly Guid HeifContainer = new("E1E62521-6787-405B-A339-500715B5763F");

    /// <summary>EXIF の方向（<c>Orientation</c> = 274）の名前（試す順）。</summary>
    internal static readonly string[] OrientationNames =
    [
        "/app1/ifd/{ushort=274}",
        "/ifd/{ushort=274}",
        "/{ushort=274}",
    ];
}
