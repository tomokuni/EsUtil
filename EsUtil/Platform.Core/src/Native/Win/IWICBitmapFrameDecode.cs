using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary><c>IWICBitmapFrameDecode</c>（デコードしたフレーム）。</summary>
[GeneratedComInterface]
[Guid("3B16811B-6A43-4EC9-A813-3D930C13B940")]
internal unsafe partial interface IWICBitmapFrameDecode : IWICBitmapSource
{
    /// <summary>メタデータの読み取り器を得る。</summary>
    /// <param name="ppIMetadataQueryReader">読み取り器（無い場合は null）。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetMetadataQueryReader(out IWICMetadataQueryReader? ppIMetadataQueryReader);

    /// <summary>色の文脈を得る。</summary>
    /// <param name="cCount">受け取る件数。</param>
    /// <param name="ppIColorContexts">受け取る領域。</param>
    /// <param name="pcActualCount">実際の件数。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetColorContexts(uint cCount, IntPtr ppIColorContexts, out uint pcActualCount);

    /// <summary>縮小版を得る。</summary>
    /// <param name="ppIThumbnail">縮小版（無い場合は null）。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetThumbnail(out IWICBitmapSource? ppIThumbnail);
}
