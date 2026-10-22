using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary><c>IWICBitmapFrameEncode</c>（書き出すフレーム）。</summary>
[GeneratedComInterface]
[Guid("00000105-A8F2-4877-BA0A-FD2B6645FB94")]
internal unsafe partial interface IWICBitmapFrameEncode
{
    /// <summary>初期化する。</summary>
    /// <param name="pIEncoderOptions">エンコーダーが用意したオプション。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Initialize(IPropertyBag2? pIEncoderOptions);

    /// <summary>寸法を設定する。</summary>
    /// <param name="uiWidth">幅。</param>
    /// <param name="uiHeight">高さ。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int SetSize(uint uiWidth, uint uiHeight);

    /// <summary>解像度を設定する。</summary>
    /// <param name="dpiX">横の解像度。</param>
    /// <param name="dpiY">縦の解像度。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int SetResolution(double dpiX, double dpiY);

    /// <summary>画素形式を設定する（対応しない場合は最も近い形式へ書き換えられる）。</summary>
    /// <param name="pPixelFormat">設定する画素形式。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int SetPixelFormat(ref Guid pPixelFormat);

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

    /// <summary>画素を書き込む。</summary>
    /// <param name="lineCount">行数。</param>
    /// <param name="cbStride">行幅（バイト）。</param>
    /// <param name="cbBufferSize">書き込む大きさ（バイト）。</param>
    /// <param name="pbPixels">画素。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int WritePixels(uint lineCount, uint cbStride, uint cbBufferSize, byte* pbPixels);

    /// <summary>別の画素の源から書き込む。</summary>
    /// <param name="pIBitmapSource">画素の源。</param>
    /// <param name="prc">範囲（全体は null）。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int WriteSource(IWICBitmapSource? pIBitmapSource, void* prc);

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
