using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary><c>IWICPalette</c>（パレット）。</summary>
[GeneratedComInterface]
[Guid("00000040-A8F2-4877-BA0A-FD2B6645FB94")]
internal unsafe partial interface IWICPalette
{
    /// <summary>定義済みのパレットで初期化する。</summary>
    /// <param name="ePaletteType">パレットの種類。</param>
    /// <param name="fAddTransparentColor">透明な色を加える場合は 0 以外。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int InitializePredefined(int ePaletteType, int fAddTransparentColor);

    /// <summary>色の並びで初期化する。</summary>
    /// <param name="pColors">色（<c>0xAARRGGBB</c> の並び）。</param>
    /// <param name="cCount">色数。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int InitializeCustom(uint* pColors, uint cCount);

    /// <summary>画像から初期化する。</summary>
    /// <param name="pISurface">対象の画素の源。</param>
    /// <param name="cCount">色数。</param>
    /// <param name="fAddTransparentColor">透明な色を加える場合は 0 以外。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int InitializeFromBitmap(IWICBitmapSource? pISurface, uint cCount, int fAddTransparentColor);

    /// <summary>別のパレットから初期化する。</summary>
    /// <param name="pIPalette">元のパレット。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int InitializeFromPalette(IWICPalette? pIPalette);

    /// <summary>種類を得る。</summary>
    /// <param name="pePaletteType">パレットの種類。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetType(out int pePaletteType);

    /// <summary>色数を得る。</summary>
    /// <param name="pcCount">色数。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetColorCount(out uint pcCount);

    /// <summary>色を得る。</summary>
    /// <param name="cCount">受け取る件数。</param>
    /// <param name="pColors">受け取る領域。</param>
    /// <param name="pcActualColors">実際の件数。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetColors(uint cCount, uint* pColors, out uint pcActualColors);

    /// <summary>白黒かを調べる。</summary>
    /// <param name="pfIsBlackWhite">白黒の場合は 0 以外。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int IsBlackWhite(out int pfIsBlackWhite);

    /// <summary>濃淡かを調べる。</summary>
    /// <param name="pfIsGrayscale">濃淡の場合は 0 以外。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int IsGrayscale(out int pfIsGrayscale);

    /// <summary>アルファを持つかを調べる。</summary>
    /// <param name="pfHasAlpha">アルファを持つ場合は 0 以外。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int HasAlpha(out int pfHasAlpha);
}
