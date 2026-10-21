using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary><c>IWICImagingFactory</c>（WIC のイメージング ファクトリ）。</summary>
/// <remarks>
/// <b>宣言は <c>CreateStream</c> まで</b>である（それより後ろのメソッドは使わないため宣言しない。
/// 位置は宣言順で決まるため、途中を飛ばさずに並べる）。
/// </remarks>
[GeneratedComInterface]
[Guid("EC5EC8A9-C395-4314-9C77-54D7A935FF70")]
internal unsafe partial interface IWICImagingFactory
{
    /// <summary>ファイルからデコーダーを作る。</summary>
    /// <param name="wzFilename">対象のファイル。</param>
    /// <param name="pguidVendor">希望するベンダー（指定しない場合は 0）。</param>
    /// <param name="dwDesiredAccess">アクセス権。</param>
    /// <param name="metadataOptions">メタデータの扱い。</param>
    /// <param name="ppIDecoder">デコーダー。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateDecoderFromFilename(
        [MarshalAs(UnmanagedType.LPWStr)] string wzFilename, IntPtr pguidVendor, uint dwDesiredAccess,
        int metadataOptions, out IWICBitmapDecoder? ppIDecoder);

    /// <summary>ストリームからデコーダーを作る。</summary>
    /// <param name="pIStream">対象のストリーム。</param>
    /// <param name="pguidVendor">希望するベンダー（指定しない場合は 0）。</param>
    /// <param name="metadataOptions">メタデータの扱い。</param>
    /// <param name="ppIDecoder">デコーダー。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateDecoderFromStream(
        IStream? pIStream, IntPtr pguidVendor, int metadataOptions, out IWICBitmapDecoder? ppIDecoder);

    /// <summary>ファイル ハンドルからデコーダーを作る。</summary>
    /// <param name="hFile">対象のファイル ハンドル。</param>
    /// <param name="pguidVendor">希望するベンダー。</param>
    /// <param name="metadataOptions">メタデータの扱い。</param>
    /// <param name="ppIDecoder">デコーダー。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateDecoderFromFileHandle(
        IntPtr hFile, IntPtr pguidVendor, int metadataOptions, out IWICBitmapDecoder? ppIDecoder);

    /// <summary>コンポーネントの情報を作る。</summary>
    /// <param name="clsidComponent">対象のクラス。</param>
    /// <param name="ppIInfo">情報。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateComponentInfo(in Guid clsidComponent, out IntPtr ppIInfo);

    /// <summary>形式からデコーダーを作る。</summary>
    /// <param name="guidContainerFormat">コンテナ形式。</param>
    /// <param name="pguidVendor">希望するベンダー。</param>
    /// <param name="ppIDecoder">デコーダー。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateDecoder(in Guid guidContainerFormat, IntPtr pguidVendor, out IWICBitmapDecoder? ppIDecoder);

    /// <summary>形式からエンコーダーを作る。</summary>
    /// <param name="guidContainerFormat">コンテナ形式。</param>
    /// <param name="pguidVendor">希望するベンダー。</param>
    /// <param name="ppIEncoder">エンコーダー。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateEncoder(in Guid guidContainerFormat, IntPtr pguidVendor, out IWICBitmapEncoder? ppIEncoder);

    /// <summary>パレットを作る。</summary>
    /// <param name="ppIPalette">パレット。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreatePalette(out IWICPalette? ppIPalette);

    /// <summary>画素形式の変換器を作る。</summary>
    /// <param name="ppIFormatConverter">変換器。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateFormatConverter(out IWICFormatConverter? ppIFormatConverter);

    /// <summary>縮小・拡大を作る。</summary>
    /// <param name="ppIBitmapScaler">縮小・拡大。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateBitmapScaler(out IWICBitmapScaler? ppIBitmapScaler);

    /// <summary>切り出しを作る。</summary>
    /// <param name="ppIBitmapClipper">切り出し。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateBitmapClipper(out IntPtr ppIBitmapClipper);

    /// <summary>回転・反転を作る。</summary>
    /// <param name="ppIBitmapFlipRotator">回転・反転。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateBitmapFlipRotator(out IWICBitmapFlipRotator? ppIBitmapFlipRotator);

    /// <summary>ストリームを作る。</summary>
    /// <param name="ppIWICStream">ストリーム。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateStream(out IWICStream? ppIWICStream);
}
