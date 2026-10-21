using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary><c>IWICBitmapEncoder</c>（エンコーダー）。</summary>
[GeneratedComInterface]
[Guid("00000103-A8F2-4877-BA0A-FD2B6645FB94")]
internal unsafe partial interface IWICBitmapEncoder
{
    /// <summary>初期化する。</summary>
    /// <param name="pIStream">書き込み先のストリーム。</param>
    /// <param name="cacheOption">キャッシュの方法。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Initialize(IStream? pIStream, int cacheOption);

    /// <summary>コンテナ形式を得る。</summary>
    /// <param name="pguidContainerFormat">コンテナ形式。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetContainerFormat(out Guid pguidContainerFormat);

    /// <summary>エンコーダーの情報を得る。</summary>
    /// <param name="ppIEncoderInfo">情報。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetEncoderInfo(out IntPtr ppIEncoderInfo);

    /// <summary>色の文脈を設定する。</summary>
    /// <param name="cCount">件数。</param>
    /// <param name="ppIColorContext">色の文脈。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int SetColorContexts(uint cCount, IntPtr ppIColorContext);

    /// <summary>パレットを設定する。</summary>
    /// <param name="pIPalette">パレット。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int SetPalette(IWICPalette? pIPalette);

    /// <summary>縮小版を設定する。</summary>
    /// <param name="pIThumbnail">縮小版。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int SetThumbnail(IWICBitmapSource? pIThumbnail);

    /// <summary>プレビューを設定する。</summary>
    /// <param name="pIPreview">プレビュー。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int SetPreview(IWICBitmapSource? pIPreview);

    /// <summary>フレームを作る。</summary>
    /// <param name="ppIFrameEncode">フレーム。</param>
    /// <param name="ppIEncoderOptions">エンコーダーが用意したオプション。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CreateNewFrame(out IWICBitmapFrameEncode? ppIFrameEncode, out IPropertyBag2? ppIEncoderOptions);

    /// <summary>確定する。</summary>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Commit();

    /// <summary>メタデータの書き込み器を得る。</summary>
    /// <param name="ppIMetadataQueryWriter">書き込み器。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetMetadataQueryWriter(out IntPtr ppIMetadataQueryWriter);
}
