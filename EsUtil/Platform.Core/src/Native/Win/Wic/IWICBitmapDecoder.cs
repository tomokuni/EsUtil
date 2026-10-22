using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary><c>IWICBitmapDecoder</c>（デコーダー）。</summary>
[GeneratedComInterface]
[Guid("9EDDE9E7-8DEE-47EA-99DF-E6FAF2ED44BF")]
internal unsafe partial interface IWICBitmapDecoder
{
    /// <summary>扱えるかを調べる。</summary>
    /// <param name="pIStream">対象のストリーム。</param>
    /// <param name="pdwCapability">扱える範囲。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int QueryCapability(IStream? pIStream, out uint pdwCapability);

    /// <summary>初期化する。</summary>
    /// <param name="pIStream">対象のストリーム。</param>
    /// <param name="cacheOptions">キャッシュの方法。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Initialize(IStream? pIStream, int cacheOptions);

    /// <summary>コンテナ形式を得る。</summary>
    /// <param name="pguidContainerFormat">コンテナ形式。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetContainerFormat(out Guid pguidContainerFormat);

    /// <summary>デコーダーの情報を得る。</summary>
    /// <param name="ppIDecoderInfo">情報。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetDecoderInfo(out IntPtr ppIDecoderInfo);

    /// <summary>パレットを複製する。</summary>
    /// <param name="pIPalette">複製先。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CopyPalette(IWICPalette? pIPalette);

    /// <summary>メタデータの読み取り器を得る。</summary>
    /// <param name="ppIMetadataQueryReader">読み取り器。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetMetadataQueryReader(out IntPtr ppIMetadataQueryReader);

    /// <summary>プレビューを得る。</summary>
    /// <param name="ppIBitmapSource">プレビュー。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetPreview(out IWICBitmapSource? ppIBitmapSource);

    /// <summary>色の文脈を得る。</summary>
    /// <param name="cCount">受け取る件数。</param>
    /// <param name="ppIColorContexts">受け取る領域。</param>
    /// <param name="pcActualCount">実際の件数。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetColorContexts(uint cCount, IntPtr ppIColorContexts, out uint pcActualCount);

    /// <summary>縮小版を得る。</summary>
    /// <param name="ppIThumbnail">縮小版。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetThumbnail(out IWICBitmapSource? ppIThumbnail);

    /// <summary>フレームの数を得る。</summary>
    /// <param name="pCount">フレームの数。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetFrameCount(out uint pCount);

    /// <summary>フレームを得る。</summary>
    /// <param name="index">フレームの番号（先頭は 0）。</param>
    /// <param name="ppIBitmapFrame">フレーム。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetFrame(uint index, out IWICBitmapFrameDecode? ppIBitmapFrame);
}
