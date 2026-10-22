using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary><c>IWICFormatConverter</c>（画素形式の変換器）。</summary>
[GeneratedComInterface]
[Guid("00000301-A8F2-4877-BA0A-FD2B6645FB94")]
internal unsafe partial interface IWICFormatConverter : IWICBitmapSource
{
    /// <summary>変換を設定する。</summary>
    /// <param name="pISource">変換元。</param>
    /// <param name="dstFormat">変換先の画素形式。</param>
    /// <param name="dither">ディザ（なしは 0）。</param>
    /// <param name="pIPalette">パレット（不要は 0）。</param>
    /// <param name="alphaThresholdPercent">アルファの閾値（0 は既定）。</param>
    /// <param name="paletteTranslate">パレットの変換（既定は 0）。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Initialize(
        IWICBitmapSource pISource, in Guid dstFormat, int dither, IWICPalette? pIPalette,
        double alphaThresholdPercent, int paletteTranslate);

    /// <summary>変換できるかを調べる。</summary>
    /// <param name="srcPixelFormat">変換元の画素形式。</param>
    /// <param name="dstPixelFormat">変換先の画素形式。</param>
    /// <param name="pfCanConvert">変換できる場合は 0 以外。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CanConvert(in Guid srcPixelFormat, in Guid dstPixelFormat, out int pfCanConvert);
}
