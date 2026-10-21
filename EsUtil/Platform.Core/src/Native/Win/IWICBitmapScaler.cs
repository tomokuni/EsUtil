using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary><c>IWICBitmapScaler</c>（縮小・拡大）。</summary>
[GeneratedComInterface]
[Guid("00000302-A8F2-4877-BA0A-FD2B6645FB94")]
internal unsafe partial interface IWICBitmapScaler : IWICBitmapSource
{
    /// <summary>縮小・拡大を設定する。</summary>
    /// <param name="pISource">変換元。</param>
    /// <param name="uiWidth">目標の幅。</param>
    /// <param name="uiHeight">目標の高さ。</param>
    /// <param name="mode">補間の方法。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Initialize(IWICBitmapSource pISource, uint uiWidth, uint uiHeight, int mode);
}
