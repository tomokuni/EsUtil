using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary><c>IPropertyBag2</c>（エンコーダーのオプション）。</summary>
[GeneratedComInterface]
[Guid("22F55882-280B-11D0-A8A9-00A0C90C2004")]
internal unsafe partial interface IPropertyBag2
{
    /// <summary>オプションを読む。</summary>
    /// <param name="cProperties">件数。</param>
    /// <param name="pPropBag">名前と型の指定。</param>
    /// <param name="pErrLog">エラーの記録先。</param>
    /// <param name="pvarValue">値。</param>
    /// <param name="phrError">項目ごとの結果。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Read(uint cProperties, void* pPropBag, IntPtr pErrLog, void* pvarValue, IntPtr phrError);

    /// <summary>オプションを書く。</summary>
    /// <param name="cProperties">件数。</param>
    /// <param name="pPropBag">名前と型の指定。</param>
    /// <param name="pvarValue">値。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int Write(uint cProperties, void* pPropBag, void* pvarValue);

    /// <summary>オプションの数を得る。</summary>
    /// <param name="pcProperties">オプションの数。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int CountProperties(out uint pcProperties);

    /// <summary>オプションの情報を得る。</summary>
    /// <param name="iProperty">項目の番号。</param>
    /// <param name="cProperties">件数。</param>
    /// <param name="pPropBag">受け取る領域。</param>
    /// <param name="pcProperties">実際の件数。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int GetPropertyInfo(uint iProperty, uint cProperties, void* pPropBag, out uint pcProperties);

    /// <summary>オブジェクトを読み込む。</summary>
    /// <param name="pstrName">オプションの名前。</param>
    /// <param name="dwHint">ヒント。</param>
    /// <param name="pUnkObject">対象のオブジェクト。</param>
    /// <param name="pErrLog">エラーの記録先。</param>
    /// <returns>HRESULT。</returns>
    [PreserveSig]
    int LoadObject([MarshalAs(UnmanagedType.LPWStr)] string pstrName, uint dwHint, IntPtr pUnkObject, IntPtr pErrLog);
}
