using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary><c>IStream</c>（<c>IWICStream</c> の基底）。</summary>
/// <remarks>
/// <b>基底として宣言する理由</b>: <c>IWICStream</c> は <c>IStream</c> を継承しており、vtable の位置は
/// <c>IStream</c> の 11 件の後になる。<c>[GeneratedComInterface]</c> は基底インターフェイスを受け付けるため、
/// 位置を手計算せずに済む。<br/>
/// <b>使わないメソッドも宣言する</b>: vtable の位置を合わせるためである。
/// </remarks>
[GeneratedComInterface]
[Guid("0000000C-0000-0000-C000-000000000046")]
internal unsafe partial interface IStream
{
    /// <summary>読み込む。</summary>
    /// <param name="pv">読み込み先。</param>
    /// <param name="cb">読み込む大きさ（バイト）。</param>
    /// <param name="pcbRead">読み込んだ大きさ（バイト）。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Read(void* pv, uint cb, out uint pcbRead);

    /// <summary>書き込む。</summary>
    /// <param name="pv">書き込む内容。</param>
    /// <param name="cb">書き込む大きさ（バイト）。</param>
    /// <param name="pcbWritten">書き込んだ大きさ（バイト）。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Write(void* pv, uint cb, out uint pcbWritten);

    /// <summary>位置を移動する。</summary>
    /// <param name="dlibMove">移動量。</param>
    /// <param name="dwOrigin">基準（0 = 先頭）。</param>
    /// <param name="plibNewPosition">移動後の位置。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Seek(long dlibMove, uint dwOrigin, out ulong plibNewPosition);

    /// <summary>大きさを変える。</summary>
    /// <param name="libNewSize">新しい大きさ（バイト）。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int SetSize(ulong libNewSize);

    /// <summary>別のストリームへ複製する。</summary>
    /// <param name="pstm">複製先。</param>
    /// <param name="cb">複製する大きさ（バイト）。</param>
    /// <param name="pcbRead">読み込んだ大きさ（バイト）。</param>
    /// <param name="pcbWritten">書き込んだ大きさ（バイト）。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CopyTo(IStream pstm, ulong cb, out ulong pcbRead, out ulong pcbWritten);

    /// <summary>確定する。</summary>
    /// <param name="grfCommitFlags">確定の仕方。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Commit(uint grfCommitFlags);

    /// <summary>確定を取り消す。</summary>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Revert();

    /// <summary>範囲をロックする。</summary>
    /// <param name="libOffset">開始位置。</param>
    /// <param name="cb">大きさ（バイト）。</param>
    /// <param name="dwLockType">ロックの種類。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int LockRegion(ulong libOffset, ulong cb, uint dwLockType);

    /// <summary>範囲のロックを解除する。</summary>
    /// <param name="libOffset">開始位置。</param>
    /// <param name="cb">大きさ（バイト）。</param>
    /// <param name="dwLockType">ロックの種類。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int UnlockRegion(ulong libOffset, ulong cb, uint dwLockType);

    /// <summary>状態を得る。</summary>
    /// <param name="pstatstg">状態を受け取る領域。</param>
    /// <param name="grfStatFlag">取得する項目。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Stat(void* pstatstg, uint grfStatFlag);

    /// <summary>複製を作る。</summary>
    /// <param name="ppstm">複製したストリーム。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Clone(out IStream ppstm);
}
