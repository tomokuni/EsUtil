using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary>WIC の呼び出しに必要な COM の生成の宣言。</summary>
/// <remarks>
/// <b><c>[GeneratedComInterface]</c> の型を <c>out</c> に取る理由</b>: CsWin32 が生成する <c>CoCreateInstance</c> は
/// <c>out object</c>（クラシックな COM 相互運用。 RCW を作る経路）であり、Native AOT では使えない。
/// 一方 <b><c>[GeneratedComInterface]</c> の型を <c>[LibraryImport]</c> の引数にすると、ソース生成された
/// <c>ComInterfaceMarshaller&lt;T&gt;</c>（<c>ComWrappers</c> ベース）が変換を担う</b>ため AOT で動く（実測 2026-10-21:
/// JIT と Native AOT の両方で <c>CoCreateInstance</c> / <c>CreateStreamOnHGlobal</c> が成功し、返った型をそのまま呼べた）。<br/>
/// <b>生ポインターを扱わない</b>: 戻り値は本ライブラリのインターフェイス型であり、
/// <c>StrategyBasedComWrappers.GetOrCreateObjectForComInstance</c> の呼び出しも手動キャストも要らない。<br/>
/// <b><c>[GeneratedComInterface]</c> を関数へは付けられない</b>: 本ファイルの 2 件は vtable を持たない
/// エクスポートされた C 関数であり、属性は<b>インターフェイス宣言でしか有効でない</b>（実測 2026-10-22:
/// <c>CS0592</c>）。 属性の対象は <c>out</c> が指す先の型（<see cref="IWICImagingFactory"/> / <see cref="IStream"/>）であり、
/// そこには既に付けてある。<br/>
/// <b>解放</b>: 生成されたラッパーはファイナライズで参照を 1 つ戻す（Native AOT では
/// <c>Marshal.ReleaseComObject</c> が使えないため、GC に任せる方針そのものは変えない）。
/// </remarks>
internal static partial class WinWicNative
{
    /// <summary>COM のオブジェクトを生成する（ole32.CoCreateInstance）。</summary>
    /// <param name="clsid">生成するクラスの CLSID。</param>
    /// <param name="outer">アウター（集約しない場合は 0）。</param>
    /// <param name="context">生成の文脈（<c>CLSCTX_INPROC_SERVER</c> は 1）。</param>
    /// <param name="iid">取得するインターフェイスの IID。</param>
    /// <param name="instance">生成したオブジェクト。</param>
    /// <returns>HRESULT。</returns>
    [LibraryImport("ole32.dll")]
    internal static partial int CoCreateInstance(
        in Guid clsid, IntPtr outer, uint context, in Guid iid, out IWICImagingFactory instance);

    /// <summary>メモリを裏付けとしたストリームを作る（ole32.CreateStreamOnHGlobal）。</summary>
    /// <param name="memory">裏付けるメモリ（0 の場合は確保から行う）。</param>
    /// <param name="deleteOnRelease">解放時にメモリも解放する場合は 0 以外。</param>
    /// <param name="stream">生成したストリーム。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>
    /// <b>メモリ ストリームを使う理由</b>: COM の解放は GC の時期に依存するため、WIC にファイルを開かせると
    /// <b>解放までファイルが開いたまま</b>になる（実測: 保存直後の削除が <c>IOException</c> になった）。
    /// </remarks>
    [LibraryImport("ole32.dll")]
    internal static partial int CreateStreamOnHGlobal(IntPtr memory, int deleteOnRelease, out IStream stream);
}
