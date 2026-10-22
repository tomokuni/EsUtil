using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary><c>IWICBitmapSource</c>（画素の源）。</summary>
[GeneratedComInterface]
[Guid("00000120-A8F2-4877-BA0A-FD2B6645FB94")]
internal unsafe partial interface IWICBitmapSource
{
    /// <summary>寸法を得る。</summary>
    /// <param name="puiWidth">幅。</param>
    /// <param name="puiHeight">高さ。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetSize(out uint puiWidth, out uint puiHeight);

    /// <summary>画素形式を得る。</summary>
    /// <param name="pPixelFormat">画素形式。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetPixelFormat(out Guid pPixelFormat);

    /// <summary>解像度を得る。</summary>
    /// <param name="pDpiX">横の解像度。</param>
    /// <param name="pDpiY">縦の解像度。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetResolution(out double pDpiX, out double pDpiY);

    /// <summary>パレットを複製する。</summary>
    /// <param name="pIPalette">複製先。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CopyPalette(IWICPalette? pIPalette);

    /// <summary>画素をコピーする。</summary>
    /// <param name="prc">範囲（全体は null）。</param>
    /// <param name="cbStride">行幅（バイト）。</param>
    /// <param name="cbBufferSize">コピー先の大きさ（バイト）。</param>
    /// <param name="pbBuffer">コピー先。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CopyPixels(void* prc, uint cbStride, uint cbBufferSize, byte* pbBuffer);
}
