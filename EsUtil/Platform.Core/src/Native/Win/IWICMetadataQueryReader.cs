using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary><c>IWICMetadataQueryReader</c>（メタデータの読み取り器）。</summary>
[GeneratedComInterface]
[Guid("30989668-E1C9-4597-B395-458EEDB808DF")]
internal unsafe partial interface IWICMetadataQueryReader
{
    /// <summary>コンテナ形式を得る。</summary>
    /// <param name="pguidContainerFormat">コンテナ形式。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetContainerFormat(out Guid pguidContainerFormat);

    /// <summary>名前空間を得る。</summary>
    /// <param name="cchMaxLength">受け取る長さ。</param>
    /// <param name="wzNamespace">受け取る領域。</param>
    /// <param name="pcchActualLength">実際の長さ。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetLocation(uint cchMaxLength, IntPtr wzNamespace, out uint pcchActualLength);

    /// <summary>名前で値を得る。</summary>
    /// <param name="wzName">メタデータの名前。</param>
    /// <param name="pvarValue">値を受け取る領域。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetMetadataByName([MarshalAs(UnmanagedType.LPWStr)] string wzName, WinPropVariant* pvarValue);

    /// <summary>名前の一覧を得る。</summary>
    /// <param name="ppIEnumString">一覧。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetEnumerator(out IntPtr ppIEnumString);
}
