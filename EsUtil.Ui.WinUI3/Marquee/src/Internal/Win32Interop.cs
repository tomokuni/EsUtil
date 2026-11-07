namespace EsUtil.Ui.WinUI3.Marquee.Internal;

/// <summary>XAML のルートからウィンドウ（<c>HWND</c>）を解決する WinRT の入口。</summary>
/// <remarks>
/// <b>位置付け</b>: 画面物理座標とクライアント座標の変換はコアの抽象（<see cref="EsUtil.Platform.Core.Native.IWindowService"/>）が
/// 提供する。 本クラスが持つのは、XAML のルートが属するウィンドウのハンドルを解決する処理だけである
/// （<c>XamlRoot.ContentIslandEnvironment.AppWindowId</c> → <c>HWND</c>）。<br/>
/// <b>P/Invoke を持たない</b>: WinRT の <see cref="Microsoft.UI.Win32Interop"/> だけを使う（本 UI 層は Win32 を直接呼ばない）。<br/>
/// <b>呼び出し</b>: ウィンドウの生成後（XAML のルートが確定した後）に、UI スレッドから呼ぶ。
/// </remarks>
internal static class Win32Interop
{
    /// <summary>ウィンドウ ハンドルを解決する。</summary>
    /// <param name="windowId">XAML のルートが属するウィンドウの ID。</param>
    /// <param name="windowHandle">解決したウィンドウ ハンドル。 解決できない場合は <see cref="IntPtr.Zero"/>。</param>
    /// <returns>解決できた場合は true。</returns>
    /// <remarks>
    /// XAML のルート（<c>XamlRoot.ContentIslandEnvironment.AppWindowId</c>）から得た ID を使うため、<br/>
    /// ホストへウィンドウを渡してもらう必要がない（コントロールだけで現在のウィンドウを特定できる）。
    /// </remarks>
    public static bool TryGetWindowHandle(Microsoft.UI.WindowId windowId, out IntPtr windowHandle)
    {
        windowHandle = Microsoft.UI.Win32Interop.GetWindowFromWindowId(windowId);
        return windowHandle != IntPtr.Zero;
    }
}
