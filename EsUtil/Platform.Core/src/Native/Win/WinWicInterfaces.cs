using System.Runtime.Versioning;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>WIC（Windows Imaging Component）の呼び出しで共通に使う値。</summary>
/// <remarks>
/// <b>COM インターフェイスの定義は型ごとのファイル</b>にある（<see cref="IWICImagingFactory"/> など）。
/// <b>メソッドの並びは <c>wincodec.h</c> の宣言順そのもの</b>である: <c>[GeneratedComInterface]</c> は
/// <b>宣言順を vtable の位置（0〜2 は <c>IUnknown</c>）として扱う</b>ため、使わないメソッドも位置を合わせて宣言する
/// （位置を推測して飛ばさない）。<br/>
/// <b>GUID は <c>wincodec.h</c> の <c>MIDL_INTERFACE</c> と一致させる</b>: 1 文字でも違うと
/// <c>InvalidCastException</c> になり、原因が分かりにくい（実測）。<br/>
/// <b>戻り値は HRESULT のまま</b>（<c>[PreserveSig]</c>）: 呼び出し側が状態値を例外メッセージへ出せるようにする。
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class WinWicInterfaces
{
    /// <summary>エンコーダーのキャッシュ方法: 一時ファイルへ書いてから確定する。</summary>
    /// <remarks>
    /// <b><c>WICBitmapEncoderNoCache</c>（0）は使えない（実測）</b>: メモリ ストリーム
    /// （<c>CreateStreamOnHGlobal</c>）でも <c>WINCODEC_ERR_UNSUPPORTEDOPERATION</c>（<c>0x88982F81</c>）で
    /// 初期化が失敗する。 一時ファイルへ書いてから確定する方法は全形式で成功した。
    /// </remarks>
    internal const int EncoderCacheTempFile = 2;

    /// <summary>スケーラーの補間方法（<c>WICBitmapInterpolationModeFant</c>）。</summary>
    /// <remarks>
    /// Fant は画素の面積平均に近い補間であり、縮小で細線が消えたり濃くなったりしにくい。
    /// 他の UI 版（WinUI 3 の <c>BitmapInterpolationMode.Fant</c>）と同じ補間である。
    /// </remarks>
    internal const int InterpolationFant = 3;

    /// <summary>ファイルを読み取り用に開くアクセス権（<c>GENERIC_READ</c>）。</summary>
    internal const uint GenericRead = 0x80000000;

    /// <summary>プロパティの値の型（<c>VT_UI2</c>）。</summary>
    internal const ushort VariantTypeUnsignedShort = 18;

    /// <summary>プロパティの値の型（<c>VT_UI4</c>）。</summary>
    internal const ushort VariantTypeUnsignedInt = 19;

    /// <summary>方向のメタデータの名前（EXIF の <c>Orientation</c> = 274）。</summary>
    /// <remarks>
    /// 形式によって階層が違うため、この順に試す（JPEG は <c>app1/ifd</c>、TIFF は <c>ifd</c>）。
    /// </remarks>
    internal static readonly string[] OrientationNames =
    [
        "/app1/ifd/{ushort=274}",
        "/ifd/{ushort=274}",
        "/{ushort=274}",
    ];
}
