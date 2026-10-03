namespace EsUtil.Platform.Core;

/// <summary>UI フレームワーク非依存のネイティブウィンドウ識別子を表す値オブジェクト。</summary>
/// <remarks>
/// 各 UI フレームワークのネイティブハンドル（Avalonia の <c>IPlatformHandle</c> 等）を、
/// UI 非依存の値として受け渡すために使用する。<br/>
/// <see cref="Handle"/> は Win32 では HWND、X11 では Window ID 等のネイティブ値であり、<br/>
/// UI フレームワークのオブジェクト（Avalonia の <c>TopLevel</c> 等）には触れない。<br/>
/// 各 UI 実装のハンドル型との変換は Interop 層が提供する。<br/>
/// </remarks>
/// <param name="Handle">ネイティブハンドル値（Win32: HWND / X11: Window ID）。 未取得時は 0。</param>
/// <param name="Descriptor">ハンドルの種別を表す説明（例: "HWND"）。 不明な場合は null。</param>
public readonly record struct EsPlatformHandle(nint Handle, string? Descriptor = null)
{
    /// <summary>有効なハンドルを保持しているかどうかを取得する。</summary>
    /// <value>ハンドル値が 0 以外の場合は <see langword="true"/>。</value>
    public bool IsValid => Handle != 0;
}
