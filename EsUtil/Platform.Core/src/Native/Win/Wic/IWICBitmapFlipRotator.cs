using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary><c>IWICBitmapFlipRotator</c>（回転・反転）。</summary>
[GeneratedComInterface]
[Guid("5009834F-2D6A-41CE-9E1B-17C5AFF7A782")]
internal unsafe partial interface IWICBitmapFlipRotator : IWICBitmapSource
{
    /// <summary>回転・反転を設定する。</summary>
    /// <param name="pISource">変換元。</param>
    /// <param name="options">変換の指定（<c>WICBitmapTransformOptions</c>）。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Initialize(IWICBitmapSource pISource, int options);
}
