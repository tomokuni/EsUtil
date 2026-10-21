using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>ウィンドウの外寸とクライアント領域の差（枠の太さ）を実測する（Windows 実装）。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: 最小サイズ・既定サイズは<b>クライアント領域</b>の DIP で決めるが、UI フレームワークが
/// 受け付けるのは<b>外寸</b>である。 枠の太さは環境（テーマ・DPI・OS のバージョン）で変わるため定数にできず、
/// 実際のウィンドウから測る必要がある。<br/>
/// <b>UI 版ごとに書かない理由</b>: 同じ計測を各アプリ・各 UI 版へ複製すると、片方だけ直したときに最小サイズが
/// 食い違う（依頼「ウィンドウの最小サイズが UI ごとに異なる」の原因になる）。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>。 宣言を手で書かない）。<br/>
/// <b>失敗時</b>: 測れない場合は <see cref="Win32Exception"/> を投げる（枠を無視した寸法でウィンドウを作ると
/// 最小サイズが破られるため、呼び出し側が気付けるようにする）。<br/>
/// <b>呼ぶ時機</b>: ウィンドウのハンドルが確定した後（表示前でも測れる）。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public static class WinWindowFrame
{
    /// <summary>ウィンドウの外寸とクライアント領域の差（枠の太さ）を取得する。</summary>
    /// <param name="handle">対象ウィンドウのネイティブ ハンドル。</param>
    /// <returns>枠の幅と高さ（画面物理ピクセル）。</returns>
    /// <exception cref="Win32Exception">外寸またはクライアント領域を取得できなかった場合。</exception>
    public static (int Width, int Height) GetSize(EsPlatformHandle handle)
    {
        var hwnd = (HWND)handle.Handle;
        if (!PInvoke.GetWindowRect(hwnd, out var windowRect))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "ウィンドウの外寸を取得できません。");
        }

        if (!PInvoke.GetClientRect(hwnd, out var clientRect))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "ウィンドウのクライアント領域を取得できません。");
        }

        return (
            (windowRect.right - windowRect.left) - (clientRect.right - clientRect.left),
            (windowRect.bottom - windowRect.top) - (clientRect.bottom - clientRect.top));
    }
}
