using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>WIC の呼び出しに必要な、生ポインターを扱う COM の宣言。</summary>
/// <remarks>
/// <b>手書きを残す理由</b>: CsWin32 が生成する <c>CoCreateInstance</c> は <c>out object</c>（クラシックな
/// COM 相互運用のマーシャリング）であり、Native AOT では使えない。 本ライブラリは生ポインターを受け取り、
/// <see cref="WinWicFactory.Wrappers"/>（<c>StrategyBasedComWrappers</c>）で包む。<br/>
/// <b><c>CreateStreamOnHGlobal</c> を手書きする理由</b>: 生成される宣言は Windows SDK の投影型
/// （<c>Windows.Win32.System.Com.IStream</c>）を返すが、本ライブラリの <c>IStream</c> は
/// <c>[GeneratedComInterface]</c> で自前の宣言順を持つ型である。 生ポインターから本ライブラリの型へ
/// 直接キャストする方が、変換の段を増やさない。
/// </remarks>
internal static partial class WinWicNative
{
    /// <summary>COM のオブジェクトを生成する（ole32.CoCreateInstance）。</summary>
    /// <param name="clsid">生成するクラスの CLSID。</param>
    /// <param name="outer">アウター（集約しない場合は 0）。</param>
    /// <param name="context">生成の文脈（<c>CLSCTX_INPROC_SERVER</c> 等）。</param>
    /// <param name="iid">取得するインターフェイスの IID。</param>
    /// <param name="instance">生成したオブジェクト。</param>
    /// <returns>HRESULT。</returns>
    [LibraryImport("ole32.dll")]
    internal static partial int CoCreateInstance(
        in Guid clsid, IntPtr outer, uint context, in Guid iid, out IntPtr instance);

    /// <summary>メモリを裏付けとしたストリームを作る（ole32.CreateStreamOnHGlobal）。</summary>
    /// <param name="memory">裏付けるメモリ（0 の場合は確保から行う）。</param>
    /// <param name="deleteOnRelease">解放時にメモリも解放する場合は 0 以外。</param>
    /// <param name="stream">生成したストリーム。</param>
    /// <returns>HRESULT。</returns>
    [LibraryImport("ole32.dll")]
    internal static partial int CreateStreamOnHGlobal(IntPtr memory, int deleteOnRelease, out IntPtr stream);
}
