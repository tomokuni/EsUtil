using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary><c>IWICStream</c>（WIC のストリーム）。</summary>
[GeneratedComInterface]
[Guid("135FF860-22B7-4DDF-B0F6-218F4F299A43")]
internal unsafe partial interface IWICStream : IStream
{
    /// <summary>既存のストリームから初期化する。</summary>
    /// <param name="pIStream">元のストリーム。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int InitializeFromIStream(IStream pIStream);

    /// <summary>ファイルへ結び付ける。</summary>
    /// <param name="wzFileName">対象のファイル。</param>
    /// <param name="dwDesiredAccess">アクセス権。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int InitializeFromFilename([MarshalAs(UnmanagedType.LPWStr)] string wzFileName, uint dwDesiredAccess);

    /// <summary>メモリへ結び付ける。</summary>
    /// <param name="pbBuffer">領域（呼び出し側が所有する）。</param>
    /// <param name="cbBufferSize">領域の大きさ（バイト）。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int InitializeFromMemory(void* pbBuffer, uint cbBufferSize);

    /// <summary>既存のストリームの範囲へ結び付ける。</summary>
    /// <param name="pIStream">元のストリーム。</param>
    /// <param name="ulOffset">開始位置。</param>
    /// <param name="ulMaxSize">大きさ（バイト）。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int InitializeFromIStreamRegion(IStream pIStream, ulong ulOffset, ulong ulMaxSize);
}
