using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

// 本ファイルは kernel32.dll の宣言（プロセス ID）を集約する。 型の説明は WinNative.cs を参照する。
internal static partial class WinNative
{

    // ============================================================
    // kernel32.dll
    // ============================================================

    /// <summary>現在のプロセス ID を取得する（kernel32.GetCurrentProcessId）。</summary>
    [LibraryImport("kernel32.dll")]
    internal static partial uint GetCurrentProcessId();
}
