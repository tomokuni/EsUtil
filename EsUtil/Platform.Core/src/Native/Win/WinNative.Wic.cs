using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>WIC（Windows Imaging Component）の P/Invoke 宣言・GUID・COM の初期化を集約する。</summary>
/// <remarks>
/// <b>COM を <c>[GeneratedComInterface]</c> で呼ぶ理由</b>: Native AOT では <c>[ComImport]</c> による相互運用が
/// 使えない（実測: <c>NotSupportedException: COM Interop requires ComWrapper instance registered for marshalling.</c>）。
/// <c>[GeneratedComInterface]</c> はソース生成で <c>StrategyBasedComWrappers</c> 用の実装を作るため AOT でも動く
/// （定義は <see cref="WinWicInterfaces"/>、生成と呼び出しは <see cref="WinWicFactory"/> が担う）。<br/>
/// <b>COM の初期化</b>: 呼び出し側は <see cref="EnsureInitialized"/> を 1 回呼ぶ。
/// 既に別のモデルで初期化済みのプロセス（<c>RPC_E_CHANGED_MODE</c>）でも続行する。
/// </remarks>
internal static partial class WinNative
{
    /// <summary>WIC のイメージング ファクトリの CLSID。</summary>
    internal static readonly Guid WicImagingFactoryClsid = new("CACAF262-9370-4615-A13B-9F5539DA4C0A");

    /// <summary>WIC のイメージング ファクトリの IID。</summary>
    internal static readonly Guid WicImagingFactoryIid = new("EC5EC8A9-C395-4314-9C77-54D7A935FF70");

    /// <summary>32bpp の BGR（アルファなし）。</summary>
    internal static readonly Guid WicPixelFormat32bppBgr = new("6FDDC324-4E03-4BFE-B185-3D77768DC90E");

    /// <summary>32bpp の BGRA（アルファ乗算なし）。</summary>
    internal static readonly Guid WicPixelFormat32bppBgra = new("6FDDC324-4E03-4BFE-B185-3D77768DC90F");

    /// <summary>32bpp の BGRA（アルファ乗算済み）。</summary>
    internal static readonly Guid WicPixelFormat32bppPBgra = new("6FDDC324-4E03-4BFE-B185-3D77768DC910");

    /// <summary>24bpp の BGR（アルファを持てない形式向け）。</summary>
    internal static readonly Guid WicPixelFormat24bppBgr = new("6FDDC324-4E03-4BFE-B185-3D77768DC90C");

    /// <summary>8bpp のインデックス カラー（パレット）。</summary>
    internal static readonly Guid WicPixelFormat8bppIndexed = new("6FDDC324-4E03-4BFE-B185-3D77768DC904");

    /// <summary>COM を初期化済みかどうか。</summary>
    private static bool s_comInitialized;

    /// <summary>HRESULT が成功したかどうかを判定する。</summary>
    /// <param name="hr">HRESULT。</param>
    /// <returns>成功（0 以上）の場合は true。</returns>
    internal static bool Succeeded(int hr) => hr >= 0;

    /// <summary>COM を初期化する（1 回だけ呼ぶ）。</summary>
    /// <returns>初期化できた場合は true。 既に別のモデルで初期化済みの場合も true。</returns>
    /// <remarks>
    /// <c>RPC_E_CHANGED_MODE</c>（0x80010106）は「既に初期化済み」を表すため、成功として扱う
    /// （WIC は呼び出し側のアパートメントに依存しない。 実測で問題なく動作した）。
    /// </remarks>
    internal static bool EnsureInitialized()
    {
        if (s_comInitialized)
        {
            return true;
        }

        const int ChangedMode = unchecked((int)0x80010106);
        var hr = CoInitializeEx(IntPtr.Zero, 0x2 /* COINIT_APARTMENTTHREADED */);
        if (hr == ChangedMode)
        {
            s_comInitialized = true;
            return true;
        }

        if (!Succeeded(hr))
        {
            return false;
        }

        s_comInitialized = true;
        return true;
    }

    /// <summary>COM を初期化する（Win32）。</summary>
    /// <param name="reserved">予約（0）。</param>
    /// <param name="model">同時実行モデル。</param>
    /// <returns>HRESULT。</returns>
    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(IntPtr reserved, uint model);

    /// <summary>COM のオブジェクトを生成する（Win32）。</summary>
    /// <param name="clsid">生成するクラスの CLSID。</param>
    /// <param name="outer">アウター（集約しない場合は 0）。</param>
    /// <param name="context">生成の文脈。</param>
    /// <param name="iid">取得するインターフェイスの IID。</param>
    /// <param name="instance">生成したオブジェクト。</param>
    /// <returns>HRESULT。</returns>
    [LibraryImport("ole32.dll")]
    internal static partial int CoCreateInstance(
        in Guid clsid, IntPtr outer, uint context, in Guid iid, out IntPtr instance);

    /// <summary>メモリを裏付けとしたストリームを作る（Win32）。</summary>
    /// <param name="memory">裏付けるメモリ（0 の場合は確保から行う）。</param>
    /// <param name="deleteOnRelease">解放時にメモリも解放する場合は 0 以外。</param>
    /// <param name="stream">生成したストリーム。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>
    /// <b>メモリ ストリームを使う理由</b>: COM の解放は GC の時期に依存するため、WIC にファイルを開かせると
    /// <b>解放までファイルが開いたまま</b>になる（実測: 保存直後の削除が <c>IOException</c> になった）。
    /// メモリ ストリームならファイルは <see cref="File"/> が開いて閉じるため、直後から削除も上書きもできる。
    /// </remarks>
    [LibraryImport("ole32.dll")]
    internal static partial int CreateStreamOnHGlobal(IntPtr memory, int deleteOnRelease, out IntPtr stream);
}
