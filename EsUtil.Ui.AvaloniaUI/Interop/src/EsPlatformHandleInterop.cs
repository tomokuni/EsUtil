using Avalonia.Platform;
using EsUtil.Platform.Core.Window;
using EsUtil.Platform.Core;

namespace EsUtil.Ui.AvaloniaUI.Interop;

/// <summary>Avalonia のネイティブハンドルをコアの <see cref="EsPlatformHandle"/> へ変換する。</summary>
/// <remarks>
/// コアのサービス抽象（<c>IWindowService</c> 等）は UI フレームワーク非依存のハンドルを受け取るため、<br/>
/// UI 側で本変換を行ってから渡す。<br/>
/// </remarks>
public static class EsPlatformHandleInterop
{
    /// <summary>Avalonia のネイティブハンドルを <see cref="EsPlatformHandle"/> へ変換する。</summary>
    /// <param name="handle">変換元のハンドル。 null の場合は <see cref="EsPlatformHandle.Handle"/> = 0 の値。</param>
    /// <returns>コアのハンドル。</returns>
    public static EsPlatformHandle ToEsPlatformHandle(this IPlatformHandle? handle)
        => handle is null
            ? default
            : EsWindow.FromHandle(handle.Handle, handle.HandleDescriptor);

    /// <summary><see cref="EsPlatformHandle"/> を Avalonia のネイティブハンドルへ変換する。</summary>
    /// <param name="handle">変換元のハンドル。</param>
    /// <returns>Avalonia のネイティブハンドル。</returns>
    public static PlatformHandle ToPlatformHandle(this EsPlatformHandle handle)
        => new(handle.Handle, handle.Descriptor ?? string.Empty);
}
