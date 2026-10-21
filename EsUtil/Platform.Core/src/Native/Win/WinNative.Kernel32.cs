namespace EsUtil.Platform.Core.Native.Win;

// 本ファイルは kernel32.dll の宣言（プロセス ID）を集約する。 型の説明は WinNative.cs を参照する。
internal static partial class WinNative
{

    // ============================================================
    // kernel32.dll
    // ============================================================

    /// <summary>現在のプロセス ID を取得する（kernel32.GetCurrentProcessId）。</summary>
    /// <returns>現在のプロセス ID。</returns>
    /// <remarks>
    /// <b>宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c> に API 名を書くと
    /// <c>PInvoke.&lt;API&gt;</c> が internal に生成される）。 手書きの宣言を増やさない。<br/>
    /// <b><c>CA1416</c> を抑止する理由</b>: 生成された宣言は「Windows 5.1 以降」と注記されるが、
    /// 本ライブラリの対応 OS は Windows 10 以降である（<c>Native/</c> は Windows 専用）。
    /// 呼び出し側の注記を増やさないよう、ここで 1 回だけ抑止する。
    /// </remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static uint GetCurrentProcessId() => Windows.Win32.PInvoke.GetCurrentProcessId();
#pragma warning restore CA1416
}
